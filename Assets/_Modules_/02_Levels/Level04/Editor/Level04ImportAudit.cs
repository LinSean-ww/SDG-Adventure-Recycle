using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class Level04ImportAudit {
 public static void Run() {
  const string scenePath="Assets/_Modules_/02_Levels/Level04/Imported/Scenes/SampleScene.unity";
  Scene scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Single);
  int objects=0,scripts=0,prefabs=0,materials=0,shaders=0,refs=0;
  foreach(var root in scene.GetRootGameObjects()) foreach(var tr in root.GetComponentsInChildren<Transform>(true)) {
   var go=tr.gameObject; objects++; int n=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
   if(n>0){scripts+=n;Debug.LogWarning("L04_MISSING_SCRIPT "+PathOf(tr)+" count="+n);}
   if(PrefabUtility.IsAnyPrefabInstanceRoot(go)&&PrefabUtility.GetPrefabInstanceStatus(go)==PrefabInstanceStatus.MissingAsset){prefabs++;Debug.LogWarning("L04_MISSING_PREFAB "+PathOf(tr));}
   foreach(var r in go.GetComponents<Renderer>()) foreach(var m in r.sharedMaterials) if(m==null){materials++;Debug.LogWarning("L04_MISSING_MATERIAL "+PathOf(tr));} else if(m.shader==null||m.shader.name=="Hidden/InternalErrorShader"){shaders++;Debug.LogWarning("L04_BAD_SHADER "+PathOf(tr)+" material="+m.name);}
   foreach(var c in go.GetComponents<Component>()){if(c==null)continue;SerializedObject so;try{so=new SerializedObject(c);}catch{continue;}var it=so.GetIterator();while(it.NextVisible(true))if(it.propertyType==SerializedPropertyType.ObjectReference&&it.objectReferenceValue==null&&it.objectReferenceInstanceIDValue!=0){refs++;Debug.LogWarning("L04_MISSING_REFERENCE "+PathOf(tr)+" component="+c.GetType().Name+" property="+it.propertyPath);}}
  }
  Debug.Log("L04_AUDIT scene="+scene.path+" roots="+scene.rootCount+" objects="+objects+" missingScripts="+scripts+" missingPrefabs="+prefabs+" missingMaterials="+materials+" badShaders="+shaders+" missingReferences="+refs+" dependencies="+AssetDatabase.GetDependencies(scenePath,true).Length);
 }
 private static string PathOf(Transform t){string p=t.name;while(t.parent!=null){t=t.parent;p=t.name+"/"+p;}return p;}
}