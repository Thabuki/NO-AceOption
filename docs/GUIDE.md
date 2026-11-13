# Ace Option guide

[README and full feature list](../README.md)

Ace Option adds configurable chase-camera, HUD, input and combat features
to Nuclear Option. This guide describes the current 1.0.2 behavior.
The README lists every visible setting in custom-menu order.

## Install and upgrade

Get the built `AceOption-1.0.2.zip` asset from the
[1.0.2 release](https://github.com/Thabuki/NO-AceOption/releases/tag/v1.0.2).
The automatic **Source code (zip)** and **Source code (tar.gz)**
downloads contain source files, not the separately built plugin.

1. Close Nuclear Option.
2. Install BepInEx 5 and, for an in-game settings menu, BepInEx Configuration
   Manager.
3. Remove older copies of the mod and any conflicting camera mods from the
   plugins directory.
4. Extract the ZIP to a temporary folder. Copy only the extracted `BepInEx`
   folder into the game folder, merging it with the existing `BepInEx` folder.
   The final plugin path is `BepInEx/plugins/AceOption/AceOption.dll`.
5. Launch the game, configure Ace Option and assign any custom actions you
   want in Flight Controls.

Keep the README, guide, license notices and build information with your
extracted download for reference. BepInEx and the installed game's assemblies
supply the runtime dependencies.

### Configuration

Settings are saved in `BepInEx/config/Thabuki.NuclearOption.AceOption.cfg`.
Configuration Manager applies changes while the game runs. Close the game
before editing the config manually. Existing Ace Option values carry over on
upgrade. Defaults apply to missing keys and new files.

The visible menu groups are Free Look, Chase Camera, Chase HUD, Flight
Controls and Combat. Saved sections also include `Other` and `Virtual
Joystick`. Use the reference table below when
editing those keys. Dependent menu editors and reset buttons become
unavailable while their parent switch is off. Their values are retained for
the next time the parent is enabled.

## Controls

These three custom actions start unbound in the game's Flight Controls.

| Action | Use |
| --- | --- |
| Chase Cam Toggle | Swap cockpit/chase, or return to cockpit from another camera view. |
| Chase Target Focus (Hold) | Follow the newest marked target after the configured hold delay. Requires Enable Target Focus. |
| Fire Guns (Hold) | Fire guns independently of the selected weapon. Follows the game's Link Guns setting. |

The throttle and yaw shortcuts use the two poles of the game's existing
Throttle and Yaw actions. Pan View, Tilt View, Center, Fire and Mark Target
also use their existing bindings.

## Camera and target focus

### Free look and recentering

Enable Free Look is on by default. Pan View and Tilt View move the chase
camera, and Center resets it immediately. Look Sensitivity multiplies the
game's view sensitivity, with game inversion and smoothing applied. Look
Deadzone ignores small inputs on either look axis. Manual pan is limited to
170 degrees in either direction and tilt to 85 degrees in either direction.

Auto-Center Delay (seconds) waits after the last look input before starting
the smooth return. Its range is 0 to 30 seconds, with 0.15 as the default. A
value of zero starts the return immediately. Auto-Center Speed ranges from 0.1
to 20, with 5 preserving the original return speed. It affects the idle return
after the delay expires. Manual look smoothing continues using the existing
rate, and Center provides an instant reset.

Menus suspend manual look and its idle timer. Target focus reserves the camera
while following a target and during the smooth return to ordinary chase.

### Distance, framing and remembered view

Enable Camera Adjustments starts off. Enabling it applies Camera Distance and
Aircraft Vertical Offset. Distance ranges from 0.25 to 2, with a suggested
value of 0.65 and an original-distance multiplier of 1. Vertical offset ranges
from -0.35 to 0.35 of screen height, with -0.15 as the suggested value.
Negative values place the aircraft lower. Target focus aims at the target
during its hold.

Disabling camera adjustments restores the original chase placement on the next
update and retains both slider values. Native zoom and camera presets are
available. Terrain checks follow distance, orbit and focus movement. Relative
transform restoration prevents frame-to-frame accumulation and supports the
game's floating-origin shifts. Very close distances can clip large aircraft.

Remember Camera View is off by default, so new aircraft use the native cockpit
start. Enable it to save cockpit/chase selections made through native controls
or Chase Cam Toggle across aircraft changes, respawns, matches and game
restarts. The initial saved choice is cockpit. Temporary orbit and spectator
subjects, death and ejection leave the saved choice intact. Turning the
setting off retains that choice for later re-enabling. The saved choice is
stored as the hidden `Other.LastCameraView` key. Existing saved On values
carry over on upgrade; turn the setting off or reset it in Configuration
Manager to use the new default.

### Target focus and marking

Enable Target Focus starts off and works independently of free look and camera
adjustments. Bind Chase Target Focus (Hold), mark a target, then hold that
action for Target Focus Hold Delay (seconds). The delay ranges from 0.3 to 2
seconds. Focus follows the newest marked target, including targets above or
behind the aircraft. Release to return smoothly to chase. With free look
enabled, Center cancels focus until the focus action is released.

Focus requires the game to consider the contact currently tracked by your
faction and uses that faction's known target position. Detection shared by
other units in your faction counts; this is not limited to your aircraft's
own sensors. The game's normal tracking timeout applies. Losing tracking or
known-position data returns the camera smoothly to chase without clearing the
mark. If the contact is reacquired while the action remains held, focus resumes.
Missing faction data prevents focus.

Chase Target Focus (Hold) can share the Mark Target button. Flight marking is
handled on release. A tap marks one target, and a hold that activates focus
consumes that press. A long hold that reaches the native bulk-mark threshold
without activating focus bulk-marks on release, including when the focus
action is unbound. Full-map selection uses the game's original controls.

### External-view corrections

Correct External Weapon FOV uses the game's external FOV for bomb aiming and
boresight zoom outside the cockpit. Hit Sound Outside Cockpit adds external
hit confirmation audio when the game's hit-marker setting is enabled. Both
switches start on.

The local chase view also runs the game's G-force visual and audio effect
paths. This correction and the guards for missing camera subjects have no
separate menu setting.

## Chase HUD

Show Flight HUD starts on. Its instrument, layout and minimap settings apply
in chase view. Show Minimap and all six visibility switches below start on.

| Setting | Display |
| --- | --- |
| Show Pitch Ladder | Pitch ladder and horizon line. |
| Show Aircraft Reference | Fixed aircraft reference mark. |
| Show Left Instruments | Speed, angle of attack, Mach, G-force and fuel. |
| Show Right Instruments | Throttle, RPM, altitude and climb rate. |
| Show Compass | Heading ribbon and bearing. |
| Show Joystick Indicator | Mouse stick line and dot. |

These six switches default to on. Existing saved values carry over on upgrade.
To adopt these defaults in an older config, enable or
individually reset these switches in Configuration Manager while Show Flight
HUD is enabled. With the game closed, you can instead set the six
corresponding `[Chase HUD]` keys in the saved settings reference to `true`, or
remove those keys so they are recreated with the defaults on the next launch.

Hiding the left instrument group preserves warning widgets. The supplementary
look-away HUD evaluates native visibility after the final camera and HUD
layout. Hiding the minimap preserves the threat-warning widgets around it,
and the full map uses its native controls.

The visible flight HUD is refreshed after the final camera pose and
floating-origin shift. Screen-space overlay canvases retain their positions
during that shift, keeping subsequent instrument and aiming-marker placement
in the same UI coordinate frame. This correction applies automatically in
local chase view with Show Flight HUD enabled, in both alignment modes.
Cockpit and spectator views, and canvases outside screen-space overlay mode,
retain their native handling.
Native weapon cues are placed after the final HUD layout. HUD Follows Camera
starts off, using aircraft-nose alignment. Enabling it centers and levels the
layout in the render viewport during free look,
focus and recentering. Pitch and heading values describe aircraft attitude,
and native world-projected weapon cues and the velocity vector follow their
world points. The velocity cue keeps its native screen position when the
camera-centered layout moves or levels the HUD parent. Both alignment
corrections apply automatically while their chase HUD features are in use.

**AUTO-LEVEL** appears near the top of the HUD while the yaw chord levels the
wings and nose. **FLIGHT ASSIST OFF** appears there while the throttle chord
temporarily disables native flight assist. Both use the native HUD font at
85% of the action-message size and appear on separate lines when both
shortcuts are active. Release or interruption removes the corresponding
indicator; these labels report the held shortcuts rather than the assist
state after release. Throttle-chord assist changes suppress their native
timed on/off messages. Ordinary assist toggles and unrelated action reports
use the native message feed. Ground braking uses the native brake icon.

## Flight controls

### Mouse flight input

Enable the game's mouse/virtual joystick flight mode, then enable Enable Mouse
Flight Input under Flight Controls. Turn Enable Free Look off and release
target focus so the mouse can steer in chase. Show Joystick Indicator
independently controls the line and dot drawn on the HUD.

### Button yaw and auto-level

Bind the negative and positive sides of native Yaw to buttons or keys, such as
L1/LB and R1/RB. Smooth Button Yaw starts off. With it enabled, Yaw Press Time
(seconds) sets the centered-to-full rudder time and Yaw Return Time (seconds)
sets the full-rudder-to-center time. Both default to 2 seconds and support
values up to 5 seconds. A 60 ms tap at the defaults reaches about 3% rudder. A
reversal returns through center before building in the other direction. Analog
and mixed input use native arbitration.

Yaw Chord: Auto-Level (Hold) starts off. Enable it and hold both yaw
directions to level the wings and nose through normal pitch/roll commands,
with the rudder centered. Release either direction to resume manual control.
It works independently of Smooth Button Yaw and clears the old smoothing tail.

Auto-level applies to airborne planes, including VTOL planes outside
auto-hover. Helicopters, auto-hover and ground operation are excluded.
Inverted recovery rolls upright before pitching toward the horizon. Aircraft
control authority and airspeed determine the response. Throttle and the
selected flight-assist state are retained during leveling.

### Absolute analog throttle

The absolute-throttle correction has no menu switch. With relative throttle
off, absolute analog input follows the current combined axis through neutral,
return-to-center and opposing-trigger cancellation. With the game's
negative-axis range enabled, full decrease is 0%, centered or equally opposed
input is 50%, and full increase is 100%. Unequal trigger pressure produces
`50% + 50% × (increase − decrease)`.

Native range conversion, helicopter collective inversion and Axis Modifier
routing to Custom Axis 1 apply. Source tracking distinguishes absolute analog
input from buttons, relative axes and mixed input.

### Throttle Chord: Assist Off (Hold)

This shortcut starts off. Enable it, then hold both Increase Throttle and
Decrease Throttle past their existing button/trigger thresholds. Native flight
assist turns off for the hold. Release either side to restore the previous
assist state, including an already-off state.

Buttons and relative throttle hold the current throttle during the chord.
Absolute analog throttle follows the combined input, including equal pressure
at 50% when the negative-axis range is enabled. Axis Modifier bypasses the
shortcut and routes to native Custom Axis 1. The native flight-assist setter
also affects VTOL tilt/vectoring automation on supported aircraft.

Menus, map, pause, focus loss, pilot control loss, death/ejection, ownership
changes and detached cockpits interrupt the hold and restore assist. The HUD
shows **FLIGHT ASSIST OFF** throughout an active hold.

### Throttle Down: Ground Brakes (Hold)

This shortcut starts off. Hold Decrease Throttle alone with throttle at zero
for one continuous second of attached landing-wheel contact to apply full
brakes. If Decrease Throttle is already held while reducing throttle, the full
delay starts when throttle reaches zero. This also works with gentle absolute
trigger reduction and at landing-rollout speeds.

Release, leaving the ground, raising throttle or an input interruption clears
added braking and resets the delay. Returning to zero with wheel contact
starts a fresh delay. Holding both throttle poles or Axis Modifier bypasses
the shortcut. The normal Brake action is available immediately and its input
is preserved when added braking is removed. Contact uses native wheel
compression, including wheels on separate physical aircraft parts and moving
decks.

## Combat

### Independent gun trigger

Fire Guns (Hold) is an assignable action with no menu switch. It can fire guns
while a missile, bomb or another weapon is selected, and follows Link Guns.

With Link Guns off, the dedicated trigger uses the fitted gun station with the
lowest native ammunition cost per round, including turret guns. Equal prices
use native weapon order. Normal weapon cycling and the weapon wheel leave this
default in place. A default gun that is empty, cooling or safety-blocked keeps
its place. Aircraft and loadout changes select the cheapest fitted gun from
the new loadout.

For independent helicopter turrets, turn Link Guns off, bind Fire Guns (Hold),
and select the other turret for normal Fire. Both triggers can then operate
their respective gun types together. With Link Guns on, the dedicated trigger
fires all ready fixed and turret gun stations. The current selected weapon and
target list are retained. Native station/gun methods handle ammo, cadence and
safeties.

### Missile Camera (Hold Fire)

This setting starts off. Enable it under Combat to launch one missile with
each press of native Fire. Keep Fire held for 0.3 seconds to follow the actual
spawned missile in native orbit view. Release or missile impact restores the
previous cockpit/chase view and FOV. Each additional launch needs a new press.
With multiple marks, the shot uses the first target.

Empty, cooling and safety-blocked presses also require a new press. Native
mount launching handles ammo, rail/bay delays and replication. Delayed spawns
are associated with their originating presses, so an earlier tap cannot claim
a later hold's camera. Native spectator movement inputs are suppressed during
the follow. Guns and other weapon types use their normal firing path.

Menus, map, pause, focus or control loss, death/ejection, aircraft changes,
explicit camera changes and disabling the option end the temporary camera
view.

### Single-Target Cycling and turret aiming

Single-Target Cycling starts off. When enabled, Mark Target taps cycle one
enemy at a time, starting near screen center and wrapping around visible
contacts. Candidates need a current visible native HUD marker, a known
position and native target eligibility. Contacts suited to the selected weapon
are preferred. With no eligible contact, the current marks are retained.
Disabling the setting returns to additive marking. Bulk and full-map selection
use their normal paths.

Aim Turrets with Camera starts on. Manual turrets without an assigned target
follow the local chase camera, including its return to center. Turning the
setting off uses the aircraft nose. Assigned targets use the game's automatic
aiming priority in either mode. Enable Free Look controls manual camera
movement, and Enable Target Focus controls target following independently.

## Saved settings reference

The README provides the visible labels and behavior for all 32 menu settings.
This table gives their saved keys plus the hidden camera preference.

| Saved section | Key | Default | Range or role |
| --- | --- | --- | --- |
| Other | AlwaysFreeLook | true | Chase look switch. |
| Other | FreeLookSensitivity | 1 | View-sensitivity multiplier. |
| Other | FreeLookDeadzone | 0.15 | 0 to 1. |
| Other | AutoCenterDelay | 0.15 | 0 to 30 seconds. |
| Other | AutoCenterSpeed | 5 | 0.1 to 20. |
| Other | RememberCameraView | false | Opt in to persist cockpit/chase choice. |
| Other | LastCameraView | Cockpit | Hidden Cockpit/Chase preference. |
| Other | CameraAdjustments | false | Distance/framing parent. |
| Other | CameraDistance | 0.65 | 0.25 to 2. |
| Other | PlaneVerticalOffset | -0.15 | -0.35 to 0.35 of screen height. |
| Other | TargetFocusEnabled | false | Target-focus parent. |
| Other | TargetFocusHoldDelay | 0.3 | 0.3 to 2 seconds. |
| Other | FixWeaponFoV | true | External weapon FOV correction. |
| Other | HitSoundOutsideCockpit | true | External hit audio. |
| Chase HUD | FlightHud | true | Chase HUD parent. |
| Chase HUD | HudFollowsCamera | false | Camera-centered layout. |
| Chase HUD | Map | true | Chase minimap. |
| Chase HUD | PitchLadder | true | Pitch ladder/horizon. |
| Chase HUD | Waterline | true | Aircraft reference. |
| Chase HUD | LeftInstruments | true | Speed/AoA/Mach/G/fuel. |
| Chase HUD | RightInstruments | true | Throttle/RPM/altitude/climb. |
| Chase HUD | Compass | true | Heading ribbon/bearing. |
| Chase HUD | VirtualJoystickIndicator | true | Mouse stick drawing. |
| Virtual Joystick | VirtualJoystick | false | Chase mouse flight input. |
| Flight Controls | ThrottleChordFlightAssist | false | Held assist-off shortcut. |
| Flight Controls | ThrottleDownGroundBrakes | false | One-second zero-throttle braking. |
| Flight Controls | YawChordAutoLevel | false | Held wings/nose leveling. |
| Flight Controls | SmoothButtonYaw | false | Digital yaw smoothing parent. |
| Flight Controls | YawRiseTime | 2 | 0.05 to 5 seconds. |
| Flight Controls | YawReturnTime | 2 | 0.02 to 5 seconds. |
| Combat | MissileFollowCamera | false | Single press/held camera. |
| Combat | TargetCycling | false | Single-target tap cycling. |
| Chase HUD | TurretAim | true | Saved here, displayed under Combat. |

## Client-side scope

Camera, HUD and audio run locally. Flight-input shaping, firing,
turret aim and target assignments use native methods and the game's existing
replication. Ace Option uses the game's aircraft control authority, weapon
statistics and pilot physiology.

[License](../LICENSE) · [Third-party notices](../THIRD-PARTY-NOTICES.md)
