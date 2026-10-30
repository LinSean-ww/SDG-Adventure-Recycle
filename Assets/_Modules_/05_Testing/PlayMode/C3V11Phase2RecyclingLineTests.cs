using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace M6IntegrationTests
{
    // Reuse the accepted fixture's persistent-inventory/input/global isolation.
    // Manual ticks call the SAME production integrator on real scene equipment;
    // native physics queries below are never replaced by calculated constants.
    public sealed class C3V11Phase2RecyclingLineTests
    {
        private const string ScenePath="Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
        private const BindingFlags Any=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private C3V11SafeCirculationTests scope;
        private Component line,geometry,device,runtime;
        internal static Type GameType(string name) => Type.GetType(name+", Assembly-CSharp",true);
        internal static Component Find(string name) => Object.FindObjectOfType(GameType(name)) as Component;
        internal static object Member(object owner,string name)
        {
            Assert.IsNotNull(owner,"Missing owner for "+name);
            Type type=owner.GetType(); PropertyInfo property=type.GetProperty(name,Any);
            if (property!=null) return property.GetValue(owner);
            FieldInfo field=type.GetField(name,Any); Assert.IsNotNull(field,type.FullName+"."+name);
            return field.GetValue(owner);
        }
        private static void Set(object owner,string name,object value)
        {
            FieldInfo field=owner.GetType().GetField(name,Any);
            Assert.IsNotNull(field,owner.GetType().FullName+"."+name); field.SetValue(owner,value);
        }
        internal static object Call(object owner,string name,params object[] args)
        {
            MethodInfo method=owner.GetType().GetMethod(name,Any,null,args.Select(a=>a.GetType()).ToArray(),null);
            Assert.IsNotNull(method,owner.GetType().FullName+"."+name+" signature");
            return method.Invoke(owner,args);
        }
        internal static object[] Items(object owner,string name) => ((IEnumerable)Member(owner,name)).Cast<object>().ToArray();
        private object Kind(string name) => Enum.Parse(GameType("EcoRecycleCargo+Kind"),name);
        private object[] Materials => Items(line,"Materials");
        private object[] Products => Items(line,"Products");
        private int Count(string name) => (int)Member(line,name);
        private void Capture()
        {
            line=Find("C3TreasurePuzzle.C3RecyclingLine");
            geometry=Find("C3TreasurePuzzle.C3RecyclingLineGeometry");
            device=Find("C3TreasurePuzzle.C3DeviceState");
            runtime=GameType("EcoAdventureRuntime").GetProperty("Instance").GetValue(null) as Component;
            Assert.IsNotNull(line); Assert.IsNotNull(geometry); Assert.IsNotNull(runtime);
            ((Behaviour)line).enabled=false; Set(line,"useFiniteTestSupply",false);
            Set(device,"useTestStartupDelay",false);
        }
        [UnitySetUp] public IEnumerator Open()
        {
            scope=new C3V11SafeCirculationTests();
            yield return scope.OpenFactory(); Capture(); Physics.SyncTransforms();
        }
        [UnityTearDown] public IEnumerator Close()
        {
            if (scope!=null) yield return scope.Cleanup();
        }
        private void Step(int ticks=1)
        {
            for (int i=0;i<ticks;i++) Call(line,"AdvanceSimulation",.05f);
            Physics.SyncTransforms();
        }
        private IEnumerator Until(Func<bool> condition,string reason,int limit=1600)
        {
            for (int i=0;i<limit && !condition();i++) { Step(); yield return null; }
            Assert.IsTrue(condition(),reason+"; blocked="+Member(line,"LastBlockedReason"));
        }
        private void Supply(string kind,int units=3,string prefix="input")
        {
            for (int i=0;i<units;i++) Assert.IsTrue((bool)Call(line,"TrySupply",prefix+"-"+i,Kind(kind)));
        }
        private void Activate(bool run=true)
        {
            Component progress=Find("C3TreasurePuzzle.C3TreasureProgress");
            foreach (string id in new[] {"fuse","control_chip","filter_module"})
                Assert.IsTrue((bool)Call(progress,"TryCollect",id,id));
            Type step=GameType("C3TreasurePuzzle.C3ProcessingStep");
            Array answer=Array.CreateInstance(step,3);
            string[] names={"Sorting","Recycling","PollutionTreatment"};
            for (int i=0;i<3;i++) answer.SetValue(Enum.Parse(step,names[i]),i);
            Assert.IsTrue((bool)Call(Find("C3TreasurePuzzle.C3PuzzleState"),"SubmitAnswer",answer));
            Assert.IsTrue((bool)Call(device,"TryActivate"));
            Assert.AreEqual("Starting",Member(device,"FactoryState").ToString());
            if (run) { Call(device,"NotifyStartupFinished"); Assert.AreEqual("Running",Member(device,"FactoryState").ToString()); }
        }
        internal static void AssertEquipmentConnections(Component geometry)
        {
            object[] sections=Items(geometry,"Sections");
            Assert.AreEqual(30,sections.Length,"Missing or duplicate transport sections");
            foreach (Component section in sections)
            {
                Collider surface=(Collider)Member(section,"Surface");
                Renderer renderer=(Renderer)Member(section,"SurfaceRenderer");
                Transform a=(Transform)Member(section,"Start"),b=(Transform)Member(section,"End");
                Assert.IsTrue(surface.enabled && !surface.isTrigger && surface.gameObject.activeInHierarchy,section.name);
                Assert.IsTrue(renderer.enabled,section.name);
                Assert.IsNotNull(renderer.sharedMaterial.shader,section.name);
                Assert.IsTrue(renderer.sharedMaterial.shader.isSupported,section.name+" shader");
                Assert.Less(Vector3.Distance(renderer.bounds.center,surface.bounds.center),.01f,section.name);
                Assert.Less(Vector3.Distance(renderer.bounds.size,surface.bounds.size),.01f,section.name);
                Assert.Greater((float)Member(section,"Width"),.75f,section.name);
                foreach (float t in new[] {0f,.25f,.5f,.75f,1f})
                {
                    Vector3 point=Vector3.Lerp(a.position,b.position,t);
                    Vector3 normal=surface.transform.up;
                    // Query 1mm inside the finite end face to avoid a ray exactly
                    // on a floating-point box edge; still compare to the real endpoint.
                    Vector3 inset=(b.position-a.position).normalized*(t==0f?.001f:t==1f?-.001f:0f);
                    Assert.IsTrue(surface.Raycast(new Ray(point+inset+normal*.3f,-normal),out RaycastHit hit,.6f),section.name);
                    Assert.Less(Vector3.Distance(hit.point,point),.01f,"Endpoint/mesh top mismatch "+section.name);
                }
            }
            Action<object,object> joint=(left,right)=>
            {
                Vector3 p=((Transform)Member(left,"End")).position;
                Vector3 q=((Transform)Member(right,"Start")).position;
                Assert.Less(Vector3.Distance(p,q),.01f,"Endpoint gap");
                Collider a=(Collider)Member(left,"Surface"),b=(Collider)Member(right,"Surface");
                Collider[] touching=Physics.OverlapSphere((p+q)*.5f+Vector3.up*.01f,.025f,~0,QueryTriggerInteraction.Ignore);
                Assert.Contains(a,touching,"First actual surface missing at joint");
                Assert.Contains(b,touching,"Second actual surface missing at joint");
            };
            object[] common=Items(geometry,"CommonSections");
            for (int i=1;i<common.Length;i++) joint(common[i-1],common[i]);
            joint(common[common.Length-1],Member(geometry,"MainBelt"));
            Collider main=(Collider)Member(Member(geometry,"MainBelt"),"Surface");
            foreach (object machine in Items(geometry,"Machines"))
            {
                Vector3 gate=((Transform)Member(Member(machine,"Chute"),"Start")).position;
                Assert.Less(Vector3.Distance(main.ClosestPoint(gate),gate),.01f,"Diversion does not meet real main belt");
                object[] input=Items(machine,"InputSections");
                for (int i=1;i<input.Length;i++) joint(input[i-1],input[i]);
                object[] output=Items(machine,"OutputSections");
                for (int i=1;i<output.Length;i++) joint(output[i-1],output[i]);
                Assert.Less(Vector3.Distance(((Collider)Member(machine,"ProcessBed")).ClosestPoint(
                    ((Transform)Member(Member(machine,"ReceivingBed"),"End")).position),
                    ((Transform)Member(Member(machine,"ReceivingBed"),"End")).position),.01f,"Input misses processing bed");
            }
        }
        [UnityTest] public IEnumerator ElevatedInfeed()
        {
            Transform entry=(Transform)Member(geometry,"BMaterialInterface");
            Assert.Greater(entry.position.y,6.8f); Assert.Greater(entry.position.x,0f);
            object first=Items(geometry,"CommonSections")[0];
            Collider belt=(Collider)Member(first,"Surface");
            Assert.That(belt.bounds.max.y,Is.EqualTo(entry.position.y).Within(.01f));
            Assert.Greater(belt.bounds.size.z,1f);
            AssertEquipmentConnections(geometry); yield break;
        }
        [UnityTest] public IEnumerator WallOpeningClearance()
        {
            Vector3 a=new Vector3(.7f,7.12f,12.5f);
            Transform proxies=GameObject.Find("C_CollisionProxies").transform;
            foreach (RaycastHit hit in Physics.SphereCastAll(a,.12f,Vector3.left,2f,~0,QueryTriggerInteraction.Ignore))
                Assert.IsFalse(hit.collider.transform.IsChildOf(proxies),"Upper material route hits wall proxy "+hit.collider.name);
            foreach (string name in new[] {"C_Wall_4_Lintel","C_Wall_4_Near","C_Wall_4_Far","B_Door_Floor",
                "B_Safety_End","B_Safety_Near","B_Safety_Far","P2_WALL_HIGH_NEAR","P2_WALL_HIGH_FAR","P2_WALL_HIGH_HEADER"})
                Assert.IsTrue(GameObject.Find(name).GetComponent<Collider>().enabled,name);
            Assert.IsNotNull(GameObject.Find("V11_BC_PVC_CURTAIN"));
            yield return scope.GroundBCDoorApproachRemainsOpenUnderTheEastFlight();
        }
        [UnityTest] public IEnumerator ContinuousConveyorRoute()
        { AssertEquipmentConnections(geometry); yield break; }
        [UnityTest] public IEnumerator IngressIdentityAndTriggerDedup()
        {
            GameObject upstream=new GameObject("P2_TEST_UPSTREAM_MATERIAL");
            upstream.transform.position=((Transform)Member(geometry,"BMaterialInterface")).position+Vector3.up*.12f;
            GameObject mesh=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mesh.transform.SetParent(upstream.transform,false); mesh.transform.localScale=Vector3.one*.24f;
            mesh.GetComponent<Collider>().isTrigger=true;
            Component cargo=upstream.AddComponent(GameType("EcoRecycleCargo"));
            Set(cargo,"materialId","external-material"); Set(cargo,"materialType",Kind("Metal"));
            GameObject detector=new GameObject("P2_TEST_INGRESS_DETECTOR");
            bool transferred=false;
            try
            {
                Component sorter=detector.AddComponent(GameType("EcoConveyorSorter"));
                detector.transform.position=((Transform)Member(geometry,"BMaterialInterface")).position;
                BoxCollider gate=detector.AddComponent<BoxCollider>(); gate.isTrigger=true;
                gate.center=Vector3.up*.12f; gate.size=new Vector3(.30f,.40f,.50f);
                Rigidbody body=detector.AddComponent<Rigidbody>(); body.isKinematic=true; body.useGravity=false;
                Physics.SyncTransforms(); int id=cargo.GetInstanceID();
                yield return new WaitForFixedUpdate(); yield return null;
                transferred=Materials.Length==1;
                Assert.IsTrue(transferred,"Native OnTriggerEnter did not transfer upstream material");
                Assert.AreEqual(id,((Component)Member(Materials.Single(),"Cargo")).GetInstanceID(),"Ingress must transfer the same object");
                Assert.IsFalse((bool)Call(sorter,"TrySort",cargo));
                Assert.AreEqual(1,Count("AcceptedUnits")); Assert.AreEqual(0,Count("ProcessedUnits"));
                Assert.AreEqual(0,Member(runtime,"RecyclingReceiptCount"));
            }
            finally { Object.DestroyImmediate(detector); if (!transferred) Object.DestroyImmediate(upstream); }
            yield break;
        }
        [UnityTest] public IEnumerator CargoFollowsRoute()
        {
            Supply("Paper",1); object record=Materials.Single();
            Component cargo=(Component)Member(record,"Cargo"); int identity=cargo.GetInstanceID();
            Vector3 previous=cargo.transform.position;
            Activate();
            Collider[] surfaces=Items(geometry,"Sections").Select(s=>(Collider)Member(s,"Surface"))
                .Concat(Items(geometry,"Machines").Select(m=>(Collider)Member(m,"ProcessBed"))).ToArray();
            for (int i=0;i<600 && Member(record,"State").ToString()!="MachineInput";i++)
            {
                Step();
                Assert.AreEqual(identity,((Component)Member(record,"Cargo")).GetInstanceID());
                Assert.Less(Vector3.Distance(previous,cargo.transform.position),.37f,"Cargo jumped between route segments");
                previous=cargo.transform.position;
                if (Member(record,"State").ToString()!="Spawned")
                {
                    float contact=surfaces.Min(s=>Mathf.Abs(Vector3.Distance(cargo.transform.position,s.ClosestPoint(cargo.transform.position))-.12f));
                    Assert.Less(contact,.01f,"Cargo is floating or penetrates the real conveyor");
                    foreach (Collider obstacle in Physics.OverlapSphere(cargo.transform.position,.12f,~0,QueryTriggerInteraction.Ignore))
                        if (!obstacle.transform.IsChildOf(cargo.transform))
                            Assert.GreaterOrEqual(Vector3.Distance(cargo.transform.position,obstacle.ClosestPoint(cargo.transform.position)),.119f,
                                "Cargo penetrates "+obstacle.name);
                    object machine=Items(geometry,"Machines")[0];
                    Transform press=(Transform)Member(machine,"Press");
                    Assert.Greater(Vector3.Distance(cargo.transform.position,press.GetComponent<Renderer>().bounds.ClosestPoint(cargo.transform.position)),.12f,
                        "Transported cargo intersects raised compression head");
                }
                yield return null;
            }
            Assert.AreEqual("MachineInput",Member(record,"State").ToString());
            CollectionAssert.AreEqual(new[] {"Spawned","Feeding","MainBelt","Identified","Diverting","MachineInput"},
                Items(record,"History").Select(x=>x.ToString()).ToArray());
        }
        private IEnumerator Sorting(string kind,int expectedMachine,int legacyKind)
        {
            Supply(kind); Activate();
            yield return Until(()=>Products.Length==1,kind+" never completed");
            Assert.IsTrue(Materials.All(r=>(int)Member(r,"TargetMachine")==expectedMachine));
            Assert.IsTrue(Materials.All(r=>Member(r,"MaterialType").ToString()==kind && (bool)Member(r,"Counted")));
            Transform tray=(Transform)Member(Items(geometry,"Machines")[expectedMachine],"Tray");
            foreach (object raw in Materials)
                Assert.Less(Vector3.Distance(((Component)Member(raw,"Cargo")).transform.position,tray.position),.6f,
                    "Raw material did not actually reach the assigned machine");
            Assert.AreEqual(kind,Member(Products[0],"MaterialType").ToString());
            Assert.IsTrue((bool)Call(runtime,"HasSortedMaterial",legacyKind));
            Assert.AreEqual(3,((string[])Member(Products[0],"SourceMaterialIds")).Distinct().Count());
        }
        [UnityTest] public IEnumerator PaperSorting() { yield return Sorting("Paper",0,2); }
        [UnityTest] public IEnumerator MetalSorting() { yield return Sorting("Metal",1,1); }
        [UnityTest] public IEnumerator PlasticSorting() { yield return Sorting("Plastic",2,0); }
        [UnityTest] public IEnumerator ProcessingState()
        {
            Supply("Paper",2); Activate(); Step(400);
            Assert.AreEqual(0,Count("ProcessingUnits")); Assert.AreEqual(0,Products.Length);
            Assert.IsFalse((bool)Call(runtime,"HasSortedMaterial",2),"Two unprocessed units must not unlock paper statistics");
            Assert.IsTrue((bool)Call(line,"TrySupply","third",Kind("Paper")));
            yield return Until(()=>Count("ProcessingUnits")==3,"Processing did not wait for all three actual arrivals");
            Assert.IsTrue(Materials.All(r=>Items(r,"History").Select(s=>s.ToString()).Contains("MachineInput")));
            Assert.AreEqual(0,Count("ProcessedUnits")); Assert.AreEqual(0,Products.Length);
        }
        [UnityTest] public IEnumerator MachineOutput()
        {
            Supply("Metal"); Activate();
            yield return Until(()=>Count("ProcessingUnits")==3,"No metal processing");
            Transform press=(Transform)Member(Items(geometry,"Machines")[1],"Press");
            Vector3 before=press.position; Step(12);
            Assert.Less(press.position.y,before.y-.1f,"Compression head must visibly move before output");
            Assert.AreEqual(0,Products.Length,"Product appeared before processing finished");
            yield return Until(()=>Products.Length==1,"No processed metal block");
            object batch=Products.Single(); Transform product=(Transform)Member(batch,"Product");
            Assert.IsTrue(product.gameObject.activeInHierarchy);
            Assert.IsFalse((bool)Member(batch,"OutputReached"));
            Assert.IsTrue(Materials.All(r=>(bool)Member(r,"ProcessingComplete") && (bool)Member(r,"ProductCreated")));
            Assert.Less(Vector3.Distance(product.position,((Transform)Member(Items(geometry,"Machines")[1],"Tray")).position),.5f,
                "New product must form inside the machine, not teleport to the external belt");
        }
        [UnityTest] public IEnumerator OutputConveyor()
        {
            Supply("Paper",3,"paper"); Supply("Metal",3,"metal"); Supply("Plastic",3,"plastic"); Activate();
            int seen=0;
            Dictionary<string,int> identities=new Dictionary<string,int>();
            Dictionary<string,Vector3> positions=new Dictionary<string,Vector3>();
            GameObject probe=new GameObject("P2_TEST_OUTPUT_SHAPE");
            BoxCollider shape=probe.AddComponent<BoxCollider>(); shape.enabled=false;
            try
            {
                for (int i=0;i<1000 && Count("CollectedProducts")<3;i++)
                {
                    Step();
                    foreach (object batch in Products)
                    {
                        int lane=Member(batch,"MaterialType").ToString()=="Paper"?0:Member(batch,"MaterialType").ToString()=="Metal"?1:2;
                        object machine=Items(geometry,"Machines")[lane]; Transform product=(Transform)Member(batch,"Product");
                        string id=(string)Member(batch,"BatchId");
                        if (identities.ContainsKey(id))
                        {
                            Assert.AreEqual(identities[id],product.GetInstanceID(),"Output swapped product objects");
                            Assert.Less(Vector3.Distance(positions[id],product.position),.18f,"Output product jumped");
                        }
                        else identities.Add(id,product.GetInstanceID());
                        positions[id]=product.position;
                        Assert.That(product.position.x,Is.EqualTo(((Transform)Member(machine,"Root")).position.x).Within(1f));
                        Collider[] supports=Items(machine,"OutputSections").Select(s=>(Collider)Member(s,"Surface"))
                            .Concat(new[] {(Collider)Member(machine,"ProcessBed")}).ToArray();
                        MeshFilter[] meshes=product.GetComponentsInChildren<MeshFilter>();
                        Assert.Greater(meshes.Length,0);
                        Bounds envelope=new Bounds(Vector3.zero,Vector3.zero);
                        foreach (MeshFilter mesh in meshes)
                            foreach (Vector3 vertex in mesh.sharedMesh.vertices)
                                envelope.Encapsulate(product.InverseTransformPoint(mesh.transform.TransformPoint(vertex)));
                        shape.center=envelope.center; shape.size=envelope.size+Vector3.one*.006f;
                        Assert.IsTrue(supports.Any(s=>Physics.ComputePenetration(shape,product.position,product.rotation,
                            s,s.transform.position,s.transform.rotation,out _,out _)),
                            "Finished product floats above its actual finite output surface");
                        shape.size=envelope.size;
                        foreach (Collider obstacle in Physics.OverlapBox(product.position,shape.size*.5f,product.rotation,~0,QueryTriggerInteraction.Ignore))
                            if (!obstacle.transform.IsChildOf(product) && Physics.ComputePenetration(shape,product.position,product.rotation,
                                obstacle,obstacle.transform.position,obstacle.transform.rotation,out _,out float depth))
                                Assert.LessOrEqual(depth,.01f,"Finished product penetrates "+obstacle.name);
                        seen++;
                    }
                    yield return null;
                }
            }
            finally { Object.DestroyImmediate(probe); }
            Assert.Greater(seen,30,"Must observe actual transport, not only final counts");
            Assert.AreEqual(3,Count("CollectedProducts"));
            foreach (object batch in Products)
            {
                int lane=Member(batch,"MaterialType").ToString()=="Paper"?0:Member(batch,"MaterialType").ToString()=="Metal"?1:2;
                Vector3 destination=((Transform)Member(Items(geometry,"Machines")[lane],"Collection")).position;
                Transform product=(Transform)Member(batch,"Product");
                Assert.That(product.position.z,Is.EqualTo(destination.z).Within(.01f));
                Assert.IsTrue((bool)Member(batch,"OutputReached"));
            }
        }
        [UnityTest] public IEnumerator MaterialAccounting()
        {
            Assert.AreEqual(0,Convert.ToInt32(Kind("Metal"))); Assert.AreEqual(1,Convert.ToInt32(Kind("Plastic")));
            Assert.AreEqual(2,Convert.ToInt32(Kind("Electronics"))); Assert.AreEqual(3,Convert.ToInt32(Kind("Paper")));
            Supply("Paper",9,"account");
            Assert.IsFalse((bool)Call(line,"TrySupply","account-0",Kind("Metal")));
            Assert.IsFalse((bool)Call(line,"TrySupply","unsupported",Kind("Electronics")));
            Assert.IsFalse((bool)Call(line,"TrySupply","full",Kind("Paper")));
            int events=0; Action<string> handler=id=>events++;
            EventInfo completed=line.GetType().GetEvent("BatchCompleted"); completed.AddEventHandler(line,handler);
            try
            {
                Activate(); string inventory=string.Join("|",Items(runtime,"inventory").Select(i=>JsonUtility.ToJson(i)));
                string[] slots=(string[])((string[])Member(runtime,"quickSlots")).Clone();
                int ammo=(int)Member(runtime,"ammoInMagazine"),reserve=(int)Member(runtime,"reserveAmmo");
                for (int i=0;i<1500 && Count("CollectedProducts")<3;i++)
                {
                    Step();
                    Assert.AreEqual(Count("AcceptedUnits"),Count("WaitingUnits")+Count("ProcessingUnits")+Count("ProcessedUnits"));
                    Assert.AreEqual(Count("ProcessedUnits"),Count("ProcessedBatches")*3);
                    Assert.LessOrEqual(Count("CollectedProducts"),Count("OutputProducts"));
                    Assert.LessOrEqual(Count("OutputProducts"),Count("ProcessedBatches"));
                    yield return null;
                }
                Assert.AreEqual(3,events); Assert.AreEqual(3,Count("CollectedProducts"));
                Assert.AreEqual(9,Member(runtime,"RecyclingReceiptCount"));
                Assert.AreEqual(inventory,string.Join("|",Items(runtime,"inventory").Select(i=>JsonUtility.ToJson(i))),"Sorting must not grant inventory resources");
                CollectionAssert.AreEqual(slots,(string[])Member(runtime,"quickSlots"));
                Assert.AreEqual(ammo,Member(runtime,"ammoInMagazine")); Assert.AreEqual(reserve,Member(runtime,"reserveAmmo"));
                object batch=Products[0];
                Assert.IsFalse((bool)Call(runtime,"TryCommitRecyclingBatch",(string)Member(batch,"BatchId"),Kind("Paper"),(string[])Member(batch,"SourceMaterialIds")));
                GameObject trigger=new GameObject("P2_TEST_LEGACY_TRIGGER");
                try
                {
                    Component sorter=trigger.AddComponent(GameType("EcoConveyorSorter"));
                    foreach (object raw in Materials) Assert.IsFalse((bool)Call(sorter,"TrySort",Member(raw,"Cargo")));
                }
                finally { Object.DestroyImmediate(trigger); }
                Step(200); Assert.AreEqual(3,events); Assert.AreEqual(3,Products.Length);
            }
            finally { completed.RemoveEventHandler(line,handler); }
        }
        [UnityTest] public IEnumerator ActivationState()
        {
            Supply("Plastic"); Step(100);
            Assert.AreEqual(0,Count("FedUnits")); Assert.AreEqual(0,Count("ProcessedUnits"));
            Activate(false); Step(100);
            Assert.AreEqual("Starting",Member(device,"FactoryState").ToString()); Assert.AreEqual(0,Products.Length);
            Call(device,"NotifyStartupFinished"); Step(12);
            Vector3 position=((Component)Member(Materials[0],"Cargo")).transform.position;
            try { Time.timeScale=0f; Step(100); Assert.AreEqual(position,((Component)Member(Materials[0],"Cargo")).transform.position); }
            finally { Time.timeScale=1f; }
            Assert.IsTrue((bool)Call(device,"TryCompleteAtExit"));
            Assert.IsFalse((bool)Call(line,"TrySupply","after-completion",Kind("Metal")));
            yield return Until(()=>Count("CollectedProducts")==1,"Completed must drain existing material without new admissions");
        }
        [UnityTest] public IEnumerator ReloadSafety()
        {
            Supply("Paper",3,"replay"); Activate();
            yield return Until(()=>Count("CollectedProducts")==1,"No first-run receipt");
            int equipment=((Transform)Member(geometry,"BuildRoot")).GetComponentsInChildren<Transform>(true).Length;
            ((Behaviour)geometry).enabled=false; ((Behaviour)geometry).enabled=true;
            ((Behaviour)line).enabled=true; ((Behaviour)line).enabled=false;
            Assert.AreEqual(equipment,((Transform)Member(geometry,"BuildRoot")).GetComponentsInChildren<Transform>(true).Length);
            Assert.AreEqual(3,Count("AcceptedUnits")); Assert.AreEqual(1,Count("ProcessedBatches"));
            Component previous=line;
            yield return SceneManager.LoadSceneAsync(ScenePath,LoadSceneMode.Single); yield return null;
            Assert.IsTrue(previous==null,"Old scene controller survived reload"); Capture(); Physics.SyncTransforms();
            Assert.AreEqual(1,Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Count(t=>t.name=="C3_P2_RecyclingLine"));
            Assert.AreEqual(0,Materials.Length); Assert.AreEqual(0,Products.Length);
            Assert.AreEqual(3,Member(runtime,"RecyclingReceiptCount"));
            for (int i=0;i<3;i++) Assert.IsFalse((bool)Call(line,"TrySupply","replay-"+i,Kind("Paper")));
            AssertEquipmentConnections(geometry);
        }
        [UnityTest] public IEnumerator UpperWalkwayClearance()
        {
            yield return scope.WorldGeometryMatchesEnabledCollidersAndHasNoHeadObstructions();
            yield return scope.WalkBThenRearConnectionAndDescendA();
        }
        [UnityTest] public IEnumerator ChipAndGuardRegression()
        {
            yield return scope.WalkACollectExactlyOneChipAndReturnDownB();
            yield return scope.EveryGuardStopsStandingAndProneCapsulesIncludingCorners();
            yield return scope.GroundBCDoorApproachRemainsOpenUnderTheEastFlight();
        }
        [UnityTest] public IEnumerator ProneWalkwayClearance()
        {
            GameObject player=GameObject.Find("M2_Player");
            Component controller=player.GetComponent(GameType("C3TreasurePuzzle.C3FirstPersonController"));
            CharacterController body=player.GetComponent<CharacterController>();
            Call(scope,"StartAt",new Vector3(-1.55f,4.5f,14.2f)); Set(controller,"isProne",true);
            try
            {
                foreach (Vector3 target in new[] {new Vector3(-1.55f,4.5f,19.2f),new Vector3(-23.2f,4.5f,19.2f),new Vector3(-23.2f,4.5f,14.2f)})
                {
                    for (int i=0;i<1800;i++)
                    {
                        Vector3 delta=target-player.transform.position; delta.y=0f;
                        if (delta.magnitude<.09f) break;
                        player.transform.rotation=Quaternion.LookRotation(delta);
                        Call(controller,"TickMovement",Vector2.up,false,false,false,1f/60f);
                        Assert.Greater(player.transform.position.y,4.2f,"Prone player fell through deck");
                        yield return null;
                    }
                    Assert.Less(Vector2.Distance(new Vector2(player.transform.position.x,player.transform.position.z),new Vector2(target.x,target.z)),.15f);
                    Assert.That(body.height,Is.EqualTo((float)Member(controller,"proneHeight")).Within(.01f));
                }
            }
            finally { Set(controller,"isProne",false); }
        }
        [UnityTest] public IEnumerator Regression()
        {
            Set(line,"useFiniteTestSupply",true); ((Behaviour)line).enabled=true;
            yield return scope.OriginalPartsConsoleSortingBenchesAndWeaponStillOperate();
        }
    }
}
