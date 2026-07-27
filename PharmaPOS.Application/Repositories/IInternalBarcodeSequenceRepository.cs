namespace PharmaPOS.Application.Repositories;

/// <summary>
/// Internal Barcode Sequence 테이블에서 다음 순번을 채번하는 인터페이스.
/// PRD: 시퀀스는 전역적이고, 영구적이며, 재사용되지 않는다.
/// </summary>
public interface IInternalBarcodeSequenceRepository
{
    /// <summary>
    /// 다음 순번을 증가시키고, INT-XXXXXXXX 형식의 문자열로 반환한다.
    /// 호출할 때마다 값이 1씩 증가하며, 동일한 번호가 두 번 반환되지 않는다.
    /// </summary>
    Task<string> GetNextInternalBarcodeAsync();
}