param(
    [Parameter(ParameterSetName = 'Bump', Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$')]
    [string]$Version,

    [Parameter(ParameterSetName = 'Check', Mandatory = $true)]
    [switch]$Check,

    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$propsPath = Join-Path $root 'Shared/Directory.Build.props'
if (-not (Test-Path $propsPath)) {
    throw "Не найден $propsPath"
}

$props = [System.IO.File]::ReadAllText($propsPath)
$versionMatch = [regex]::Match($props, '<Version>([^<]+)</Version>')
if (-not $versionMatch.Success) {
    throw "В $propsPath нет <Version>...</Version>"
}

$targetVersion = if ($Check) { $versionMatch.Groups[1].Value } else { $Version }
$pattern = '(<PackageReference\s+Include="Otus\.PaymentSystem\.(?:SharedKernel|AspNetCore)"\s+Version=")([^"]+)(")'
$utf8 = New-Object System.Text.UTF8Encoding($false)
$files = Get-ChildItem -Path $root -Recurse -File -Filter '*.csproj' |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }

$references = 0
$mismatches = @()
$changedFiles = @()

foreach ($file in $files) {
    $text = [System.IO.File]::ReadAllText($file.FullName)
    $matches = [regex]::Matches($text, $pattern)
    if ($matches.Count -eq 0) { continue }

    $references += $matches.Count

    if ($Check) {
        foreach ($match in $matches) {
            if ($match.Groups[2].Value -ne $targetVersion) {
                $mismatches += "$($file.FullName): $($match.Groups[2].Value) != $targetVersion"
            }
        }
        continue
    }

    $updated = [regex]::Replace(
        $text,
        $pattern,
        { param($match) $match.Groups[1].Value + $targetVersion + $match.Groups[3].Value }
    )
    if ($updated -ne $text) {
        $changedFiles += $file.FullName
        if (-not $DryRun) {
            [System.IO.File]::WriteAllText($file.FullName, $updated, $utf8)
        }
    }
}

if ($references -eq 0) {
    throw "Не найдено ни одной PackageReference на Otus.PaymentSystem.SharedKernel/AspNetCore"
}

if ($Check) {
    if ($mismatches.Count -gt 0) {
        Write-Host "Версия Shared в Directory.Build.props: $targetVersion" -ForegroundColor Yellow
        Write-Host "Несовпадения:" -ForegroundColor Red
        $mismatches | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
        exit 1
    }
    Write-Host "OK: все $references PackageReference указывают на $targetVersion" -ForegroundColor Green
    exit 0
}

if (-not $DryRun) {
    $updatedProps = [regex]::Replace(
        $props,
        '<Version>[^<]+</Version>',
        "<Version>$targetVersion</Version>",
        1
    )
    [System.IO.File]::WriteAllText($propsPath, $updatedProps, $utf8)
}

$prefix = if ($DryRun) { 'Будет обновлено' } else { 'Обновлено' }
Write-Host "${prefix}: $($changedFiles.Count) csproj, $references PackageReference -> $targetVersion"
$changedFiles | ForEach-Object { Write-Host "  $_" }

if (-not $DryRun) {
    Write-Host "Проверка:" -ForegroundColor Cyan
    & $PSCommandPath -Check
}
