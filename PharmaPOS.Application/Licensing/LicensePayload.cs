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
using System.Buffers.Binary;

namespace PharmaPOS.Application.Licensing;

/// <summary>
/// 라이선스 코드 안에 서명되어 들어가는 내용. 13바이트 고정.
///
/// 고객명은 일부러 넣지 않는다. 이름을 넣으면 코드가 길어지고 한글 인코딩 문제가 생긴다.
/// 대신 일련번호만 넣고, 번호와 고객의 대응은 발급 도구가 로컬 대장에 기록한다.
/// 코드가 유출됐을 때 일련번호로 어느 고객에게 나간 것인지 추적할 수 있다.
/// </summary>
public class LicensePayload
{
    /// <summary>이 앱이 이해하는 포맷 번호. 나중에 담을 내용이 바뀌면 올린다.</summary>
    public const byte CurrentVersion = 1;

    public const int ByteLength = 13;

    public byte Version { get; init; } = CurrentVersion;

    /// <summary>발급 일련번호. 발급 대장의 행 번호와 같다.</summary>
    public uint SerialNumber { get; init; }

    /// <summary>발급 시각(Unix 초).</summary>
    public uint IssuedAt { get; init; }

    /// <summary>만료 시각(Unix 초). 0이면 무기한.</summary>
    public uint ExpiresAt { get; init; }

    public bool IsPerpetual => ExpiresAt == 0;

    public byte[] ToBytes()
    {
        var bytes = new byte[ByteLength];

        bytes[0] = Version;
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(1, 4), SerialNumber);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(5, 4), IssuedAt);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(9, 4), ExpiresAt);

        return bytes;
    }

    public static bool TryFromBytes(ReadOnlySpan<byte> bytes, out LicensePayload payload)
    {
        payload = new LicensePayload();

        if (bytes.Length != ByteLength)
            return false;

        payload = new LicensePayload
        {
            Version = bytes[0],
            SerialNumber = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(1, 4)),
            IssuedAt = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(5, 4)),
            ExpiresAt = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(9, 4))
        };

        return true;
    }
}
