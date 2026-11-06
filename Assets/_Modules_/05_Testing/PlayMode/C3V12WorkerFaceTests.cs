using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace M6IntegrationTests
{
    // Front-face geometry contracts, using the production worker and scene.
    // Rendering/visual acceptance still requires Windows; bounds are not pixels.
    public sealed class C3V12WorkerFaceTests
    {
        private C3V11Phase2RecyclingLineTests scope;
        private static object Get(object o,string n)=>C3V11Phase2RecyclingLineTests.Member(o,n);
        private static object Call(object o,string n,params object[] a)=>C3V11Phase2RecyclingLineTests.Call(o,n,a);
        private static GameObject Player=>GameObject.Find("M2_Player");
        private static Component Worker=>Player.GetComponent(C3V11Phase2RecyclingLineTests.GameType("C3TreasurePuzzle.C3WorkerAppearance"));
        private static Component Rig=>Player.GetComponents<MonoBehaviour>().Single(c=>c && c.GetType().Name=="PlayerPerspectiveRig");
        private static Transform Head=>(Transform)Get(Worker,"head");
        private static Transform Feature(string n)
        {
            Transform t=Head.Find(n);Assert.IsNotNull(t,n);return t;
        }
        private static void Mode(int value)
        {
            PropertyInfo mode=Rig.GetType().Assembly.GetType("SDGAdventure.SharedPlayer.PlayerViewPreference",true).GetProperty("Mode");
            mode.SetValue(null,Enum.ToObject(mode.PropertyType,value));Call(Rig,"ApplyMode");
        }
        private static Bounds InHead(MeshFilter mesh)
        {
            Bounds source=mesh.sharedMesh.bounds;
            Bounds result=new Bounds(Head.InverseTransformPoint(mesh.transform.TransformPoint(source.center)),Vector3.zero);
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                result.Encapsulate(Head.InverseTransformPoint(mesh.transform.TransformPoint(
                    source.center+Vector3.Scale(source.extents,new Vector3(x,y,z)))));
            return result;
        }
        private static Bounds Envelope(string n)=>InHead(Feature(n).GetComponent<MeshFilter>());
        // A conservative test: no frame mesh's own bounds may intersect a
        // finite front-to-pupil sight segment. It also works for rotated temples.
        private static bool Blocks(MeshFilter mesh,Vector3 from,Vector3 to)
        {
            Vector3 a=mesh.transform.InverseTransformPoint(Head.TransformPoint(from));
            Vector3 b=mesh.transform.InverseTransformPoint(Head.TransformPoint(to));
            Vector3 delta=b-a;
            return mesh.sharedMesh.bounds.IntersectRay(new Ray(a,delta.normalized),out float distance)
                && distance<=delta.magnitude;
        }
        [UnitySetUp] public IEnumerator Open()
        {
            scope=new C3V11Phase2RecyclingLineTests();yield return scope.Open();
            Assert.IsNotNull(Worker);Assert.IsNotNull(Head);
        }
        [UnityTearDown] public IEnumerator Close()
        {
            if(scope!=null)yield return scope.Close();scope=null;
        }

        [UnityTest] public IEnumerator BothEyeAperturesStayOpenBehindSafetyFrames()
        {
            Mode(1);Transform glasses=Feature("SafetyGlasses");
            Assert.IsNull(glasses.GetComponent<Renderer>(),"No opaque slab across both eyes");
            MeshFilter[] frame=glasses.GetComponentsInChildren<MeshFilter>();Assert.IsNotEmpty(frame);
            foreach(string side in new[]{"Left","Right"})
            {
                Bounds white=Envelope("EyeWhite_"+side),pupil=Envelope("EyePupil_"+side);
                Transform eye=Feature("EyeWhite_"+side),iris=Feature("EyePupil_"+side);
                Vector3 whiteFront=eye.TransformPoint(Vector3.forward*.5f);
                Vector3 pupilFront=iris.TransformPoint(Vector3.forward*.5f);
                Assert.Greater(Vector3.Dot(pupilFront-whiteFront,eye.forward),.0005f,
                    "Pupil must be visible in front of the white along the curved face normal");
                Assert.Less(pupil.size.x,white.size.x*.5f);
                Assert.IsTrue(Feature("EyeWhite_"+side).GetComponent<Renderer>().enabled);
                foreach(float dx in new[]{-.009f,0,.009f})foreach(float dy in new[]{-.004f,0,.004f})
                {
                    Vector3 to=new Vector3(pupil.center.x+dx,pupil.center.y+dy,pupil.center.z);
                    Vector3 from=new Vector3(to.x,to.y,.30f);
                    foreach(MeshFilter mesh in frame)
                        Assert.IsFalse(Blocks(mesh,from,to),side+" eye obscured by "+mesh.name);
                }
            }
            Assert.IsEmpty(Head.GetComponentsInChildren<Collider>(true));
            yield return null;
        }

        [UnityTest] public IEnumerator RoundedFeaturesSeparateFromEyesAndThinHelmetBrim()
        {
            Bounds face=Envelope("Face"),nose=Envelope("Nose"),beard=Envelope("Beard"),brim=Envelope("HardHatBrim");
            foreach(string n in new[]{"Nose","Beard"})
                Assert.Less(Feature(n).GetComponent<MeshFilter>().sharedMesh.vertices.Max(v=>v.sqrMagnitude),.30f,
                    n+" needs a rounded primitive silhouette rather than cube corners");
            Assert.That(face.size.y,Is.EqualTo(.25f).Within(.001f),"Keep accepted head/neck proportions");
            Assert.Less(nose.size.x,face.size.x*.18f);Assert.Less(beard.size.z,face.size.z*.28f);
            Assert.Greater(nose.min.y,beard.max.y+.01f,"No stacked nose/beard blocks");
            float lowestRim=Feature("SafetyGlasses").GetComponentsInChildren<MeshFilter>()
                .Where(m=>m.name.StartsWith("RimHorizontal_")).Min(m=>InHead(m).min.y);
            Assert.Greater(lowestRim,nose.max.y,"Nose stays below eye frames");
            Material hair=Feature("Beard").GetComponent<Renderer>().sharedMaterial;
            Assert.IsNull(hair.mainTexture,"Machinery rust texture is not facial hair");
            Assert.Less(hair.GetFloat("_Metallic"),.2f);
            float upperFrame=Feature("SafetyGlasses").GetComponentsInChildren<MeshFilter>().Max(m=>InHead(m).max.y);
            Assert.Less(brim.size.y,.015f);Assert.Less(brim.max.z,.15f);
            Assert.Greater(brim.min.y,upperFrame+.03f,"Hat brim clears the eye region");
            Assert.AreSame(Feature("YellowHardHat").GetComponent<Renderer>().sharedMaterial,
                Feature("HardHatBrim").GetComponent<Renderer>().sharedMaterial,"Retain yellow industrial PPE");
            yield return null;
        }

        [UnityTest] public IEnumerator FaceFollowsExistingPosesAndViewVisibilityWithoutMovingPlayerOrCamera()
        {
            CharacterController motor=Player.GetComponent<CharacterController>();
            Camera camera=Player.GetComponentInChildren<Camera>();
            Renderer[] renderers=Head.GetComponentsInChildren<Renderer>(true);
            Bounds eye=Envelope("EyePupil_Left");
            foreach(int mode in new[]{0,1})foreach(float height in new[]{1.8f,1.1f,.75f})
            {
                Mode(mode);motor.height=height;motor.center=Vector3.up*(height*.5f);
                Vector3 playerPosition=Player.transform.position,cameraPosition=camera.transform.position;
                Quaternion playerRotation=Player.transform.rotation,cameraRotation=camera.transform.rotation;
                Call(Worker,"LateUpdate");
                Assert.AreEqual(playerPosition,Player.transform.position);Assert.AreEqual(playerRotation,Player.transform.rotation);
                Assert.AreEqual(cameraPosition,camera.transform.position);Assert.AreEqual(cameraRotation,camera.transform.rotation);
                Assert.That(motor.height,Is.EqualTo(height));Assert.AreEqual(Vector3.up*(height*.5f),motor.center);
                Assert.Less(Vector3.Distance(Envelope("EyePupil_Left").center,eye.center),.0001f);
                foreach(Renderer r in renderers)
                {
                    Assert.IsTrue(r.enabled);Assert.IsTrue(r.transform.IsChildOf(Head));
                    Assert.AreEqual(mode==0?ShadowCastingMode.ShadowsOnly:ShadowCastingMode.On,r.shadowCastingMode);
                }
                Transform legacy=Player.transform.Find("fk");
                Assert.IsTrue(legacy.GetComponentsInChildren<Renderer>(true).All(r=>!r.enabled));
            }
            Assert.AreEqual(1,Player.GetComponentsInChildren<CharacterController>(true).Length);
            Assert.AreEqual(1,Player.GetComponentsInChildren<Camera>(true).Length);
            Assert.IsEmpty(Head.GetComponentsInChildren<Collider>(true));
            yield return null;
        }

        [UnityTest] public IEnumerator ThreeSceneLoadsDoNotAccumulateHeadRenderersOrOwnedMaterials()
        {
            int count=Head.GetComponentsInChildren<Renderer>(true).Length;
            for(int reload=0;reload<3;reload++)
            {
                Material old=Feature("Beard").GetComponent<Renderer>().sharedMaterial;
                yield return scope.Close();yield return null;
                Assert.IsTrue(old==null,"Scene-owned facial material must be disposed");
                scope=new C3V11Phase2RecyclingLineTests();yield return scope.Open();
                Assert.AreEqual(count,Head.GetComponentsInChildren<Renderer>(true).Length);
                Assert.AreEqual(1,Object.FindObjectsOfType(Worker.GetType()).Length);
                Assert.AreEqual(1,Player.GetComponents<CharacterController>().Length);
                Assert.AreEqual(1,Player.GetComponentsInChildren<Camera>(true).Length);
            }
        }
    }
}
