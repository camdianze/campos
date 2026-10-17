# CamPOS 새 버전 만들기 — 버전 올림 → 빌드 → Velopack 패키지 → 올릴 파일 목록
#
#   .\deploy.ps1 -Version 1.27
#   .\deploy.ps1 -Version 1.27 -Channel test
#
# 이 스크립트는 <b>만들기만</b> 한다. 업로드는 하지 않는다 — Supabase 대시보드에서
# 사람이 직접 올린다. 업로드 열쇠를 저장소에 두면, 저장소를 보는 사람 누구나
# 모든 약국에 아무 버전이나 보낼 수 있게 된다.

param(
    [Parameter(Mandatory = $true)][string]$Version,
    [ValidateSet("test", "stable")][string]$Channel = "test",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$publish = Join-Path $env:TEMP "campos-publish"
$output = Join-Path $root "releases"

function Step($text) { Write-Host "`n== $text" -ForegroundColor Cyan }

# ── 버전 번호는 한 곳에서만 온다 ────────────────────────────────────────
# AppVersion.Number가 화면·내보낸 파일 이름·패키지 버전을 모두 정한다. 패키지만
# 따로 올리면 약국 화면의 버전과 실제로 설치된 것이 달라지고, 그 상태는 전화로
# 확인할 수단을 없앤다.
Step "버전을 $Version 으로"

$appVersion = Join-Path $root "CamPOS.Application\AppVersion.cs"
$text = Get-Content $appVersion -Raw

if ($text -notmatch 'Number = "([^"]+)"') {
    throw "AppVersion.cs에서 버전 줄을 찾지 못했습니다."
}

$previous = $Matches[1]
Write-Host "   $previous -> $Version"

if ($previous -ne $Version) {
    ($text -replace 'Number = "[^"]+"', "Number = `"$Version`"") |
        Set-Content $appVersion -Encoding UTF8 -NoNewline
}

# Velopack은 3자리 SemVer를 요구한다. 화면에는 "CamPOS v.1.27"로 두 자리가 나가는데,
# 약국이 읽을 숫자에 의미 없는 .0을 붙일 이유가 없어서다. 패키지 쪽만 채워 준다 —
# 둘 다 AppVersion.Number에서 나오므로 어긋날 수가 없다.
$packVersion = if ($Version -match '^\d+\.\d+$') { "$Version.0" } else { $Version }
if ($packVersion -ne $Version) { Write-Host "   패키지 버전: $packVersion" }

# ── 빌드 ────────────────────────────────────────────────────────────────
# 프레임워크 의존으로 게시한다. 자체포함이면 패키지가 64MB가 되어 Supabase
# 무료 요금제의 파일 한도(50MB)를 넘고, 느린 회선에서 받을 양도 그만큼 커진다.
# 런타임은 설치 프로그램이 Microsoft에서 받아 깔아 준다(--framework).
if (-not $SkipBuild) {
    Step "테스트"
    & dotnet test (Join-Path $root "CamPOS.Tests") --nologo --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw "테스트가 실패했습니다. 올리지 마세요." }

    Step "게시 (프레임워크 의존)"
    if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }

    & dotnet publish (Join-Path $root "CamPOS.Wpf") `
        -c Release -r win-x64 --self-contained false `
        -p:PublishSingleFile=false -p:DebugType=none `
        -o $publish --nologo
    if ($LASTEXITCODE -ne 0) { throw "게시가 실패했습니다." }
}

# ── 패키지 ──────────────────────────────────────────────────────────────
Step "Velopack 패키지"

# 코드 서명 인증서가 생기면 아래 두 줄의 주석을 풀고 값을 채운다. 서명이 없으면
# 첫 설치에서 SmartScreen 경고가 뜨지만, 설치된 뒤의 업데이트에는 영향이 없다.
# $signParams = "/a /f `"$root\cert.pfx`" /p <비밀번호> /fd sha256 /tr http://timestamp.digicert.com /td sha256"
$signArgs = @()
# $signArgs = @("--signParams", $signParams)

& vpk pack `
    --packId CamPOS `
    --packVersion $packVersion `
    --packDir $publish `
    --mainExe CamPOS.exe `
    --packTitle "CamPOS" `
    --channel $Channel `
    --framework net10.0-x64-desktop `
    --runtime win-x64 `
    --outputDir $output `
    @signArgs

if ($LASTEXITCODE -ne 0) { throw "패키지 만들기가 실패했습니다." }

# ── 올릴 것 ─────────────────────────────────────────────────────────────
Step "Supabase Storage의 releases/$Channel/ 에 올릴 파일"

$files = Get-ChildItem $output -File |
    Where-Object { $_.LastWriteTime -gt (Get-Date).AddMinutes(-10) } |
    Sort-Object Length -Descending

$total = 0
foreach ($f in $files) {
    $mb = $f.Length / 1MB
    $total += $mb
    $warn = if ($mb -gt 50) { "  <-- 50MB 초과!" } else { "" }
    Write-Host ("   {0,-46} {1,7:N1} MB{2}" -f $f.Name, $mb, $warn) `
        -ForegroundColor $(if ($mb -gt 50) { "Red" } else { "White" })
}

Write-Host ("`n   합계 {0:N1} MB" -f $total) -ForegroundColor DarkGray
Write-Host "   폴더: $output" -ForegroundColor DarkGray

Write-Host "`n다음 순서:" -ForegroundColor Yellow
Write-Host "   1. 위 파일을 Supabase Storage -> releases -> $Channel/ 에 올린다"
Write-Host "      (RELEASES 파일을 반드시 포함한다. 그것이 목록이고, 없으면 아무도 새 버전을 못 본다)"
if ($Channel -eq "test") {
    Write-Host "   2. 본인 PC와 약국 한 곳에서 확인한다"
    Write-Host "   3. 확인되면 같은 파일을 stable/ 에 올린다"
} else {
    Write-Host "   2. 올린 뒤에는 되돌릴 수 없다. 문제가 있으면 더 높은 번호로 수정판을 올린다"
}
Write-Host ""
