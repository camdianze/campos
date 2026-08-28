namespace PharmaPOS.Application.Import;

/// <summary>
/// 사진 임포트가 읽을 파일 묶음. 보통은 사용자가 고른 폴더다.
///
/// 파일 내용을 한꺼번에 들고 있지 않는 이유: 사진 300장이면 원본만 1GB에 가깝다.
/// 이름으로 먼저 짝을 맞추고, 실제로 넣기로 한 파일만 한 장씩 읽는다.
/// </summary>
public interface IImportPhotoSource
{
    /// <summary>폴더 안의 이미지 파일 이름 (경로 없이, 확장자 포함).</summary>
    IReadOnlyList<string> FileNames { get; }

    /// <summary>파일 하나를 읽는다. 읽지 못하면 예외를 던진다 — 부르는 쪽이 그 파일만 실패로 센다.</summary>
    byte[] Read(string fileName);
}
