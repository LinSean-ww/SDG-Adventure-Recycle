using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
public static class Level04AcceptanceProbe {
 static string PathOf(Transform t){string s=t.name;while(t.parent){t=t.parent;s=t.name+"/"+s;}return s;}
 public static void Run(){
  var scene=EditorSceneManager.OpenScene("Assets/_Modules_/02_Levels/Level04/Imported/Scenes/SampleScene.unity",OpenSceneMode.Single);Physics.SyncTransforms();
  foreach(var r in scene.GetRootGameObjects())foreach(var t in r.GetComponentsInChildren<Transform>(true)){string n=t.name.ToLowerInvariant();if(n.Contains("door")||n.Contains("gate")||n.Contains("entrance")||n.Contains("factory")||n.Contains("sited")){var rs=t.GetComponents<Renderer>();var cs=t.GetComponents<Collider>();Debug.Log("L04_GATE path="+PathOf(t)+" pos="+t.position.ToString("F2")+" fwd="+t.forward.ToString("F2")+" active="+t.gameObject.activeInHierarchy+" render="+string.Join(";",rs.Select(x=>x.bounds.ToString()))+" coll="+string.Join(";",cs.Select(x=>x.GetType().Name+":"+x.enabled+":"+x.isTrigger+":"+x.bounds.ToString())));}}
  var boss=UnityEngine.Object.FindFirstObjectByType<ToiletMechBossAI>();if(boss){var a=boss.agent?boss.agent:boss.GetComponent<NavMeshAgent>();var anim=boss.animator?boss.animator:boss.GetComponentInChildren<Animator>();Debug.Log("L04_AI boss="+PathOf(boss.transform)+" pos="+boss.transform.position+" agent="+(a?$"radius={a.radius} height={a.height} speed={a.speed} angular={a.angularSpeed} stopping={a.stoppingDistance} baseOffset={a.baseOffset} onMesh={a.isOnNavMesh}":"none")+" anim="+(anim?anim.name:"none")+" rootMotion="+(anim&&anim.applyRootMotion));if(anim&&anim.runtimeAnimatorController){var c=anim.runtimeAnimatorController as AnimatorController;if(c)foreach(var layer in c.layers)foreach(var st in layer.stateMachine.states)Debug.Log("L04_ANIM state="+st.state.name+" speed="+st.state.speed+" motion="+(st.state.motion?st.state.motion.name:"null")+" duration="+(st.state.motion?st.state.motion.averageDuration:0));}}
  var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Modules_/02_Levels/Level04/Imported/Plasma Guns Lite/Prefabs/Plasma_SMG_Dirty_Prefab.prefab");if(prefab){var obj=(GameObject)PrefabUtility.InstantiatePrefab(prefab);var rr=obj.GetComponentsInChildren<Renderer>(true);if(rr.Length>0){var b=rr[0].bounds;foreach(var r in rr.Skip(1))b.Encapsulate(r.bounds);Debug.Log("L04_GUN bounds="+b+" localScale="+obj.transform.localScale+" renderers="+rr.Length); }UnityEngine.Object.DestroyImmediate(obj);}
 }
 public static void RunDetail(){
  var scene=EditorSceneManager.OpenScene("Assets/_Modules_/02_Levels/Level04/Imported/Scenes/SampleScene.unity",OpenSceneMode.Single);
  var factory=scene.GetRootGameObjects().First(x=>x.name=="ONLYfactory");foreach(var t in factory.GetComponentsInChildren<Transform>(true)){var r=t.GetComponent<Renderer>();if(r){var b=r.bounds;Debug.Log("L04_FACTORY "+PathOf(t)+" center="+b.center.ToString("F2")+" size="+b.size.ToString("F2")+" mats="+string.Join(",",r.sharedMaterials.Select(m=>m?m.name:"null")));}}
  var boss=UnityEngine.Object.FindFirstObjectByType<ToiletMechBossAI>();var anim=boss.animator?boss.animator:boss.GetComponentInChildren<Animator>(true);if(!anim)throw new Exception("Boss animator not found");foreach(var t in anim.GetComponentsInChildren<Transform>(true))if(t.name.ToLowerInvariant().Contains("hand")||t.name.ToLowerInvariant().Contains("arm"))Debug.Log("L04_BONE "+PathOf(t)+" pos="+t.position.ToString("F2"));
  var clip=anim.runtimeAnimatorController.animationClips.First(x=>x.name=="Attack1");foreach(var e in AnimationUtility.GetAnimationEvents(clip))Debug.Log("L04_ATTACK_EVENT "+e.functionName+" time="+e.time);foreach(var b in AnimationUtility.GetCurveBindings(clip).Where(x=>x.path.ToLowerInvariant().Contains("hand")||x.path.ToLowerInvariant().Contains("arm")).Take(20))Debug.Log("L04_ATTACK_CURVE "+b.path+" "+b.propertyName);
 }

 public static void SampleAttackAndD(){
  var scene=EditorSceneManager.OpenScene("Assets/_Modules_/02_Levels/Level04/Imported/Scenes/SampleScene.unity",OpenSceneMode.Single);var boss=UnityEngine.Object.FindFirstObjectByType<ToiletMechBossAI>();var anim=boss.animator;Debug.Log("L04_VISUAL_PARENT "+PathOf(anim.transform)+" parent="+(anim.transform.parent?PathOf(anim.transform.parent):"ROOT"));
  var clip=anim.runtimeAnimatorController.animationClips.First(x=>x.name=="Attack1");var hands=anim.GetComponentsInChildren<Transform>(true).Where(x=>x.name=="hand_l"||x.name=="hand_r").ToArray();
  for(int i=0;i<=20;i++){float time=clip.length*i/20f;clip.SampleAnimation(anim.gameObject,time);Debug.Log("L04_ATTACK_SAMPLE t="+time.ToString("F3")+" "+string.Join(" ",hands.Select(h=>h.name+"="+anim.transform.InverseTransformPoint(h.position).ToString("F2"))));}
  var f=scene.GetRootGameObjects().First(x=>x.name=="ONLYfactory").GetComponentsInChildren<MeshFilter>(true).First(x=>x.name=="factory");var mesh=f.sharedMesh;var v=mesh.vertices;var mats=f.GetComponent<Renderer>().sharedMaterials;
  for(int k=0;k<mesh.subMeshCount;k++){if(k>=mats.Length||!mats[k]||!mats[k].name.ToLowerInvariant().Contains("sited"))continue;var idx=mesh.GetIndices(k);if(idx.Length==0)continue;Bounds b=new Bounds(f.transform.TransformPoint(v[idx[0]]),Vector3.zero);foreach(var n in idx)b.Encapsulate(f.transform.TransformPoint(v[n]));Debug.Log("L04_D_SUBMESH "+mats[k].name+" bounds="+b);}
 }

 public static void ScanDOpenings(){
  var scene=EditorSceneManager.OpenScene("Assets/_Modules_/02_Levels/Level04/Imported/Scenes/SampleScene.unity",OpenSceneMode.Single);Physics.SyncTransforms();
  var fac=scene.GetRootGameObjects().First(x=>x.name=="ONLYfactory").GetComponentsInChildren<MeshCollider>().Single(x=>x.name=="factory");
  foreach(int side in new[]{0,1,2,3}){float start=0,last=0;bool open=false;for(int i=1;i<=99;i++){float v=-25+i*.25f;Vector3 origin,dir;if(side==0){origin=new Vector3(-1.5f,1.5f,v);dir=Vector3.right;}else if(side==1){origin=new Vector3(v,1.5f,-1.5f);dir=Vector3.forward;}else if(side==2){origin=new Vector3(-23.5f,1.5f,v);dir=Vector3.left;}else{origin=new Vector3(v,1.5f,-23.5f);dir=Vector3.back;}bool gap=!fac.Raycast(new Ray(origin,dir),out _,3f);if(gap&&!open){start=v;open=true;}if(!gap&&open){if(last-start>=.5f)Debug.Log($"L04_D_GAP boundary={side} range={start:F2}..{last:F2} height=1.5 path=ONLYfactory/factory");open=false;}last=v;}if(open&&last-start>=.5f)Debug.Log($"L04_D_GAP boundary={side} range={start:F2}..{last:F2} height=1.5 path=ONLYfactory/factory");}
 }

}