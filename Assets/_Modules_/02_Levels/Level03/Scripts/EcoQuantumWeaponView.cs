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
    private Material beamMaterial;
    private C3TreasurePuzzle.C3VisualBuilder visualArt;
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
    private void BuildRifle()
    {
        visualArt=new C3TreasurePuzzle.C3VisualBuilder();
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
        visualArt.QuantumRifle(root,1f,ShadowCastingMode.Off);
        muzzle=new GameObject("Energy muzzle").transform;
        muzzle.SetParent(root,false); muzzle.localPosition=new Vector3(0,.012f,1.02f);
        GameObject light=new GameObject("Muzzle discharge light");
        light.transform.SetParent(muzzle,false);
        muzzleLight=light.AddComponent<Light>();
        muzzleLight.type=LightType.Point; muzzleLight.range=3f;
        muzzleLight.intensity=0f; muzzleLight.color=new Color(.2f,1f,.25f);
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
        line.startColor=new Color(.2f,1f,.25f);line.endColor=new Color(.1f,.65f,.18f);
        Destroy(tracer,.13f);
        GameObject hit=new GameObject("Quantum impact flash");
        hit.transform.position=hitPosition;
        Light point=hit.AddComponent<Light>();point.type=LightType.Point;
        point.range=1.7f;point.intensity=.65f;point.color=new Color(.2f,1f,.25f);
        point.shadows=LightShadows.None;
        Destroy(hit,.075f);
    }
    public void OnReload()
    {
        if(fireAudio && fireClip)fireAudio.PlayOneShot(fireClip,.3f);
    }
    private void OnDestroy()
    {
        if(rifle)
        {
            rifle.SetActive(false);
            foreach(Renderer renderer in rifle.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials=System.Array.Empty<Material>();
            Destroy(rifle);
        }
        foreach(Material m in ownedMaterials) if(m) Destroy(m);
        if(fireClip) Destroy(fireClip);
        if(visualArt!=null)visualArt.Dispose();
    }
}
