# ClientSim Actors — agent entry

Purpose: reuse one external Humanoid appearance package across installed Unity World projects. Configure the library and default actor **once per machine/OS user**. A new consumer needs the plugin, not another copy of the character or another library-path configuration.

## Install / project bootstrap

Supported, actually tested target: Unity 2022.3.22f1, VRChat Worlds/ClientSim 3.10.2, Windows64, Built-in pipeline. Install a compatible Worlds SDK first. Do not change SDK versions automatically.

Install the VPM package `io.github.tobenot.clientsim-avatar-library` through the VCC listing in README. Public C# APIs are in the Editor-only assembly `Luobei.ClientSimAvatarLibrary.Editor`; `Tools~/actors.py` is the local Python client. Do not copy duplicate scripts into Assets. The C# exporter requires a compatible Worlds/ClientSim source project; Avatar-only projects are not supported by this package.

`Tools~/actors.py` is the portable local Python 3 client. Keep it in any accessible tools directory. No packages, credentials, SSH helpers, host names or private paths needed. It does not start Unity or enter Play.

## Configure once

Run on the computer/user running Unity, with one installed project already open:

```sh
python /path/to/actors.py machine-set --project /path/to/World --library /absolute/external/CharacterLibrary --actor my-character
```

Substitute only your actual paths and package id. Config is `%LOCALAPPDATA%/Luobei/ClientSimActors/settings.json` on Windows, using .NET's LocalApplicationData in the actual Unity user context. It is outside Git and outside the character payload. It is not a separate service or registry of projects.

**Every newly installed consumer with no overrides inherits that default automatically.** Do not generate a project JSON with a repeated libraryRoot/actorId for every project. When a library moves or the usual actor changes, update `machine-set` once; inheriting projects use it next Play. Existing running Play sessions keep their current actor until next Play.

## Ordinary project usage

```sh
# From the Unity project directory, --project is unnecessary:
python /path/to/actors.py status
python /path/to/actors.py list
python /path/to/actors.py settings
# Optional project exceptions:
python /path/to/actors.py select --actor another-character
python /path/to/actors.py disable
python /path/to/actors.py use-defaults
```

`select` changes only this project's actor. It still inherits the library unless deliberately overridden with `--library`. `disable` selects the default Robot **only in this project**. `use-defaults` removes actor/library/mode pins but preserves this project's idle animation. Neither operation modifies the shared defaults.

Existing prototype-1 configs remain pinned for compatibility; explicitly run `use-defaults` once to migrate. The plugin never silently resets an established project selection.

## What a project may need locally

No JSON at all is required for inherited character selection. If a project supplies its own idle controller, keep only an optional local override, for example:

```json
{
  "schemaVersion": 2,
  "mode": "inherit",
  "idleControllerPath": "Assets/MyWorld/Animation/Idle.controller"
}
```

File: `UserSettings/LuobeiClientSimActor.json`. Optional `mode` values: `inherit`, `actor`, `robot`. With `actor`, supply `actorId`. An optional nonempty `libraryRoot` is a deliberate project library override. Empty or omitted strings inherit. Do not put absolute machine paths into committed project files.

The appearance comes from the bundle; animations belong to the consuming project. No supplied idle controller means no additional idle animation, not automatic discovery of an arbitrary project controller. A custom Station adapter still belongs to its project. Generic loading does not install a particular petrification mechanic.

Human entry: **Tools → Luobei → ClientSim Actors**. Default screen: inherit / another character / Robot. Machine setup and optional project animation/library controls are folded sections.

## Verify, don't infer

1. `settings` reports machine/project files, effective config and field sources. `status.enabled/selectedActor` mean **configuration**, not a live load.
2. Enter ClientSim Play using the project's existing authorized method, then call `status`.
3. Require `phase=loaded`, expected `loadedActor`, valid Humanoid identity and `bones[*].providerMatches=true` / API errors near zero. `bundlePath` must stay outside the project. `validate` and `list.ready` only validate metadata/compatibility/file presence.
4. Exit Play; expect `phase=edit` and no currently loaded actor. `last-loaded.json` preserves the previous successful identity snapshot.
5. For an override, check another inheriting project remains unchanged. Restore defaults after a bounded test if requested.

## Machine-readable protocol

Mailbox: `<project>/UserSettings/LuobeiClientSimActors/`. `editor.json` announces the actual Editor's project, PID and plugin version. The CLI reads it, binds requests to that project/PID, creates a new ID and waits for `<id>/result.json`. Rejections return `ok=false` and an error, not fabricated success.

Actions: `settings`, `list`, `status`, `validate`, `select`, `disable`, `use-defaults`, `machine-set`. `machine-set` requires `enabled`, `libraryRoot`, `actorId`; `select` requires `actorId`, optional `libraryRoot`. All configuration writes require Edit mode and are applied next Play. The mailbox is a single-consumer slot; a pending request must be reconciled before another action in the **same project**. Separate projects have separate mailboxes.

On timeout the CLI returns a request id, receipt path and exit2. Read that exact receipt or repeat with `--request-id ID`; don't issue the same write with a new ID. This client doesn't provide a distributed multi-writer queue. A stale editor marker cannot authorize a new Editor: mismatched project/PID requests are rejected by the plugin.

Typical failures: missing marker = plugin not imported/open; invalid library = choose an absolute directory outside the consumer; unknown actor = `list` and fix the id; Unity/target/pipeline mismatch = use a compatible exported package; Play-mode write = exit Play first. Keep detailed diagnostics for actual errors, not the main usage flow.

## Package / capability boundary

Each library child is a safe `actorId` directory with `actor.json` + self-contained `actor.bundle`; package schema and optional export C# entry are in `README.md`. Export supply is separate from routine project consumption. Keep avatar licenses/appearance files private as appropriate.

Binding currently uses the installed SDK's version-sensitive serialized `avatarAnimator` field through Unity Editor SerializedObject and verifies the provider. This is not a guaranteed public VRChat avatar-switch API. No native-client upload, Avatar FX, expressions, PhysBones, VR/FBT or arbitrary-version claim. Do not distribute private desktop adapters as required dependencies.
