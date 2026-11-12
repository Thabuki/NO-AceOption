# Third-party notices

This repository contains code adapted from the projects below. Their notices
are reproduced here as required by their licenses.

## ChaseCamPlus behavioral reference

Copyright (c) 2026 CarloApri — MIT License

Ace Option is a newly written reimplementation informed by the supplied custom
ChaseCamPlus 2.0.6 source and its regression tests. It is not a clean-room
rewrite. The active standalone implementation is under `AceOption`. The
original MIT notice remains in LICENSE and accompanies the install package.

Upstream: <https://github.com/CarloApri/NO-ChaseCamPlus> Starting upstream
revision: `21d0198eea2e42cf528c021de6bef3ad9db0a23c`. Custom work and the
standalone reimplementation were developed with substantial ChatGPT/Codex
assistance for Thabuki.

---

## YawOnMouse

Copyright (c) 2024 Muj — MIT License

The previous ChaseCamPlus Rewired registration was adapted from YawOnMouse.
The registration in `AceOption/Patches/RewiredAwakePatches.cs` follows that
technique and preserves compatible action identifiers. Its notice is retained
for this lineage.

```text
MIT License

Copyright (c) 2024 Muj

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

## TargetCamControl

by 9138noms — <https://github.com/9138noms/TargetCamControl>

The technique of injecting user-assignable `InputAction` entries into
Rewired's user data before the input manager reads it originates here;
YawOnMouse credits it as the source of its own implementation, and this mod
inherits it through that adaptation.

No code was taken from TargetCamControl directly, and its license terms have
not been reviewed. This notice is attribution for the technique. If its author
objects to the credit or wishes it worded differently, please open an issue.

---

## ChaseView reference

[ChaseView by Oliepolie](https://github.com/Oliepolie/ChaseView) documents and
fixes the missing G-force effects in chase view. Its `GForceEffects.cs` was
reviewed to cross-check the issue in the previous custom build. Ace Option
reimplements the fix by widening two camera checks in the game's own effect
paths; it does not include ChaseView source. ChaseView is available under the
MIT license, copyright (c) 2026 Oliepolie.

## Build dependencies

Not redistributed — referenced at build time and expected to be present at
runtime.

- [BepInEx](https://github.com/BepInEx/BepInEx) — LGPL-2.1
- [HarmonyX](https://github.com/BepInEx/HarmonyX) — MIT
- Game assemblies from Nuclear Option (Shockfront Studios), referenced for
  compilation only and not included in this repository or in any release.
