using UnityEngine;

namespace C3TreasurePuzzle
{
    // Owns one freshly created runtime material on a single-slot renderer.
    // The tracer/held-part GameObject owns this component, so delayed Destroy,
    // item changes and scene unload all use the same release path.
    [DisallowMultipleComponent]
    public sealed class C3OwnedVisualMaterial : MonoBehaviour
    {
        private Renderer target;
        private Material owned;

        public static void Assign(Renderer renderer, Material material)
        {
            var owner = renderer.gameObject.AddComponent<C3OwnedVisualMaterial>();
            owner.target = renderer;
            owner.owned = material;
            renderer.sharedMaterial = material;
        }

        private void OnDestroy()
        {
            if (target && target.sharedMaterial == owned)
            {
                target.enabled = false;
                target.sharedMaterial = null;
            }
            if (owned) Destroy(owned);
            owned = null;
        }
    }
}
