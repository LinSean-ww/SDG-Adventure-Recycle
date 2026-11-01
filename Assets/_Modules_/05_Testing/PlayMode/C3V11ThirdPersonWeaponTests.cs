using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace M6IntegrationTests
{
    // The accepted inventory/prefs/input fixture is reused, not replaced.
    // Tests equip the actual runtime weapon; pose controls simulate accepted
    // orbit/lean/ADS state, not hardware key/mouse events or a substitute gun.
    public sealed class C3V11ThirdPersonWeaponTests
    {
        private C3V11Phase2RecyclingLineTests scope;
        private const BindingFlags Any=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static Type TypeOf(string name) => C3V11Phase2RecyclingLineTests.GameType(name);
        private static object Get(object owner,string name) => C3V11Phase2RecyclingLineTests.Member(owner,name);
        private static object Call(object owner,string name,params object[] args) => C3V11Phase2RecyclingLineTests.Call(owner,name,args);
        private static void Set(object owner,string name,object value)
        {
            FieldInfo field=owner.GetType().GetField(name,Any);Assert.IsNotNull(field,name);field.SetValue(owner,value);
        }
        private static GameObject Player => GameObject.Find("M2_Player");
        private static Component Game => TypeOf("EcoAdventureRuntime").GetProperty("Instance").GetValue(null) as Component;
        private static Component Worker => Player.GetComponent(TypeOf("C3TreasurePuzzle.C3WorkerAppearance"));
        private static Component Rig => Player.GetComponents<MonoBehaviour>().Single(c=>c && c.GetType().Name=="PlayerPerspectiveRig");
        private static Camera Camera => Player.GetComponentInChildren<Camera>();
        private static GameObject Model => Get(Game,"weaponModel") as GameObject;
        private static Component View => Model.GetComponent(TypeOf("EcoQuantumWeaponView"));
        private static void Mode(int value)
        {
            PropertyInfo mode=Rig.GetType().Assembly.GetType("SDGAdventure.SharedPlayer.PlayerViewPreference",true).GetProperty("Mode");
            mode.SetValue(null,Enum.ToObject(mode.PropertyType,value));Call(Rig,"ApplyMode");
        }
        private static void FreezeScene()
        {
            ((Behaviour)Player.GetComponent(TypeOf("C3TreasurePuzzle.C3FirstPersonController"))).enabled=false;
            ((Behaviour)Player.GetComponent(TypeOf("C3TreasurePuzzle.C3PlayerInteractor"))).enabled=false;
            Set(Rig,"smoothTime",0f);
        }
        private static void Equip()
        {
            Call(Game,"AddItem","eco_quantum_projector","循環量子投射器",1);
            ((string[])Get(Game,"quickSlots"))[0]="eco_quantum_projector";Set(Game,"selectedSlot",0);
            Call(Game,"BindCamera");Call(Game,"BuildHeldModel");
            Assert.IsNotNull(Model);Assert.IsNotNull(View);
        }
        private static void Pose(float yaw,float pitch,bool ads=false)
        {
            Set(Rig,"orbitYaw",yaw);Set(Rig,"orbitPitch",pitch);
            Call(Game,"LateUpdate"); // official equipment/presentation handoff
            if(ads)Call(View,"SetThirdPersonPresentation",Rig,true,true);
            Call(Worker,"LateUpdate");Call(Rig,"LateUpdate");Call(View,"LateUpdate");
        }
        private static void RequireGrips()
        {
            Transform primary=(Transform)Get(View,"PrimaryGrip"),support=(Transform)Get(View,"SupportGrip");
            Assert.IsNotNull(primary);Assert.IsNotNull(support);
            Transform right=(Transform)Get(Worker,"rightHand"),left=(Transform)Get(Worker,"leftHand");
            Assert.That(Vector3.Distance(right.position,primary.position),Is.LessThan(.025f),"Right hand misses the real grip.");
            Assert.That(Vector3.Distance(left.position,support.position),Is.LessThan(.025f),"Left hand misses the real foregrip.");
            Vector3 aim=(Vector3)Get(View,"VisualAimPoint")-Model.transform.position;
            Assert.Greater(Vector3.Dot(aim.normalized,Model.transform.forward),.999f,"Weapon does not converge on the camera aim point.");
        }
        [UnitySetUp] public IEnumerator Open()
        {
            scope=new C3V11Phase2RecyclingLineTests();yield return scope.Open();FreezeScene();
            CharacterController capsule=Player.GetComponent<CharacterController>();capsule.enabled=false;
            Player.transform.SetPositionAndRotation(new Vector3(128,128,128),Quaternion.identity);capsule.enabled=true;
            Physics.SyncTransforms();
        }
        [UnityTearDown] public IEnumerator Close(){if(scope!=null)yield return scope.Close();}

        [UnityTest] public IEnumerator EquippedWeaponAndBothHandsFollowPitchOrbitAndViewSwitches()
        {
            Equip();Mode(1);GameObject weapon=Model;
            CharacterController capsule=Player.GetComponent<CharacterController>();
            Vector3 position=Player.transform.position,center=capsule.center;Quaternion rotation=Player.transform.rotation;
            float height=capsule.height,radius=capsule.radius;
            foreach(float yaw in new[]{0f,90f,180f,270f})foreach(float pitch in new[]{-60f,0f,48f})
            {
                Pose(yaw,pitch);RequireGrips();
                Assert.AreEqual(position,Player.transform.position);Assert.AreEqual(rotation,Player.transform.rotation);
                Assert.AreEqual(center,capsule.center);Assert.AreEqual(height,capsule.height);Assert.AreEqual(radius,capsule.radius);
                Assert.AreSame(weapon,Model);Assert.AreSame(Player.transform,weapon.transform.parent);
            }
            Pose(0,0);
            GameObject aimSurface=GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                aimSurface.name="V11_TEST_CAMERA_AIM_SURFACE";
                aimSurface.transform.position=Camera.transform.position+Camera.transform.forward*6f;
                aimSurface.transform.localScale=new Vector3(2f,2f,.1f);Physics.SyncTransforms();Pose(0,0);
                Assert.IsTrue(Physics.Raycast(Camera.transform.position,Camera.transform.forward,out RaycastHit hit,85f,~0,QueryTriggerInteraction.Ignore));
                Assert.AreSame(aimSurface.GetComponent<Collider>(),hit.collider);
                Assert.Less(Vector3.Distance(hit.point,(Vector3)Get(View,"VisualAimPoint")),.001f);
                RequireGrips();
            }
            finally {aimSurface.SetActive(false);Object.Destroy(aimSurface);Physics.SyncTransforms();}
            for(int i=0;i<3;i++)
            {
                Mode(0);Call(Game,"LateUpdate");Call(Worker,"LateUpdate");
                Assert.AreSame(Camera.transform,weapon.transform.parent);
                Assert.IsFalse((bool)Get(View,"UsesThirdPersonPresentation"));
                Assert.That(Quaternion.Angle(((Transform)Get(Worker,"leftElbow")).localRotation,Quaternion.identity),Is.LessThan(.01f));
                Mode(1);Pose(0,0);RequireGrips();Assert.AreSame(weapon,Model);
            }
            Assert.AreEqual(1,Object.FindObjectsOfType(TypeOf("EcoQuantumWeaponView")).Length);
            Assert.IsFalse(weapon.GetComponentsInChildren<Collider>(true).Any(c=>c.enabled));
            Set(Game,"selectedSlot",1);Call(Game,"LateUpdate");yield return null;yield return null;
            Call(Worker,"LateUpdate");Assert.IsTrue(weapon==null);Assert.IsNull(Model);
            Assert.That(Quaternion.Angle(((Transform)Get(Worker,"rightElbow")).localRotation,Quaternion.identity),Is.LessThan(.01f));
        }

        [UnityTest] public IEnumerator WalkingLeanProneAndAdsKeepGripsWithoutChangingMovementState()
        {
            Equip();Mode(1);CharacterController capsule=Player.GetComponent<CharacterController>();
            Component controller=Player.GetComponent(TypeOf("C3TreasurePuzzle.C3FirstPersonController"));
            float originalHeight=capsule.height;Vector3 originalCenter=capsule.center;
            try
            {
                foreach(float height in new[]{1.8f,1.1f,.75f})foreach(float lean in new[]{-1f,1f,0f})
                {
                    // Stance/lean are accepted controller outputs, not changes to
                    // production controller dimensions or the configured movement rules.
                    capsule.height=height;capsule.center=Vector3.up*(height*.5f);Set(controller,"leanAmount",lean);
                    capsule.Move(Vector3.right*.04f);Vector3 moved=Player.transform.position;
                    Pose(0,-35f,true);RequireGrips();
                    Assert.AreEqual(moved,Player.transform.position);Assert.AreEqual(height,capsule.height);
                    Assert.AreEqual(Vector3.up*(height*.5f),capsule.center);
                    Assert.That(((Transform)Get(Worker,"chest")).localPosition.x,Is.EqualTo(lean*.075f).Within(.001f));
                    for(int frame=0;frame<3;frame++){yield return null;Pose(0,35f,true);}
                    RequireGrips();Assert.Greater((float)Get(View,"adsBlend"),0f);
                }
            }
            finally {capsule.height=originalHeight;capsule.center=originalCenter;Set(controller,"leanAmount",0f);}
        }

        [UnityTest] public IEnumerator CloseWallRaisesHeldRifleAndKeepsBothHandsOnGrips()
        {
            Equip();Mode(1);Pose(0,0);
            GameObject obstruction=GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                obstruction.name="V11_TEST_RIFLE_CLEARANCE_WALL";
                obstruction.transform.SetPositionAndRotation(
                    Model.transform.position+Model.transform.forward*.43f,
                    Quaternion.LookRotation(Model.transform.forward));
                obstruction.transform.localScale=new Vector3(1f,1.1f,.14f);
                Physics.SyncTransforms();
                for(int i=0;i<6;i++){yield return null;Pose(0,0);}
                Assert.That((float)Get(View,"obstructionBlend"),Is.GreaterThan(.4f),
                    "A solid obstacle in front of the muzzle must trigger high-ready pose.");
                Transform right=(Transform)Get(Worker,"rightHand");
                Transform left=(Transform)Get(Worker,"leftHand");
                Assert.Less(Vector3.Distance(right.position,((Transform)Get(View,"PrimaryGrip")).position),.025f);
                Assert.Less(Vector3.Distance(left.position,((Transform)Get(View,"SupportGrip")).position),.025f);
                Transform muzzle=(Transform)Get(View,"muzzle");
                Collider wall=obstruction.GetComponent<Collider>();
                Assert.Greater(Vector3.Distance(muzzle.position,wall.ClosestPoint(muzzle.position)),.025f,
                    "The muzzle must not remain embedded in the wall after the visual tuck.");
            }
            finally {obstruction.SetActive(false);Object.Destroy(obstruction);Physics.SyncTransforms();}
        }

        [UnityTest] public IEnumerator FirstPersonBarrelRetractsBeforeSolidWallWithoutChangingRig()
        {
            Equip();Mode(0);Call(Game,"LateUpdate");Call(Rig,"LateUpdate");Call(View,"LateUpdate");
            Assert.IsFalse((bool)Get(View,"UsesThirdPersonPresentation"));
            Assert.AreSame(Camera.transform,Model.transform.parent);
            GameObject rifle=(GameObject)Get(View,"rifle");
            Transform muzzle=(Transform)Get(View,"muzzle");
            Assert.IsNotNull(rifle);Assert.IsNotNull(muzzle);
            float freeLocalZ=rifle.transform.localPosition.z;
            GameObject wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                wall.name="V11_TEST_FPS_BARREL_CLEARANCE";
                wall.transform.SetPositionAndRotation(Camera.transform.position+Camera.transform.forward*1.20f,
                    Quaternion.LookRotation(Camera.transform.forward));
                wall.transform.localScale=new Vector3(3f,3f,.12f);
                Physics.SyncTransforms();
                for(int i=0;i<3;i++)
                {
                    yield return null;
                    Call(Game,"LateUpdate");Call(Rig,"LateUpdate");Call(View,"LateUpdate");
                }
                Assert.Less(rifle.transform.localPosition.z,freeLocalZ-.20f,
                    "First-person gun must retract, not intersect a close wall.");
                Assert.That((float)Get(View,"firstPersonClearance"),Is.LessThan(1.20f));
                float muzzleDepth=Vector3.Dot(muzzle.position-Camera.transform.position,
                    Camera.transform.forward);
                Assert.Less(muzzleDepth,1.20f-.06f,
                    "First-person muzzle must remain on the camera side of the wall.");
                Assert.IsFalse((bool)Get(View,"UsesThirdPersonPresentation"));
                Assert.AreSame(Camera.transform,Model.transform.parent);
                Assert.IsFalse(Model.GetComponentsInChildren<Collider>(true).Any(c=>c.enabled));
            }
            finally {wall.SetActive(false);Object.Destroy(wall);Physics.SyncTransforms();}
        }

        [UnityTest] public IEnumerator SwitchingToThirdPersonAfterFpsWallRetractionRestoresRiflePose()
        {
            Equip();Mode(0);Call(Game,"LateUpdate");Call(Rig,"LateUpdate");Call(View,"LateUpdate");
            GameObject rifle=(GameObject)Get(View,"rifle");
            float freeLocalZ=rifle.transform.localPosition.z;
            GameObject wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                wall.name="V11_TEST_FPS_TO_TPS_CLEARANCE_HANDOFF";
                wall.transform.SetPositionAndRotation(
                    Camera.transform.position+Camera.transform.forward*1.20f,
                    Quaternion.LookRotation(Camera.transform.forward));
                wall.transform.localScale=new Vector3(3f,3f,.12f);
                Physics.SyncTransforms();
                for(int i=0;i<3;i++)
                {
                    yield return null;
                    Call(Game,"LateUpdate");Call(Rig,"LateUpdate");Call(View,"LateUpdate");
                }
                Assert.Less(rifle.transform.localPosition.z,freeLocalZ-.20f,
                    "FPS rifle must retract before perspective handoff.");
            }
            finally {wall.SetActive(false);Object.Destroy(wall);Physics.SyncTransforms();}
            Mode(1);Pose(0,0);
            Assert.IsTrue((bool)Get(View,"UsesThirdPersonPresentation"));
            Assert.That(rifle.transform.localPosition.sqrMagnitude,Is.LessThan(.000001f),
                "FPS clearance offset must not carry into third-person grips.");
            Assert.That(Quaternion.Angle(rifle.transform.localRotation,Quaternion.identity),Is.LessThan(.01f),
                "FPS recoil/ADS rotation must not carry into third-person grips.");
            RequireGrips();
        }

        [UnityTest] public IEnumerator EquippedSceneReloadsReleaseOldViewsAndDoNotDuplicateAnchors()
        {
            Equip();Mode(1);Pose(0,0);
            const string scene="Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
            for(int round=0;round<2;round++)
            {
                GameObject oldWeapon=Model;Component oldWorker=Worker;
                yield return SceneManager.LoadSceneAsync(scene,LoadSceneMode.Single);yield return null;yield return null;
                FreezeScene();Call(Game,"BindCamera");Call(Game,"BuildHeldModel");Mode(1);Pose(0,0);
                Assert.IsTrue(oldWeapon==null && oldWorker==null);
                Assert.AreEqual(1,Object.FindObjectsOfType(TypeOf("EcoQuantumWeaponView")).Length);
                Assert.AreEqual(1,Player.GetComponentsInChildren<Transform>(true).Count(t=>t.name=="V11_ThirdPersonWeaponAnchor"));
                Assert.AreEqual(1,Player.GetComponents<CharacterController>().Length);
                Assert.AreEqual(1,Player.GetComponentsInChildren<Camera>(true).Length);RequireGrips();
            }
        }
    }
}
