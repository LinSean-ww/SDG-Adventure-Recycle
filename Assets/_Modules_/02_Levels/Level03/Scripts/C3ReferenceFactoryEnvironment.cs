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
    /// Phase 2 mechanical equipment and cargo are owned by the recycling line;
    /// this component retains the warehouse, live workshop art, PVC and utilities.
    /// Coordinates are in C_Main's existing -25..0 / 0..25 hall.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class C3ReferenceFactoryEnvironment : MonoBehaviour
    {
        public const float InputBeltY = 5.8f;
        public const float InputBeltZ = 16.85f;
        public const float SorterRoofY = 4.45f;
        public const float SorterFrontZ = 12.75f;
        public const float OutfeedTopY = 1.25f;
        public const float OutputEndZ = 9.10f;
        public const float OutputStartZ = 12.75f;
        public const int LaneCount = 3;

        private Transform root;
        private C3DeviceState device;
        private Material floor, tile, steel, stainless, graphite, belt, yellow, rust,
            paper, plastic, metalBale, green, tealGlow, purpleGlow, lightLens,
            darkGlass, wall, pipe, warmWhite;
        private readonly List<Material> owned = new List<Material>();
        private readonly List<Light> lamps = new List<Light>();
        public int DecorativeObjects { get; private set; }
        public int ConveyorPackageCount => GetComponentsInChildren<EcoRecycleCargo>(true).Length;
        public bool ConveyorMoving => device && device.FactoryState >= C3FactoryState.Running && Time.timeScale>0f;
        public bool ConveyorEndsInSorter => GetComponent<C3RecyclingLineGeometry>() &&
            GetComponent<C3RecyclingLineGeometry>().ConnectionsMatch(.01f);

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
            BuildInputWasteCages();
            BuildWorkStations();
            BuildPollutionSkid();
            // V3 walkways/stairs/rails use authored meshes in C3V3Circulation.
            // BuildUpperWalkways() was an obstructing visual overlay; do not call it.
            BuildOverheadPiping();
            BuildPendantLights();
            BuildIndustrialAgeing(); // visual-only wear; preserve all gameplay colliders
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.46f, .49f, .51f);
            RenderSettings.ambientIntensity = 1.0f;
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
            foreach (Light l in lamps) if (l) l.intensity = 1.55f;
        }
        private void InitMaterials()
        {
            // Long-used but serviceable plant: faded paint and oxidized steel,
            // without making active indicators, materials or safety lanes unreadable.
            floor = Mat("Concrete floor", new Color(.49f, .50f, .47f), .05f, .13f, "FactoryConcrete");
            tile = Mat("Polished wet concrete", new Color(.52f, .52f, .47f), .12f, .29f, "FactoryConcrete");
            wall = Mat("Factory light walls", new Color(.52f, .53f, .48f), .04f, .17f, "FactoryConcrete");
            steel = Mat("Industrial painted steel", new Color(.57f, .62f, .60f), .42f, .28f, "FactorySteel");
            stainless = Mat("Galvanized sorting plant", new Color(.74f, .78f, .77f), .60f, .40f, "FactorySteel");
            graphite = Mat("Black structural steel", new Color(.22f, .25f, .25f), .40f, .28f, "FactorySteel");
            belt = Mat("Dark conveyer rubber", new Color(.36f, .38f, .37f), .02f, .14f, "FactoryRubber");
            yellow = Mat("Industrial railing yellow", new Color(.82f, .62f, .19f), .24f, .22f);
            rust = Mat("Oxide equipment", new Color(.36f, .23f, .15f), .20f, .20f, "FactorySteel");
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
        private GameObject Cyl(string name,Vector3 pos,Vector3 scale,Material mat,bool solid=false)
        {
            // A primitive Cylinder starts with a CapsuleCollider. On a shallow
            // flange its minimum diameter can exceed the visible height. Use a
            // small local box proxy matching this cylinder mesh's unit bounds;
            // never leave a hidden, tall capsule around the flat flange.
            GameObject g=Shape(name,PrimitiveType.Cylinder,pos,scale,mat);
            if (solid) g.AddComponent<BoxCollider>().size=new Vector3(1f,2f,1f);
            return g;
        }
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
            // interactables remain live; Phase 2 owns material transport and accounting.
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
            if (oldSimulation) { oldSimulation.SetActive(false); Destroy(oldSimulation); }
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
                    // Full-height warehouse posts are embedded in the wall plane;
                    // distinct below-deck columns carry the walkway cantilevers.
                    float x=side==0?-24.70f:-.12f;
                    Box("WAREHOUSE structural column",x,6.05f,z,.18f,10.95f,.22f,graphite);
                    Box("Yellow column kickplate",x,.87f,z,.22f,.48f,.26f,yellow);
                }
                Box("WAREHOUSE top roof truss",-12.41f,11.45f,z,24.58f,.28f,.29f,steel);
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
                    // Clip the panel's full extent to the real doorway, not just
                    // its center. The z=10.3 panel otherwise hides the pedestrian
                    // opening at z=10.7 even after the shutter is removed.
                    float nearZ=z-1.23f, farZ=z+1.23f;
                    if (z<12.5f) farZ=Mathf.Min(farZ,10.2f);
                    else nearZ=Mathf.Max(nearZ,14.8f);
                    Box("Sidewall weathered concrete inset",-.37f,4.2f,(nearZ+farZ)*.5f,
                        .10f,6.2f,farZ-nearZ,wall);
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
            // V11: the shared B/C doorway admits pedestrians in both directions.
            // Retain the scene's jambs, lintel, floor and outer safety colliders;
            // the separate B feed remains at z=12.5, north of the z=10.7 aisle.
            BuildBCPvcCurtain();
            // Both transitions retain named, legible signs on their real doors.
            // V12's real level deck clears the doorway. Seat its nameplate
            // below the deck underside (4.32), above the PVC header (3.58).
            // Offset 0.2m along the opening so the Z=13.75 support column
            // also clears the full right edge of the nameplate.
            Sign("BZone",-.51f,3.85f,12.30f,2.55f,.54f,90f);
            Sign("DZone",-12.50f,4.38f,.44f,2.55f,.54f);
        }
        private void BuildBCPvcCurtain()
        {
            // Built-in render pipeline, as configured by this project. Standard's
            // transparent mode needs blending and ZWrite settings as well as alpha.
            Material pvc = Mat("V11 translucent blue PVC", new Color(.58f,.84f,.91f,.26f), 0f, .68f);
            pvc.SetFloat("_Mode", 3f);
            pvc.SetOverrideTag("RenderType", "Transparent");
            pvc.SetInt("_SrcBlend", (int)BlendMode.One);
            pvc.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            pvc.SetInt("_ZWrite", 0);
            pvc.DisableKeyword("_ALPHATEST_ON");
            pvc.DisableKeyword("_ALPHABLEND_ON");
            pvc.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            pvc.renderQueue = (int)RenderQueue.Transparent;

            Transform curtain = new GameObject("V11_BC_PVC_CURTAIN").transform;
            curtain.SetParent(root, false);
            // Existing wall opening: z=10.2..14.8, floor y=.55, lintel y=3.5.
            // The header occupies the lintel; strips stop .07m above the floor.
            GameObject header = Box("HEADER_RAIL",-.30f,3.54f,12.50f,.10f,.08f,4.60f,steel);
            header.transform.SetParent(curtain, true);
            const int stripCount = 20;
            const float pitch = .23f;
            for (int i=0;i<stripCount;i++)
            {
                // .25m strips at .23m pitch overlap .02m. Alternate their depth
                // to avoid coplanar overlap. Thin cubes have faces towards BOTH
                // zones, so no custom mesh, two-sided shader or material clone.
                GameObject strip = Box("PVC_STRIP_"+i.ToString("D2"),
                    -.30f+(i%2==0?-.008f:.008f),2.06f,10.315f+i*pitch,
                    .012f,2.88f,.25f,pvc);
                strip.transform.SetParent(curtain, true);
                Renderer renderer = strip.GetComponent<Renderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            // Box defaults to solid=false: primitive colliders are disabled
            // immediately and destroyed by Shape, including on the header.
            // Awake builds once; scene ownership and owned materials handle reload.
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
        private void BuildInputWasteCages()
        {
            System.Random rnd=new System.Random(31528);
            for(int c=0;c<3;c++)
            {
                float x=-19.50f+c*2.55f;
                float z=22.10f;
                Box("SORTING BIN - input refuse floor",x,.78f,z,1.18f,.18f,2.16f,steel,true);
                for(int side=-1;side<=1;side+=2)
                for(int end=-1;end<=1;end+=2)
                    Box("Refuse cage metal upright",x+side*.57f,1.27f,z+end*1.01f,
                        .07f,.98f,.06f,graphite,true);
                for(int h=0;h<2;h++)
                for(int side=-1;side<=1;side+=2)
                {
                    Box("Bin lateral welded rail",x,1.00f+h*.56f,z+side*1.02f,
                        1.20f,.065f,.06f,stainless,true);
                    Box("Bin end welded rail",x+side*.60f,1.00f+h*.56f,z,
                        .07f,.06f,2.08f,stainless,true);
                }
                for(int k=0;k<18;k++)
                {
                    float px=x-.38f+(k%4)*.25f;
                    float pz=z-.74f+(k/4)*.35f;
                    string[] samples={"Paper_FoldedCarton","Metal_CrushedCan","Plastic_CrushedBottle"};
                    string[] kinds={"Paper","Metal","Plastic"};
                    int lane=(k+c)%3;
                    Transform visual=new GameObject("Caged recycled packaging / DECOR_ONLY").transform;
                    visual.SetParent(root,false);
                    visual.SetPositionAndRotation(new Vector3(px,1.02f+((k%3)*.14f),pz),
                        Quaternion.Euler(0f,(float)rnd.NextDouble()*90f,0));
                    visual.localScale=Vector3.one*1.7f;
                    foreach(string suffix in new[]{"_Body","_Details"})
                    {
                        GameObject mesh=Resources.Load<GameObject>("RecyclingArt/Meshes/"+samples[lane]+suffix);
                        if(!mesh){Debug.LogError("Missing decorative waste model: "+samples[lane]+suffix);continue;}
                        GameObject detail=Instantiate(mesh,visual,false);
                        foreach(Renderer renderer in detail.GetComponentsInChildren<Renderer>())
                            renderer.sharedMaterial=Resources.Load<Material>("RecyclingArt/Materials/Raw_"+kinds[lane]);
                    }
                    ++DecorativeObjects;
                }
            }
        }
        private void BuildWorkStations()
        {
            // The four original stations receive scene-owned 3D art in C3WorkstationVisualUpgrade.
            // The actual checking and crafting benches are positioned by
            // EcoFactoryWorkshop at ground level, in front of the machine bank.
            // EcoFactoryWorkshop already fixes small plates directly on the two real benches.
            // Do NOT add a second pair of giant suspended signs above them.
            // Give the real benches physical visual context; glowing status rails
            // are purely cosmetic and never duplicate the F interaction target.
            // Screenshot 03's freestanding black bar, cyan panel and two legs
            // were cosmetic duplicates in front of D. The REAL inspection and
            // weapon benches (including their interaction components) remain.
            for(int side=-1;side<=1;side+=2)
            {
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
                2.90f,.23f,5.46f,graphite,true);
            for(int i=0;i<3;i++)
            {
                float z=15.88f+i*1.56f;
                Cyl("POLLUTION stainless purification vessel",
                    new Vector3(-2.70f,1.82f,z),new Vector3(.54f,1.00f,.54f),stainless,true);
                Cyl("Purifier flange lower",new Vector3(-2.70f,.88f,z),
                    new Vector3(.61f,.09f,.61f),graphite,true);
                Cyl("Purifier flange upper",new Vector3(-2.70f,2.78f,z),
                    new Vector3(.62f,.08f,.62f),pipe,true);
                BuildPurifierExhaust(z);
                if(i>0)
                    Beam("POLLUTION connected pressurized manifold",
                        new Vector3(-2.70f,3.34f,z-1.56f),
                        new Vector3(-2.70f,3.34f,z),.08f,pipe);
                Box("POLLUTION pressure controller",-1.63f,1.64f,z,
                    .39f,.69f,.42f,steel,true);
                Box("POLLUTION controller mounting arm",-2.13f,1.64f,z,
                    .61f,.08f,.10f,steel,true);
                Box("Scrubber status screen",-1.84f,1.66f,z,
                    .04f,.26f,.27f,tealGlow);
            }
            BuildPurifierExhaustHeader();
            Sign("PollutionControl",-1.92f,3.85f,15.8f,2.65f,.61f,90f);
        }
        private const float ExhaustWallX=-.50f,ExhaustOffsetY=3.60f,ExhaustHeaderY=7.80f;
        private const float ExhaustDiameter=.105f;
        private void BuildPurifierExhaust(float z)
        {
            // User identified the Z=19 riser: the old Y=2.8..6.72 pipe cut
            // through the rear deck at Y=4.32..4.50. All three vessels now
            // offset BELOW that deck, then rise OUTSIDE its east guard.
            Vector3 source=new Vector3(-2.70f,2.80f,z);
            Vector3 low=new Vector3(source.x,ExhaustOffsetY,z);
            Vector3 wallLow=new Vector3(ExhaustWallX,ExhaustOffsetY,z);
            Vector3 wallHigh=new Vector3(ExhaustWallX,ExhaustHeaderY,z);
            Beam("Purifier connected exhaust riser",source,low,ExhaustDiameter,pipe);
            Beam("Purifier under-deck exhaust offset",low,wallLow,ExhaustDiameter,pipe);
            Beam("Purifier outboard exhaust riser",wallLow,wallHigh,ExhaustDiameter,pipe);
            foreach (Vector3 joint in new[] {low,wallLow,wallHigh})
                Shape("Purifier exhaust elbow coupling",PrimitiveType.Sphere,joint,Vector3.one*.12f,pipe);
            // Pipe/fittings stay east of the rail's X=-.61 outer face and
            // west of the wall's X=-.42 inner face. Only this small support
            // backplate intentionally seats into the wall, above the rail.
            Box("Purifier exhaust wall clamp",-.455f,6.60f,z,.11f,.12f,.15f,steel);
        }
        private void BuildPurifierExhaustHeader()
        {
            Vector3 first=new Vector3(ExhaustWallX,ExhaustHeaderY,15.88f);
            Vector3 turn=new Vector3(ExhaustWallX,ExhaustHeaderY,20.30f);
            Vector3 roof=new Vector3(ExhaustWallX,10.45f,20.30f);
            Vector3 outlet=new Vector3(-.70f,10.45f,20.30f);
            Beam("Purifier exhaust common header",first,turn,ExhaustDiameter,pipe);
            Beam("Purifier exhaust roof riser",turn,roof,ExhaustDiameter,pipe);
            Beam("Purifier exhaust roof connection",roof,outlet,ExhaustDiameter,pipe);
            foreach (Vector3 joint in new[] {turn,roof})
                Shape("Purifier exhaust roof elbow",PrimitiveType.Sphere,joint,Vector3.one*.12f,pipe);
            // Join the existing Cross-hall ceiling ventilation at Z=20.30,
            // Y=10.45, X=-.70; this is a connected exhaust, not a blind stub.
            Beam("Purifier exhaust roof sleeve",outlet-Vector3.right*.06f,outlet+Vector3.right*.06f,.14f,pipe);
        }
        private void BuildOverheadPiping()
        {
            for(int p=0;p<5;p++)
            {
                float x=p==0?-24.55f:p==4?-.15f:-23.7f+p*5.58f;
                // Outer risers move over the wall; lowest hangers are now 8.09m,
                // above the 6.30m standing head and the elevated material bridge.
                float y=8.80f+(p%2)*.36f;
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
            Beam("Central machine extraction duct",new Vector3(-5.80f,4.28f,15.94f),
                new Vector3(-5.80f,8.65f,15.94f),.41f,stainless);
            Beam("Machine extraction roof return",new Vector3(-5.80f,8.65f,15.94f),
                new Vector3(-3.9f,9.20f,17.10f),.38f,pipe);
            for(int i=0;i<5;i++)
            {
                float z=3.1f+i*4.30f;
                Beam("Cross-hall ceiling ventilation",new Vector3(-23.6f,10.45f,z),
                    new Vector3(-.7f,10.45f,z),.11f,stainless);
            }
        }
        // Every weathering mark is built through Shape(solid:false), which
        // disables its collider immediately. These are thin surface marks, not
        // a second factory layout, and never sit across the B/C or D doorway.
        private void AgeMark(string name, Vector3 position, Vector3 dimensions, Material material,
            float yaw = 0f)
        {
            GameObject mark = Shape("V11_AGE_" + name, PrimitiveType.Cube,
                position, dimensions, material, Quaternion.Euler(0f, yaw, 0f));
            Renderer renderer = mark.GetComponent<Renderer>();
            if (renderer)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private void BuildIndustrialAgeing()
        {
            Material oxidation = Mat("V11 aged oxide streak", new Color(.29f, .17f, .105f), .06f, .11f);
            Material soot = Mat("V11 aged wall soot", new Color(.29f, .30f, .27f), .01f, .07f);
            Material oilFilm = Mat("V11 aged oil film", new Color(.19f, .22f, .21f), .05f, .13f);
            Material paintChip = Mat("V11 aged chipped enamel", new Color(.40f, .43f, .38f), .11f, .17f);
            System.Random random = new System.Random(150922); // stable screenshots and layout

            // Inside west wall, rust drips from the aged cladding seams.
            // Its visible face is x=-24.58; marks are 1-2cm inside the hall.
            for (int i = 0; i < 19; i++)
            {
                float z = 1.9f + i * 1.18f;
                float y = 1.35f + (i % 5) * .91f;
                AgeMark("WALL_W_" + i, new Vector3(-24.566f, y, z),
                    new Vector3(.012f, .31f + (i % 4) * .14f, .13f + (i % 3) * .085f),
                    i % 3 == 0 ? soot : oxidation);
            }
            // East wall has a real B/C passage at z=10.2..14.8: leave it open.
            for (int i = 0; i < 12; i++)
            {
                float z = 2.1f + i * 1.95f;
                if (z > 9.9f && z < 15.1f) continue;
                AgeMark("WALL_E_" + i,
                    new Vector3(-.433f, 1.55f + (i % 4) * 1.1f, z),
                    new Vector3(.010f, .37f + (i % 3) * .19f, .16f + (i % 2) * .12f),
                    i % 4 == 0 ? soot : oxidation);
            }
            // North end wall: uneven water runs on the concrete, not a solid panel.
            for (int i = 0; i < 12; i++)
            {
                float x = -22.6f + i * 1.79f;
                AgeMark("WALL_N_" + i,
                    new Vector3(x, 2.25f + (i % 4) * .85f, 24.585f),
                    new Vector3(.17f + (i % 3) * .09f, .35f + (i % 4) * .15f, .009f),
                    i % 3 == 0 ? oxidation : soot);
            }

            // Low-profile deposits lie above the finished-floor render plane.
            // All are decorative: no solid puddle, debris mesh or physics blocker.
            for (int i = 0; i < 34; i++)
            {
                float x = -22.8f + (float)random.NextDouble() * 20.1f;
                float z = 1.6f + (float)random.NextDouble() * 21.4f;
                float sx = .20f + (float)random.NextDouble() * .58f;
                float sz = .13f + (float)random.NextDouble() * .40f;
                AgeMark("FLOOR_OIL_" + i,
                    new Vector3(x, .5825f, z), new Vector3(sx, .003f, sz),
                    i % 4 == 0 ? oxidation : oilFilm, (i * 47) % 180);
            }
            // Visible scuffed areas at the service bays, away from pickup rays.
            for (int i = 0; i < 12; i++)
            {
                float x = -21.9f + i * 1.69f;
                AgeMark("FLOOR_SCUFF_" + i,
                    new Vector3(x, .5828f, i % 2 == 0 ? 6.60f : 19.55f),
                    new Vector3(.38f, .003f, .085f), paintChip, (i % 3 - 1) * 11f);
            }
            // Aged collars on the high wall pipe supports. All remain above
            // the elevated walkway and never obstruct stairs or the camera.
            for (int i = 0; i < 10; i++)
            {
                float z = 3.1f + (i % 5) * 4.28f;
                float x = i < 5 ? -24.55f : -.15f;
                float y = 8.80f;
                AgeMark("PIPE_" + i, new Vector3(x, y - .13f, z),
                    new Vector3(.21f, .065f, .20f), oxidation);
            }
            // Worn kick-plates embedded against wall-side structural columns.
            for (int i = 0; i < 12; i++)
            {
                float z = 1.0f + (i % 6) * 3.75f;
                float x = i < 6 ? -24.578f : -.250f;
                if (i >= 6 && Mathf.Abs(z - 12.5f) < 1f) continue;
                AgeMark("COLUMN_" + i, new Vector3(x, .91f, z),
                    new Vector3(.012f, .36f, .13f), i % 3 == 0 ? paintChip : oxidation);
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
