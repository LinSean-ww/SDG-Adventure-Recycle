using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace M7SharedPlayerTests {
 public sealed class Level04RecycleReachabilityTests {
  static Type T(string n)=>Type.GetType(n+", Assembly-CSharp",true);
  [UnityTest] public IEnumerator SceneItemsHaveUnobstructedNearbyRayCandidates(){
   SceneManager.LoadScene("Assets/_Modules_/02_Levels/Level04/Imported/Scenes/SampleScene.unity",LoadSceneMode.Single);yield return null;yield return null;
   var boss=UnityEngine.Object.FindObjectOfType(T("BossHealth"));boss.GetType().GetMethod("TakeDamage").Invoke(boss,new object[]{5});Physics.SyncTransforms();
   var items=UnityEngine.Object.FindObjectsOfType(T("RecycleItem")).Cast<Component>().ToArray();Assert.AreEqual(5,items.Length);
   foreach(var item in items){var proxy=item.GetComponents<Collider>().FirstOrDefault(c=>!c.isTrigger&&c.enabled);Assert.NotNull(proxy,item.name+" missing interaction proxy");bool found=false;
    foreach(float radius in new[]{1f,1.5f,2f})for(int i=0;i<24&&!found;i++){
     float angle=i*Mathf.PI/12;Vector3 start=proxy.bounds.center+new Vector3(Mathf.Cos(angle)*radius,5,Mathf.Sin(angle)*radius);
     var floors=Physics.RaycastAll(start,Vector3.down,12f,~0,QueryTriggerInteraction.Ignore).OrderBy(x=>x.distance);
     foreach(var floor in floors){if(floor.normal.y<.7f||floor.collider.transform.IsChildOf(item.transform))continue;Vector3 eye=floor.point+Vector3.up*1.5f;Vector3 delta=proxy.bounds.center-eye;if(delta.magnitude>2.95f)continue;
      var hits=Physics.RaycastAll(eye,delta.normalized,delta.magnitude+.05f,~0,QueryTriggerInteraction.Ignore).OrderBy(x=>x.distance).ToArray();if(hits.Length>0&&hits[0].collider.transform.IsChildOf(item.transform)){found=true;break;}}
    }
    Debug.Log("L04_REACH "+item.name+" "+found);Assert.IsTrue(found,"No clear nearby ray candidate for "+item.name);
   }
   var house=(Component)UnityEngine.Object.FindObjectOfType(T("RecycleHouse"));var point=house.GetComponentsInChildren<Transform>(true).First(x=>x.name=="SubmitPoint");var houseProxy=point.GetComponents<Collider>().First(c=>!c.isTrigger&&c.enabled);bool houseFound=false;
   foreach(float radius in new[]{1f,1.5f,2f})for(int i=0;i<24&&!houseFound;i++){
    float angle=i*Mathf.PI/12;Vector3 start=houseProxy.bounds.center+new Vector3(Mathf.Cos(angle)*radius,5,Mathf.Sin(angle)*radius);
    foreach(var floor in Physics.RaycastAll(start,Vector3.down,12f,~0,QueryTriggerInteraction.Ignore).OrderBy(x=>x.distance)){
     if(floor.normal.y<.7f||floor.collider.transform.IsChildOf(house.transform))continue;Vector3 eye=floor.point+Vector3.up*1.5f;Vector3 delta=houseProxy.bounds.center-eye;if(delta.magnitude>2.95f)continue;
     var hits=Physics.RaycastAll(eye,delta.normalized,delta.magnitude+.05f,~0,QueryTriggerInteraction.Ignore).OrderBy(x=>x.distance).ToArray();if(hits.Length>0&&hits[0].collider.transform.IsChildOf(house.transform)){houseFound=true;break;}
    }
   }
   Debug.Log("L04_REACH RecycleHouse "+houseFound);Assert.IsTrue(houseFound,"No clear nearby ray candidate for recycle house");
  }
 }
}