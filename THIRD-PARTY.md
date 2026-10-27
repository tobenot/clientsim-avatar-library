# Third-party dependencies and assets

This repository contains the plugin's source and documentation, licensed under MIT.
It does not redistribute Unity, the VRChat SDK, Newtonsoft.Json, avatars, clothing,
textures, shaders, purchased assets or private project files.

- Install Unity and the VRChat Worlds SDK through their normal official channels.
  Those products retain their own terms and licenses.
- Newtonsoft.Json is resolved through Unity's `com.unity.nuget.newtonsoft-json`
  package. Its upstream license is MIT; no DLL or source copy is included here.
- The integration is built against the installed SDK's ClientSim APIs and a
  version-sensitive serialized binding field. It is an independent integration,
  not an official VRChat extension or a stable API guarantee.
- Supply only avatars/assets that you are entitled to use. A local AssetBundle
  export is not permission to redistribute somebody else's character, outfit,
  texture or shader. The plugin's MIT license applies to this plugin, not to
  resources processed by it.

Primary technical references:

- https://vcc.docs.vrchat.com/vpm/packages/
- https://vcc.docs.vrchat.com/vpm/repos/
- https://vcc.docs.vrchat.com/vpm/cli/
- https://creators.vrchat.com/worlds/clientsim/
- https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AssetBundle.LoadFromFile.html
