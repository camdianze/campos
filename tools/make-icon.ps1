# 앱 아이콘을 다시 만든다.
#
#   .\tools\make-icon.ps1
#
# 아이콘은 브랜드 색 두 개와 저장소에 이미 들어 있는 글꼴에서 나온다. 손으로 그린
# 그림이 아니라서, 메인 색이 바뀌면 이 스크립트를 다시 돌리면 된다 — 색을 다섯 번
# 바꾸는 동안 아이콘만 옛 색으로 남는 일을 막으려고 둔 것이다.
#
#   바탕  #44705D  PrimaryBrush (Pantone 553 C)
#   글자  #ECEADC  PageBackgroundBrush
#   글꼴  Fonts/AbrilFatface-Regular.ttf — 로그인 화면의 CAMDIANS와 같은 것
#
# 색을 바꾸려면 MakeIcon.cs 위쪽의 Back/Fore 두 줄을 App.xaml의 값과 맞춘다.

param(
    [string]$Output = (Join-Path $PSScriptRoot "..\CamPOS.Wpf\CamdiansRX.ico")
)

$ErrorActionPreference = "Stop"

$source = Join-Path $PSScriptRoot "MakeIcon.cs"
$font   = Join-Path $PSScriptRoot "..\CamPOS.Wpf\Fonts\AbrilFatface-Regular.ttf"

if (-not (Test-Path $font)) { throw "글꼴을 찾지 못했습니다: $font" }

# PowerShell 5.1은 .NET Framework 위에서 돈다. System.Drawing.Common이 아니라
# System.Drawing이고, 둘을 바꿔 쓰면 메타데이터를 찾지 못한다는 오류가 난다.
# 같은 세션에서 두 번 돌릴 때 형식 이름이 겹치지 않도록 무작위 접미사를 붙인다.
$suffix = -join ((48..57) + (97..122) | Get-Random -Count 8 | ForEach-Object { [char]$_ })
$type   = "MakeIcon_$suffix"
$code   = (Get-Content $source -Raw) -replace 'class MakeIcon\b', "class $type"

Add-Type -TypeDefinition $code -ReferencedAssemblies System.Drawing

# 윈도우가 자리마다 다른 크기를 집어 간다. 목록에 없는 크기는 가까운 것을 늘려
# 쓰는데, 그러면 흐릿해진다 — 작업표시줄·바탕화면·탐색기가 쓰는 크기를 모두 넣는다.
$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)

$full = [System.IO.Path]::GetFullPath($Output)
[void]([type]$type)::Write($full, $sizes, [System.IO.Path]::GetFullPath($font))

$file = Get-Item $full
Write-Host ""
Write-Host ("  {0}  ({1:N0} KB, {2}장)" -f $file.Name, ($file.Length / 1KB), $sizes.Count) -ForegroundColor Green
Write-Host ""
Write-Host "  이 파일을 쓰는 곳:" -ForegroundColor DarkGray
Write-Host "    CamPOS.Wpf.csproj  <ApplicationIcon>  -> exe, 창 제목 표시줄"
Write-Host "    deploy.ps1         vpk --icon         -> Setup.exe, 바탕화면 바로가기"
Write-Host ""
