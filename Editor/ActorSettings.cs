#if UNITY_EDITOR
using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Luobei.ClientSimActors {
[Serializable] public class ActorMachineDefaults {
 public int schemaVersion=1;
 public bool enabled;
 public string libraryRoot="",actorId="";
}
[Serializable] public class ActorProjectOverrides {
 public int schemaVersion=2;
 // No project file, or mode=inherit: use this user's machine defaults.
 public string mode="inherit",actorId="",libraryRoot="",idleControllerPath="";
}
public static class ActorSettings {
 public static string MachinePath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Luobei","ClientSimActors","settings.json");
 public static ActorMachineDefaults ReadMachine(){
  if(!File.Exists(MachinePath))return new ActorMachineDefaults();
  var m=JsonUtility.FromJson<ActorMachineDefaults>(File.ReadAllText(MachinePath));
  if(m==null||m.schemaVersion!=1)throw new InvalidDataException("Unsupported machine defaults: "+MachinePath);
  return m;
 }
 static void EditOnly(){if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Change configuration in Edit mode; applied next Play");}
 static void Write(string path,object value){Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,JsonUtility.ToJson(value,true));}
 public static bool Legacy=>File.Exists(ExternalActors.ConfigPath)&&(int?)JObject.Parse(File.ReadAllText(ExternalActors.ConfigPath))["schemaVersion"]==1;
 public static ActorProjectOverrides ReadProject(){
  if(!File.Exists(ExternalActors.ConfigPath))return new ActorProjectOverrides();
  var text=File.ReadAllText(ExternalActors.ConfigPath);
  if((int?)JObject.Parse(text)["schemaVersion"]==1){
   var old=JsonUtility.FromJson<ActorSelection>(text);
   return new ActorProjectOverrides{mode=old.enabled?"actor":"robot",actorId=old.actorId,libraryRoot=old.libraryRoot,idleControllerPath=old.idleControllerPath};
  }
  var p=JsonUtility.FromJson<ActorProjectOverrides>(text);CheckProject(p);return p;
 }
 static void CheckProject(ActorProjectOverrides p){
  if(p==null||p.schemaVersion!=2)throw new InvalidDataException("Unsupported project overrides");
  if(p.mode!="inherit"&&p.mode!="actor"&&p.mode!="robot")throw new InvalidDataException("Project mode must be inherit, actor or robot");
  if(p.mode=="actor"&&string.IsNullOrWhiteSpace(p.actorId))throw new InvalidDataException("Actor override requires actorId");
 }
 public static ActorSelection Resolve(ActorProjectOverrides p=null,ActorMachineDefaults m=null){
  p=p??ReadProject();m=m??ReadMachine();CheckProject(p);
  return new ActorSelection{enabled=p.mode=="actor"||(p.mode=="inherit"&&m.enabled),libraryRoot=string.IsNullOrWhiteSpace(p.libraryRoot)?m.libraryRoot:p.libraryRoot,actorId=p.mode=="actor"?p.actorId:m.actorId,idleControllerPath=p.idleControllerPath??""};
 }
 public static void SaveProject(ActorProjectOverrides p){EditOnly();CheckProject(p);var s=Resolve(p);if(s.enabled)ExternalActors.ReadPackage(s);Write(ExternalActors.ConfigPath,p);}
 public static void SaveMachine(ActorMachineDefaults m){
  EditOnly();if(m==null||m.schemaVersion!=1)throw new InvalidDataException("Unsupported machine defaults");
  if(m.enabled)ExternalActors.ReadPackage(new ActorSelection{enabled=true,libraryRoot=m.libraryRoot,actorId=m.actorId});
  Write(MachinePath,m);
 }
 // Explicit migration: preserve only project animation, clear character/path pins.
 public static void UseDefaults(){var p=ReadProject();SaveProject(new ActorProjectOverrides{idleControllerPath=p.idleControllerPath});}
 public static void Select(string actorId,string libraryRoot=null){var p=ReadProject();p.mode="actor";p.actorId=actorId;if(libraryRoot!=null)p.libraryRoot=libraryRoot;SaveProject(p);}
 public static void Disable(){var p=ReadProject();p.mode="robot";SaveProject(p);}
 // Compatibility for callers using the prototype-1 resolved-selection API.
 public static void Configure(ActorSelection s){
  if(s==null||s.schemaVersion!=1)throw new InvalidDataException("Unsupported selection schema");
  var m=ReadMachine();SaveProject(new ActorProjectOverrides{mode=s.enabled?"actor":"robot",actorId=s.actorId,libraryRoot=s.libraryRoot==m.libraryRoot?"":s.libraryRoot,idleControllerPath=s.idleControllerPath});
 }
 public static object Snapshot(){var p=ReadProject();var m=ReadMachine();return new{machinePath=MachinePath,projectPath=ExternalActors.ConfigPath,projectFileExists=File.Exists(ExternalActors.ConfigPath),legacyProject=Legacy,machine=m,overrides=p,effective=Resolve(p,m),sources=new{enabled=p.mode=="inherit"?"machine":"project",actorId=p.mode=="actor"?"project":"machine",libraryRoot=string.IsNullOrWhiteSpace(p.libraryRoot)?"machine":"project",idleControllerPath="project"}};}
}
}
#endif
