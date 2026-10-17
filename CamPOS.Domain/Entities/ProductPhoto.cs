namespace PharmaPOS.Domain.Entities;

/// <summary>
/// 상품 사진 한 장.
///
/// Product에 담지 않고 따로 둔 이유: 상품 목록은 수백 줄을 한 번에 읽는데
/// 거기에 장당 수백 KB짜리 이미지가 딸려 오면 검색이 눈에 띄게 느려진다.
/// 사진은 상세 화면을 열 때만 따로 읽는다.
/// </summary>
/// <param name="Bytes">JPEG로 다시 압축한 이미지.</param>
/// <param name="UpdatedAt">사진을 마지막으로 바꾼 시각 (Unix epoch 밀리초).</param>
public sealed record ProductPhoto(byte[] Bytes, long UpdatedAt);
