# ClientSim Avatar Library

Load external Humanoid avatars in VRChat ClientSim and share one local library across projects.

**Configure once per computer/user. Install the package in each World. Keep your character files outside every project.**

A developer-oriented, Editor-only plugin by **LuobeiLetters (tobenot)**. MIT-licensed source. Not affiliated with or endorsed by VRChat.

## Install with VCC

1. Open **VCC → Settings → Packages → Add Repository**.
2. Add this listing URL:

   ```text
   https://raw.githubusercontent.com/tobenot/clientsim-avatar-library/main/vpm/index.json
   ```

3. Close that project's Unity Editor, then manage a compatible **World** project in VCC and add **ClientSim Avatar Library**. Close Unity before package/SDK updates to avoid locked native DLLs.
4. Open Unity. Use **Tools → Luobei → ClientSim Actors**.

Package ID: `io.github.tobenot.clientsim-avatar-library`.

The listing and versioned ZIP must both be publicly reachable. A private clone alone does not make the package installable through a public VCC listing. For local development, VCC's **User Packages** can use a checkout containing `package.json`.

## First use

You provide your own legitimately obtained character. This repository contains **no avatar models, textures, outfits, shaders or SDK binaries**.

1. Export a compatible appearance package from your character source, as below.
2. In **ClientSim Actors → Machine defaults — configure once**, browse to your external character library, select the character, enable it, and save.
3. Enter ClientSim Play using your normal World workflow. The external model supplies the appearance and Humanoid Avatar; the World retains its player root, input, interactions and Station behavior.
4. New compatible Worlds with this package installed inherit the same defaults. No project JSON or repeated library path is needed.

Choose **Use another character** for a project-specific character, or **Use default Robot** to disable it only in that World. **Use machine default** returns to inheritance while preserving the project's animation setting. Changes apply **next Play**, not during an existing session.

### Export a character

The source must already contain exactly one valid Humanoid Animator and its appearance dependencies. In this version the exporter ships with the Worlds-dependent plugin, so use a **Worlds/ClientSim-compatible source project**. An Avatar-only SDK project is not a supported installation target. Export is an Editor C# entry suitable for Agents; it does not require a separate authoring wizard.

For example, select your character root in Unity and call the following from your own Editor menu or an existing authorized Editor automation entry:

```csharp
using Luobei.ClientSimActors;
using UnityEditor;

public static class MyCharacterExport
{
    [MenuItem("Tools/My World/Export selected character")]
    public static void ExportSelected()
    {
        ExternalActorExport.Export(
            Selection.activeGameObject,
            "D:/MyCharacterLibrary", // your external library, not this project's Assets
            "my-character",         // a new safe id: letters, digits, underscore, hyphen
            "My character");
    }
}
```

An Agent can instead call the same public C# method with an explicit source GameObject. Do not export a whole scene as a character. Supply a **new ID** when the source changes: this version preserves an existing bundle and does not prove content identity from matching metadata.

The export copy retains meshes, weights, materials, textures and a valid Humanoid Avatar. It strips MonoBehaviours, physics/colliders, authoring components and animation controllers. A bounded bind-matrix repair may resolve missing weighted skin bones; all repairs are listed in `export-report.json`. Original scene/model assets are not rewritten. Review any repairs on your own character.

Unity's bundle builder uses temporary assets under `Assets/Luobei/ClientSimActors/Editor/ExportStaging`; the exporter removes that temporary staging asset in `finally`. The result stays in the external library, not in the project or Git.

### Animations belong to the World

The avatar's own Humanoid skeleton is retained. An optional idle Controller comes from the consumer project, selected as a Unity object in **Project animation / optional library override**. Leaving it empty means no additional idle animation, not automatic discovery of arbitrary locomotion.

Humanoid clips can be retargeted. Transform-path-bound wardrobe/material/face curves require their own adaptation. Custom Station output handoff may require a project-owned adapter; this package does **not** install a petrification mechanic or claim every Station system is automatically compatible.

## Agent / CLI use

Start with [AGENTS.md](AGENTS.md). Python 3, standard library only; no private hostnames, SSH helpers or account credentials.

From a Unity project directory with the package loaded:

```sh
# One-time machine default. Substitute your actual library and id.
python Packages/io.github.tobenot.clientsim-avatar-library/Tools~/actors.py machine-set --library D:/MyCharacterLibrary --actor my-character

# Every installed consumer inherits it; no repeated path argument.
python Packages/io.github.tobenot.clientsim-avatar-library/Tools~/actors.py status
python Packages/io.github.tobenot.clientsim-avatar-library/Tools~/actors.py list
python Packages/io.github.tobenot.clientsim-avatar-library/Tools~/actors.py settings

# Optional per-project exceptions:
python Packages/io.github.tobenot.clientsim-avatar-library/Tools~/actors.py select --actor another-character
python Packages/io.github.tobenot.clientsim-avatar-library/Tools~/actors.py disable
python Packages/io.github.tobenot.clientsim-avatar-library/Tools~/actors.py use-defaults
```

`--project /absolute/World` targets another local installed project. The CLI does not open Unity, enter Play, export assets or upload anything. `status.selectedActor` is configuration; **`phase=loaded` and `loadedActor`** prove live binding. `settings` reports the effective config and where each value comes from.

## Files and configuration

```text
package.json                 VPM / Unity package manifest
Editor/                      Editor-only assembly and public C# API
Tools~/actors.py              local Agent client, ignored by Unity's asset importer
AGENTS.md                    executable first-use and protocol contract
vpm/index.json               public package listing (repository only)
scripts/build_release.py     deterministic ZIP + checksum/listing generation
```

Machine defaults use .NET `LocalApplicationData`: `%LOCALAPPDATA%/Luobei/ClientSimActors/settings.json` on Windows. Project overrides, when needed, are `UserSettings/LuobeiClientSimActor.json`. Both are local settings, not character payloads. The character library is a directory containing one subdirectory per `actorId`, each with `actor.json`, self-contained `actor.bundle` and optional export report.

```json
{
  "schemaVersion": 1,
  "enabled": true,
  "libraryRoot": "D:/MyCharacterLibrary",
  "actorId": "my-character"
}
```

The above is the machine configuration. A project needs **no file** for inheritance. An optional project-local idle setting is:

```json
{
  "schemaVersion": 2,
  "mode": "inherit",
  "idleControllerPath": "Assets/MyWorld/Animation/Idle.controller"
}
```

Project modes: `inherit`, `actor` with an explicit `actorId`, or `robot`. An optional nonempty `libraryRoot` overrides the machine's library. An empty/omitted library inherits. Prototype-1 selection files remain pinned until explicitly migrated with `use-defaults`; that migration retains the local idle path.

## Compatibility and limitations

Tested target: **Unity 2022.3.22f1, VRChat Worlds/ClientSim 3.10.2, Windows64, Built-in render pipeline**. This initial manifest pins Worlds `3.10.2` because the serialized binding is version-sensitive; broader SDK versions need explicit verification. Packages must match the Unity version stored in the export metadata. Do not downgrade or upgrade an unrelated SDK installation just to try the plugin; use a compatible project.

The installed ClientSim provider exposes bone reads but not a public custom-avatar setter. The binding uses Unity Editor `SerializedObject` on the SDK's serialized `avatarAnimator` field, then checks the provider. **That seam is version-sensitive, not a stable public VRChat avatar-switch API.** No SDK source is modified and no private reflection is used. Failed loads restore the saved default model.

Verified during development: one appearance package shared by two existing Worlds, live LocalPlayer provider/API bone consistency, initial standing eye calibration, and a project-specific complete Station lifecycle with output restoration, walking and reentry. The VPM ZIP was then installed through the official VPM CLI with Unity closed; after reopening, the `Packages` Editor assembly loaded the same external character, inherited the machine defaults, matched the six queried player bones and evaluated the consumer's own Humanoid animation. This packaged-consumer check is not a repeat of the complete custom Station lifecycle. See [CHANGELOG.md](CHANGELOG.md).

Not verified or provided: another avatar's full compatibility, arbitrary SDK/Unity versions, native VRChat avatar switching, VR/FBT, Avatar FX/expressions, PhysBones, complete locomotion blending, live hot-swap or a generic Station adapter. Initial standing eye calibration does not prove crouch/prone/rescaling behavior. This is an Editor tool; uploaded Worlds do not read users' local avatar libraries.

## Build / maintain

```sh
python scripts/build_release.py
python -m unittest discover -s tests -v
```

The build creates a ZIP with `package.json` at its root, a release `package.json`, `SHA256SUMS`, and `vpm/index.json` with `zipSHA256`. Generated ZIPs stay out of Git. The listing retains published versions; never delete old release downloads in use by projects.

See [RELEASING.md](RELEASING.md) for the explicit publish steps. No service daemon, project registry, telemetry or cross-device synchronization is installed.

## License

[MIT](LICENSE). Copyright © 2026 LuobeiLetters (tobenot). Dependencies and avatars remain under their own licenses; this repository's MIT license does not relicense VRChat, Unity or third-party appearance assets. See [THIRD-PARTY.md](THIRD-PARTY.md).
