using UnityEngine;

namespace C3TreasurePuzzle
{
    // Endpoints describe the actual top surface, not a second animation-only path.
    public sealed class C3ConveyorSection : MonoBehaviour
    {
        public Transform Start { get; private set; }
        public Transform End { get; private set; }
        public BoxCollider Surface { get; private set; }
        public Renderer SurfaceRenderer { get; private set; }
        public float Width { get; private set; }
        public Vector3 Normal => Surface.transform.up;
        public float Length => Vector3.Distance(Start.position,End.position);

        public void Configure(Transform start,Transform end,BoxCollider surface,float width)
        {
            Start=start; End=end; Surface=surface; Width=width;
            SurfaceRenderer=surface.GetComponent<Renderer>();
        }

        public Vector3 SurfacePoint(float distance) => Vector3.Lerp(Start.position,End.position,
            Length>0f?Mathf.Clamp01(distance/Length):0f);

        public bool Supports(Vector3 point,float radius,float tolerance)
        {
            if (!Surface || !Surface.enabled || !Surface.gameObject.activeInHierarchy) return false;
            return Mathf.Abs(Vector3.Distance(point,Surface.ClosestPoint(point))-radius)<=tolerance;
        }
    }
}
