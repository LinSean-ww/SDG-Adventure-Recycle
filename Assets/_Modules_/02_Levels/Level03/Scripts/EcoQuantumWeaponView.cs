using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Layered hard-surface first-person rifle, built as one reusable camera-attached
// object. No collider and no second camera; ADS still uses the existing camera.
public sealed class EcoQuantumWeaponView : MonoBehaviour
{
    private Transform cameraMount;
    private GameObject rifle;
    private Transform muzzle;
    private readonly List<Material> ownedMaterials = new List<Material>();
    private Material gunMetal, panelMetal, grips, highlight, charge, coil, beamMaterial;
    private bool visible, aiming;
    private float recoil, shotFlash, reloadGlow;
    private Light muzzleLight;
    private AudioSource fireAudio;
    private AudioClip fireClip;

    public void AttachTo(Transform cameraTransform)
    {
        if (!cameraTransform) return;
        cameraMount=cameraTransform;
        transform.SetParent(cameraTransform,false);
        transform.localPosition=Vector3.zero;
        transform.localRotation=Quaternion.identity;
        if (!rifle) BuildRifle();
    }
    public void SetVisible(bool value)
    {
        visible=value;
        if (rifle) rifle.SetActive(value);
    }
    public void SetAiming(bool value) { aiming=value; }
    private Material MaterialFor(string name, Color color, float metallic, float gloss, bool emissive=false)
    {
        Shader shader=Shader.Find("Standard");
        Material m=new Material(shader?shader:Shader.Find("Diffuse"));
        m.name=name; m.color=color;
        if(m.HasProperty("_Metallic")) m.SetFloat("_Metallic",metallic);
        if(m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness",gloss);
        if(emissive && m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor",color*2.7f);
        }
        ownedMaterials.Add(m);
        return m;
    }
    private GameObject Part(string name, PrimitiveType shape, Transform parent, Vector3 pos,
        Vector3 scale, Material mat, Vector3? euler = null)
    {
        GameObject g=GameObject.CreatePrimitive(shape);
        g.name=name; g.transform.SetParent(parent,false);
        g.transform.localPosition=pos;
        g.transform.localScale=scale;
        if(euler.HasValue) g.transform.localRotation=Quaternion.Euler(euler.Value);
        Collider c=g.GetComponent<Collider>();if(c)Destroy(c);
        Renderer r=g.GetComponent<Renderer>();
        r.sharedMaterial=mat;
        r.shadowCastingMode=ShadowCastingMode.Off;
        return g;
    }
    private void BuildRifle()
    {
        gunMetal=MaterialFor("Salvaged carbon-steel receiver",new Color(.13f,.16f,.18f),.91f,.42f);
        panelMetal=MaterialFor("Machined ceramic-titanium cladding",new Color(.34f,.38f,.40f),.72f,.56f);
        grips=MaterialFor("Recycled polymer grip",new Color(.055f,.07f,.08f),.08f,.19f);
        highlight=MaterialFor("Oxidized bronze detail",new Color(.34f,.25f,.14f),.70f,.41f);
        charge=MaterialFor("Cyan energy conduits",new Color(.02f,.75f,.92f),.13f,.65f,true);
        coil=MaterialFor("Blue quantum chamber",new Color(.06f,.37f,.56f),.45f,.62f,true);
        Shader lineShader=Shader.Find("Sprites/Default");
        if(lineShader)
        {
            beamMaterial=new Material(lineShader);
            beamMaterial.name="Reusable quantum ray";
            ownedMaterials.Add(beamMaterial);
        }
        rifle=new GameObject("Eco Quantum Projector / modular receiver");
        rifle.transform.SetParent(transform,false);
        Transform root=rifle.transform;
        // Main receiver and recycled modular armor, intentionally asymmetrical.
        Part("Upper split receiver",PrimitiveType.Cube,root,new Vector3(0,0,.08f),new Vector3(.22f,.19f,.67f),gunMetal);
        Part("Left plated housing",PrimitiveType.Cube,root,new Vector3(-.12f,.016f,.04f),new Vector3(.065f,.17f,.54f),panelMetal);
        Part("Right plated housing",PrimitiveType.Cube,root,new Vector3(.12f,-.006f,.04f),new Vector3(.075f,.16f,.54f),panelMetal);
        Part("Rear fixed stock",PrimitiveType.Cube,root,new Vector3(0,-.03f,-.44f),new Vector3(.19f,.14f,.39f),gunMetal);
        Part("Rubber shoulder support",PrimitiveType.Cube,root,new Vector3(0,-.03f,-.67f),new Vector3(.25f,.23f,.07f),grips);
        Part("Rear frame spine",PrimitiveType.Cube,root,new Vector3(0,.09f,-.49f),new Vector3(.085f,.10f,.40f),panelMetal);
        Part("Forged front shroud",PrimitiveType.Cube,root,new Vector3(0,-.013f,.43f),new Vector3(.18f,.17f,.53f),gunMetal);
        Part("Vented muzzle shroud",PrimitiveType.Cylinder,root,new Vector3(0,.012f,.75f),new Vector3(.105f,.22f,.105f),panelMetal,new Vector3(90,0,0));
        Part("Emit aperture",PrimitiveType.Cylinder,root,new Vector3(0,.012f,.98f),new Vector3(.075f,.042f,.075f),charge,new Vector3(90,0,0));
        muzzle=new GameObject("Energy muzzle").transform;
        muzzle.SetParent(root,false); muzzle.localPosition=new Vector3(0,.012f,1.02f);
        // Replace the prototype's bright cylinder with a segmented reactor chamber.
        for(int i=0;i<5;i++)
        {
            float z=-.2f+i*.105f;
            Part("Ceramic reactor rib "+i,PrimitiveType.Cube,root,new Vector3(0,.12f,z),new Vector3(.245f,.046f,.034f),panelMetal);
            Part("Energy chamber window "+i,PrimitiveType.Cube,root,new Vector3(0,.153f,z+.015f),new Vector3(.115f,.014f,.053f),charge);
        }
        for(int side=-1;side<=1;side+=2)
        {
            Part("Lateral emitter tube",PrimitiveType.Cylinder,root,new Vector3(side*.093f,.014f,.68f),
                new Vector3(.033f,.29f,.033f),coil,new Vector3(90,0,0));
            Part("Sleek armor ridge",PrimitiveType.Cube,root,new Vector3(side*.13f,.110f,.24f),
                new Vector3(.041f,.044f,.54f),panelMetal);
            for(int i=0;i<9;i++)
            {
                float z=.34f+i*.059f;
                Part("Vent slots",PrimitiveType.Cube,root,new Vector3(side*.153f,-.016f,z),
                    new Vector3(.014f,.038f,.026f),grips);
            }
            for(int i=0;i<5;i++)
            {
                float z=-.24f+i*.13f;
                Part("Receiver socket screw",PrimitiveType.Cylinder,root,new Vector3(side*.162f,-.039f,z),
                    new Vector3(.015f,.005f,.015f),highlight,new Vector3(0,0,90));
            }
            Part("External recycling identification strip",PrimitiveType.Cube,root,
                new Vector3(side*.16f,.056f,-.11f),new Vector3(.016f,.013f,.31f),charge);
        }
        // A mechanical iron sight and top optic housing.
        Part("Top equipment rail",PrimitiveType.Cube,root,new Vector3(0,.154f,.19f),new Vector3(.10f,.04f,.76f),gunMetal);
        for(int i=0;i<12;i++)
            Part("Optic rail serration",PrimitiveType.Cube,root,new Vector3(0,.18f,-.15f+i*.06f),new Vector3(.125f,.022f,.023f),panelMetal);
        Part("Holographic sight frame",PrimitiveType.Cube,root,new Vector3(0,.242f,-.04f),new Vector3(.12f,.09f,.12f),gunMetal);
        Part("Illuminated sight lens",PrimitiveType.Cube,root,new Vector3(0,.245f,.03f),new Vector3(.073f,.055f,.01f),coil);
        Part("Foregrip lower mount",PrimitiveType.Cube,root,new Vector3(0,-.155f,.39f),new Vector3(.14f,.10f,.19f),panelMetal);
        Part("Ergonomic pistol grip",PrimitiveType.Cube,root,new Vector3(0,-.25f,-.12f),new Vector3(.115f,.32f,.14f),grips,new Vector3(13,0,0));
        Part("Trigger guard",PrimitiveType.Cube,root,new Vector3(0,-.19f,.035f),new Vector3(.11f,.035f,.17f),highlight);
        Part("Magnetic energy magazine",PrimitiveType.Cube,root,new Vector3(0,-.22f,.12f),new Vector3(.15f,.28f,.17f),panelMetal);
        Part("Mag indicator",PrimitiveType.Cube,root,new Vector3(.082f,-.17f,.12f),new Vector3(.013f,.18f,.043f),charge);
        Part("Thermal coil underbelly",PrimitiveType.Cylinder,root,new Vector3(0,-.12f,.55f),new Vector3(.054f,.27f,.054f),highlight,new Vector3(90,0,0));
        GameObject light=new GameObject("Muzzle discharge light");
        light.transform.SetParent(muzzle,false);
        muzzleLight=light.AddComponent<Light>();
        muzzleLight.type=LightType.Point; muzzleLight.range=3f;
        muzzleLight.intensity=0f; muzzleLight.color=Color.cyan;
        muzzleLight.shadows=LightShadows.None;
        fireAudio=rifle.AddComponent<AudioSource>();
        fireAudio.playOnAwake=false;
        fireAudio.spatialBlend=0f;
        fireAudio.volume=.21f;
        // Original synthesised arcade pulse: no external sample dependency.
        const int samples=6615;
        float[] data=new float[samples];
        for(int i=0;i<samples;i++)
        {
            float t=i/44100f;
            float envelope=Mathf.Exp(-t*31f);
            data[i]=Mathf.Sin(2f*Mathf.PI*(425f*t+1500f*t*t))*.22f*envelope;
        }
        fireClip=AudioClip.Create("EcoQuantumDischarge",samples,1,44100,false);
        fireClip.SetData(data,0);
    }
    public void UpdateViewmodel()
    {
        if (!rifle || !cameraMount || !visible) return;
        recoil=Mathf.MoveTowards(recoil,0f,Time.deltaTime*4f);
        shotFlash=Mathf.MoveTowards(shotFlash,0f,Time.deltaTime*9f);
        if(muzzleLight) muzzleLight.intensity=shotFlash*2.8f;
        Vector3 hip=new Vector3(.27f,-.235f,.59f);
        Vector3 ads=new Vector3(0f,-.185f,.45f);
        Vector3 target=aiming?ads:hip;
        target.z-=recoil*.14f;
        // Prevent walls swallowing the rifle near narrow factory passageways.
        if (Physics.Raycast(cameraMount.position,cameraMount.forward,out RaycastHit wall,.83f,
            ~0,QueryTriggerInteraction.Ignore))
        {
            float blocked=Mathf.Clamp01((.83f-wall.distance)/.55f);
            target.z-=blocked*.19f;
            target.y-=blocked*.22f;
        }
        rifle.transform.localPosition=Vector3.Lerp(rifle.transform.localPosition,target,Time.deltaTime*13f);
        rifle.transform.localRotation=Quaternion.Slerp(rifle.transform.localRotation,
            Quaternion.Euler(-recoil*7f,aiming?0f:-7f,aiming?0f:-2.5f),Time.deltaTime*13f);
    }
    public void OnShot(Vector3 hitPosition)
    {
        if(!visible || !cameraMount) return;
        recoil=Mathf.Min(1f,recoil+.53f);
        shotFlash=1f;
        if(fireAudio && fireClip)fireAudio.PlayOneShot(fireClip);
        Vector3 origin=muzzle ? muzzle.position : cameraMount.position+cameraMount.forward*.4f;
        GameObject tracer=new GameObject("Quantum beam (short-lived)");
        LineRenderer line=tracer.AddComponent<LineRenderer>();
        if(beamMaterial) line.sharedMaterial=beamMaterial;
        line.positionCount=2;line.useWorldSpace=true;
        line.SetPosition(0,origin);line.SetPosition(1,hitPosition);
        line.startWidth=.035f;line.endWidth=.004f;
        line.startColor=new Color(.12f,.94f,1f);line.endColor=new Color(.18f,.64f,1f);
        Destroy(tracer,.13f);
        GameObject hit=new GameObject("Quantum impact flash");
        hit.transform.position=hitPosition;
        Light point=hit.AddComponent<Light>();point.type=LightType.Point;
        point.range=1.7f;point.intensity=.65f;point.color=Color.cyan;
        point.shadows=LightShadows.None;
        Destroy(hit,.075f);
    }
    public void OnReload()
    {
        if(fireAudio && fireClip)fireAudio.PlayOneShot(fireClip,.3f);
    }
    private void OnDestroy()
    {
        foreach(Material m in ownedMaterials) if(m) Destroy(m);
        if(fireClip) Destroy(fireClip);
    }
}
