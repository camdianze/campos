using System.Text;
using PharmaPOS.Domain.Enums;

namespace PharmaPOS.Application.Counselling;

/// <summary>
/// AWaRe 시드 CSV 파서.
///
/// 기대하는 형식 (첫 줄은 헤더, 컬럼 순서는 상관없음 — 이름으로 찾는다):
///
///   atc_code,antibiotic_name,aware_group,is_systemic,source_version
///   J01CA04,Amoxicillin,ACCESS,true,WHO AWaRe 2025
///   ,Amoxicillin/clavulanic acid FDC,NOT_RECOMMENDED,true,WHO AWaRe 2025
///
///   - atc_code: 비어 있을 수 있다 (고유 ATC가 없는 복합제).
///   - aware_group: ACCESS | WATCH | RESERVE | NOT_RECOMMENDED.
///   - is_systemic: true/false (1/0, yes/no도 받는다). false면 국소 제제로 보고 안내 대상에서 뺀다.
///   - source_version: 파일이 스스로 출처를 밝히게 한다. 코드에 상수로 박아두지 않는다.
///
/// 외부 CSV 라이브러리를 쓰지 않는 이유: 의존성 하나를 더 늘리는 것보다,
/// 따옴표 처리 정도만 하는 짧은 파서를 두는 편이 이 프로젝트 규모에 맞다.
/// </summary>
public static class AwareSeedCsvParser
{
    private const string ColumnAtcCode = "atc_code";
    private const string ColumnAntibioticName = "antibiotic_name";
    private const string ColumnAwareGroup = "aware_group";
    private const string ColumnIsSystemic = "is_systemic";
    private const string ColumnSourceVersion = "source_version";

    public static AwareSeedParseResult Parse(string csvContent)
    {
        if (string.IsNullOrWhiteSpace(csvContent))
        {
            return AwareSeedParseResult.Failure("The AWaRe seed file is empty.");
        }

        var lines = SplitLines(csvContent);

        if (lines.Count == 0)
        {
            return AwareSeedParseResult.Failure("The AWaRe seed file is empty.");
        }

        var header = ParseLine(lines[0]);
        var columnIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < header.Count; i++)
        {
            // BOM이 첫 컬럼명 앞에 붙어 들어오는 경우가 흔해서 함께 털어낸다.
            var name = header[i].Trim().TrimStart('﻿');
            columnIndexes[name] = i;
        }

        var requiredColumns = new[]
        {
            ColumnAtcCode, ColumnAntibioticName, ColumnAwareGroup, ColumnIsSystemic, ColumnSourceVersion
        };

        var missing = requiredColumns.Where(c => !columnIndexes.ContainsKey(c)).ToList();

        if (missing.Count > 0)
        {
            return AwareSeedParseResult.Failure(
                $"The AWaRe seed file is missing required columns: {string.Join(", ", missing)}.");
        }

        var rows = new List<AwareSeedRow>();
        var errors = new List<string>();

        for (var lineNumber = 1; lineNumber < lines.Count; lineNumber++)
        {
            var rawLine = lines[lineNumber];

            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var fields = ParseLine(rawLine);

            // 사람이 손대는 파일이라 줄 끝 컬럼이 빠져 있는 경우가 생긴다.
            // 필수 값만 확보되면 통과시키고, 아니면 그 줄만 건너뛴다.
            var name = GetField(fields, columnIndexes, ColumnAntibioticName);

            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add($"Line {lineNumber + 1}: antibiotic_name is empty.");
                continue;
            }

            var groupText = GetField(fields, columnIndexes, ColumnAwareGroup);

            if (!AwareGroupCodes.TryParse(groupText, out var group))
            {
                errors.Add($"Line {lineNumber + 1}: unknown aware_group '{groupText}'.");
                continue;
            }

            var systemicText = GetField(fields, columnIndexes, ColumnIsSystemic);

            if (!TryParseBoolean(systemicText, out var isSystemic))
            {
                errors.Add($"Line {lineNumber + 1}: is_systemic must be true or false, but was '{systemicText}'.");
                continue;
            }

            var sourceVersion = GetField(fields, columnIndexes, ColumnSourceVersion)?.Trim();

            if (string.IsNullOrWhiteSpace(sourceVersion))
            {
                errors.Add($"Line {lineNumber + 1}: source_version is empty.");
                continue;
            }

            var atcCode = AntibioticNameNormalizer.NormalizeAtcCode(
                GetField(fields, columnIndexes, ColumnAtcCode));

            rows.Add(new AwareSeedRow
            {
                AtcCode = atcCode.Length == 0 ? null : atcCode,
                AntibioticName = name.Trim(),
                AwareGroup = group,
                IsSystemic = isSystemic,
                SourceVersion = sourceVersion
            });
        }

        return AwareSeedParseResult.Success(rows, errors);
    }

    private static string? GetField(
        IReadOnlyList<string> fields, IReadOnlyDictionary<string, int> columnIndexes, string columnName)
    {
        var index = columnIndexes[columnName];
        return index < fields.Count ? fields[index] : null;
    }

    private static bool TryParseBoolean(string? value, out bool result)
    {
        result = false;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "true":
            case "1":
            case "y":
            case "yes":
                result = true;
                return true;
            case "false":
            case "0":
            case "n":
            case "no":
                result = false;
                return true;
            default:
                return false;
        }
    }

    private static List<string> SplitLines(string content)
    {
        return content
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n')
            .ToList();
    }

    /// <summary>
    /// CSV 한 줄을 필드로 나눈다. 따옴표로 감싼 필드 안의 쉼표와,
    /// 따옴표 안의 두 번 겹친 따옴표("")를 처리한다.
    /// </summary>
    private static List<string> ParseLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }
}
