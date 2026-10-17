using System.IO;
using PharmaPOS.Application.Import;

namespace Lightweight_Digital_Inventory_Management___POS_System.Services;

/// <summary>
/// 사용자가 고른 폴더를 사진 임포트의 입력으로 삼는다.
///
/// 파일을 미리 읽어 두지 않는 이유: 사진 300장이면 원본만 1GB에 가깝다.
/// 이름으로 짝을 맞춘 뒤 실제로 넣을 파일만 한 장씩 읽는다.
/// 하위 폴더는 보지 않는다 — 현장에서 폴더 하나에 모아 넘기는 방식이고,
/// 하위까지 훑으면 같은 이름의 사진이 여러 벌 잡힐 수 있다.
/// </summary>
public sealed class FolderPhotoSource : IImportPhotoSource
{
    private readonly string _folderPath;

    public FolderPhotoSource(string folderPath)
    {
        _folderPath = folderPath;

        FileNames = Directory
            .EnumerateFiles(folderPath, "*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<string> FileNames { get; }

    public byte[] Read(string fileName)
    {
        // 파일 이름만 받아 폴더 안에서 다시 만든다. 목록 밖의 경로를 읽지 않게 하기 위한 것이다.
        var safeName = Path.GetFileName(fileName);

        return File.ReadAllBytes(Path.Combine(_folderPath, safeName));
    }
}
