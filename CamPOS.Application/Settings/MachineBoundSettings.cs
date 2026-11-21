namespace CamPOS.Application.Settings;

/// <summary>
/// 데이터가 아니라 <b>이 컴퓨터</b>에 속하는 설정들.
///
/// 백업은 데이터베이스 파일 하나를 통째로 복사해 덮어쓰는 일인데, 이 설정들도 같은
/// 파일 안(<c>App_Setting</c>)에 들어 있다. 그래서 복원하면 "이 PC가 어느 약국의
/// 어느 컴퓨터인가"가 백업을 뜬 PC의 것으로 바뀌거나, 백업이 그보다 오래된 것이면
/// 아예 사라진다.
///
/// 실제로 그렇게 됐다. 새 라이선스로 활성화해 토큰을 받은 PC에 옛 백업을 복원했더니
/// 토큰이 사라졌고, 화면은 "Never sent"에 수천 행이 밀린 상태로 남았다. 라이선스는
/// 멀쩡했다 — <c>license.dat</c>는 폴더의 <b>파일</b>이라 복원이 건드리지 않는다.
/// 데이터베이스 안에 있던 것만 날아간 것이고, 그 차이가 이 목록이 있는 이유다.
///
/// <see cref="IBackupRepository.RestoreDatabaseAsync"/>가 복원 직전에 이 키들을 꺼내
/// 두었다가 복원 직후에 되돌려 쓴다.
///
/// <b>워터마크(<c>Sync_State</c>)는 여기 없다.</b> 그것은 데이터의 것이다 — 어디까지
/// 보냈는지는 그 행들에 대한 사실이고, 복원으로 행이 통째로 바뀌면 함께 바뀌어야 한다.
/// 남겨 두면 복원해 온 행들을 이미 보낸 것으로 쳐서 영영 올라가지 않는다. 반대로
/// 되감겨 다시 보내는 쪽은 안전하다 — 서버가 <c>(site_id, 원본 ID)</c>로 덮어쓴다.
/// </summary>
public static class MachineBoundSettings
{
    /// <summary>
    /// 복원을 건너서 살아남아야 하는 키들.
    ///
    /// 새 키를 더할 때의 기준은 하나다 — <b>"이 값이 백업을 뜬 PC의 것이면 틀린가?"</b>
    /// 그렇다면 여기 넣는다. 약국의 영업 내용(가격·영수증 문구·상담 설정)은 반대로
    /// 데이터와 함께 와야 하므로 넣지 않는다.
    /// </summary>
    public static readonly IReadOnlyList<string> Keys =
    [
        // 이 컴퓨터를 서버에 알리는 값. DPAPI로 묶여 있어 다른 PC로 옮겨도 못 읽지만,
        // 같은 PC에서 복원했을 뿐인데 사라지는 것은 다른 이야기다.
        AppSettingKeys.SyncToken,

        // 주소와 공개 키는 앱에 기본값이 실려 있어 없어도 돌아가지만, 손으로 고친
        // 곳이라면 그 고침이 이 PC의 것이다.
        AppSettingKeys.SyncServerUrl,
        AppSettingKeys.SyncPublishableKey,

        // 약국이 "보낸다"고 체크한 것. 기본값이 꺼짐이므로 복원이 지우면 조용히
        // 전송이 멈추고, 아무도 끄지 않았는데 꺼져 있는 상태가 된다.
        AppSettingKeys.SyncAutomatic,

        // 이 PC를 test 채널에 둔 것은 이 PC에 대한 결정이다.
        AppSettingKeys.UpdateChannel,
    ];
}
