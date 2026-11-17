using CamPOS.Application.Sync;

namespace CamPOS.Tests.Sync;

/// <summary>
/// 전송이 실패했을 때 화면에 적히는 한 줄.
///
/// 이 테스트가 있는 이유는 실제로 일어난 일 때문이다. Supabase 프로젝트가 잠들었다
/// 깨어나는 동안 Edge Function은 떠 있고 그 뒤의 데이터베이스는 아직이어서,
/// 드라이버가 받은 Cloudflare의 521 오류 <b>페이지</b>가 함수의 메시지에 이어 붙어
/// 정상적인 JSON으로 돌아왔다. 파싱은 성공했고, 앱은 "서버가 보낸 사유를 그대로
/// 전한다"는 규칙대로 HTML 한 쪽을 Data Center 화면에 그렸다.
///
/// 약국이 보기에 그것은 오류 메시지가 아니라 망가진 화면이고, 무엇을 해야 하는지도
/// 말해 주지 않는다.
/// </summary>
public class SyncFailureMessageTests
{
    private const string CloudflarePage =
        """
        <!DOCTYPE html>
        <html class="no-js" lang="en-US">
        <head><title>supabase.co | 521: Web server is down</title></head>
        <body><div id="cf-wrapper"><h1>Web server is down</h1></div></body>
        </html>
        """;

    // ── 화면에 나가면 안 되는 것 ────────────────────────────────────────────

    /// <summary>
    /// 서버가 보낸 <c>error</c>가 정상적인 JSON 안에 들어 있어도, 내용이 HTML이면
    /// 쓰지 않는다. "JSON이 아니면 버린다"는 검사로는 이것이 걸리지 않는다 —
    /// 실제로 걸리지 않아서 화면에 나왔다.
    /// </summary>
    [Fact]
    public void AnErrorFieldCarryingAWholeWebPageIsNotShown()
    {
        var body = System.Text.Json.JsonSerializer.Serialize(new
        {
            ok = false,
            error = "The server could not check this device: " + CloudflarePage,
        });

        var message = HttpSyncClient.DescribeFailure(521, body);

        Assert.DoesNotContain("<", message, StringComparison.Ordinal);
        Assert.DoesNotContain("DOCTYPE", message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("<!DOCTYPE html><html><body>down</body></html>")]
    [InlineData("<html lang=\"en\">Web server is down</html>")]
    [InlineData("error: <div id=\"cf-wrapper\">521</div>")]
    [InlineData("<script>alert(1)</script>")]
    public void AnythingThatLooksLikeMarkupIsDropped(string served)
    {
        Assert.Equal(string.Empty, HttpSyncClient.Tidy(served));
    }

    /// <summary>
    /// 길이로만 잘라서는 안 된다. HTML의 첫 200자를 보여 주는 것은 오류 메시지가
    /// 아니라 깨진 화면이다.
    /// </summary>
    [Fact]
    public void MarkupIsDroppedRatherThanTruncated()
    {
        var message = HttpSyncClient.Tidy(CloudflarePage);

        Assert.Equal(string.Empty, message);
    }

    // ── 대신 나가는 것 ──────────────────────────────────────────────────────

    /// <summary>
    /// 버린 자리는 상태 코드가 채운다. 약국이 할 일이 적혀 있어야 한다 —
    /// 깨어나는 중인 서버에 할 수 있는 일은 기다리는 것뿐이다.
    /// </summary>
    [Theory]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    [InlineData(521)]
    public void AGatewayFailureSaysToWaitAndTryAgain(int status)
    {
        var message = HttpSyncClient.DescribeFailure(status, "<!DOCTYPE html>");

        Assert.Contains("try again", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<", message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownServerErrorStillNamesItsCode()
    {
        var message = HttpSyncClient.DescribeFailure(500, "not json at all");

        Assert.Contains("500", message, StringComparison.Ordinal);
    }

    // ── 쓸 만한 메시지는 그대로 ─────────────────────────────────────────────

    /// <summary>
    /// 함수가 돌려주는 문구는 무엇을 고쳐야 하는지 이미 말한다. 뭉개면 그 정보가
    /// 사라지므로, 쓸 만한 모양이면 손대지 않는다.
    /// </summary>
    [Fact]
    public void APlainServerMessageIsPassedThroughUnchanged()
    {
        var body = System.Text.Json.JsonSerializer.Serialize(new
        {
            error = "The server does not know the field \"notes\". Update the server first.",
        });

        Assert.Equal(
            "The server does not know the field \"notes\". Update the server first.",
            HttpSyncClient.DescribeFailure(400, body));
    }

    [Fact]
    public void AMessageWithNoBodyFallsBackToTheStatusText()
    {
        Assert.Contains("device token", HttpSyncClient.DescribeFailure(401, string.Empty),
            StringComparison.OrdinalIgnoreCase);

        Assert.Contains("server address", HttpSyncClient.DescribeFailure(404, string.Empty),
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 여러 줄짜리 서버 오류는 뒤쪽이 스택 추적인 경우가 많다. 첫 줄이 사유다.
    /// </summary>
    [Fact]
    public void OnlyTheFirstLineIsKept()
    {
        var served = "connection refused\n  at Object.connect (node:net:1234)\n  at async run";

        Assert.Equal("connection refused", HttpSyncClient.Tidy(served));
    }

    /// <summary>아주 긴 한 줄은 자른다. 화면 한 칸에 들어가야 한다.</summary>
    [Fact]
    public void AVeryLongLineIsTrimmedWithAnEllipsis()
    {
        var message = HttpSyncClient.Tidy(new string('a', 500));

        Assert.True(message.Length <= 201);
        Assert.EndsWith("…", message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\n")]
    public void NothingUsefulBecomesNothing(string served)
    {
        Assert.Equal(string.Empty, HttpSyncClient.Tidy(served));
    }
}
