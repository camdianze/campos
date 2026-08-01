using System.Text;

namespace PharmaPOS.Application.Counselling;

/// <summary>
/// ICounsellingSheetFileWriter의 구현체.
///
/// UTF-8 BOM을 붙여 저장한다. 메모장이 인코딩을 잘못 잡아 크메르 문자나
/// 도형 문자(■□▨)가 깨져 보이면, 내용이 멀쩡한데도 렌더링 문제로 오해하게 된다.
/// </summary>
public class CounsellingSheetFileWriter : ICounsellingSheetFileWriter
{
    private readonly string _defaultFolder;

    /// <param name="defaultFolder">설정에 폴더가 지정되지 않았을 때 쓸 기본 저장 위치.</param>
    public CounsellingSheetFileWriter(string defaultFolder)
    {
        _defaultFolder = defaultFolder;
    }

    public async Task<CounsellingPrintResult> WriteAsync(
        CounsellingSheetDocument document, string? folder, string fileNameHint)
    {
        var targetFolder = string.IsNullOrWhiteSpace(folder) ? _defaultFolder : folder!;

        try
        {
            Directory.CreateDirectory(targetFolder);

            var path = Path.Combine(targetFolder, BuildFileName(document.ProductName, fileNameHint));

            await File.WriteAllTextAsync(path, document.ToPlainText(), new UTF8Encoding(true));

            return CounsellingPrintResult.Success();
        }
        catch (Exception)
        {
            return CounsellingPrintResult.Failure(
                "The counselling sheet could not be saved to the output folder.");
        }
    }

    /// <summary>
    /// 같은 초에 여러 건이 나가도 덮어쓰지 않도록 거래 식별자를 파일명에 넣는다.
    /// (한 거래에 항생제가 여러 개면 상품별로 한 장씩 나온다.)
    /// </summary>
    private static string BuildFileName(string productName, string fileNameHint)
    {
        var timestamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
        var hint = MakeFileNameSafe(fileNameHint);
        var product = MakeFileNameSafe(productName);

        if (hint.Length > 8)
        {
            hint = hint[..8];
        }

        return $"counselling_{timestamp}_{hint}_{product}.txt";
    }

    private static string MakeFileNameSafe(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);

        foreach (var c in value)
        {
            builder.Append(Array.IndexOf(invalid, c) >= 0 || c == ' ' ? '_' : c);
        }

        var safe = builder.ToString().Trim('_');
        return safe.Length == 0 ? "sheet" : safe;
    }
}
