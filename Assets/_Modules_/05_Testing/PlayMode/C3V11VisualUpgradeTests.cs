using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace M6IntegrationTests
{
    // New art checks complement, never replace, the unchanged gameplay suites.
    public sealed class C3V11VisualUpgradeTests
    {
        private C3V11Phase2RecyclingLineTests scope;
        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const string ScenePath = "Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
        private readonly List<Object> temporary = new List<Object>();
        private static Type Runtime(string name) => Type.GetType("C3TreasurePuzzle."+name+", Assembly-CSharp",true);
        private static Component Find(string name) => Object.FindObjectOfType(Runtime(name)) as Component;
        private static object Get(object owner,string name) => C3V11Phase2RecyclingLineTests.Member(owner,name);
        private static object Call(object owner,string name,params object[] args) => C3V11Phase2RecyclingLineTests.Call(owner,name,args);
        private static void Set(object owner,string name,object value)
        {
            FieldInfo field=owner.GetType().GetField(name,Any);
            Assert.IsNotNull(field,owner.GetType().FullName+"."+name);field.SetValue(owner,value);
        }
        private T Own<T>(T value) where T:Object {temporary.Add(value);return value;}
        private static Component Game => Type.GetType("EcoAdventureRuntime, Assembly-CSharp",true).GetProperty("Instance").GetValue(null) as Component;
        private static GameObject Player => GameObject.Find("M2_Player");
        private static Component Rig => Player.GetComponents<MonoBehaviour>().Single(c=>c && c.GetType().Name=="PlayerPerspectiveRig");
        private static void View(int value)
        {
            PropertyInfo mode=Rig.GetType().Assembly.GetType("SDGAdventure.SharedPlayer.PlayerViewPreference",true).GetProperty("Mode");
            mode.SetValue(null,Enum.ToObject(mode.PropertyType,value));Call(Rig,"ApplyMode");
        }
        private static void Gameplay()
        {
            Type type=Type.GetType("SDGAdventure.SharedPlayer.PlayerInputService, SDGAdventure.SharedPlayer",true);
            object service=type.GetProperty("Instance").GetValue(null);Assert.IsNotNull(service);
            Call(service,"SetContext",Enum.Parse(Get(service,"Context").GetType(),"Gameplay"));
            Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;
        }
        private static Component InputService => Type.GetType("SDGAdventure.SharedPlayer.PlayerInputService, SDGAdventure.SharedPlayer",true)
            .GetProperty("Instance").GetValue(null) as Component;
        private static Renderer[] Overlays(GameObject owner) => owner.GetComponentsInChildren<Renderer>(true)
            .Where(r=>r.name.StartsWith("V11_HintOverlay_",StringComparison.Ordinal)).ToArray();
        private static string Inventory() => string.Join("|",((IEnumerable)Get(Game,"inventory")).Cast<object>().Select(item=>JsonUtility.ToJson(item)));
        private static int Quantity(string id) => ((IEnumerable)Get(Game,"inventory")).Cast<object>()
            .Where(item=>(string)Get(item,"id")==id).Sum(item=>(int)Get(item,"quantity"));

        // Move only the fixture-owned player/camera. The query itself uses native
        // scene physics and the production interactor; no synthetic hit is returned.
        private static void Aim(Collider target,float frontDistance=1.5f)
        {
            View(0);
            Player.GetComponent<CharacterController>().enabled=false;
            Vector3 eye=target.bounds.center+Vector3.back*frontDistance+Vector3.up*.25f;
            Player.transform.SetPositionAndRotation(eye-Vector3.up*1.5f,Quaternion.identity);
            Camera camera=Player.GetComponentInChildren<Camera>();
            camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target.bounds.center-eye));
            Physics.SyncTransforms();
        }
        private static void RequireFirstTarget(Component expected,float range,Type interactionType)
        {
            Camera camera=Player.GetComponentInChildren<Camera>();
            RaycastHit[] hits=Physics.RaycastAll(camera.transform.position,camera.transform.forward,range,~0,QueryTriggerInteraction.Collide)
                .Where(hit=>!hit.collider.transform.IsChildOf(Player.transform)).OrderBy(hit=>hit.distance).ToArray();
            Assert.IsNotEmpty(hits,"No native ray hit for "+expected.name);
            Assert.LessOrEqual(hits[0].distance,range);
            Assert.AreSame(expected,hits[0].collider.GetComponentInParent(interactionType),
                "The first real hit must belong to the gameplay target, not decorative geometry: "+hits[0].collider.name);
        }
        private static void QuestAction(Component target)
        {
            Aim(target.GetComponent<Collider>(),1f);Gameplay();
            RequireFirstTarget(target,3f,Runtime("C3Interactable"));
            Component interactor=Player.GetComponent(Runtime("C3PlayerInteractor"));
            Assert.AreSame(target,Call(interactor,"FindTarget"));
            bool enabled=((Behaviour)interactor).enabled;
            try
            {
                Call(interactor,"UseSharedInput",InputService);
                ((Behaviour)interactor).enabled=true;
                // Same callback/Update path as the input action, without pretending
                // that an OS F key or a physical mouse was generated by this test.
                Call(interactor,"QueueInteract");Call(interactor,"Update");
            }
            finally {((Behaviour)interactor).enabled=enabled;}
        }
        private static void WorkshopAction(GameObject station)
        {
            Component target=station.GetComponent(Type.GetType("EcoWorkshopInteractable, Assembly-CSharp",true));
            Assert.IsNotNull(target);Aim(station.GetComponent<Collider>());Gameplay();
            RequireFirstTarget(target,3.5f,target.GetType());
            Assert.IsTrue((bool)Call(Game,"GameplayEnabled"));
            // Workshop F uses EcoAdventureRuntime's own ray dispatcher, not
            // C3PlayerInteractor. Invoke that exact route, never target.Interact().
            Call(Game,"BindCamera");Call(Game,"InteractWorkshop");
        }
        private static void Parts()
        {
            Component progress=Find("C3TreasureProgress");
            foreach(string id in new[]{"fuse","control_chip","filter_module"})
                Assert.IsTrue((bool)Call(progress,"TryCollect",id,id));
        }
        private static void Running()
        {
            Parts();Type step=Runtime("C3ProcessingStep");Array answer=Array.CreateInstance(step,3);
            string[] names={"Sorting","Recycling","PollutionTreatment"};
            for(int i=0;i<3;i++)answer.SetValue(Enum.Parse(step,names[i]),i);
            Assert.IsTrue((bool)Call(Find("C3PuzzleState"),"SubmitAnswer",answer));
            Component device=Find("C3DeviceState");Assert.IsTrue((bool)Call(device,"TryActivate"));
            Call(device,"NotifyStartupFinished");Assert.AreEqual("Running",Get(device,"FactoryState").ToString());
            Assert.IsTrue((bool)Get(Game,"factoryPowered"));
        }
        [UnitySetUp] public IEnumerator Open()
        {
            temporary.Clear();
            scope=new C3V11Phase2RecyclingLineTests();yield return scope.Open();yield return null;
        }
        [UnityTearDown] public IEnumerator Close()
        {
            // Destroy temporary owners before their borrowed materials, then let
            // the accepted fixture restore inventory, prefs, input, cursor and time.
            foreach(Object item in temporary)if(item && item is GameObject go){go.SetActive(false);Object.Destroy(go);}
            yield return null;
            foreach(Object item in temporary)if(item && !(item is GameObject))Object.Destroy(item);
            temporary.Clear();yield return null;
            if(scope!=null)yield return scope.Close();
        }

        [UnityTest] public IEnumerator WorkerIsVolumetricAndOnlyItsPresentationChangesWithViewMode()
        {
            GameObject player=GameObject.Find("M2_Player");
            Transform worker=player.transform.Find("V11_RecyclingEngineer_Visual");
            Assert.IsNotNull(worker);
            Assert.Greater(worker.GetComponentsInChildren<MeshFilter>().Length,30);
            Assert.IsEmpty(worker.GetComponentsInChildren<Collider>(true));
            Assert.AreEqual(1,player.GetComponents<CharacterController>().Length);
            Assert.AreEqual(1,player.GetComponents<MonoBehaviour>().Count(c=>c && c.GetType().Name=="C3FirstPersonController"));
            Component rig=player.GetComponents<MonoBehaviour>().Single(c=>c && c.GetType().Name=="PlayerPerspectiveRig");
            Type preference=rig.GetType().Assembly.GetType("SDGAdventure.SharedPlayer.PlayerViewPreference",true);
            PropertyInfo mode=preference.GetProperty("Mode");object previous=mode.GetValue(null);
            Vector3 position=player.transform.position;
            try
            {
                for(int cycle=0;cycle<3;cycle++)
                {
                mode.SetValue(null,Enum.ToObject(mode.PropertyType,1));rig.GetType().GetMethod("ApplyMode").Invoke(rig,null);
                Assert.IsTrue(worker.GetComponentsInChildren<Renderer>().All(r=>r.enabled && r.shadowCastingMode!=ShadowCastingMode.ShadowsOnly));
                Assert.IsTrue(player.transform.Find("fk").GetComponentsInChildren<Renderer>(true).All(r=>!r.enabled),
                    "The legacy white body/shadow proxy must never reappear as the third-person character.");
                mode.SetValue(null,Enum.ToObject(mode.PropertyType,0));rig.GetType().GetMethod("ApplyMode").Invoke(rig,null);
                Assert.IsTrue(worker.GetComponentsInChildren<Renderer>().All(r=>r.shadowCastingMode==ShadowCastingMode.ShadowsOnly));
                Assert.AreEqual(position,player.transform.position,"Changing visuals must not move the capsule.");
                }
                Behaviour appearance=(Behaviour)player.GetComponent(Runtime("C3WorkerAppearance"));
                appearance.enabled=false;Assert.IsFalse(worker.gameObject.activeInHierarchy);
                yield return null;
                appearance.enabled=true;yield return null;
                Assert.AreEqual(1,player.GetComponentsInChildren<Transform>(true).Count(t=>t.name=="V11_RecyclingEngineer_Visual"));
                Assert.IsTrue(player.transform.Find("fk").GetComponentsInChildren<Renderer>(true).All(r=>!r.enabled));
            }
            finally {mode.SetValue(null,previous);rig.GetType().GetMethod("ApplyMode").Invoke(rig,null);}
            yield return null;
        }
        [UnityTest] public IEnumerator FourOriginalStationsKeepTheirGameplayCollidersAndDoNotCreateCargo()
        {
            Component art=Find("C3WorkstationVisualUpgrade");Assert.IsNotNull(art);
            Assert.AreEqual(4,art.GetType().GetProperty("StationCount").GetValue(art));
            foreach(string name in new[]{"M4_PuzzleConsole","Secondary_RecyclingInspectionWorkbench_F","Hidden_WeaponAssemblyBench_F","UpperChipMaintenanceBench"})
            {
                GameObject station=GameObject.Find(name);Assert.IsNotNull(station,name);
                Collider collider=station.GetComponent<Collider>();Assert.IsNotNull(collider,name);
                Assert.IsTrue(collider.enabled && !collider.isTrigger,name);
                Transform visual=station.transform.Find("V11_StationArt_"+name);Assert.IsNotNull(visual,name);
                Assert.Greater(visual.GetComponentsInChildren<MeshFilter>().Length,20,name);
                Assert.IsFalse(visual.GetComponentsInChildren<Collider>(true).Any(c=>c.enabled),name+" art blocks interaction");
                Assert.AreEqual(0,visual.GetComponentsInChildren(Type.GetType("EcoRecycleCargo, Assembly-CSharp",true),true).Length);
            }
            Component line=Find("C3RecyclingLine");
            Assert.AreEqual(0,line.GetType().GetProperty("AcceptedUnits").GetValue(line),"Art must not supply a tenth input or credit materials.");
            foreach(string name in new[]{"Hidden_ElectronicScrapCache_F","Distributed_RecycledPlastic_F","Distributed_RecycledMetal_F"})
            {
                GameObject cache=GameObject.Find(name);
                int sourceCount=cache.GetComponentsInChildren<Renderer>(true).Count(r=>!r.name.StartsWith("V11_HintOverlay_",StringComparison.Ordinal));
                Assert.AreEqual(sourceCount,Overlays(cache).Length,"Hint capture ran before visual cache geometry was ready: "+name);
            }
            yield return null;
        }

        [UnityTest] public IEnumerator WorkerRemovalRestoresThePerspectiveBaselineAndSupportsReinstall()
        {
            Component appearance=Player.GetComponent(Runtime("C3WorkerAppearance"));
            Renderer[] legacy=(Renderer[])Get(appearance,"legacyRenderers");
            bool[] enabledValues=(bool[])Get(appearance,"legacyEnabled");
            ShadowCastingMode[] casting=(ShadowCastingMode[])Get(appearance,"legacyCasting");
            Assert.IsNotEmpty(legacy);Assert.IsTrue(enabledValues.Any(value=>value));
            Transform visual=Player.transform.Find("V11_RecyclingEngineer_Visual");
            Material[] owned=visual.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Distinct().ToArray();
            View(1);Object.Destroy(appearance);yield return null;yield return null;
            Assert.IsTrue(visual==null,"Standalone component removal left its geometry behind.");
            Assert.IsTrue(owned.All(m=>!m),"Worker palette was not released.");
            Vector3 position=Player.transform.position;
            for(int cycle=0;cycle<3;cycle++)for(int mode=0;mode<2;mode++)
            {
                View(mode);
                for(int i=0;i<legacy.Length;i++)
                {
                    Assert.AreEqual(enabledValues[i],legacy[i].enabled,"Stale perspective enabled snapshot.");
                    ShadowCastingMode expected=mode==0?ShadowCastingMode.ShadowsOnly:
                        casting[i]==ShadowCastingMode.ShadowsOnly?ShadowCastingMode.On:casting[i];
                    Assert.AreEqual(expected,legacy[i].shadowCastingMode,"Stale perspective shadow snapshot.");
                }
                Assert.AreEqual(position,Player.transform.position);
            }
            Player.AddComponent(Runtime("C3WorkerAppearance"));yield return null;
            Assert.AreEqual(1,Player.GetComponentsInChildren<Transform>(true).Count(t=>t.name=="V11_RecyclingEngineer_Visual"));
            Assert.IsTrue(legacy.All(r=>!r.enabled));
            View(0);Assert.IsTrue(Player.transform.Find("V11_RecyclingEngineer_Visual").GetComponentsInChildren<Renderer>()
                .All(r=>r.enabled && r.shadowCastingMode==ShadowCastingMode.ShadowsOnly));
            Renderer[] cached=(Renderer[])Get(Rig,"bodyRenderers");
            Assert.IsTrue(cached.All(r=>r),"The rig kept destroyed worker renderers after replacement.");
        }

        [UnityTest] public IEnumerator WorkerFacesActualControllerTravelWithoutMovingTheCapsuleItself()
        {
            CharacterController motor=Player.GetComponent<CharacterController>();motor.enabled=false;
            Player.transform.SetPositionAndRotation(new Vector3(128,128,128),Quaternion.identity);motor.enabled=true;
            Component appearance=Player.GetComponent(Runtime("C3WorkerAppearance"));
            Transform visual=Player.transform.Find("V11_RecyclingEngineer_Visual");
            float deadline=Time.realtimeSinceStartup+2f;
            while(Vector3.Dot(visual.forward,Vector3.right)<.9f && Time.realtimeSinceStartup<deadline)
            {
                motor.Move(Vector3.right*.05f);Vector3 position=Player.transform.position;
                Assert.Greater(Vector3.Dot(motor.velocity,Vector3.right),0f);
                Call(appearance,"LateUpdate");Assert.AreEqual(position,Player.transform.position,"Visual animation moved the capsule.");
                yield return null;
            }
            Assert.Greater(Vector3.Dot(visual.forward,Vector3.right),.9f,"Worker must face real travel rather than the old FBX axis.");
        }

        [UnityTest] public IEnumerator WorkerRemovedWhilePlayerIsInactiveRestoresWhenThePlayerReturns()
        {
            GameObject player=Player;Component appearance=player.GetComponent(Runtime("C3WorkerAppearance"));
            Renderer[] legacy=(Renderer[])Get(appearance,"legacyRenderers");
            bool[] enabledValues=(bool[])Get(appearance,"legacyEnabled");
            player.SetActive(false);Object.Destroy(appearance);yield return null;yield return null;
            Assert.IsTrue(legacy.All(r=>!r.enabled),"Inactive/unloading renderers must not be restored early.");
            player.SetActive(true);yield return null;yield return null;
            Assert.IsNull(player.transform.Find("V11_RecyclingEngineer_Visual"));
            for(int mode=0;mode<2;mode++)
            {
                View(mode);
                for(int i=0;i<legacy.Length;i++)Assert.AreEqual(enabledValues[i],legacy[i].enabled);
            }
        }

        [UnityTest] public IEnumerator ConsoleNativeRayOpensTheOriginalPuzzleAndActivationFlow()
        {
            Parts();Component console=Find("C3PuzzleConsole");
            Assert.AreEqual("M4_PuzzleConsole",console.name);
            QuestAction(console);Assert.IsTrue((bool)Get(console,"PanelOpen"));
            yield return null;
            Component ui=Find("C3UiView");Assert.IsNotNull(ui);
            Canvas canvas=Object.FindObjectsOfType<Canvas>().Single(c=>c.name=="C3UiCanvas");
            Assert.IsTrue(canvas.gameObject.activeInHierarchy);
            Assert.IsTrue(((GameObject)Get(ui,"puzzlePanel")).activeInHierarchy,"The real puzzle UI did not open.");
            Type step=Runtime("C3ProcessingStep");
            foreach(string name in new[]{"Sorting","Recycling","PollutionTreatment"})
                Assert.IsTrue((bool)Call(console,"SelectStep",Enum.Parse(step,name)));
            Assert.IsTrue((bool)Call(console,"Confirm"));
            Assert.IsTrue((bool)Get(Find("C3PuzzleState"),"PuzzleSolved"));
            Assert.IsFalse((bool)Get(Find("C3DeviceState"),"IsActivated"),"Solving alone must not activate the factory.");
            Assert.IsTrue((bool)Call(console,"ConfirmActivation"));
            Assert.IsFalse((bool)Get(console,"PanelOpen"));
            Assert.AreEqual("Starting",Get(Find("C3DeviceState"),"FactoryState").ToString());
            Call(Find("C3DeviceState"),"NotifyStartupFinished");
            Assert.AreEqual("Running",Get(Find("C3DeviceState"),"FactoryState").ToString());
            Assert.IsTrue((bool)Get(Game,"factoryPowered"));
            yield return null;
        }

        [UnityTest] public IEnumerator InspectionNativeRayShowsTheOriginalReportWithoutGrantingMaterials()
        {
            GameObject station=GameObject.Find("Secondary_RecyclingInspectionWorkbench_F");
            string before=Inventory();WorkshopAction(station);
            StringAssert.Contains("尚未供電",(string)Get(Game,"message"));Assert.AreEqual(before,Inventory());
            Running();yield return null;
            before=Inventory();WorkshopAction(station);
            StringAssert.Contains("材料檢測結果",(string)Get(Game,"message"));
            StringAssert.Contains("再生金屬",(string)Get(Game,"message"));
            StringAssert.Contains("再生塑膠",(string)Get(Game,"message"));
            StringAssert.Contains("廢電路板",(string)Get(Game,"message"));
            Assert.AreEqual(before,Inventory(),"The inspection station must only report existing state.");
            Assert.AreEqual(0,Quantity("eco_quantum_projector"));
        }

        [UnityTest] public IEnumerator AssemblyNativeRayRequiresMaterialsConsumesExactlyOneAndCannotRepeat()
        {
            Running();yield return null;
            GameObject station=GameObject.Find("Hidden_WeaponAssemblyBench_F");
            string before=Inventory();WorkshopAction(station);
            Assert.IsFalse((bool)Get(Game,"weaponCrafted"));Assert.AreEqual(0,Quantity("eco_quantum_projector"));
            Assert.AreEqual(before,Inventory());StringAssert.Contains("缺少材料",(string)Get(Game,"message"));
            // Arrange completed upstream batches using the real receipt transaction.
            // Full conveyor motion/counts remain covered by the unchanged Phase 2 suite.
            Type kind=Type.GetType("EcoRecycleCargo+Kind, Assembly-CSharp",true);
            foreach(string material in new[]{"Plastic","Metal"})
                Assert.IsTrue((bool)Call(Game,"TryCommitRecyclingBatch","visual-"+material,Enum.Parse(kind,material),
                    new[]{"visual-"+material+"-0","visual-"+material+"-1","visual-"+material+"-2"}));
            foreach(string name in new[]{"Distributed_RecycledPlastic_F","Distributed_RecycledMetal_F","Hidden_ElectronicScrapCache_F"})
            {
                GameObject cache=GameObject.Find(name);WorkshopAction(cache);Assert.IsFalse(cache.activeSelf,"Native pickup did not claim "+name);
            }
            string[] ids={"eco_recycled_plastic","eco_recycled_metal","eco_recovered_circuit"};
            foreach(string id in ids)
            {
                Assert.AreEqual(1,Quantity(id));
                // A second fixture-owned unit distinguishes exactly-one consumption
                // from incorrectly removing every stack. This is not a station grant.
                Call(Game,"AddItem",id,id,1);Assert.AreEqual(2,Quantity(id));
            }
            WorkshopAction(station);Assert.IsTrue((bool)Get(Game,"weaponCrafted"));
            Assert.AreEqual(1,Quantity("eco_quantum_projector"));
            foreach(string id in ids)Assert.AreEqual(1,Quantity(id),"Crafting must consume exactly one "+id);
            before=Inventory();WorkshopAction(station);Assert.AreEqual(before,Inventory(),"Repeated F dispatched a duplicate craft/reward.");
            Assert.AreEqual(6,Get(Game,"RecyclingReceiptCount"),"Visual interactions created extra processing receipts.");
        }

        [UnityTest] public IEnumerator MaintenanceKeepsItsExistingBenchAndNearbyChipWithoutInventingAnFTask()
        {
            GameObject bench=GameObject.Find("UpperChipMaintenanceBench");Assert.IsNotNull(bench);
            Assert.IsNull(bench.GetComponent(Runtime("C3Interactable")),"Maintenance is not an independent quest interaction.");
            Assert.IsNull(bench.GetComponent(Type.GetType("EcoWorkshopInteractable, Assembly-CSharp",true)));
            Component chip=Object.FindObjectsOfType(Runtime("C3CollectiblePart")).Cast<Component>()
                .Single(part=>(string)Get(part,"PartId")=="control_chip");
            Assert.Less(Vector3.Distance(chip.transform.position,bench.transform.position),3f,"The original nearby chip was moved away.");
            QuestAction(chip);Assert.IsFalse(chip.gameObject.activeSelf);
            Assert.IsTrue((bool)Call(Find("C3TreasureProgress"),"HasPart","control_chip"));
            Assert.AreEqual(1,Quantity("control_chip"));yield return null;
        }
        [UnityTest] public IEnumerator AllThreeLabelsFaceThePlayerOutsideTheirMachineFronts()
        {
            string[] names={"PaperHall","MetalHall","PlasticHall"};
            for(int i=0;i<3;i++)
            {
                Transform machine=GameObject.Find("SORTER_MAIN_BODY_"+i).transform;
                Transform label=machine.Find("C3_IndustrialSign_"+names[i]);Assert.IsNotNull(label,names[i]);
                Transform face=label.Find("ReadableFront");Assert.IsNotNull(face);
                Renderer image=face.GetComponent<Renderer>();Assert.IsTrue(image.enabled && image.gameObject.activeInHierarchy);
                Assert.IsNotNull(image.sharedMaterial.mainTexture);
                Assert.AreSame(Resources.Load<Texture2D>("C3Signage/"+names[i]),image.sharedMaterial.mainTexture);
                Assert.AreEqual("SDGAdventure/C3BakedSign",image.sharedMaterial.shader.name);
                Vector3 normal=face.TransformDirection(face.GetComponent<MeshFilter>().sharedMesh.normals[0]);
                Assert.Less(normal.z,-.9f,"Labels must face the -Z approach, not the closed cabinet.");
                Assert.Less(image.bounds.max.z,machine.Find("FRONT_TOP").GetComponent<Renderer>().bounds.min.z,
                    "A valid texture embedded in the opaque front panel is still invisible.");
                Assert.Greater(label.lossyScale.x,0f);Assert.Greater(label.lossyScale.y,0f);
                Assert.IsFalse(label.GetComponentsInChildren<Collider>(true).Any(c=>c.enabled));
                Assert.That(Quaternion.Angle(face.localRotation,Quaternion.Euler(0,180,0)),Is.LessThan(.01f));
                Assert.That(Quaternion.Angle(label.rotation,Quaternion.Euler(0,180,0)),Is.LessThan(.01f));
                Mesh mesh=face.GetComponent<MeshFilter>().sharedMesh;
                Assert.Less(mesh.normals[0].z,-.9f,"Observe the actual Unity primitive mesh; do not assume a +Z front.");
                // For a viewer on -Z, increasing U must move right (+X), and
                // increasing V up (+Y). Positive scale alone cannot prove this.
                Vector3[] vertices=mesh.vertices;Vector2[] uv=mesh.uv;
                bool uChecked=false,vChecked=false;
                for(int a=0;a<uv.Length;a++)for(int b=a+1;b<uv.Length;b++)
                {
                    Vector2 d=uv[b]-uv[a];Vector3 world=face.TransformPoint(vertices[b])-face.TransformPoint(vertices[a]);
                    if(Mathf.Abs(d.y)<.001f && Mathf.Abs(d.x)>.5f){Assert.Greater(world.x/d.x,0f,"Mirrored U axis");uChecked=true;}
                    if(Mathf.Abs(d.x)<.001f && Mathf.Abs(d.y)>.5f){Assert.Greater(world.y/d.y,0f,"Inverted V axis");vChecked=true;}
                }
                Assert.IsTrue(uChecked && vChecked,"No usable UV axes on the real sign mesh.");
                var approach=new Vector3(image.bounds.center.x,image.bounds.center.y,image.bounds.center.z-1f);
                Assert.IsFalse(Physics.Raycast(approach,Vector3.forward,out RaycastHit blocker,.99f,~0,QueryTriggerInteraction.Ignore),
                    "Opaque geometry blocks the -Z approach: "+(blocker.collider?blocker.collider.name:"none"));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator DiscoveryCueRejectsAnOpaqueWallAndRecoversAfterItIsRemoved()
        {
            Transform observer=new GameObject("V11_TEST_HINT_OBSERVER").transform;
            Transform target=new GameObject("V11_TEST_HINT_TARGET").transform;
            GameObject wall=null;
            try
            {
                observer.position=new Vector3(128,128,128);
                target.position=observer.position+new Vector3(0,1.4f,3f);
                MethodInfo visible=Runtime("C3ProximityHint").GetMethod("VisibleFrom",BindingFlags.Static|BindingFlags.NonPublic);
                var buffer=new RaycastHit[32];
                Func<bool> query=()=>(bool)visible.Invoke(null,new object[]{observer,target,target.position,buffer});
                Physics.SyncTransforms();Assert.IsTrue(query(),"Open approach must be visible.");
                wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position=observer.position+new Vector3(0,1.4f,1.5f);wall.transform.localScale=new Vector3(2,2,.2f);
                Physics.SyncTransforms();Assert.IsFalse(query(),"Nearby items must not glow through an opaque obstruction.");
                Object.Destroy(wall);yield return null;
                Physics.SyncTransforms();Assert.IsTrue(query(),"Removing the obstruction must restore the cue.");
            }
            finally {if(wall)Object.Destroy(wall);Object.Destroy(observer.gameObject);Object.Destroy(target.gameObject);}
        }

        [UnityTest] public IEnumerator HintLifecyclePreservesGlobalIndexedAndConcurrentRendererState()
        {
            GameObject target=Own(GameObject.CreatePrimitive(PrimitiveType.Cube));target.name="V11_TEST_HINT_SURFACE";
            target.transform.position=new Vector3(128,128,128);
            Renderer source=target.GetComponent<Renderer>();
            Material first=Own(new Material(Shader.Find("Standard")) {name="V11_TEST_SOURCE_A"});
            Material second=Own(new Material(Shader.Find("Standard")) {name="V11_TEST_SOURCE_B"});
            source.sharedMaterials=new[]{first,second};
            var global=new MaterialPropertyBlock();global.SetColor("_EmissionColor",new Color(.13f,.23f,.37f,1));global.SetFloat("_ExternalValue",17f);
            source.SetPropertyBlock(global);
            var indexed=new MaterialPropertyBlock();indexed.SetColor("_EmissionColor",new Color(.31f,.47f,.59f,1));indexed.SetFloat("_ExternalValue",23f);
            source.SetPropertyBlock(indexed,1);
            Light light=target.AddComponent<Light>();light.enabled=true;light.intensity=.83f;
            Behaviour hint=(Behaviour)target.AddComponent(Runtime("C3ProximityHint"));
            Call(hint,"Configure",Color.green,light);
            GameObject observer=Own(new GameObject("V11_TEST_HINT_OBSERVER"));observer.transform.position=target.transform.position+Vector3.back*1.5f;
            Set(hint,"player",observer.transform);yield return null;yield return null;
            Renderer[] shells=Overlays(target);Assert.IsNotEmpty(shells);Assert.IsTrue(shells.All(r=>r.enabled));
            Material owned=shells[0].sharedMaterial;
            var actual=new MaterialPropertyBlock();source.GetPropertyBlock(actual);
            Assert.AreEqual(global.GetColor("_EmissionColor"),actual.GetColor("_EmissionColor"));Assert.AreEqual(17f,actual.GetFloat("_ExternalValue"));
            source.GetPropertyBlock(actual,1);Assert.AreEqual(indexed.GetColor("_EmissionColor"),actual.GetColor("_EmissionColor"));
            source.GetPropertyBlock(actual,0);Assert.IsTrue(actual.isEmpty,"An originally empty per-material block was overwritten.");
            CollectionAssert.AreEqual(new[]{first,second},source.sharedMaterials);

            // A separate system writes BOTH cue-related and unrelated properties
            // while the hint is active. Disable/removal must not restore stale data.
            global.SetColor("_EmissionColor",Color.magenta);global.SetFloat("_ExternalValue",41f);source.SetPropertyBlock(global);
            indexed.SetColor("_EmissionColor",Color.cyan);indexed.SetFloat("_ExternalValue",43f);source.SetPropertyBlock(indexed,1);
            hint.enabled=false;Assert.IsTrue(shells.All(r=>!r.enabled));
            Assert.IsTrue(light.enabled);Assert.AreEqual(.83f,light.intensity,"Capture must precede Configure's first write.");
            observer.transform.position=target.transform.position+Vector3.back*8f;hint.enabled=true;yield return null;
            Assert.IsTrue(shells.All(r=>!r.enabled),"Far re-enable reused the previous near cue.");
            observer.transform.position=target.transform.position+Vector3.back*1.5f;
            GameObject wall=Own(GameObject.CreatePrimitive(PrimitiveType.Cube));
            wall.transform.position=Vector3.Lerp(observer.transform.position+Vector3.up*1.4f,target.transform.position+Vector3.up*.25f,.5f);
            wall.transform.localScale=new Vector3(2,2,.2f);Physics.SyncTransforms();yield return null;
            Assert.IsTrue(shells.All(r=>!r.enabled),"Re-enable must re-evaluate obstruction.");
            wall.SetActive(false);Physics.SyncTransforms();yield return null;
            Assert.IsTrue(shells.All(r=>r.enabled));
            Object.Destroy(hint);yield return null;yield return null;
            Assert.IsEmpty(Overlays(target));Assert.IsTrue(owned==null,"Owned overlay material leaked.");
            CollectionAssert.AreEqual(new[]{first,second},source.sharedMaterials);
            Assert.IsTrue(light.enabled);Assert.AreEqual(.83f,light.intensity);
            source.GetPropertyBlock(actual);Assert.AreEqual(Color.magenta,actual.GetColor("_EmissionColor"));Assert.AreEqual(41f,actual.GetFloat("_ExternalValue"));
            source.GetPropertyBlock(actual,1);Assert.AreEqual(Color.cyan,actual.GetColor("_EmissionColor"));Assert.AreEqual(43f,actual.GetFloat("_ExternalValue"));
            source.GetPropertyBlock(actual,0);Assert.IsTrue(actual.isEmpty);
        }

        [UnityTest] public IEnumerator HintDoesNotOverwriteAnotherLightWriterOnDisableOrRemoval()
        {
            GameObject target=Own(GameObject.CreatePrimitive(PrimitiveType.Cube));target.transform.position=new Vector3(128,128,128);
            Light light=target.AddComponent<Light>();light.enabled=true;light.intensity=.71f;
            Behaviour hint=(Behaviour)target.AddComponent(Runtime("C3ProximityHint"));Call(hint,"Configure",Color.green,light);
            GameObject observer=Own(new GameObject("V11_TEST_LIGHT_OBSERVER"));observer.transform.position=target.transform.position+Vector3.back;
            Set(hint,"player",observer.transform);yield return null;yield return null;
            Assert.IsTrue(light.enabled); // first demonstrate the hint actually wrote it
            light.enabled=false;light.intensity=2.71f;yield return null;
            Assert.IsFalse(light.enabled);Assert.AreEqual(2.71f,light.intensity);
            hint.enabled=false;Assert.IsFalse(light.enabled);Assert.AreEqual(2.71f,light.intensity);
            Object.Destroy(hint);yield return null;yield return null;
            Assert.IsFalse(light.enabled);Assert.AreEqual(2.71f,light.intensity);
        }

        [UnityTest] public IEnumerator CollectibleDisableReenableAndPickupClearTheCueWithoutRepeatingRewards()
        {
            Component part=Object.FindObjectsOfType(Runtime("C3CollectiblePart")).Cast<Component>().Single(p=>(string)Get(p,"PartId")=="fuse");
            part.transform.position=new Vector3(128,128,128);Player.GetComponent<CharacterController>().enabled=false;
            Player.transform.position=part.transform.position+Vector3.back;
            var renderer=part.GetComponentsInChildren<Renderer>(true).First(r=>!r.name.StartsWith("V11_HintOverlay_",StringComparison.Ordinal));
            Material[] materials=renderer.sharedMaterials;
            var block=new MaterialPropertyBlock();block.SetColor("_EmissionColor",Color.blue);block.SetFloat("_OtherSystem",73f);renderer.SetPropertyBlock(block);
            Set(part,"player",Player.transform);Physics.SyncTransforms();yield return null;
            Assert.IsTrue((bool)Get(part,"IsGlowing"));Renderer[] shells=Overlays(part.gameObject);Assert.IsTrue(shells.Any(r=>r.enabled));
            part.gameObject.SetActive(false);Assert.IsFalse((bool)Get(part,"IsGlowing"));Assert.IsTrue(shells.All(r=>!r.enabled));
            Player.transform.position=part.transform.position+Vector3.back*8f;part.gameObject.SetActive(true);yield return null;
            Assert.IsFalse((bool)Get(part,"IsGlowing"));Assert.IsTrue(shells.All(r=>!r.enabled));
            Player.transform.position=part.transform.position+Vector3.back;
            GameObject wall=Own(GameObject.CreatePrimitive(PrimitiveType.Cube));
            wall.transform.position=Vector3.Lerp(Player.transform.position+Vector3.up*1.4f,part.GetComponent<Collider>().bounds.center,.5f);
            wall.transform.localScale=new Vector3(2,2,.2f);Physics.SyncTransforms();yield return null;
            Assert.IsFalse((bool)Get(part,"IsGlowing"));wall.SetActive(false);Physics.SyncTransforms();yield return null;
            Assert.IsTrue((bool)Get(part,"IsGlowing"));QuestAction(part);
            Assert.IsFalse(part.gameObject.activeSelf);Assert.IsFalse((bool)Get(part,"IsGlowing"));Assert.IsTrue(shells.All(r=>!r.enabled));
            Assert.AreEqual(1,Quantity("fuse"));Call(part,"Interact",Player);Assert.AreEqual(1,Quantity("fuse"));
            Assert.AreEqual(1,Get(Find("C3TreasureProgress"),"CollectedCount"));
            var actual=new MaterialPropertyBlock();renderer.GetPropertyBlock(actual);
            Assert.AreEqual(Color.blue,actual.GetColor("_EmissionColor"));Assert.AreEqual(73f,actual.GetFloat("_OtherSystem"));
            CollectionAssert.AreEqual(materials,renderer.sharedMaterials);
        }

        [UnityTest] public IEnumerator CollectibleStandaloneRemovalLeavesItsOriginalSurfaceAndNoOwnedBeacon()
        {
            Component part=Object.FindObjectsOfType(Runtime("C3CollectiblePart")).Cast<Component>().First();
            GameObject owner=part.gameObject;part.transform.position=new Vector3(128,128,128);
            Player.GetComponent<CharacterController>().enabled=false;Player.transform.position=part.transform.position+Vector3.back;
            Set(part,"player",Player.transform);Physics.SyncTransforms();yield return null;
            Assert.IsTrue((bool)Get(part,"IsGlowing"));
            Renderer source=part.GetComponentsInChildren<Renderer>(true).First(r=>!r.name.StartsWith("V11_HintOverlay_",StringComparison.Ordinal));
            Material[] originals=source.sharedMaterials;var before=new MaterialPropertyBlock();source.GetPropertyBlock(before);
            Material[] owned=Overlays(owner).Select(r=>r.sharedMaterial).Distinct().ToArray();
            Object.Destroy(part);yield return null;yield return null;
            Assert.IsNotNull(owner);Assert.IsNotNull(source);CollectionAssert.AreEqual(originals,source.sharedMaterials);
            var actual=new MaterialPropertyBlock();source.GetPropertyBlock(actual);Assert.AreEqual(before.isEmpty,actual.isEmpty);
            Assert.IsEmpty(Overlays(owner));Assert.IsNull(owner.transform.Find("NearbyPartGlow_DoNotCollect"));
            Assert.IsTrue(owned.All(m=>!m));Assert.AreEqual(0,Quantity("fuse"));Assert.AreEqual(0,Get(Find("C3TreasureProgress"),"CollectedCount"));
        }

        [UnityTest] public IEnumerator RemovingStationVisualsReturnsBorrowedSignsAndOnlyReleasesOwnedMaterials()
        {
            Component appearance=Find("C3WorkstationVisualUpgrade");
            object[] leases=((IEnumerable)Get(appearance,"borrowedSigns")).Cast<object>().ToArray();
            Assert.IsNotEmpty(leases,"No borrowed original nameplates were recorded.");
            Transform[] signs=leases.Select(lease=>(Transform)Get(lease,"Sign")).ToArray();
            Material[] borrowed=signs.SelectMany(t=>t.GetComponentsInChildren<Renderer>(true)).SelectMany(r=>r.sharedMaterials).Distinct().ToArray();
            Material[] owned=((IEnumerable)Get(appearance,"ownedSignMaterials")).Cast<Material>().ToArray();
            Assert.IsNotEmpty(owned,"The new maintenance nameplate must have an owner.");
            Transform[] stations=new[]{"M4_PuzzleConsole","Secondary_RecyclingInspectionWorkbench_F","Hidden_WeaponAssemblyBench_F","UpperChipMaintenanceBench"}
                .Select(name=>GameObject.Find(name).transform).ToArray();
            Collider[] colliders=stations.Select(t=>t.GetComponent<Collider>()).ToArray();
            Vector3[] positions=stations.Select(t=>t.position).ToArray();
            Object.Destroy(appearance);yield return null;yield return null;
            for(int i=0;i<leases.Length;i++)
            {
                Assert.IsTrue(signs[i],"Borrowed nameplate was deleted with the replacement visual root.");
                Assert.AreSame(Get(leases[i],"Parent"),signs[i].parent);
                Assert.That(Vector3.Distance((Vector3)Get(leases[i],"LocalPosition"),signs[i].localPosition),Is.LessThan(.0001f));
                Assert.That(Quaternion.Angle((Quaternion)Get(leases[i],"LocalRotation"),signs[i].localRotation),Is.LessThan(.01f));
                Assert.That(Vector3.Distance((Vector3)Get(leases[i],"LocalScale"),signs[i].localScale),Is.LessThan(.0001f));
                Assert.AreEqual(Get(leases[i],"Sibling"),signs[i].GetSiblingIndex());
                Assert.IsTrue(signs[i].GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).All(m=>m));
            }
            Assert.IsTrue(borrowed.All(m=>m),"Borrowed sign materials were released by the wrong owner.");
            Assert.IsTrue(owned.All(m=>!m),"Owned sign materials leaked.");
            for(int i=0;i<stations.Length;i++)
            {
                Assert.AreEqual(positions[i],stations[i].position);Assert.IsTrue(colliders[i] && colliders[i].enabled);
                Assert.IsNull(stations[i].Find("V11_StationArt_"+stations[i].name));
            }
            Assert.IsTrue(stations[0].GetComponent(Runtime("C3PuzzleConsole")));
            Assert.IsTrue(stations[1].GetComponent(Type.GetType("EcoWorkshopInteractable, Assembly-CSharp",true)));
            Assert.IsTrue(stations[2].GetComponent(Type.GetType("EcoWorkshopInteractable, Assembly-CSharp",true)));
        }

        [UnityTest] public IEnumerator SalvageVisualDisableReenableAndRemovalCleanOnlyOwnedGeometry()
        {
            Component appearance=Object.FindObjectsOfType(Runtime("C3SalvageAppearance")).Cast<Component>().First();
            GameObject pickup=appearance.gameObject;Component gameplay=pickup.GetComponent(Runtime("EcoEnergyPickup"));
            Collider collider=pickup.GetComponent<Collider>();Assert.IsNotNull(gameplay);Assert.IsNotNull(collider);
            string kind=Get(gameplay,"Kind").ToString();int value=(int)Get(gameplay,"EnergyValue");
            Assert.AreEqual(kind=="PlasticBottle"?5:kind=="MetalCan"?8:3,value);
            Transform root=pickup.transform.Find("V11_SalvageArt_Visual");Assert.IsNotNull(root);
            Assert.IsNotNull(root.GetComponent(Runtime("C3ProximityHint")));
            Material[] materials=root.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Distinct().ToArray();
            for(int cycle=0;cycle<3;cycle++)
            {
                ((Behaviour)appearance).enabled=false;Assert.IsFalse(root.gameObject.activeInHierarchy);
                ((Behaviour)appearance).enabled=true;yield return null;
                Assert.IsTrue(root.gameObject.activeInHierarchy);
                Assert.AreEqual(1,pickup.GetComponentsInChildren<Transform>(true).Count(t=>t.name=="V11_SalvageArt_Visual"));
            }
            Object.Destroy(appearance);yield return null;yield return null;
            Assert.IsTrue(pickup);Assert.IsTrue(gameplay);Assert.IsTrue(collider.enabled);
            Assert.IsNull(pickup.transform.Find("V11_SalvageArt_Visual"));Assert.IsEmpty(Overlays(pickup));
            Assert.IsTrue(materials.All(m=>!m),"A salvage palette or attached hint material leaked.");
            Assert.AreEqual(kind,Get(gameplay,"Kind").ToString());Assert.AreEqual(value,Get(gameplay,"EnergyValue"));
            Assert.IsFalse((bool)Get(gameplay,"Collected"));
        }

        [UnityTest] public IEnumerator SalvageKeepsWeaponAndFullMagazineGatesAndChargesOnlyOnce()
        {
            foreach(Component pickup in Object.FindObjectsOfType(Runtime("EcoEnergyPickup")))
            {
                // Test the production collection API with fixture-owned inventory;
                // natural proximity/F input remains a separate Windows acceptance.
                ((Behaviour)pickup).enabled=false;
            }
            Component[] representatives=Object.FindObjectsOfType(Runtime("EcoEnergyPickup"),true).Cast<Component>()
                .GroupBy(p=>Get(p,"Kind").ToString()).Select(group=>group.First()).ToArray();
            Assert.AreEqual(3,representatives.Length);
            string weapon="eco_quantum_projector";
            foreach(Component pickup in representatives)
            {
                ((Behaviour)pickup).enabled=true;
                Set(Game,"ammoInMagazine",0);Set(Game,"selectedSlot",4);
                Assert.IsFalse((bool)Call(pickup,"TryCollect"),"Unequipped weapon must not recharge.");Assert.IsTrue(pickup.gameObject.activeSelf);
                if(Quantity(weapon)==0)Call(Game,"AddItem",weapon,weapon,1);
                ((string[])Get(Game,"quickSlots"))[0]=weapon;Set(Game,"selectedSlot",0);
                Set(Game,"ammoInMagazine",24);int reserve=(int)Get(Game,"reserveAmmo");
                Assert.IsFalse((bool)Call(pickup,"TryCollect"));Assert.IsTrue(pickup.gameObject.activeSelf,"Full magazine must preserve salvage.");
                Set(Game,"ammoInMagazine",0);int value=(int)Get(pickup,"EnergyValue");
                string kind=Get(pickup,"Kind").ToString();
                Assert.AreEqual(kind=="PlasticBottle"?5:kind=="MetalCan"?8:3,value);
                Assert.IsTrue((bool)Call(pickup,"TryCollect"));Assert.AreEqual(value,Get(Game,"ammoInMagazine"));
                Assert.AreEqual(reserve,Get(Game,"reserveAmmo"));Assert.IsFalse(pickup.gameObject.activeSelf);
                Assert.IsFalse((bool)Call(pickup,"TryCollect"));Assert.AreEqual(value,Get(Game,"ammoInMagazine"));
            }
            yield return null;
        }

        [UnityTest] public IEnumerator SharedRifleGeometryUsesViewmodelOffAndAssemblyDisplayOnShadows()
        {
            Transform bench=GameObject.Find("Hidden_WeaponAssemblyBench_F").transform;
            Transform display=bench.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="ReclaimedQuantumRifle_Visual");
            Renderer[] tableRenderers=display.GetComponentsInChildren<Renderer>(true);
            Assert.IsNotEmpty(tableRenderers);Assert.IsTrue(tableRenderers.All(r=>r.shadowCastingMode==ShadowCastingMode.On));
            // Test the actual equipped-weapon creation path, not a standalone
            // visual component that production might never install.
            const string weapon="eco_quantum_projector";Call(Game,"AddItem",weapon,weapon,1);
            ((string[])Get(Game,"quickSlots"))[0]=weapon;Set(Game,"selectedSlot",0);View(0);
            Call(Game,"BindCamera");Call(Game,"BuildHeldModel");
            GameObject host=(GameObject)Get(Game,"weaponModel");Assert.IsNotNull(host);
            Component view=host.GetComponent(Type.GetType("EcoQuantumWeaponView, Assembly-CSharp",true));Assert.IsNotNull(view);
            yield return null;
            Transform held=host.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="ReclaimedQuantumRifle_Visual");
            Renderer[] heldRenderers=held.GetComponentsInChildren<Renderer>(true);
            Assert.AreEqual(tableRenderers.Length,heldRenderers.Length,"Both uses must share the same rifle construction.");
            CollectionAssert.AreEqual(tableRenderers.Select(r=>r.name).ToArray(),heldRenderers.Select(r=>r.name).ToArray());
            Assert.IsTrue(heldRenderers.All(r=>r.shadowCastingMode==ShadowCastingMode.Off));
            Assert.IsFalse(held.GetComponentsInChildren<Collider>(true).Any(c=>c.enabled));
            Assert.IsFalse(display.GetComponentsInChildren<Collider>(true).Any(c=>c.enabled));
            Assert.AreEqual(1,Player.GetComponentsInChildren<Camera>(true).Length);
            Material[] owned=heldRenderers.SelectMany(r=>r.sharedMaterials).Distinct().ToArray();
            for(int mode=0;mode<2;mode++)
            {
                View(mode);Call(Game,"LateUpdate");
                Assert.AreSame(mode==0?Player.GetComponentInChildren<Camera>().transform:Player.transform,host.transform.parent);
                Assert.IsTrue(heldRenderers.All(r=>r.shadowCastingMode==ShadowCastingMode.Off));
            }
            View(0);Call(Game,"BuildHeldModel");yield return null;yield return null;
            Assert.IsTrue(host==null && held==null);Assert.IsTrue(owned.All(m=>!m));
            GameObject replacement=(GameObject)Get(Game,"weaponModel");Assert.IsNotNull(replacement);
            Assert.AreEqual(1,replacement.GetComponentsInChildren<Transform>(true).Count(t=>t.name=="ReclaimedQuantumRifle_Visual"));
            Assert.IsTrue(tableRenderers.All(r=>r && r.sharedMaterial),"Viewmodel cleanup released the separate assembly palette.");
        }
        [UnityTest] public IEnumerator ReloadDoesNotDuplicateWorkerOrStationVisualOwners()
        {
            int hintCount=Object.FindObjectsOfType(Runtime("C3ProximityHint")).Length;
            int materialCount=Resources.FindObjectsOfTypeAll<Material>().Count(m=>m && m.name=="V11_OwnedHintOverlay");
            for(int round=0;round<2;round++)
            {
            int handle=SceneManager.GetActiveScene().handle;
            yield return SceneManager.LoadSceneAsync(ScenePath,LoadSceneMode.Single);
            yield return null;
            yield return null;
            Assert.AreNotEqual(handle,SceneManager.GetActiveScene().handle,"Reload must use a new scene instance.");
            Assert.AreEqual(1,Object.FindObjectsOfType(Runtime("C3WorkerAppearance")).Length);
            Assert.AreEqual(1,Object.FindObjectsOfType(Runtime("C3WorkstationVisualUpgrade")).Length);
            Component art=Find("C3WorkstationVisualUpgrade");
            Assert.AreEqual(4,art.GetType().GetProperty("StationCount").GetValue(art));
            Assert.AreEqual(hintCount,Object.FindObjectsOfType(Runtime("C3ProximityHint")).Length);
            Assert.AreEqual(materialCount,Resources.FindObjectsOfTypeAll<Material>().Count(m=>m && m.name=="V11_OwnedHintOverlay"),
                "Reload leaked owned hint materials.");
            Assert.AreEqual(1,Player.GetComponents<CharacterController>().Length);
            Assert.AreEqual(1,Player.GetComponentsInChildren<Camera>(true).Length);
            }
        }
    }
}
