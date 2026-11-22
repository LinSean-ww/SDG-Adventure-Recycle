using UnityEngine;
using UnityEngine.UI;

namespace SDGAdventure.Level03StoryV2
{
    // A continuous (joined, not rectangular blocks) 2D spline ribbon mesh.
    // Static grid, glowing hazard borders, and pulsing current share one graphic;
    // no instantiated line segments or expensive collision components.
    public sealed class SDGLevel03TraceGraphic : MaskableGraphic
    {
        public Vector2[] Points;
        private float progress;
        private float pulse;
        public float Progress
        {
            get { return progress; }
            set { float v=Mathf.Clamp01(value);if(Mathf.Abs(v-progress)>.002f){progress=v;SetVerticesDirty();} }
        }
        public void AdvancePulse(float time)
        {
            float v=Mathf.Repeat(time*.21f,1f);
            if(Mathf.Abs(v-pulse)>.012f){pulse=v;SetVerticesDirty();}
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if(Points==null || Points.Length<2)return;
            // Subtle industrial engineering grid, behind the conducting cable.
            for(int x=-340;x<=340;x+=34)
                Quad(vh,new Vector2(x,-112),new Vector2(x+1,-112),new Vector2(x+1,112),new Vector2(x,112),new Color32(64,118,132,26));
            for(int y=-110;y<=110;y+=22)
                Quad(vh,new Vector2(-341,y),new Vector2(341,y),new Vector2(341,y+1),new Vector2(-341,y+1),new Color32(64,118,132,26));
            // Miter-joined continuous spline: no cracks or square seams.
            Strip(vh,Points,0,Points.Length-1,43f,new Color32(219,49,72,37));
            Strip(vh,Points,0,Points.Length-1,34f,new Color32(236,61,77,235));
            Strip(vh,Points,0,Points.Length-1,27.5f,new Color32(44,152,165,255));
            Strip(vh,Points,0,Points.Length-1,18.5f,new Color32(6,31,43,255));
            Strip(vh,Points,0,Points.Length-1,1.5f,new Color32(67,194,204,110));
            for(int i=2;i<Points.Length-1;i+=15)
                Strip(vh,Points,i,Mathf.Min(i+6,Points.Length-1),1.4f,new Color32(143,243,229,160));
            // A permanently visible glowing tracer follows ONLY the player's path.
            int n=Mathf.Clamp(Mathf.RoundToInt(progress*(Points.Length-1)),0,Points.Length-1);
            if(n>1)
            {
                Strip(vh,Points,0,n,7f,new Color32(27,246,214,52));
                Strip(vh,Points,0,n,3f,new Color32(133,255,241,255));
                Disc(vh,Points[n],9f,new Color32(164,255,253,255));
            }
            // Animated electric packet travels along the whole cable.
            int pi=Mathf.Clamp(Mathf.RoundToInt(pulse*(Points.Length-1)),0,Points.Length-2);
            Strip(vh,Points,pi,Mathf.Min(pi+11,Points.Length-1),5.5f,new Color32(159,255,245,140));
            Disc(vh,Points[pi],5f,new Color32(235,255,249,255));
            // Open-end terminals are rendered as actual concentric circular shapes.
            Ring(vh,Points[0],24f,17f,new Color32(65,236,136,230));
            Ring(vh,Points[Points.Length-1],24f,17f,new Color32(255,196,66,245));
        }
        private static void Strip(VertexHelper vh,Vector2[] p,int from,int to,float half,Color32 color)
        {
            if(to<=from)return;
            int count=to-from+1;
            int start=vh.currentVertCount;
            for(int i=0;i<count;i++)
            {
                int k=from+i;
                Vector2 delta=p[Mathf.Min(p.Length-1,k+1)]-p[Mathf.Max(0,k-1)];
                if(delta.sqrMagnitude<.0001f)delta=Vector2.right;
                Vector2 normal=new Vector2(-delta.y,delta.x).normalized*half;
                UIVertex v=UIVertex.simpleVert;v.color=color;
                v.position=p[k]+normal;vh.AddVert(v);
                v.position=p[k]-normal;vh.AddVert(v);
            }
            for(int i=0;i<count-1;i++)
            {
                int j=start+i*2;
                vh.AddTriangle(j,j+1,j+3);vh.AddTriangle(j,j+3,j+2);
            }
        }
        private static void Quad(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color32 color)
        {
            int start=vh.currentVertCount;
            UIVertex v=UIVertex.simpleVert;v.color=color;
            v.position=a;vh.AddVert(v);
            v.position=b;vh.AddVert(v);
            v.position=c;vh.AddVert(v);
            v.position=d;vh.AddVert(v);
            vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+3);
        }
        private static void Disc(VertexHelper vh,Vector2 center,float radius,Color32 color)
        {
            const int n=16;
            for(int i=0;i<n;i++)
            {
                float a=i*Mathf.PI*2/n,b=(i+1)*Mathf.PI*2/n;
                Vector2 p1=center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;
                Vector2 p2=center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius;
                Quad(vh,center,p1,p2,center,color);
            }
        }
        private static void Ring(VertexHelper vh,Vector2 center,float outer,float inner,Color32 color)
        {
            const int n=24;
            for(int i=0;i<n;i++)
            {
                float a=i*Mathf.PI*2/n,b=(i+1)*Mathf.PI*2/n;
                Vector2 q0=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
                Vector2 q1=new Vector2(Mathf.Cos(b),Mathf.Sin(b));
                Quad(vh,center+q0*outer,center+q1*outer,center+q1*inner,center+q0*inner,color);
            }
        }
        public static Vector2[] Smooth(Vector2[] knots,int samplesPerSegment)
        {
            int n=(knots.Length-1)*samplesPerSegment+1;
            Vector2[] result=new Vector2[n];int k=0;
            for(int i=0;i<knots.Length-1;i++)
            {
                Vector2 p0=knots[Mathf.Max(0,i-1)],p1=knots[i],p2=knots[i+1],p3=knots[Mathf.Min(knots.Length-1,i+2)];
                for(int j=0;j<samplesPerSegment;j++)
                {
                    float t=j/(float)samplesPerSegment,t2=t*t,t3=t2*t;
                    result[k++]=.5f*((2f*p1)+(-p0+p2)*t+(2f*p0-5f*p1+4f*p2-p3)*t2+(-p0+3f*p1-3f*p2+p3)*t3);
                }
            }
            result[k]=knots[knots.Length-1];return result;
        }
    }
}
