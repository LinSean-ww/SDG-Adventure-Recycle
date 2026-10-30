using System.Collections;
using UnityEngine;

namespace C3TreasurePuzzle
{
    // Local visual simulation. Gameplay completion remains owned by C3DeviceState.
    public sealed class C3FactoryPresentation : MonoBehaviour
    {
        [SerializeField] private C3DeviceState device;
        [SerializeField] private Transform sortingLine;
        [SerializeField, Min(0.1f)] private float lightRestoreSeconds = 2f;
        [SerializeField, Min(0.1f)] private float motorStartSeconds = 1f;
        [SerializeField, Min(0.1f)] private float cycleSeconds = 6f;
        [Tooltip("Adjust this C-side endpoint after the B-area connection is agreed.")]
        [SerializeField] private Vector3 bFeedEndpoint = new Vector3(-0.7f, 2.15f, 12.5f);
        [SerializeField] private Light[] factoryLights;
        private float[] lightLevels;
        private GameObject[] waste;
        private GameObject[,] sortedMarkers;
        private readonly int[] sortedCounts = new int[3];
        private Transform[] rollers;
        private Transform pusher;
        private Renderer sensorIndicator;
        private MaterialPropertyBlock sensorBlock;
        private Transform[] feedMarks;
        private Transform[] feedRollers;
        private Transform[] mainBeltSlats;
        private Transform[] machineRotors;
        private Light[] machineIndicators;
        private Renderer mainBeltRenderer;
        private Material mainBeltMaterial;
        private Vector3 feedStart;
        private Vector3 feedEnd;
        private float feedPhase;
        private bool motorActive;
        private float cycleTime;
        private int cycleIndex;
        private Coroutine startup;
        private AudioSource motorAudio;
        private Material simulationMaterial;
        private Light faultLight;
        private float faultIntensity;
        public bool MotorActive => motorActive;
        public int CompletedCycles { get; private set; }
        public int CurrentWasteKind => cycleIndex;

        private void Awake()
        {
            if (!device) device = GetComponent<C3DeviceState>();
            if (!sortingLine)
            {
                GameObject line = GameObject.Find("WasteSortingLine");
                if (line) sortingLine = line.transform;
            }
            if (factoryLights == null || factoryLights.Length == 0)
            {
                string[] names = { "Warm_EntrySafety", "Warm_WestService", "Warm_CentralReactor", "Warm_EastService", "Warm_UpperWalkway" };
                factoryLights = new Light[names.Length];
                for (int i = 0; i < names.Length; i++)
                {
                    GameObject item = GameObject.Find(names[i]);
                    if (item) factoryLights[i] = item.GetComponent<Light>();
                }
            }
            lightLevels = new float[factoryLights.Length];
            GameObject fault = GameObject.Find("Red_EntryFault");
            if (fault) faultLight = fault.GetComponent<Light>();
            if (faultLight) faultIntensity = faultLight.intensity;
            for (int i = 0; i < factoryLights.Length; i++)
            {
                if (!factoryLights[i]) continue;
                lightLevels[i] = factoryLights[i].intensity;
                factoryLights[i].intensity *= 0.45f;
            }
            if (sortingLine) BuildSimulation();
            // Scene-owned art pass. No editor-only builder or external packages required.
            // Rebuilt as a single connected assembly; do not overlay the old rust pass.
            // Install the optional eco-weapon easter egg without replacing the existing sorter.
            if (!GetComponent<EcoFactoryWorkshop>()) gameObject.AddComponent<EcoFactoryWorkshop>();
            // Reference-inspired recycling plant, attached at Play even in the untouched C_Main scene.
            // The earlier equipment overlay created disconnected, duplicated machines.
            if (!GetComponent<C3ReferenceFactoryEnvironment>()) gameObject.AddComponent<C3ReferenceFactoryEnvironment>();
            if (!GetComponent<C3V3Circulation>()) gameObject.AddComponent<C3V3Circulation>();
        }

        private void OnEnable()
        {
            if (!device) return;
            device.Activated += BeginStartup;
            if (device.FactoryState == C3FactoryState.Starting && startup == null) BeginStartup();
            if (motorActive && motorAudio && motorAudio.clip && !motorAudio.isPlaying) motorAudio.Play();
        }
        private void OnDisable()
        {
            if (device) device.Activated -= BeginStartup;
            if (startup != null) StopCoroutine(startup);
            startup = null;
            if (motorAudio) motorAudio.Stop();
        }
        private void OnDestroy()
        {
            if (simulationMaterial) Destroy(simulationMaterial);
            if (mainBeltMaterial) Destroy(mainBeltMaterial);
        }

        private void BeginStartup()
        {
            if (startup == null) startup = StartCoroutine(StartupSequence());
        }

        private IEnumerator StartupSequence()
        {
            float elapsed = 0f;
            while (elapsed < lightRestoreSeconds)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / lightRestoreSeconds);
                if (faultLight) faultLight.intensity = faultIntensity * (1f - progress);
                for (int i = 0; i < factoryLights.Length; i++)
                {
                    if (!factoryLights[i]) continue;
                    float local = Mathf.Clamp01(progress * 1.8f - i * 0.16f);
                    factoryLights[i].intensity = lightLevels[i] * Mathf.Lerp(0.45f, 1f, local);
                }
                yield return null;
            }
            for (int i = 0; i < factoryLights.Length; i++)
                if (factoryLights[i]) factoryLights[i].intensity = lightLevels[i];
            motorActive = true;
            if (motorAudio && motorAudio.clip && !motorAudio.isPlaying) motorAudio.Play();
            if (machineIndicators != null)
                foreach (Light indicator in machineIndicators) if (indicator) indicator.enabled = true;
            yield return new WaitForSeconds(motorStartSeconds);
            device.NotifyStartupFinished();
            startup = null;
        }

        private void Update()
        {
            if (!motorActive || rollers == null || waste == null || device == null) return;
            foreach (Transform roller in rollers) roller.Rotate(0f, -140f * Time.deltaTime, 0f, Space.Self);
            if (machineRotors != null)
                foreach (Transform rotor in machineRotors) if (rotor) rotor.Rotate(0f, 0f, 125f * Time.deltaTime, Space.Self);
            if (feedRollers != null)
                foreach (Transform roller in feedRollers) if (roller) roller.Rotate(0f, -140f * Time.deltaTime, 0f, Space.Self);
            if (mainBeltMaterial && mainBeltMaterial.HasProperty("_MainTex"))
                mainBeltMaterial.mainTextureOffset += Vector2.up * (Time.deltaTime * 0.4f);
            if (mainBeltSlats != null)
                for (int i = 0; i < mainBeltSlats.Length; i++)
                    mainBeltSlats[i].localPosition = new Vector3(Mathf.Lerp(-1.5f, 1.5f,
                        Mathf.Repeat(cycleTime * 0.35f + i / (float)mainBeltSlats.Length, 1f)), 1.39f, 0f);
            feedPhase += Time.deltaTime / 5f;
            for (int i = 0; i < feedMarks.Length; i++)
                feedMarks[i].localPosition = Vector3.Lerp(feedEnd, feedStart, Mathf.Repeat(feedPhase + i / (float)feedMarks.Length, 1f));
            cycleTime += Time.deltaTime;
            if (cycleTime >= cycleSeconds)
            {
                cycleTime %= cycleSeconds;
                int marker = Mathf.Min(sortedCounts[cycleIndex], 2);
                sortedMarkers[cycleIndex, marker].SetActive(true);
                sortedCounts[cycleIndex] = Mathf.Min(sortedCounts[cycleIndex] + 1, 3);
                CompletedCycles++;
                cycleIndex = (cycleIndex + 1) % waste.Length;
            }
            float t = cycleTime / cycleSeconds;
            for (int i = 0; i < waste.Length; i++) waste[i].SetActive(i == cycleIndex);
            // Convey to the sensor; then a pusher sends each item to its assigned container.
            Vector3 start = new Vector3(-1.2f, 1.48f, 0f);
            Vector3 sensor = new Vector3(0.15f, 1.48f, 0f);
            Vector3 destination = new Vector3(-0.85f + cycleIndex * 0.85f, 1.10f, 1.05f);
            waste[cycleIndex].transform.localPosition = t < 0.55f ? Vector3.Lerp(feedEnd, feedStart, t / 0.55f) :
                t < 0.70f ? Vector3.Lerp(feedStart, start, (t - 0.55f) / 0.15f) :
                t < 0.85f ? Vector3.Lerp(start, sensor, (t - 0.70f) / 0.15f) :
                Vector3.Lerp(sensor, destination, (t - 0.85f) / 0.15f);
            pusher.localPosition = new Vector3(0.15f, 1.5f, t > 0.85f && t < 0.96f ? -0.03f : -0.45f);
            if (sensorIndicator)
            {
                Color sensorColor = t > 0.70f && t < 0.86f ? new Color(0.55f, 0.7f, 0.35f) : new Color(0.15f, 0.20f, 0.17f);
                sensorIndicator.GetPropertyBlock(sensorBlock);
                sensorBlock.SetColor("_Color", sensorColor);
                sensorIndicator.SetPropertyBlock(sensorBlock);
            }
        }

        private void BuildSimulation()
        {
            GameObject root = new GameObject("C3_SortingSimulation");
            root.transform.SetParent(sortingLine, false);
            GameObject ambient = GameObject.Find("DormantFactoryAmbientHum");
            AudioSource ambientSource = ambient ? ambient.GetComponent<AudioSource>() : null;
            if (ambientSource && ambientSource.clip)
            {
                motorAudio = root.AddComponent<AudioSource>();
                motorAudio.clip = ambientSource.clip;
                motorAudio.loop = true;
                motorAudio.playOnAwake = false;
                motorAudio.spatialBlend = 0.8f;
                motorAudio.volume = 0.14f;
                motorAudio.pitch = 1.35f;
                motorAudio.maxDistance = 15f;
            }
            Shader shader = Shader.Find("Standard");
            if (shader)
            {
                simulationMaterial = new Material(shader) { name = "C3_Simulation_Material" };
                simulationMaterial.SetFloat("_Metallic", 0.15f);
                simulationMaterial.SetFloat("_Glossiness", 0.25f);
            }
            Material material = simulationMaterial ? simulationMaterial : sortingLine.GetComponentInChildren<Renderer>()?.sharedMaterial;
            Renderer beltRenderer = sortingLine.Find("Rubber_SortingBelt")?.GetComponent<Renderer>();
            mainBeltRenderer = beltRenderer;
            if (beltRenderer && beltRenderer.sharedMaterial)
            {
                mainBeltMaterial = new Material(beltRenderer.sharedMaterial);
                mainBeltMaterial.name = "C3_RunningBelt_Instance";
                beltRenderer.sharedMaterial = mainBeltMaterial;
            }
            GameObject rustObject = GameObject.Find("Rust_WasteContainer_ABody");
            Renderer rustRenderer = rustObject ? rustObject.GetComponent<Renderer>() : null;
            GameObject steelObject = GameObject.Find("DarkServicePillar");
            Renderer steelRenderer = steelObject ? steelObject.GetComponent<Renderer>() : null;
            Material rubber = beltRenderer ? beltRenderer.sharedMaterial : material;
            Material rust = rustRenderer ? rustRenderer.sharedMaterial : material;
            Material steel = steelRenderer ? steelRenderer.sharedMaterial : rust;
            AddFloorWear(root.transform);
            BuildFeed(root.transform, rubber, steel);
            mainBeltSlats = new Transform[9];
            for (int i = 0; i < mainBeltSlats.Length; i++)
            {
                GameObject slat = Shape(PrimitiveType.Cube, "主分選帶移動橫紋", root.transform,
                    new Vector3(-1.5f + i * 0.34f, 1.39f, 0f), new Vector3(0.055f, 0.018f, 1.05f),
                    steel, new Color(0.15f, 0.16f, 0.15f));
                mainBeltSlats[i] = slat.transform;
            }
            rollers = new Transform[2];
            for (int i = 0; i < rollers.Length; i++)
            {
                GameObject roller = Shape(PrimitiveType.Cylinder, "Roller", root.transform,
                    new Vector3(-1.35f + i * 2.7f, 1.29f, 0), new Vector3(0.16f, 0.46f, 0.16f), material,
                    new Color(0.35f, 0.31f, 0.26f));
                roller.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                rollers[i] = roller.transform;
                Shape(PrimitiveType.Cube, "RollerMark", roller.transform, new Vector3(0f, 0f, 0.52f),
                    new Vector3(0.18f, 1.5f, 0.08f), material, new Color(0.7f, 0.57f, 0.28f));
            }
            pusher = Shape(PrimitiveType.Cube, "SortingPusher", root.transform, new Vector3(0.15f, 1.5f, -0.45f),
                new Vector3(0.35f, 0.18f, 0.16f), material, new Color(0.45f, 0.38f, 0.24f)).transform;
            string[] labels = { "塑膠", "金屬", "紙類" };
            Color[] colors = { new Color(0.25f, 0.40f, 0.45f), new Color(0.42f, 0.43f, 0.39f), new Color(0.48f, 0.37f, 0.24f) };
            waste = new GameObject[3];
            sortedMarkers = new GameObject[3, 3];
            for (int i = 0; i < 3; i++)
            {
                float x = -0.85f + i * 0.85f;
                Shape(PrimitiveType.Cube, labels[i] + "回收箱", root.transform, new Vector3(x, 0.78f, 1.05f),
                    new Vector3(0.65f, 0.62f, 0.58f), rust, colors[i]);
                for (int slot = 0; slot < 3; slot++)
                {
                    sortedMarkers[i, slot] = Shape(PrimitiveType.Cube, labels[i] + "已分類" + (slot + 1), root.transform,
                        new Vector3(x + (slot - 1) * 0.18f, 1.105f, 1.05f), new Vector3(0.14f, 0.04f, 0.22f), material, colors[i]);
                    sortedMarkers[i, slot].SetActive(false);
                }
                string signKey = i == 0 ? "Plastic" : i == 1 ? "Metal" : "Paper";
                C3BakedSignFactory.AtAnchor(root.transform, signKey,
                    new Vector3(x, 0.85f, 1.39f), Quaternion.identity, 0.50f, 0.21f);
                PrimitiveType kind = i == 2 ? PrimitiveType.Cube : PrimitiveType.Cylinder;
                waste[i] = Shape(kind, labels[i] + "廢棄物", root.transform, Vector3.zero,
                    i == 2 ? new Vector3(0.22f, 0.09f, 0.16f) : new Vector3(0.13f, 0.14f, 0.13f), material, colors[i]);
                if (i == 0)
                {
                    Shape(PrimitiveType.Cylinder, "瓶頸", waste[i].transform, new Vector3(0f, 1.05f, 0f),
                        new Vector3(0.48f, 0.32f, 0.48f), material, colors[i]);
                    Shape(PrimitiveType.Cylinder, "瓶身標籤", waste[i].transform, new Vector3(0f, 0f, 0f),
                        new Vector3(1.04f, 0.32f, 1.04f), material, new Color(0.65f, 0.67f, 0.60f));
                }
                if (i == 1)
                    Shape(PrimitiveType.Cylinder, "罐蓋", waste[i].transform, new Vector3(0f, 0.98f, 0f),
                        new Vector3(1.05f, 0.08f, 1.05f), material, new Color(0.7f, 0.7f, 0.68f));
                waste[i].SetActive(false);
            }
            GameObject sensor = Shape(PrimitiveType.Cube, "分選感測指示", root.transform, new Vector3(0.15f, 1.67f, -0.5f),
                new Vector3(0.12f, 0.12f, 0.08f), material, new Color(0.15f, 0.20f, 0.17f));
            sensorIndicator = sensor.GetComponent<Renderer>();
            sensorBlock = new MaterialPropertyBlock();
            BuildMachineMotion(root.transform, steel);
        }

        private void BuildMachineMotion(Transform root, Material steel)
        {
            // Three visible drive wheels make the pump, reactor and filter equipment look powered.
            Vector3[] locations = {
                new Vector3(-8.5f, 2.04f, 7.45f), // feed pump
                new Vector3(-11.85f, 2.20f, 9.35f), // purification reactor
                new Vector3(-4.92f, 2.28f, 20.25f) // filter skid
            };
            machineRotors = new Transform[locations.Length];
            machineIndicators = new Light[locations.Length];
            for (int i = 0; i < locations.Length; i++)
            {
                GameObject rotor = new GameObject("RestartableIndustrialRotor_" + i);
                rotor.transform.SetParent(root, false);
                rotor.transform.localPosition = sortingLine.InverseTransformPoint(locations[i]);
                // A flat fan wheel; rotate about its local Z axis when the factory starts.
                Shape(PrimitiveType.Cylinder, "DriveHub", rotor.transform, Vector3.zero,
                    new Vector3(0.15f, 0.08f, 0.15f), steel, new Color(0.31f, 0.29f, 0.23f))
                    .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                for (int blade = 0; blade < 4; blade++)
                {
                    GameObject fin = Shape(PrimitiveType.Cube, "DriveBlade", rotor.transform,
                        new Vector3(0.33f, 0f, 0f), new Vector3(0.62f, 0.09f, 0.045f), steel,
                        new Color(0.30f, 0.33f, 0.30f));
                    fin.transform.localRotation = Quaternion.Euler(0f, 0f, blade * 90f);
                    fin.transform.localPosition = Quaternion.Euler(0f, 0f, blade * 90f) * new Vector3(0.33f, 0f, 0f);
                }
                machineRotors[i] = rotor.transform;
                GameObject lightObject = new GameObject("DriveReadyLamp");
                lightObject.transform.SetParent(root, false);
                lightObject.transform.localPosition = rotor.transform.localPosition + Vector3.up * 0.58f;
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 2.2f;
                light.intensity = 0.45f;
                light.color = new Color(0.38f, 1f, 0.63f);
                light.shadows = LightShadows.None;
                light.enabled = false;
                machineIndicators[i] = light;
            }
        }

        private static void AddFloorWear(Transform root)
        {
            GameObject original = GameObject.Find("Dirt_FloorStain_01");
            if (!original || !original.GetComponent<Renderer>()) return;
            Vector3[] positions = { new Vector3(-2.5f, 0.012f, -1.2f), new Vector3(2.2f, 0.012f, 1.6f),
                new Vector3(-1.5f, 0.012f, 2.4f) };
            for (int i = 0; i < positions.Length; i++)
            {
                GameObject stain = Instantiate(original, root, false);
                stain.name = "C3_工業油污_" + (i + 1);
                stain.transform.localPosition = positions[i];
                stain.transform.localRotation = Quaternion.Euler(-90f, i * 37f, 0f);
                stain.transform.localScale = new Vector3(0.8f + i * 0.15f, 1.1f, 1f);
            }
        }

        private void BuildFeed(Transform root, Material rubber, Material steel)
        {
            feedStart = sortingLine.InverseTransformPoint(new Vector3(-14.5f, 2.15f, 7.5f));
            feedEnd = sortingLine.InverseTransformPoint(bFeedEndpoint);
            Vector3 direction = feedEnd - feedStart;
            Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            GameObject belt = Shape(PrimitiveType.Cube, "B區進料帶面", root, (feedStart + feedEnd) * 0.5f,
                new Vector3(0.92f, 0.14f, direction.magnitude), rubber, new Color(0.4f, 0.4f, 0.38f));
            belt.transform.localRotation = rotation;
            Vector3 side = Vector3.Cross(Vector3.up, direction.normalized) * 0.52f;
            for (int i = -1; i <= 1; i += 2)
            {
                GameObject rail = Shape(PrimitiveType.Cube, "B區輸送帶支架", root, (feedStart + feedEnd) * 0.5f + side * i,
                    new Vector3(0.1f, 0.22f, direction.magnitude), steel, new Color(0.38f, 0.42f, 0.44f));
                rail.transform.localRotation = rotation;
            }
            feedRollers = new Transform[2];
            for (int i = 0; i < feedRollers.Length; i++)
            {
                Vector3 end = i == 0 ? feedStart : feedEnd;
                GameObject roller = Shape(PrimitiveType.Cylinder, "B區滾輪", root, end,
                    new Vector3(0.18f, 0.34f, 0.18f), steel, new Color(0.38f, 0.42f, 0.44f));
                roller.transform.localRotation = rotation * Quaternion.Euler(0f, 0f, 90f);
                feedRollers[i] = roller.transform;
                Shape(PrimitiveType.Cube, "B區支柱", root, end + side + Vector3.down * 1.5f,
                    new Vector3(0.12f, 3f, 0.12f), steel, new Color(0.38f, 0.42f, 0.44f));
            }
            feedMarks = new Transform[8];
            for (int i = 0; i < feedMarks.Length; i++)
            {
                GameObject mark = Shape(PrimitiveType.Cube, "B區進料帶紋", root, Vector3.zero,
                    new Vector3(0.86f, 0.025f, 0.10f), steel, new Color(0.38f, 0.42f, 0.44f));
                mark.transform.localRotation = rotation;
                feedMarks[i] = mark.transform;
                feedMarks[i].localPosition = Vector3.Lerp(feedEnd, feedStart, i / (float)feedMarks.Length) + Vector3.up * 0.075f;
            }
            // Additional supports and crossmembers make the bridge read as an industrial conveyor.
            for (int i = 0; i < 6; i++)
            {
                float t = (i + 0.5f) / 6f;
                Vector3 anchor = Vector3.Lerp(feedStart, feedEnd, t);
                float localFloor = 0.05f - sortingLine.position.y;
                float supportHeight = Mathf.Max(0.4f, anchor.y - localFloor);
                for (int sideIndex = -1; sideIndex <= 1; sideIndex += 2)
                {
                    Shape(PrimitiveType.Cube, "B區輸送帶鏽蝕立柱", root, anchor + side * sideIndex + Vector3.down * supportHeight * 0.5f,
                        new Vector3(0.11f, supportHeight, 0.11f), steel, new Color(0.30f, 0.28f, 0.25f));
                }
                Shape(PrimitiveType.Cube, "B區輸送帶橫向補強", root, anchor + Vector3.down * 0.15f,
                    new Vector3(1.12f, 0.09f, 0.11f), steel, new Color(0.34f, 0.31f, 0.28f));
            }
            // The feed endpoint is physically aligned with the pre-existing B-side doorway.
            GameObject bEndMarker = new GameObject("B區入口_輸送帶連接端");
            bEndMarker.transform.SetParent(root, false);
            bEndMarker.transform.localPosition = feedEnd;
            feedStart += Vector3.up * 0.075f;
            feedEnd += Vector3.up * 0.075f;
            Vector3 chuteEnd = new Vector3(-1.2f, 1.43f, 0f);
            Vector3 chuteDirection = chuteEnd - feedStart;
            GameObject chute = Shape(PrimitiveType.Cube, "進料溜槽", root, (feedStart + chuteEnd) * 0.5f,
                new Vector3(0.92f, 0.1f, chuteDirection.magnitude), steel, new Color(0.38f, 0.42f, 0.44f));
            chute.transform.localRotation = Quaternion.LookRotation(chuteDirection.normalized, Vector3.up);
        }

        private static GameObject Shape(PrimitiveType type, string name, Transform parent, Vector3 position,
            Vector3 scale, Material material, Color color)
        {
            GameObject item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            Collider collider = item.GetComponent<Collider>();
            if (collider) { collider.enabled = false; Destroy(collider); }
            Renderer renderer = item.GetComponent<Renderer>();
            if (material) renderer.sharedMaterial = material;
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
            return item;
        }
    }
}
