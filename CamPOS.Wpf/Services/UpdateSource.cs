namespace CamPOS.Wpf.Services;

/// <summary>
/// 새 버전을 어디서 받는가. 동기화에 이미 쓰고 있는 Supabase 프로젝트의
/// Storage 버킷이고, 별도 서비스를 두지 않는다.
///
/// <b>읽기 전용 공개 주소다.</b> 올리는 권한은 이 앱 어디에도 없고, 저장소에도
/// 없다 — 업로드는 Supabase 대시보드에서 사람이 한다. 약국 PC에 업로드 열쇠를
/// 두면 그중 한 대만 뜯겨도 모든 약국에 아무 버전이나 보낼 수 있게 된다.
/// 받는 쪽만 열어 두면 최악의 경우가 "남이 우리 설치 파일을 내려받는 것"으로
/// 끝난다.
///
/// 채널(<c>test/</c>, <c>stable/</c>)은 이 주소 뒤에 붙는다. 먼저 test에 올려
/// 본인 PC와 약국 한 곳에서 확인한 뒤 stable로 옮긴다 — 잘못 올린 버전은
/// 되돌릴 수 없고, 더 높은 번호의 수정판을 올리는 것이 유일한 수습이다.
/// </summary>
public static class UpdateSource
{
    /// <summary>
    /// 버킷의 공개 읽기 주소. 끝의 채널 폴더는 <c>VelopackUpdateService</c>가 붙인다.
    ///
    /// 비워 두면 업데이트 기능 전체가 조용히 꺼진다 — 개발 중이거나 아직 버킷을
    /// 만들지 않았을 때 시작을 막지 않기 위해서다.
    /// </summary>
    public const string ReleasesBaseUrl =
        "https://wigewqyblckkguaslupp.supabase.co/storage/v1/object/public/releases";
}
