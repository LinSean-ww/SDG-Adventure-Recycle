using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace C3TreasurePuzzle
{
    // Geometry only. Never adds gameplay, receipts, lights, or interaction colliders.
    // One palette per owner; release it when that scene/player owner is destroyed.
    public sealed class C3VisualBuilder : IDisposable
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
            if (texture != null) m.mainTexture = Resources.Load<Texture2D>("V11Visual/" + texture);
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
            if (c) { c.enabled = false; C3FactoryLayoutUtility.Remove(c); }
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
        public void Screen(Transform p, Vector3 center, Vector2 size)
        {
            Part(p,"MonitorSteelBezel",center,new Vector3(size.x,size.y,.07f),"rust");
            Part(p,"GreenControlDisplay",center+Vector3.back*.039f,new Vector3(size.x*.88f,size.y*.82f,.012f),"screen");
            for(int i=0;i<5;i++)
            {
                float h=size.y*(.13f+.075f*i);
                Part(p,"MonitorTelemetry",center+new Vector3(size.x*(-.32f+i*.13f),-.18f*size.y+h*.5f,-.048f),new Vector3(size.x*.055f,h,.004f),"glow");
            }
            Wear(p,center+new Vector3(0,-size.y*.43f,-.041f),size.x);
        }
        public void CircuitBoard(Transform parent, Vector3 pos, float scale)
        {
            Transform pcb = Group(parent,"RecoveredCircuitBoard_Visual",pos); pcb.localScale=Vector3.one*scale;
            Part(pcb,"FibreglassPCB",Vector3.zero,new Vector3(.42f,.018f,.30f),"green");
            Part(pcb,"Processor",new Vector3(-.035f,.031f,0),new Vector3(.105f,.032f,.10f),"dark");
            for(int i=0;i<7;i++)
            {
                Part(pcb,"CopperTrace",new Vector3(-.16f+i*.049f,.011f,.04f),new Vector3(.003f,.002f,.20f),"yellow");
                Part(pcb,"SolderPad",new Vector3(-.15f+i*.047f,.016f,-.12f),new Vector3(.013f,.004f,.014f),"silver");
            }
            for(int i=0;i<4;i++)
            {
                Part(pcb,"Capacitor",new Vector3(.14f,.045f,-.105f+i*.066f),new Vector3(.025f,.03f,.025f),"silver",PrimitiveType.Cylinder);
                Part(pcb,"ExternalSocket",new Vector3(-.202f,.027f,-.095f+i*.064f),new Vector3(.05f,.042f,.04f),"silver");
                Part(pcb,"HeatSinkFin",new Vector3(.045f+i*.018f,.043f,.05f),new Vector3(.006f,.047f,.08f),"silver");
            }
            Rod(pcb,"RecoveredWire",new Vector3(-.1f,.018f,.14f),new Vector3(.16f,.03f,.16f),.008f,"red");
        }
        public void Bottle(Transform p, Vector3 pos, float scale)
        {
            Transform r=Group(p,"SalvagedPETBottle_Visual",pos);r.localScale=Vector3.one*scale;
            Part(r,"BottleBody",new Vector3(0,.15f,0),new Vector3(.14f,.12f,.14f),"blue",PrimitiveType.Cylinder);
            Part(r,"CrushedShoulder",new Vector3(0,.29f,0),new Vector3(.13f,.09f,.13f),"blue",PrimitiveType.Sphere);
            Part(r,"BottleNeck",new Vector3(0,.345f,0),new Vector3(.055f,.025f,.055f),"blue",PrimitiveType.Cylinder);
            Part(r,"RecoveredCap",new Vector3(0,.377f,0),new Vector3(.06f,.012f,.06f),"yellow",PrimitiveType.Cylinder);
            for(int i=0;i<4;i++)Part(r,"MouldedBottleRib",new Vector3(0,.075f+i*.047f,0),new Vector3(.146f,.008f,.146f),"blue",PrimitiveType.Cylinder);
            Part(r,"BottleLabel",new Vector3(0,.17f,-.072f),new Vector3(.095f,.07f,.004f),"white");
        }
        public void Can(Transform p, Vector3 pos, float scale, string tint="red")
        {
            Transform r=Group(p,"SalvagedCan_Visual",pos);r.localScale=Vector3.one*scale;
            Part(r,"CanBody",new Vector3(0,.14f,0),new Vector3(.15f,.13f,.15f),tint,PrimitiveType.Cylinder);
            foreach(float y in new[]{.015f,.27f})Part(r,"RolledAluminiumRim",new Vector3(0,y,0),new Vector3(.16f,.009f,.16f),"silver",PrimitiveType.Cylinder);
            Part(r,"CanTop",new Vector3(0,.279f,0),new Vector3(.145f,.004f,.145f),"silver",PrimitiveType.Cylinder);
            Part(r,"PullTab",new Vector3(0,.286f,0),new Vector3(.025f,.005f,.043f),"dark");
            Wear(r,new Vector3(0,.14f,-.078f),.12f);
        }
        public void QuantumRifle(Transform parent, float scale, ShadowCastingMode shadows = ShadowCastingMode.On)
        {
            Transform r=Group(parent,"ReclaimedQuantumRifle_Visual",Vector3.zero);r.localScale=Vector3.one*scale;
            Part(r,"ReclaimedMetalReceiver",new Vector3(0,0,.08f),new Vector3(.23f,.20f,.63f),"green");
            Part(r,"Stock",new Vector3(0,-.025f,-.40f),new Vector3(.19f,.16f,.34f),"rust");
            Part(r,"StockPad",new Vector3(0,-.03f,-.59f),new Vector3(.23f,.22f,.07f),"dark");
            Part(r,"Grip",new Vector3(0,-.21f,-.13f),new Vector3(.11f,.30f,.14f),"dark",PrimitiveType.Cube,new Vector3(15,0,0));
            Part(r,"Magazine",new Vector3(0,-.20f,.12f),new Vector3(.15f,.25f,.17f),"rust");
            for(int side=-1;side<=1;side+=2)
            {
                Rod(r,"GreenEnergyCell",new Vector3(side*.126f,0,-.12f),new Vector3(side*.126f,0,.29f),.065f,"glow");
                Rod(r,"CopperFeedPipe",new Vector3(side*.09f,-.1f,-.12f),new Vector3(side*.09f,-.1f,.65f),.023f,"rust");
                for(int i=0;i<5;i++)
                    Part(r,"ArmourBand",new Vector3(side*.155f,0,-.15f+i*.11f),new Vector3(.02f,.21f,.027f),"silver");
                Transform badge=Group(r,"RecyclingReceiverBadge",new Vector3(side*.166f,.02f,-.24f));
                badge.localRotation=Quaternion.Euler(0,side*90f,0);Recycle(badge,Vector3.zero,.07f);
            }
            Rod(r,"Barrel",new Vector3(0,0,.32f),new Vector3(0,0,.92f),.11f,"rust");
            for(int i=0;i<4;i++)
                Part(r,"EmitterRing",new Vector3(0,0,.68f+i*.075f),new Vector3(.20f,.016f,.20f),i%2==0?"silver":"glow",PrimitiveType.Cylinder,new Vector3(90,0,0));
            Part(r,"EnergyMuzzle",new Vector3(0,0,.97f),new Vector3(.12f,.014f,.12f),"glow",PrimitiveType.Cylinder,new Vector3(90,0,0));
            Part(r,"OpticMount",new Vector3(0,.14f,.01f),new Vector3(.09f,.06f,.26f),"dark");
            Rod(r,"Sight",new Vector3(0,.20f,-.11f),new Vector3(0,.20f,.14f),.075f,"rust");
            foreach(Renderer renderer in r.GetComponentsInChildren<Renderer>(true))
                renderer.shadowCastingMode=shadows;
        }
        public void Dispose()
        {
            foreach(Material m in palette.Values) C3FactoryLayoutUtility.ReleaseTransient(m);
            palette.Clear();
        }
    }
}
