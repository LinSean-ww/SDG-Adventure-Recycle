using UnityEngine;

namespace C3TreasurePuzzle
{
    [DisallowMultipleComponent]
    public sealed class C3SalvageAppearance : MonoBehaviour
    {
        private C3VisualBuilder art;
        private readonly System.Collections.Generic.Dictionary<Material, Material> runtimePalette =
            new System.Collections.Generic.Dictionary<Material, Material>();
        [SerializeField] private Transform visualRoot;
        [SerializeField] private C3ProximityHint hint;
        private void Start() => EnsureLayout();

        public void EnsureLayout()
        {
            if(visualRoot)
            {
                if (Application.isPlaying && art == null && runtimePalette.Count == 0)
                    C3FactoryLayoutUtility.BindOwnedPalette(visualRoot.gameObject, runtimePalette);
                return;
            }
            EcoEnergyPickup pickup=GetComponent<EcoEnergyPickup>();if(!pickup)return;
            art=new C3VisualBuilder();
            Transform r=visualRoot=art.Group(transform,"V11_SalvageArt_Visual",Vector3.zero);
            Color hintColor;
            if(pickup.Kind==EcoEnergyPickup.SalvageKind.PlasticBottle)
            {art.Bottle(r,Vector3.zero,1f);hintColor=new Color(.20f,.85f,.45f);}
            else if(pickup.Kind==EcoEnergyPickup.SalvageKind.MetalCan)
            {art.Can(r,Vector3.zero,1f);hintColor=new Color(.95f,.65f,.20f);}
            else
            {
                for(int i=0;i<5;i++)
                {
                    art.Part(r,"RecoveredPaper",new Vector3(0,.025f+i*.024f,0),new Vector3(.30f,.013f,.23f),"paper",PrimitiveType.Cube,new Vector3(0,i*4,0));
                    for(int line=0;line<4;line++)art.Part(r,"PrintedNewspaperColumn",new Vector3(-.10f+line*.065f,.033f+i*.024f,0),new Vector3(.035f,.002f,.16f),"dark");
                }
                hintColor=new Color(.85f,.80f,.53f);
            }
            hint=r.gameObject.AddComponent<C3ProximityHint>();
            hint.Configure(hintColor);
        }
        private void OnEnable(){if(visualRoot)visualRoot.gameObject.SetActive(true);}
        private void OnDisable(){if(visualRoot)visualRoot.gameObject.SetActive(false);}
        private void OnDestroy()
        {
            if (!Application.isPlaying) return;
            if(hint){hint.enabled=false;hint.ReleaseVisuals();}
            if(visualRoot)
            {
                visualRoot.gameObject.SetActive(false);
                foreach(Renderer renderer in visualRoot.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterials=System.Array.Empty<Material>();
                Destroy(visualRoot.gameObject);
            }
            if(art!=null)art.Dispose();
            foreach (Material material in runtimePalette.Values) C3FactoryLayoutUtility.ReleaseTransient(material);
            runtimePalette.Clear();
        }
    }
}
