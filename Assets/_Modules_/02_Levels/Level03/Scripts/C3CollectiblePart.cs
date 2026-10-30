using System.Collections.Generic;
using UnityEngine;

namespace C3TreasurePuzzle
{
    // Proximity-only discovery cue; never marks an item through the HUD or changes collection logic.
    [RequireComponent(typeof(Collider))]
    public sealed class C3CollectiblePart : C3Interactable
    {
        [SerializeField] private string partId;
        [SerializeField] private string displayName;
        [SerializeField] private C3TreasureProgress progress;
        [SerializeField, Min(0.1f)] private float glowDistance = 3.6f;
        [SerializeField, Range(0f, 6f)] private float glowStrength = 2.2f;
        [SerializeField, Min(0f)] private float pulseSpeed = 3f;
        private Transform player;
        private Renderer[] renderers;
        private MaterialPropertyBlock block;
        private Light proximityLight;
        private readonly Dictionary<Material, Material> glowMaterials = new Dictionary<Material, Material>();
        private bool glowing;
        private bool collected;
        public bool IsGlowing => glowing;
        public string PartId => partId;
        public override string DisplayName => displayName;

        public void Configure(string id, string name, C3TreasureProgress owner)
        {
            partId = id;
            displayName = name;
            progress = owner;
        }

        private void Awake()
        {
            if (!progress) progress = FindObjectOfType<C3TreasureProgress>();
            C3FirstPersonController controller = FindObjectOfType<C3FirstPersonController>();
            if (controller) player = controller.transform;
            renderers = GetComponentsInChildren<Renderer>(true);
            block = new MaterialPropertyBlock();
            foreach (Renderer target in renderers)
            {
                Material[] materials = target.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material original = materials[i];
                    if (!original || !original.HasProperty("_EmissionColor")) continue;
                    if (!glowMaterials.TryGetValue(original, out Material glowingMaterial))
                    {
                        glowingMaterial = new Material(original) { name = original.name + "_C3_Nearby" };
                        glowingMaterial.EnableKeyword("_EMISSION");
                        glowMaterials.Add(original, glowingMaterial);
                    }
                    materials[i] = glowingMaterial;
                }
                target.sharedMaterials = materials;
            }
            // A small, shadow-free light makes all three parts discoverable even on dark materials.
            GameObject beacon = new GameObject("NearbyPartGlow_DoNotCollect");
            beacon.transform.SetParent(transform, false);
            beacon.transform.localPosition = Vector3.up * 0.15f;
            proximityLight = beacon.AddComponent<Light>();
            proximityLight.type = LightType.Point;
            proximityLight.shadows = LightShadows.None;
            proximityLight.range = 1.6f;
            proximityLight.intensity = 0f;
            proximityLight.color = PartColor();
            proximityLight.enabled = false;
        }

        private Color PartColor()
        {
            if (partId == C3TreasureProgress.FuseId) return new Color(1f, 0.68f, 0.24f);
            if (partId == C3TreasureProgress.ChipId) return new Color(0.22f, 0.85f, 1f);
            return new Color(0.45f, 1f, 0.58f);
        }

        private void OnDestroy()
        {
            foreach (Material material in glowMaterials.Values) if (material) Destroy(material);
            glowMaterials.Clear();
        }

        private void Update()
        {
            if (!player)
            {
                C3FirstPersonController controller = FindObjectOfType<C3FirstPersonController>();
                if (controller) player = controller.transform;
            }
            float distance = player ? Vector3.Distance(player.position, transform.position) : float.PositiveInfinity;
            bool near = !collected && distance <= glowDistance;
            if (!near && !glowing) return;
            glowing = near;
            float fade = near ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((glowDistance - distance) / 1.5f)) : 0f;
            float strength = glowStrength * fade * (0.7f + 0.3f * Mathf.Sin(Time.time * pulseSpeed));
            Color hint = PartColor();
            if (proximityLight)
            {
                proximityLight.enabled = near;
                proximityLight.intensity = Mathf.Max(0f, strength * 0.75f);
            }
            foreach (Renderer target in renderers)
            {
                if (!target || !target.sharedMaterial) continue;
                target.GetPropertyBlock(block);
                Material material = target.sharedMaterial;
                if (material.HasProperty("_Color"))
                    block.SetColor("_Color", Color.Lerp(material.color, Color.white, fade * 0.22f));
                if (material.HasProperty("_EmissionColor"))
                    block.SetColor("_EmissionColor", material.GetColor("_EmissionColor") + hint * strength);
                target.SetPropertyBlock(block);
            }
        }

        public override bool CanInteract(GameObject actor) =>
            isActiveAndEnabled && !collected && progress != null && !progress.HasPart(partId);

        public override void Interact(GameObject actor)
        {
            if (!CanInteract(actor) || !progress.TryCollect(partId, displayName)) return;
            collected = true;
            // TryCollect owns both the once-only quest transition and inventory grant.
            gameObject.SetActive(false);
        }
    }
}
