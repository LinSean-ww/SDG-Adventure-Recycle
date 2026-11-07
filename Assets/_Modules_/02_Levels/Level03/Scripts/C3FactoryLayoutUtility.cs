using UnityEngine;
using System.Collections.Generic;

namespace C3TreasurePuzzle
{
    // Only construction helpers call this. Runtime still uses deferred destruction.
    public static class C3FactoryLayoutUtility
    {
        public static void Remove(Object item)
        {
            if (!item) return;
            if (Application.isPlaying) Object.Destroy(item);
            else Object.DestroyImmediate(item);
        }

        public static void ReleaseTransient(Object item)
        {
            if (!item || !Application.isPlaying) return;
#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.IsPersistent(item)) return;
#endif
            Object.Destroy(item);
        }

        // Visual owners have an explicit removable lifetime in V12. Give those
        // owners session copies without changing the shared authored assets.
        public static void BindOwnedPalette(GameObject root, Dictionary<Material, Material> copies)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (!source) continue;
                    if (!copies.TryGetValue(source, out Material copy))
                    {
                        copy = new Material(source) { name = source.name };
                        copies.Add(source, copy);
                    }
                    materials[i] = copy;
                }
                renderer.sharedMaterials = materials;
            }
        }
    }
}
