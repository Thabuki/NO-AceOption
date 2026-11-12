# Ace Option

A quality-of-life mod for Nuclear Option with improved camera controls, a
customizable HUD, and an Ace Combat-inspired flying experience. Requires
**BepInEx 5**. Configure the mod through **BepInEx Configuration Manager** or
its config file.

Current version: **1.0.1**. [Download the release](https://github.com/Thabuki/NO-AceOption/releases/tag/v1.0.1).

## Features

The custom menu has five groups. The defaults below apply to new configurations
and missing keys. Settings apply live, and saved choices carry over on
upgrade.

### Free Look

| Setting                     | Default | Behavior                                                                                                    |
| --------------------------- | ------- | ----------------------------------------------------------------------------------------------------------- |
| Enable Free Look            | On      | Look around in chase with the existing Pan View and Tilt View controls. Center resets the view immediately. |
| Look Sensitivity            | 1       | Scale the game's view sensitivity.                                                                          |
| Look Deadzone               | 0.15    | Ignore small look inputs, with a range of 0 to 1.                                                           |
| Auto-Center Delay (seconds) | 0.15    | Wait 0 to 30 seconds after look input stops before returning to center.                                     |
| Auto-Center Speed           | 5       | Adjust the automatic return speed from 0.1 to 20 while respecting the game's view smoothing.                |

The sensitivity, deadzone and auto-center settings require Enable Free Look.
Manual look supports the game's inversion settings and is limited to 170
degrees left/right and 85 degrees up/down. Menus suspend look input and the
idle timer.

### Chase Camera

| Setting                           | Default | Behavior                                                                                                                            |
| --------------------------------- | ------- | ----------------------------------------------------------------------------------------------------------------------------------- |
| Remember Camera View              | Off     | Enable to restore your last cockpit/chase choice across aircraft changes, respawns, matches and game restarts.                      |
| Enable Camera Adjustments         | Off     | Apply the saved distance and vertical framing adjustments.                                                                          |
| Camera Distance                   | 0.65    | Scale chase distance from 0.25 to 2. A value of 1 uses the original distance. Requires Enable Camera Adjustments.                   |
| Aircraft Vertical Offset          | -0.15   | Move the aircraft vertically by -0.35 to 0.35 of screen height. Negative values place it lower. Requires Enable Camera Adjustments. |
| Enable Target Focus               | Off     | Hold Chase Target Focus (Hold) to follow the newest marked target. Requires assigning that action in Flight Controls.               |
| Target Focus Hold Delay (seconds) | 0.3     | Set the hold threshold from 0.3 to 2 seconds. Requires Enable Target Focus.                                                         |
| Correct External Weapon FOV       | On      | Use external-view FOV for bomb aiming and boresight zoom outside the cockpit.                                                       |
| Hit Sound Outside Cockpit         | On      | Play external-view hit confirmation audio when the game's hit markers are enabled.                                                  |

Free look, camera adjustments and target focus have independent switches.
Remember Camera View starts off, so new aircraft use the native cockpit start.
Existing saved On values carry over; turn the setting off or reset it in
Configuration Manager to use the new default.
Native chase zoom and camera presets are available. Release target focus to
return to chase. With free look enabled, Center cancels focus until the focus
button is released.

### Chase HUD

The layout, minimap and instrument options require Show Flight HUD.

| Setting                 | Default | Behavior                                                                                                                                     |
| ----------------------- | ------- | -------------------------------------------------------------------------------------------------------------------------------------------- |
| Show Flight HUD         | On      | Display the flight HUD in chase view.                                                                                                        |
| HUD Follows Camera      | Off     | Center and level the layout in the camera viewport during free look and target focus. Aircraft attitude values still come from the aircraft. |
| Show Minimap            | On      | Display the chase minimap alongside the flight HUD.                                                                                          |
| Show Pitch Ladder       | On      | Display the pitch ladder and horizon line.                                                                                                   |
| Show Aircraft Reference | On      | Display the fixed aircraft reference mark.                                                                                                   |
| Show Left Instruments   | On      | Display speed, angle of attack, Mach, G-force and fuel. Warning widgets remain available when this group is hidden.                          |
| Show Right Instruments  | On      | Display throttle, RPM, altitude and climb rate.                                                                                              |
| Show Compass            | On      | Display the heading ribbon and bearing using aircraft heading.                                                                               |
| Show Joystick Indicator | On      | Draw the mouse stick line and dot. Mouse steering is enabled separately under Flight Controls.                                               |

These six visibility switches default to On for new configs, missing keys and
individual resets. Existing saved Off values carry over; enable the switches
or reset each one in Configuration Manager to use these defaults.

### Flight Controls

| Setting                             | Default | Behavior                                                                                                                                         |
| ----------------------------------- | ------- | ------------------------------------------------------------------------------------------------------------------------------------------------ |
| Enable Mouse Flight Input           | Off     | Allow the game's mouse flight input in chase. Requires the native mouse flight mode, Enable Free Look off and target focus inactive.             |
| Throttle Chord: Assist Off (Hold)   | Off     | Hold Increase Throttle and Decrease Throttle together to temporarily disable native flight assist. Release either to restore its previous state. |
| Throttle Down: Ground Brakes (Hold) | Off     | Hold Decrease Throttle alone with throttle at zero for one continuous second of wheel contact to apply brakes.                                   |
| Yaw Chord: Auto-Level (Hold)        | Off     | Hold both yaw directions to level the wings and nose through normal pitch/roll controls. Applies to airborne planes outside auto-hover.          |
| Smooth Button Yaw                   | Off     | Ramp digital yaw input for smaller tap corrections and gradual full rudder.                                                                      |
| Yaw Press Time (seconds)            | 2       | Set the centered-to-full rudder time from 0.05 to 5 seconds. Requires Smooth Button Yaw.                                                         |
| Yaw Return Time (seconds)           | 2       | Set the full-rudder-to-center time from 0.02 to 5 seconds. Reversals pass through center. Requires Smooth Button Yaw.                            |

The throttle and yaw shortcuts use both poles of existing native actions, so
they need no extra custom binding. Axis Modifier bypasses both throttle
shortcuts. During an assist-off hold, absolute analog throttle follows the
combined input, while buttons and relative throttle hold the current value.
The native assist switch also controls VTOL automation on supported aircraft.

Ground-brake timing starts when throttle reaches zero, including if Decrease
Throttle was already held. Release, leaving the ground, raising throttle or an
interruption resets the delay. The normal Brake control is available
immediately.

### Combat

| Setting                    | Default | Behavior                                                                                                                                                                                   |
| -------------------------- | ------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Missile Camera (Hold Fire) | Off     | Launch one missile per Fire press. Hold for 0.3 seconds to follow its actual spawn, then release or wait for impact to return to the previous cockpit/chase view and FOV.                  |
| Single-Target Cycling      | Off     | Cycle one visible, detected enemy per Mark Target tap, preferring contacts suited to the selected weapon.                                                                                  |
| Aim Turrets with Camera    | On      | Aim manual turrets without an assigned target along the chase camera, including recentering. Disabling uses the aircraft nose. Native automatic aiming at assigned targets takes priority. |

The missile camera uses the first marked target and supports rail, bay and
network spawn delays. Each additional missile requires a new press. Target
cycling uses current HUD contacts with known positions and the game's
exclusions. Full-map selection uses the native controls.

## Additional controls and automatic feedback

These three actions appear in the game's Flight Controls and start unbound.

| Action                    | Behavior                                                                                                   |
| ------------------------- | ---------------------------------------------------------------------------------------------------------- |
| Chase Cam Toggle          | Swap cockpit/chase or return to cockpit from another view.                                                 |
| Chase Target Focus (Hold) | Follow the newest marked target after the configured delay. Can share the Mark Target button.              |
| Fire Guns (Hold)          | Fire guns independently of the selected missile, bomb or other weapon, using the game's Link Guns setting. |

With Link Guns off, Fire Guns (Hold) uses the fitted gun with the lowest
ammunition cost per round. Equal prices use native weapon order. Selecting
another gun or turret for normal Fire leaves that dedicated trigger on the
cheapest gun, so two different turret types can be fired independently. An
empty, cooling or safety-blocked default keeps its place. Link Guns fires all
ready fixed and turret gun stations. Ammo, cadence and safeties use the native
weapon methods.

The HUD automatically shows **AUTO-LEVEL** during yaw-chord leveling and
**FLIGHT ASSIST OFF** during the throttle chord. These labels use the native
HUD font at 85% of the action-message size. Releasing or interrupting the
shortcut removes its indicator. Throttle-chord assist changes use this held
indicator instead of queueing timed assist messages.

## Always-on fixes and integration

These behaviors have no separate menu switch.

- **Absolute analog throttle correction** follows the combined axis
  consistently through neutral and opposing-trigger cancellation. With the
  negative-axis range enabled, equal pressure produces 50% throttle and
  unequal pressure stays proportional.
- **Chase-view G-force effects** run the game's visual and audio effect paths
  for your local aircraft in chase view.
- **Final-camera HUD alignment** refreshes the visible chase flight HUD after
  camera movement and floating-origin shifts. Native weapon cues are placed
  after the final HUD layout.
- **Screen-space HUD stability** preserves overlay-canvas positions during
  world-origin shifts before projecting chase instruments and weapon cues.
- **Look-away HUD timing** evaluates supplementary instrument visibility after
  the final camera and HUD layout.
- **Velocity-cue placement** preserves the native screen projection when HUD
  Follows Camera moves or levels the cue's parent.
- **Shared target-mark/focus handling** marks on release, consumes a press
  that activated focus, and bulk-marks on release after an unused long hold.
  This also supports an unbound focus action.
- **Camera placement safeguards** check terrain after camera distance,
  free-look and target-focus movement. Relative transform restoration prevents
  camera adjustments accumulating between frames and supports floating-origin
  shifts.
- **Chase lifecycle guards** handle a missing followed unit during camera
  updates and the native chase-exit dereference after that unit is cleared.
- **Dependent menu controls** gray out child editors and reset buttons while
  their parent is disabled, retaining saved values for re-enabling.

## Install and upgrade

Download the built `AceOption-1.0.1.zip` asset from the
[1.0.1 release](https://github.com/Thabuki/NO-AceOption/releases/tag/v1.0.1).
GitHub's automatic **Source code** downloads contain the
repository files; the installable mod is the separately attached ZIP.

1. Close Nuclear Option and install BepInEx 5 if needed.
2. Remove older copies of the mod and any conflicting camera mods.
3. Extract the ZIP to a temporary folder. Copy only the extracted `BepInEx`
   folder into the game folder, merging it with the existing `BepInEx` folder.
   The final plugin path is `BepInEx/plugins/AceOption/AceOption.dll`.
4. Launch the game and configure Ace Option through BepInEx Configuration
   Manager or `BepInEx/config/Thabuki.NuclearOption.AceOption.cfg`.
5. Assign the optional custom actions in the game's Flight Controls.

Keep the README, guide, license notices and build information with your
extracted download for reference.

Existing Ace Option settings and saved action bindings carry over on upgrade.

Camera, HUD and audio run locally. Input actions, weapon firing and native
turret targeting use the game's existing simulation and replication.

The guide covers controls, configuration and saved settings.

[Guide](docs/GUIDE.md) · [MIT license](LICENSE) ·
[Third-party notices](THIRD-PARTY-NOTICES.md)
