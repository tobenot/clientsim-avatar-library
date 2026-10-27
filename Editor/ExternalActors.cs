#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using VRC.SDKBase;
using VRC.SDK3.ClientSim;
using Object=UnityEngine.Object;

namespace Luobei.ClientSimActors {
[Serializable] public class ActorSelection {
 public int schemaVersion=1;public bool enabled;public string libraryRoot="",actorId="",idleControllerPath="";
}
[Serializable] public class ActorPackage {
 public int schemaVersion=1;public string id,displayName,bundleFile="actor.bundle",prefabAsset="actor",animatorPath="",sourceUnityVersion,buildTarget="StandaloneWindows64",renderPipeline="BuiltIn";
 public Vector3 viewPosition;public string[] dependencies=new string[0];
}
[InitializeOnLoad] public static class ExternalActors {
 public const string Version="0.1.0";
 public static string Project=>Path.GetFullPath(Application.dataPath+"/..").Replace('\\','/');
 public static string ConfigPath=>Project+"/UserSettings/LuobeiClientSimActor.json";
 public static string Mailbox=>Project+"/UserSettings/LuobeiClientSimActors";
 public static Animator ActiveAnimator {get;private set;}
 public static RuntimeAnimatorController BaseController {get;private set;}
 static ActorSelection selection;static ActorPackage package;static string phase="edit",error="",loadedActor="";
 static AssetBundle bundle;static GameObject instance;static ClientSimPlayer player;static ClientSimPlayerAvatarManager manager;
 static Animator robot;static bool robotEnabled;static Renderer[] robotRenderers;static bool[] rendererEnabled;
 static float oldHeight;static bool heightChanged;static double nextTick,deadline;static bool attempted;
 static Transform trackingHead;static Vector3 savedHeadLocalPosition;
 public static event Action<Animator> ModelReady;
 static ExternalActors(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=State;AssemblyReloadEvents.beforeAssemblyReload+=Cleanup;Directory.CreateDirectory(Mailbox);File.WriteAllText(Mailbox+"/editor.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{project=Project,pid=System.Diagnostics.Process.GetCurrentProcess().Id,version=Version,loadedUtc=DateTime.UtcNow}));}
 public static ActorSelection ReadConfig()=>ActorSettings.Resolve();
 public static void Configure(ActorSelection value)=>ActorSettings.Configure(value);
 static bool IsWithin(string path,string root){return (Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar).StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);}
 public static string PackageDir(ActorSelection s){
  if(!Path.IsPathRooted(s.libraryRoot)||IsWithin(s.libraryRoot,Project))throw new InvalidDataException("Library must be an absolute directory outside the consumer project: "+s.libraryRoot);
  if(!System.Text.RegularExpressions.Regex.IsMatch(s.actorId??"","^[a-zA-Z0-9_-]+$"))throw new InvalidDataException("Invalid actor id");
  return Path.Combine(s.libraryRoot,s.actorId);
 }
 public static ActorPackage ReadPackage(ActorSelection s){
  string dir=PackageDir(s),file=Path.Combine(dir,"actor.json");if(!File.Exists(file))throw new FileNotFoundException("Missing actor metadata",file);
  var p=JsonUtility.FromJson<ActorPackage>(File.ReadAllText(file));
  if(p==null||p.schemaVersion!=1||p.id!=s.actorId)throw new InvalidDataException("Metadata schema/id mismatch: "+file);
  if(p.buildTarget!="StandaloneWindows64"||EditorUserBuildSettings.activeBuildTarget!=BuildTarget.StandaloneWindows64)throw new InvalidDataException("Prototype requires Windows64 Editor target");
  if(p.renderPipeline!="BuiltIn"||UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline)throw new InvalidDataException("Prototype requires Built-in render pipeline");
  if(p.sourceUnityVersion!=Application.unityVersion)throw new InvalidDataException("Bundle Unity version differs: "+p.sourceUnityVersion+" vs "+Application.unityVersion);
  if(p.dependencies!=null&&p.dependencies.Length!=0)throw new InvalidDataException("Prototype accepts self-contained bundles only");
  var b=Path.GetFullPath(Path.Combine(dir,p.bundleFile??""));if(!IsWithin(b,dir)||!File.Exists(b))throw new FileNotFoundException("Missing/unsafe bundle",b);
  if(string.IsNullOrEmpty(p.prefabAsset)||p.viewPosition.y<=0)throw new InvalidDataException("Missing prefab or invalid local-space view position");return p;
 }
 public static object List(string libraryRoot=null){var root=libraryRoot??ReadConfig().libraryRoot;if(!Directory.Exists(root))return new object[0];return Directory.GetDirectories(root).OrderBy(x=>x).Select(dir=>{try{var c=new ActorSelection{libraryRoot=root,actorId=Path.GetFileName(dir)};var p=ReadPackage(c);return (object)new{id=p.id,name=p.displayName,ready=true,error=""};}catch(Exception e){return new{id=Path.GetFileName(dir),name="",ready=false,error=e.Message};}}).ToArray();}
 public static object Status(){var s=ReadConfig();var bones=ActiveAnimator&&player?new[]{HumanBodyBones.Head,HumanBodyBones.Hips,HumanBodyBones.LeftHand,HumanBodyBones.RightHand,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot}.Select(b=>new{bone=b.ToString(),providerMatches=player.GetAvatarDataProvider().GetBoneTransform(b)==ActiveAnimator.GetBoneTransform(b),apiError=Vector3.Distance(player.Player.GetBonePosition(b),ActiveAnimator.GetBoneTransform(b).position)}).ToArray():null;
  return new{version=Version,project=Project,pid=System.Diagnostics.Process.GetCurrentProcess().Id,enabled=s.enabled,selectedActor=s.actorId,settings=ActorSettings.Snapshot(),loadedActor,phase,error,playing=EditorApplication.isPlaying,animator=ActiveAnimator?ActiveAnimator.name:null,animatorId=ActiveAnimator?ActiveAnimator.GetInstanceID():0,avatar=ActiveAnimator&&ActiveAnimator.avatar?ActiveAnimator.avatar.name:null,playerId=player&&player.Player!=null?player.Player.playerId:0,controller=ActiveAnimator?AssetDatabase.GetAssetPath(ActiveAnimator.runtimeAnimatorController):null,defaultRendererVisible=robotRenderers!=null&&robotRenderers.Any(r=>r&&r.enabled),eyeError=player&&instance&&package!=null?Vector3.Distance(player.GetTrackingProvider().GetTrackingTransform(VRCPlayerApi.TrackingDataType.Head).position,instance.transform.TransformPoint(package.viewPosition)):-1f,bundlePath=package!=null&&selection!=null?Path.Combine(PackageDir(selection),package.bundleFile):null,bones};
 }
 public static Animator Resolve(ClientSimPlayer p){return p?.GetAvatarDataProvider()?.GetBoneTransform(HumanBodyBones.Hips)?.GetComponentInParent<Animator>();}
 static void State(PlayModeStateChange s){
  if(s==PlayModeStateChange.EnteredPlayMode){selection=ReadConfig();phase=selection.enabled?"waiting-player":"default-robot";attempted=false;error="";deadline=EditorApplication.timeSinceStartup+30;}
  if(s==PlayModeStateChange.ExitingPlayMode)Cleanup();
  if(s==PlayModeStateChange.EnteredEditMode){phase="edit";WriteStatus();}
 }
 static void Tick(){
  if(EditorApplication.timeSinceStartup<nextTick)return;nextTick=EditorApplication.timeSinceStartup+.2;
  if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
  if(EditorApplication.isPlaying&&phase=="waiting-player"&&!attempted){
   var api=Networking.LocalPlayer;
   if(api!=null&&Networking.IsNetworkSettled){attempted=true;try{Bind(api.GetClientSimPlayer());}catch(Exception e){error=e.ToString();Cleanup();phase="failed-default-restored";Debug.LogError("ExternalActors: "+error);}WriteStatus();File.Copy(Mailbox+"/status.json",Mailbox+(phase=="loaded"?"/last-loaded.json":"/last-failed.json"),true);}
   else if(EditorApplication.timeSinceStartup>deadline){phase="failed-default-restored";error="ClientSim LocalPlayer not ready within 30 seconds";WriteStatus();}
  }
  ProcessMailbox();
 }
 static void SetAnimator(ClientSimPlayerAvatarManager target,Animator value){
  // Official installed ClientSim source serializes this field but has no public setter.
  // Editor SerializedObject is used once; SDK files and private reflection are untouched.
  var serialized=new SerializedObject(target);var prop=serialized.FindProperty("avatarAnimator");
  if(prop==null||prop.propertyType!=SerializedPropertyType.ObjectReference)throw new NotSupportedException("ClientSim avatarAnimator serialized seam unavailable");
  prop.objectReferenceValue=value;serialized.ApplyModifiedPropertiesWithoutUndo();
 }
 static void Bind(ClientSimPlayer target){
  package=ReadPackage(selection);var dir=PackageDir(selection);phase="loading";
  bundle=AssetBundle.LoadFromFile(Path.Combine(dir,package.bundleFile));if(!bundle)throw new InvalidDataException("AssetBundle.LoadFromFile returned null");
  var prefab=bundle.LoadAsset<GameObject>(package.prefabAsset);if(!prefab)throw new InvalidDataException("Prefab not found: "+package.prefabAsset);
  instance=Object.Instantiate(prefab);instance.SetActive(false);instance.name="ExternalActor_"+package.id;instance.hideFlags=HideFlags.DontSave;
  var at=string.IsNullOrEmpty(package.animatorPath)?instance.transform:instance.transform.Find(package.animatorPath);var a=at?at.GetComponent<Animator>():null;
  if(!a||!a.avatar||!a.avatar.isHuman||!a.avatar.isValid)throw new InvalidDataException("Loaded prefab has no valid Humanoid at animatorPath");
  if(instance.GetComponentsInChildren<MonoBehaviour>(true).Length>0)throw new InvalidDataException("Appearance package must not require MonoBehaviour scripts");
  player=target;manager=target.GetAvatarDataProvider() as ClientSimPlayerAvatarManager;if(!manager)throw new NotSupportedException("Installed player uses another Avatar provider");
  robot=Resolve(target);if(!robot)throw new InvalidDataException("Default Humanoid Animator missing");
  robotEnabled=robot.enabled;robotRenderers=robot.GetComponentsInChildren<Renderer>(true);rendererEnabled=robotRenderers.Select(r=>r.enabled).ToArray();
  instance.transform.SetParent(manager.transform,false);instance.transform.localPosition=Vector3.zero;instance.transform.localRotation=Quaternion.identity;
  // Keep native metre-sized package while retaining later ClientSim scale changes.
  instance.transform.localScale=Vector3.one/manager.transform.lossyScale.y;
  BaseController=string.IsNullOrEmpty(selection.idleControllerPath)?null:AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(selection.idleControllerPath);
  if(!string.IsNullOrEmpty(selection.idleControllerPath)&&!BaseController)throw new InvalidDataException("Project idle controller not found: "+selection.idleControllerPath);
  a.runtimeAnimatorController=BaseController;a.applyRootMotion=false;a.cullingMode=AnimatorCullingMode.AlwaysAnimate;a.enabled=true;
  foreach(var t in instance.GetComponentsInChildren<Transform>(true)){t.gameObject.layer=robot.gameObject.layer;t.gameObject.hideFlags=HideFlags.DontSave;}
  foreach(var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))skin.updateWhenOffscreen=true;
  instance.SetActive(true);a.Rebind();if(BaseController)a.Update(0);
  SetAnimator(manager,a);
  if(manager.GetBoneTransform(HumanBodyBones.Hips)!=a.GetBoneTransform(HumanBodyBones.Hips))throw new InvalidOperationException("Avatar provider did not accept new Animator");
  foreach(var r in robotRenderers)r.enabled=false;robot.enabled=false;ActiveAnimator=a;
  trackingHead=player.GetTrackingProvider().GetTrackingTransform(VRCPlayerApi.TrackingDataType.Head);if(trackingHead)savedHeadLocalPosition=trackingHead.localPosition;
  var main=Object.FindFirstObjectByType<ClientSimMain>();if(main){oldHeight=main.HeightManager.GetAvatarEyeHeightAsMeters();heightChanged=true;main.HeightManager.SetAvatarEyeHeightByMeters(package.viewPosition.y);}
  instance.transform.localScale=Vector3.one/manager.transform.lossyScale.y;
  if(trackingHead)trackingHead.position=instance.transform.TransformPoint(package.viewPosition);
  loadedActor=package.id;phase="loaded";ModelReady?.Invoke(a);
 }
 public static void Cleanup(){
  if(manager&&robot)SetAnimator(manager,robot);
  if(robot)robot.enabled=robotEnabled;
  if(robotRenderers!=null)for(int i=0;i<robotRenderers.Length;i++)if(robotRenderers[i])robotRenderers[i].enabled=rendererEnabled[i];
  if(trackingHead)trackingHead.localPosition=savedHeadLocalPosition;trackingHead=null;
  var main=heightChanged?Object.FindFirstObjectByType<ClientSimMain>():null;if(main&&main.HeightManager!=null)main.HeightManager.SetAvatarEyeHeightByMeters(oldHeight);
  heightChanged=false;if(instance)Object.DestroyImmediate(instance);instance=null;
  if(bundle)bundle.Unload(true);bundle=null;ActiveAnimator=null;BaseController=null;robot=null;manager=null;player=null;robotRenderers=null;package=null;loadedActor="";
 }
 static void WriteStatus(){Directory.CreateDirectory(Mailbox);File.WriteAllText(Mailbox+"/status.json",Newtonsoft.Json.JsonConvert.SerializeObject(Status(),Newtonsoft.Json.Formatting.Indented));}
 [Serializable] class Request{public string id,action,project,actorId,libraryRoot;public int pid;public bool enabled=true;}
 static void ProcessMailbox(){
  var file=Mailbox+"/request.json";if(!File.Exists(file))return;var q=JsonUtility.FromJson<Request>(File.ReadAllText(file));
  if(q==null||!System.Text.RegularExpressions.Regex.IsMatch(q.id??"","^[a-z0-9-]{1,80}$"))return;
  var dir=Mailbox+"/"+q.id;if(File.Exists(dir+"/started.json"))return;Directory.CreateDirectory(dir);File.WriteAllText(dir+"/started.json",JsonUtility.ToJson(q));
  try{
   if(q.project!=Project||q.pid!=System.Diagnostics.Process.GetCurrentProcess().Id)throw new InvalidOperationException("Project/PID mismatch");
   object result;
   switch(q.action){
    case "list":result=List(q.libraryRoot);break;
    case "status":result=Status();break;
    case "settings":result=ActorSettings.Snapshot();break;
    case "machine-set":ActorSettings.SaveMachine(new ActorMachineDefaults{enabled=q.enabled,libraryRoot=q.libraryRoot,actorId=q.actorId});result=ActorSettings.Snapshot();break;
    case "use-defaults":ActorSettings.UseDefaults();result=Status();break;
    case "validate":result=Newtonsoft.Json.Linq.JObject.Parse(JsonUtility.ToJson(ReadPackage(ReadConfig())));break;
    case "select":ActorSettings.Select(q.actorId,q.libraryRoot);result=Status();break;
    case "disable":ActorSettings.Disable();result=Status();break;
    default:throw new InvalidOperationException("Unknown actor action: "+q.action);
   }
   File.WriteAllText(dir+"/result.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{ok=true,id=q.id,result},Newtonsoft.Json.Formatting.Indented));
  }catch(Exception e){File.WriteAllText(dir+"/result.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{ok=false,id=q.id,error=e.ToString()},Newtonsoft.Json.Formatting.Indented));}
 }
}
public class ExternalActorsWindow:EditorWindow {
 ActorProjectOverrides project;ActorMachineDefaults machine;string message="";bool advanced,editMachine;Vector2 scroll;
 [MenuItem("Tools/Luobei/ClientSim Actors")]public static void Open(){GetWindow<ExternalActorsWindow>("ClientSim Actors");}
 void OnEnable(){Reload();}
 void Reload(){try{project=ActorSettings.ReadProject();machine=ActorSettings.ReadMachine();}catch(Exception e){message=e.Message;}}
 void Act(Action action){try{action();message="Saved. Applied next Play.";Reload();}catch(Exception e){message=e.Message;}}
 static string Folder(string label,string path){EditorGUILayout.BeginHorizontal();path=EditorGUILayout.TextField(label,path);if(GUILayout.Button("Browse",GUILayout.Width(65))){var chosen=EditorUtility.OpenFolderPanel(label,path,"");if(!string.IsNullOrEmpty(chosen))path=chosen;}EditorGUILayout.EndHorizontal();return path;}
 static string Character(string root,string current){
  var ids=Directory.Exists(root)?Directory.GetDirectories(root).Select(Path.GetFileName).OrderBy(x=>x).ToArray():new string[0];
  if(ids.Length==0)return EditorGUILayout.TextField("Character ID",current);
  // Keep an invalid or removed selection visible; don't silently choose another actor.
  var options=new[]{string.IsNullOrEmpty(current)?"Choose character":"Current: "+current}.Concat(ids).ToArray();
  int pick=EditorGUILayout.Popup("Character",0,options);return pick>0?ids[pick-1]:current;
 }
 void OnGUI(){
  if(project==null||machine==null){EditorGUILayout.HelpBox(message,MessageType.Error);if(GUILayout.Button("Reload"))Reload();return;}
  scroll=EditorGUILayout.BeginScrollView(scroll);
  EditorGUILayout.LabelField("ClientSim Actors",EditorStyles.boldLabel);
  EditorGUILayout.HelpBox("One library + default character for all installed projects on this machine/user. Changes apply next Play.",MessageType.Info);
  using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode)){
   int mode=project.mode=="actor"?1:project.mode=="robot"?2:0;
   project.mode=new[]{"inherit","actor","robot"}[EditorGUILayout.Popup("This project",mode,new[]{"Use machine default","Use another character","Use default Robot"})];
   var effective=new ActorSelection{enabled=project.mode=="actor"||(project.mode=="inherit"&&machine.enabled),actorId=project.mode=="actor"?project.actorId:machine.actorId,libraryRoot=string.IsNullOrWhiteSpace(project.libraryRoot)?machine.libraryRoot:project.libraryRoot};
   EditorGUILayout.LabelField("Effective character",effective.enabled?effective.actorId:"Default Robot");
   EditorGUILayout.LabelField("Library",effective.libraryRoot,EditorStyles.wordWrappedLabel);
   if(project.mode=="actor")project.actorId=Character(effective.libraryRoot,project.actorId);
   if(ActorSettings.Legacy)EditorGUILayout.HelpBox("Old project settings are pinned. Use machine default to inherit future changes; project animations are preserved.",MessageType.Warning);
   if(GUILayout.Button("Save this project"))Act(()=>ActorSettings.SaveProject(project));
   if(GUILayout.Button("Use machine default (keep project animations)"))Act(()=>ActorSettings.UseDefaults());
   advanced=EditorGUILayout.Foldout(advanced,"Project animation / optional library override");
   if(advanced){
    var controller=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(project.idleControllerPath??"");
    var selected=(RuntimeAnimatorController)EditorGUILayout.ObjectField("Idle controller",controller,typeof(RuntimeAnimatorController),false);
    if(selected!=controller)project.idleControllerPath=selected?AssetDatabase.GetAssetPath(selected):"";
    EditorGUILayout.HelpBox("Animations belong to this project. No controller means no additional idle animation. Leave library override empty to inherit.",MessageType.Info);
    project.libraryRoot=Folder("Library override",project.libraryRoot);
   }
   EditorGUILayout.Space();editMachine=EditorGUILayout.Foldout(editMachine,"Machine defaults — configure once");
   if(editMachine){
    EditorGUILayout.HelpBox("Affects installed projects that inherit defaults, next Play. Explicit project overrides stay unchanged.",MessageType.Info);
    machine.enabled=EditorGUILayout.Toggle("Use external character",machine.enabled);
    machine.libraryRoot=Folder("Library folder",machine.libraryRoot);machine.actorId=Character(machine.libraryRoot,machine.actorId);
    if(GUILayout.Button("Save machine defaults"))Act(()=>ActorSettings.SaveMachine(machine));
   }
  }
  if(!string.IsNullOrEmpty(message))EditorGUILayout.HelpBox(message,MessageType.Info);
  try{var status=Newtonsoft.Json.Linq.JObject.FromObject(ExternalActors.Status());EditorGUILayout.LabelField("Actual load",status.Value<string>("phase")+" / "+status.Value<string>("loadedActor"));if(!string.IsNullOrEmpty(status.Value<string>("error")))EditorGUILayout.HelpBox(status.Value<string>("error"),MessageType.Error);
   diagnostics=EditorGUILayout.Foldout(diagnostics,"Agent / diagnostic JSON");if(diagnostics)EditorGUILayout.TextArea(status.ToString());
  }catch(Exception e){EditorGUILayout.HelpBox(e.Message,MessageType.Error);}
  EditorGUILayout.EndScrollView();
 }
 bool diagnostics;
}
}
#endif
