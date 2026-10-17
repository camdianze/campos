using System.Globalization;

namespace CamPOS.Application.Updates;

/// <summary>
/// 업데이트가 언제 무엇을 했는지 한 줄씩 적는다.
///
/// 업데이트는 <b>약국 몰래</b> 일어난다 — 묻지도 알리지도 않는다. 그 대가로,
/// "어제까지 되던 게 안 된다"는 말을 들었을 때 짚을 것이 있어야 한다. 이 파일이
/// 그것이다: 언제, 몇 번에서 몇 번으로, 성공했는지 미뤘는지.
///
/// 데이터베이스가 아니라 텍스트 파일인 이유: 업데이트가 깨졌을 때도 읽혀야 하고,
/// 전화로 "그 파일 열어서 마지막 줄 읽어 주세요"가 되어야 한다.
/// </summary>
public sealed class UpdateLog
{
    public const string FileName = "update-log.txt";

    /// <summary>
    /// 남길 줄 수. 넘으면 오래된 것부터 버린다. 이 파일이 자라서 문제가 될 일은
    /// 없지만, 몇 년 뒤 수천 줄에서 마지막 줄을 찾게 하고 싶지도 않다.
    /// </summary>
    public const int MaxLines = 200;

    private readonly string _path;

    public UpdateLog(string folder)
    {
        _path = Path.Combine(folder, FileName);
    }

    public void Record(string message) => Append(message);

    public void RecordChecked(string? foundVersion) =>
        Append(foundVersion is null
            ? "checked - already up to date"
            : $"checked - found {foundVersion}");

    public void RecordDownloaded(string version) => Append($"downloaded {version}");

    public void RecordApplied(string fromVersion, string toVersion) =>
        Append($"applied {fromVersion} -> {toVersion}");

    public void RecordPostponed(string reason) => Append($"postponed - {reason}");

    public void RecordFailed(string what, string reason) => Append($"FAILED {what} - {reason}");

    /// <summary>화면에 보여 줄 마지막 줄들. 새 것이 위로 온다.</summary>
    public IReadOnlyList<string> ReadRecent(int count)
    {
        try
        {
            return File.Exists(_path)
                ? File.ReadAllLines(_path).Reverse().Take(count).ToList()
                : [];
        }
        catch
        {
            return [];
        }
    }

    private void Append(string message)
    {
        try
        {
            // 시각은 사람이 읽는 것이지만 기계가 정렬할 수도 있으므로 고정 형식으로
            // 적는다. PC의 지역 설정을 따르면 약국마다 다른 모양이 되고, 전화로
            // 읽어 줄 때 어느 쪽이 월인지가 애매해진다.
            var line = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                + "  " + message;

            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.AppendAllLines(_path, [line]);

            Trim();
        }
        catch
        {
            // 기록을 못 남긴 것으로 업데이트를 실패시키지 않는다. 이 파일은
            // 나중에 읽기 위한 것이지 동작에 쓰이지 않는다.
        }
    }

    private void Trim()
    {
        try
        {
            var lines = File.ReadAllLines(_path);

            if (lines.Length > MaxLines)
            {
                File.WriteAllLines(_path, lines.Skip(lines.Length - MaxLines));
            }
        }
        catch
        {
            // 위와 같다.
        }
    }
}
