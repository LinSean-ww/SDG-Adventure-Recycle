using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace C3TreasurePuzzle
{
    // An additive, depth-tested shell owns ALL hint writes. Source renderers are
    // borrowed: their materials and global/indexed property blocks are never set.
    // Unity cannot remove just one MPB property or merge an old snapshot with
    // concurrent writers, so restoring a whole source block would lose their work.
    internal sealed class C3HintVisuals : IDisposable
    {
        internal const string OverlayPrefix = "V11_HintOverlay_";
        private sealed class Surface
        {
            public Renderer Source, Overlay;
            public Material[] OriginalMaterials;
            public MaterialPropertyBlock OriginalGlobal;
            public MaterialPropertyBlock[] OriginalIndexed;
        }

        private readonly List<Surface> surfaces = new List<Surface>();
        private readonly MaterialPropertyBlock cue = new MaterialPropertyBlock();
        private Material material;
        private static readonly int Emission = Shader.PropertyToID("_EmissionColor");

        public C3HintVisuals(Transform owner)
        {
            Renderer[] sources = owner.GetComponentsInChildren<Renderer>(true);
            Shader shader = Resources.Load<Shader>("V11Visual/C3HintOverlay");
            if (!shader)
            {
                Debug.LogError("V11 hint shader is missing: V11Visual/C3HintOverlay", owner);
                return;
            }
            material = new Material(shader) { name = "V11_OwnedHintOverlay" };
            material.EnableKeyword("_EMISSION");
            foreach (Renderer source in sources)
            {
                if (source.name.StartsWith(OverlayPrefix, StringComparison.Ordinal)) continue;
                MeshFilter filter = source.GetComponent<MeshFilter>();
                SkinnedMeshRenderer skin = source as SkinnedMeshRenderer;
                Mesh mesh = skin ? skin.sharedMesh : filter ? filter.sharedMesh : null;
                if (!mesh || (!(source is MeshRenderer) && !skin)) continue;

                var state = new Surface
                {
                    Source = source,
                    OriginalMaterials = source.sharedMaterials,
                    OriginalGlobal = new MaterialPropertyBlock()
                };
                source.GetPropertyBlock(state.OriginalGlobal);
                state.OriginalIndexed = new MaterialPropertyBlock[state.OriginalMaterials.Length];
                for (int i = 0; i < state.OriginalIndexed.Length; i++)
                {
                    state.OriginalIndexed[i] = new MaterialPropertyBlock();
                    source.GetPropertyBlock(state.OriginalIndexed[i], i);
                }

                GameObject shell = new GameObject(OverlayPrefix + source.name);
                shell.layer = source.gameObject.layer;
                shell.transform.SetParent(source.transform, false);
                if (skin)
                {
                    var copy = shell.AddComponent<SkinnedMeshRenderer>();
                    copy.sharedMesh = mesh;
                    copy.bones = skin.bones;
                    copy.rootBone = skin.rootBone;
                    copy.localBounds = skin.localBounds;
                    copy.updateWhenOffscreen = skin.updateWhenOffscreen;
                    state.Overlay = copy;
                }
                else
                {
                    shell.AddComponent<MeshFilter>().sharedMesh = mesh;
                    state.Overlay = shell.AddComponent<MeshRenderer>();
                }
                var slots = new Material[mesh.subMeshCount];
                for (int i = 0; i < slots.Length; i++) slots[i] = material;
                state.Overlay.sharedMaterials = slots;
                state.Overlay.shadowCastingMode = ShadowCastingMode.Off;
                state.Overlay.receiveShadows = false;
                state.Overlay.lightProbeUsage = LightProbeUsage.Off;
                state.Overlay.reflectionProbeUsage = ReflectionProbeUsage.Off;
                state.Overlay.enabled = false;
                surfaces.Add(state);
            }
        }

        public void SetCue(Color color, float strength)
        {
            cue.SetColor(Emission, color * Mathf.Max(0f, strength));
            foreach (Surface state in surfaces)
            {
                if (!state.Overlay) continue;
                bool visible = strength > 0f && state.Source && state.Source.enabled &&
                    !state.Source.forceRenderingOff && state.Source.gameObject.activeInHierarchy &&
                    state.Source.shadowCastingMode != ShadowCastingMode.ShadowsOnly;
                state.Overlay.enabled = visible;
                if (!visible) continue;
                if (state.Source is SkinnedMeshRenderer skin && state.Overlay is SkinnedMeshRenderer copy)
                    for (int i = 0; i < skin.sharedMesh.blendShapeCount; i++)
                        copy.SetBlendShapeWeight(i, skin.GetBlendShapeWeight(i));
                state.Overlay.SetPropertyBlock(cue);
            }
        }

        public void Dispose()
        {
            foreach (Surface state in surfaces)
            {
                if (!state.Overlay) continue;
                state.Overlay.enabled = false;
                state.Overlay.sharedMaterials = Array.Empty<Material>();
                UnityEngine.Object.Destroy(state.Overlay.gameObject);
            }
            surfaces.Clear();
            if (material) UnityEngine.Object.Destroy(material);
            material = null;
            // Originals need no write-back: no source reference/block was changed.
            // In particular, keep external changes made AFTER the snapshots above.
        }
    }
}
