# Releasing

The repository root is a VPM/Unity package. No Unity license is needed to build
its source ZIP. Unity/ClientSim verification remains a separate step.

1. Make changes, verify the Editor integration in a compatible World, and set
   `package.json.version`, its versioned `url`, and `ExternalActors.Version`.
2. Update the changelog. Keep previous `vpm/index.json` versions and existing
   release files intact.
3. Run `python scripts/build_release.py` and
   `python -m unittest discover -s tests -v`. Review the exact ZIP member list,
   MIT notice, public docs and `vpm/index.json`.
4. Commit the reviewed source and generated listing; tag that exact commit
   `v<VERSION>`. Push the commit and tag only when publication is authorized.
5. Create the versioned GitHub Release with the ZIP, `package.json`, and
   `SHA256SUMS` from `dist/`. The download URL must match the package manifest.
6. Read back the published tag/commit, release assets and anonymous listing URL.
   Download the actual release ZIP and verify its SHA against the listing.
7. Add the listing to VCC or the official VPM CLI, install the package in a
   compatible World, and verify actual live loading. Do not infer successful
   installation from valid JSON or an upload receipt.

The root `vpm/index.json` is served at:

```text
https://raw.githubusercontent.com/tobenot/clientsim-avatar-library/main/vpm/index.json
```

Example explicit release commands (after approval and after the tag exists):

```sh
gh release create v0.1.0 \
  dist/io.github.tobenot.clientsim-avatar-library-0.1.0.zip \
  dist/package.json dist/SHA256SUMS \
  --repo tobenot/clientsim-avatar-library --verify-tag \
  --title 'ClientSim Avatar Library 0.1.0' --notes-file RELEASE-NOTES.md
```

No workflow auto-publishes on push. This keeps source commits separate from the
explicit external-publication decision. For local package development, add the
checkout as a VCC User Package. Do not pass a folder path to `vpm add package`
without checking your CLI version: the tested 0.1.28 treats it as a package name.
For repository installs use the package ID and a reachable listing. Close the
target Unity Editor first, and inspect both CLI logs and the installed manifests:
this old CLI can emit `[ERR]` while still returning exit 0. Remove old prototype
scripts only after preserving project-specific adapters and configuration.
