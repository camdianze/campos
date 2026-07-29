using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PharmaPOS.Application.Licensing;

namespace PharmaPOS.Tools.LicenseIssuer;

/// <summary>
/// 라이선스 코드 발급 도구.
///
/// 개인키는 저장소가 아니라 %APPDATA%\PharmaPOS.Issuer 아래에 둔다.
/// 실수로 git에 커밋되거나 배포본에 딸려 들어가는 일을 막기 위해서다.
/// 이 파일을 잃어버리면 새 코드를 영영 발급할 수 없으니 반드시 백업해 둔다.
/// </summary>
public static class Program
{
    private static readonly string IssuerFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PharmaPOS.Issuer");

    private static string PrivateKeyPath => Path.Combine(IssuerFolder, "private.key");
    private static string LedgerPath => Path.Combine(IssuerFolder, "licenses.csv");

    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        try
        {
            return args[0].ToLowerInvariant() switch
            {
                "keygen" => RunKeygen(),
                "issue" => RunIssue(args),
                "list" => RunList(),
                _ => PrintUsageWithError($"Unknown command: {args[0]}")
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"오류: {ex.Message}");
            return 1;
        }
    }

    // ── keygen ────────────────────────────────────────────────────────────

    private static int RunKeygen()
    {
        if (File.Exists(PrivateKeyPath))
        {
            Console.Error.WriteLine($"이미 개인키가 있습니다: {PrivateKeyPath}");
            Console.Error.WriteLine("덮어쓰면 지금까지 발급한 코드가 전부 무효가 됩니다.");
            Console.Error.WriteLine("정말 새로 만들려면 위 파일을 직접 옮기거나 지운 뒤 다시 실행하세요.");
            return 1;
        }

        Directory.CreateDirectory(IssuerFolder);

        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        File.WriteAllBytes(PrivateKeyPath, ecdsa.ExportPkcs8PrivateKey());

        var publicKeyBase64 = Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo());

        Console.WriteLine("키 쌍을 만들었습니다.");
        Console.WriteLine();
        Console.WriteLine($"  개인키: {PrivateKeyPath}");
        Console.WriteLine("          이 파일을 백업하세요. 잃어버리면 새 코드를 발급할 수 없습니다.");
        Console.WriteLine("          절대 배포하거나 저장소에 커밋하지 마세요.");
        Console.WriteLine();
        Console.WriteLine("  공개키: 아래 값을 PharmaPOS.Application/Licensing/LicenseService.cs 의");
        Console.WriteLine("          PublicKeyBase64 상수에 붙여넣고 다시 게시하세요.");
        Console.WriteLine();
        Console.WriteLine(publicKeyBase64);

        return 0;
    }

    // ── issue ─────────────────────────────────────────────────────────────

    private static int RunIssue(string[] args)
    {
        if (args.Length < 2)
            return PrintUsageWithError("고객명을 입력하세요.");

        if (!File.Exists(PrivateKeyPath))
        {
            Console.Error.WriteLine("개인키가 없습니다. 먼저 keygen을 실행하세요.");
            return 1;
        }

        var customerName = args[1];

        // 만료일은 선택. 없으면 무기한.
        uint expiresAtUnixSeconds = 0;

        if (args.Length >= 3)
        {
            if (!DateTime.TryParseExact(args[2], "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var expiryDate))
            {
                return PrintUsageWithError($"만료일 형식이 잘못됐습니다: {args[2]} (yyyy-MM-dd)");
            }

            // 그 날 하루는 쓸 수 있도록 자정이 아니라 하루 끝으로 잡는다.
            var expiryMoment = new DateTimeOffset(expiryDate.Date.AddDays(1).AddSeconds(-1), TimeSpan.Zero);
            expiresAtUnixSeconds = (uint)expiryMoment.ToUnixTimeSeconds();
        }

        var serialNumber = GetNextSerialNumber();

        var payload = new LicensePayload
        {
            Version = LicensePayload.CurrentVersion,
            SerialNumber = serialNumber,
            IssuedAt = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ExpiresAt = expiresAtUnixSeconds
        };

        using var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(File.ReadAllBytes(PrivateKeyPath), out _);

        var signature = ecdsa.SignData(
            LicenseCodeCodec.GetSignableBytes(payload),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        var code = LicenseCodeCodec.Encode(payload, signature);

        AppendToLedger(serialNumber, customerName, payload, code);

        Console.WriteLine($"발급 번호 : {serialNumber}");
        Console.WriteLine($"고객      : {customerName}");
        Console.WriteLine($"만료      : {(payload.IsPerpetual ? "무기한" : DateTimeOffset.FromUnixTimeSeconds(payload.ExpiresAt).ToLocalTime().ToString("yyyy-MM-dd"))}");
        Console.WriteLine();
        Console.WriteLine("아래 코드를 고객에게 전달하세요.");
        Console.WriteLine();
        Console.WriteLine(code);
        Console.WriteLine();
        Console.WriteLine($"발급 대장: {LedgerPath}");

        return 0;
    }

    // ── list ──────────────────────────────────────────────────────────────

    private static int RunList()
    {
        if (!File.Exists(LedgerPath))
        {
            Console.WriteLine("아직 발급한 코드가 없습니다.");
            return 0;
        }

        foreach (var line in File.ReadAllLines(LedgerPath))
            Console.WriteLine(line);

        return 0;
    }

    // ── 발급 대장 ─────────────────────────────────────────────────────────

    private static uint GetNextSerialNumber()
    {
        if (!File.Exists(LedgerPath))
            return 1;

        // 헤더를 뺀 줄 수 + 1. 대장을 직접 편집하지 않는다는 전제다.
        var dataLineCount = File.ReadAllLines(LedgerPath).Length - 1;
        return (uint)Math.Max(dataLineCount, 0) + 1;
    }

    private static void AppendToLedger(uint serialNumber, string customerName, LicensePayload payload, string code)
    {
        Directory.CreateDirectory(IssuerFolder);

        if (!File.Exists(LedgerPath))
            File.WriteAllText(LedgerPath, "serial,customer,issued_at,expires_at,code\n", Encoding.UTF8);

        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(payload.IssuedAt).ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        var expiresAt = payload.IsPerpetual
            ? "perpetual"
            : DateTimeOffset.FromUnixTimeSeconds(payload.ExpiresAt).ToLocalTime().ToString("yyyy-MM-dd");

        // 고객명에 쉼표가 들어가도 깨지지 않게 큰따옴표로 감싼다.
        var escapedName = customerName.Replace("\"", "\"\"");

        File.AppendAllText(LedgerPath,
            $"{serialNumber},\"{escapedName}\",{issuedAt},{expiresAt},{code}\n", Encoding.UTF8);
    }

    // ── 사용법 ────────────────────────────────────────────────────────────

    private static int PrintUsageWithError(string message)
    {
        Console.Error.WriteLine(message);
        Console.Error.WriteLine();
        PrintUsage();
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("PharmaPOS 라이선스 발급 도구");
        Console.WriteLine();
        Console.WriteLine("  keygen                          키 쌍을 만든다 (최초 1회)");
        Console.WriteLine("  issue <고객명> [만료일]          코드를 발급한다 (만료일 없으면 무기한)");
        Console.WriteLine("  list                            발급 대장을 본다");
        Console.WriteLine();
        Console.WriteLine("예)");
        Console.WriteLine("  dotnet run --project tools/LicenseIssuer -- keygen");
        Console.WriteLine("  dotnet run --project tools/LicenseIssuer -- issue \"A약국\"");
        Console.WriteLine("  dotnet run --project tools/LicenseIssuer -- issue \"B약국\" 2027-12-31");
        Console.WriteLine("  dotnet run --project tools/LicenseIssuer -- list");
    }
}
