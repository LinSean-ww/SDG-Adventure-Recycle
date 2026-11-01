using UnityEngine;

namespace C3TreasurePuzzle
{
    // Runtime-built hidden workshop. It reuses the existing paper/metal/plastic
    // sorter and does not change the educational puzzle or its bins.
    [DisallowMultipleComponent]
    public sealed class EcoFactoryWorkshop : MonoBehaviour
    {
        [SerializeField] private C3DeviceState device;
        [SerializeField] private C3FactoryPresentation presentation;
        private Transform workshopRoot;
        private Transform coreRotor;
        private Light coreLight;
        private bool powerEstablished;

        public bool Powered => device != null &&
            (device.FactoryState == C3FactoryState.Running ||
             device.FactoryState == C3FactoryState.Completed);

        private void Awake()
        {
            if (!device) device = GetComponent<C3DeviceState>();
            if (!presentation) presentation = GetComponent<C3FactoryPresentation>();
            BuildWorkshop();
        }

        // If the sorter wasn't yet discoverable when the component was added in Awake,
        // retry after all scene objects have completed Awake before spawning pickups.
        private void Start()
        {
            if (workshopRoot == null) BuildWorkshop();
        }

        private void OnEnable()
        {
            if (!device) device = GetComponent<C3DeviceState>();
            if (device != null) device.Running += OnRunning;
        }

        private void OnDisable()
        {
            if (device != null) device.Running -= OnRunning;
        }

        private void OnRunning()
        {
            powerEstablished = true;
            if (EcoAdventureRuntime.Instance != null)
                EcoAdventureRuntime.Instance.FactoryPoweredOn();
        }

        private void Update()
        {
            if (Powered && !powerEstablished) OnRunning();
            if (coreLight != null)
            {
                coreLight.intensity = Powered
                    ? 1.8f + Mathf.Sin(Time.time * 3f) * 0.25f : 0.12f;
                if (coreRotor != null)
                    coreRotor.Rotate(Vector3.up, Powered ? 75f * Time.deltaTime : 0f);
            }
            // Material credits are committed by the authoritative Phase 2 batch
            // transaction. Never infer a material type from a cycle counter.
        }

        private void BuildWorkshop()
        {
            GameObject sorter = GameObject.Find("WasteSortingLine");
            if (sorter == null)
            {
                Debug.LogWarning("EcoFactoryWorkshop: WasteSortingLine not found.");
                return;
            }
            GameObject root = new GameObject("EASTER_EGG_EcoQuantumWorkshop");
            // The reference has the quantum assembly at the front-right,
            // independent of the rear sorting machine's transform.
            root.transform.position = new Vector3(-6.80f, 0.58f, 3.05f);
            root.transform.rotation = Quaternion.identity;
            workshopRoot = root.transform;
            Material steel = MakeMaterial("SalvagedSteel", new Color(0.20f,0.25f,0.24f));
            Material rust = MakeMaterial("RustyFrame", new Color(0.28f,0.19f,0.13f));
            Material energy = MakeMaterial("QuantumGlow", new Color(0.10f,0.93f,0.81f), true);
            Material plastic = MakeMaterial("RecycledPlastic", new Color(0.09f,0.12f,0.14f));

            GameObject bench = new GameObject("Hidden_WeaponAssemblyBench_F");
            bench.transform.SetParent(workshopRoot, false);
            bench.transform.localPosition = Vector3.zero;
            EcoWorkshopInteractable action = bench.AddComponent<EcoWorkshopInteractable>();
            action.Configure(this, EcoWorkshopInteractable.StationKind.Workbench);

            BoxCollider interaction = bench.AddComponent<BoxCollider>();
            interaction.center = new Vector3(0f, 0.70f, 0f);
            interaction.size = new Vector3(1.85f, 1.4f, 1.06f);
            for (int i = -1; i <= 1; i += 2)
            {
                Shape(PrimitiveType.Cube, "RustyWorkbenchLeg", bench.transform,
                    new Vector3(i * 0.77f, 0.44f, -0.3f),
                    new Vector3(0.12f,0.82f,0.12f), rust);
                Shape(PrimitiveType.Cube, "RustyWorkbenchLeg", bench.transform,
                    new Vector3(i * 0.77f, 0.44f, 0.3f),
                    new Vector3(0.12f,0.82f,0.12f), rust);
            }
            Shape(PrimitiveType.Cube, "RecycledSteelWorksurface", bench.transform,
                new Vector3(0f, 0.84f, 0f), new Vector3(1.75f, 0.10f, 0.95f), steel);
            Shape(PrimitiveType.Cube, "DisusedAssemblyCabinet", bench.transform,
                new Vector3(-0.63f, 1.12f, -0.29f), new Vector3(0.37f,0.52f,0.30f), rust);
            Shape(PrimitiveType.Cube, "QuantumAssemblyRail", bench.transform,
                new Vector3(0.17f, 1.00f, 0f), new Vector3(0.68f,0.09f,0.25f), plastic);
            coreRotor = Shape(PrimitiveType.Cylinder, "SalvagedPowerCore", bench.transform,
                new Vector3(0.14f, 1.12f, 0f), new Vector3(0.16f,0.08f,0.16f), energy).transform;
            Shape(PrimitiveType.Cube, "RecoveredMetalMagazine", bench.transform,
                new Vector3(0.52f,1.00f,0f), new Vector3(0.17f,0.17f,0.26f), steel);
            Shape(PrimitiveType.Cube, "RecycleInstructionPlate", bench.transform,
                new Vector3(0.0f,0.91f,0.46f), new Vector3(1.50f,0.10f,0.06f), rust);
            coreLight = new GameObject("ReactorBlueGlow").AddComponent<Light>();
            coreLight.transform.SetParent(bench.transform, false);
            coreLight.transform.localPosition = new Vector3(0.1f,1.25f,0f);
            coreLight.type = LightType.Point;
            coreLight.color = new Color(0.20f,1.0f,0.88f);
            coreLight.range = 2.5f;
            coreLight.intensity = 0.12f;
            coreLight.shadows = LightShadows.None;
            // Keep text on an actual metal nameplate, with a readable face on BOTH sides.
            // Do not leave a giant world-space label floating above the workbench.
            BuildDoubleSidedNameplate(bench.transform, "Assembly", rust,
                new Vector3(0f, 1.40f, -0.39f), 1.55f);

            // A second, independent worktable for checking material readiness.
            // It does not dispense resources or replace the assembly table.
            BuildInspectionWorkbench(steel, rust, plastic, energy);

            // Material depots and energy salvage are scattered across the *factory*,
            // NOT placed in a single line in front of the original sorting bins.
            BuildScatteredResources(steel, rust, plastic, energy);
        }

        // This is the SECOND physical workbench. It is purposefully offset from the
        // first table, not stacked on top of it, and is operational via the F key.
        private void BuildInspectionWorkbench(Material steel, Material rust,
            Material plastic, Material energy)
        {
            GameObject desk = new GameObject("Secondary_RecyclingInspectionWorkbench_F");
            desk.transform.SetParent(workshopRoot, false);
            // Actual interactive inspection table is front-center.
            desk.transform.localPosition = new Vector3(-8.10f, 0f, 0.0f);
            desk.transform.localRotation = Quaternion.identity;
            EcoWorkshopInteractable interactable = desk.AddComponent<EcoWorkshopInteractable>();
            interactable.Configure(this, EcoWorkshopInteractable.StationKind.InspectionWorkbench);

            // The body, tabletop, lower shelf and equipment make this a distinct
            // reachable work surface rather than another floating sign.
            BoxCollider area = desk.AddComponent<BoxCollider>();
            area.center = new Vector3(0f, 0.73f, 0f);
            area.size = new Vector3(1.72f, 1.48f, 1.02f);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int depth = -1; depth <= 1; depth += 2)
                {
                    Shape(PrimitiveType.Cube, "InspectionTableSteelLeg", desk.transform,
                        new Vector3(side * 0.69f, 0.40f, depth * 0.33f),
                        new Vector3(0.11f, 0.76f, 0.11f), rust);
                }
            }
            Shape(PrimitiveType.Cube, "InspectionTableWorktop", desk.transform,
                new Vector3(0f, 0.83f, 0f), new Vector3(1.58f, 0.09f, 0.89f), steel);
            Shape(PrimitiveType.Cube, "InspectionTableLowerShelf", desk.transform,
                new Vector3(0f, 0.27f, 0f), new Vector3(1.43f, 0.065f, 0.72f), rust);
            Shape(PrimitiveType.Cube, "SalvagePartsSortingTray", desk.transform,
                new Vector3(-0.32f, 0.95f, 0.10f), new Vector3(0.56f, 0.13f, 0.43f), plastic);
            Shape(PrimitiveType.Cube, "IndustrialTestingMeter", desk.transform,
                new Vector3(0.41f, 1.07f, -0.15f), new Vector3(0.37f, 0.35f, 0.31f), rust);
            Shape(PrimitiveType.Cube, "MeterScreen", desk.transform,
                new Vector3(0.41f, 1.10f, 0.024f), new Vector3(0.26f, 0.19f, 0.018f), energy);
            for (int i = 0; i < 3; i++)
            {
                Shape(PrimitiveType.Cylinder, "RecycledCalibrationPart_" + i,
                    desk.transform, new Vector3(-0.50f + i * 0.18f, 1.04f, 0.10f),
                    new Vector3(0.057f, 0.070f, 0.057f), i == 2 ? plastic : steel);
            }
            BuildDoubleSidedNameplate(desk.transform, "Inspection", rust,
                new Vector3(0f, 1.40f, -0.38f), 1.49f);
        }

        // A pair of physically mounted plates; the two baked faces are always readable.
        private static void BuildDoubleSidedNameplate(Transform desk,
            string textureName, Material rust, Vector3 location, float width)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                Shape(PrimitiveType.Cube, "NameplateFixedSupport", desk,
                    location + new Vector3(side * (width * 0.43f), -0.29f, 0f),
                    new Vector3(0.045f, 0.52f, 0.045f), rust);
            }
            C3BakedSignFactory.AtAnchor(desk, textureName, location,
                Quaternion.identity, width, 0.34f);
        }

        // This desk provides an inventory and sorting-status report, without
        // handing out crafting items or bypassing the existing classification puzzle.
        public void InspectMaterials()
        {
            EcoAdventureRuntime game = EcoAdventureRuntime.Instance;
            if (game == null) return;
            if (!Powered)
            {
                game.Notify("材料檢測台尚未供電，請先啟動工廠");
                return;
            }
            if (game.weaponCrafted)
            {
                game.Notify("材料檢測完成：量子武器已組裝成功");
                return;
            }
            string metalStatus = game.Has(EcoAdventureRuntime.MetalId)
                ? "已拾取" : game.HasSortedMaterial(1) ? "可尋找" : "未完成分類";
            string plasticStatus = game.Has(EcoAdventureRuntime.PlasticId)
                ? "已拾取" : game.HasSortedMaterial(0) ? "可尋找" : "未完成分類";
            string circuitStatus = game.Has(EcoAdventureRuntime.CircuitId)
                ? "已拾取" : "待尋找";
            game.Notify("材料檢測結果\n" +
                "再生金屬｜東側：" + metalStatus + "\n" +
                "再生塑膠｜西側：" + plasticStatus + "\n" +
                "廢電路板｜西北側：" + circuitStatus + "\n" +
                "前往上述區域尋找材料箱，靠近並按 F 拾取。", 8f);
        }

        private void BuildScatteredResources(Material steel, Material rust,
            Material plastic, Material energy)
        {
            GameObject scatter = new GameObject("C3_Distributed_Materials_And_Energy");
            Transform parent = scatter.transform; // scene-owned world space

            // Positions are within the C-zone industrial hall (x = -25..0,
            // z = 0..24). Keep walking corridors and the original conveyor clear.
            // Separate metal, plastic and electronics so the player must explore.
            BuildMaterialCache(parent, "Hidden_ElectronicScrapCache_F",
                EcoWorkshopInteractable.StationKind.CircuitMaterial,
                new Vector3(-22.8f, 0.58f, 22.0f), steel, rust, energy);
            BuildMaterialCache(parent, "Distributed_RecycledPlastic_F",
                EcoWorkshopInteractable.StationKind.PlasticMaterial,
                new Vector3(-19.9f, 0.58f, 8.1f), plastic, rust, energy);
            BuildMaterialCache(parent, "Distributed_RecycledMetal_F",
                EcoWorkshopInteractable.StationKind.MetalMaterial,
                new Vector3(-4.90f, 0.58f, 10.6f), steel, rust, energy);

            // Nine independent recharge objects, distributed in three search zones.
            // No pile around the sorter, and no auto-recharge without weapon in hand.
            EcoEnergyPickup.SalvageKind[] kinds =
            {
                EcoEnergyPickup.SalvageKind.PlasticBottle,
                EcoEnergyPickup.SalvageKind.MetalCan,
                EcoEnergyPickup.SalvageKind.WastePaper,
                EcoEnergyPickup.SalvageKind.PlasticBottle,
                EcoEnergyPickup.SalvageKind.MetalCan,
                EcoEnergyPickup.SalvageKind.WastePaper,
                EcoEnergyPickup.SalvageKind.PlasticBottle,
                EcoEnergyPickup.SalvageKind.MetalCan,
                EcoEnergyPickup.SalvageKind.WastePaper
            };
            Vector3[] positions =
            {
                new Vector3(-21.4f, 0.58f, 4.2f),
                new Vector3(-5.5f, 0.58f, 4.1f),
                new Vector3(-15.2f, 0.58f, 19.2f),
                new Vector3(-4.0f, 0.58f, 22.3f),
                new Vector3(-19.6f, 0.58f, 9.1f),
                new Vector3(-4.95f, 0.58f, 7.4f),
                new Vector3(-11.2f, 0.58f, 21.1f),
                new Vector3(-8.0f, 0.58f, 8.3f),
                new Vector3(-19.5f, 0.58f, 22.0f)
            };
            int[] count = new int[3];
            for (int i = 0; i < kinds.Length; i++)
            {
                int index = (int)kinds[i];
                BuildEnergyPickup(parent, kinds[i], positions[i], count[index]++,
                    steel, rust, plastic, energy);
            }
        }

        private void BuildMaterialCache(Transform parent, string name,
            EcoWorkshopInteractable.StationKind kind, Vector3 worldPosition,
            Material material, Material rust, Material energy)
        {
            GameObject prop = new GameObject(name);
            prop.transform.SetParent(parent, false);
            prop.transform.position = worldPosition;
            BoxCollider box = prop.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.30f, 0f);
            box.size = new Vector3(0.65f, 0.55f, 0.55f);
            EcoWorkshopInteractable interaction = prop.AddComponent<EcoWorkshopInteractable>();
            interaction.Configure(this, kind);
            PickupShape(PrimitiveType.Cube, "RustyMaterialCase", prop.transform,
                new Vector3(0f, 0.25f, 0f), new Vector3(0.58f, 0.45f, 0.48f), rust);
            PickupShape(PrimitiveType.Cube, "VisibleSalvagedMaterial", prop.transform,
                new Vector3(0f, 0.52f, 0f), new Vector3(0.39f, 0.12f, 0.34f), material);
            PickupShape(PrimitiveType.Cube, "PickupMarker", prop.transform,
                new Vector3(0f, 0.58f, 0f), new Vector3(0.30f, 0.015f, 0.25f), energy);
            // No permanent nameplate: a free-standing world sign would survive
            // after the cache is collected, and reveal the secret material from far away.
            // EcoAdventureRuntime displays a short nearby-only prompt instead.
            Light clue = new GameObject("NearbyMaterialLocatorLight").AddComponent<Light>();
            clue.transform.SetParent(prop.transform, false);
            clue.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            clue.type = LightType.Point;
            clue.color = new Color(0.10f, 0.80f, 0.70f);
            clue.range = 2.2f;
            clue.intensity = 0.75f;
            clue.shadows = LightShadows.None;
            Color hint = kind == EcoWorkshopInteractable.StationKind.CircuitMaterial ? new Color(.2f,.8f,1f) :
                kind == EcoWorkshopInteractable.StationKind.MetalMaterial ? new Color(1f,.65f,.2f) : new Color(.2f,1f,.4f);
            prop.AddComponent<C3ProximityHint>().Configure(hint, clue);
        }

        private static void BuildEnergyPickup(Transform parent, EcoEnergyPickup.SalvageKind kind,
            Vector3 position, int row, Material steel, Material rust, Material plastic, Material energy)
        {
            GameObject prop = new GameObject("EnergySalvage_" + kind + "_" + (row + 1));
            prop.transform.SetParent(parent, false);
            prop.transform.localPosition = position;
            prop.transform.localRotation = Quaternion.Euler(0f, 25f * row, 0f);
            // Auto pickup uses distance; a trigger never blocks movement or the aim ray.
            BoxCollider collider = prop.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.23f, 0f);
            collider.size = new Vector3(0.38f, 0.45f, 0.38f);
            collider.isTrigger = true;
            EcoEnergyPickup pickup = prop.AddComponent<EcoEnergyPickup>();
            pickup.Configure(kind);

            prop.AddComponent<C3SalvageAppearance>();
        }

        private static void PickupShape(PrimitiveType kind, string name, Transform parent,
            Vector3 position, Vector3 scale, Material material)
        {
            GameObject shape = Shape(kind, name, parent, position, scale, material);
            Collider childCollider = shape.GetComponent<Collider>();
            if (childCollider != null) childCollider.enabled = false;
        }

        private static Material MakeMaterial(string name, Color color, bool glow = false)
        {
            Shader shader = Shader.Find("Standard");
            Material material = new Material(shader != null ? shader : Shader.Find("Diffuse"));
            material.name = name;
            material.color = color;
            if (glow && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 2f);
            }
            return material;
        }

        private static GameObject Shape(PrimitiveType kind, string name, Transform parent,
            Vector3 position, Vector3 scale, Material material)
        {
            GameObject obj = GameObject.CreatePrimitive(kind);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = scale;
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            return obj;
        }

    }
}
