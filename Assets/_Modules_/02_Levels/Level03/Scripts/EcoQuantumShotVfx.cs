using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Scene/weapon-owned bounded pool. Presentation only; never applies damage.</summary>
[DisallowMultipleComponent]
public sealed class EcoQuantumShotVfx : MonoBehaviour
{
    public const int Capacity = 12;
    [SerializeField, Range(.15f,.4f)] private float lifetime = .28f;
    private sealed class Pulse
    {
        public GameObject Root;
        public LineRenderer Core, Halo, Flash;
        public readonly LineRenderer[] Sparks = new LineRenderer[5];
        public Light Impact;
        public Vector3 Start, End, Normal;
        public float Age;
        public bool Hit, Active;
    }
    private readonly Pulse[] pool = new Pulse[Capacity];
    private Material material;
    private int next;
    public Vector3 LastVisualStart { get; private set; }
    public Vector3 LastVisualEnd { get; private set; }
    public int ActiveCount
    {
        get { int count=0; foreach(Pulse p in pool)if(p!=null && p.Active)count++; return count; }
    }

    private void Awake()
    {
        Shader shader=Shader.Find("Sprites/Default");
        if(!shader)shader=Shader.Find("Legacy Shaders/Particles/Additive");
        if(!shader){Debug.LogError("Quantum VFX requires a built-in unlit particle shader.",this);enabled=false;return;}
        material=new Material(shader){name="V12 pooled quantum glow"};
        for(int i=0;i<Capacity;i++)
        {
            Pulse p=new Pulse {Root=new GameObject("V12_QuantumPulse_"+i)};
            p.Root.transform.SetParent(transform,false);
            p.Core=Line(p.Root,"BrightCore",.022f);
            p.Halo=Line(p.Root,"CyanTail",.095f);
            p.Flash=Line(p.Root,"MuzzleStar",.045f,6);
            for(int j=0;j<p.Sparks.Length;j++)p.Sparks[j]=Line(p.Root,"ImpactSpark_"+j,.016f);
            p.Impact=new GameObject("ImpactLight").AddComponent<Light>();
            p.Impact.transform.SetParent(p.Root.transform,false);
            p.Impact.type=LightType.Point;p.Impact.shadows=LightShadows.None;
            p.Impact.range=1.8f;p.Impact.color=new Color(.1f,.8f,1f);
            p.Root.SetActive(false);pool[i]=p;
        }
    }
    private LineRenderer Line(GameObject parent,string label,float width,int points=2)
    {
        GameObject go=new GameObject(label);go.transform.SetParent(parent.transform,false);
        LineRenderer line=go.AddComponent<LineRenderer>();
        line.sharedMaterial=material;line.positionCount=points;line.useWorldSpace=true;
        line.startWidth=width;line.endWidth=width*.3f;line.numCapVertices=3;
        line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
        return line;
    }
    private static bool Nearest(Vector3 from,Vector3 to,Transform owner,out RaycastHit nearest)
    {
        nearest=default;Vector3 delta=to-from;float distance=delta.magnitude;
        if(distance<.0001f)return false;
        float best=distance+.0001f;bool found=false;
        foreach(RaycastHit hit in Physics.RaycastAll(from,delta/distance,distance,~0,QueryTriggerInteraction.Ignore))
            if(hit.collider && !hit.transform.IsChildOf(owner) && hit.distance<best)
            {best=hit.distance;nearest=hit;found=true;}
        return found;
    }
    public void Fire(Vector3 eye,Vector3 muzzle,Vector3 hit,Transform owner,bool surfaceHit)
    {
        if(!isActiveAndEnabled || !material || !owner)return;
        Vector3 start=muzzle;
        // Retraction/ADS can move the muzzle behind or beyond nearby cover.
        // Clip both eye->muzzle and muzzle->hit, without changing the real hit.
        if(Nearest(eye,start,owner,out RaycastHit cover))
            start=cover.point+(eye-cover.point).normalized*.025f;
        foreach(Collider overlap in Physics.OverlapSphere(start,.015f,~0,QueryTriggerInteraction.Ignore))
            if(overlap && !overlap.transform.IsChildOf(owner)){start=eye;break;}
        Vector3 end=hit,normal=(start-hit).normalized;
        if(Nearest(start,end,owner,out RaycastHit obstacle))
        {end=obstacle.point+obstacle.normal*.012f;normal=obstacle.normal;surfaceHit=true;}
        // Avoid endpoint depth fighting even when camera and muzzle rays coincide.
        else if(surfaceHit)end=Vector3.MoveTowards(hit,start,.012f);
        Pulse p=pool[next];next=(next+1)%Capacity;
        p.Start=LastVisualStart=start;p.End=LastVisualEnd=end;
        p.Normal=normal;p.Hit=surfaceHit;p.Age=0f;p.Active=true;p.Root.SetActive(true);Draw(p);
    }
    private void Update()
    {
        foreach(Pulse p in pool)
        {
            if(p==null || !p.Active)continue;
            p.Age+=Time.deltaTime;
            if(p.Age>=Mathf.Clamp(lifetime,.15f,.4f)){p.Active=false;p.Root.SetActive(false);}
            else Draw(p);
        }
    }
    private void Draw(Pulse p)
    {
        float duration=Mathf.Clamp(lifetime,.15f,.4f),t=p.Age/duration;
        float arrival=Mathf.Min(.065f,Vector3.Distance(p.Start,p.End)/500f);
        float head=arrival<.001f?1f:Mathf.Clamp01((p.Age+.008f)/arrival);
        float tail=Mathf.Clamp01((p.Age-.055f)/Mathf.Max(.01f,duration-.055f));
        Vector3 a=Vector3.Lerp(p.Start,p.End,Mathf.Min(tail,head)),b=Vector3.Lerp(p.Start,p.End,head);
        p.Core.SetPosition(0,a);p.Core.SetPosition(1,b);
        p.Halo.SetPosition(0,a);p.Halo.SetPosition(1,b);
        p.Core.startColor=p.Core.endColor=new Color(.72f,1f,1f,1f-t);
        p.Halo.startColor=new Color(.02f,.5f,1f,(1f-t)*.5f);
        p.Halo.endColor=new Color(.05f,1f,1f,(1f-t)*.75f);
        Vector3 forward=(p.End-p.Start).normalized;
        if(forward.sqrMagnitude<.001f)forward=Vector3.forward;
        Vector3 right=Vector3.Cross(forward,Vector3.up).normalized;
        if(right.sqrMagnitude<.001f)right=Vector3.right;
        Vector3 up=Vector3.Cross(right,forward).normalized;
        p.Flash.enabled=p.Age<.065f;
        float flash=(1f-Mathf.Clamp01(p.Age/.065f))*.18f;
        p.Flash.SetPosition(0,p.Start-right*flash);p.Flash.SetPosition(1,p.Start+right*flash);
        p.Flash.SetPosition(2,p.Start);p.Flash.SetPosition(3,p.Start+up*flash);
        p.Flash.SetPosition(4,p.Start-up*flash);p.Flash.SetPosition(5,p.Start+forward*flash);
        p.Flash.startColor=p.Flash.endColor=new Color(.7f,1f,1f,1f);
        bool impact=p.Hit && p.Age>=arrival;
        p.Impact.enabled=impact;p.Impact.transform.position=p.End;
        p.Impact.intensity=impact?2.2f*(1f-t):0f;
        Vector3 sparkRight=Vector3.Cross(p.Normal,Vector3.up).normalized;
        if(sparkRight.sqrMagnitude<.001f)sparkRight=Vector3.right;
        Vector3 sparkUp=Vector3.Cross(sparkRight,p.Normal).normalized;
        for(int i=0;i<p.Sparks.Length;i++)
        {
            LineRenderer spark=p.Sparks[i];spark.enabled=impact;
            float angle=i*Mathf.PI*2f/p.Sparks.Length;
            Vector3 direction=(sparkRight*Mathf.Cos(angle)+sparkUp*Mathf.Sin(angle)+p.Normal*.5f).normalized;
            float travel=Mathf.Max(0f,p.Age-arrival)*1.5f;
            spark.SetPosition(0,p.End+direction*travel);
            spark.SetPosition(1,p.End+direction*(travel+.09f*(1f-t)));
            spark.startColor=spark.endColor=i%2==0?new Color(1f,.55f,.13f,1f-t):new Color(.1f,.9f,1f,1f-t);
        }
    }
    public void Clear()
    {
        foreach(Pulse p in pool)if(p!=null){p.Active=false;if(p.Root)p.Root.SetActive(false);}
    }
    private void OnDisable()=>Clear();
    private void OnDestroy()
    {
        Clear();
        foreach(Pulse p in pool)if(p!=null && p.Root)Destroy(p.Root);
        if(material)Destroy(material);
    }
}
