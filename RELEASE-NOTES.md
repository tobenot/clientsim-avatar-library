# ClientSim Avatar Library 0.1.0

An Editor/ClientSim tool for sharing one external Humanoid appearance library
across multiple Unity World projects.

- Configure the library and default character once per machine/user.
- Install this VPM package in each compatible World; no repeated library paths.
- Override a character or use the default Robot only when a project needs it.
- Keep animation controllers in each World and avatar payloads outside Git.
- Use the selection UI or the standard-library-only Python Agent client.

Tested target: Unity 2022.3.22f1, VRChat Worlds/ClientSim 3.10.2, Windows64,
Built-in pipeline. ClientSim's serialized avatar binding is version-sensitive.
This is not native VRChat avatar switching, Avatar FX/PhysBones, VR or FBT.
No character models or third-party appearance assets are included.

MIT source by LuobeiLetters (tobenot). See README and AGENTS.md for setup,
character export, actual load checks and limitations.

VCC listing:
https://raw.githubusercontent.com/tobenot/clientsim-avatar-library/main/vpm/index.json
