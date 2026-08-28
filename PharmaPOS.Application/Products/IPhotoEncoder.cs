namespace PharmaPOS.Application.Products;

/// <summary>
/// 원본 이미지를 저장할 형태로 줄이고 다시 압축한다.
///
/// 구현이 WPF 쪽에 있는 이유: 이미지를 읽고 크기를 바꾸는 일은 화면 기술(PresentationCore)이
/// 들고 있는 기능이라, UI 프레임워크를 몰라야 하는 Application 계층에 둘 수 없다.
/// DPAPI·인쇄와 같은 구조다.
/// </summary>
public interface IPhotoEncoder
{
    /// <summary>
    /// 성공하면 JPEG 바이트를, 이미지로 읽히지 않으면 null을 돌려준다.
    /// 예외를 던지지 않는다 — 사진 한 장 때문에 상품 화면이 죽으면 안 된다.
    /// </summary>
    byte[]? Encode(byte[] source, int maxEdgePixels);
}
