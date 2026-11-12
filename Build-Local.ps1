param(
    [Parameter(Mandatory = $true)][string]$GameDir,
    [string]$Compiler = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe',
    [string]$FrameworkDir = 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2'
)
$ErrorActionPreference = 'Stop'
$projectDir = Join-Path $PSScriptRoot 'AceOption'
$buildDir = Join-Path $projectDir 'obj\local'
$outputDir = Join-Path $projectDir 'bin\Release\net472'
[xml]$project = Get-Content -LiteralPath (Join-Path $projectDir 'AceOption.csproj') -Raw
$version = [string]($project.Project.PropertyGroup | Where-Object Version | Select-Object -First 1).Version
$title = [string]($project.Project.PropertyGroup | Where-Object Title | Select-Object -First 1).Title
$description = [string]($project.Project.PropertyGroup | Where-Object Description | Select-Object -First 1).Description
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "Expected a three-part project version: $version" }
New-Item -ItemType Directory -Path $buildDir,$outputDir -Force | Out-Null
$generated = Join-Path $buildDir 'AssemblyInfo.cs'
@'
using System.Reflection;
[assembly: AssemblyVersion("__VERSION__.0")]
[assembly: AssemblyFileVersion("__VERSION__.0")]
[assembly: AssemblyInformationalVersion("__VERSION__")]
[assembly: AssemblyTitle(@"__TITLE__")]
[assembly: AssemblyDescription(@"__DESCRIPTION__")]
'@ | ForEach-Object {
    $_.Replace('__VERSION__', $version).Replace('__TITLE__', $title.Replace('"', '""')).Replace('__DESCRIPTION__', $description.Replace('"', '""'))
} | Set-Content -LiteralPath $generated -Encoding UTF8
$references = @(
    (Join-Path $FrameworkDir 'mscorlib.dll'),
    (Join-Path $FrameworkDir 'System.dll'),
    (Join-Path $FrameworkDir 'System.Core.dll'),
    (Join-Path $FrameworkDir 'Facades\netstandard.dll'),
    (Join-Path $GameDir 'BepInEx\core\BepInEx.dll'),
    (Join-Path $GameDir 'BepInEx\core\0Harmony.dll')
)
foreach ($name in @('Assembly-CSharp','Mirage','Rewired_Core','UnityEngine',
    'UnityEngine.CoreModule','UnityEngine.PhysicsModule','UnityEngine.AudioModule',
    'UnityEngine.InputLegacyModule','UnityEngine.IMGUIModule','UnityEngine.UI','UnityEngine.UIModule','Unity.TextMeshPro')) {
    $references += Join-Path $GameDir "NuclearOption_Data\Managed\$name.dll"
}
foreach ($path in @($Compiler) + $references) {
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Required file not found: $path" }
}
$sources = @((Join-Path $projectDir 'Plugin.cs'))
$sources += Get-ChildItem -Path (Join-Path $projectDir 'Helpers'),(Join-Path $projectDir 'Patches') -Filter '*.cs' | ForEach-Object FullName
$sources += $generated
$output = Join-Path $outputDir 'AceOption.dll'
$arguments = @('/nologo','/target:library','/langversion:latest','/optimize+',
    '/deterministic+','/unsafe+','/nostdlib+',"/out:$output")
$arguments += $references | ForEach-Object { "/reference:$_" }
$arguments += $sources
& $Compiler @arguments
if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $LASTEXITCODE" }
Write-Output "Built $output"
