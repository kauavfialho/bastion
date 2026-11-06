# Gera o latest.yml no mesmo formato do electron-builder: o Vitall Launcher lê o SHA-512 (base64)
# de cada instalador e confere o download antes de instalar.
#
# Uso: .\latest-yml.ps1 -Versao 1.0.0 -Pasta ..\dist
param(
    [Parameter(Mandatory)] [string]$Versao,
    [Parameter(Mandatory)] [string]$Pasta
)
$ErrorActionPreference = 'Stop'
$instaladores = Get-ChildItem $Pasta -File | Where-Object { $_.Name -match '^Bastion-win-(x64|arm64)\.exe$' } | Sort-Object Name -Descending
if (-not $instaladores) { throw "Nenhum instalador Bastion-win-*.exe em $Pasta" }

function Sha512Base64([string]$caminho) {
    $sha = [System.Security.Cryptography.SHA512]::Create()
    $fs = [IO.File]::OpenRead($caminho)
    try { [Convert]::ToBase64String($sha.ComputeHash($fs)) } finally { $fs.Dispose(); $sha.Dispose() }
}

$linhas = @("version: $Versao", 'files:')
$principal = $null
foreach ($i in $instaladores) {
    $hash = Sha512Base64 $i.FullName
    $linhas += "  - url: $($i.Name)"
    $linhas += "    sha512: $hash"
    $linhas += "    size: $($i.Length)"
    if ($i.Name -eq 'Bastion-win-x64.exe') { $principal = @{ Nome = $i.Name; Hash = $hash } }
}
if (-not $principal) { $principal = @{ Nome = $instaladores[0].Name; Hash = (Sha512Base64 $instaladores[0].FullName) } }
$linhas += "path: $($principal.Nome)"
$linhas += "sha512: $($principal.Hash)"
$linhas += "releaseDate: '$((Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ss.fffZ'))'"

$saida = Join-Path $Pasta 'latest.yml'
[IO.File]::WriteAllText($saida, ($linhas -join "`n") + "`n", (New-Object System.Text.UTF8Encoding($false)))
Get-Content $saida
