using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
using static M6IntegrationTests.C3V11Phase2RecyclingLineTests;

namespace M6IntegrationTests
{
    internal static class C3Phase21TestDriver
    {
        internal static object Kind(string kind) => Enum.Parse(GameType("EcoRecycleCargo+Kind"),kind);
        internal static void Tick(Component line,int count=1)
        {
            for (int i=0;i<count;i++) Call(line,"AdvanceSimulation",.05f);
            Physics.SyncTransforms();
        }
        internal static void Activate(Component device)
        {
            Component progress=Find("C3TreasurePuzzle.C3TreasureProgress");
            foreach (string id in new[] {"fuse","control_chip","filter_module"}) Assert.IsTrue((bool)Call(progress,"TryCollect",id,id));
            Type step=GameType("C3TreasurePuzzle.C3ProcessingStep"); Array answer=Array.CreateInstance(step,3);
            string[] names={"Sorting","Recycling","PollutionTreatment"};
            for (int i=0;i<3;i++) answer.SetValue(Enum.Parse(step,names[i]),i);
            Assert.IsTrue((bool)Call(Find("C3TreasurePuzzle.C3PuzzleState"),"SubmitAnswer",answer));
            Assert.IsTrue((bool)Call(device,"TryActivate")); Call(device,"NotifyStartupFinished");
        }
        internal static void Supply(Component line,string kind,int units=3,string prefix="art")
        {
            for (int i=0;i<units;i++) Assert.IsTrue((bool)Call(line,"TrySupply",prefix+kind+i,Kind(kind)));
        }
        internal static Bounds LocalEnvelope(Transform root)
        {
            MeshFilter[] meshes=root.GetComponentsInChildren<MeshFilter>(true); Assert.Greater(meshes.Length,0);
            Bounds box=new Bounds(Vector3.zero,Vector3.zero);
            foreach (MeshFilter mesh in meshes)
                foreach (Vector3 vertex in mesh.sharedMesh.vertices) box.Encapsulate(root.InverseTransformPoint(mesh.transform.TransformPoint(vertex)));
            return box;
        }
    }
    public sealed class C3V11RecyclingArtTests
    {
        private C3V11Phase2RecyclingLineTests scope;
        private Component line,library,device;
        [UnitySetUp] public IEnumerator Open()
        {
            scope=new C3V11Phase2RecyclingLineTests(); yield return scope.Open();
            line=Find("C3TreasurePuzzle.C3RecyclingLine"); library=Find("C3TreasurePuzzle.C3RecyclingVisuals"); device=Find("C3TreasurePuzzle.C3DeviceState");
        }
        [UnityTearDown] public IEnumerator Close() { if (scope!=null) yield return scope.Close(); }
        [UnityTest] public IEnumerator FifteenImportedVariantsUseRealVolumeSharedMaterialsAndSafeProxy()
        {
            GameObject owner=new GameObject("P21_TEST_IMPORTED_ART"); owner.transform.position=Vector3.one*128f;
            try
            {
                foreach (string kind in new[] {"Metal","Plastic","Paper"})
                {
                    Material shared=null;
                    for (int variant=0;variant<5;variant++)
                    {
                        Transform visual=(Transform)Call(library,"CreateRaw",C3Phase21TestDriver.Kind(kind),variant,owner.transform);
                        MeshFilter[] meshes=visual.GetComponentsInChildren<MeshFilter>(); Assert.GreaterOrEqual(meshes.Length,2);
                        foreach (MeshFilter mesh in meshes)
                        {
                            Assert.IsTrue(mesh.sharedMesh.isReadable); Assert.GreaterOrEqual(mesh.sharedMesh.triangles.Length,36);
                            Assert.Greater(mesh.sharedMesh.bounds.size.x*mesh.sharedMesh.bounds.size.y*mesh.sharedMesh.bounds.size.z,0f);
                            Assert.AreEqual(mesh.sharedMesh.vertexCount,mesh.sharedMesh.uv.Length);
                            Assert.IsTrue(mesh.sharedMesh.normals.All(n=>n.sqrMagnitude>.5f));
                            foreach (Vector3 vertex in mesh.sharedMesh.vertices)
                            {
                                Vector3 p=visual.InverseTransformPoint(mesh.transform.TransformPoint(vertex));
                                Assert.IsFalse(float.IsNaN(p.sqrMagnitude)); Assert.LessOrEqual(p.magnitude,.12001f);
                            }
                            Material material=mesh.GetComponent<Renderer>().sharedMaterial;
                            Assert.IsNotNull(material.mainTexture); Assert.IsTrue(material.shader.isSupported);
                            Assert.AreEqual("Standard",material.shader.name); Assert.AreEqual(0f,material.GetFloat("_Mode"));
                            if (shared) Assert.AreSame(shared,material); else shared=material;
                        }
                        Assert.AreEqual(0,visual.GetComponentsInChildren<Collider>().Length);
                        Assert.IsTrue((bool)GameType("C3TreasurePuzzle.C3RecyclingVisuals").GetMethod("FitsRawEnvelope").Invoke(null,new object[] {owner.transform}));
                        Object.DestroyImmediate(visual.gameObject);
                    }
                }
            }
            finally { Object.DestroyImmediate(owner); }
            yield return null;
        }
        [UnityTest] public IEnumerator OfficialNineInputsKeepTypesIdsAndDeterministicVisualVariety()
        {
            foreach (string kind in new[] {"Metal","Plastic","Paper"}) C3Phase21TestDriver.Supply(line,kind);
            object[] records=Items(line,"Materials"); Assert.AreEqual(9,records.Length);
            Assert.AreEqual(9,records.Select(r=>Member(r,"MaterialId")).Distinct().Count());
            foreach (string kind in new[] {"Metal","Plastic","Paper"})
            {
                object[] lane=records.Where(r=>Member(r,"MaterialType").ToString()==kind).ToArray(); Assert.AreEqual(3,lane.Length);
                Assert.AreEqual(3,lane.Select(r=>((Transform)Member(r,"Visual")).GetChild(0).name).Distinct().Count());
                foreach (object record in lane)
                {
                    Component cargo=(Component)Member(record,"Cargo"); Transform visual=(Transform)Member(record,"Visual");
                    Assert.AreEqual("RAW_MATERIAL_VISUAL_ROOT",visual.name); Assert.AreSame(cargo.transform,visual.parent);
                    Assert.AreEqual(Member(record,"MaterialId"),Member(cargo,"materialId"));
                    Assert.IsTrue(visual.GetChild(0).name.StartsWith(kind+"_"));
                }
            }
            Assert.IsFalse((bool)Call(line,"TrySupply","unsupported-glass-placeholder",C3Phase21TestDriver.Kind("Electronics")));
            Assert.AreEqual(9,Items(line,"Materials").Length);
            yield return null;
        }
        [UnityTest] public IEnumerator MultiMeshBottleCapsAndBodiesCompressTogether()
        {
            C3Phase21TestDriver.Supply(line,"Plastic"); C3Phase21TestDriver.Activate(device);
            for (int i=0;i<1200 && (int)Member(line,"ProcessingUnits")<3;i++) { C3Phase21TestDriver.Tick(line); yield return null; }
            Assert.AreEqual(3,Member(line,"ProcessingUnits"));
            Transform visual=(Transform)Member(Items(line,"Materials")[0],"Visual");
            Renderer[] parts=visual.GetComponentsInChildren<Renderer>(); Assert.GreaterOrEqual(parts.Length,2);
            float scale=visual.localScale.y; float[] heights=parts.Select(r=>r.bounds.size.y).ToArray();
            C3Phase21TestDriver.Tick(line,10);
            Assert.Less(visual.localScale.y,scale);
            for (int i=0;i<parts.Length;i++)
                Assert.That(parts[i].bounds.size.y,Is.EqualTo(heights[i]*visual.localScale.y/scale).Within(.0003f),parts[i].name+" left behind by compression");
            Assert.AreEqual(0,Items(line,"Products").Length,"No early bale during the 2.5s press cycle");
        }
        [UnityTest] public IEnumerator ExternalCargoRetainsStrictEnvelopeAndWholeVisualRoot()
        {
            GameObject external=new GameObject("P21_TEST_EXTERNAL_MULTIMESH");
            external.transform.position=((Transform)Member(Find("C3TreasurePuzzle.C3RecyclingLineGeometry"),"BMaterialInterface")).position+Vector3.up*.12f;
            Component cargo=external.AddComponent(GameType("EcoRecycleCargo"));
            cargo.GetType().GetField("materialId").SetValue(cargo,"external-art"); cargo.GetType().GetField("materialType").SetValue(cargo,C3Phase21TestDriver.Kind("Metal"));
            bool accepted=false;
            try
            {
                for (int i=0;i<2;i++)
                {
                    GameObject part=GameObject.CreatePrimitive(PrimitiveType.Cube); part.transform.SetParent(external.transform,false);
                    part.transform.localScale=Vector3.one*.06f; part.transform.localPosition=Vector3.right*(i==0?-.035f:.035f);
                    part.GetComponent<Collider>().isTrigger=true;
                }
                Transform first=external.transform.GetChild(0);
                first.localPosition=Vector3.right*.3f; Physics.SyncTransforms();
                Assert.IsFalse((bool)Call(line,"TryAcceptCargo",cargo),"Offset tiny parts bypassed the aggregate proxy");
                first.localPosition=Vector3.left*.035f; first.GetComponent<Collider>().isTrigger=false; Physics.SyncTransforms();
                Assert.IsFalse((bool)Call(line,"TryAcceptCargo",cargo)); Assert.IsTrue(first.GetComponent<Collider>().enabled,"Rejected solid collider changed");
                first.GetComponent<Collider>().isTrigger=true; Physics.SyncTransforms();
                accepted=(bool)Call(line,"TryAcceptCargo",cargo); Assert.IsTrue(accepted);
                Assert.AreSame(cargo,Member(Items(line,"Materials").Single(),"Cargo"));
                Transform visual=(Transform)Member(Items(line,"Materials").Single(),"Visual"); Assert.AreEqual(2,visual.childCount);
            }
            finally { if (!accepted) Object.DestroyImmediate(external); }
            yield return null;
        }
        [UnityTest] public IEnumerator RealBalesRetainProductEnvelopeProvenanceAndCollection()
        {
            foreach (string kind in new[] {"Metal","Plastic","Paper"}) C3Phase21TestDriver.Supply(line,kind);
            C3Phase21TestDriver.Activate(device);
            for (int i=0;i<1600 && (int)Member(line,"CollectedProducts")<3;i++) { C3Phase21TestDriver.Tick(line); yield return null; }
            Assert.AreEqual(3,Member(line,"CollectedProducts"));
            foreach (object batch in Items(line,"Products"))
            {
                Transform product=(Transform)Member(batch,"Product"); Bounds envelope=C3Phase21TestDriver.LocalEnvelope(product);
                Assert.Less(envelope.center.magnitude,.0001f); Assert.Less(Vector3.Distance(envelope.size,new Vector3(.52f,.43f,.51f)),.0002f);
                Assert.AreEqual(3,((string[])Member(batch,"SourceMaterialIds")).Distinct().Count());
                Assert.IsTrue((bool)Member(batch,"OutputReached") && (bool)Member(batch,"Collected"));
                Assert.AreEqual(3,product.GetComponentsInChildren<MeshFilter>().Length);
                string kind=Member(batch,"MaterialType").ToString();
                Assert.IsTrue(product.GetComponentsInChildren<Renderer>().Any(r=>r.sharedMaterial.name=="BaleFront_"+kind));
            }
        }
        [UnityTest] public IEnumerator BackOfMachineStockIsDecorationAndLeavesRearMaintenanceAisle()
        {
            Transform decor=GameObject.Find("P21_STORAGE_DECORATION").transform;
            Assert.AreEqual(3,decor.childCount); Assert.AreEqual(0,Items(line,"Materials").Length); Assert.AreEqual(0,Items(line,"Products").Length);
            Assert.AreEqual(0,decor.GetComponentsInChildren(GameType("EcoRecycleCargo")).Length);
            foreach (Transform load in decor)
            {
                Collider[] colliders=load.GetComponentsInChildren<Collider>(); Assert.AreEqual(2,colliders.Length);
                foreach (Collider c in colliders)
                {
                    Assert.IsTrue(c.enabled && !c.isTrigger); Assert.Greater(c.bounds.min.z,18.3f); Assert.Less(c.bounds.max.z,19.2f);
                }
            }
            CharacterController player=GameObject.Find("M2_Player").GetComponent<CharacterController>();
            for (float x=-20.5f;x<=-6f;x+=.1f)
            {
                Vector3 foot=new Vector3(x,.58f,20f),a=foot+Vector3.up*player.radius,b=foot+Vector3.up*(player.height-player.radius);
                Assert.IsFalse(Physics.OverlapCapsule(a,b,player.radius,~0,QueryTriggerInteraction.Ignore).Any(c=>c.transform.IsChildOf(decor)),"Storage blocks the maintenance aisle");
            }
            Assert.AreEqual(0,Member(Find("EcoAdventureRuntime"),"RecyclingReceiptCount"));
            yield return null;
        }
    }
}
