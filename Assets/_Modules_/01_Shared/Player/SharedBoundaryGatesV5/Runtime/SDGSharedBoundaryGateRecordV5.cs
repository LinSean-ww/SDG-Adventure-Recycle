using UnityEngine;

namespace SDGAdventure.SharedBoundaryGates
{
    [DisallowMultipleComponent]
    public sealed class SDGSharedBoundaryGateRecordV5 : MonoBehaviour
    {
        [SerializeField] private string boundaryId;
        [SerializeField] private GameObject createdGate;
        [SerializeField] private Renderer[] originalRenderers;
        [SerializeField] private bool[] rendererEnabledBefore;
        [SerializeField] private Collider[] originalColliders;
        [SerializeField] private bool[] colliderEnabledBefore;

        public string BoundaryId { get { return boundaryId; } }
        public GameObject CreatedGate { get { return createdGate; } }
        public Renderer[] OriginalRenderers { get { return originalRenderers; } }
        public bool[] RendererEnabledBefore { get { return rendererEnabledBefore; } }
        public Collider[] OriginalColliders { get { return originalColliders; } }
        public bool[] ColliderEnabledBefore { get { return colliderEnabledBefore; } }

        public void Capture(string id, GameObject gate, Renderer[] renderers, Collider[] colliders)
        {
            boundaryId = id;
            createdGate = gate;
            originalRenderers = renderers;
            originalColliders = colliders;
            rendererEnabledBefore = new bool[renderers.Length];
            colliderEnabledBefore = new bool[colliders.Length];
            for (int i = 0; i < renderers.Length; i++)
                rendererEnabledBefore[i] = renderers[i] != null && renderers[i].enabled;
            for (int i = 0; i < colliders.Length; i++)
                colliderEnabledBefore[i] = colliders[i] != null && colliders[i].enabled;
        }
    }
}
