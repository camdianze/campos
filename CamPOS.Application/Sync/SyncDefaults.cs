namespace CamPOS.Application.Sync;

/// <summary>
/// 모든 약국이 같은 값. 앱에 실어 보내고, 설치할 때 입력받지 않는다.
///
/// 둘 다 비밀이 아니다 — publishable 키는 이름 그대로 공개돼도 되는 값이고,
/// 그것만으로는 아무것도 못 한다. 표에는 RLS가 걸려 있고 쓰기는 Edge Function
/// 하나뿐이며, 어느 약국인지는 <b>토큰</b>이 정한다. 입력받아서 얻을 것이
/// 없는데 오타가 날 자리만 둘 늘어난다.
///
/// 설정 화면의 칸은 남긴다. 자체 서버를 쓰거나 프로젝트를 옮길 때 필요하고,
/// 적어 넣은 값이 있으면 그쪽이 이긴다 — 박아 둔 값은 비어 있을 때의 기본값이다.
/// </summary>
public static class SyncDefaults
{
    public const string ServerUrl = "https://wigewqyblckkguaslupp.supabase.co";

    public const string PublishableKey = "sb_publishable_wSGmQvVHrqw8a8Pr6NZUYg_Ea3K7sq_";
}
