using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using SDGAdventure.SharedPlayer;

namespace C3TreasurePuzzle
{
    // A visual-only articulated worker. No Animator/avatar claims, input or root motion.
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class C3WorkerAppearance : MonoBehaviour
    {
        private C3VisualBuilder art;
        private CharacterController motor;
        private Transform body,head,hips,leftArm,rightArm,leftLeg,rightLeg,leftKnee,rightKnee;
        private float phase;
        private PlayerPerspectiveRig perspective;
        private Renderer[] legacyRenderers = Array.Empty<Renderer>();
        private bool[] legacyEnabled;
        private ShadowCastingMode[] legacyCasting;
        public static void Install(GameObject player)
        {
            if(player.GetComponent<C3WorkerAppearance>())return;
            player.AddComponent<C3WorkerAppearance>();
        }
        private void Awake()
        {
            motor=GetComponent<CharacterController>();
            if(!motor){enabled=false;return;}
            Transform legacy=transform.Find("fk");
            // Both the original white skin and M3_FirstPersonShadow are under fk.
            // Preserve bones/FBX/GUIDs; only replace the visual presentation.
            perspective=GetComponent<PlayerPerspectiveRig>();
            legacyRenderers=legacy?legacy.GetComponentsInChildren<Renderer>(true):Array.Empty<Renderer>();
            legacyEnabled=new bool[legacyRenderers.Length];
            legacyCasting=new ShadowCastingMode[legacyRenderers.Length];
            for(int i=0;i<legacyRenderers.Length;i++)
            {
                legacyEnabled[i]=legacyRenderers[i].enabled;
                legacyCasting[i]=legacyRenderers[i].shadowCastingMode;
            }
            art=new C3VisualBuilder();
            body=art.Group(transform,"V11_RecyclingEngineer_Visual",Vector3.zero);
            hips=art.Group(body,"WorkerPelvis",new Vector3(0,.88f,0));
            art.Part(hips,"WorkTrousers",Vector3.zero,new Vector3(.31f,.23f,.22f),"dark");
            art.Part(hips,"ToolBelt",new Vector3(0,.07f,0),new Vector3(.35f,.07f,.25f),"rust");
            art.Part(hips,"BeltBuckle",new Vector3(0,.07f,.132f),new Vector3(.065f,.05f,.016f),"silver");
            art.Part(hips,"ToolPouch",new Vector3(.20f,-.05f,0),new Vector3(.11f,.17f,.13f),"rust");
            Transform chest=art.Group(hips,"WorkerChest",new Vector3(0,.28f,0));
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
            art.Part(head,"Nose",new Vector3(0,-.01f,.112f),new Vector3(.037f,.048f,.038f),"skin");
            art.Part(head,"SafetyGlasses",new Vector3(0,.035f,.110f),new Vector3(.19f,.043f,.023f),"dark");
            art.Part(head,"Beard",new Vector3(0,-.075f,.085f),new Vector3(.15f,.065f,.055f),"rust");
            art.Part(head,"YellowHardHat",new Vector3(0,.12f,0),new Vector3(.255f,.13f,.25f),"yellow",PrimitiveType.Sphere);
            art.Part(head,"HardHatBrim",new Vector3(0,.10f,.015f),new Vector3(.29f,.015f,.30f),"yellow",PrimitiveType.Cylinder);
            art.Recycle(head,new Vector3(0,.137f,.126f),.037f);
            leftArm=Arm(chest,-1);rightArm=Arm(chest,1);
            leftLeg=Leg(-1,out leftKnee);rightLeg=Leg(1,out rightKnee);
        }

        private void OnEnable()
        {
            if(!body)return;
            for(int i=0;i<legacyRenderers.Length;i++)
                SetBaseline(legacyRenderers[i],false,legacyCasting[i]);
            body.gameObject.SetActive(true);
            foreach(Renderer renderer in body.GetComponentsInChildren<Renderer>(true))
                SetBaseline(renderer,true,ShadowCastingMode.On);
        }

        private void SetBaseline(Renderer renderer,bool enabledValue,ShadowCastingMode casting)
        {
            if(!renderer)return;
            if(perspective)perspective.SetBodyRendererBaseline(renderer,enabledValue,casting);
            else {renderer.enabled=enabledValue;renderer.shadowCastingMode=casting;}
        }

        private void OnDisable()
        {
            if(body)body.gameObject.SetActive(false);
            QueueLegacyRestore();
        }

        private void QueueLegacyRestore()
        {
            if(!perspective || legacyRenderers.Length==0)return;
            // Active players use their rig; inactive players need a surviving
            // coroutine host. Reuse the EXISTING persistent runtime solely as a
            // scheduler, never its inventory/reward API; create no new object.
            MonoBehaviour host=gameObject.activeInHierarchy?(MonoBehaviour)perspective:EcoAdventureRuntime.Instance;
            if(host && host.gameObject.activeInHierarchy)
                host.StartCoroutine(RestoreLegacyIfPlayerSurvives(gameObject,perspective,
                    legacyRenderers,legacyEnabled,legacyCasting));
        }

        private static IEnumerator RestoreLegacyIfPlayerSurvives(GameObject player,PlayerPerspectiveRig rig,
            Renderer[] renderers,bool[] enabledValues,ShadowCastingMode[] casting)
        {
            yield return null;
            while(player && rig && player.scene.isLoaded && !player.activeInHierarchy)yield return null;
            if(!player || !rig || !player.scene.isLoaded)return;
            C3WorkerAppearance replacement=player.GetComponent<C3WorkerAppearance>();
            if(replacement && replacement.isActiveAndEnabled)return;
            for(int i=0;i<renderers.Length;i++)
                if(renderers[i])rig.SetBodyRendererBaseline(renderers[i],enabledValues[i],casting[i]);
        }
        private Transform Arm(Transform chest,int side)
        {
            Transform joint=art.Group(chest,side<0?"LeftShoulder":"RightShoulder",new Vector3(side*.23f,.10f,0));
            art.Part(joint,"WorkSleeve",new Vector3(0,-.14f,0),new Vector3(.13f,.30f,.14f),"dark",PrimitiveType.Capsule);
            art.Part(joint,"ProtectiveGlove",new Vector3(0,-.34f,.025f),new Vector3(.12f,.14f,.11f),"yellow",PrimitiveType.Capsule);
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
            if(speed>.15f)
            {
                Vector3 local=transform.InverseTransformDirection(travel.normalized);
                body.localRotation=Quaternion.RotateTowards(body.localRotation,Quaternion.LookRotation(local),Time.deltaTime*600f);
                phase+=Time.deltaTime*Mathf.Lerp(5f,12f,Mathf.Clamp01(speed/7f));
            }
            float crouch=Mathf.Clamp01((1.8f-motor.height)/.7f);
            bool prone=motor.height<.85f;
            float stride=Mathf.Sin(phase)*Mathf.Clamp01(speed/4f)*(motor.isGrounded?1f:0f);
            hips.localPosition=new Vector3(0,prone?.36f:Mathf.Lerp(.88f,.55f,crouch),0);
            hips.localRotation=Quaternion.Euler(prone?82f:crouch*18f,0,0);
            head.localRotation=Quaternion.Euler(prone?-65f:-crouch*12f,0,0);
            leftLeg.localRotation=Quaternion.Euler(prone?-15f:crouch*-55f+stride*28f,0,0);
            rightLeg.localRotation=Quaternion.Euler(prone?15f:crouch*-55f-stride*28f,0,0);
            leftKnee.localRotation=rightKnee.localRotation=Quaternion.Euler(prone?20f:crouch*95f,0,0);
            leftArm.localRotation=Quaternion.Euler(prone?-135f:-stride*24f-crouch*25f,0,prone?10f:5f);
            rightArm.localRotation=Quaternion.Euler(prone?-135f:stride*24f-crouch*25f,0,prone?-10f:-5f);
        }
        private void OnDestroy()
        {
            // A coroutine on a rig is stopped if its whole GameObject is disabled.
            // Cover removal while inactive too; destroyed/unloaded players fail
            // the deferred liveness check and are never visually restored.
            if(!gameObject.activeInHierarchy)QueueLegacyRestore();
            if(body)
            {
                if(perspective)perspective.RemoveBodyRenderers(body);
                body.gameObject.SetActive(false);
                foreach(Renderer renderer in body.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterials=Array.Empty<Material>();
                Destroy(body.gameObject);
            }
            if(art!=null)art.Dispose();
        }
    }
}
