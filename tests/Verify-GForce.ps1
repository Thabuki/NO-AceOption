param(
    [Parameter(Mandatory = $true)][string]$GameDir,
    [string]$PluginDll,
    [switch]$OptionalCombatOnly,
    [switch]$AbsoluteThrottleOnly,
    [string]$Compiler = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe',
    [string]$FrameworkDir = 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2'
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (!$PluginDll) { $PluginDll = Join-Path $repo 'AceOption\bin\Release\net472\AceOption.dll' }
$testDir = Join-Path $repo 'AceOption\obj\local'
New-Item -ItemType Directory -Path $testDir -Force | Out-Null
$testExe = Join-Path $testDir 'TranspilerTests.exe'
$references = @(
    (Join-Path $GameDir 'BepInEx\core\0Harmony.dll'),
    (Join-Path $GameDir 'BepInEx\core\BepInEx.dll'),
    (Join-Path $GameDir 'BepInEx\core\Mono.Cecil.dll'),
    (Join-Path $GameDir 'NuclearOption_Data\Managed\Assembly-CSharp.dll'),
    (Join-Path $GameDir 'NuclearOption_Data\Managed\Rewired_Core.dll'),
    (Join-Path $GameDir 'NuclearOption_Data\Managed\Rewired_Core.dll'),
    (Join-Path $GameDir 'NuclearOption_Data\Managed\UnityEngine.CoreModule.dll'),
    (Join-Path $GameDir 'NuclearOption_Data\Managed\UnityEngine.PhysicsModule.dll'),
    (Join-Path $FrameworkDir 'Facades\netstandard.dll')
)
$arguments = @('/nologo','/target:exe','/langversion:latest',"/out:$testExe")
$arguments += $references | ForEach-Object { "/reference:$_" }
$arguments += Join-Path $PSScriptRoot 'TranspilerTests.cs'
& $Compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Transpiler test compilation failed' }
if ($AbsoluteThrottleOnly) { & $testExe $GameDir $PluginDll '--absolute-throttle' }
elseif ($OptionalCombatOnly) { & $testExe $GameDir $PluginDll '--optional-combat' }
else { & $testExe $GameDir $PluginDll }
if ($LASTEXITCODE -ne 0) { throw 'Transpiler tests failed' }
