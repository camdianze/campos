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

    /// <summary>고객에게 건네줄 코드 파일을 모아두는 곳. 발급 1건당 파일 1개.</summary>
    private static string IssuedFolder => Path.Combine(IssuerFolder, "issued");

    /// <summary>클라우드 업로드 설정. 없으면 업로드는 꺼진 채로 동작한다.</summary>
    private static string CloudConfigPath => Path.Combine(IssuerFolder, "cloud.json");

    /// <summary>아직 클라우드에 못 올린 발급 건.</summary>
    private static string PendingUploadsPath => Path.Combine(IssuerFolder, "pending-uploads.jsonl");

    public static async Task<int> Main(string[] args)
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
                "issue" => await RunIssueAsync(args),
                "list" => RunList(),
                "sync" => await RunSyncAsync(),
                "cloud" => RunCloudStatus(),
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

    private static async Task<int> RunIssueAsync(string[] args)
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
            // Kind를 Local로 지정해야 한국 시간 기준 2027-12-31 23:59:59가 된다.
            // TimeSpan.Zero(UTC)로 만들면 표시할 때 9시간 밀려 다음 날로 보인다.
            var endOfDayLocal = DateTime.SpecifyKind(
                expiryDate.Date.AddDays(1).AddSeconds(-1), DateTimeKind.Local);

            expiresAtUnixSeconds = (uint)new DateTimeOffset(endOfDayLocal).ToUnixTimeSeconds();
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

        var record = new IssuanceRecord
        {
            SerialNumber = serialNumber,
            CustomerName = customerName,
            IssuedAt = payload.IssuedAt,
            ExpiresAt = payload.ExpiresAt,
            Code = code,
            IssuedBy = Environment.MachineName
        };

        AppendToLedger(serialNumber, customerName, payload, code);
        var codeFilePath = WriteCodeFile(serialNumber, customerName, code);

        Console.WriteLine($"발급 번호 : {serialNumber}");
        Console.WriteLine($"고객      : {customerName}");
        Console.WriteLine($"만료      : {(payload.IsPerpetual ? "무기한" : DateTimeOffset.FromUnixTimeSeconds(payload.ExpiresAt).ToLocalTime().ToString("yyyy-MM-dd"))}");
        Console.WriteLine();
        Console.WriteLine(code);
        Console.WriteLine();
        Console.WriteLine("USB에 담아 보낼 파일 (이것만 내보냅니다):");
        Console.WriteLine($"  {codeFilePath}");
        Console.WriteLine();
        Console.WriteLine("회사에 보관 (절대 내보내지 마세요):");
        Console.WriteLine($"  {PrivateKeyPath}");
        Console.WriteLine($"  {LedgerPath}");
        Console.WriteLine();

        // 업로드가 실패해도 발급은 이미 끝났다. 종료 코드를 0으로 두는 이유다 —
        // 여기서 1을 반환하면 "코드가 안 나왔다"로 오해하게 된다.
        await UploadToCloudAsync(record);

        return 0;
    }

    // ── 클라우드 업로드 ───────────────────────────────────────────────────

    /// <summary>발급 직후 호출. 밀려 있던 건까지 같이 올라간다.</summary>
    private static async Task UploadToCloudAsync(IssuanceRecord record)
    {
        var ledger = TryCreateCloudLedger(quiet: false);

        if (ledger is null)
            return;

        PrintOutcome(await ledger.UploadAsync(record));
    }

    private static async Task<int> RunSyncAsync()
    {
        var ledger = TryCreateCloudLedger(quiet: false);

        if (ledger is null)
            return 1;

        if (ledger.PendingCount == 0)
        {
            Console.WriteLine("올릴 것이 없습니다. 모든 발급 건이 클라우드에 있습니다.");
            return 0;
        }

        Console.WriteLine($"밀린 발급 {ledger.PendingCount}건을 올리는 중…");

        var outcome = await ledger.FlushAsync();
        PrintOutcome(outcome);

        return outcome.IsFullySynced ? 0 : 1;
    }

    private static int RunCloudStatus()
    {
        Console.WriteLine($"설정 파일 : {CloudConfigPath}");

        if (CloudConfig.TryLoad(CloudConfigPath, out var config, out var error))
        {
            Console.WriteLine($"프로젝트  : {config!.ProjectId}");
            Console.WriteLine($"컬렉션    : {config.Collection}");
            Console.WriteLine($"자격 증명 : {(string.IsNullOrWhiteSpace(config.CredentialsPath) ? "환경 기본값(GOOGLE_APPLICATION_CREDENTIALS / gcloud)" : config.CredentialsPath)}");
            Console.WriteLine();

            var pending = new CloudLedger(config, PendingUploadsPath).PendingCount;

            Console.WriteLine(pending == 0
                ? "대기 중인 업로드가 없습니다."
                : $"올리지 못한 발급 {pending}건이 있습니다. sync 명령으로 올리세요.");

            return 0;
        }

        if (error is not null)
        {
            Console.Error.WriteLine($"설정을 읽지 못했습니다: {error}");
            return 1;
        }

        PrintCloudSetupGuide();
        return 0;
    }

    /// <summary>
    /// 설정이 없거나 잘못됐으면 null. 설정 없음은 오류가 아니라 "업로드를 안 쓰는 상태"다.
    /// </summary>
    private static CloudLedger? TryCreateCloudLedger(bool quiet)
    {
        if (CloudConfig.TryLoad(CloudConfigPath, out var config, out var error))
            return new CloudLedger(config!, PendingUploadsPath);

        if (quiet)
            return null;

        if (error is not null)
        {
            // 설정해 뒀는데 오타 하나로 몇 달치가 안 올라가는 상황을 막으려면 조용히 넘기면 안 된다.
            Console.Error.WriteLine($"클라우드 업로드: 설정 오류 — {error}");
            Console.Error.WriteLine($"  {CloudConfigPath}");
        }
        else
        {
            Console.WriteLine("클라우드 업로드: 꺼짐 (cloud 명령으로 설정 방법을 볼 수 있습니다)");
        }

        return null;
    }

    private static void PrintOutcome(CloudUploadOutcome outcome)
    {
        if (outcome.IsFullySynced)
        {
            Console.WriteLine($"클라우드 업로드: 완료 ({outcome.Uploaded}건)");
            return;
        }

        Console.WriteLine($"클라우드 업로드: 실패 — {outcome.Remaining}건이 대기열에 남았습니다.");

        if (outcome.Error is not null)
            Console.WriteLine($"  사유: {outcome.Error}");

        Console.WriteLine("  코드 발급은 정상적으로 끝났습니다. 인터넷 연결 후 sync를 실행하세요.");
    }

    private static void PrintCloudSetupGuide()
    {
        Console.WriteLine();
        Console.WriteLine("아직 설정되지 않았습니다. 준비 순서:");
        Console.WriteLine();
        Console.WriteLine("  1. GCP 콘솔에서 Firestore 데이터베이스를 만든다 (Native 모드).");
        Console.WriteLine("  2. 서비스 계정을 만들고 역할에 'Cloud Datastore User'를 준다.");
        Console.WriteLine("  3. 그 계정의 JSON 키를 내려받아 아래 폴더에 둔다.");
        Console.WriteLine($"       {IssuerFolder}");
        Console.WriteLine("  4. 같은 폴더에 cloud.json을 만들고 아래 내용을 채운다.");
        Console.WriteLine();
        Console.WriteLine($"     {CloudConfigPath}");
        Console.WriteLine();

        foreach (var line in CloudConfig.SampleJson(Path.Combine(IssuerFolder, "service-account.json")).Split('\n'))
            Console.WriteLine($"     {line.TrimEnd()}");

        Console.WriteLine();
        Console.WriteLine("  서비스 계정 키는 개인키만큼은 아니어도 외부로 나가면 안 되는 파일입니다.");
        Console.WriteLine("  USB에는 절대 담지 마세요.");
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

    // ── 고객에게 줄 코드 파일 ─────────────────────────────────────────────

    /// <summary>
    /// 발급 건마다 별도 파일로 남긴다. 이름을 매번 license.txt로 하면 이전 고객 것을
    /// 덮어써 버리므로 발급 번호와 고객명을 파일명에 넣는다.
    /// 파일에는 코드만 넣는다. 앱의 "Load from file"이 이 파일을 그대로 읽는다.
    /// </summary>
    private static string WriteCodeFile(uint serialNumber, string customerName, string code)
    {
        Directory.CreateDirectory(IssuedFolder);

        var fileName = $"license_{serialNumber:D4}_{MakeFileNameSafe(customerName)}.txt";
        var path = Path.Combine(IssuedFolder, fileName);

        File.WriteAllText(path, code, Encoding.UTF8);

        return path;
    }

    /// <summary>고객명에 \ / : * ? " &lt; &gt; | 같은 글자가 있어도 파일명이 되게 만든다.</summary>
    private static string MakeFileNameSafe(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(name.Length);

        foreach (var c in name)
            builder.Append(Array.IndexOf(invalidChars, c) >= 0 || c == ' ' ? '_' : c);

        var safe = builder.ToString().Trim('_');
        return safe.Length == 0 ? "customer" : safe;
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
        Console.WriteLine("  issue <고객명> [만료일]          코드를 발급하고 클라우드에 올린다");
        Console.WriteLine("  list                            발급 대장을 본다");
        Console.WriteLine("  sync                            못 올린 발급 건을 클라우드에 올린다");
        Console.WriteLine("  cloud                           클라우드 설정 상태를 본다");
        Console.WriteLine();
        Console.WriteLine("예)");
        Console.WriteLine("  dotnet run --project tools/LicenseIssuer -- keygen");
        Console.WriteLine("  dotnet run --project tools/LicenseIssuer -- issue \"A약국\"");
        Console.WriteLine("  dotnet run --project tools/LicenseIssuer -- issue \"Hyo Pharmacy\" 2027-12-31");
        Console.WriteLine("  dotnet run --project tools/LicenseIssuer -- list");
        Console.WriteLine("  dotnet run --project tools/LicenseIssuer -- sync");
    }
}
