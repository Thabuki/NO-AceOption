param([Parameter(Mandatory = $true)][string]$GameDir)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Build-Local.ps1') -GameDir $GameDir
& (Join-Path $PSScriptRoot 'tests\Verify-Build.ps1') -GameDir $GameDir
[xml]$project = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'AceOption\AceOption.csproj') -Raw
$version = [string]($project.Project.PropertyGroup | Where-Object Version | Select-Object -First 1).Version
$dist = Join-Path $PSScriptRoot 'dist'
$stage = Join-Path $dist "AceOption-$version"
$pluginDir = Join-Path $stage 'BepInEx\plugins\AceOption'
New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null
$dll = Join-Path $PSScriptRoot 'AceOption\bin\Release\net472\AceOption.dll'
Copy-Item -LiteralPath $dll -Destination $pluginDir -Force
foreach ($name in @('README.md', 'LICENSE', 'THIRD-PARTY-NOTICES.md')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $stage -Force
}
$docsDir = Join-Path $stage 'docs'
New-Item -ItemType Directory -Path $docsDir -Force | Out-Null
# Remove the retired document if this version's staging directory is reused.
$retiredDoc = Join-Path $docsDir 'VALIDATION.md'
if (Test-Path -LiteralPath $retiredDoc -PathType Leaf) {
    Remove-Item -LiteralPath $retiredDoc -Force
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\GUIDE.md') -Destination $docsDir -Force
$lines = @("Ace Option $version", "Built UTC: $([DateTime]::UtcNow.ToString('o'))", 'Offline verification passed. In-game, hardware and multiplayer checks are separate.')
foreach ($path in @($dll, (Join-Path $GameDir 'NuclearOption_Data\Managed\Assembly-CSharp.dll'),
    (Join-Path $GameDir 'BepInEx\core\BepInEx.dll'), (Join-Path $GameDir 'BepInEx\core\0Harmony.dll'))) {
    $hash = Get-FileHash -LiteralPath $path -Algorithm SHA256
    $lines += "$([IO.Path]::GetFileName($path)): $($hash.Hash)"
}
$lines | Set-Content -LiteralPath (Join-Path $stage 'BUILD-INFO.txt') -Encoding UTF8
$zip = Join-Path $dist "AceOption-$version.zip"
# Explicit paths keep unrelated staging files out of the package on repeated builds.
Compress-Archive -LiteralPath (Join-Path $stage 'BepInEx'),(Join-Path $stage 'README.md'),
    (Join-Path $stage 'docs'),
    (Join-Path $stage 'LICENSE'),(Join-Path $stage 'THIRD-PARTY-NOTICES.md'),
    (Join-Path $stage 'BUILD-INFO.txt') -DestinationPath $zip -Force
Write-Output "Packaged $zip"
