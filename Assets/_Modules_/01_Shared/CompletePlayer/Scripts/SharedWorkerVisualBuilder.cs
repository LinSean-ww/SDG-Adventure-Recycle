using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SDGAdventure.CompletePlayer
{
    // Geometry only. Never adds gameplay, receipts, lights, or interaction colliders.
    // One palette per owner; release it when that scene/player owner is destroyed.
    internal sealed class SharedWorkerVisualBuilder : IDisposable
    {
        private readonly Dictionary<string, Material> palette = new Dictionary<string, Material>();
        public Material Paint(string key)
        {
            if (palette.TryGetValue(key, out Material found)) return found;
            Color color;
            switch (key)
            {
                case "orange": color = new Color(.88f,.26f,.035f); break;
                case "yellow": color = new Color(.92f,.67f,.07f); break;
                case "green": color = new Color(.06f,.29f,.16f); break;
                case "glow": color = new Color(.20f,.95f,.25f); break;
                case "screen": color = new Color(.025f,.11f,.08f); break;
                case "silver": color = new Color(.68f,.72f,.68f); break;
                case "rust": color = new Color(.38f,.20f,.09f); break;
                case "skin": color = new Color(.56f,.37f,.23f); break;
                case "oil": color = new Color(.15f,.13f,.10f); break;
                case "paper": color = new Color(.68f,.63f,.47f); break;
                case "blue": color = new Color(.08f,.31f,.49f); break;
                case "red": color = new Color(.58f,.055f,.025f); break;
                case "white": color = new Color(.82f,.84f,.72f); break;
                default: color = new Color(.075f,.10f,.105f); break;
            }
            Material m = new Material(Shader.Find("Standard")) { name = "V11_Visual_" + key, color = color };
            m.SetFloat("_Metallic", key == "silver" || key == "rust" ? .65f : .12f);
            m.SetFloat("_Glossiness", key == "glow" ? .55f : .25f);
            if (key == "glow") { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * 1.3f); }
            string texture = key == "oil" ? "Oil" : key == "rust" ? "Rust" : key == "silver" || key == "green" ? "Steel" : null;
            if (texture != null) m.mainTexture = Resources.Load<Texture2D>("SharedWorker/" + texture);
            palette.Add(key, m);
            return m;
        }
        public Transform Group(Transform parent, string name, Vector3 position)
        {
            Transform t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = position; return t;
        }
        public Transform Part(Transform parent, string name, Vector3 position, Vector3 size, string color,
            PrimitiveType shape = PrimitiveType.Cube, Vector3 euler = default(Vector3))
        {
            GameObject go = GameObject.CreatePrimitive(shape); go.name = name;
            Collider c = go.GetComponent<Collider>();
            if (c) { c.enabled = false; UnityEngine.Object.Destroy(c); }
            go.transform.SetParent(parent, false); go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler);
            // Unity's capsule mesh is 2 units high. Worker sizes specify full height.
            go.transform.localScale = shape == PrimitiveType.Capsule
                ? new Vector3(size.x,size.y*.5f,size.z) : size;
            go.GetComponent<Renderer>().sharedMaterial = Paint(color);
            return go.transform;
        }
        public void Rod(Transform p, string name, Vector3 a, Vector3 b, float diameter, string color)
        {
            Transform t = Part(p,name,(a+b)*.5f,new Vector3(diameter,Vector3.Distance(a,b)*.5f,diameter),color,PrimitiveType.Cylinder);
            t.localRotation = Quaternion.FromToRotation(Vector3.up,b-a);
        }
        public void Recycle(Transform p, Vector3 center, float radius)
        {
            // Three raised, genuinely 3D arrows. Front of plates is local -Z.
            for (int i=0;i<3;i++)
            {
                Transform arrow = Group(p,"RecycleArrow_"+i,center);
                arrow.localRotation = Quaternion.Euler(0,0,i*120f);
                Part(arrow,"RaisedArrowShaft",new Vector3(0,radius*.55f,0),new Vector3(radius*.82f,radius*.18f,.008f),"white");
                Part(arrow,"ArrowheadA",new Vector3(radius*.32f,radius*.63f,0),new Vector3(radius*.39f,radius*.16f,.009f),"white",PrimitiveType.Cube,new Vector3(0,0,-40));
                Part(arrow,"ArrowheadB",new Vector3(radius*.32f,radius*.45f,0),new Vector3(radius*.39f,radius*.16f,.009f),"white",PrimitiveType.Cube,new Vector3(0,0,40));
            }
        }
        public void Wear(Transform p, Vector3 center, float width)
        {
            for (int i=0;i<8;i++)
                Part(p,"PaintChip_"+i,center+new Vector3((i-3.5f)*width/9f,(i%3-1)*.021f,-.004f),
                    new Vector3(width*.035f,.006f,.003f),i%2==0?"rust":"silver",PrimitiveType.Cube,new Vector3(0,0,i*31));
        }
        public void Dispose()
        {
            foreach (Material material in palette.Values)
                if (material != null) UnityEngine.Object.Destroy(material);
            palette.Clear();
        }
    }
}
