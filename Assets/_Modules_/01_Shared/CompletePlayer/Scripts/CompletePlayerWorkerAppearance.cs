using UnityEngine;
using UnityEngine.Rendering;
using SDGAdventure.SharedPlayer;

namespace SDGAdventure.CompletePlayer
{
    // Geometry and unarmed pose formulas are adapted from C3WorkerAppearance.
    // The weapon integration is deliberately optional and scene independent.
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class CompletePlayerWorkerAppearance : MonoBehaviour
    {
        private SharedWorkerVisualBuilder art;
        private CharacterController motor;
        private Transform body,head,hips,chest,leftArm,rightArm,leftLeg,rightLeg,leftKnee,rightKnee;
        private Transform leftElbow,rightElbow,leftHand,rightHand,weaponAnchor;
        private const float UpperArmLength=.34f, ForearmLength=.33f;
        private float phase, visualLean;
        private PlayerPerspectiveRig perspective;
        private Object weaponOwner;
        private bool HoldingWeapon => weaponOwner != null;
        public bool HasWorker => body && hips && chest && head && leftArm && rightArm && leftLeg && rightLeg;
        public Transform WeaponAnchor => weaponAnchor;
        public Transform LeftShoulder => leftArm;
        public Transform RightShoulder => rightArm;
        public Transform LeftElbow => leftElbow;
        public Transform RightElbow => rightElbow;
        public Transform LeftHand => leftHand;
        public Transform RightHand => rightHand;
        public void SetWeaponPoseOwner(Object owner) => weaponOwner = owner;

        private void Awake()
        {
            motor = GetComponent<CharacterController>();
            if (!motor) { enabled = false; return; }
            perspective = GetComponent<PlayerPerspectiveRig>();
            art=new SharedWorkerVisualBuilder();
            body=art.Group(transform,"V11_RecyclingEngineer_Visual",Vector3.zero);
            hips=art.Group(body,"WorkerPelvis",new Vector3(0,.88f,0));
            art.Part(hips,"WorkTrousers",Vector3.zero,new Vector3(.31f,.23f,.22f),"dark");
            art.Part(hips,"ToolBelt",new Vector3(0,.07f,0),new Vector3(.35f,.07f,.25f),"rust");
            art.Part(hips,"BeltBuckle",new Vector3(0,.07f,.132f),new Vector3(.065f,.05f,.016f),"silver");
            art.Part(hips,"ToolPouch",new Vector3(.20f,-.05f,0),new Vector3(.11f,.17f,.13f),"rust");
            chest=art.Group(hips,"WorkerChest",new Vector3(0,.28f,0));
            art.Part(chest,"DarkWorkJacket",Vector3.zero,new Vector3(.40f,.39f,.24f),"dark",PrimitiveType.Capsule);
            art.Part(chest,"OrangeSafetyVest",new Vector3(0,.005f,.008f),new Vector3(.40f,.36f,.255f),"orange");
            for(int side=-1;side<=1;side+=2)
            {
                art.Part(chest,"ReflectiveShoulder",new Vector3(side*.12f,.01f,.141f),new Vector3(.035f,.32f,.008f),"white");
                art.Part(chest,"ReflectiveBack",new Vector3(side*.12f,.01f,-.127f),new Vector3(.035f,.32f,.008f),"white");
                art.Part(chest,"VestPocket",new Vector3(side*.11f,-.10f,.148f),new Vector3(.115f,.08f,.025f),"rust");
            }
            art.Part(chest,"ReflectiveWaist",new Vector3(0,-.04f,.147f),new Vector3(.39f,.025f,.008f),"white");
            art.Recycle(chest,new Vector3(-.105f,.09f,.157f),.052f);
            art.Wear(chest,new Vector3(0,-.16f,.149f),.36f);
            art.Part(hips,"Neck",new Vector3(0,.50f,0),new Vector3(.09f,.035f,.09f),"skin",PrimitiveType.Cylinder);
            head=art.Group(hips,"WorkerHead",new Vector3(0,.62f,0));
            art.Part(head,"Face",Vector3.zero,new Vector3(.205f,.25f,.21f),"skin",PrimitiveType.Capsule);
            // Keep the accepted head/neck pose and face proportions. Shallow rounded features
            // replace the stacked cubes; the beard uses untextured hair shading,
            // rather than the metallic rust palette used by equipment and boots.
            art.Part(head,"Nose",new Vector3(0,-.012f,.109f),new Vector3(.031f,.040f,.033f),"skin",PrimitiveType.Sphere);
            art.Part(head,"Beard",new Vector3(0,-.078f,.084f),new Vector3(.135f,.054f,.050f),"dark",PrimitiveType.Sphere);
            // Open safety frames, with visible eyes behind them. A single opaque
            // .19m dark slab used to hide the entire eye region.
            Transform glasses=art.Group(head,"SafetyGlasses",Vector3.zero);
            for(int side=-1;side<=1;side+=2)
            {
                string eye=side<0?"Left":"Right";
                float x=side*.049f;
                // Seat the eyes in the curved face instead of floating on a flat plane.
                Vector3 eyeCentre=new Vector3(x,.035f,.090f),eyeEuler=new Vector3(0,side*28f,0);
                Vector3 eyeForward=Quaternion.Euler(eyeEuler)*Vector3.forward;
                art.Part(head,"EyeWhite_"+eye,eyeCentre,new Vector3(.022f,.014f,.012f),"white",PrimitiveType.Sphere,eyeEuler);
                art.Part(head,"EyePupil_"+eye,eyeCentre+eyeForward*.007f,new Vector3(.006f,.008f,.002f),"dark",PrimitiveType.Sphere,eyeEuler);
                foreach(int edge in new[]{-1,1})
                {
                    art.Part(glasses,"RimHorizontal_"+eye+"_"+edge,new Vector3(x,.035f+edge*.022f,.113f),new Vector3(.068f,.004f,.006f),"dark");
                    art.Part(glasses,"RimVertical_"+eye+"_"+edge,new Vector3(x+edge*.034f,.035f,.113f),new Vector3(.004f,.044f,.006f),"dark");
                }
                art.Rod(glasses,"Temple_"+eye,new Vector3(side*.085f,.035f,.113f),new Vector3(side*.099f,.035f,.02f),.005f,"dark");
            }
            art.Part(glasses,"Bridge",new Vector3(0,.052f,.113f),new Vector3(.030f,.004f,.006f),"dark");
            art.Part(head,"YellowHardHat",new Vector3(0,.13f,0),new Vector3(.255f,.13f,.25f),"yellow",PrimitiveType.Sphere);
            art.Part(head,"HardHatBrim",new Vector3(0,.11f,.008f),new Vector3(.275f,.006f,.27f),"yellow",PrimitiveType.Cylinder);
            art.Recycle(head,new Vector3(0,.137f,.126f),.037f);
            leftArm=Arm(chest,-1);rightArm=Arm(chest,1);
            weaponAnchor=art.Group(body,"V11_ThirdPersonWeaponAnchor",Vector3.zero);
            leftLeg=Leg(-1,out leftKnee);rightLeg=Leg(1,out rightKnee);
        }

        private void OnEnable()
        {
            if (!body) return;
            body.gameObject.SetActive(true);
            foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>(true))
                if (perspective) perspective.SetBodyRendererBaseline(renderer, true, ShadowCastingMode.On);
        }
        private void OnDisable()
        {
            if (body) body.gameObject.SetActive(false);
        }
        private void Update()
        {
            var input = PlayerInputService.Instance;
            float target = 0f;
            if (perspective && perspective.IsThirdPerson && input != null &&
                input.Context == PlayerInputContext.Gameplay && PlayerCursor.LockState == CursorLockMode.Locked)
            {
                if (input.LeanLeftActive) target = -1f;
                if (input.LeanRightActive) target = 1f;
                if (target != 0f)
                {
                    Vector3 origin = transform.position + transform.up * Mathf.Min(motor.height * .8f, 1.4f);
                    Vector3 candidate = origin + transform.right * (target * .21f);
                    foreach (Collider hit in Physics.OverlapSphere(candidate, .10f, ~0, QueryTriggerInteraction.Ignore))
                        if (!hit.transform.IsChildOf(transform)) { target = 0f; break; }
                }
            }
            visualLean = Mathf.MoveTowards(visualLean, target, Time.deltaTime * 5.5f);
        }
        private Transform Arm(Transform chest,int side)
        {
            Transform joint=art.Group(chest,side<0?"LeftShoulder":"RightShoulder",new Vector3(side*.23f,.10f,0));
            art.Part(joint,"WorkSleeve",Vector3.down*(UpperArmLength*.5f),new Vector3(.13f,UpperArmLength,.14f),"dark",PrimitiveType.Capsule);
            Transform elbow=art.Group(joint,side<0?"LeftElbow":"RightElbow",Vector3.down*UpperArmLength);
            art.Part(elbow,"WorkForearm",Vector3.down*(ForearmLength*.5f),new Vector3(.11f,ForearmLength,.12f),"dark",PrimitiveType.Capsule);
            Transform hand=art.Part(elbow,"ProtectiveGlove",Vector3.down*ForearmLength,new Vector3(.12f,.14f,.11f),"yellow",PrimitiveType.Capsule);
            if(side<0){leftElbow=elbow;leftHand=hand;}else{rightElbow=elbow;rightHand=hand;}
            return joint;
        }

        private Transform Leg(int side,out Transform knee)
        {
            Transform thigh=art.Group(hips,side<0?"LeftHip":"RightHip",new Vector3(side*.095f,-.08f,0));
            art.Part(thigh,"TrouserThigh",new Vector3(0,-.17f,0),new Vector3(.15f,.34f,.17f),"dark",PrimitiveType.Capsule);
            knee=art.Group(thigh,"Knee",new Vector3(0,-.32f,0));
            art.Part(knee,"Shin",new Vector3(0,-.15f,0),new Vector3(.13f,.31f,.15f),"dark",PrimitiveType.Capsule);
            art.Part(knee,"KneeProtection",new Vector3(0,0,.085f),new Vector3(.13f,.12f,.035f),"rust");
            art.Part(knee,"ReflectiveAnkle",new Vector3(0,-.23f,.08f),new Vector3(.12f,.025f,.01f),"white");
            art.Part(knee,"WorkBoot",new Vector3(0,-.36f,.045f),new Vector3(.16f,.13f,.28f),"rust");
            art.Part(knee,"BootSole",new Vector3(0,-.42f,.045f),new Vector3(.17f,.025f,.29f),"dark");
            return thigh;
        }
        private void LateUpdate()
        {
            if(!motor || !body)return;
            Vector3 travel=Vector3.ProjectOnPlane(motor.velocity,Vector3.up);
            float speed=travel.magnitude;
            if(HoldingWeapon && perspective && perspective.IsThirdPerson)
                body.localRotation=Quaternion.LookRotation(transform.InverseTransformDirection(perspective.CameraFlatForward));
            if(speed>.15f)
            {
                Vector3 local=transform.InverseTransformDirection(travel.normalized);
                if(!HoldingWeapon)body.localRotation=Quaternion.RotateTowards(body.localRotation,Quaternion.LookRotation(local),Time.deltaTime*600f);
                phase+=Time.deltaTime*Mathf.Lerp(5f,12f,Mathf.Clamp01(speed/7f));
            }
            float crouch=Mathf.Clamp01((1.8f-motor.height)/.7f);
            bool prone=motor.height<.85f;
            float stride=Mathf.Sin(phase)*Mathf.Clamp01(speed/4f)*(motor.isGrounded?1f:0f);
            // Visual-only torso/head lean. Keep hips, legs, controller capsule and
            // first-person camera on their existing paths, including while prone.
            float lean=perspective && perspective.IsThirdPerson
                ? visualLean : 0f;
            hips.localPosition=new Vector3(0,prone?.36f:Mathf.Lerp(.88f,.55f,crouch),0);
            hips.localRotation=Quaternion.Euler(prone?82f:crouch*18f,0,0);
            chest.localPosition=new Vector3(lean*.075f,.28f,0);
            chest.localRotation=Quaternion.Euler(0,0,-lean*12f);
            head.localPosition=new Vector3(lean*.09f,.62f,0);
            head.localRotation=Quaternion.Euler(prone?-65f:-crouch*12f,0,-lean*7f);
            if(HoldingWeapon && perspective)
                head.localRotation*=Quaternion.Euler(Mathf.Clamp(perspective.OrbitPitch*.25f,-15f,15f),0,0);
            leftLeg.localRotation=Quaternion.Euler(prone?-15f:crouch*-55f+stride*28f,0,0);
            rightLeg.localRotation=Quaternion.Euler(prone?15f:crouch*-55f-stride*28f,0,0);
            leftKnee.localRotation=rightKnee.localRotation=Quaternion.Euler(prone?20f:crouch*95f,0,0);
            if(!HoldingWeapon)
            {
                leftArm.localRotation=Quaternion.Euler(prone?-135f:-stride*24f-crouch*25f,0,prone?-10f:-5f);
                rightArm.localRotation=Quaternion.Euler(prone?-135f:stride*24f-crouch*25f,0,prone?10f:5f);
                leftElbow.localRotation=rightElbow.localRotation=Quaternion.identity;
                leftHand.localRotation=rightHand.localRotation=Quaternion.identity;
            }
        }
        private void OnDestroy()
        {
            if (body)
            {
                if (perspective) perspective.RemoveBodyRenderers(body);
                body.gameObject.SetActive(false);
                foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterials = System.Array.Empty<Material>();
                Destroy(body.gameObject);
            }
            if (art != null) art.Dispose();
        }
    }
}
