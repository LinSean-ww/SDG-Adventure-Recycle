using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace C3TreasurePuzzle
{
    // Lightweight scene art built from existing industrial materials at load time.
    // All props are visual-only: the original collision, item rays, stairs and doors stay untouched.
    public sealed class C3FactoryDecay : MonoBehaviour
    {
        private C3DeviceState device;
        private Transform artRoot;
        private Material rust;
        private Material steel;
        private Material concrete;
        private Material oil;
        private Light exitIndicator;
        private Light brokenFixture;
        private float flickerPhase;
        public bool ExitSignReady => exitIndicator != null && exitIndicator.enabled;
        public int DetailCount { get; private set; }

        private void Awake()
        {
            device = GetComponent<C3DeviceState>();
            if (GameObject.Find("C3_AbandonedFactory_DetailPass")) return;
            GameObject root = new GameObject("C3_AbandonedFactory_DetailPass");
            root.transform.SetParent(transform, false);
            artRoot = root.transform;
            rust = SourceMaterial("Rust_Barrel_WestBody");
            steel = SourceMaterial("Steel_SortingFrame");
            concrete = SourceMaterial("C_Wall_1_Left");
            oil = SourceMaterial("Dirt_FloorStain_01");
            BuildDebris();
            BuildWallDamage();
            BuildDamagedPipes();
            BuildDamagedFixtures();
            BuildDoorIndicators();
        }

        private static Material SourceMaterial(string name)
        {
            GameObject found = GameObject.Find(name);
            Renderer renderer = found ? found.GetComponent<Renderer>() : null;
            return renderer ? renderer.sharedMaterial : null;
        }

        private void OnEnable()
        {
            if (!device) device = GetComponent<C3DeviceState>();
            if (device) device.Running += RestoreExit;
            if (device && device.FactoryState >= C3FactoryState.Running) RestoreExit();
        }

        private void OnDisable()
        {
            if (device) device.Running -= RestoreExit;
        }

        private void Update()
        {
            // Faulty ceiling light before reboot; stabilizes once the factory is running.
            if (!brokenFixture) return;
            if (device && device.FactoryState >= C3FactoryState.Starting)
            {
                brokenFixture.intensity = Mathf.MoveTowards(brokenFixture.intensity, 0.42f, Time.deltaTime * 0.7f);
                return;
            }
            flickerPhase += Time.deltaTime;
            brokenFixture.intensity = Mathf.Sin(flickerPhase * 9.1f) > 0.92f ? 0.35f : 0.03f;
        }

        private void RestoreExit()
        {
            if (exitIndicator) exitIndicator.enabled = true;
        }

        private void BuildDebris()
        {
            // Fixed seed gives deterministic layout for screenshots and QA.
            System.Random random = new System.Random(1203);
            for (int i = 0; i < 45; i++)
            {
                bool west = i % 2 == 0;
                float x = west ? -24.3f + (float)random.NextDouble() * 1.6f :
                    -2.8f + (float)random.NextDouble() * 1.7f;
                float z = 2.2f + (float)random.NextDouble() * 20.5f;
                float size = 0.10f + (float)random.NextDouble() * 0.24f;
                GameObject fragment = Primitive(PrimitiveType.Cube, "碎裂混凝土與鏽蝕金屬_" + i,
                    new Vector3(x, 0.58f + size * 0.12f, z),
                    new Vector3(size, 0.06f + size * 0.15f, size * (0.7f + (float)random.NextDouble())),
                    i % 3 == 0 ? rust : concrete,
                    i % 3 == 0 ? new Color(0.28f, 0.18f, 0.12f) : new Color(0.29f, 0.28f, 0.25f));
                fragment.transform.rotation = Quaternion.Euler((float)random.NextDouble() * 17f,
                    (float)random.NextDouble() * 180f, (float)random.NextDouble() * 14f);
            }
            for (int i = 0; i < 14; i++)
            {
                float x = -23.4f + (i % 7) * 3.5f;
                float z = 2.6f + (i / 7) * 19.5f + (i % 3) * 0.28f;
                GameObject sample = GameObject.Find("Dirt_FloorStain_01");
                if (sample && sample.GetComponent<Renderer>())
                {
                    GameObject stain = Instantiate(sample, artRoot, false);
                    stain.name = "堆積油污_" + i;
                    stain.transform.position = new Vector3(x, 0.563f, z);
                    stain.transform.rotation = Quaternion.Euler(90f, (i * 43) % 360, 0f);
                    stain.transform.localScale = new Vector3(0.7f + (i % 4) * 0.18f,
                        0.8f + (i % 3) * 0.2f, 1f);
                    DisableColliders(stain);
                    DetailCount++;
                }
            }
            // Scrapped plates and rods near walls; never place solid collision across a walking route.
            for (int i = 0; i < 14; i++)
            {
                float z = 2.5f + i * 1.4f;
                GameObject sheet = Primitive(PrimitiveType.Cube, "散落廢鋼板_" + i,
                    new Vector3(i % 2 == 0 ? -23.6f : -1.8f, 0.61f, z),
                    new Vector3(0.65f + (i % 4) * 0.08f, 0.025f, 0.28f), rust,
                    new Color(0.24f, 0.17f, 0.12f));
                sheet.transform.rotation = Quaternion.Euler(5f, i * 47f, i % 3 * 3f);
            }
        }

        private void BuildWallDamage()
        {
            // Rust streaks and peeling strips on four interior-facing surfaces.
            for (int i = 0; i < 20; i++)
            {
                float z = 1.8f + (i % 10) * 2.23f;
                float y = 0.8f + (i % 4) * 0.7f;
                bool west = i < 10;
                Primitive(PrimitiveType.Cube, "外牆滲水鏽斑_" + i,
                    new Vector3(west ? -24.91f : -0.09f, y, z),
                    new Vector3(0.013f, 0.35f + (i % 3) * 0.18f, 0.12f + (i % 4) * 0.07f),
                    rust, new Color(0.24f, 0.13f, 0.085f));
            }
            for (int i = 0; i < 10; i++)
            {
                float x = -23.6f + i * 2.25f;
                GameObject scrape = Primitive(PrimitiveType.Cube, "南側牆面脫落_" + i,
                    new Vector3(x, 0.9f + (i % 4) * 0.8f, 0.37f),
                    new Vector3(0.30f + (i % 3) * 0.12f, 0.035f, 0.018f),
                    rust, new Color(0.25f, 0.18f, 0.12f));
                scrape.transform.Rotate(0f, 0f, i % 2 == 0 ? 15f : -14f);
            }
        }

        private void BuildDamagedPipes()
        {
            for (int i = 0; i < 6; i++)
            {
                float z = 3f + i * 3.55f;
                Vector3 a = new Vector3(-24.1f, 6.0f + (i % 2) * 0.35f, z);
                Vector3 b = a + new Vector3(0.55f, -0.3f - (i % 3) * 0.08f, 1.8f);
                Between(PrimitiveType.Cylinder, "彎折廢棄管線_" + i, a, b, 0.055f,
                    i % 3 == 0 ? rust : steel, new Color(0.31f, 0.28f, 0.24f));
                Primitive(PrimitiveType.Cube, "鏽蝕管夾_" + i, b, new Vector3(0.20f, 0.08f, 0.20f),
                    rust, new Color(0.26f, 0.17f, 0.11f));
            }
        }

        private void BuildDamagedFixtures()
        {
            // Broken non-interactive grating and hanging panel pieces overhead.
            for (int i = 0; i < 8; i++)
            {
                GameObject panel = Primitive(PrimitiveType.Cube, "變形懸吊線槽_" + i,
                    new Vector3(-19.5f + i * 1.65f, 6.2f + (i % 2) * 0.2f, 4.6f + i % 3 * 0.6f),
                    new Vector3(1.5f, 0.035f, 0.2f), steel, new Color(0.25f, 0.27f, 0.25f));
                panel.transform.rotation = Quaternion.Euler(i % 2 == 0 ? 9f : -7f, 0f, i % 3 * 6f);
            }
            GameObject lamp = new GameObject("短路閃爍工作燈");
            lamp.transform.SetParent(artRoot, false);
            lamp.transform.position = new Vector3(-22.3f, 5.3f, 13f);
            brokenFixture = lamp.AddComponent<Light>();
            brokenFixture.type = LightType.Point;
            brokenFixture.range = 5f;
            brokenFixture.shadows = LightShadows.None;
            brokenFixture.color = new Color(0.98f, 0.72f, 0.40f);
            brokenFixture.intensity = 0.04f;
        }

        private void BuildDoorIndicators()
        {
            GameObject exit = new GameObject("D區出口_重新啟動後亮起");
            exit.transform.SetParent(artRoot, false);
            exit.transform.position = new Vector3(-12.5f, 2.7f, 1.05f);
            exitIndicator = exit.AddComponent<Light>();
            exitIndicator.type = LightType.Point;
            exitIndicator.range = 4.4f;
            exitIndicator.intensity = 1.15f;
            exitIndicator.shadows = LightShadows.None;
            exitIndicator.color = new Color(0.35f, 1f, 0.5f);
            exitIndicator.enabled = false;
            C3BakedSignFactory.AtWorld("DZone", new Vector3(-12.5f, 3.07f, 0.65f),
                Quaternion.identity, 2.45f, 0.72f);
            C3BakedSignFactory.AtWorld("BZone", new Vector3(-1.0f, 3.05f, 12.5f),
                Quaternion.Euler(0f, -90f, 0f), 2.40f, 0.72f);
            C3BakedSignFactory.ReplaceLegacySigns();
        }

        private GameObject Primitive(PrimitiveType kind, string label, Vector3 position, Vector3 size,
            Material source, Color tint)
        {
            GameObject item = GameObject.CreatePrimitive(kind);
            item.name = label;
            item.transform.SetParent(artRoot, false);
            item.transform.position = position;
            item.transform.localScale = size;
            DisableColliders(item);
            Renderer renderer = item.GetComponent<Renderer>();
            if (source) renderer.sharedMaterial = source;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            properties.SetColor("_Color", tint);
            renderer.SetPropertyBlock(properties);
            DetailCount++;
            return item;
        }

        private void Between(PrimitiveType kind, string label, Vector3 from, Vector3 to, float radius,
            Material source, Color tint)
        {
            GameObject item = Primitive(kind, label, (from + to) * 0.5f,
                new Vector3(radius * 2f, Vector3.Distance(from, to) * 0.5f, radius * 2f), source, tint);
            item.transform.up = (to - from).normalized;
        }

        private static void DisableColliders(GameObject go)
        {
            foreach (Collider collider in go.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
                Destroy(collider);
            }
        }
    }
}
