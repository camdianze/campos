# 미리보기 JSON을 서버로 실제로 보내 본다. 2단계의 검증이다.
#
# 앱은 아직 전송 기능이 없으므로(3단계), 서버가 제대로 받는지는 1단계가 만든
# 파일을 손으로 보내 확인한다. 보내는 내용이 3단계에서 앱이 보낼 것과 글자
# 하나까지 같다 — 같은 SyncJson 설정으로 쓰인 파일이기 때문이다.
#
#   .\test-upload.ps1 -Url https://xxxx.supabase.co -AnonKey eyJ... -Token abc... `
#                     -File "$env:USERPROFILE\Desktop\sync-preview-20261010-101500.json"
#
# 두 번 실행해서 서버의 행 수가 늘지 않는지 보는 것이 이 확인의 핵심이다.
# 끊긴 연결에서 재전송은 늘 일어난다.

param(
    [Parameter(Mandatory = $true)][string]$Url,
    [Parameter(Mandatory = $true)][string]$AnonKey,
    [Parameter(Mandatory = $true)][string]$Token,
    [Parameter(Mandatory = $true)][string]$File
)

if (-not (Test-Path $File)) {
    Write-Host "파일이 없습니다: $File" -ForegroundColor Red
    exit 1
}

# 보내기 전에 파일이 무엇을 담고 있는지 센다. 응답의 accepted와 맞아야 한다.
$json = Get-Content $File -Raw -Encoding UTF8
$payload = $json.TrimStart([char]0xFEFF) | ConvertFrom-Json

Write-Host ""
Write-Host "보낼 파일: $(Split-Path $File -Leaf)  ($([math]::Round((Get-Item $File).Length / 1KB))KB)" -ForegroundColor Cyan
Write-Host ("  app_version      : " + $payload.app_version)
Write-Host ("  products         : " + $payload.products.Count)
Write-Host ("  inventory        : " + $payload.inventory.Count)
Write-Host ("  users            : " + $payload.users.Count)
Write-Host ("  transactions     : " + $payload.transactions.Count)
Write-Host ("  counselling_logs : " + $payload.counselling_logs.Count)
Write-Host ""

$endpoint = $Url.TrimEnd('/') + "/functions/v1/sync"

try {
    $response = Invoke-RestMethod -Method Post -Uri $endpoint -Body ([System.IO.File]::ReadAllBytes($File)) -Headers @{
        # Supabase 게이트웨이는 경로에 따라 둘 중 하나를 본다. 둘 다 보내면
        # 어느 쪽을 보든 통과한다 — anon 키는 비밀이 아니므로 잃을 것이 없다.
        "Authorization"   = "Bearer $AnonKey"
        "apikey"          = $AnonKey
        "x-campos-token"  = $Token
        "content-type"    = "application/json"
    }

    Write-Host "성공" -ForegroundColor Green
    Write-Host ""
    $response | ConvertTo-Json -Depth 5 | Write-Host
    Write-Host ""

    # 보낸 수와 받은 수가 다르면 조용히 넘어가서는 안 된다.
    $expect = @{
        product           = $payload.products.Count
        inventory         = $payload.inventory.Count
        app_user          = $payload.users.Count
        stock_transaction = $payload.transactions.Count
        counselling_log   = $payload.counselling_logs.Count
    }

    $mismatch = $false
    foreach ($table in $expect.Keys) {
        $got = $response.accepted.$table
        if ($got -ne $expect[$table]) {
            Write-Host "불일치 $table : 보냄 $($expect[$table]) / 받음 $got" -ForegroundColor Red
            $mismatch = $true
        }
    }

    if (-not $mismatch) {
        Write-Host "보낸 건수와 받은 건수가 전부 일치합니다." -ForegroundColor Green
        Write-Host "이제 같은 명령을 한 번 더 실행하고, SQL에서 count(*)가 늘지 않는지 보세요." -ForegroundColor Yellow
    }
}
catch {
    Write-Host "실패" -ForegroundColor Red
    Write-Host ""

    $webResponse = $_.Exception.Response

    if ($null -ne $webResponse) {
        $reader = New-Object System.IO.StreamReader($webResponse.GetResponseStream())
        $body = $reader.ReadToEnd()
        Write-Host ("HTTP " + [int]$webResponse.StatusCode)
        Write-Host $body
    }
    else {
        Write-Host $_.Exception.Message
    }

    exit 1
}
