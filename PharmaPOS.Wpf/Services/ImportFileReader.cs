using System.IO;
using System.Security.Cryptography;
using System.Text;
using ClosedXML.Excel;
using PharmaPOS.Application.Import;

namespace Lightweight_Digital_Inventory_Management___POS_System.Services;

/// <summary>
/// 임포트 파일(CSV/Excel)을 행 목록으로 읽어 들인다.
///
/// 파일 형식을 아는 코드는 여기까지다. 그 뒤의 판정은 전부 Application의 임포트 서비스가 한다 —
/// 그래야 CSV로 넣든 Excel로 넣든 같은 규칙이 적용되고, 규칙만 따로 테스트할 수 있다.
/// </summary>
public static class ImportFileReader
{
    /// <summary>파일 내용의 SHA-256. 이름을 바꿔 다시 넣어도 같은 파일로 알아본다.</summary>
    public static string ComputeHash(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    /// <summary>확장자에 맞는 방식으로 읽는다. 지원하지 않는 확장자면 예외를 던진다.</summary>
    public static IReadOnlyList<ImportSourceRow> Read(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".csv" => ReadCsv(filePath),
            ".xlsx" => ReadExcel(filePath),
            _ => throw new NotSupportedException("Only .csv and .xlsx are supported.")
        };
    }

    private static IReadOnlyList<ImportSourceRow> ReadCsv(string filePath)
    {
        var lines = File.ReadAllLines(filePath);

        if (lines.Length < 2)
        {
            return [];
        }

        var headers = SplitCsvLine(lines[0])
            .Select(InitialImportColumns.NormalizeHeader)
            .ToArray();

        var rows = new List<ImportSourceRow>();

        for (var i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }

            var columns = SplitCsvLine(lines[i]);
            var values = new Dictionary<string, string>(StringComparer.Ordinal);

            for (var c = 0; c < headers.Length; c++)
            {
                if (headers[c].Length == 0)
                {
                    continue;
                }

                values[headers[c]] = c < columns.Length ? columns[c].Trim() : string.Empty;
            }

            // 파일에서 사람이 보는 행 번호. 헤더가 1행이므로 첫 데이터 행은 2다.
            rows.Add(new ImportSourceRow { LineNumber = i + 1, Values = values });
        }

        return rows;
    }

    private static IReadOnlyList<ImportSourceRow> ReadExcel(string filePath)
    {
        using var workbook = new XLWorkbook(filePath);
        var worksheet = workbook.Worksheet(1);

        var usedRows = worksheet.RangeUsed()?.RowsUsed().ToList();

        if (usedRows is null || usedRows.Count < 2)
        {
            return [];
        }

        var headers = usedRows[0].Cells()
            .Select(cell => InitialImportColumns.NormalizeHeader(cell.GetString()))
            .ToArray();

        var rows = new List<ImportSourceRow>();

        for (var i = 1; i < usedRows.Count; i++)
        {
            var excelRow = usedRows[i];
            var values = new Dictionary<string, string>(StringComparer.Ordinal);

            for (var c = 0; c < headers.Length; c++)
            {
                if (headers[c].Length == 0)
                {
                    continue;
                }

                values[headers[c]] = ReadCell(excelRow.Cell(c + 1));
            }

            rows.Add(new ImportSourceRow
            {
                // 엑셀 화면에 보이는 행 번호를 그대로 쓴다. 빈 행을 건너뛴 경우에도 어긋나지 않는다.
                LineNumber = excelRow.RowNumber(),
                Values = values
            });
        }

        return rows;
    }

    /// <summary>
    /// 셀 하나를 문자열로. 날짜 셀은 서식(2027년 5월 1일 등)이 아니라 yyyy-MM-dd로 바꿔 넘긴다 —
    /// 서식 문자열을 그대로 넘기면 지역 설정에 따라 파싱이 되기도 하고 안 되기도 한다.
    /// </summary>
    private static string ReadCell(IXLCell cell)
    {
        if (cell.DataType == XLDataType.DateTime && cell.TryGetValue<DateTime>(out var date))
        {
            return date.ToString("yyyy-MM-dd");
        }

        return cell.GetString().Trim();
    }

    /// <summary>따옴표 안의 쉼표를 구분자로 보지 않는다.</summary>
    private static string[] SplitCsvLine(string line)
    {
        var result = new List<string>();
        var inQuotes = false;
        var current = new StringBuilder();

        foreach (var c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        result.Add(current.ToString());
        return result.ToArray();
    }
}
