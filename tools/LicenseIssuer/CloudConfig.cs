using System.Text.Json;
using System.Text.Json.Serialization;

namespace PharmaPOS.Tools.LicenseIssuer;

/// <summary>
/// 클라우드(Firestore) 업로드 설정. %APPDATA%\PharmaPOS.Issuer\cloud.json 에 둔다.
///
/// 저장소가 아니라 개인키 옆에 두는 이유: 여기 적힌 서비스 계정 키 파일만 있으면
/// 발급 대장 전체를 읽고 쓸 수 있다. 개인키만큼은 아니어도 배포물에 딸려 나가면 안 되는 값이다.
///
/// 파일이 없으면 업로드는 그냥 꺼진 채로 동작한다. 인터넷도 GCP 계정도 없는 상태에서
/// 코드 발급 자체는 언제나 되어야 하기 때문이다.
/// </summary>
public sealed class CloudConfig
{
    [JsonPropertyName("projectId")]
    public string ProjectId { get; init; } = string.Empty;

    /// <summary>Firestore 컬렉션 이름. 비워두면 licenses.</summary>
    [JsonPropertyName("collection")]
    public string Collection { get; init; } = "licenses";

    /// <summary>
    /// 서비스 계정 JSON 키 파일 경로.
    /// 비워두면 GOOGLE_APPLICATION_CREDENTIALS 환경변수나 gcloud 로그인 자격 증명을 쓴다.
    /// </summary>
    [JsonPropertyName("credentialsPath")]
    public string? CredentialsPath { get; init; }

    /// <summary>
    /// 설정 파일을 읽는다. 파일이 없으면 (false, null) — 오류가 아니라 "업로드 꺼짐"이다.
    /// 파일은 있는데 내용이 깨졌으면 (false, 오류메시지) — 이건 조용히 넘기면 안 된다.
    /// 설정해 뒀는데 오타 때문에 몇 달치 발급이 안 올라가는 상황을 막기 위해서다.
    /// </summary>
    public static bool TryLoad(string path, out CloudConfig? config, out string? error)
    {
        config = null;
        error = null;

        if (!File.Exists(path))
            return false;

        try
        {
            var loaded = JsonSerializer.Deserialize<CloudConfig>(File.ReadAllText(path));

            if (loaded is null || string.IsNullOrWhiteSpace(loaded.ProjectId))
            {
                error = "projectId가 비어 있습니다.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(loaded.CredentialsPath) && !File.Exists(loaded.CredentialsPath))
            {
                error = $"서비스 계정 키 파일을 찾을 수 없습니다: {loaded.CredentialsPath}";
                return false;
            }

            config = loaded;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>설정이 없을 때 화면에 보여줄 예시. 그대로 복사해 값만 바꾸면 된다.</summary>
    public static string SampleJson(string credentialsPathHint) =>
        $$"""
        {
          "projectId": "여기에-GCP-프로젝트-ID",
          "collection": "licenses",
          "credentialsPath": "{{credentialsPathHint.Replace("\\", "\\\\")}}"
        }
        """;
}
