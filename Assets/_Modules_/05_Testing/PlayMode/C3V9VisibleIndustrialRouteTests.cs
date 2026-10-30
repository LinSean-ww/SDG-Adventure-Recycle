using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace M6IntegrationTests
{
    /// <summary>
    /// Current-layout *Unity PlayMode* checks (original file/GUID retained). None of these results can be claimed to pass
    /// until actually run in Unity 2023.1.22f1. Renderer bounds test the imported
    /// OBJ mesh, not the coordinates from the offline mesh generator.
    /// </summary>
    public sealed class C3V9VisibleIndustrialRouteTests
    {
        const string Scene="Assets/_Modules_/02_Levels/Level03/Scenes/C_Main.unity";
        private const float BoundsTolerance=.01f;
        private IEnumerator Open(Action<Transform> beforePhysicsSync=null)
        {
            yield return SceneManager.LoadSceneAsync(Scene,LoadSceneMode.Single);
            yield return null;
            Transform root=CirculationRoot();
            // Capture real pre-sync data before this one-time load boundary.
            // autoSyncTransforms is deliberately left at the project's setting.
            beforePhysicsSync?.Invoke(root);
            Physics.SyncTransforms();
        }
        [UnityTest]
        public IEnumerator ReplacementStairsHaveMatchingPhysicalTreadsOnBothSides()
        {
            Type layout=Type.GetType("C3TreasurePuzzle.C3V3Circulation, Assembly-CSharp");
            Assert.IsNotNull(layout,"Missing production C3V3Circulation type");
            int steps=LayoutConstant<int>(layout,"StairSteps");
            Assert.AreEqual(22,steps,"V11 requires 22 treads on each of its two stairs");
            float ground=LayoutConstant<float>(layout,"GroundSurface");
            float rise=(LayoutConstant<float>(layout,"WalkSurface")-ground)/steps;
            List<TreadProbe> probes=new List<TreadProbe>();
            yield return Open(root=>
            {
                Assert.IsNotNull(root.parent,"Circulation root must belong to its scene generator");
                Assert.IsNotNull(root.parent.GetComponent(layout),"Root owner has no C3V3Circulation");
                foreach (string side in new[] { "A_WEST", "B_EAST" })
                {
                    Transform stair=UniqueChild(root,"STAIR_"+side);
                    Assert.AreEqual(steps,stair.Cast<Transform>().Count(t=>t.name.StartsWith("TREAD_")),
                        "Expected "+steps+" treads; actual children of "+Hierarchy(stair)+"\n"+TransformChain(stair));
                    for (int i=1;i<=steps;i++)
                    {
                        Transform tread=UniqueChild(stair,"TREAD_"+i.ToString("D2"));
                        probes.Add(new TreadProbe(tread,ground+i*rise));
                    }
                }
            });
            foreach (TreadProbe probe in probes) probe.AfterSync=new TreadSnapshot(probe.Tread);

            string fixedStep="not needed";
            if (probes.Any(p=>!p.AfterSync.Matches(p.ExpectedTop)))
            {
                // A normal physics step can distinguish registration delay from
                // a persistent mismatch. Never reactivate objects or change time,
                // simulation mode, collider data or the global auto-sync flag.
                if (probes.All(p=>p.AfterSync.StructureReady) && Time.timeScale>0f &&
                    Time.fixedDeltaTime>0f && Physics.simulationMode==SimulationMode.FixedUpdate)
                {
                    fixedStep="waited one normal FixedUpdate; no second SyncTransforms";
                    yield return new WaitForFixedUpdate();
                    foreach (TreadProbe probe in probes) probe.AfterFixed=new TreadSnapshot(probe.Tread);
                }
                else fixedStep="not waited: invalid activation/components or no running automatic fixed simulation";
            }
            foreach (TreadProbe probe in probes)
            {
                TreadSnapshot actual=probe.Current;
                string evidence=probe.Evidence(fixedStep);
                Assert.IsNotNull(actual.Renderer,evidence);
                Assert.IsNotNull(actual.Collider,evidence);
                Assert.IsTrue(actual.Active,evidence);
                Assert.IsTrue(actual.RendererEnabled,evidence);
                Assert.IsTrue(actual.ColliderEnabled,evidence);
                Assert.IsFalse(actual.Trigger,evidence);
                Assert.IsInstanceOf<BoxCollider>(actual.Collider,evidence);
                Assert.IsTrue(HasVolume(actual.Visual),"Empty/non-finite Renderer bounds\n"+evidence);
                Assert.IsTrue(HasVolume(actual.Physical),"Empty/non-finite Collider bounds\n"+evidence);
                Assert.Less(Vector3.Distance(actual.Visual.center,actual.Physical.center),BoundsTolerance,evidence);
                Assert.That(actual.Physical.max.y,Is.EqualTo(probe.ExpectedTop).Within(BoundsTolerance),evidence);
            }
            // One evidence block only when a pre-sync/post-sync anomaly occurred.
            // Persistent problems fail above with the affected tread's snapshots.
            TreadProbe first=probes.FirstOrDefault(p=>!p.Before.Matches(p.ExpectedTop) || !p.AfterSync.Matches(p.ExpectedTop));
            if (first!=null) Debug.Log("V9 stair bounds diagnostic\n"+first.Evidence(fixedStep));
            Assert.IsNull(GameObject.Find("V9_RIGHT_STAIR_WALKABLE_RAMP"));
            Assert.IsNotNull(GameObject.Find("UpperChipMaintenanceBench"));
        }
        private static T LayoutConstant<T>(Type layout,string name)
        {
            FieldInfo field=layout.GetField(name,BindingFlags.Public|BindingFlags.Static);
            Assert.IsNotNull(field,"Missing production layout constant: "+name);
            Assert.IsTrue(field.IsLiteral,"Expected a production const: "+name);
            return (T)field.GetRawConstantValue();
        }
        private static Transform CirculationRoot()
        {
            UnityEngine.SceneManagement.Scene loaded=SceneManager.GetSceneByPath(Scene);
            Assert.IsTrue(loaded.IsValid() && loaded.isLoaded,"Expected loaded scene: "+Scene);
            Transform[] roots=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Where(t=>t.name=="C3_V3_VerifiedCirculation").ToArray();
            Assert.AreEqual(1,roots.Length,"Expected one circulation root across loaded scenes; actual="+
                string.Join("; ",roots.Select(t=>t.gameObject.scene.path+":"+Hierarchy(t)+"#"+t.GetInstanceID())));
            Transform root=roots[0];
            Assert.AreEqual(loaded,root.gameObject.scene,"Wrong-scene circulation root\n"+TransformChain(root));
            Assert.IsTrue(root.gameObject.activeInHierarchy,"Inactive circulation root\n"+TransformChain(root));
            return root;
        }
        private static Transform UniqueChild(Transform parent,string name)
        {
            // Direct scoped lookup includes inactive children and rejects duplicate
            // names, rather than borrowing an arbitrary GameObject.Find match.
            Transform[] matches=parent.Cast<Transform>().Where(t=>t.name==name).ToArray();
            Assert.AreEqual(1,matches.Length,"Expected one "+Hierarchy(parent)+"/"+name+
                "; actual count="+matches.Length+"; ids="+string.Join(",",matches.Select(t=>t.GetInstanceID()))+
                "\n"+TransformChain(parent));
            return matches[0];
        }
        private static string Hierarchy(Transform item)
        {
            string path=item.name;
            for (Transform parent=item.parent;parent;parent=parent.parent) path=parent.name+"/"+path;
            return path;
        }
        private static string TransformChain(Transform item)
        {
            StringBuilder text=new StringBuilder();
            text.AppendLine("scene="+item.gameObject.scene.name+" path="+item.gameObject.scene.path);
            for (Transform current=item;current;current=current.parent)
                text.AppendLine(current.name+"#"+current.GetInstanceID()+
                    " position="+current.position.ToString("F6")+" localPosition="+current.localPosition.ToString("F6")+
                    " lossyScale="+current.lossyScale.ToString("F6")+" rotation="+current.rotation.ToString("F6")+
                    " localRotation="+current.localRotation.ToString("F6")+
                    " activeSelf="+current.gameObject.activeSelf+" activeInHierarchy="+current.gameObject.activeInHierarchy);
            return text.ToString();
        }
        private static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsNaN(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.x) && !float.IsInfinity(value.y) && !float.IsInfinity(value.z);
        private static bool HasVolume(Bounds bounds) => Finite(bounds.center) && Finite(bounds.size) &&
            bounds.size.x>0f && bounds.size.y>0f && bounds.size.z>0f;
        private static string BoundsText(Bounds bounds) => "center="+bounds.center.ToString("F6")+
            " size="+bounds.size.ToString("F6")+" min="+bounds.min.ToString("F6")+" max="+bounds.max.ToString("F6")+
            " nonemptyFinite="+HasVolume(bounds);

        private sealed class TreadSnapshot
        {
            public readonly Renderer Renderer;
            public readonly Collider Collider;
            public readonly Bounds Visual,Physical;
            public readonly bool Active,RendererEnabled,ColliderEnabled,Trigger;
            public readonly string Details;
            public bool StructureReady => Renderer && Collider is BoxCollider && Active &&
                RendererEnabled && ColliderEnabled && !Trigger;
            public bool Matches(float top) => StructureReady && HasVolume(Visual) && HasVolume(Physical) &&
                Vector3.Distance(Visual.center,Physical.center)<BoundsTolerance && Mathf.Abs(Physical.max.y-top)<=BoundsTolerance;
            public TreadSnapshot(Transform tread)
            {
                Renderer=tread.GetComponent<Renderer>();
                Collider=tread.GetComponent<Collider>();
                Active=tread.gameObject.activeInHierarchy;
                RendererEnabled=Renderer && Renderer.enabled;
                ColliderEnabled=Collider && Collider.enabled;
                Trigger=Collider && Collider.isTrigger;
                Visual=Renderer?Renderer.bounds:default(Bounds);
                Physical=Collider?Collider.bounds:default(Bounds);
                BoxCollider box=Collider as BoxCollider;
                Details="scene="+tread.gameObject.scene.name+" path="+tread.gameObject.scene.path+
                    " hierarchy="+Hierarchy(tread)+" frame="+Time.frameCount+" fixedTime="+Time.fixedTime+
                    " autoSyncTransforms="+Physics.autoSyncTransforms+" simulationMode="+Physics.simulationMode+
                    " timeScale="+Time.timeScale+" fixedDeltaTime="+Time.fixedDeltaTime+"\n"+TransformChain(tread)+
                    "Renderer exists="+(Renderer!=null)+" enabled="+RendererEnabled+" "+BoundsText(Visual)+"\n"+
                    "Collider exists="+(Collider!=null)+" type="+(Collider?Collider.GetType().FullName:"missing")+
                    " enabled="+ColliderEnabled+" isTrigger="+Trigger+" "+BoundsText(Physical)+"\n"+
                    (box?"BoxCollider local center="+box.center.ToString("F6")+" size="+box.size.ToString("F6")+"\n":"")+
                    "centerDistance="+Vector3.Distance(Visual.center,Physical.center).ToString("F9");
            }
        }
        private sealed class TreadProbe
        {
            public readonly Transform Tread;
            public readonly float ExpectedTop;
            public readonly TreadSnapshot Before;
            public TreadSnapshot AfterSync,AfterFixed;
            public TreadSnapshot Current => AfterFixed??AfterSync;
            public TreadProbe(Transform tread,float expectedTop)
            {
                Tread=tread;
                ExpectedTop=expectedTop;
                Before=new TreadSnapshot(tread);
            }
            public string Evidence(string fixedStep)
            {
                string result=!Before.StructureReady || !Current.StructureReady?"activation/component state invalid":
                    !Before.Matches(ExpectedTop) && AfterSync.Matches(ExpectedTop)?"matched after SyncTransforms; supports sync-timing hypothesis for this run":
                    AfterFixed!=null && !AfterSync.Matches(ExpectedTop) && AfterFixed.Matches(ExpectedTop)?"matched after normal FixedUpdate":
                    !HasVolume(Current.Physical)?"empty/non-finite Collider bounds persist":
                    !Current.Matches(ExpectedTop)?"bounds/height mismatch persists":"bounds match";
                return Hierarchy(Tread)+" expected: active/enabled solid BoxCollider, nonempty finite bounds, centerDistance<"+
                    BoundsTolerance+", topY="+ExpectedTop.ToString("F6")+" +/-"+BoundsTolerance+
                    "\nclassification="+result+"; fixedStep="+fixedStep+
                    "\nBEFORE SYNC\n"+Before.Details+"\nAFTER SYNC\n"+AfterSync.Details+
                    "\nAFTER FIXED UPDATE\n"+(AfterFixed!=null?AfterFixed.Details:"NOT ATTEMPTED");
            }
        }
        [UnityTest]
        public IEnumerator BReceivingBeltActuallyExistsOnCFrontFaceAndMeetsMainHighBelt()
        {
            // Validate the actual imported belt and rebuilt doorway on both loads;
            // duplicate curtain roots/strips must not survive a scene reload.
            for (int load=0;load<2;load++)
            {
                yield return Open();
                GameObject g=GameObject.Find("V9_BDoorToSorterVisibleBelt");
                Assert.IsNotNull(g,"V9 B-port feed mesh is missing");
                MeshRenderer r=g.GetComponentInChildren<MeshRenderer>(true);
                Assert.IsNotNull(r);
                Assert.IsTrue(r.enabled && r.gameObject.activeInHierarchy);
                Bounds b=r.bounds;
                Assert.Greater(b.min.z,11.1f,"B belt extends into front walkway");
                Assert.Less(b.min.z,12.0f,"Raised B feed still hidden entirely behind sorter");
                Assert.Greater(b.max.z,16.85f,"B conveyor does not reach C sorting line");
                Assert.Less(b.min.x,-6.15f, "Unity OBJ X-axis mirrored: B belt outside C hall");
                Assert.Greater(b.max.x,-.90f);
                Assert.Less(b.max.x,-.44f,"V9 feed intrudes through B-room wall");
                Assert.Greater(b.max.y,6.48f,"B feeder upper section should be visible over sorter");
                Assert.Greater(b.min.z,10.7f+.30f+.08f,"B feed overlaps the standing pedestrian corridor");
                foreach (string name in new[] { "V9_BDoorToSorterSteelFrame",
                    "V9_BDoorToSorterYellowGuards", "V9_BDoorToSorterRollers" })
                {
                    GameObject part=GameObject.Find(name);
                    Assert.IsNotNull(part,name);
                    Assert.IsTrue(part.GetComponentsInChildren<Renderer>().Any(v=>v.enabled),name);
                }
                Transform[] sceneTransforms=Object.FindObjectsByType<Transform>(
                    FindObjectsInactive.Include,FindObjectsSortMode.None);
                Assert.AreEqual(7,sceneTransforms.Count(t=>t.name.StartsWith("B_PORTAL_GENUINE_FEED_CARGO_")));
                Assert.AreEqual(1,sceneTransforms.Count(t=>t.name=="V11_BC_PVC_CURTAIN"));
                AssertBCDoorwayAndCurtain();
            }
        }
        private static Collider RequiredSolid(string name)
        {
            GameObject item=GameObject.Find(name);
            Assert.IsNotNull(item,"Required doorway structure missing: "+name);
            Collider collider=item.GetComponent<Collider>();
            Assert.IsNotNull(collider,name);
            Assert.IsTrue(collider.enabled && !collider.isTrigger,name+" must remain solid");
            return collider;
        }
        private static void AssertBCDoorwayAndCurtain()
        {
            foreach (string obsolete in new[] { "V7_OPAQUE_C_TO_B_RECEIVING_SHUTTER",
                "V7_B_SHUTTER_PRESSED_STEEL_SLAT", "V7_C_SIDE_B_FEED_RECEIVING_HATCH" })
                Assert.IsNull(GameObject.Find(obsolete),"Opaque shutter art must be replaced: "+obsolete);

            Collider near=RequiredSolid("C_Wall_4_Near");
            Collider far=RequiredSolid("C_Wall_4_Far");
            Collider lintel=RequiredSolid("C_Wall_4_Lintel");
            Collider floor=RequiredSolid("B_Door_Floor");
            Collider hallFloor=RequiredSolid("C_Floor");
            Assert.That(near.bounds.max.z,Is.EqualTo(10.2f).Within(.01f));
            Assert.That(far.bounds.min.z,Is.EqualTo(14.8f).Within(.01f));
            Assert.That(lintel.bounds.min.y,Is.EqualTo(3.5f).Within(.01f));
            Assert.That(floor.bounds.min.x,Is.EqualTo(-.4f).Within(.01f));
            Assert.That(floor.bounds.max.x,Is.EqualTo(2f).Within(.01f));
            Assert.That(floor.bounds.max.y,Is.EqualTo(.55f).Within(.01f));
            Assert.LessOrEqual(floor.bounds.min.x,hallFloor.bounds.max.x,"Floor gap at zone boundary");
            Assert.That(hallFloor.bounds.max.y,Is.EqualTo(floor.bounds.max.y).Within(.02f));
            Assert.That(RequiredSolid("B_Safety_End").bounds.min.x,Is.EqualTo(1.8f).Within(.01f));
            Assert.That(RequiredSolid("B_Safety_Near").bounds.max.z,Is.EqualTo(9.6f).Within(.01f));
            Assert.That(RequiredSolid("B_Safety_Far").bounds.min.z,Is.EqualTo(15.4f).Within(.01f));
            foreach (string frame in new[] { "B_NearJamb", "B_FarJamb", "B_LintelCap" })
            {
                GameObject item=GameObject.Find(frame);
                Assert.IsNotNull(item,frame);
                Renderer visible=item.GetComponent<Renderer>();
                Assert.IsNotNull(visible,frame);
                Assert.IsTrue(visible.enabled,frame);
            }
            Assert.IsTrue(Physics.Raycast(new Vector3(.6f,3f,10.7f),Vector3.down,
                out RaycastHit support,3f,~0,QueryTriggerInteraction.Ignore));
            Assert.AreSame(floor,support.collider,"B-side target must stand on the retained doorway floor");
            GameObject factory=GameObject.Find("C3_ReferenceRecyclingFactory_Visuals");
            Assert.IsNotNull(factory);
            Bounds opening=new Bounds(new Vector3(-.3f,2f,12.5f),new Vector3(.4f,2.8f,4.58f));
            foreach (Renderer panel in factory.GetComponentsInChildren<Renderer>())
                if (panel.name=="Sidewall weathered concrete inset" && panel.enabled)
                    Assert.IsFalse(opening.Intersects(panel.bounds),"Opaque wall panel hides the PVC doorway");

            GameObject curtain=GameObject.Find("V11_BC_PVC_CURTAIN");
            Assert.IsNotNull(curtain);
            Assert.IsEmpty(curtain.GetComponentsInChildren<Collider>(true),"PVC must never block the player or feed");
            Renderer[] strips=curtain.GetComponentsInChildren<Renderer>()
                .Where(v=>v.name.StartsWith("PVC_STRIP_")).OrderBy(v=>v.bounds.center.z).ToArray();
            Assert.AreEqual(20,strips.Length,"Missing or duplicate PVC strips");
            Assert.That(strips[0].bounds.min.z,Is.EqualTo(near.bounds.max.z).Within(.02f));
            Assert.That(strips[strips.Length-1].bounds.max.z,Is.EqualTo(far.bounds.min.z).Within(.02f));
            for (int i=0;i<strips.Length;i++)
            {
                Renderer strip=strips[i];
                Assert.IsTrue(strip.enabled,strip.name);
                Assert.That(strip.bounds.center.x,Is.EqualTo(-.3f).Within(.02f));
                Assert.Greater(strip.bounds.min.y,floor.bounds.max.y);
                Assert.That(strip.bounds.max.y,Is.EqualTo(lintel.bounds.min.y).Within(.01f));
                Assert.Less(strip.bounds.size.x,.03f,"PVC is a thin vertical strip");
                if (i>0) Assert.Greater(strips[i-1].bounds.max.z,strip.bounds.min.z,"PVC strips must overlap");
                Material material=strip.sharedMaterial;
                Assert.IsNotNull(material);
                Assert.IsNotNull(material.shader);
                Assert.AreEqual("Standard",material.shader.name);
                Assert.IsTrue(material.shader.isSupported,"PVC shader is unsupported");
                Assert.That(material.color.a,Is.InRange(.15f,.4f));
                Assert.That(material.GetFloat("_Mode"),Is.EqualTo(3f));
                Assert.That(material.GetInt("_ZWrite"),Is.EqualTo(0));
                Assert.AreEqual((int)UnityEngine.Rendering.BlendMode.One,material.GetInt("_SrcBlend"));
                Assert.AreEqual((int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha,material.GetInt("_DstBlend"));
                Assert.IsTrue(material.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON"));
                Assert.AreEqual((int)UnityEngine.Rendering.RenderQueue.Transparent,material.renderQueue);
                MeshFilter mesh=strip.GetComponent<MeshFilter>();
                Assert.IsNotNull(mesh);
                Assert.IsNotNull(mesh.sharedMesh);
                Assert.IsTrue(mesh.sharedMesh.normals.Any(n=>n.x>.9f) &&
                    mesh.sharedMesh.normals.Any(n=>n.x<-.9f),"Strip needs faces visible from both B and C");
            }
        }
        [UnityTest]
        public IEnumerator ReplacementDeckWorldBoundsMatchBothStairExitsAndRearBay()
        {
            yield return Open();
            var west = GameObject.Find("2F_WEST_SERVICE_ROUTE").GetComponent<Collider>();
            var east = GameObject.Find("2F_EAST_SERVICE_ROUTE").GetComponent<Collider>();
            var rear = GameObject.Find("2F_CONNECTING_REAR_ROUTE").GetComponent<Collider>();
            Assert.That(west.bounds.min.x, Is.EqualTo(-24.1f).Within(.01f));
            Assert.That(west.bounds.max.x, Is.EqualTo(-22.3f).Within(.01f));
            Assert.That(east.bounds.min.x, Is.EqualTo(-2.45f).Within(.01f));
            Assert.That(east.bounds.max.x, Is.EqualTo(-.65f).Within(.01f));
            foreach (Collider side in new[] { west, east })
            {
                Assert.That(side.bounds.min.z, Is.EqualTo(13.7f).Within(.01f));
                Assert.That(side.bounds.max.z, Is.EqualTo(rear.bounds.min.z).Within(.01f));
                Assert.That(side.bounds.max.y, Is.EqualTo(4.5f).Within(.01f));
            }
            Assert.That(rear.bounds.min.z, Is.EqualTo(18.1f).Within(.01f));
            Assert.That(rear.bounds.max.z, Is.EqualTo(21.3f).Within(.01f));
        }
        [UnityTest]
        public IEnumerator PlayerCanClimbRightStairAndReachRearMaintenanceBridge()
        {
            yield return Open();
            GameObject player = GameObject.Find("M2_Player");
            CharacterController cc = player.GetComponent<CharacterController>();
            foreach (MonoBehaviour behaviour in player.GetComponents<MonoBehaviour>())
                if (behaviour && behaviour.GetType().Name == "C3FirstPersonController") behaviour.enabled = false;
            cc.enabled = false;
            player.transform.position = new Vector3(-1.55f, .60f, 3.1f);
            cc.enabled = true;
            Physics.SyncTransforms();
            // Production capsule dimensions, not a smaller/slope-relaxed test walker.
            Vector3[] route = { new Vector3(-1.55f,4.5f,14.2f),
                new Vector3(-1.55f,4.5f,19.2f), new Vector3(-13.2f,4.5f,19.2f) };
            foreach (Vector3 goal in route)
            {
                for (int i = 0; i < 600; i++)
                {
                    Vector3 delta = goal - player.transform.position;
                    delta.y = 0f;
                    if (delta.magnitude < .08f) break;
                    cc.Move(delta.normalized * .04f + Vector3.down * .025f);
                    yield return null;
                }
                Assert.Less(Vector2.Distance(new Vector2(player.transform.position.x, player.transform.position.z),
                    new Vector2(goal.x, goal.z)), .15f, "Blocked before " + goal);
                Assert.Greater(player.transform.position.y, 4.2f, "Fell on upper route");
            }
        }
        [UnityTest]
        public IEnumerator PriorFloorStripeIsPassable()
        {
            yield return Open();
            GameObject walker=new GameObject("V9_TEST_STRIPE_PASSAGE");
            CharacterController cc=walker.AddComponent<CharacterController>();
            cc.height=1.80f;cc.center=Vector3.up*.90f;cc.radius=.27f;
            cc.stepOffset=.30f;cc.slopeLimit=54f;
            walker.transform.position=new Vector3(-5.14f,.70f,9.82f);
            for(int i=0;i<120;i++){cc.Move(new Vector3(0f,-.055f,-.043f));yield return null;}
            Assert.Less(walker.transform.position.z,5.6f);
            Object.Destroy(walker);
        }
    }
}
