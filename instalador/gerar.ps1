# Gera tudo o que vai numa Release, na pasta release\:
#   Bastion-win-x64.exe, Bastion-win-arm64.exe   instaladores (aceitam /S)
#   Bastion-win-x64-portable.exe                 versão portátil, sem instalar
#   latest.yml                                   SHA-512 que o Vitall Launcher confere
# Precisa do .NET 8 SDK e do NSIS (makensis).
$ErrorActionPreference = 'Stop'
$raiz = Split-Path $PSScriptRoot -Parent
$versao = ([xml](Get-Content "$raiz\Bastion.csproj")).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$dist = Join-Path $raiz 'release'
$publicar = Join-Path $raiz 'publicar'
Remove-Item $dist, $publicar -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $dist | Out-Null

$makensis = (Get-Command makensis -ErrorAction SilentlyContinue).Source
if (-not $makensis) { $makensis = @("${env:ProgramFiles(x86)}\NSIS\makensis.exe", "$env:ProgramFiles\NSIS\makensis.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1 }
if (-not $makensis) { throw 'NSIS (makensis) não encontrado.' }

foreach ($arq in 'x64', 'arm64') {
    Write-Host "== Publicando $arq (versão $versao)"
    dotnet publish "$raiz\Bastion.csproj" -c Release -r "win-$arq" -o "$publicar\$arq" -nologo
    if ($LASTEXITCODE) { throw "dotnet publish $arq falhou" }
    Write-Host "== Instalador $arq"
    & $makensis /V2 "/DVERSAO=$versao" "/DARQ=$arq" "/DORIGEM=$publicar\$arq\Bastion.exe" "/DSAIDA=$dist\Bastion-win-$arq.exe" "$PSScriptRoot\bastion.nsi"
    if ($LASTEXITCODE) { throw "makensis $arq falhou" }
}
Copy-Item "$publicar\x64\Bastion.exe" "$dist\Bastion-win-x64-portable.exe"
& "$PSScriptRoot\latest-yml.ps1" -Versao $versao -Pasta $dist | Out-Null
Get-ChildItem $dist | Select-Object Name, @{ n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } } | Format-Table -AutoSize
