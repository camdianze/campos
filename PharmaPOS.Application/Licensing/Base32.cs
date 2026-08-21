// ─────────────────────────────────────────────────────────────────────────────
// 이 파일은 두 저장소에 같은 내용으로 존재한다.
//
//   CamPOS          PharmaPOS.Application/Licensing/   코드를 검증한다
//   라이선스 발급 도구  src/CamPos.Licensing/              코드를 만든다
//
// 한쪽만 고치면 새로 발급한 코드가 고객 PC에서 거부된다. 발급도, 대장 기록도,
// 코드 생김새도 전부 정상으로 보이기 때문에 계산대에서 거부당하고 나서야 안다.
// 그래서 양쪽 저장소가 같은 license-vectors.json으로 서로를 검사한다 —
// 한쪽이 어긋나면 고객이 아니라 그쪽 빌드가 먼저 깨진다.
//
// 네임스페이스까지 같게 두는 이유: 두 사본을 그대로 diff 할 수 있어야 한다.
// ─────────────────────────────────────────────────────────────────────────────
using System.Text;

namespace PharmaPOS.Application.Licensing;

/// <summary>
/// 라이선스 코드를 사람이 다루기 좋은 문자열로 바꾸는 인코딩.
///
/// Base64가 아니라 Base32를 쓰는 이유:
/// - 대소문자를 구분하지 않아 고객이 대충 입력해도 통과한다
/// - 서로 헷갈리는 글자(0/O, 1/I/L)를 알파벳에서 빼서 오타를 줄인다
/// - +, / 같은 기호가 없어 메신저나 메일에서 깨지지 않는다
/// Base64보다 20%쯤 길어지지만 붙여넣기가 전제라 길이보다 정확도가 중요하다.
/// </summary>
public static class Base32
{
    // Crockford Base32에서 U를 뺀 배열. 0/O, 1/I/L 혼동이 없다.
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static string Encode(byte[] data)
    {
        var builder = new StringBuilder((data.Length * 8 + 4) / 5);

        var buffer = 0;
        var bitsInBuffer = 0;

        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bitsInBuffer += 8;

            while (bitsInBuffer >= 5)
            {
                bitsInBuffer -= 5;
                builder.Append(Alphabet[(buffer >> bitsInBuffer) & 0x1F]);
            }
        }

        // 남은 비트는 0으로 채워 한 글자로 만든다.
        if (bitsInBuffer > 0)
            builder.Append(Alphabet[(buffer << (5 - bitsInBuffer)) & 0x1F]);

        return builder.ToString();
    }

    /// <summary>
    /// 실패하면 false. 잘못 입력된 코드는 정상 흐름이라 예외를 던지지 않는다.
    /// </summary>
    public static bool TryDecode(string text, out byte[] data)
    {
        data = [];

        var bytes = new List<byte>(text.Length * 5 / 8 + 1);

        var buffer = 0;
        var bitsInBuffer = 0;

        foreach (var rawChar in text)
        {
            // 구분용 하이픈과 공백은 무시한다. 고객이 줄바꿈째 붙여넣는 경우도 있다.
            if (rawChar is '-' or ' ' or '\r' or '\n' or '\t')
                continue;

            var normalized = NormalizeChar(rawChar);
            var value = Alphabet.IndexOf(normalized);

            if (value < 0)
                return false;

            buffer = (buffer << 5) | value;
            bitsInBuffer += 5;

            if (bitsInBuffer >= 8)
            {
                bitsInBuffer -= 8;
                bytes.Add((byte)((buffer >> bitsInBuffer) & 0xFF));
            }
        }

        data = bytes.ToArray();
        return true;
    }

    /// <summary>혼동하기 쉬운 글자를 알파벳에 있는 글자로 바꾼다.</summary>
    private static char NormalizeChar(char value)
    {
        var upper = char.ToUpperInvariant(value);

        return upper switch
        {
            'O' => '0',
            'I' or 'L' => '1',
            'U' => 'V',
            _ => upper
        };
    }
}
