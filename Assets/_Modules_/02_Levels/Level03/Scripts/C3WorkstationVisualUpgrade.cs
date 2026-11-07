using System.Collections.Generic;
using UnityEngine;

namespace C3TreasurePuzzle
{
    // Art for the four ORIGINAL stations; all gameplay components/colliders stay on their owners.
    [DisallowMultipleComponent]
    public sealed class C3WorkstationVisualUpgrade : MonoBehaviour
    {
        private C3VisualBuilder art;
        [SerializeField] private List<GameObject> roots = new List<GameObject>();
        private readonly Dictionary<Renderer,bool> hidden = new Dictionary<Renderer,bool>();
        [SerializeField] private List<Material> ownedSignMaterials = new List<Material>();
        [SerializeField] private List<BorrowedSign> borrowedSigns = new List<BorrowedSign>();
        private readonly Dictionary<Material, Material> runtimePalette = new Dictionary<Material, Material>();
        [System.Serializable]
        private struct HiddenRenderer { public Renderer Renderer; public bool Enabled; }
        [SerializeField] private List<HiddenRenderer> hiddenLayout = new List<HiddenRenderer>();
        [System.Serializable]
        private sealed class BorrowedSign
        {
            public Transform Sign, Parent;
            public Vector3 LocalPosition, LocalScale, WorldPosition, WorldScale;
            public Quaternion LocalRotation, WorldRotation;
            public int Sibling;
            [SerializeField] private bool hadParent;

            public BorrowedSign(Transform sign)
            {
                Sign=sign;Parent=sign.parent;hadParent=Parent;Sibling=sign.GetSiblingIndex();
                LocalPosition=sign.localPosition;LocalRotation=sign.localRotation;
                LocalScale=sign.localScale;WorldPosition=sign.position;WorldRotation=sign.rotation;WorldScale=sign.lossyScale;
            }

            public void Restore()
            {
                if(!Sign)return;
                if (Sign.parent != Parent)
                    Sign.SetParent(Parent, false);
                if(Parent)
                {Sign.localPosition=LocalPosition;Sign.localRotation=LocalRotation;}
                else Sign.SetPositionAndRotation(WorldPosition,WorldRotation);
                Sign.localScale=Parent || !hadParent?LocalScale:WorldScale;
            }
        }
        [field: SerializeField] public int StationCount { get; private set; }
        private void Start() => EnsureLayout();

        public void EnsureLayout()
        {
            if (roots.Count != 0)
            {
                if (Application.isPlaying && runtimePalette.Count == 0 && art == null)
                {
                    // Unity deserializes absent Object references as managed fake-null
                    // wrappers. Restore the original lease's real null root parent.
                    foreach (BorrowedSign sign in borrowedSigns) if (!sign.Parent) sign.Parent = null;
                    foreach (GameObject root in roots) if (root) C3FactoryLayoutUtility.BindOwnedPalette(root, runtimePalette);
                    for (int i = 0; i < ownedSignMaterials.Count; i++)
                        if (runtimePalette.TryGetValue(ownedSignMaterials[i], out Material copy)) ownedSignMaterials[i] = copy;
                    foreach (HiddenRenderer state in hiddenLayout) if (state.Renderer) hidden[state.Renderer] = state.Enabled;
                }
                return;
            }
            art=new C3VisualBuilder();
            // Capture siblings before moving ANY original world-root nameplates.
            foreach(string name in new[]{"ControlCenter","Inspection","Assembly"})
            {
                GameObject sign=GameObject.Find("C3_IndustrialSign_"+name);
                if(sign)borrowedSigns.Add(new BorrowedSign(sign.transform));
            }
            Install("M4_PuzzleConsole",0);
            Install("Secondary_RecyclingInspectionWorkbench_F",1);
            Install("Hidden_WeaponAssemblyBench_F",2);
            Install("UpperChipMaintenanceBench",3);
            DecorateMaterialCache("Hidden_ElectronicScrapCache_F",0);
            DecorateMaterialCache("Distributed_RecycledPlastic_F",1);
            DecorateMaterialCache("Distributed_RecycledMetal_F",2);
            DecorateWear();
        }
        private void Hide(Renderer r)
        {
            if(!r || hidden.ContainsKey(r))return;
            Collider physical=r.GetComponent<Collider>();
            // Keep original physical table members visibly present; never create
            // hidden solid furniture by hiding an enabled-collider renderer.
            if(physical && physical.enabled && !physical.isTrigger)return;
            hidden.Add(r,r.enabled);
            hiddenLayout.Add(new HiddenRenderer { Renderer = r, Enabled = r.enabled });
            r.enabled=false;
        }
        private void Install(string name,int kind)
        {
            GameObject station=GameObject.Find(name);
            if(!station){Debug.LogWarning("V11 visual station not found: "+name,this);return;}
            Collider collider=station.GetComponent<Collider>();
            if(!collider){Debug.LogError("V11 requires original station Collider: "+name,this);return;}
            // Capture only original presentation before creating new descendants.
            foreach(Renderer r in station.GetComponentsInChildren<Renderer>(true))
            {
                // Leave powered activation lamps live; they are gameplay feedback.
                if(r.name=="M5_ActivationLight" || r.name.Contains("Activation"))continue;
                Hide(r);
            }
            Transform root=art.Group(transform,"V11_StationArt_"+name,Vector3.zero);
            root.SetPositionAndRotation(new Vector3(station.transform.position.x,collider.bounds.min.y,station.transform.position.z),Quaternion.identity);
            root.SetParent(station.transform,true);
            roots.Add(root.gameObject);
            float width=kind==0?2.15f:kind==3?1.45f:1.65f;
            float depth=kind==0?.76f:.90f;
            Table(root,width,depth);
            if(kind==0)
            {
                // The replacement table was visual-only while the original
                // interaction collider covers a smaller footprint. Keep a
                // separate solid *sibling* of the visual art, parented to the
                // existing interactable, so F rays still resolve to the console.
                InstallConsoleWorktopCollision(station.transform,root,width,depth);
                Console(root);
            }
            else if(kind==1)Inspection(root);
            else if(kind==2)Assembly(root);
            else Maintenance(root);
            string sign=kind==0?"ControlCenter":kind==1?"Inspection":kind==2?"Assembly":"Repair";
            Vector3 platePosition=root.TransformPoint(new Vector3(0,.62f,-depth*.5f-.045f));
            GameObject label=kind==3?null:GameObject.Find("C3_IndustrialSign_"+sign);
            bool borrowed=label;
            if(label)
            {
                label.transform.SetPositionAndRotation(platePosition,Quaternion.Euler(0,180,0));
            }
            else label=C3BakedSignFactory.AtWorld(sign,platePosition,Quaternion.Euler(0,180,0),width*.65f,.22f,false);
            if (label)
{
            if (!borrowed)
            {
                label.transform.SetParent(root, true);

                foreach (Renderer renderer in label.GetComponentsInChildren<Renderer>())
               {
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material && !ownedSignMaterials.Contains(material))
                    ownedSignMaterials.Add(material);
            }
        }
    }
}

            StationCount++;
        }
        private void InstallConsoleWorktopCollision(Transform station,Transform visual,float width,float depth)
        {
            // Do not add collision to V11_StationArt: its primitives must
            // remain ray/physics-neutral for the existing visual tests.
            GameObject blocker=new GameObject("V11_ConsoleWorktopCollision");
            blocker.layer=station.gameObject.layer;
            Transform proxy=blocker.transform;
            proxy.SetParent(station,false);
            proxy.localPosition=visual.localPosition;
            proxy.localRotation=visual.localRotation;
            proxy.localScale=visual.localScale;
            // A full-width solid collider blocked the M6 gameplay ray aimed
            // from the south aisle at M4_PuzzleConsole's original collider.
            // Two solid panels retain the same physical front and top extent,
            // but leave a narrow center ray channel. The 0.18m channel is
            // smaller than the player's 0.60m CharacterController diameter,
            // so a player cannot walk through it. No raycast masks, triggers,
            // or gameplay targeting rules are changed.
            const float centerRayGap=.18f;
            float panelWidth=(width-centerRayGap)*.5f;
            float offset=(width+centerRayGap)*.25f;
            for(int side=-1;side<=1;side+=2)
            {
                GameObject panel=new GameObject(side<0?
                    "V11_ConsoleBarrier_Left":"V11_ConsoleBarrier_Right");
                panel.layer=station.gameObject.layer;
                panel.transform.SetParent(proxy,false);
                panel.transform.localPosition=new Vector3(side*offset,0f,0f);
                BoxCollider solid=panel.AddComponent<BoxCollider>();
                solid.center=new Vector3(0f,.43f,0f);
                solid.size=new Vector3(panelWidth,.86f,depth);
                solid.isTrigger=false;
            }
            roots.Add(blocker); // destroyed with this visual upgrade, not gameplay
        }

        private void Table(Transform r,float width,float depth)
        {
            art.Part(r,"WeatheredSteelWorktop",new Vector3(0,.80f,0),new Vector3(width,.10f,depth),"silver");
            for(int side=-1;side<=1;side+=2)
            {
                art.Part(r,"DrawerCabinet",new Vector3(side*(width*.5f-.22f),.39f,0),new Vector3(.38f,.74f,depth*.85f),"green");
                for(int j=0;j<3;j++)
                {
                    art.Part(r,"SteelDrawerFace",new Vector3(side*(width*.5f-.22f),.23f+j*.19f,-depth*.435f),new Vector3(.33f,.15f,.035f),"silver");
                    art.Part(r,"DrawerPull",new Vector3(side*(width*.5f-.22f),.24f+j*.19f,-depth*.47f),new Vector3(.13f,.025f,.025f),"dark");
                }
                art.Part(r,"Foot",new Vector3(side*(width*.5f-.17f),.04f,0),new Vector3(.29f,.08f,depth*.9f),"rust");
            }
            art.Part(r,"CentralCabinet",new Vector3(0,.38f,.14f),new Vector3(width*.37f,.65f,.39f),"green");
            art.Part(r,"FixedNameplateMountRail",new Vector3(0,.62f,-depth*.5f+.005f),
                new Vector3(width*.94f,.28f,.06f),"rust");
            art.Recycle(r,new Vector3(0,.38f,-.065f),.16f);
            for(int i=0;i<8;i++)art.Part(r,"HazardStripe",new Vector3((i-3.5f)*width*.105f,.79f,-depth*.5f-.003f),new Vector3(width*.045f,.055f,.008f),i%2==0?"yellow":"dark",PrimitiveType.Cube,new Vector3(0,0,-20));
            art.Wear(r,new Vector3(0,.85f,-depth*.49f),width*.9f);
        }
        private void Console(Transform r)
        {
            art.Screen(r,new Vector3(-.34f,1.43f,.22f),new Vector2(1.08f,.65f));
            art.Screen(r,new Vector3(.60f,1.59f,.22f),new Vector2(.52f,.30f));
            art.Screen(r,new Vector3(.60f,1.23f,.22f),new Vector2(.52f,.30f));
            for(int i=0;i<12;i++)art.Part(r,"ControlButton",new Vector3(-.70f+(i%6)*.19f,.91f,-.22f+(i/6)*.14f),new Vector3(.07f,.04f,.07f),i%3==0?"glow":"yellow");
            for(int i=0;i<3;i++)
            {
                art.Rod(r,"ControlLever",new Vector3(.42f+i*.16f,.88f,-.22f),new Vector3(.42f+i*.16f,1.04f,-.17f),.027f,"silver");
                art.Part(r,"LeverGrip",new Vector3(.42f+i*.16f,1.05f,-.17f),new Vector3(.065f,.065f,.065f),"dark",PrimitiveType.Sphere);
            }
            art.Part(r,"EmergencyStop",new Vector3(.91f,.94f,-.13f),new Vector3(.12f,.045f,.12f),"red",PrimitiveType.Cylinder);
            for(int i=0;i<7;i++)art.Part(r,"VentSlat",new Vector3(-.91f,.26f+i*.05f,-.39f),new Vector3(.17f,.014f,.014f),"dark");
            art.Rod(r,"ExternalConduit",new Vector3(-1f,.12f,.22f),new Vector3(-1f,1.48f,.22f),.055f,"rust");
        }
        private void Inspection(Transform r)
        {
            art.Part(r,"SampleTray",new Vector3(-.25f,.89f,-.06f),new Vector3(.78f,.06f,.49f),"dark");
            art.CircuitBoard(r,new Vector3(-.34f,.93f,-.05f),.7f);
            for(int i=0;i<7;i++)art.Part(r,"PolymerFragment",new Vector3(-.12f+i*.047f,.94f,.12f),new Vector3(.06f,.02f,.07f),i%2==0?"blue":"yellow",PrimitiveType.Cube,new Vector3(0,i*31,8));
            for(int side=-1;side<=1;side+=2)art.Part(r,"ScannerUpright",new Vector3(side*.65f,1.25f,.25f),new Vector3(.10f,.85f,.12f),"rust");
            art.Part(r,"ScannerCrossbeam",new Vector3(0,1.68f,.25f),new Vector3(1.40f,.12f,.16f),"silver");
            art.Part(r,"OpticalScanner",new Vector3(-.25f,1.55f,.08f),new Vector3(.22f,.16f,.24f),"dark");
            art.Part(r,"ScannerLens",new Vector3(-.25f,1.46f,.08f),new Vector3(.14f,.02f,.14f),"glow",PrimitiveType.Cylinder);
            art.Screen(r,new Vector3(.47f,1.20f,-.10f),new Vector2(.43f,.34f));
        }
        private void Assembly(Transform r)
        {
            for(int side=-1;side<=1;side+=2)
            {
                Vector3 a=new Vector3(side*.64f,.90f,.18f),b=new Vector3(side*.53f,1.40f,.19f),c=new Vector3(side*.21f,1.18f,0);
                art.Rod(r,"RobotArmUpper",a,b,.09f,"yellow");art.Rod(r,"RobotArmForearm",b,c,.075f,"silver");
                art.Part(r,"RobotPivot",b,new Vector3(.16f,.16f,.16f),"dark",PrimitiveType.Sphere);
                for(int claw=-1;claw<=1;claw+=2)art.Rod(r,"AssemblyGripper",c+Vector3.right*(claw*.045f),c+new Vector3(claw*.025f,-.12f,0),.02f,"silver");
            }
            Transform display=art.Group(r,"ClampedWeaponDisplay",new Vector3(0,1.01f,0));display.localRotation=Quaternion.Euler(0,90,0);
            art.QuantumRifle(display,.60f);
            art.Part(r,"WeaponVice",new Vector3(0,.91f,0),new Vector3(.32f,.12f,.33f),"dark");
            art.Screen(r,new Vector3(-.59f,1.06f,-.18f),new Vector2(.33f,.26f));
        }
        private void Maintenance(Transform r)
        {
            art.Part(r,"ToolPegboard",new Vector3(0,1.24f,.34f),new Vector3(1.35f,.76f,.06f),"rust");
            for(int i=0;i<9;i++)for(int j=0;j<4;j++)art.Part(r,"PegHole",new Vector3(-.57f+i*.14f,1.02f+j*.15f,.302f),new Vector3(.017f,.017f,.003f),"dark");
            for(int i=0;i<5;i++)
            {
                art.Rod(r,i<3?"Spanner":"Screwdriver",new Vector3(-.50f+i*.22f,1.06f,.27f),new Vector3(-.50f+i*.22f,1.40f,.27f),.023f,"silver");
                art.Part(r,"ToolHandle",new Vector3(-.50f+i*.22f,1.07f,.25f),new Vector3(.055f,.14f,.04f),i<3?"silver":"yellow");
                if(i<3)art.Part(r,"SpannerJaw",new Vector3(-.50f+i*.22f,1.41f,.25f),new Vector3(.09f,.06f,.04f),"silver");
            }
            // One hanging pair of pliers in its own right-hand bay, clear of
            // the screwdriver at X=.38. Only the two handles meet at the pivot.
            Transform pliers=art.Group(r,"MaintenancePliers",new Vector3(.55f,1.37f,.25f));
            art.Rod(pliers,"PliersLeft",Vector3.zero,new Vector3(-.055f,-.23f,0),.025f,"red");
            art.Rod(pliers,"PliersRight",Vector3.zero,new Vector3(.055f,-.23f,0),.025f,"red");
            art.Part(pliers,"PliersPivot",Vector3.zero,new Vector3(.045f,.045f,.028f),"silver",PrimitiveType.Sphere);
            for(int side=-1;side<=1;side+=2)
                art.Rod(pliers,"PliersJaw_"+side,Vector3.zero,new Vector3(side*.028f,.07f,0),.018f,"silver");
            art.Rod(r,"PliersPeg",new Vector3(.55f,1.39f,.31f),new Vector3(.55f,1.39f,.25f),.014f,"silver");
            // Decorative PCB only: keep the original quest chip/Collider in place.
            // PCB base rests on the .85m worktop instead of floating above it.
            Transform board=art.Group(r,"MaintenanceCircuitBoard",new Vector3(-.43f,.855f,-.20f));
            board.localRotation=Quaternion.Euler(0,28f,0);
            art.CircuitBoard(board,Vector3.zero,.55f);
            // The existing chip's visual underside is .8672m above this root,
            // while the tabletop is .85m. Support it without moving gameplay.
            art.Part(r,"ChipESDSupport",new Vector3(0,.8585f,-.03f),new Vector3(.28f,.017f,.20f),"dark");
            art.Part(r,"MaintenanceToolbox",new Vector3(.39f,.98f,.02f),new Vector3(.35f,.25f,.27f),"green");
            art.Part(r,"ToolboxHandle",new Vector3(.39f,1.13f,.02f),new Vector3(.17f,.035f,.06f),"silver");
            art.Rod(r,"WorkLampArm",new Vector3(-.6f,.87f,.19f),new Vector3(-.6f,1.65f,.19f),.025f,"silver");
            art.Part(r,"MaintenanceLamp",new Vector3(-.46f,1.64f,.12f),new Vector3(.35f,.065f,.15f),"glow");
        }
        private void DecorateMaterialCache(string name,int kind)
        {
            GameObject go=GameObject.Find(name);if(!go)return;
            Transform r=art.Group(go.transform,"V11_CacheContents_Visual",new Vector3(0,.5f,0));
            roots.Add(r.gameObject);
            if(kind==0)art.CircuitBoard(r,Vector3.zero,1f);
            else for(int i=0;i<5;i++)
            {
                Vector3 p=new Vector3((i%3-1)*.13f,.02f*(i/3),(i/3)*.16f);
                if(kind==1)art.Bottle(r,p,.40f);else art.Can(r,p,.40f,i%2==0?"silver":"red");
            }
            C3ProximityHint hint=go.GetComponent<C3ProximityHint>();
            if(hint)hint.RegisterVisuals(r);
        }
        private void DecorateWear()
        {
            // Surface paint only: no holes, dropped structural members, lights or collision changes.
            Transform r=art.Group(transform,"V11_SurfaceWeathering_Visual",Vector3.zero);
            r.SetPositionAndRotation(Vector3.zero,Quaternion.identity);roots.Add(r.gameObject);
            for(int i=0;i<16;i++)
            {
                float x=-23f+(i%8)*3.0f,z=7.9f+(i/8)*11.8f;
                art.Part(r,"OilAndDustDeposit",new Vector3(x,.574f,z),new Vector3(.42f+.08f*(i%3),.002f,.26f),"oil",PrimitiveType.Sphere);
                for(int track=0;track<3;track++)art.Part(r,"WornWheelTrace",new Vector3(x+track*.035f,.575f,z+.40f),new Vector3(.012f,.001f,.65f),"oil");
            }
            for(int i=0;i<12;i++)
            {
                float z=2.1f+i*1.83f;
                // Keep every decoration away from the actual B/C opening.
                if(z>9.9f && z<15.1f)continue;
                art.Part(r,"WallRustRunoff",new Vector3(-.47f,1.6f+(i%3)*.61f,z),new Vector3(.003f,.38f,.065f),"rust");
            }
        }
        private void OnDestroy()
        {
            if (!Application.isPlaying) return;
            // Borrowed signs must leave the outgoing roots BEFORE deferred Destroy.
            // Their original materials belong to their original creators, not us.
            foreach(BorrowedSign sign in borrowedSigns)sign.Restore();
            borrowedSigns.Sort((a,b)=>a.Sibling.CompareTo(b.Sibling));
            foreach(BorrowedSign sign in borrowedSigns)if(sign.Sign)sign.Sign.SetSiblingIndex(sign.Sibling);
            borrowedSigns.Clear();
            foreach(var pair in hidden)if(pair.Key && !pair.Key.enabled)pair.Key.enabled=pair.Value;
            foreach(GameObject root in roots)if(root)
            {
                root.SetActive(false);
                foreach(Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterials=System.Array.Empty<Material>();
                Destroy(root);
            }
            foreach(Material m in ownedSignMaterials) C3FactoryLayoutUtility.ReleaseTransient(m);
            ownedSignMaterials.Clear();
            foreach (Material material in runtimePalette.Values) C3FactoryLayoutUtility.ReleaseTransient(material);
            runtimePalette.Clear();
            if(art!=null)art.Dispose();
        }
    }
}
