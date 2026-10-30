using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace C3TreasurePuzzle
{
    // Non-interactive scene art and lighting. All generated geometry is collider-free;
    // it must not compromise the original exploration route or puzzle hit tests.
    public sealed class C3EnvironmentUpgrade : MonoBehaviour
    {
        private Transform root;
        private Material oxidized, darkMetal, concrete, rubber, copper, dirtyGlass;
        private readonly List<Light> workingLights = new List<Light>();
        private C3DeviceState device;
        private bool powered;
        public int RenderPropCount { get; private set; }

        private void Awake()
        {
            device = GetComponent<C3DeviceState>();
            if (GameObject.Find("C3_IndustrialArtUpgrade")) return;
            root = new GameObject("C3_IndustrialArtUpgrade").transform;
            root.SetParent(transform, false);
            oxidized = NewMaterial("Corroded iron", new Color(.30f,.20f,.14f), .65f, .20f);
            darkMetal = NewMaterial("Forged steel", new Color(.22f,.25f,.27f), .82f, .36f);
            concrete = NewMaterial("Spalled masonry", new Color(.38f,.37f,.33f), .04f, .12f);
            rubber = NewMaterial("Aged cables", new Color(.075f,.087f,.09f), .02f, .14f);
            copper = NewMaterial("Dull copper", new Color(.32f,.22f,.15f), .67f, .29f);
            dirtyGlass = NewMaterial("Industrial lamps", new Color(.34f,.46f,.48f), .08f, .61f);
            BuildDucts(); BuildCableRuns(); BuildConcreteRubble(); BuildEquipment(); BuildLamps();
            RepairExistingLabels();
            // Keep the industrial space readable while retaining its abandoned atmosphere.
            RenderSettings.ambientIntensity = Mathf.Max(RenderSettings.ambientIntensity, 1.10f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(.13f,.15f,.16f);
            RenderSettings.fogStartDistance = 18f;
            RenderSettings.fogEndDistance = 72f;
        }

        private void OnEnable()
        {
            if (device == null) device = GetComponent<C3DeviceState>();
            if (device != null) device.Running += Restore;
            if (device != null && device.FactoryState >= C3FactoryState.Running) Restore();
        }
        private void OnDisable() { if (device != null) device.Running -= Restore; }
        private void OnDestroy()
        {
            foreach (Material mat in new[] { oxidized,darkMetal,concrete,rubber,copper,dirtyGlass })
                if (mat) Destroy(mat);
        }
        private void Restore()
        {
            powered = true;
            foreach (Light light in workingLights) if (light) light.enabled = true;
        }
        private void Update()
        {
            // In the abandoned state, only sporadic overhead emergency light survives.
            if (powered) return;
            for (int i=0; i<workingLights.Count; i++)
                if (workingLights[i]) workingLights[i].enabled = (i==0 && Mathf.Sin(Time.time*2.8f) > .89f);
        }

        private Material NewMaterial(string name, Color tint, float metallic, float smooth)
        {
            Shader shader = Shader.Find("Standard");
            Material m = new Material(shader != null ? shader : Shader.Find("Diffuse"));
            m.name = name;
            m.color = tint;
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            return m;
        }
        private GameObject Box(string name, Vector3 position, Vector3 scale, Material mat, Quaternion? rot = null)
        {
            GameObject o = GameObject.CreatePrimitive(PrimitiveType.Cube);
            o.name = name;
            o.transform.SetParent(root, false);
            o.transform.position = position;
            o.transform.localScale = scale;
            o.transform.rotation = rot ?? Quaternion.identity;
            Destroy(o.GetComponent<Collider>());
            Renderer r = o.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.On;
            RenderPropCount++;
            return o;
        }
        private void Pipe(string name, Vector3 a, Vector3 b, float width, Material mat)
        {
            Vector3 diff = b-a;
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            obj.name = name;
            obj.transform.SetParent(root,false);
            obj.transform.position = (a+b)*.5f;
            obj.transform.up = diff.normalized;
            obj.transform.localScale = new Vector3(width, diff.magnitude*.5f, width);
            obj.GetComponent<Renderer>().sharedMaterial=mat;
            Destroy(obj.GetComponent<Collider>());
            RenderPropCount++;
        }
        private void BuildDucts()
        {
            // Two corroded mainlines above the north/south service bays.
            for(int lane=0;lane<3;lane++)
            {
                float x = lane==0 ? -23.9f : lane==1 ? -1.25f : -18.8f;
                float y = lane==2 ? 6.75f : 6.0f;
                Pipe("主蒸汽管", new Vector3(x,y,1.6f),new Vector3(x,y,23.8f),.105f,oxidized);
                for(int i=0;i<7;i++)
                {
                    float z = 3f+i*3.2f;
                    Box("管路固定支架",new Vector3(x,y+.18f,z),new Vector3(.5f,.1f,.35f),darkMetal);
                    Pipe("分流彎管",new Vector3(x,y,z),new Vector3(x+(lane==0?1.3f:-.9f),y-.85f,z),.048f,copper);
                    Box("維修閥門座",new Vector3(x,y-.08f,z),new Vector3(.22f,.18f,.21f),oxidized);
                }
            }
            for(int i=0;i<6;i++)
            {
                float z=2.4f+i*3.8f;
                Pipe("空調風管",new Vector3(-20.5f,7.15f,z),new Vector3(-4.8f,7.15f,z),.065f,darkMetal);
                for(int j=0;j<5;j++)
                    Box("通風格柵框",new Vector3(-20+j*3.7f,7.0f,z),new Vector3(.07f,.22f,.40f),oxidized);
            }
        }
        private void BuildCableRuns()
        {
            for(int side=0;side<2;side++)
            {
                float x=side==0 ? -24.25f:-.75f;
                for(int line=0;line<4;line++)
                {
                    float y=3.0f+line*.11f;
                    for(int i=0;i<16;i++)
                    {
                        float z=2.0f+i*1.3f;
                        Vector3 a=new Vector3(x,y,z);
                        Vector3 b=new Vector3(x,y-.07f-(i%5)*.015f,z+1.3f);
                        Pipe("鬆脫電力線纜",a,b,.018f,rubber);
                    }
                }
                for(int i=0;i<12;i++)
                {
                    float z=2.1f+i*1.75f;
                    Box("鏽蝕壁面固定夾",new Vector3(x,3.1f,z),new Vector3(.16f,.12f,.13f),oxidized);
                }
            }
        }
        private void BuildConcreteRubble()
        {
            System.Random random = new System.Random(11029);
            for(int i=0;i<64;i++)
            {
                bool west=i%2==0;
                float x=west ? -23.65f+(float)random.NextDouble()*.9f:-1.30f-(float)random.NextDouble()*.9f;
                float z=1.4f+(float)random.NextDouble()*21.2f;
                float size=.13f+(float)random.NextDouble()*.32f;
                Box("散落碎磚與廢鋼材",new Vector3(x,.58f+size*.16f,z),new Vector3(size,.04f+size*.14f,size*.7f),
                    i%4==0 ? oxidized:concrete,
                    Quaternion.Euler((float)random.NextDouble()*18f,(float)random.NextDouble()*180f,10f));
            }
            for(int i=0;i<17;i++)
            {
                float z=1.9f+i*1.22f;
                Box("裂紋地板修補鋼板",new Vector3(i%2==0?-23.1f:-1.9f,.568f,z),
                    new Vector3(.4f+(i%3)*.11f,.016f,.25f),darkMetal,
                    Quaternion.Euler(0f,i*31f,0f));
            }
        }
        private void BuildEquipment()
        {
            // Distributed pipe pumps and wall-mounted service cabinets; stay against walls.
            for(int i=0;i<9;i++)
            {
                float x=i%2==0 ? -23.4f : -1.7f;
                float z=2.3f+i*2.27f;
                Box("老舊配電箱",new Vector3(x,1.55f,z),new Vector3(.48f,.88f,.83f),darkMetal);
                Box("生鏽櫃門",new Vector3(x+(i%2==0?.25f:-.25f),1.55f,z),
                    new Vector3(.037f,.75f,.68f),oxidized);
                for(int n=0;n<4;n++)
                    Box("配電箱散熱格柵",new Vector3(x+(i%2==0?.275f:-.275f),1.35f+n*.12f,z),
                        new Vector3(.015f,.028f,.50f),rubber);
                Pipe("冷凝排水管",new Vector3(x,1.15f,z+.42f),new Vector3(x,.62f,z+.65f),.034f,copper);
            }
        }
        private void RepairExistingLabels()
        {
            // These three TextMeshes were embedded in C_Main before this upgrade.
            // Their backside can be seen through the open two-storey factory.
            foreach (string name in new[] { "Plate_ElectricalIsolation", "Plate_Purifier03",
                "Log_UpperBayChipTransfer" })
            {
                GameObject original = GameObject.Find(name);
                if (!original) continue;
                TextMesh old = original.GetComponent<TextMesh>();
                if (!old || string.IsNullOrEmpty(old.text)) continue;
                Renderer originalRenderer = original.GetComponent<Renderer>();
                if (originalRenderer) originalRenderer.enabled = false;
                GameObject plaque = new GameObject("雙面實體告示牌_" + name);
                plaque.transform.SetParent(root,false);
                plaque.transform.position = original.transform.position;
                plaque.transform.rotation = original.transform.rotation;
                Box("固定標籤背板",plaque.transform.position,
                    new Vector3(2.25f,.65f,.07f),darkMetal,plaque.transform.rotation)
                    .transform.SetParent(plaque.transform,true);
                for (int i=0;i<2;i++)
                {
                    GameObject face=new GameObject(i==0?"正面印刷文字":"反面印刷文字");
                    face.transform.SetParent(plaque.transform,false);
                    face.transform.localPosition=new Vector3(0f,0f,i==0 ? -.043f:.043f);
                    face.transform.localRotation=i==0?Quaternion.identity:Quaternion.Euler(0f,180f,0f);
                    TextMesh text=face.AddComponent<TextMesh>();
                    text.text=old.text;
                    text.anchor=TextAnchor.MiddleCenter;
                    text.alignment=TextAlignment.Center;
                    text.fontSize=36;
                    text.characterSize=.065f;
                    text.color=new Color(.9f,.93f,.81f);
                }
            }
        }

        private void BuildLamps()
        {
            for(int i=0;i<6;i++)
            {
                float x=-20f+i%3*7.25f;
                float z=5f+i/3*12f;
                Box("工業吊燈金屬罩",new Vector3(x,6.15f,z),new Vector3(.94f,.13f,.48f),darkMetal);
                Box("工業燈罩磨砂玻璃",new Vector3(x,6.04f,z),new Vector3(.79f,.055f,.31f),dirtyGlass);
                GameObject emitter=new GameObject("設備重啟後的工作照明");
                emitter.transform.SetParent(root,false);
                emitter.transform.position=new Vector3(x,5.82f,z);
                Light light=emitter.AddComponent<Light>();
                light.type=LightType.Point;
                light.range=9f;
                light.intensity=.45f;
                light.color=i%2==0 ? new Color(.94f,.77f,.52f):new Color(.58f,.76f,.87f);
                light.shadows=LightShadows.None;
                light.enabled=false;
                workingLights.Add(light);
            }
        }
    }
}
