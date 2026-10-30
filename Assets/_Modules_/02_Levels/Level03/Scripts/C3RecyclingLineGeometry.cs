using System.Collections.Generic;
using UnityEngine;

namespace C3TreasurePuzzle
{
    [DisallowMultipleComponent]
    public sealed class C3RecyclingLineGeometry : MonoBehaviour
    {
        public const string RootName="C3_P2_RecyclingLine";
        public const float RawRadius=.12f;
        public sealed class Machine
        {
            public int Index;
            public Transform Root,Intake,Tray,Output,Collection,Press;
            public Renderer Indicator;
            public BoxCollider ProcessBed;
            public C3ConveyorSection Chute,IntakeRamp,InternalOutput,Outfeed,CollectionBed;
            public readonly List<C3ConveyorSection> InputSections=new List<C3ConveyorSection>();
            public readonly List<C3ConveyorSection> OutputSections=new List<C3ConveyorSection>();
        }
        public Transform BuildRoot { get; private set; }
        public Transform BMaterialInterface { get; private set; }
        public Transform MainEntry { get; private set; }
        public C3ConveyorSection MainBelt { get; private set; }
        public readonly List<C3ConveyorSection> CommonSections=new List<C3ConveyorSection>();
        public readonly List<C3ConveyorSection> Sections=new List<C3ConveyorSection>();
        public readonly List<Machine> Machines=new List<Machine>();
        private readonly List<Material> materials=new List<Material>();
        private readonly List<Transform> rollers=new List<Transform>();
        private Material steel,rubber,yellow,glass;
        private Material[] types;
        private readonly MaterialPropertyBlock indicatorBlock=new MaterialPropertyBlock();

        private void Awake()
        {
            if (transform.Find(RootName)) return;
            BuildRoot=new GameObject(RootName).transform;
            BuildRoot.SetParent(transform,true);
            BuildRoot.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            steel=Material("P2 galvanized steel",new Color(.45f,.54f,.58f),.65f);
            rubber=Material("P2 conveyor rubber",new Color(.13f,.17f,.18f),.05f);
            yellow=Material("P2 machine safety",new Color(.98f,.65f,.08f),.25f);
            glass=Material("P2 scanner lens",new Color(.12f,.72f,.83f),.1f);
            types=new[] { Material("Paper",new Color(.72f,.51f,.29f),0f),
                Material("Metal",new Color(.59f,.68f,.74f),.7f),
                Material("Plastic",new Color(.12f,.65f,.75f),.05f) };
            BuildElevatedInfeed();
            BuildMachines();
        }

        private Material Material(string name,Color color,float metallic)
        {
            Material mat=new Material(Shader.Find("Standard")) { name=name,color=color };
            mat.SetFloat("_Metallic",metallic); mat.SetFloat("_Glossiness",.38f);
            materials.Add(mat); return mat;
        }
        private Transform Marker(string name,Vector3 world,Transform parent=null)
        {
            Transform t=new GameObject(name).transform;
            t.SetParent(parent?parent:BuildRoot,false); t.position=world;
            return t;
        }
        private GameObject Box(string name,Vector3 position,Vector3 size,Material mat,
            Transform parent=null,bool solid=true,Quaternion? rotation=null)
        {
            GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name=name; g.transform.SetParent(parent?parent:BuildRoot,false);
            g.transform.SetPositionAndRotation(position,rotation??Quaternion.identity);
            g.transform.localScale=size; g.GetComponent<Renderer>().sharedMaterial=mat;
            if (!solid) { Collider c=g.GetComponent<Collider>(); c.enabled=false; Destroy(c); }
            return g;
        }
        private C3ConveyorSection Section(string name,Vector3 a,Vector3 b,float width,Transform parent=null)
        {
            Transform sectionRoot=Marker(name,Vector3.zero,parent);
            C3ConveyorSection section=sectionRoot.gameObject.AddComponent<C3ConveyorSection>();
            Vector3 direction=b-a;
            Quaternion rotation=Quaternion.LookRotation(direction.normalized,Vector3.up);
            Vector3 normal=rotation*Vector3.up,side=rotation*Vector3.right;
            GameObject surface=Box("BELT_SURFACE",(a+b)*.5f-normal*.09f,
                new Vector3(width,.18f,direction.magnitude+.12f),rubber,sectionRoot,true,rotation);
            section.Configure(Marker("START",a,sectionRoot),Marker("END",b,sectionRoot),
                surface.GetComponent<BoxCollider>(),width);
            foreach (int sign in new[] {-1,1})
            {
                Box("SIDE_GUARD",(a+b)*.5f+side*(sign*(width*.5f+.035f))+normal*.14f,
                    new Vector3(.07f,.40f,Mathf.Max(.08f,direction.magnitude-width-.2f)),steel,sectionRoot,true,rotation);
                Box("YELLOW_EDGE",(a+b)*.5f+side*(sign*(width*.5f+.035f))+normal*.35f,
                    new Vector3(.075f,.035f,Mathf.Max(.08f,direction.magnitude-width-.2f)),yellow,sectionRoot,false,rotation);
                Box("STEEL_UNDERFRAME",(a+b)*.5f+side*(sign*width*.40f)-normal*.26f,
                    new Vector3(.09f,.14f,direction.magnitude),steel,sectionRoot,true,rotation);
            }
            int spans=Mathf.Max(2,Mathf.CeilToInt(direction.magnitude/.6f));
            for (int i=0;i<=spans;i++)
            {
                GameObject roller=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                roller.name="ROLLER"; roller.transform.SetParent(sectionRoot,false);
                roller.transform.SetPositionAndRotation(Vector3.Lerp(a,b,i/(float)spans)-normal*.15f,
                    rotation*Quaternion.Euler(0f,0f,90f));
                roller.transform.localScale=new Vector3(.14f,width*.46f,.14f);
                roller.GetComponent<Renderer>().sharedMaterial=steel;
                Collider c=roller.GetComponent<Collider>(); c.enabled=false; Destroy(c);
                rollers.Add(roller.transform);
            }
            Sections.Add(section); return section;
        }

        private void BuildElevatedInfeed()
        {
            Vector3[] nodes={new Vector3(1.2f,7f,12.5f),new Vector3(-.9f,7f,12.5f),
                new Vector3(-5f,7f,12.5f),new Vector3(-5f,7f,16.85f),new Vector3(-8.1f,5.8f,16.85f)};
            BMaterialInterface=Marker("P2_B_HIGH_INFEED_INTERFACE",nodes[0]);
            MainEntry=Marker("P2_MAIN_ENTRY",nodes[nodes.Length-1]);
            for (int i=0;i<nodes.Length-1;i++) CommonSections.Add(Section("P2_INFEED_"+i,nodes[i],nodes[i+1],1.1f));
            MainBelt=Section("P2_MAIN_BELT",nodes[nodes.Length-1],new Vector3(-19.5f,5.8f,16.85f),1.52f);
            // Roof hangers keep the staircase and ground aisle clear; no floor
            // columns below the crossing of the east stair or the PVC doorway.
            foreach (Vector3 anchor in new[] {new Vector3(.8f,7f,12.5f),new Vector3(-4.5f,7f,12.5f),new Vector3(-5f,7f,16.85f)})
                foreach (float side in new[] {-.66f,.66f})
                    Box("P2_ROOF_HANGER",new Vector3(anchor.x,8.9f,anchor.z+side),new Vector3(.10f,3.8f,.10f),steel);
            // New serialized proxy pieces preserve the rest of the original wall.
            Box("P2_LINTEL_LOWER_CLADDING",new Vector3(-.3f,5f,12.5f),new Vector3(.16f,3f,5f),steel,solid:false);
            foreach (float z in new[] {11.60f,13.40f})
                Box("P2_INFEED_JAMB",new Vector3(-.3f,7.30f,z),new Vector3(.28f,1.7f,.10f),yellow);
            foreach (float y in new[] {6.45f,8.15f})
                Box("P2_INFEED_HEADER",new Vector3(-.3f,y,12.5f),new Vector3(.28f,.10f,1.9f),yellow);
            Marker("P2_APERTURE_CENTER",new Vector3(-.3f,7.30f,12.5f));
        }

        private void BuildMachines()
        {
            float[] centers={-17.9f,-13f,-8.1f};
            string[] signs={"PaperHall","MetalHall","PlasticHall"};
            for (int lane=0;lane<centers.Length;lane++)
            {
                float x=centers[lane];
                Machine machine=new Machine {Index=lane,Root=Marker("SORTER_MAIN_BODY_"+lane,new Vector3(x,.55f,14.8f))};
                // Hollow casing: actual roof inlet and front output, not a solid
                // cube through which animated material would have to teleport.
                BoxCollider baseCollider=machine.Root.gameObject.AddComponent<BoxCollider>();
                baseCollider.center=new Vector3(0f,.1f,0f); baseCollider.size=new Vector3(4.4f,.2f,3.9f);
                Box("BASE",new Vector3(x,.65f,14.8f),baseCollider.size,steel,machine.Root,false);
                foreach (int sign in new[] {-1,1})
                    Box("MACHINE_SIDE",new Vector3(x+sign*2.12f,2.48f,14.8f),new Vector3(.16f,3.66f,3.9f),steel,machine.Root);
                Box("MACHINE_BACK",new Vector3(x,2.48f,16.68f),new Vector3(4.4f,3.66f,.14f),steel,machine.Root);
                foreach (int sign in new[] {-1,1})
                    Box("FRONT_COLUMN",new Vector3(x+sign*1.65f,2.48f,12.92f),new Vector3(1.05f,3.66f,.14f),steel,machine.Root);
                Box("FRONT_TOP",new Vector3(x,3.7f,12.92f),new Vector3(2.25f,1.22f,.14f),types[lane],machine.Root);
                GameObject label=C3BakedSignFactory.AtWorld(signs[lane],new Vector3(x,3.70f,12.82f),Quaternion.identity,2.05f,.55f,false);
                if (label) { label.transform.SetParent(machine.Root,true); OwnSignMaterials(label); }
                machine.Indicator=Box("PROCESS_INDICATOR",new Vector3(x+1.64f,3.7f,12.80f),new Vector3(.17f,.17f,.06f),glass,machine.Root,false).GetComponent<Renderer>();
                Vector3 main=new Vector3(x,5.8f,16.85f),intake=new Vector3(x,4.45f,15.8f);
                Vector3 tray=new Vector3(x,2.4f,15.2f),output=new Vector3(x,1.25f,12.75f),collection=new Vector3(x,1.25f,9.1f);
                machine.Intake=Marker("MACHINE_INPUT",intake,machine.Root);
                machine.Tray=Marker("PROCESS_TRAY",tray,machine.Root);
                machine.Output=Marker("MACHINE_OUTPUT",output,machine.Root);
                machine.Collection=Marker("FINISHED_COLLECTION",collection,machine.Root);
                machine.Chute=Section("V3_CONNECTED_DIVERT_TO_HOPPER_"+lane,main,intake,1.25f);
                machine.IntakeRamp=Section("P2_INTAKE_RAMP_"+lane,intake,tray,1.25f);
                machine.InputSections.Add(machine.Chute); machine.InputSections.Add(machine.IntakeRamp);
                machine.InternalOutput=Section("P2_INTERNAL_OUTPUT_"+lane,tray,output,1.55f);
                machine.Outfeed=Section("CONNECTED_LOWER_OUTFEED_"+lane,output,new Vector3(x,1.25f,9.8f),1.55f);
                machine.CollectionBed=Section("P2_COLLECTION_BED_"+lane,machine.Outfeed.End.position,collection,2.4f);
                machine.OutputSections.Add(machine.InternalOutput); machine.OutputSections.Add(machine.Outfeed); machine.OutputSections.Add(machine.CollectionBed);
                machine.ProcessBed=Box("PROCESS_BED",tray-Vector3.up*.09f,new Vector3(1.5f,.18f,1.0f),steel,machine.Root).GetComponent<BoxCollider>();
                machine.Press=Box("COMPRESSION_PRESS",tray+Vector3.up*1.25f,new Vector3(1.25f,.22f,.85f),yellow,machine.Root,false).transform;
                for (int leg=0;leg<2;leg++)
                    Box("OUTPUT_LEG",new Vector3(x+(leg==0?-.65f:.65f),.8f,10.1f),new Vector3(.12f,.5f,.12f),steel,machine.Root);
                Box("EMPTY_COLLECTION_PALLET",new Vector3(x,1.1f,9.1f),new Vector3(2.5f,.22f,1.4f),steel,machine.Root);
                Machines.Add(machine);
            }
        }
        private void OwnSignMaterials(GameObject sign)
        {
            foreach (Renderer r in sign.GetComponentsInChildren<Renderer>())
                foreach (Material m in r.sharedMaterials) if (m && !materials.Contains(m)) materials.Add(m);
        }
        public Material MaterialFor(int machine) => types[machine];
        public void AnimateRollers(float dt)
        {
            foreach (Transform roller in rollers) roller.Rotate(Vector3.up,180f*dt,Space.Self);
        }
        public void SetPress(int machine,float compression)
        {
            Machine m=Machines[machine];
            m.Press.position=m.Tray.position+Vector3.up*Mathf.Lerp(1.25f,.42f,compression);
            indicatorBlock.SetColor("_Color",compression>0f?new Color(.1f,.95f,.6f):new Color(.12f,.3f,.32f));
            m.Indicator.SetPropertyBlock(indicatorBlock);
        }
        public bool ConnectionsMatch(float tolerance)
        {
            for (int i=0;i<CommonSections.Count-1;i++)
                if (Vector3.Distance(CommonSections[i].End.position,CommonSections[i+1].Start.position)>tolerance) return false;
            if (Vector3.Distance(CommonSections[CommonSections.Count-1].End.position,MainBelt.Start.position)>tolerance) return false;
            foreach (Machine m in Machines)
            {
                if (Vector3.Distance(m.Chute.End.position,m.IntakeRamp.Start.position)>tolerance) return false;
                if (Vector3.Distance(m.InternalOutput.End.position,m.Outfeed.Start.position)>tolerance) return false;
                if (Vector3.Distance(m.Outfeed.End.position,m.CollectionBed.Start.position)>tolerance) return false;
            }
            foreach (C3ConveyorSection s in Sections)
                if (!s.Surface.enabled || !s.SurfaceRenderer.enabled || s.Surface.bounds.size==Vector3.zero) return false;
            return true;
        }
        private void OnDestroy()
        {
            if (BuildRoot) { BuildRoot.gameObject.SetActive(false); Destroy(BuildRoot.gameObject); }
            foreach (Material material in materials) if (material) Destroy(material);
        }
    }
}
