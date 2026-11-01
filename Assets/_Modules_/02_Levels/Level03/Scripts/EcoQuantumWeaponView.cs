using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using SDGAdventure.SharedPlayer;

// Layered hard-surface first-person rifle, built as one reusable camera-attached
// object. No collider and no second camera; ADS still uses the existing camera.
[DefaultExecutionOrder(400)] // worker 200 -> camera rig 300 -> weapon and final hand solve
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
    private Transform primaryGrip,supportGrip;
    private PlayerPerspectiveRig thirdPersonRig;
    private C3TreasurePuzzle.C3WorkerAppearance worker;
    private bool thirdPerson;
    private float adsBlend;
    private readonly RaycastHit[] aimHits=new RaycastHit[48];
    private readonly RaycastHit[] clearanceHits=new RaycastHit[32];
    private readonly Collider[] firstPersonOverlaps=new Collider[32];
    private float obstructionBlend;
    private float firstPersonClearance; // visual-only; never modifies Shoot()
    public bool UsesThirdPersonPresentation => thirdPerson && visible && isActiveAndEnabled;
    public Vector3 VisualAimPoint { get; private set; }
    public Transform PrimaryGrip => primaryGrip;
    public Transform SupportGrip => supportGrip;

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
        if(!value)ReleaseGripPose();
    }
    public void SetAiming(bool value) { aiming=value; }

    public void SetThirdPersonPresentation(PlayerPerspectiveRig rig,bool enabledValue,bool isAiming)
    {
        // Runtime calls this for BOTH modes; FPS ADS must update too.
        aiming=isAiming;
        if(!enabledValue || !rig || !visible || !rifle)
        {
            ReleaseGripPose();
            if(rifle)rifle.transform.localScale=Vector3.one;
            return;
        }
        thirdPersonRig=rig;thirdPerson=true;aiming=isAiming;
        if(!worker)worker=rig.GetComponent<C3TreasurePuzzle.C3WorkerAppearance>();
        // Parent handoff is immediate; final position/rotation have one writer
        // below, after this frame's camera collision/orbit correction.
        if(transform.parent!=rig.transform)transform.SetParent(rig.transform,true);
        // FPS wall clearance can leave the child rifle retracted/tilted.
        // Restore its neutral pose before applying third-person grip scaling.
        rifle.transform.localPosition=Vector3.zero;
        rifle.transform.localRotation=Quaternion.identity;
        rifle.transform.localScale=Vector3.one*.65f;
        if(worker && worker.isActiveAndEnabled)worker.SetWeaponPresentation(this,true);
    }

    private void LateUpdate()
    {
        // In the accepted FPS runtime UpdateViewmodel was never invoked.
        // Own the visual offset here after the runtime moves the camera mount;
        // third-person presentation keeps the existing arm/aim solver below.
        if(!UsesThirdPersonPresentation)
        {
            UpdateViewmodel();
            return;
        }
        if(!thirdPersonRig || !cameraMount)return;
        adsBlend=Mathf.MoveTowards(adsBlend,aiming?1f:0f,Time.deltaTime*6f);
        Transform anchor=worker?worker.PrepareWeaponAnchor(thirdPersonRig.CameraFlatForward,adsBlend):null;
        Vector3 position=anchor?anchor.position:thirdPersonRig.transform.position+Vector3.up*1.3f+
            thirdPersonRig.CameraFlatRight*.22f+thirdPersonRig.CameraFlatForward*.35f;
        Ray ray=new Ray(cameraMount.position,cameraMount.forward);
        float nearest=85f;
        int count=Physics.RaycastNonAlloc(ray,aimHits,nearest,~0,QueryTriggerInteraction.Ignore);
        // NonAlloc does not guarantee the nearest subset when full. Only that
        // exceptional case uses the same complete query as production Shoot.
        RaycastHit[] hits=count==aimHits.Length
            ? Physics.RaycastAll(ray,nearest,~0,QueryTriggerInteraction.Ignore) : aimHits;
        if(hits!=aimHits)count=hits.Length;
        for(int i=0;i<count;i++)
            if(hits[i].collider && !hits[i].transform.IsChildOf(thirdPersonRig.transform))
                nearest=Mathf.Min(nearest,hits[i].distance);
        VisualAimPoint=ray.GetPoint(nearest);
        Vector3 direction=VisualAimPoint-position;
        // A point behind the held model at very close range must not flip it
        // through the torso. This is visual-only; Shoot still uses its own ray.
        if(Vector3.Dot(direction,ray.direction)<.15f)direction=ray.direction;
        float lean=worker?worker.WeaponLeanAmount:0f;
        Quaternion aimed=Quaternion.LookRotation(direction.normalized,Vector3.up)*
            Quaternion.Euler(0,0,-lean*12f);
        // This third-person rifle extends .66m beyond its grip. Near a wall,
        // camera-correct aim can put the *visual* barrel and hands inside
        // solid geometry. Detect obstruction from the actual weapon pivot,
        // ignoring the player's own controller, and move to a high-ready pose.
        // Shoot(), hit detection and camera aiming remain completely unchanged.
        float reach=.65f*1.02f;
        int clearanceCount=Physics.SphereCastNonAlloc(
            new Ray(position,aimed*Vector3.forward),.075f,clearanceHits,
            reach+.12f,~0,QueryTriggerInteraction.Ignore);
        float clearance=reach+.12f;
        for(int i=0;i<clearanceCount;i++)
        {
            Collider obstacle=clearanceHits[i].collider;
            if(obstacle && !obstacle.transform.IsChildOf(thirdPersonRig.transform))
                clearance=Mathf.Min(clearance,clearanceHits[i].distance);
        }
        float blocked=Mathf.Clamp01((reach+.12f-clearance)/.35f);
        obstructionBlend=Mathf.MoveTowards(obstructionBlend,blocked,Time.deltaTime*12f);
        Quaternion highReady=Quaternion.LookRotation(thirdPersonRig.CameraFlatForward,Vector3.up)*
            Quaternion.Euler(-80f,0f,-lean*12f);
        transform.SetPositionAndRotation(
            position-thirdPersonRig.CameraFlatForward*(.20f*obstructionBlend),
            Quaternion.Slerp(aimed,highReady,obstructionBlend));
        if(worker)worker.ApplyWeaponHands(this,primaryGrip,supportGrip);
    }

    private void ReleaseGripPose()
    {
        if(worker)worker.SetWeaponPresentation(this,false);
        thirdPerson=false;thirdPersonRig=null;adsBlend=0f;obstructionBlend=0f;
    }

    private void OnDisable() => ReleaseGripPose();
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
        primaryGrip=new GameObject("V11_PrimaryGrip_Visual").transform;
        primaryGrip.SetParent(root,false);primaryGrip.localPosition=new Vector3(.02f,-.18f,-.13f);
        supportGrip=new GameObject("V11_SupportGrip_Visual").transform;
        supportGrip.SetParent(root,false);supportGrip.localPosition=new Vector3(-.13f,-.10f,.18f);
        supportGrip.localRotation=Quaternion.Euler(90,0,0);
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
        // EcoAdventureRuntime already owns the FPS hip/ADS camera offset.
        // An additional +.59m here would push the 1.02m barrel through walls.
        Vector3 target=new Vector3(0f,0f,-recoil*.14f);
        float clear=FirstPersonForwardClearance();
        firstPersonClearance=clear;
        // The muzzle is 1.02m ahead of the rifle pivot. Account for the
        // camera-mounted parent offset, not just the child model position.
        // Retraction is immediate toward an obstruction and smooth on release,
        // so rapid movement into cover never leaves a barrel inside the wall.
        float mountForward=Vector3.Dot(transform.position-cameraMount.position,cameraMount.forward);
        float maximumLocalZ=clear-mountForward-1.02f-.10f;
        target.z=Mathf.Min(target.z,maximumLocalZ);
        Vector3 smooth=Vector3.Lerp(rifle.transform.localPosition,target,Time.deltaTime*13f);
        smooth.z=Mathf.Min(smooth.z,target.z);
        rifle.transform.localPosition=smooth;
        rifle.transform.localRotation=Quaternion.Slerp(rifle.transform.localRotation,
            Quaternion.Euler(-recoil*7f,aiming?0f:-7f,aiming?0f:-2.5f),Time.deltaTime*13f);
    }

    private float FirstPersonForwardClearance()
    {
        const float maxDistance=2.0f;
        const float weaponRadius=.26f;
        float closest=maxDistance;
        CharacterController owner=cameraMount.GetComponentInParent<CharacterController>();
        Transform bodyRoot=owner?owner.transform:cameraMount.root;
        Vector3 origin=cameraMount.position;
        // Sphere casts do not report geometry already overlapping their
        // starting sphere (e.g. the camera extremely close to a wall).
        int overlaps=Physics.OverlapSphereNonAlloc(origin,weaponRadius,
            firstPersonOverlaps,~0,QueryTriggerInteraction.Ignore);
        for(int i=0;i<overlaps;i++)
        {
            Collider collider=firstPersonOverlaps[i];
            if(collider && !collider.transform.IsChildOf(bodyRoot))
                return 0f;
        }
        Ray ray=new Ray(origin,cameraMount.forward);
        int count=Physics.SphereCastNonAlloc(ray,weaponRadius,
            clearanceHits,maxDistance,~0,QueryTriggerInteraction.Ignore);
        // When the fixed buffer is full, use the complete query rather than
        // silently missing a nearer wall (NonAlloc results are not sorted).
        RaycastHit[] hits=count==clearanceHits.Length
            ?Physics.SphereCastAll(ray,weaponRadius,maxDistance,~0,QueryTriggerInteraction.Ignore)
            :clearanceHits;
        if(hits!=clearanceHits)count=hits.Length;
        for(int i=0;i<count;i++)
        {
            Collider collider=hits[i].collider;
            if(collider && !collider.transform.IsChildOf(bodyRoot))
                closest=Mathf.Min(closest,hits[i].distance);
        }
        return closest;
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
        ReleaseGripPose();
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
