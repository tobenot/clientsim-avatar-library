# Changelog

## 0.1.0

Initial developer release.

- External self-contained Windows Humanoid appearance packages, loaded only for
  Editor/ClientSim Play sessions.
- One machine/user library and default character; new installed Worlds inherit
  without a project configuration file.
- Optional per-project character/Robot/library overrides; animations stay local
  to each World.
- Selection/settings UI, structured live load status and local Python Agent CLI.
- C# appearance exporter with source-copy stripping, weighted-skin validation and
  recorded bounded bind-matrix repair.
- Editor-only VPM package, deterministic release ZIP and VCC repository listing.

Verification status before the first published release:

- Prototype source was exercised in two existing Unity 2022.3.22f1 / Worlds 3.10.2
  projects using the same external character file.
- One project's custom Station lifecycle, walking, restoration and reentry were
  exercised. The second project evaluated its own Humanoid animation against the
  shared model. This is not two complete Station lifecycle tests.
- Shared defaults, explicit project disable, missing project settings and scoped
  overrides were exercised through the portable CLI against the real Editors.
- The versioned ZIP was installed through official VPM CLI 0.1.28 with Unity
  closed. The reopened consumer loaded the external model from the Editor-only
  package under `Packages`, inherited machine defaults, matched the queried
  provider/API bones and evaluated its own Humanoid animation. This is a packaged
  consumer check, not another complete custom Station lifecycle.
- The initial manifest pins Worlds 3.10.2. Close Unity before VCC/VPM installs:
  native SDK DLL locks can make a package transaction fail even if the old CLI
  exits with code 0. Check installer output and actual installed versions.
- No claim of another character, arbitrary SDK versions, native VRChat, VR/FBT
  or human first-use acceptance.
