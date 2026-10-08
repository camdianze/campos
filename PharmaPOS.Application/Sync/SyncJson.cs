using System.Text.Encodings.Web;
using System.Text.Json;

namespace PharmaPOS.Application.Sync;

/// <summary>
/// 동기화 JSON을 쓰는 규칙. 미리보기 파일과 실제 전송이 <b>같은 것</b>을 써야 한다 —
/// 미리보기의 존재 이유가 "서버로 갈 모양을 먼저 보는 것"인데, 둘이 다르면 미리보기가
/// 보여주는 것은 서버로 가지 않는 모양이다.
///
/// 이름을 snake_case로 쓰는 이유는 서버 컬럼이 그 이름이기 때문이다. 맞춰 두면
/// Edge Function의 허용 목록이 곧 컬럼 목록이 되고, 이름을 바꿔 주는 표가 사라진다.
/// 그 표는 한 줄만 틀려도 그 컬럼만 조용히 비어서 들어가는 종류의 코드다.
/// </summary>
public static class SyncJson
{
    /// <summary>전송용. 사람이 읽을 일이 없으므로 들여쓰지 않는다.</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,

        // ៛나 크메르 글자가 \uXXXX로 부풀지 않게 한다. 전송은 UTF-8이고 서버도 UTF-8이다.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>미리보기 파일용. 내용은 전송과 같고 들여쓰기만 다르다.</summary>
    public static readonly JsonSerializerOptions PreviewOptions = new(Options)
    {
        WriteIndented = true
    };
}
