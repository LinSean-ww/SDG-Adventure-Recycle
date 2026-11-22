using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SDGAdventure.Level03StoryV2
{
    // The player grabs real 3D parts in a RenderTexture and drags them to a glowing
    // 3D socket. There are no text cards masquerading as assembly interactions.
    public sealed class SDGLevel03AssemblyPreview : MonoBehaviour
    {
        private const int PreviewLayer = 30;
        private readonly List<Material> materials = new List<Material>();
        private Transform root;
        private Camera cam;
        private RenderTexture texture;
        private RawImage image;
        private readonly Transform[] parts = new Transform[3];
        private readonly bool[] installed = new bool[3];
        private readonly Vector3[] staging = { new Vector3(-1.36f,-.27f,0), new Vector3(-1.36f,.24f,0), new Vector3(-1.36f,.72f,0) };
        private readonly Vector3[] mount = { new Vector3(.35f,-.06f,0), new Vector3(.33f,.10f,-.10f), new Vector3(.68f,.23f,.08f) };
        private GameObject beacon;
        private Transform grabbed;
        private int requiredPart;
        private bool shown;
        public System.Action<int> OnPartSnapped;

        public void Initialize(RectTransform parent)
        {
            if (image) return;
            GameObject ui = new GameObject("V2.3 3D interactable assembly viewport",typeof(RectTransform),typeof(RawImage));
            ui.transform.SetParent(parent,false);
            RectTransform r = ui.GetComponent<RectTransform>();
            r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.pivot=new Vector2(.5f,.5f);
            r.sizeDelta=new Vector2(720f,298f);r.anchoredPosition=new Vector2(0f,-24f);
            image=ui.GetComponent<RawImage>();image.raycastTarget=true;
            SDGLevel03AssemblyInteractor interaction=ui.AddComponent<SDGLevel03AssemblyInteractor>();
            interaction.Preview=this;
            texture=new RenderTexture(768,384,16,RenderTextureFormat.ARGB32);
            texture.name="V23_Actual3DPartAssembly";
            texture.antiAliasing=2;texture.Create();image.texture=texture;
            root=new GameObject("V23_Isolated3DAssemblyWorld").transform;
            root.position=new Vector3(9000f,9000f,9000f);
            root.gameObject.layer=PreviewLayer;
            GameObject cameraGO=new GameObject("V23_Assembly3DCamera");
            cam=cameraGO.AddComponent<Camera>();
            cam.cullingMask=1<<PreviewLayer;
            cam.clearFlags=CameraClearFlags.SolidColor;
            cam.backgroundColor=new Color(.014f,.047f,.061f,1f);
            cam.orthographic=true;cam.orthographicSize=1.21f;
            cam.nearClipPlane=.1f;cam.farClipPlane=30f;
            cam.targetTexture=texture;
            cam.transform.position=root.position+new Vector3(.4f,.55f,-4.5f);
            cam.transform.LookAt(root.position+new Vector3(.12f,.10f,0));
            cam.enabled=false;
            GameObject lightGO=new GameObject("V23_SoftLight");
            lightGO.transform.SetParent(root,false);lightGO.layer=PreviewLayer;
            lightGO.transform.localPosition=new Vector3(-.5f,1.6f,-2f);
            Light light=lightGO.AddComponent<Light>();light.type=LightType.Point;light.range=8f;
            light.intensity=3f;light.shadows=LightShadows.None;light.cullingMask=1<<PreviewLayer;
            Shape(PrimitiveType.Cube,"Industrial table",root,new Vector3(0,-.73f,.25f),new Vector3(3.6f,.12f,1f),new Color(.12f,.25f,.28f),false);
            Shape(PrimitiveType.Cylinder,"Circular gun mounting clamp",root,new Vector3(.48f,-.55f,.12f),new Vector3(.81f,.05f,.69f),new Color(.1f,.36f,.41f),false);
            GameObject receiver=new GameObject("01_METAL_RECEIVER_GrabThis");
            receiver.transform.SetParent(root,false);parts[0]=receiver.transform;
            Shape(PrimitiveType.Cube,"Metal receiver",parts[0],Vector3.zero,new Vector3(.83f,.2f,.26f),new Color(.58f,.75f,.79f),true);
            Shape(PrimitiveType.Cylinder,"Long barrel",parts[0],new Vector3(.51f,.015f,0),new Vector3(.10f,.66f,.10f),new Color(.55f,.78f,.83f),false).transform.localRotation=Quaternion.Euler(0,0,90);
            Shape(PrimitiveType.Cube,"Grip",parts[0],new Vector3(-.2f,-.22f,0),new Vector3(.17f,.32f,.17f),new Color(.09f,.15f,.19f),false);
            Shape(PrimitiveType.Cube,"Stock",parts[0],new Vector3(-.55f,0,0),new Vector3(.28f,.2f,.22f),new Color(.28f,.41f,.46f),false);
            GameObject pcb=new GameObject("02_GREEN_PCB_GrabThis");
            pcb.transform.SetParent(root,false);parts[1]=pcb.transform;
            Shape(PrimitiveType.Cube,"Circuit board",parts[1],Vector3.zero,new Vector3(.45f,.06f,.31f),new Color(.09f,.65f,.36f),true);
            for(int j=0;j<4;j++)Shape(PrimitiveType.Cube,"Gold pin",parts[1],new Vector3(-.15f+j*.09f,.06f,0),new Vector3(.045f,.03f,.17f),new Color(.95f,.78f,.24f),false);
            GameObject cell=new GameObject("03_BLUE_ENERGY_CORE_GrabThis");
            cell.transform.SetParent(root,false);parts[2]=cell.transform;
            Shape(PrimitiveType.Sphere,"Glowing reactor",parts[2],Vector3.zero,new Vector3(.32f,.27f,.32f),new Color(.1f,.89f,1f),true);
            Shape(PrimitiveType.Cylinder,"Energy mounting ring",parts[2],new Vector3(0,-.15f,0),new Vector3(.24f,.035f,.24f),new Color(.55f,.76f,.83f),false);
            for(int i=0;i<parts.Length;i++)
            {
                parts[i].gameObject.layer=PreviewLayer;
                SDGLevel03AssemblyPartMarker tag=parts[i].gameObject.AddComponent<SDGLevel03AssemblyPartMarker>();
                tag.PartId=i;
                parts[i].localPosition=staging[i];
            }
            beacon=Shape(PrimitiveType.Sphere,"GLOWING_3D_ASSEMBLY_SOCKET",root,mount[0],new Vector3(.26f,.26f,.26f),new Color(.13f,.99f,.87f),true);
            Collider beaconCollider=beacon.GetComponent<Collider>();if(beaconCollider)Destroy(beaconCollider);
            ResetParts();SetVisible(false);
        }
        private Material Mat(Color color,bool emissive)
        {
            Shader shader=Shader.Find("Standard");if(!shader)shader=Shader.Find("Unlit/Color");
            Material m=new Material(shader);m.color=color;
            if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",.45f);
            if(m.HasProperty("_Glossiness"))m.SetFloat("_Glossiness",.75f);
            if(emissive && m.HasProperty("_EmissionColor"))
            {m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*.82f);}
            materials.Add(m);return m;
        }
        private GameObject Shape(PrimitiveType type,string name,Transform parent,Vector3 pos,Vector3 size,Color color,bool emissive)
        {
            GameObject part=GameObject.CreatePrimitive(type);part.name=name;
            part.transform.SetParent(parent,false);part.transform.localPosition=pos;part.transform.localScale=size;
            part.layer=PreviewLayer;
            Renderer render=part.GetComponent<Renderer>();if(render)render.sharedMaterial=Mat(color,emissive);
            // Keep the true 3D colliders so mouse raycasts can grab this mesh.
            return part;
        }
        public void ResetParts()
        {
            requiredPart=0;grabbed=null;
            for(int i=0;i<3;i++)
            { installed[i]=false;if(parts[i]){parts[i].localPosition=staging[i];parts[i].localRotation=Quaternion.identity;} }
            UpdateBeacon();
        }
        public void SetStage(int stage)
        {
            requiredPart=Mathf.Clamp(stage,0,3);
            UpdateBeacon();
        }
        private void UpdateBeacon()
        {
            if(!beacon)return;
            beacon.SetActive(requiredPart<3);
            if(requiredPart<3)beacon.transform.localPosition=mount[requiredPart];
        }
        public void SetVisible(bool value)
        {
            shown=value;
            if(image)image.gameObject.SetActive(value);
            if(cam)cam.enabled=value;
            if(!value)grabbed=null;
        }
        private bool MakeRay(Vector2 screen,Camera eventCamera,out Ray ray)
        {
            ray=new Ray();
            if(!image || !cam)return false;
            Vector2 point;
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(image.rectTransform,screen,eventCamera,out point))return false;
            Rect rect=image.rectTransform.rect;
            float u=(point.x-rect.xMin)/rect.width;
            float v=(point.y-rect.yMin)/rect.height;
            if(u<0||u>1||v<0||v>1)return false;
            ray=cam.ViewportPointToRay(new Vector3(u,v,0f));
            return true;
        }
        public void PointerDown(Vector2 pointer,Camera eventCamera)
        {
            if(!shown || requiredPart>=3)return;
            Ray ray;
            if(!MakeRay(pointer,eventCamera,out ray))return;
            RaycastHit[] hits=Physics.RaycastAll(ray,20f,1<<PreviewLayer,QueryTriggerInteraction.Ignore);
            float best=float.MaxValue;
            for(int i=0;i<hits.Length;i++)
            {
                SDGLevel03AssemblyPartMarker tag=hits[i].collider.GetComponentInParent<SDGLevel03AssemblyPartMarker>();
                if(tag!=null && tag.PartId==requiredPart && hits[i].distance<best)
                {best=hits[i].distance;grabbed=parts[requiredPart];}
            }
        }
        public void PointerDrag(Vector2 pointer,Camera eventCamera)
        {
            if(!grabbed)return;
            Ray ray;
            if(!MakeRay(pointer,eventCamera,out ray))return;
            Plane plane=new Plane(-cam.transform.forward,root.position);
            float distance;
            if(!plane.Raycast(ray,out distance))return;
            Vector3 local=root.InverseTransformPoint(ray.GetPoint(distance));
            grabbed.localPosition=new Vector3(Mathf.Clamp(local.x,-1.5f,1.5f),Mathf.Clamp(local.y,-.48f,.93f),staging[requiredPart].z);
        }
        public void PointerUp(Vector2 pointer,Camera eventCamera)
        {
            if(!grabbed)return;
            PointerDrag(pointer,eventCamera);
            int part=requiredPart;
            float d=Vector2.Distance(new Vector2(grabbed.localPosition.x,grabbed.localPosition.y),new Vector2(mount[part].x,mount[part].y));
            if(d<.46f)
            {
                installed[part]=true;
                grabbed.localPosition=mount[part];
                grabbed.localRotation=Quaternion.identity;
                grabbed=null;
                requiredPart++;
                UpdateBeacon();
                if(OnPartSnapped!=null)OnPartSnapped(part);
            }
            else
            {
                grabbed.localPosition=staging[part];
                grabbed=null;
            }
        }
        private void Update()
        {
            if(!shown)return;
            if(beacon && beacon.activeSelf)
            {
                float k=1f+Mathf.Sin(Time.unscaledTime*4.5f)*.20f;
                beacon.transform.localScale=Vector3.one*.26f*k;
            }
            for(int i=0;i<parts.Length;i++)
                if(installed[i] && parts[i] && parts[i]!=grabbed)
                    parts[i].localPosition=Vector3.Lerp(parts[i].localPosition,mount[i],Time.unscaledDeltaTime*10f);
        }
        private void OnDestroy()
        {
            if(cam){cam.targetTexture=null;Destroy(cam.gameObject);}
            if(texture){texture.Release();Destroy(texture);}
            if(root)Destroy(root.gameObject);
            foreach(Material m in materials)if(m)Destroy(m);
        }
    }
}
