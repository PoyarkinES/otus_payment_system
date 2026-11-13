param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root "artifacts/packages"

New-Item -ItemType Directory -Force -Path $out | Out-Null
dotnet pack (Join-Path $root "Shared/src/Otus.PaymentSystem.SharedKernel/Otus.PaymentSystem.SharedKernel.csproj") -c $Configuration -o $out
if ($LASTEXITCODE -ne 0) { throw "SharedKernel pack failed" }

dotnet pack (Join-Path $root "Shared/src/Otus.PaymentSystem.AspNetCore/Otus.PaymentSystem.AspNetCore.csproj") -c $Configuration -o $out
if ($LASTEXITCODE -ne 0) { throw "AspNetCore pack failed" }

Write-Host "Packages: $out"
Get-ChildItem $out -Filter *.nupkg | Select-Object Name, Length
