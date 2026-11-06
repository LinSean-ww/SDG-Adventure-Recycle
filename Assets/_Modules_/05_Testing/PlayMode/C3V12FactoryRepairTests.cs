using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace M6IntegrationTests
{
    // Keep the accepted asmdef boundary and reuse real input/inventory isolation.
    // No direct successful Interact calls: pickup tests dispatch real F state events.
    public sealed class C3V12FactoryRepairTests
    {
        private C3V11ThirdPersonInteractionTests scope;
        private const BindingFlags Any=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static Type T(string name)=>C3V11Phase2RecyclingLineTests.GameType(name);
        private static object Get(object o,string name)=>C3V11Phase2RecyclingLineTests.Member(o,name);
        private static object Call(object o,string name,params object[] args)=>C3V11Phase2RecyclingLineTests.Call(o,name,args);
        private static void Set(object o,string name,object value)=>o.GetType().GetField(name,Any).SetValue(o,value);
        private static GameObject Player=>GameObject.Find("M2_Player");
        private static CharacterController Body=>Player.GetComponent<CharacterController>();
        private static Camera Camera=>Player.GetComponentInChildren<Camera>();
        private static Component Controller=>Player.GetComponent(T("C3TreasurePuzzle.C3FirstPersonController"));
        private static Component Rig=>Player.GetComponents<MonoBehaviour>().Single(c=>c && c.GetType().Name=="PlayerPerspectiveRig");
        private static Component Game=>T("EcoAdventureRuntime").GetProperty("Instance").GetValue(null) as Component;
        private static Component Part(string id)=>Object.FindObjectsOfType(T("C3TreasurePuzzle.C3CollectiblePart"))
            .Cast<Component>().Single(c=>(string)Get(c,"PartId")==id);
        private static void Mode(int value)=>typeof(C3V11ThirdPersonInteractionTests)
            .GetMethod("Mode",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{value});
        private void Press()=>typeof(C3V11ThirdPersonInteractionTests).GetMethod("Press",Any).Invoke(scope,new object[]{false});
        private static int Quantity(string id)=>((IEnumerable)Get(Game,"inventory")).Cast<object>()
            .Where(e=>(string)Get(e,"id")==id).Sum(e=>(int)Get(e,"quantity"));
        [UnitySetUp] public IEnumerator Open()
        {scope=new C3V11ThirdPersonInteractionTests();yield return scope.Open();}
        [UnityTearDown] public IEnumerator Close(){if(scope!=null)yield return scope.Close();}
        private static void Place(Vector3 point)
        {
            Body.enabled=false;Player.transform.SetPositionAndRotation(point,Quaternion.identity);
            Body.enabled=true;Physics.SyncTransforms();
        }

        [UnityTest] public IEnumerator ThreeReloadsKeepOneAuthoritativeFactoryAndNoPulseOrphans()
        {
            for(int run=0;run<3;run++)
            {
                foreach(string name in new[]{"C3_ReferenceRecyclingFactory_Visuals","C3_P2_RecyclingLine","C3_V3_VerifiedCirculation"})
                    Assert.AreEqual(1,Object.FindObjectsOfType<Transform>().Count(t=>t.name==name),name);
                Component geometry=C3V11Phase2RecyclingLineTests.Find("C3TreasurePuzzle.C3RecyclingLineGeometry");
                Transform root=(Transform)Get(geometry,"BuildRoot");int count=root.GetComponentsInChildren<Transform>(true).Length;
                ((Behaviour)geometry).enabled=false;((Behaviour)geometry).enabled=true;
                Assert.AreEqual(count,root.GetComponentsInChildren<Transform>(true).Length);
                Assert.IsTrue((bool)Call(geometry,"ConnectionsMatch",.01f));
                Assert.AreEqual(0,Object.FindObjectsOfType<Transform>().Count(t=>t.name.StartsWith("V12_QuantumPulse_")));
                if(run<2){yield return scope.Close();scope=new C3V11ThirdPersonInteractionTests();yield return scope.Open();}
            }
        }

        [UnityTest] public IEnumerator BFlightHasRealLevelDeckAndMatchingPhysicalTreads()
        {
            Transform root=GameObject.Find("C3_V3_VerifiedCirculation").transform;
            Transform east=root.Find("STAIR_B_EAST"),west=root.Find("STAIR_A_WEST");
            BoxCollider landing=root.Find("B_EAST_LEVEL_LANDING").GetComponent<BoxCollider>();
            Assert.That(landing.bounds.min.z,Is.EqualTo(10.10f).Within(.001f));
            Assert.That(landing.bounds.max.z,Is.EqualTo(13.70f).Within(.001f));
            Assert.That(landing.bounds.max.y,Is.EqualTo(4.5f).Within(.001f));
            float last=.56f;
            for(int i=1;i<=22;i++)
            {
                BoxCollider tread=east.Find("TREAD_"+i.ToString("D2")).GetComponent<BoxCollider>();
                Renderer visible=tread.GetComponent<Renderer>();
                Assert.IsTrue(tread.enabled && visible.enabled && !tread.isTrigger);
                Assert.Less(Vector3.Distance(tread.bounds.size,visible.bounds.size),.001f);
                Assert.That(tread.bounds.max.y-last,Is.InRange(.17f,Body.stepOffset));last=tread.bounds.max.y;
                if(i==22)Assert.That(tread.bounds.max.z,Is.EqualTo(landing.bounds.min.z).Within(.001f));
            }
            Assert.That(west.Find("TREAD_22").GetComponent<Collider>().bounds.max.z,Is.EqualTo(13.7f).Within(.001f));
            foreach(int side in new[]{-1,1})
            {
                Transform guard=root.Find("B_EAST_LEVEL_GUARDS/GUARD_LEVEL_"+side);
                Assert.IsTrue(guard.GetComponent<MeshCollider>().enabled);
                Assert.That(guard.GetComponent<Collider>().bounds.min.y,Is.EqualTo(4.5f).Within(.01f));
            }
            yield return null;
        }

        [UnityTest] public IEnumerator BRouteClimbsAndDescendsInBothViewsAndStances()
        {
            foreach(int view in new[]{0,1})foreach(bool prone in new[]{false,true})
            {
                Mode(view);Set(Rig,"orbitYaw",0f);Set(Controller,"isProne",prone);
                Place(new Vector3(-1.55f,.60f,3.1f));
                yield return Walk(14.2f,4.5f);
                yield return Walk(3.1f,.56f);
            }
        }
        [UnityTest] public IEnumerator BNameplateIsBelowTheDeckAndVisibleFromTheGroundAisle()
        {
            Transform sign=GameObject.Find("C3_IndustrialSign_BZone").transform;
            Bounds plate=sign.Find("FixedMetalBacking").GetComponent<Renderer>().bounds;
            Assert.Less(plate.max.y,4.32f);
            Assert.GreaterOrEqual(plate.min.y,3.579f);
            foreach(float z in new[]{11.8f,12.5f,13.2f})
            foreach(float h in new[]{-.22f,0f,.22f})
            foreach(float lateral in new[]{-1.1f,0f,1.1f})
            {
                Vector3 eye=new Vector3(-3.5f,2.06f,z),target=sign.position+Vector3.up*h+Vector3.forward*lateral;
                Vector3 ray=target-eye;
                foreach(RaycastHit hit in Physics.RaycastAll(eye,ray.normalized,ray.magnitude,~0,QueryTriggerInteraction.Ignore))
                    Assert.IsFalse(hit.transform.IsChildOf(GameObject.Find("C3_V3_VerifiedCirculation").transform),
                        "Stair/deck still blocks B signage: "+hit.collider.name);
            }
            yield return null;
        }
        private static IEnumerator Walk(float targetZ,float targetY)
        {
            for(int tick=0;tick<1400 && Mathf.Abs(Player.transform.position.z-targetZ)>.09f;tick++)
            {
                float direction=Mathf.Sign(targetZ-Player.transform.position.z);
                Call(Controller,"TickMovement",new Vector2(0,direction),false,false,false,.02f);
                Physics.SyncTransforms();if(tick%5==0)yield return null;
            }
            Assert.That(Player.transform.position.z,Is.EqualTo(targetZ).Within(.1f),"Stalled at flight/deck joint");
            Assert.That(Player.transform.position.y,Is.EqualTo(targetY).Within(.12f));
        }

        [UnityTest] public IEnumerator QuestPartsRestOnSurfacesWithoutPenetratingEquipment()
        {
            foreach(string id in new[]{"fuse","filter_module"})
            {
                Component part=Part(id);Collider pickup=part.GetComponent<Collider>();
                Renderer[] visuals=part.transform.Find("FactoryPartVisual").GetComponentsInChildren<Renderer>();
                Bounds bounds=visuals[0].bounds;foreach(Renderer r in visuals)bounds.Encapsulate(r.bounds);
                RaycastHit[] supports=Physics.RaycastAll(bounds.center,Vector3.down,2f,~0,QueryTriggerInteraction.Ignore)
                    .Where(h=>!h.transform.IsChildOf(part.transform)).OrderBy(h=>h.distance).ToArray();
                Assert.Greater(supports.Length,0,id+" is floating without a support");
                Assert.That(bounds.min.y-supports[0].point.y,Is.InRange(-.002f,id=="fuse"?.020f:.015f),id);
                foreach(Collider other in Physics.OverlapBox(pickup.bounds.center,pickup.bounds.extents,
                    Quaternion.identity,~0,QueryTriggerInteraction.Ignore))
                {
                    if(other.transform.IsChildOf(part.transform))continue;
                    bool penetrates=Physics.ComputePenetration(pickup,pickup.transform.position,pickup.transform.rotation,
                        other,other.transform.position,other.transform.rotation,out _,out float depth);
                    Assert.IsFalse(penetrates && depth>.002f,id+" intersects "+other.name+" depth="+depth);
                }
            }
            yield return null;
        }

        [UnityTest] public IEnumerator ScenePlacedPartsCanBeCollectedWithFOnceInFirstPerson()
        {yield return CollectPlaced(0);}
        [UnityTest] public IEnumerator ScenePlacedPartsCanBeCollectedWithFOnceInThirdPerson()
        {yield return CollectPlaced(1);}
        private IEnumerator CollectPlaced(int mode)
        {
            Mode(mode);
            foreach(string id in new[]{"fuse","filter_module"})
            {
                Component part=Part(id);Vector3 target=part.GetComponent<Collider>().bounds.center;
                Vector3 approach=id=="fuse"?new Vector3(-20.7f,.58f,19.3f):new Vector3(-4.85f,.58f,18f);
                Place(approach);
                Vector3 eye=approach+Vector3.up*1.5f;
                if(mode==1)eye+=id=="fuse"?Vector3.back*.6f:Vector3.left*.6f;
                Camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye));
                Component interactor=Player.GetComponent(T("C3TreasurePuzzle.C3PlayerInteractor"));
                Call(interactor,"SetCamera",Camera);Physics.SyncTransforms();
                Assert.AreSame(part,Call(interactor,"FindTarget"),id+" is obstructed at its real scene position");
                Press();Assert.IsFalse(part.gameObject.activeSelf);Assert.AreEqual(1,Quantity(id));
                Press();Assert.AreEqual(1,Quantity(id));yield return null;
            }
            Assert.AreEqual(2,Get(C3V11Phase2RecyclingLineTests.Find("C3TreasurePuzzle.C3TreasureProgress"),"CollectedCount"));
            Assert.AreEqual(0,Quantity("control_chip"),"Relocation must not skip the upper-floor objective");
            Assert.IsFalse((bool)Call(C3V11Phase2RecyclingLineTests.Find("C3TreasurePuzzle.C3DeviceState"),"TryActivate"));
        }

        [UnityTest] public IEnumerator FirstPersonNearPlaneClearsRealSorterFrontAndSideAfterLean()
        {
            Mode(0);Component clearance=Player.GetComponent(T("C3TreasurePuzzle.C3FactoryCameraClearance"));
            Collider panel=GameObject.Find("SORTER_MAIN_BODY_2").transform.Find("FRONT_TOP").GetComponent<Collider>();
            Collider side=GameObject.Find("SORTER_MAIN_BODY_2").GetComponentsInChildren<Collider>()
                .Where(c=>c.name=="MACHINE_SIDE").OrderBy(c=>c.transform.position.x).Last();
            foreach(Collider obstacle in new[]{panel,side})
            {
                Vector3 normal=obstacle==panel?Vector3.back:Vector3.right;
                Vector3 face=obstacle.bounds.center+normal*(obstacle==panel?.07f:.08f);
                Vector3 desiredEye=face+normal*.06f;
                Vector3 neutralEye=face+normal*.31f;
                Place(neutralEye-Vector3.up*1.5f);
                Camera.transform.SetPositionAndRotation(desiredEye,Quaternion.LookRotation(-normal));
                Physics.SyncTransforms();Call(clearance,"LateUpdate");
                float radius=(float)clearance.GetType().GetMethod("NearPlaneRadius").Invoke(null,new object[]{Camera});
                Assert.GreaterOrEqual(Vector3.Distance(obstacle.ClosestPoint(Camera.transform.position),Camera.transform.position),radius-.005f);
                Assert.IsTrue(obstacle.enabled && !obstacle.isTrigger);
            }
            Mode(1);Vector3 unchanged=Camera.transform.position;Call(clearance,"LateUpdate");
            Assert.AreEqual(unchanged,Camera.transform.position,"C3 guard must not overwrite the third-person solver");
            yield return null;
        }

        [UnityTest] public IEnumerator OriginalCapsuleIsStoppedByRealFrontAndSidePanelsInBothStances()
        {
            Transform machine=GameObject.Find("SORTER_MAIN_BODY_2").transform;
            Collider front=machine.Find("FRONT_TOP").GetComponent<Collider>();
            Collider side=machine.GetComponentsInChildren<Collider>().Where(c=>c.name=="MACHINE_SIDE")
                .OrderBy(c=>c.transform.position.x).Last();
            foreach(bool prone in new[]{false,true})foreach(Collider obstacle in new[]{front,side})
            {
                Vector3 normal=obstacle==front?Vector3.back:Vector3.right;
                float floor=prone?3.25f:2.14f;
                float height=prone?.75f:1.8f;
                Vector3 face=obstacle.bounds.center+normal*(obstacle==front?.07f:.08f);
                Vector3 start=face+normal*.8f;start.y=floor;
                Place(start);Body.height=height;Body.center=Vector3.up*height*.5f;
                C3V11CollisionRecorder contacts=Player.GetComponent<C3V11CollisionRecorder>();
                Assert.IsNotNull(contacts);contacts.SideContacts.Clear();Physics.SyncTransforms();
                for(int i=0;i<80;i++){Body.Move(-normal*.025f);if(i%5==0)yield return null;}
                Assert.IsTrue(contacts.SideContacts.Contains(obstacle),"An unrelated obstacle stopped the body");
                Vector3 centre=Player.transform.TransformPoint(Body.center);
                Assert.GreaterOrEqual(Vector3.Dot(centre-face,normal),Body.radius-Body.skinWidth-.02f);
                Renderer mesh=obstacle.GetComponent<Renderer>();
                Assert.Less(Vector3.Distance(mesh.bounds.size,obstacle.bounds.size),.001f);
                Assert.IsTrue(obstacle.enabled && mesh.enabled);
            }
        }

        private static Component Equip()
        {
            Call(Game,"AddItem","eco_quantum_projector","循環量子投射器",1);
            ((string[])Get(Game,"quickSlots"))[0]="eco_quantum_projector";Set(Game,"selectedSlot",0);
            Call(Game,"BuildHeldModel");GameObject model=(GameObject)Get(Game,"weaponModel");
            return model.GetComponent(T("EcoQuantumShotVfx"));
        }
        [UnityTest] public IEnumerator OfficialShootUsesPooledVfxAndKeepsHitscanAmmoAndCooldown()
        {
            Mode(0);Place(new Vector3(128,128,128));
            Camera.transform.SetPositionAndRotation(Player.transform.position+Vector3.up*1.5f,Quaternion.identity);
            Component vfx=Equip();int ammo=(int)Get(Game,"ammoInMagazine"),shots=(int)Get(Game,"totalShots");
            Set(Game,"nextShotTime",0f);Call(Game,"Shoot");
            Assert.AreEqual(ammo-1,Get(Game,"ammoInMagazine"));Assert.AreEqual(shots+1,Get(Game,"totalShots"));
            Assert.AreEqual(1,Get(vfx,"ActiveCount"));Call(Game,"Shoot");
            Assert.AreEqual(ammo-1,Get(Game,"ammoInMagazine"));Assert.AreEqual(1,Get(vfx,"ActiveCount"));
            yield return null;
        }
        [UnityTest] public IEnumerator HundredShotsReuseMaterialsExpireAndClearWithWeapon()
        {
            Place(new Vector3(128,128,128));Component vfx=Equip();
            int materials=Resources.FindObjectsOfTypeAll<Material>().Count(m=>m.name=="V12 pooled quantum glow");
            for(int i=0;i<100;i++)Call(vfx,"Fire",new Vector3(128,130,128),new Vector3(128,130,128.4f),
                new Vector3(128,130,140),Player.transform,true);
            Assert.AreEqual(12,Get(vfx,"ActiveCount"));
            Assert.AreEqual(materials,Resources.FindObjectsOfTypeAll<Material>().Count(m=>m.name=="V12 pooled quantum glow"));
            float deadline=Time.time+.5f;
            while((int)Get(vfx,"ActiveCount")>0 && Time.time<deadline)yield return null;
            Assert.AreEqual(0,Get(vfx,"ActiveCount"));
            GameObject model=(GameObject)Get(Game,"weaponModel");Object.Destroy(model);yield return null;yield return null;
            Assert.AreEqual(0,Object.FindObjectsOfType<Transform>().Count(t=>t.name.StartsWith("V12_QuantumPulse_")));
            Assert.AreEqual(materials-1,Resources.FindObjectsOfTypeAll<Material>().Count(m=>m.name=="V12 pooled quantum glow"));
        }
        [UnityTest] public IEnumerator MuzzleBehindCoverCannotDrawThroughWallOrChangeGameplayHit()
        {
            Place(new Vector3(128,128,128));Component vfx=Equip();GameObject wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                wall.transform.position=new Vector3(128,130,130);wall.transform.localScale=new Vector3(3,3,.2f);Physics.SyncTransforms();
                Vector3 hit=new Vector3(128,130,140);
                Call(vfx,"Fire",new Vector3(128,130,128),new Vector3(128,130,131),hit,Player.transform,true);
                Assert.Less(((Vector3)Get(vfx,"LastVisualStart")).z,129.9f);
                Assert.Less(((Vector3)Get(vfx,"LastVisualEnd")).z,129.9f);
                Assert.AreEqual(new Vector3(128,130,140),hit);
            }
            finally{Object.Destroy(wall);}
            yield return null;
        }
        [UnityTest] public IEnumerator BothViewsAndAdsUseActualHitPointAndMuzzleFlash()
        {
            Place(new Vector3(128,128,128));Component vfx=Equip();
            GameObject wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                wall.transform.position=new Vector3(128,129.5f,140);
                wall.transform.localScale=new Vector3(4,4,.2f);Physics.SyncTransforms();
                foreach(int mode in new[]{0,1})foreach(bool ads in new[]{false,true})
                {
                    Mode(mode);Camera.transform.SetPositionAndRotation(new Vector3(128,129.5f,128),Quaternion.identity);
                    Component view=((GameObject)Get(Game,"weaponModel")).GetComponent(T("EcoQuantumWeaponView"));
                    Call(view,"SetThirdPersonPresentation",Rig,mode==1,ads);
                    Call(view,"LateUpdate");
                    Assert.IsTrue(Physics.Raycast(Camera.transform.position,Camera.transform.forward,out RaycastHit hit,85f));
                    Assert.AreSame(wall.GetComponent<Collider>(),hit.collider);
                    Call(vfx,"Clear");Set(Game,"nextShotTime",0f);Call(Game,"Shoot");
                    Assert.AreEqual(1,Get(vfx,"ActiveCount"));
                    Assert.Less(Vector3.Distance(hit.point,(Vector3)Get(vfx,"LastVisualEnd")),.025f);
                    Assert.IsTrue(vfx.GetComponentsInChildren<LineRenderer>().Any(l=>l.name=="MuzzleStar" && l.enabled));
                }
            }
            finally{Object.Destroy(wall);}
            yield return null;
        }
        [UnityTest] public IEnumerator DisablingVfxClearsPulsesAndMissesNeverEmitAnImpact()
        {
            Place(new Vector3(128,128,128));Component vfx=Equip();
            Vector3 start=new Vector3(128,130,128),end=new Vector3(128,130,140);
            Call(vfx,"Fire",start,start+Vector3.forward*.4f,end,Player.transform,false);
            Assert.AreEqual(end,Get(vfx,"LastVisualEnd"));
            Assert.IsFalse(vfx.GetComponentsInChildren<Light>().Any(l=>l.name=="ImpactLight" && l.enabled));
            ((Behaviour)vfx).enabled=false;Assert.AreEqual(0,Get(vfx,"ActiveCount"));
            ((Behaviour)vfx).enabled=true;Call(vfx,"Fire",start,start+Vector3.forward*.4f,end,Player.transform,true);
            Assert.AreEqual(1,Get(vfx,"ActiveCount"));yield return null;
        }
        [UnityTest] public IEnumerator SceneReloadRemovesActiveShotPoolAndItsOwnedMaterial()
        {
            Place(new Vector3(128,128,128));Component vfx=Equip();
            Call(vfx,"Fire",new Vector3(128,130,128),new Vector3(128,130,128.4f),
                new Vector3(128,130,140),Player.transform,true);
            Material owned=(Material)Get(vfx,"material");Assert.IsNotNull(owned);
            Assert.AreEqual(1,Get(vfx,"ActiveCount"));
            yield return scope.Close();scope=null;
            scope=new C3V11ThirdPersonInteractionTests();yield return scope.Open();
            Assert.IsTrue(vfx==null);Assert.IsTrue(owned==null);
            Assert.AreEqual(0,Object.FindObjectsOfType<Transform>().Count(t=>t.name.StartsWith("V12_QuantumPulse_")));
        }
    }
}
