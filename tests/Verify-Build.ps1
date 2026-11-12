param([Parameter(Mandatory = $true)][string]$GameDir)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$compiler = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe'
$testExe = Join-Path $repo 'AceOption\obj\local\FreeLookOrbitTests.exe'
& $compiler /nologo /target:exe /langversion:latest "/out:$testExe" `
    (Join-Path $repo 'AceOption\Helpers\FreeLookOrbit.cs') `
    (Join-Path $PSScriptRoot 'FreeLookOrbitTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed' }
& $testExe
if ($LASTEXITCODE -ne 0) { throw 'Behavior tests failed' }

$turretTestExe = Join-Path $repo 'AceOption\obj\local\TurretCameraTests.exe'
& $compiler /nologo /target:exe /langversion:latest "/out:$turretTestExe" `
    (Join-Path $repo 'AceOption\Helpers\PatchHelper.cs') `
    (Join-Path $PSScriptRoot 'TurretCameraTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Turret camera test compilation failed' }
& $turretTestExe
if ($LASTEXITCODE -ne 0) { throw 'Turret camera tests failed' }

$hudTestExe = Join-Path $repo 'AceOption\obj\local\HudElementsTests.exe'
& $compiler /nologo /target:exe /langversion:latest "/out:$hudTestExe" `
    (Join-Path $repo 'AceOption\Helpers\HudElements.cs') `
    (Join-Path $PSScriptRoot 'HudElementsTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'HUD test compilation failed' }
& $hudTestExe
if ($LASTEXITCODE -ne 0) { throw 'HUD visibility tests failed' }

$menuTestExe = Join-Path $repo 'AceOption\obj\local\ConfigMenuTests.exe'
& $compiler /nologo /target:exe /langversion:latest "/out:$menuTestExe" `
    "/reference:$(Join-Path $GameDir 'BepInEx\core\0Harmony.dll')" `
    "/reference:$(Join-Path $GameDir 'BepInEx\core\BepInEx.dll')" `
    (Join-Path $repo 'AceOption\Helpers\ConfigMenu.cs') `
    (Join-Path $repo 'AceOption\Patches\ConfigMenuPatch.cs') `
    (Join-Path $PSScriptRoot 'ConfigMenuTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Menu test compilation failed' }
& $menuTestExe $GameDir
if ($LASTEXITCODE -ne 0) { throw 'Menu dependency tests failed' }

$alignmentTestExe = Join-Path $repo 'AceOption\obj\local\HudAlignmentTests.exe'
& $compiler /nologo /target:exe /langversion:latest "/out:$alignmentTestExe" `
    "/reference:$(Join-Path $GameDir 'BepInEx\core\0Harmony.dll')" `
    "/reference:$(Join-Path $GameDir 'BepInEx\core\BepInEx.dll')" `
    (Join-Path $repo 'AceOption\Helpers\HudAlignment.cs') `
    (Join-Path $repo 'AceOption\Helpers\ConfigMenu.cs') `
    (Join-Path $repo 'AceOption\Patches\HudAlignmentPatches.cs') `
    (Join-Path $PSScriptRoot 'HudAlignmentTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'HUD alignment test compilation failed' }
& $alignmentTestExe $GameDir
if ($LASTEXITCODE -ne 0) { throw 'HUD alignment tests failed' }

$cameraTestExe = Join-Path $repo 'AceOption\obj\local\CameraOverlayTests.exe'
& $compiler /nologo /target:exe /langversion:latest /reference:System.Numerics.dll "/out:$cameraTestExe" `
    (Join-Path $repo 'AceOption\Helpers\TargetFocus.cs') `
    (Join-Path $repo 'AceOption\Helpers\ChasePlacement.cs') `
    (Join-Path $PSScriptRoot 'CameraOverlayTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Camera overlay test compilation failed' }
& $cameraTestExe
if ($LASTEXITCODE -ne 0) { throw 'Camera overlay tests failed' }

$preferenceTestExe = Join-Path $repo 'AceOption\obj\local\CameraPreferenceTests.exe'
& $compiler /nologo /target:exe /langversion:latest "/out:$preferenceTestExe" `
    "/reference:$(Join-Path $GameDir 'BepInEx\core\BepInEx.dll')" `
    (Join-Path $repo 'AceOption\Helpers\CameraPreference.cs') `
    (Join-Path $repo 'AceOption\Helpers\ConfigMenu.cs') `
    (Join-Path $PSScriptRoot 'CameraPreferenceTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Camera preference test compilation failed' }
& $preferenceTestExe $GameDir
if ($LASTEXITCODE -ne 0) { throw 'Camera preference persistence tests failed' }

$markTestExe = Join-Path $repo 'AceOption\obj\local\TargetMarkTests.exe'
& $compiler /nologo /target:exe /langversion:latest "/out:$markTestExe" `
    (Join-Path $repo 'AceOption\Helpers\TargetMark.cs') `
    (Join-Path $PSScriptRoot 'TargetMarkTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Target mark test compilation failed' }
& $markTestExe
if ($LASTEXITCODE -ne 0) { throw 'Target mark tests failed' }

$gunTestExe = Join-Path $repo 'AceOption\obj\local\GunCommandsTests.exe'
& $compiler /nologo /target:exe /langversion:latest "/out:$gunTestExe" `
    (Join-Path $repo 'AceOption\Helpers\GunCommands.cs') `
    (Join-Path $PSScriptRoot 'GunCommandsTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Gun test compilation failed' }
& $gunTestExe
if ($LASTEXITCODE -ne 0) { throw 'Gun input/selection tests failed' }

$missileTestExe = Join-Path $repo 'AceOption\obj\local\MissileCameraTests.exe'
& $compiler /nologo /target:exe /langversion:latest "/out:$missileTestExe" `
    "/reference:$(Join-Path $GameDir 'BepInEx\core\0Harmony.dll')" `
    (Join-Path $repo 'AceOption\Helpers\MissileCamera.cs') `
    (Join-Path $repo 'AceOption\Helpers\InstructionEdits.cs') `
    (Join-Path $repo 'AceOption\Patches\MissileCameraPatches.cs') `
    (Join-Path $PSScriptRoot 'MissileCameraTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Missile camera test compilation failed' }
& $missileTestExe $GameDir
if ($LASTEXITCODE -ne 0) { throw 'Missile single-fire/camera tests failed' }

$yawTestExe = Join-Path $repo 'AceOption\obj\local\YawInputTests.exe'
& $compiler /nologo /target:exe /langversion:latest "/out:$yawTestExe" `
    (Join-Path $repo 'AceOption\Helpers\ButtonYaw.cs') `
    (Join-Path $repo 'AceOption\Helpers\YawCommands.cs') `
    (Join-Path $PSScriptRoot 'YawInputTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Yaw test compilation failed' }
& $yawTestExe
if ($LASTEXITCODE -ne 0) { throw 'Yaw timing/input tests failed' }

$absoluteThrottleTestExe = Join-Path $repo 'AceOption\obj\local\AbsoluteThrottleTests.exe'
& $compiler /nologo /target:exe /langversion:latest "/out:$absoluteThrottleTestExe" `
    "/reference:$(Join-Path $GameDir 'BepInEx\core\0Harmony.dll')" `
    (Join-Path $repo 'AceOption\Helpers\AbsoluteThrottle.cs') `
    (Join-Path $repo 'AceOption\Patches\AbsoluteThrottleInputPatches.cs') `
    (Join-Path $repo 'AceOption\Patches\FlightAssistInputPatches.cs') `
    (Join-Path $PSScriptRoot 'AbsoluteThrottleTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Absolute-throttle test compilation failed' }
& $absoluteThrottleTestExe $GameDir
if ($LASTEXITCODE -ne 0) { throw 'Absolute-throttle tests failed' }

$assistTestExe = Join-Path $repo 'AceOption\obj\local\FlightAssistInputTests.exe'
& $compiler /nologo /target:exe /langversion:latest "/out:$assistTestExe" `
    (Join-Path $repo 'AceOption\Helpers\FlightAssistCommands.cs') `
    (Join-Path $PSScriptRoot 'FlightAssistInputTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Flight-assist test compilation failed' }
& $assistTestExe
if ($LASTEXITCODE -ne 0) { throw 'Throttle-chord assist tests failed' }

$brakeTestExe = Join-Path $repo 'AceOption\obj\local\GroundBrakeInputTests.exe'
& $compiler /nologo /target:exe /langversion:latest "/out:$brakeTestExe" `
    "/reference:$(Join-Path $GameDir 'BepInEx\core\0Harmony.dll')" `
    (Join-Path $repo 'AceOption\Helpers\GroundBrakeCommands.cs') `
    (Join-Path $repo 'AceOption\Helpers\GroundContact.cs') `
    (Join-Path $PSScriptRoot 'GroundBrakeInputTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Ground-brake test compilation failed' }
& $brakeTestExe $GameDir
if ($LASTEXITCODE -ne 0) { throw 'Throttle-down ground-brake tests failed' }

$levelTestExe = Join-Path $repo 'AceOption\obj\local\AutoLevelInputTests.exe'
& $compiler /nologo /target:exe /langversion:latest /reference:System.Numerics.dll "/out:$levelTestExe" `
    (Join-Path $repo 'AceOption\Helpers\HorizonLevel.cs') `
    (Join-Path $repo 'AceOption\Helpers\AutoLevelCommands.cs') `
    (Join-Path $PSScriptRoot 'AutoLevelInputTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Auto-level test compilation failed' }
& $levelTestExe
if ($LASTEXITCODE -ne 0) { throw 'Yaw-chord auto-level tests failed' }

$combatTestExe = Join-Path $repo 'AceOption\obj\local\OptionalCombatTests.exe'
& $compiler /nologo /target:exe /langversion:latest "/out:$combatTestExe" `
    (Join-Path $repo 'AceOption\Helpers\TargetCycling.cs') `
    (Join-Path $repo 'AceOption\Helpers\VibrationEnvelope.cs') `
    (Join-Path $repo 'AceOption\Helpers\ControllerFeedback.cs') `
    (Join-Path $PSScriptRoot 'OptionalCombatTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Optional combat test compilation failed' }
& $combatTestExe
if ($LASTEXITCODE -ne 0) { throw 'Optional targeting/controller feedback tests failed' }

# Read metadata only; do not launch the game or execute its assemblies.
Add-Type -Path (Join-Path $GameDir 'BepInEx\core\Mono.Cecil.dll')
$game = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameDir 'NuclearOption_Data\Managed\Assembly-CSharp.dll'))
$chase = $game.MainModule.Types | Where-Object Name -EQ 'CameraChaseState'
$update = $chase.Methods | Where-Object Name -EQ 'UpdateState'
$instructions = $update.Body.Instructions
$matches = 0
for ($i = 1; $i -lt $instructions.Count; $i++) {
    if ($instructions[$i-1].OpCode.Name -eq 'ldstr' -and $instructions[$i-1].Operand -eq 'Center' `
        -and $instructions[$i].Operand.ToString() -eq 'System.Boolean Rewired.Player::GetButtonDown(System.String)') {
        $matches++
    }
}
if ($matches -ne 1) { throw "Game Center patch target mismatch: $matches matches" }
$game.Dispose()

$built = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $repo 'AceOption\bin\Release\net472\AceOption.dll'))
[xml]$project = Get-Content -LiteralPath (Join-Path $repo 'AceOption\AceOption.csproj') -Raw
$version = [string]($project.Project.PropertyGroup | Where-Object Version | Select-Object -First 1).Version
if ($built.Name.Version.ToString() -ne "$version.0") { throw 'Unexpected assembly version' }
if ($built.Name.Name -ne 'AceOption') { throw 'Unexpected assembly name' }
$cameraPatch = $built.MainModule.Types | Where-Object Name -EQ 'CameraChaseStatePatches'
$updatePatch = $cameraPatch.NestedTypes | Where-Object Name -EQ 'UpdateState'
if (!($updatePatch.Methods | Where-Object Name -EQ 'Transpiler')) { throw 'Center transpiler missing' }
$postfix = $updatePatch.Methods | Where-Object Name -EQ 'Postfix'
$operands = @($postfix.Body.Instructions | ForEach-Object { if ($null -ne $_.Operand) { $_.Operand.ToString() } })
foreach ($required in @('FreeLookOrbit::Update','FreeLookOrbit::GetSmoothing','AutoCenterDelay','AutoCenterSpeed','FreeLookDeadzone','GetButtonDown','ResetOrbit')) {
    if (!($operands | Where-Object { $_ -like "*$required*" })) { throw "Postfix missing $required" }
}
$built.Dispose()
Write-Output "PASS: Center patch matches the installed game; DLL includes delay, speed, deadzone, recenter, and version $version"

& (Join-Path $PSScriptRoot 'Verify-GForce.ps1') -GameDir $GameDir
