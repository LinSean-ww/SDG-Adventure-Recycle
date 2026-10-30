using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace C3TreasurePuzzle
{
    /// <summary>
    /// A single, spatially connected factory layout. The three output machines sit
    /// BELOW the shared infeed conveyor; a down-chute enters each roof hopper and
    /// each lower conveyor touches the corresponding output throat.
    /// The legacy WasteSortingLine still owns puzzle timing and counts, but its
    /// duplicate visible meshes are hidden and its transform is moved into the bank.
    /// Coordinates are in C_Main's existing -25..0 / 0..25 hall.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class C3ReferenceFactoryEnvironment : MonoBehaviour
    {
        public const float InputBeltY = 5.62f;
        public const float InputBeltZ = 16.85f;
        public const float SorterRoofY = 4.12f;
        public const float SorterFrontZ = 12.95f;
        public const float OutfeedTopY = 1.16f;
        public const float OutputEndZ = 10.02f;
        public const float OutputStartZ = 12.96f;
        public const int LaneCount = 3;
        private static readonly float[] Centers = { -17.9f, -13.0f, -8.1f }; // paper, metal, plastic

        private Transform root;
        private C3DeviceState device;
        private Material floor, tile, steel, stainless, graphite, belt, yellow, rust,
            paper, plastic, metalBale, green, tealGlow, purpleGlow, lightLens,
            darkGlass, wall, pipe, warmWhite;
        private readonly List<Material> owned = new List<Material>();
        private readonly List<Transform> packages = new List<Transform>();
        private readonly List<Transform> bFeedPackets = new List<Transform>();
        private readonly List<Transform> rollers = new List<Transform>();
        private readonly List<Renderer> machineIndicators = new List<Renderer>();
        private readonly List<Light> lamps = new List<Light>();
        private bool running;
        private float clock;
        public int DecorativeObjects { get; private set; }
        public int ConveyorPackageCount => packages.Count;
        public bool ConveyorMoving => running;
        public bool ConveyorEndsInSorter =>
            Mathf.Abs(InputBeltY - 5.62f) < .001f &&
            Mathf.Abs(OutputStartZ - SorterFrontZ) < .05f && LaneCount == Centers.Length;

        private void Awake()
        {
            device = GetComponent<C3DeviceState>();
            if (GameObject.Find("C3_ReferenceRecyclingFactory_Visuals")) return;
            root = new GameObject("C3_ReferenceRecyclingFactory_Visuals").transform;
            root.SetParent(transform, false);
            root.localPosition = Vector3.zero;
            InitMaterials();
            ReplaceMismatchedLegacyArt(); // hide invisible legacy collision walls too
            BuildWarehouse();
            BuildFloorAndCirculation();
            BuildUnifiedSorter();
            BuildInfeedConveyorAndPhysicalDrops();
            BuildOutfeedPallets();
            BuildInputWasteCages();
            BuildWorkStations();
            BuildPollutionSkid();
            // V3 walkways/stairs/rails use authored meshes in C3V3Circulation.
            // BuildUpperWalkways() was an obstructing visual overlay; do not call it.
            BuildOverheadPiping();
            BuildPendantLights();
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.66f, .69f, .71f);
            RenderSettings.ambientIntensity = 1.25f;
            RenderSettings.fog = false;
        }

        private void OnEnable()
        {
            if (!device) device = GetComponent<C3DeviceState>();
            if (device) device.Running += StartMachinery;
            if (device && device.FactoryState >= C3FactoryState.Running) StartMachinery();
        }
        private void OnDisable() { if (device) device.Running -= StartMachinery; }
        private void OnDestroy() { foreach (Material m in owned) if (m) Destroy(m); }
        private void StartMachinery()
        {
            running = true;
            foreach (Renderer r in machineIndicators) if (r) r.sharedMaterial = tealGlow;
            foreach (Light l in lamps) if (l) l.intensity = 1.55f;
        }
        private void Update()
        {
            if (!running) return;
            clock += Time.deltaTime;
            for (int i = 0; i < packages.Count; ++i)
            {
                Transform item = packages[i];
                if (!item) continue;
                int lane = i % LaneCount; // paper / metal / plastic correspond to the three machines
                float phase = Mathf.Repeat(clock * .105f + i / (float)packages.Count, 1f);
                item.gameObject.SetActive(phase < .76f || phase >= .87f);
                if (phase < .58f)
                {
                    // The package stays on the SAME elevated conveyor until its own lane.
                    item.localPosition = new Vector3(
                        Mathf.Lerp(-6.32f, Centers[lane], phase / .58f),
                        InputBeltY + .75f, InputBeltZ);
                }
                else if (phase < .76f)
                {
                    // It travels down the visible chute into the real machine roof.
                    float t = (phase - .58f) / .18f;
                    item.localPosition = new Vector3(Centers[lane],
                        Mathf.Lerp(InputBeltY + .75f, 4.37f, t),
                        Mathf.Lerp(InputBeltZ, 16.04f, t));
                }
                else if (phase >= .87f)
                {
                    // It is then shown again on the CONNECTED lower discharge belt.
                    float t = (phase - .87f) / .13f;
                    item.localPosition = new Vector3(Centers[lane], 1.40f,
                        Mathf.Lerp(OutputStartZ, OutputEndZ, t));
                }
            }
            // V9 feed is front-visible and terminates at the shared three-machine belt.
            for (int i=0;i<bFeedPackets.Count;i++)
            {
                Transform packet=bFeedPackets[i];
                if (!packet) continue;
                float t=Mathf.Repeat(clock*.065f+i/(float)bFeedPackets.Count,1f);
                Vector3 p0=new Vector3(-.67f,1.30f,12.50f);
                Vector3 p1=new Vector3(-1.20f,1.30f,12.50f);
                Vector3 p2=new Vector3(-6.90f,6.67f,11.85f);
                Vector3 p3=new Vector3(-6.90f,6.67f,16.20f);
                Vector3 p4=new Vector3(-6.20f,5.77f,16.85f);
                if (t<.06f) packet.position=Vector3.Lerp(p0,p1,t/.06f);
                else if(t<.55f) packet.position=Vector3.Lerp(p1,p2,(t-.06f)/.49f);
                else if(t<.92f) packet.position=Vector3.Lerp(p2,p3,(t-.55f)/.37f);
                else packet.position=Vector3.Lerp(p3,p4,(t-.92f)/.08f);
            }
            foreach (Transform r in rollers) if (r) r.Rotate(Vector3.up, 135f * Time.deltaTime, Space.Self);
        }

        private void InitMaterials()
        {
            floor = Mat("Concrete floor", new Color(.91f, .94f, .93f), .08f, .23f, "FactoryConcrete");
            tile = Mat("Polished wet concrete", new Color(.92f, .92f, .89f), .22f, .51f, "FactoryConcrete");
            wall = Mat("Factory light walls", new Color(.82f, .87f, .87f), .09f, .3f, "FactoryConcrete");
            steel = Mat("Industrial painted steel", new Color(.70f, .77f, .79f), .63f, .4f, "FactorySteel");
            stainless = Mat("Galvanized sorting plant", new Color(.92f, .96f, .98f), .76f, .56f, "FactorySteel");
            graphite = Mat("Black structural steel", new Color(.23f, .29f, .32f), .55f, .37f, "FactorySteel");
            belt = Mat("Dark conveyer rubber", new Color(.40f, .43f, .46f), .03f, .17f, "FactoryRubber");
            yellow = Mat("Industrial railing yellow", new Color(.96f, .65f, .095f), .42f, .35f);
            rust = Mat("Oxide equipment", new Color(.40f, .29f, .21f), .4f, .31f, "FactorySteel");
            paper = Mat("Cardboard bale", new Color(.75f, .54f, .31f), .01f, .21f, "FactoryCardboard");
            plastic = Mat("Plastic pale blue", new Color(.36f, .73f, .80f), .06f, .42f);
            metalBale = Mat("Crushed aluminum", new Color(.47f, .59f, .72f), .65f, .55f, "FactorySteel");
            green = Mat("Plastic bay green", new Color(.11f, .48f, .31f), .34f, .38f);
            tealGlow = Mat("Machine active teal", new Color(.10f, .73f, .92f), .12f, .75f, null, true);
            purpleGlow = Mat("Quantum device luminous", new Color(.52f, .20f, .90f), .1f, .65f, null, true);
            lightLens = Mat("Diffused frosted lamp", new Color(1f, .91f, .74f), .1f, .36f, null, true);
            darkGlass = Mat("Unpowered computer glass", new Color(.035f, .13f, .18f), .1f, .71f);
            pipe = Mat("Plant ventilation pipes", new Color(.59f, .65f, .69f), .68f, .56f, "FactorySteel");
            warmWhite = Mat("Workspace label metallic", new Color(.89f, .86f, .77f), .15f, .45f);
        }
        private Material Mat(string name, Color color, float metallic, float gloss,
            string resource = null, bool emissive = false)
        {
            Shader shader = Shader.Find("Standard");
            if (!shader) shader = Shader.Find("Diffuse");
            Material m = new Material(shader) { name = name, color = color };
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", gloss);
            if (!string.IsNullOrEmpty(resource))
            {
                Texture2D tex = Resources.Load<Texture2D>("C3FactorySurface/" + resource);
                if (tex) m.mainTexture = tex;
            }
            if (name == "Concrete floor") m.mainTextureScale = new Vector2(12f, 12f);
            if (name == "Polished wet concrete") m.mainTextureScale = new Vector2(2f, 1.5f);
            if (emissive && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * 1.45f);
            }
            owned.Add(m); return m;
        }
        private GameObject Shape(string name, PrimitiveType type, Vector3 world,
            Vector3 size, Material mat, Quaternion? rotation = null, bool solid = false,
            Transform parent = null)
        {
            GameObject g = GameObject.CreatePrimitive(type);
            g.name = name;
            g.transform.SetParent(parent ? parent : root, false);
            g.transform.position = world;
            g.transform.rotation = rotation ?? Quaternion.identity;
            g.transform.localScale = size;
            Collider c = g.GetComponent<Collider>();
            if (c && !solid) { c.enabled = false; Destroy(c); }
            Renderer r = g.GetComponent<Renderer>();
            if (r) { r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.On; }
            ++DecorativeObjects;
            return g;
        }
        private GameObject Box(string name, float x,float y,float z, float sx,float sy,float sz,
            Material mat,bool solid=false)
            => Shape(name,PrimitiveType.Cube,new Vector3(x,y,z),new Vector3(sx,sy,sz),mat,
                null,solid);
        private GameObject Cyl(string name,Vector3 pos,Vector3 scale,Material mat)
            => Shape(name,PrimitiveType.Cylinder,pos,scale,mat);
        private void Beam(string name, Vector3 from,Vector3 to,float width,Material mat)
        {
            Vector3 delta=to-from;
            if(delta.sqrMagnitude < .00001f) return;
            Shape(name,PrimitiveType.Cylinder,(from+to)*.5f,
                new Vector3(width,delta.magnitude*.5f,width),mat,
                Quaternion.FromToRotation(Vector3.up,delta));
        }
        private void Sign(string name,float x,float y,float z,float w,float h,float yaw=0f)
        {
            C3BakedSignFactory.AtWorld(name,new Vector3(x,y,z),Quaternion.Euler(0,yaw,0),w,h);
        }
        private void ReplaceMismatchedLegacyArt()
        {
            // Hide the OLD machine renderers, not the entire objects: their state,
            // interactables, and sorting cycle remain owned by C3FactoryPresentation.
            GameObject art = GameObject.Find("C_Factory_Art");
            if (art)
            {
                // These are the original live upper stations, not obsolete factory art.
                // Exclude their complete subtrees BEFORE hiding the old presentation,
                // so collider state does not depend on another component's Start().
                Transform bench = art.transform.Find("Floor2_Industrial/UpperChipMaintenanceBench");
                Transform box = art.transform.Find("Floor2_Industrial/UpperDistributionBox");
                foreach (Renderer r in art.GetComponentsInChildren<Renderer>(true))
                    if (!IsUpperStation(r.transform, bench, box)) r.enabled=false;
                // Invisible decorative stair/pipe meshes must not remain solid
                // barriers that trap the player's CharacterController.
                foreach (string path in RetiredArtCollisionPaths)
                {
                    Transform item = art.transform.Find(path);
                    if (!item) continue;
                    foreach (Collider c in item.GetComponents<Collider>())
                        if (!c.isTrigger) c.enabled = false;
                }
            }
            GameObject oldSimulation = GameObject.Find("C3_SortingSimulation");
            if (oldSimulation)
            {
                foreach (Renderer r in oldSimulation.GetComponentsInChildren<Renderer>(true)) r.enabled=false;
            }
            foreach (string label in new[] { "Plastic", "Metal", "Paper" })
            {
                GameObject old = GameObject.Find("C3_IndustrialSign_" + label);
                if (old) old.SetActive(false);
            }
            // Move the one REAL sorting trigger/machine logic under the bank.
            GameObject line=GameObject.Find("WasteSortingLine");
            if (line) line.transform.position = new Vector3(-13f,.55f,14.55f);
        }

        // C_Main V10 inventory: only the collision volumes of replaced art.
        // Never extend this to arbitrary descendants: live stations, new triggers
        // and future equipment must retain their own physics and interaction.
        private static readonly string[] RetiredArtCollisionPaths =
        {
            "Architecture_Additions/Steel_MainDeckCrossBraces/Collision_CrossBraceA_-10.0",
            "Architecture_Additions/Steel_MainDeckCrossBraces/Collision_CrossBraceA_-17.5",
            "Architecture_Additions/Steel_MainDeckCrossBraces/Collision_CrossBraceA_-21.5",
            "Architecture_Additions/Steel_MainDeckCrossBraces/Collision_CrossBraceA_-5.5",
            "Architecture_Additions/Steel_MainDeckCrossBraces/Collision_CrossBraceB_-10.0",
            "Architecture_Additions/Steel_MainDeckCrossBraces/Collision_CrossBraceB_-17.5",
            "Architecture_Additions/Steel_MainDeckCrossBraces/Collision_CrossBraceB_-21.5",
            "Architecture_Additions/Steel_MainDeckCrossBraces/Collision_CrossBraceB_-5.5",
            "Floor1_Industrial/EastToolCabinet",
            "Floor1_Industrial/WestPowerCabinet_A",
            "Floor1_Industrial/WestPowerCabinet_B",
            "IndustrialMachines/FeedPump",
            "IndustrialMachines/NortheastFilterArray",
            "IndustrialMachines/PurificationReactor",
            "IndustrialMachines/WasteSortingLine",
            "Lighting/Cyan_Console/IndicatorLens",
            "Pipes/Pipe_MountsAndConnections/CeilingCableHanger_-10.0_14.8",
            "Pipes/Pipe_MountsAndConnections/CeilingCableHanger_-10.0_16.0",
            "Pipes/Pipe_MountsAndConnections/CeilingCableHanger_-15.0_14.8",
            "Pipes/Pipe_MountsAndConnections/CeilingCableHanger_-15.0_16.0",
            "Pipes/Pipe_MountsAndConnections/CeilingCableHanger_-20.0_14.8",
            "Pipes/Pipe_MountsAndConnections/CeilingCableHanger_-20.0_16.0",
            "Pipes/Pipe_MountsAndConnections/CeilingCableHanger_-6.0_14.8",
            "Pipes/Pipe_MountsAndConnections/CeilingCableHanger_-6.0_16.0",
            "Pipes/Pipe_MountsAndConnections/NorthWallHanger_-10.5",
            "Pipes/Pipe_MountsAndConnections/NorthWallHanger_-16.0",
            "Pipes/Pipe_MountsAndConnections/NorthWallHanger_-21.0",
            "Pipes/Pipe_MountsAndConnections/NorthWallHanger_-5.0",
            "Pipes/Pipe_MountsAndConnections/NorthWestJoin",
            "Pipes/Pipe_MountsAndConnections/ReactorDeckHanger_14.4",
            "Pipes/Pipe_MountsAndConnections/ReactorDeckHanger_17.0",
            "Pipes/Pipe_MountsAndConnections/ReactorDeckHanger_19.5",
            "Pipes/Pipe_MountsAndConnections/WestNorthElbow",
            "Pipes/Pipe_MountsAndConnections/WestWallHanger_14.0",
            "Pipes/Pipe_MountsAndConnections/WestWallHanger_18.6",
            "Pipes/Pipe_MountsAndConnections/WestWallHanger_22.0",
            "Pipes/Pipe_MountsAndConnections/WestWallHanger_4.0",
            "Pipes/Pipe_MountsAndConnections/WestWallHanger_9.0",
            "Pipes/Pipe_NorthFilterFeed/Collision_NorthFilterBranch",
            "Pipes/Pipe_NorthFilterFeed/Collision_NorthFilterMain",
            "Pipes/Pipe_ReactorReturn/Collision_ReactorOut",
            "Pipes/Pipe_ReactorReturn/Collision_ReactorReturnJoin",
            "Pipes/Pipe_ReactorReturn/Collision_ReactorReturnMain",
            "Pipes/Pipe_UpperCableConduit/Collision_UpperConduitA",
            "Pipes/Pipe_UpperCableConduit/Collision_UpperConduitB",
            "Pipes/Pipe_WestServiceTrunk/Collision_MainWestTrunk",
            "Pipes/Pipe_WestServiceTrunk/Collision_WestCabinetA",
            "Pipes/Pipe_WestServiceTrunk/Collision_WestCabinetB",
            "Props/Barrel_East",
            "Props/Barrel_North",
            "Props/Barrel_West",
            "Props/WasteContainer_A",
            "Props/WasteContainer_B",
        };

        private static bool IsUpperStation(Transform item, Transform bench, Transform box)
            => (bench && item.IsChildOf(bench)) || (box && item.IsChildOf(box));

        private void BuildWarehouse()
        {
            // Actual roof ceiling proxy has been raised to 12.5m in C_Main.unity.
            // This is a visual industrial roof *below* the original collision lid,
            // not an invisible roof above the player's camera.
            Box("V4_HIGH_ROOF_VISIBLE_DARK_STEEL",-12.5f,12.34f,12.5f,
                24.45f,.08f,24.45f,graphite);
            // Do not occlude the existing B/D doors or their colliders.
            for(int i=0;i<7;i++)
            {
                float z=1.0f + i*3.75f;
                for(int side=0;side<2;side++)
                {
                    // Do not block the actual B doorway with an ornamental pillar.
                    if (side==1 && Mathf.Abs(z-12.5f)<1f) continue;
                    // West posts sit inside the wall, outside the stair rail envelope.
                    // Keep the east column's inner face (-0.60) outside the V11
                    // deck edge (-0.65), rather than protruding into the side route.
                    float x=side==0?-24.53f:-.45f;
                    Box("WAREHOUSE structural column",x,6.05f,z,.30f,10.95f,.32f,graphite);
                    Box("Yellow column kickplate",x,.87f,z,.36f,.48f,.39f,yellow);
                }
                Box("WAREHOUSE top roof truss",-12.5f,11.45f,z,24.0f,.28f,.29f,steel);
                for(int t=0;t<6;t++)
                    Beam("Roof web bracing",new Vector3(-23f+t*4f,11.38f,z),
                        new Vector3(-21f+t*4f,10.75f,z),.045f,pipe);
            }
            for(int j=0;j<7;j++)
                Box("Warehouse roof joist",-23f+j*3.5f,11.69f,12.7f,.18f,.28f,23.2f,graphite);
            // Light paneling along walls makes labels and mechanisms readable.
            for(int i=0;i<8;i++)
            {
                float z=1.6f+i*2.9f;
                Box("Sidewall weathered concrete inset",-24.63f,4.2f,z,.10f,6.2f,2.46f,wall);
                Box("RAISED_WEST_UPPER_WALL",-24.63f,9.80f,z,.10f,5.00f,2.46f,wall);
                // The B-area door is in the EAST wall near z=12.5:
                // its visual opening must remain open for the player and feed ramp.
                if (Mathf.Abs(z - 12.5f) > 2.0f)
                {
                    Box("Sidewall weathered concrete inset",-.37f,4.2f,z,.10f,6.2f,2.46f,wall);
                    Box("RAISED_EAST_UPPER_WALL",-.37f,9.80f,z,.10f,5.00f,2.46f,wall);
                }
            }
            for(int i=0;i<8;i++)
            {
                Box("North wall insulated panel",-22.8f+i*2.95f,4.25f,24.66f,
                    2.83f,7.25f,.10f,wall);
                Box("RAISED_NORTH_UPPER_WALL",-22.8f+i*2.95f,10.12f,24.66f,
                    2.83f,4.50f,.10f,wall);
            }
            Box("RAISED_SOUTH_UPPER_WALL",-12.5f,10.10f,.32f,
                24.35f,4.50f,.12f,wall);
            // Broad beams and real roof frames instead of floating lines.
            for(int i=0;i<6;i++)
            {
                float z=2.2f+i*4.05f;
                Box("Industrial overhead cable rack",-12.5f,10.60f,z,21f,.12f,.28f,steel);
            }
            // The B area is a different zone. C manufacturing must NOT expose
            // its interior. A closed industrial material-receiving shutter sits
            // immediately behind the C-side conveyor terminus, at the existing
            // x=-0.3 zone boundary. D doorway remains open for level completion.
            Box("V7_OPAQUE_C_TO_B_RECEIVING_SHUTTER",-.36f,3.43f,12.50f,
                .16f,5.77f,4.62f,graphite,true);
            for (int k=0;k<21;k++)
                Box("V7_B_SHUTTER_PRESSED_STEEL_SLAT",-.461f,.76f+k*.265f,12.50f,
                    .027f,.038f,4.48f,steel);
            Box("V7_C_SIDE_B_FEED_RECEIVING_HATCH",-.48f,1.40f,12.50f,
                .025f,.83f,1.25f,graphite);
            // Both transitions retain named, legible signs on their real doors.
            Sign("BZone",-.51f,4.36f,12.50f,2.55f,.54f,90f);
            Sign("DZone",-12.50f,4.38f,.44f,2.55f,.54f);
        }
        private void BuildFloorAndCirculation()
        {
            // Floor rests on C_Floor collision proxy (top ~0.56), no duplicated floor physics.
            Box("REFINISHED concrete production floor",-12.5f,.561f,12.5f,23.7f,.012f,23.7f,floor);
            for(int x=0;x<11;x++)
            for(int z=0;z<11;z++)
            {
                float xx=-22.6f+x*2.02f;
                float zz=2.0f+z*2.00f;
                if(x%3==0)
                    Box("Concrete expansion joint",xx,.571f,zz,.025f,.004f,1.97f,graphite);
                if(z%3==0)
                    Box("Concrete expansion joint",xx,.571f,zz,1.95f,.004f,.025f,graphite);
            }
            // Material handling lanes and 3 clearly distinguishable front workstations.
            for(int row=0;row<2;row++)
            {
                float z=row==0?7.3f:20.7f;
                Box("Yellow pedestrian aisle marking",-12.5f,.574f,z,22.4f,.008f,.068f,yellow);
            }
            foreach(float x in new[]{-23.2f,-3.1f})
                Box("Yellow bay aisle line",x,.575f,11.9f,.068f,.006f,15.3f,yellow);
            for(int i=0;i<14;i++)
            {
                float x=-23f+i*1.55f;
                Box("Forklift lane dashed yellow",x,.577f,8.35f,.73f,.006f,.06f,yellow);
            }
            // Small diamond plate stage bases, visibly anchored to the ground.
            foreach(float x in new[]{-19.80f,-12.4f,-4.2f})
            {
                Box("Station grounding plate",x,.582f,4.3f,4.5f,.032f,3.18f,tile);
                for(int side=-1;side<=1;side+=2)
                    Box("Station station-edge paint",x+side*2.22f,.603f,4.3f,
                        .08f,.010f,3.17f,side<0?yellow:tealGlow);
            }
        }
        private void BuildUnifiedSorter()
        {
            string[] signs={"PaperHall","MetalHall","PlasticHall"};
            Material[] plates={paper,metalBale,green};
            for(int lane=0;lane<LaneCount;lane++)
            {
                float x=Centers[lane];
                // Body faces the south aisle. Top, body, output throat and roof
                // intake share EXACT axis x: no floating second sorter.
                Box("SORTER_MAIN_BODY_"+lane,x,2.48f,14.87f,4.51f,2.91f,3.86f,stainless,true);
                Box("SORTER_RECESSED_FRONT_PANEL_"+lane,x,2.67f,12.911f,4.13f,2.37f,.10f,steel);
                Box("SORTER_LANE_COLOR_"+lane,x,3.43f,12.845f,3.86f,.82f,.065f,plates[lane]);
                Box("SORTER_ROOF_CONTACT_"+lane,x,SorterRoofY,14.87f,4.66f,.15f,4.03f,pipe);
                // Grounded support feet, so the main housings do not float.
                for(int side=-1;side<=1;side+=2)
                    for(int back=-1;back<=1;back+=2)
                        Box("SORTER_MACHINE_GROUNDED_LEG_"+lane,
                            x+side*1.89f,.79f,14.88f+back*1.43f,
                            .27f,.49f,.35f,graphite);
                Box("SORTER_DARK_THROAT_"+lane,x,1.65f,12.823f,2.55f,1.18f,.06f,graphite);
                Box("SORTER_ACTIVE_OUTLET_"+lane,x,1.22f,12.784f,2.08f,.29f,.065f,belt);
                for(int side=-1;side<=1;side+=2)
                {
                    Box("SORTER_YELLOW_GUARD_"+lane,x+side*2.14f,2.53f,12.76f,
                        .13f,2.83f,.19f,yellow);
                    Box("SORTER_GRATE_EDGE_"+lane,x+side*1.31f,1.24f,11.60f,
                        .14f,.47f,2.75f,yellow);
                }
                // A machine indicator next to a machine, not floating over it.
                Renderer indicator=Box("SORTER_INDICATOR_"+lane,x+1.66f,2.56f,12.737f,
                    .13f,.13f,.065f,graphite).GetComponent<Renderer>();
                machineIndicators.Add(indicator);
                Sign(signs[lane],x,3.45f,12.725f,3.20f,.65f);
                // Three lower conveyors start flush against the true throat.
                Box("CONNECTED_LOWER_OUTFEED_"+lane,x,OutfeedTopY,11.48f,
                    2.56f,.18f,2.95f,belt);
                for(int k=0;k<9;k++)
                {
                    float z=OutputEndZ+k*(OutputStartZ-OutputEndZ)/8f;
                    GameObject r=Cyl("Outfeed steel roller L"+lane,
                        new Vector3(x,1.20f,z),new Vector3(.105f,1.26f,.105f),stainless);
                    r.transform.rotation=Quaternion.Euler(0,0,90);
                }
                for(int k=0;k<3;k++)
                {
                    float z=10.30f+k*1.02f;
                    Box("Conveyor underframe crossmember",x,.81f,z,2.5f,.12f,.22f,graphite);
                }
                // Draw actual tapered hood anchored at roof, fed by drop chutes.
                Box("Sorter intake hopper mouth",x,4.27f,16.04f,2.05f,.32f,1.20f,graphite);
            }
            // Classification category plates are fixed directly to each machine face.
        }
        private void BuildInfeedConveyorAndPhysicalDrops()
        {
            // FIXED V3 GEOMETRY: the conveyor occupies only x [-20.35,-6.20]
            // (NOT the pedestrian stair corridors at x=-22.25 and x=-4.65).
            // All three actual sorting inlets are centered on this one continuous belt.
            const float cx = -13.275f;
            const float beltWidth = 14.15f;
            Box("V3_LIVE_SINGLE_INFEED_RUBBER_SURFACE",cx,InputBeltY,InputBeltZ,
                beltWidth,.18f,1.52f,belt);
            for(int side=-1;side<=1;side+=2)
            {
                float z=InputBeltZ+side*.79f;
                Box("V3_CONVEYOR_STEEL_SIDEWALL",cx,5.74f,z,
                    beltWidth,.43f,.12f,steel);
                // Guard plates stay low, avoiding fake pedestrian railing appearance.
                Box("V3_BELT_RETENTION_LIP",cx,6.02f,z,
                    beltWidth,.052f,.065f,yellow);
            }
            for(int k=0;k<15;k++)
            {
                float x=-20.10f+k*.98f;
                GameObject r=Cyl("V3_MAIN_BELT_ROLLER",new Vector3(x,5.49f,InputBeltZ),
                    new Vector3(.09f,.69f,.09f),stainless);
                r.transform.rotation=Quaternion.Euler(90,0,0);
                rollers.Add(r.transform);
            }
            // Support points terminate ON the machine roof structure, not on a walkway.
            for(int i=0;i<6;i++)
            {
                float x=-19.8f+i*2.60f;
                for(int side=-1;side<=1;side+=2)
                {
                    float z=InputBeltZ+side*.62f;
                    Box("V3_INFEED_MACHINE_MOUNT",x,4.80f,z,
                        .13f,1.35f,.14f,graphite);
                }
            }
            for(int lane=0;lane<LaneCount;lane++)
            {
                float x=Centers[lane];
                Vector3 top=new Vector3(x,5.60f,InputBeltZ);
                Vector3 bottom=new Vector3(x,4.35f,16.04f);
                Vector3 d=bottom-top;
                Quaternion rotation=Quaternion.FromToRotation(Vector3.forward,d.normalized);
                Shape("V3_CONNECTED_DIVERT_TO_HOPPER_"+lane,PrimitiveType.Cube,
                    (top+bottom)*.5f,new Vector3(1.37f,.14f,d.magnitude+.14f),
                    stainless,rotation);
                // The authored V3_HopperShields mesh provides continuous sidewalls.
                // A black inlet is visibly inset in the machine top.
                Box("V3_SORTER_HOPPER_MOUTH_"+lane,x,4.30f,16.03f,
                    1.35f,.12f,.54f,graphite);
            }
            // B side is a fully visible continuous authored OBJ system:
            // visible floor-height belt from B door -> lift at x=-2.90,z=12.50
            // -> stair-clearance crossover (7.05m descending to 5.87m) -> sorter main belt.
            // C3V3Circulation loads the V4 meshes; do not overlay fake gray cubes.
            for(int i=0;i<7;i++)
            {
                GameObject cargo=new GameObject("B_PORTAL_GENUINE_FEED_CARGO_"+i);
                cargo.transform.SetParent(root,false);
                cargo.transform.localPosition=Vector3.Lerp(
                    new Vector3(-.67f,1.31f,12.50f),
                    new Vector3(-2.35f,1.31f,12.50f),i/7f);
                bFeedPackets.Add(cargo.transform);
                Shape("B_portal_recycled_material",PrimitiveType.Cube,
                    new Vector3(-.67f,1.31f,12.50f),new Vector3(.24f,.20f,.24f),
                    i%3==0?paper:i%3==1?metalBale:plastic,
                    parent:cargo.transform).transform.localPosition=Vector3.zero;
            }
            // Ground guide arrows point towards the live B input at the right wall.
            for(int j=0;j<3;j++)
                Box("B_ZONE_DIRECTION_FLOOR_STRIPE",-3.55f+j*.44f,.582f,11.3f,
                    .28f,.012f,.07f,yellow);
            // Infeed canisters: drawn on the physical black belt, NOT floating above it.
            for(int i=0;i<18;i++)
            {
                GameObject carrier=new GameObject("BELT_MOTION_PACKAGE_"+i);
                carrier.transform.SetParent(root,false);
                carrier.transform.localPosition=new Vector3(-6.32f-i*.71f,
                    InputBeltY+.75f,InputBeltZ);
                packages.Add(carrier.transform);
                Material m=i%3==0?plastic:i%3==1?metalBale:paper;
                GameObject piece=Shape("Recycler product on real belt",i%3==1?
                    PrimitiveType.Cylinder:PrimitiveType.Cube,
                    carrier.transform.position,new Vector3(.40f,.48f,.42f),m,
                    parent:carrier.transform);
                piece.transform.localPosition=Vector3.zero;
            }
            Sign("WasteInput",-20.8f,7.25f,16.02f,3.32f,.65f);
        }
        private void BuildOutfeedPallets()
        {
            System.Random rng=new System.Random(20261029);
            Material[] color={paper,metalBale,plastic};
            string[] name={"PAPER","METAL","PLASTIC"};
            for(int lane=0;lane<LaneCount;lane++)
            {
                float x=Centers[lane];
                // Pallet at the END of the matching conveyor, not somewhere else.
                Box("PALLET_AT_OUTPUT_"+name[lane],x,.66f,9.75f,2.94f,.21f,1.53f,graphite);
                for(int q=0;q<3;q++)
                for(int row=0;row<2;row++)
                {
                    float px=x-1.03f+q*1.03f;
                    float pz=9.49f+row*.57f;
                    Box(name[lane]+" BALED OUTPUT",px,1.10f,pz,
                        .91f,.69f,.51f,color[lane]);
                    for(int k=0;k<2;k++)
                        Box("Bale binding straps",px+(k==0?-.22f:.22f),1.11f,pz,
                            .035f,.75f,.55f,stainless);
                    if(lane==1)
                        for(int chip=0;chip<4;chip++)
                            Cyl("Recycled aluminium glints",new Vector3(px-.27f+chip*.18f,1.46f,pz),
                                new Vector3(.065f,.035f,.07f),stainless);
                }
            }
            // V8: removed loose bales from the west stair lower access aisle.
        }
        private void BuildInputWasteCages()
        {
            Material[] stuff={paper,metalBale,plastic};
            System.Random rnd=new System.Random(31528);
            for(int c=0;c<3;c++)
            {
                float x=-19.50f+c*2.55f;
                float z=22.10f;
                Box("SORTING BIN - input refuse floor",x,.78f,z,1.18f,.18f,2.16f,steel);
                for(int side=-1;side<=1;side+=2)
                for(int end=-1;end<=1;end+=2)
                    Box("Refuse cage metal upright",x+side*.57f,1.27f,z+end*1.01f,
                        .07f,.98f,.06f,graphite);
                for(int h=0;h<2;h++)
                for(int side=-1;side<=1;side+=2)
                {
                    Box("Bin lateral welded rail",x,1.00f+h*.56f,z+side*1.02f,
                        1.20f,.065f,.06f,stainless);
                    Box("Bin end welded rail",x+side*.60f,1.00f+h*.56f,z,
                        .07f,.06f,2.08f,stainless);
                }
                for(int k=0;k<18;k++)
                {
                    float px=x-.38f+(k%4)*.25f;
                    float pz=z-.74f+(k/4)*.35f;
                    Material mat=stuff[(k+c)%3];
                    Shape("Caged recycled packaging",k%4==1?PrimitiveType.Cylinder:PrimitiveType.Cube,
                        new Vector3(px,1.02f+((k%3)*.14f),pz),
                        new Vector3(.20f,.17f,.23f),mat,
                        Quaternion.Euler(0f,(float)rnd.NextDouble()*90f,0));
                }
            }
        }
        private void BuildWorkStations()
        {
            // The LEFT station is the ORIGINAL interactable M4 puzzle console;
            // no new fake console is placed elsewhere.
            GameObject puzzle=GameObject.Find("M4_PuzzleConsole");
            Vector3 p=puzzle? puzzle.transform.position : new Vector3(-20.9f,1.0f,.67f);
            Box("REAL PUZZLE CONTROL CABINET shell",p.x,1.24f,p.z+.48f,2.65f,1.42f,1.40f,steel);
            for(int k=0;k<3;k++)
            {
                float x=p.x-0.80f+k*.80f;
                Box("Recycling control computer bezel",x,2.03f,p.z+.18f,
                    .72f,.53f,.10f,graphite);
                Box("Recycling control monitor",x,2.03f,p.z+.113f,
                    .64f,.44f,.05f,tealGlow);
                Box("Control keypad with buttons",x,1.57f,p.z-.18f,
                    .62f,.055f,.41f,stainless);
                for(int key=0;key<6;key++)
                    Box("Control desk physical pushbutton",x-.22f+key*.09f,1.61f,p.z-.31f,
                        .06f,.025f,.06f,key%2==0?yellow:graphite);
            }
            Sign("ControlCenter",p.x,2.80f,p.z-.26f,3.05f,.63f);
            // The actual checking and crafting benches are positioned by
            // EcoFactoryWorkshop at ground level, in front of the machine bank.
            // EcoFactoryWorkshop already fixes small plates directly on the two real benches.
            // Do NOT add a second pair of giant suspended signs above them.
            // Give the real benches physical visual context; glowing status rails
            // are purely cosmetic and never duplicate the F interaction target.
            Box("SENSOR_LAB monitor gantry",-12.35f,1.90f,4.77f,2.55f,.22f,.17f,graphite);
            Box("MATERIAL_SCANNER active monitor",-12.35f,1.81f,4.67f,1.06f,.50f,.10f,tealGlow);
            for(int side=-1;side<=1;side+=2)
            {
                Box("Scanner gantry leg",-12.35f+side*1.21f,1.18f,4.82f,
                    .10f,1.38f,.09f,steel);
                Box("Quantum coil support",-6.80f+side*.94f,1.24f,3.05f,
                    .09f,1.13f,.11f,stainless);
            }
            Cyl("Quantum halo at active bench",new Vector3(-6.80f,1.50f,3.05f),
                new Vector3(.72f,.06f,.72f),purpleGlow);
        }
        private void MarkActualBench(string objectName,string signName,float width)
        {
            GameObject obj=GameObject.Find(objectName);
            if(!obj) return;
            Vector3 p=obj.transform.position;
            Sign(signName,p.x,Mathf.Max(2.42f,p.y+2.05f),p.z-.28f,width,.63f,
                obj.transform.eulerAngles.y);
            for(int side=-1;side<=1;side+=2)
                Box("Station nameplate hanger",p.x+side*width*.42f,
                    Mathf.Max(2.10f,p.y+1.73f),p.z-.25f,
                    .065f,.64f,.07f,yellow);
        }
        private void BuildPollutionSkid()
        {
            Box("WASTEWATER treatment grated foundation",-2.64f,.71f,17.60f,
                2.90f,.23f,5.46f,graphite);
            for(int i=0;i<3;i++)
            {
                float z=15.88f+i*1.56f;
                Cyl("POLLUTION stainless purification vessel",
                    new Vector3(-2.70f,1.82f,z),new Vector3(.54f,1.00f,.54f),stainless);
                Cyl("Purifier flange lower",new Vector3(-2.70f,.88f,z),
                    new Vector3(.61f,.09f,.61f),graphite);
                Cyl("Purifier flange upper",new Vector3(-2.70f,2.78f,z),
                    new Vector3(.62f,.08f,.62f),pipe);
                Beam("Purifier connected exhaust riser",new Vector3(-2.70f,2.8f,z),
                    new Vector3(-2.70f,6.72f,z),.105f,pipe);
                if(i>0)
                    Beam("POLLUTION connected pressurized manifold",
                        new Vector3(-2.70f,3.34f,z-1.56f),
                        new Vector3(-2.70f,3.34f,z),.08f,pipe);
                Box("POLLUTION pressure controller",-1.63f,1.64f,z,
                    .39f,.69f,.42f,steel);
                Box("Scrubber status screen",-1.84f,1.66f,z,
                    .04f,.26f,.27f,tealGlow);
            }
            Sign("PollutionControl",-1.92f,3.85f,15.8f,2.65f,.61f,90f);
        }
        private void BuildOverheadPiping()
        {
            for(int p=0;p<5;p++)
            {
                float x=-23.7f+p*5.58f;
                // Standing head is at 6.30m on 2F. Keep the hanging clamps (0.71m)
                // above it across both side routes and the widened maintenance bay.
                float y=7.60f+(p%2)*.36f;
                Beam("Industrial wall-longitudinal supply",new Vector3(x,y,1.1f),
                    new Vector3(x,y,24.0f),.13f,p%2==0?pipe:stainless);
                for(int i=0;i<5;i++)
                {
                    float z=3.1f+i*4.28f;
                    Beam("Industrial pipe clamp drop",new Vector3(x,y,z),
                        new Vector3(x,y-.71f,z),.055f,pipe);
                    Box("Pipe support U clamp",x,y+.12f,z,.37f,.15f,.39f,graphite);
                }
            }
            // The reference's large duct elbow above the right side of the sorting line.
            Beam("Central machine extraction duct",new Vector3(-6.00f,4.28f,15.94f),
                new Vector3(-5.70f,5.60f,16.52f),.41f,stainless);
            Beam("Machine extraction roof return",new Vector3(-5.70f,5.60f,16.52f),
                new Vector3(-3.9f,5.98f,17.10f),.38f,pipe);
            for(int i=0;i<5;i++)
            {
                float z=3.1f+i*4.30f;
                Beam("Cross-hall ceiling ventilation",new Vector3(-23.6f,10.45f,z),
                    new Vector3(-.7f,10.45f,z),.11f,stainless);
            }
        }
        private void BuildPendantLights()
        {
            for(int i=0;i<12;i++)
            {
                float x=-21.1f+(i%4)*5.70f;
                float z=3.6f+(i/4)*8.1f;
                Box("Hanging ceiling lamp cord",x,10.15f,z,.052f,2.05f,.052f,graphite);
                Cyl("Metal industrial pendant shade",new Vector3(x,9.08f,z),
                    new Vector3(.62f,.14f,.62f),graphite);
                Cyl("Frosted industrial light diffuser",new Vector3(x,8.96f,z),
                    new Vector3(.50f,.045f,.50f),lightLens);
                GameObject go=new GameObject("Bright industrial bay lamp " + i);
                go.transform.SetParent(root,false);
                go.transform.position=new Vector3(x,8.82f,z);
                Light l=go.AddComponent<Light>();
                l.type=LightType.Point;
                l.color=i%3==0?new Color(1f,.84f,.69f):new Color(.93f,.95f,1f);
                l.range=14.5f;
                l.intensity=1.30f;
                l.shadows=LightShadows.None;
                lamps.Add(l);
            }
        }
    }
}
