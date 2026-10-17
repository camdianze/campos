namespace CamPOS.Application.Updates;

/// <summary>
/// 업데이트를 <b>언제</b> 할지 정하는 규칙. 받는 일과 교체하는 일 자체는 Velopack이
/// 하고, 여기는 그것을 불러도 되는 때인지만 판단한다.
///
/// 판단을 Application에 두는 이유는 그 규칙이 화면의 사정이 아니기 때문이다.
/// 약국 계산대에서 프로그램이 멈추거나 데이터가 백업 없이 교체되는 일은 조용히
/// 일어나고, 그런 종류의 규칙은 테스트가 붙잡을 수 있는 자리에 있어야 한다.
/// </summary>
public static class UpdatePolicy
{
    /// <summary>
    /// 지금 업데이트를 확인해도 되는가.
    ///
    /// 켤 때 한 번만 본다. 쓰는 중에 주기적으로 확인하면 느린 회선에서 판매 중에
    /// 대역폭을 가져가고, 그렇게 얻는 것은 "몇 시간 일찍 받는 것"뿐이다.
    /// </summary>
    public static UpdateCheckDecision ShouldCheck(UpdateContext context)
    {
        if (!context.IsAtStartup)
        {
            return UpdateCheckDecision.No("Updates are only checked when the app starts.");
        }

        if (!context.IsUpdateSourceConfigured)
        {
            return UpdateCheckDecision.No("No update source is configured for this installation.");
        }

        // 개발 중이거나 압축을 풀어 쓰는 설치에서는 교체할 대상이 없다. 이 경우
        // Velopack은 예외를 던지는데, 그것이 개발 중 시작을 막아서는 안 된다.
        if (!context.IsInstalled)
        {
            return UpdateCheckDecision.No("This copy was not installed by the updater.");
        }

        return UpdateCheckDecision.Yes();
    }

    /// <summary>
    /// 받아 둔 업데이트를 지금 적용해도 되는가.
    ///
    /// 적용은 <b>앱이 꺼질 때</b>만 한다. 쓰는 중에 교체하면 판매 도중에 재시작이
    /// 걸리고, 시작할 때 교체하면 켜자마자 한 번 더 꺼졌다 켜진다.
    ///
    /// 그리고 <b>백업이 먼저다.</b> 교체 자체는 데이터베이스를 건드리지 않지만,
    /// 새 버전이 스키마를 고치는 경우 그 직전 상태로 돌아갈 방법이 있어야 한다.
    /// 백업이 안 되면 미룬다 — 하루 늦게 업데이트되는 것과 돌아갈 데가 없는 것은
    /// 견줄 수 없다.
    /// </summary>
    public static UpdateApplyDecision ShouldApply(UpdateContext context)
    {
        if (!context.IsAtShutdown)
        {
            return UpdateApplyDecision.No("Updates are applied when the app closes.");
        }

        if (!context.HasDownloadedUpdate)
        {
            return UpdateApplyDecision.No("Nothing has been downloaded.");
        }

        if (!context.BackupSucceeded)
        {
            // 미루는 것이지 포기하는 것이 아니다. 받아 둔 것은 그대로 남아
            // 다음에 다시 시도된다.
            return UpdateApplyDecision.Postponed(
                "The database could not be backed up, so the update was left for next time.");
        }

        return UpdateApplyDecision.Yes();
    }
}

/// <summary>
/// 판단에 필요한 사실들. 어디서 왔는지(파일 시스템·네트워크·Velopack)는 여기서
/// 알 바가 아니고, 그래서 이 판단이 테스트된다.
/// </summary>
public sealed class UpdateContext
{
    public bool IsAtStartup { get; init; }

    public bool IsAtShutdown { get; init; }

    /// <summary>업데이트 주소가 설정돼 있는가.</summary>
    public bool IsUpdateSourceConfigured { get; init; }

    /// <summary>설치 프로그램으로 깔린 사본인가. 개발 빌드는 아니다.</summary>
    public bool IsInstalled { get; init; }

    public bool HasDownloadedUpdate { get; init; }

    /// <summary>교체 직전 백업이 성공했는가.</summary>
    public bool BackupSucceeded { get; init; }
}

public sealed class UpdateCheckDecision
{
    private UpdateCheckDecision(bool shouldCheck, string reason)
    {
        ShouldCheck = shouldCheck;
        Reason = reason;
    }

    public bool ShouldCheck { get; }

    public string Reason { get; }

    public static UpdateCheckDecision Yes() => new(true, string.Empty);

    public static UpdateCheckDecision No(string reason) => new(false, reason);
}

public sealed class UpdateApplyDecision
{
    private UpdateApplyDecision(bool shouldApply, bool postponed, string reason)
    {
        ShouldApply = shouldApply;
        IsPostponed = postponed;
        Reason = reason;
    }

    public bool ShouldApply { get; }

    /// <summary>
    /// 받아 둔 것은 있는데 지금은 안 한다. 아무 할 일이 없는 것과 구분된다 —
    /// 이쪽은 기록에 남겨야 다음에 왜 안 됐는지 알 수 있다.
    /// </summary>
    public bool IsPostponed { get; }

    public string Reason { get; }

    public static UpdateApplyDecision Yes() => new(true, false, string.Empty);

    public static UpdateApplyDecision No(string reason) => new(false, false, reason);

    public static UpdateApplyDecision Postponed(string reason) => new(false, true, reason);
}
