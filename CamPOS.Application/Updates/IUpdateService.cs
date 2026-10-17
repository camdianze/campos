namespace CamPOS.Application.Updates;

/// <summary>
/// 업데이트를 확인하고 받아 두고, 꺼질 때 적용한다.
///
/// 어떤 실패도 예외로 나가지 않는다. 호출하는 자리가 <b>앱의 시작과 종료</b>라서,
/// 여기서 던지면 약국은 업데이트 때문에 프로그램이 안 열리는 것을 겪는다 —
/// 업데이트는 없어도 되는 기능이고, 판매는 아니다.
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// 켤 때 한 번. 새 버전이 있으면 <b>받아만</b> 둔다. 화면에는 아무것도 띄우지
    /// 않는다 — 묻지도 알리지도 않는 것이 이 기능의 설계다.
    /// </summary>
    Task CheckAndDownloadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 꺼질 때. 받아 둔 것이 있으면 데이터베이스를 백업하고 교체를 예약한다.
    /// 백업이 실패하면 미룬다.
    /// </summary>
    Task ApplyIfReadyAsync();

    /// <summary>관리자 화면이 보여 줄 상태.</summary>
    UpdateStatus GetStatus();

    /// <summary>"지금 확인" 버튼. 하는 일은 <see cref="CheckAndDownloadAsync"/>와 같다.</summary>
    Task<UpdateStatus> CheckNowAsync();
}

/// <summary>관리자 화면 한 칸에 들어갈 것들.</summary>
public sealed class UpdateStatus
{
    public required string CurrentVersion { get; init; }

    /// <summary>받아 둔 새 버전. 없으면 null.</summary>
    public string? DownloadedVersion { get; init; }

    public DateTimeOffset? LastChecked { get; init; }

    /// <summary>test 또는 stable.</summary>
    public required string Channel { get; init; }

    /// <summary>설치 프로그램으로 깔린 사본인가. 개발 중에는 false다.</summary>
    public bool IsInstalled { get; init; }

    /// <summary>마지막으로 일어난 일 한 줄. 비어 있을 수 있다.</summary>
    public string LastMessage { get; init; } = string.Empty;
}

/// <summary>
/// 배포 채널. 먼저 <see cref="Test"/>에 올려 본인 PC와 약국 한 곳에서 확인한 뒤
/// <see cref="Stable"/>로 옮긴다 — 잘못된 버전은 되돌릴 수 없고, 더 높은 번호의
/// 수정판을 올리는 것이 유일한 수습이기 때문이다.
/// </summary>
public static class UpdateChannels
{
    public const string Test = "test";

    public const string Stable = "stable";

    /// <summary>
    /// 기본은 stable이다. 설정을 읽지 못하는 상황에서 test로 떨어지면, 검증 전
    /// 버전이 약국에 가는 쪽으로 실패하게 된다.
    /// </summary>
    public const string Default = Stable;

    public static string Normalize(string? value) =>
        string.Equals(value?.Trim(), Test, StringComparison.OrdinalIgnoreCase)
            ? Test
            : Stable;
}
