#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace Luobei.ClientSimActors {
public static class ExternalActorExport {
 public static ActorPackage Export(GameObject source,string libraryRoot,string id,string displayName){
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Export in Edit mode only");
  var selection=new ActorSelection{libraryRoot=libraryRoot,actorId=id};var outDir=ExternalActors.PackageDir(selection);
  bool existingBundle=File.Exists(Path.Combine(outDir,"actor.bundle"));
  const string temp="Assets/Luobei/ClientSimActors/Editor/ExportStaging";
  if(AssetDatabase.IsValidFolder(temp))throw new IOException("Previous export staging exists; inspect it first");
  Directory.CreateDirectory(temp);AssetDatabase.Refresh();GameObject copy=null;
  try{
   copy=Object.Instantiate(source);copy.SetActive(false);copy.name="Actor";copy.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);copy.transform.localScale=Vector3.one;
   // A static appearance payload: no Avatar authoring scripts, gameplay, FX or colliders.
   foreach(var m in copy.GetComponentsInChildren<MonoBehaviour>(true).Reverse())if(m)Object.DestroyImmediate(m);
   foreach(var t in copy.GetComponentsInChildren<Transform>(true))GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
   foreach(var c in copy.GetComponentsInChildren<Component>(true).Reverse())if(c&&!(c is Transform)&&!(c is Animator)&&!(c is Renderer)&&!(c is MeshFilter))Object.DestroyImmediate(c);
   var animators=copy.GetComponentsInChildren<Animator>(true).Where(a=>a.avatar&&a.avatar.isHuman&&a.avatar.isValid).ToArray();if(animators.Length!=1)throw new InvalidDataException("Export requires one valid Humanoid");
   var a=animators[0];foreach(var other in copy.GetComponentsInChildren<Animator>(true))if(other!=a)Object.DestroyImmediate(other);
   a.runtimeAnimatorController=null;a.applyRootMotion=false;a.enabled=true;a.cullingMode=AnimatorCullingMode.AlwaysAnimate;
   var leftEye=a.GetBoneTransform(HumanBodyBones.LeftEye);var rightEye=a.GetBoneTransform(HumanBodyBones.RightEye);var head=a.GetBoneTransform(HumanBodyBones.Head);
   var view=leftEye&&rightEye?(leftEye.position+rightEye.position)*.5f:head.position;view=copy.transform.InverseTransformPoint(view);
   var skins=copy.GetComponentsInChildren<SkinnedMeshRenderer>(true);var repaired=new System.Collections.Generic.List<object>();
   var references=skins.Where(s=>s.sharedMesh).SelectMany(s=>s.bones.Select((b,i)=>new{bone=b,index=i,skin=s})).Where(x=>x.bone&&x.index<x.skin.sharedMesh.bindposes.Length).Select(x=>new{bone=x.bone,matrix=x.skin.transform.localToWorldMatrix*x.skin.sharedMesh.bindposes[x.index].inverse}).ToArray();
   foreach(var skin in skins){bool visible=skin.enabled;for(var t=skin.transform;t&&t!=copy.transform;t=t.parent)visible&=t.gameObject.activeSelf;if(!visible)continue;if(!skin.sharedMesh)throw new InvalidDataException("Missing mesh: "+skin.name);var bones=skin.bones;var used=new System.Collections.Generic.HashSet<int>();foreach(var w in skin.sharedMesh.boneWeights){if(w.weight0>0)used.Add(w.boneIndex0);if(w.weight1>0)used.Add(w.boneIndex1);if(w.weight2>0)used.Add(w.boneIndex2);if(w.weight3>0)used.Add(w.boneIndex3);}
    foreach(var index in used){if(index>=bones.Length)throw new InvalidDataException("Bone array too short: "+skin.name);if(bones[index])continue;
     var matrix=skin.transform.localToWorldMatrix*skin.sharedMesh.bindposes[index].inverse;
     var matches=references.Select(r=>{float e=0;for(int k=0;k<16;k++)e=Mathf.Max(e,Mathf.Abs(matrix[k]-r.matrix[k]));return new{r.bone,error=e};}).GroupBy(r=>r.bone).Select(g=>g.OrderBy(r=>r.error).First()).OrderBy(r=>r.error).ToArray();
     if(matches.Length<2||matches[0].error>=.001f||matches[0].error>=matches[1].error*.25f)throw new InvalidDataException("Missing/ambiguous weighted skin bone: "+skin.name+" index "+index);
     bones[index]=matches[0].bone;repaired.Add(new{renderer=skin.name,index,bone=AnimationUtility.CalculateTransformPath(bones[index],copy.transform),matches[0].error});
    }
    skin.bones=bones;if(!skin.rootBone)skin.rootBone=a.GetBoneTransform(HumanBodyBones.Hips);
   }
   foreach(var r in copy.GetComponentsInChildren<Renderer>(true))foreach(var m in r.sharedMaterials)if(!m||!m.shader)throw new InvalidDataException("Missing material/Shader: "+r.name);
   copy.SetActive(true);string prefab=temp+"/Actor.prefab";PrefabUtility.SaveAsPrefabAsset(copy,prefab);AssetDatabase.SaveAssets();
   var deps=AssetDatabase.GetDependencies(prefab,true);if(deps.Any(p=>p.EndsWith(".cs",StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("Payload still depends on script source");
   Directory.CreateDirectory(outDir);var build=new AssetBundleBuild{assetBundleName="actor.bundle",assetNames=new[]{prefab},addressableNames=new[]{"actor"}};
   if(!existingBundle){var manifest=BuildPipeline.BuildAssetBundles(outDir,new[]{build},BuildAssetBundleOptions.ChunkBasedCompression|BuildAssetBundleOptions.StrictMode,BuildTarget.StandaloneWindows64);
   if(!manifest||manifest.GetAllDependencies("actor.bundle").Length!=0)throw new InvalidDataException("Bundle build failed or has external bundle dependencies");}
   var metadata=new ActorPackage{id=id,displayName=displayName,animatorPath=AnimationUtility.CalculateTransformPath(a.transform,copy.transform),viewPosition=view,sourceUnityVersion=Application.unityVersion};
   if(existingBundle){if(!File.Exists(Path.Combine(outDir,"actor.json"))||File.ReadAllText(Path.Combine(outDir,"actor.json"))!=JsonUtility.ToJson(metadata,true))throw new IOException("Existing bundle metadata differs; choose another id");}
   else File.WriteAllText(Path.Combine(outDir,"actor.json"),JsonUtility.ToJson(metadata,true));
   File.WriteAllText(Path.Combine(outDir,"export-report.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new{source=source.name,sourceScene=source.scene.path,unity=Application.unityVersion,reusedExistingBundle=existingBundle,rendererCount=copy.GetComponentsInChildren<Renderer>(true).Length,skinCount=skins.Length,humanoid=a.avatar.isHuman,valid=a.avatar.isValid,scriptCount=copy.GetComponentsInChildren<MonoBehaviour>(true).Length,repairedSkinBindings=repaired.ToArray(),dependencies=deps,metadata=Newtonsoft.Json.Linq.JObject.Parse(JsonUtility.ToJson(metadata))},Newtonsoft.Json.Formatting.Indented));return metadata;
  }finally{if(copy)Object.DestroyImmediate(copy);AssetDatabase.DeleteAsset(temp);AssetDatabase.Refresh();}
 }
}
}
#endif
