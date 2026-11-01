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
            public Transform Root,Intake,Tray,Output,Collection,Press,Pusher;
            public Renderer Indicator;
            public BoxCollider ProcessBed;
            public C3ConveyorSection Chute,HopperDrop,IntakeRamp,ReceivingBed,DischargeBed,InternalOutput,Outfeed,CollectionBed;
            public readonly List<C3ConveyorSection> InputSections=new List<C3ConveyorSection>();
            public readonly List<C3ConveyorSection> OutputSections=new List<C3ConveyorSection>();
        }
        public Transform BuildRoot { get; private set; }
        public Transform BMaterialInterface { get; private set; }
        public Transform MainEntry { get; private set; }
        public C3ConveyorSection MainBelt { get; private set; }
        public C3RecyclingVisuals Visuals { get; private set; }
        public readonly List<C3ConveyorSection> CommonSections=new List<C3ConveyorSection>();
        public readonly List<C3ConveyorSection> Sections=new List<C3ConveyorSection>();
        public readonly List<Machine> Machines=new List<Machine>();
        private readonly List<Material> materials=new List<Material>();
        private readonly List<Transform> rollers=new List<Transform>();
        private Material steel,rubber,yellow,glass;
        private Material[] types;
        private MaterialPropertyBlock indicatorBlock;
        private static readonly float[] MachineCenters={-17.9f,-13f,-8.1f};

        private void Awake()
        {
            if (transform.Find(RootName)) return;
            BuildRoot=new GameObject(RootName).transform;
            BuildRoot.SetParent(transform,true);
            BuildRoot.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            indicatorBlock=new MaterialPropertyBlock();
            steel=CreateMaterial("P2 galvanized steel",new Color(.45f,.54f,.58f),.65f);
            rubber=CreateMaterial("P2 conveyor rubber",new Color(.13f,.17f,.18f),.05f);
            yellow=CreateMaterial("P2 machine safety",new Color(.98f,.65f,.08f),.25f);
            glass=CreateMaterial("P2 scanner lens",new Color(.12f,.72f,.83f),.1f);
            types=new[] { CreateMaterial("Paper",new Color(.72f,.51f,.29f),0f),
                CreateMaterial("Metal",new Color(.59f,.68f,.74f),.7f),
                CreateMaterial("Plastic",new Color(.12f,.65f,.75f),.05f) };
            Visuals=GetComponent<C3RecyclingVisuals>();
            if (!Visuals) Visuals=gameObject.AddComponent<C3RecyclingVisuals>();
            BuildElevatedInfeed();
            BuildMachines();
            BuildTerminalGuards();
            BuildStorageDecoration();
            Physics.SyncTransforms();
        }

        private Material CreateMaterial(string name,Color color,float metallic)
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
        private C3ConveyorSection Section(string name,Vector3 a,Vector3 b,float width,Transform parent=null,bool withGuards=true)
        {
            Transform sectionRoot=Marker(name,Vector3.zero,parent);
            C3ConveyorSection section=sectionRoot.gameObject.AddComponent<C3ConveyorSection>();
            Vector3 direction=b-a;
            Quaternion rotation=Quaternion.LookRotation(direction.normalized,Vector3.up);
            Vector3 normal=rotation*Vector3.up,side=rotation*Vector3.right;
            GameObject surface=Box("BELT_SURFACE",(a+b)*.5f-normal*.09f,
                new Vector3(width,.18f,direction.magnitude),rubber,sectionRoot,true,rotation);
            section.Configure(Marker("START",a,sectionRoot),Marker("END",b,sectionRoot),
                surface.GetComponent<BoxCollider>(),width);
            foreach (int sign in new[] {-1,1})
            {
                if (withGuards && !(name=="P2_MAIN_BELT" && sign<0))
                {
                    Box("SIDE_GUARD",(a+b)*.5f+side*(sign*(width*.5f+.035f))+normal*.14f,
                        new Vector3(.07f,.40f,Mathf.Max(.08f,direction.magnitude-width-.2f)),steel,sectionRoot,true,rotation);
                    Box("YELLOW_EDGE",(a+b)*.5f+side*(sign*(width*.5f+.035f))+normal*.35f,
                        new Vector3(.075f,.035f,Mathf.Max(.08f,direction.magnitude-width-.2f)),yellow,sectionRoot,false,rotation);
                }
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
                new Vector3(-3.8f,7f,12.5f),new Vector3(-3.8f,7f,16.85f),
                new Vector3(-4.8f,7f,16.85f),new Vector3(-6.2f,5.8f,16.85f)};
            BMaterialInterface=Marker("P2_B_HIGH_INFEED_INTERFACE",nodes[0]);
            MainEntry=Marker("P2_MAIN_ENTRY",nodes[nodes.Length-1]);
            for (int i=0;i<nodes.Length-1;i++) CommonSections.Add(Section("P2_INFEED_"+i,nodes[i],nodes[i+1],1.1f));
            BuildInfeedCorner(CommonSections[1],CommonSections[2],1);
            BuildInfeedCorner(CommonSections[2],CommonSections[3],2);
            MainBelt=Section("P2_MAIN_BELT",nodes[nodes.Length-1],new Vector3(-19.5f,5.8f,16.85f),1.52f);
            float edge=MainBelt.End.position.x;
            foreach (float center in MachineCenters)
            {
                float gap=center+1.2f;
                MainFrontGuard(edge,gap-.7f);
                edge=gap+.7f;
            }
            MainFrontGuard(edge,MainBelt.Start.position.x);
            // Roof hangers keep the staircase and ground aisle clear; no floor
            // columns below the crossing of the east stair or the PVC doorway.
            Vector3[] anchors={new Vector3(.8f,7f,12.5f),new Vector3(-2.8f,7f,12.5f),new Vector3(-3.8f,7f,16f)};
            for (int i=0;i<anchors.Length;i++)
            {
                Vector3 sideAxis=i<2?Vector3.forward:Vector3.right;
                foreach (float side in new[] {-.66f,.66f})
                    Box("P2_ROOF_HANGER",anchors[i]+sideAxis*side+Vector3.up*2.225f,new Vector3(.10f,4.45f,.10f),steel);
                Box("P2_ROOF_TIE",new Vector3(anchors[i].x,11.45f,anchors[i].z),
                    i<2?new Vector3(.16f,.20f,1.8f):new Vector3(1.8f,.20f,.16f),steel);
            }
            Box("P2_B_SIDE_CANTILEVER",new Vector3(.34f,11.45f,12.25f),new Vector3(1.08f,.20f,.20f),steel);
            foreach (float x in new[] {-8.5f,-10.5f,-15.4f,-19.3f})
            {
                foreach (float side in new[] {-.88f,.88f})
                    Box("P2_MAIN_ROOF_HANGER",new Vector3(x,8.625f,16.85f+side),new Vector3(.10f,5.65f,.10f),steel);
                Box("P2_MAIN_ROOF_TIE",new Vector3(x,11.45f,16.85f),new Vector3(.16f,.20f,2.14f),steel);
            }
            // New serialized proxy pieces preserve the rest of the original wall.
            Box("P2_LINTEL_LOWER_CLADDING",new Vector3(-.3f,5f,12.5f),new Vector3(.16f,3f,5f),steel,solid:false);
            foreach (float z in new[] {11.60f,13.40f})
                Box("P2_INFEED_JAMB",new Vector3(-.3f,7.30f,z),new Vector3(.28f,1.7f,.10f),yellow);
            foreach (float y in new[] {6.45f,8.15f})
                Box("P2_INFEED_HEADER",new Vector3(-.3f,y,12.5f),new Vector3(.28f,.10f,1.9f),yellow);
            Marker("P2_APERTURE_CENTER",new Vector3(-.3f,7.30f,12.5f));
        }

        private void MemberBetween(string name,Vector3 a,Vector3 b,float width,float height,Material material,Transform parent,bool solid=true)
        {
            Vector3 delta=b-a;
            if (delta.sqrMagnitude<.000001f) return;
            Box(name,(a+b)*.5f,new Vector3(width,height,delta.magnitude),material,parent,solid,Quaternion.LookRotation(delta,Vector3.up));
        }

        private void BuildInfeedCorner(C3ConveyorSection incoming,C3ConveyorSection outgoing,int index)
        {
            // The verified 30 finite surfaces and all path endpoints stay intact.
            // Complete the perimeter of their rectangular union, never close an
            // entrance/exit with an end plate or put a post on the cargo centreline.
            Vector3 joint=incoming.End.position;
            Vector3 before=(incoming.End.position-incoming.Start.position).normalized;
            Vector3 after=(outgoing.End.position-outgoing.Start.position).normalized;
            Vector3 sideA=Vector3.Cross(Vector3.up,before),sideB=Vector3.Cross(Vector3.up,after);
            float outer=-Mathf.Sign(Vector3.Dot(after,sideA));
            Transform root=Marker("P21_INFEED_JUNCTION_"+index,Vector3.zero);
            float offset=incoming.Width*.5f+.035f,cut=incoming.Width*.5f+.1f;
            foreach (float sign in new[] {outer,-outer})
            {
                string edge=sign==outer?"OUTER":"INNER";
                Vector3 corner=joint+(sideA+sideB)*(sign*offset);
                Vector3 a=joint-before*cut+sideA*(sign*offset),b=joint+after*cut+sideB*(sign*offset);
                MemberBetween(edge+"_GUARD_IN",a+Vector3.up*.14f,corner+Vector3.up*.14f,.07f,.40f,steel,root);
                MemberBetween(edge+"_GUARD_OUT",corner+Vector3.up*.14f,b+Vector3.up*.14f,.07f,.40f,steel,root);
                MemberBetween(edge+"_WARNING_IN",a+Vector3.up*.35f,corner+Vector3.up*.35f,.075f,.035f,yellow,root,false);
                MemberBetween(edge+"_WARNING_OUT",corner+Vector3.up*.35f,b+Vector3.up*.35f,.075f,.035f,yellow,root,false);
                float frameOffset=incoming.Width*.40f;
                Vector3 frameCorner=joint+(sideA+sideB)*(sign*frameOffset)-Vector3.up*.26f;
                MemberBetween(edge+"_FRAME_IN",joint-before*cut+sideA*(sign*frameOffset)-Vector3.up*.26f,
                    frameCorner,.09f,.14f,steel,root);
                MemberBetween(edge+"_FRAME_OUT",frameCorner,joint+after*cut+sideB*(sign*frameOffset)-Vector3.up*.26f,
                    .09f,.14f,steel,root);
            }
            Vector3 anchor=joint+(sideA+sideB)*(outer*.66f);
            Box("CORNER_ROOF_HANGER",new Vector3(anchor.x,9.06f,anchor.z),new Vector3(.10f,4.78f,.10f),steel,root);
            Vector3 saddle=joint+(sideA+sideB)*(outer*.44f)-Vector3.up*.26f;
            MemberBetween("CORNER_HANGER_SADDLE",new Vector3(anchor.x,6.74f,anchor.z),saddle,.14f,.14f,steel,root);
            MemberBetween("CORNER_ROOF_TIE",new Vector3(anchor.x,11.45f,anchor.z),new Vector3(joint.x,11.45f,joint.z),.18f,.20f,steel,root);
            // End cheeks are below the belt, with visible fasteners; no cap
            // projects above the finite surface where material must turn.
            foreach (Vector3 axis in new[] {sideA,sideB})
                MemberBetween("BOLTED_UNDERSIDE_CROSSMEMBER",joint-axis*.61f-Vector3.up*.36f,
                    joint+axis*.61f-Vector3.up*.36f,.10f,.12f,steel,root);
        }

        private void BuildTerminalGuards()
        {
            // The paper diversion is at X=-16.7. No supported material travels
            // beyond it to this true main-belt dead end at X=-19.5.
            TerminalGuard(MainBelt,"P21_MAIN_TERMINAL",.12f,.52f);
            foreach (Machine machine in Machines)
                TerminalGuard(machine.CollectionBed,"P21_COLLECTION_TERMINAL_"+machine.Index,ProductHalfLength+.12f,.36f);
        }
        private const float ProductHalfLength=.255f; // ProductSize.z / 2; keep the collection centre unchanged
        private void BuildStorageDecoration()
        {
            Transform root=Marker("P21_STORAGE_DECORATION",Vector3.zero);
            EcoRecycleCargo.Kind[] kinds={EcoRecycleCargo.Kind.Paper,EcoRecycleCargo.Kind.Metal,EcoRecycleCargo.Kind.Plastic};
            for (int lane=0;lane<Machines.Count;lane++)
                Visuals.CreateStorage(kinds[lane],root,new Vector3(MachineCenters[lane],.56f,18.75f));
        }
        private void TerminalGuard(C3ConveyorSection section,string name,float setback,float height)
        {
            Vector3 forward=(section.End.position-section.Start.position).normalized,up=section.Normal;
            Quaternion rotation=Quaternion.LookRotation(forward,up);
            Transform root=Marker(name,Vector3.zero);
            Vector3 center=section.End.position+forward*setback+up*(height*.5f);
            Box("END_STOP",center,new Vector3(section.Width+.14f,height,.10f),steel,root,true,rotation);
            Box("YELLOW_WARNING_EDGE",center+up*(height*.5f-.025f),new Vector3(section.Width+.16f,.05f,.12f),yellow,root,false,rotation);
            foreach (float sign in new[] {-1f,1f})
                Box("END_STOP_MOUNT",center+(rotation*Vector3.right)*(sign*section.Width*.40f)-up*(height*.5f+.12f),
                    new Vector3(.13f,.24f,.24f),steel,root,true,rotation);
        }

        private void MainFrontGuard(float from,float to)
        {
            if (to-from<.01f) return;
            Vector3 center=new Vector3((from+to)*.5f,5.94f,16.055f);
            Box("MAIN_FRONT_GUARD_BETWEEN_GATES",center,new Vector3(to-from,.40f,.07f),steel,MainBelt.transform);
            Box("MAIN_FRONT_YELLOW_EDGE",center+Vector3.up*.21f,new Vector3(to-from,.035f,.075f),yellow,MainBelt.transform,false);
        }

        private void BuildMachines()
        {
            float[] centers=MachineCenters;
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
                GameObject label=C3BakedSignFactory.AtWorld(signs[lane],new Vector3(x,3.70f,12.82f),Quaternion.Euler(0f,180f,0f),2.05f,.55f,false);
                if (label) { label.transform.SetParent(machine.Root,true); OwnSignMaterials(label); }
                machine.Indicator=Box("PROCESS_INDICATOR",new Vector3(x+1.64f,3.7f,12.80f),new Vector3(.17f,.17f,.06f),glass,machine.Root,false).GetComponent<Renderer>();
                Vector3 main=new Vector3(x+1.2f,5.8f,16.85f),intake=new Vector3(x+1.2f,4.45f,15.6f);
                Vector3 tray=new Vector3(x,2.4f,15.2f),output=new Vector3(x,1.25f,12.75f),collection=new Vector3(x,1.25f,9.1f);
                machine.Intake=Marker("MACHINE_INPUT",intake,machine.Root);
                machine.Tray=Marker("PROCESS_TRAY",tray,machine.Root);
                machine.Output=Marker("MACHINE_OUTPUT",output,machine.Root);
                machine.Collection=Marker("FINISHED_COLLECTION",collection,machine.Root);
                // Scan on the shared main belt BEFORE the diversion. At main.x
                // the front post sat directly across the -Z branch centreline.
                // This setback clears that route, its guards and the pusher while
                // keeping both posts outside the shared -X material lane.
                Vector3 scanner=main+Vector3.right*.45f;
                foreach (int side in new[] {-1,1})
                    Box("IDENTIFIER_UPRIGHT",scanner+new Vector3(.0f,.35f,side*.92f),new Vector3(.10f,.7f,.10f),steel,machine.Root);
                Box("IDENTIFIER_GANTRY",scanner+Vector3.up*.75f,new Vector3(.16f,.12f,1.95f),yellow,machine.Root);
                Box("IDENTIFIER_SENSOR",scanner+Vector3.up*.63f,new Vector3(.16f,.08f,.24f),glass,machine.Root,false);
                machine.Pusher=Box("DIVERT_PUSHER",main+new Vector3(0f,.20f,.8f),new Vector3(.7f,.20f,.08f),yellow,machine.Root,false).transform;
                // Leave the full-width main belt horizontally before descending;
                // a ramp starting at its centre would cut through the main surface.
                Vector3 transfer=new Vector3(x+1.2f,5.8f,15.8f);
                machine.Chute=Section("V3_CONNECTED_DIVERT_TO_HOPPER_"+lane,main,transfer,1.25f);
                machine.HopperDrop=Section("P2_HOPPER_DROP_"+lane,transfer,intake,1.25f);
                // Drop beside the raised press, then feed sideways underneath it.
                // A centered steep ramp would pass through the retracted head.
                Vector3 bedEntry=new Vector3(x+1.2f,2.4f,15.3f);
                machine.IntakeRamp=Section("P2_INTAKE_RAMP_"+lane,intake,bedEntry,.9f);
                machine.ReceivingBed=Section("P2_RECEIVING_BED_"+lane,bedEntry,
                    new Vector3(x+1.2f,2.4f,15f),.9f,withGuards:false);
                machine.InputSections.Add(machine.Chute); machine.InputSections.Add(machine.HopperDrop);
                machine.InputSections.Add(machine.IntakeRamp); machine.InputSections.Add(machine.ReceivingBed);
                Vector3 bedEdge=tray+Vector3.back*.5f;
                machine.DischargeBed=Section("P2_DISCHARGE_BED_"+lane,tray,bedEdge,1.55f,withGuards:false);
                machine.InternalOutput=Section("P2_INTERNAL_OUTPUT_"+lane,bedEdge,output,1.55f);
                machine.Outfeed=Section("CONNECTED_LOWER_OUTFEED_"+lane,output,new Vector3(x,1.25f,9.8f),1.55f);
                machine.CollectionBed=Section("P2_COLLECTION_BED_"+lane,machine.Outfeed.End.position,collection,2.4f);
                machine.OutputSections.Add(machine.DischargeBed); machine.OutputSections.Add(machine.InternalOutput);
                machine.OutputSections.Add(machine.Outfeed); machine.OutputSections.Add(machine.CollectionBed);
                machine.ProcessBed=Box("PROCESS_BED",tray-Vector3.up*.09f,new Vector3(3.2f,.18f,1.0f),steel,machine.Root).GetComponent<BoxCollider>();
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
        public Material BindingMaterial => steel;
        public void AnimateRollers(float dt)
        {
            foreach (Transform roller in rollers) roller.Rotate(Vector3.up,180f*dt,Space.Self);
        }
        public void SetPress(int machine,float compression)
        {
            Machine m=Machines[machine];
            m.Press.position=m.Tray.position+Vector3.up*Mathf.Lerp(1.25f,.21f,compression);
            indicatorBlock.SetColor("_Color",compression>0f?new Color(.1f,.95f,.6f):new Color(.12f,.3f,.32f));
            m.Indicator.SetPropertyBlock(indicatorBlock);
        }
        public void SetDiverter(int machine,float travel,bool occupied,float dt)
        {
            Machine m=Machines[machine];
            float offset=occupied?RawRadius+.04f-Mathf.Clamp01(travel)*m.Chute.Length:.8f;
            Vector3 target=m.Chute.Start.position+new Vector3(0f,.2f,offset);
            m.Pusher.position=Vector3.MoveTowards(m.Pusher.position,target,C3RecyclingLine.TransportSpeed*2f*dt);
        }
        public bool ConnectionsMatch(float tolerance)
        {
            for (int i=0;i<CommonSections.Count-1;i++)
                if (Vector3.Distance(CommonSections[i].End.position,CommonSections[i+1].Start.position)>tolerance) return false;
            if (Vector3.Distance(CommonSections[CommonSections.Count-1].End.position,MainBelt.Start.position)>tolerance) return false;
            foreach (Machine m in Machines)
            {
                if (Vector3.Distance(m.Chute.End.position,m.HopperDrop.Start.position)>tolerance) return false;
                if (Vector3.Distance(m.HopperDrop.End.position,m.IntakeRamp.Start.position)>tolerance) return false;
                if (Vector3.Distance(m.IntakeRamp.End.position,m.ReceivingBed.Start.position)>tolerance) return false;
                if (Vector3.Distance(m.InternalOutput.End.position,m.Outfeed.Start.position)>tolerance) return false;
                if (Vector3.Distance(m.DischargeBed.End.position,m.InternalOutput.Start.position)>tolerance) return false;
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
