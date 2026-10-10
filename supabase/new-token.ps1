# 약국 PC용 동기화 토큰을 하나 만든다.
#
# 토큰 원본은 이 창에만 보이고 어디에도 저장되지 않는다. SQL에는 SHA-256만
# 들어가므로, Supabase SQL Editor의 쿼리 기록에 토큰 원본이 남지 않는다 —
# 그 기록은 지우기 번거롭고, 지웠는지 확인할 방법도 없다.
#
#   .\new-token.ps1 -SiteId KH-0001 -Label "약국 PC"

param(
    [Parameter(Mandatory = $true)][string]$SiteId,
    [string]$Label = "Pharmacy PC"
)

function ToHex([byte[]]$bytes) {
    return ($bytes | ForEach-Object { '{0:x2}' -f $_ }) -join ''
}

# 암호학적 난수. Get-Random은 이 용도에 쓰지 않는다 — 예측 가능한 시드에서 나온다.
$raw = New-Object byte[] 32
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($raw)
$token = ToHex $raw

$sha = [System.Security.Cryptography.SHA256]::Create()
$hash = ToHex $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($token))

Write-Host ""
Write-Host "토큰 (한 번만 보입니다 - 비밀번호 관리자에 저장하세요)" -ForegroundColor Yellow
Write-Host ""
Write-Host "    $token" -ForegroundColor White
Write-Host ""
Write-Host "서버는 아래 해시만 가집니다. 토큰을 잃으면 다시 발급해야 합니다." -ForegroundColor DarkGray
Write-Host ""
Write-Host "─── Supabase SQL Editor에 붙여넣기 ───────────────────────────" -ForegroundColor Cyan
Write-Host ""

@"
insert into public.site (site_id, label)
values ('$SiteId', '$Label')
on conflict (site_id) do nothing;

insert into public.site_token (token_sha256, site_id, label)
values ('$hash', '$SiteId', '$Label')
on conflict (token_sha256) do nothing;
"@ | Write-Host

Write-Host ""
Write-Host "───────────────────────────────────────────────────────────────" -ForegroundColor Cyan
Write-Host ""
