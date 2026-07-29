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
