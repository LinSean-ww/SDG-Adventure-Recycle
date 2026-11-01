using UnityEngine;

namespace C3TreasurePuzzle
{
    // A bounded, line-of-sight cue only. Does not implement any pickup or reward.
    // Later additions are registered explicitly; execution order is not a readiness signal.
    [DefaultExecutionOrder(250)]
    [DisallowMultipleComponent]
    public sealed class C3ProximityHint : MonoBehaviour
    {
        private Transform player;
        private C3HintVisuals visuals;
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private Light existingLight;
        private bool originalEnabled, lastEnabled, ownsEnabled, ownsIntensity, lightWritten;
        private float originalIntensity, lastIntensity;
        private Color color = Color.green;

        public void Configure(Color value, Light light = null)
        {
            RestoreLight();
            color = value;
            existingLight = light;
            lightWritten = false;
            if (!light) return;
            // Capture BEFORE the first change, including when Configure precedes Start.
            originalEnabled = light.enabled;
            originalIntensity = light.intensity;
            ownsEnabled = ownsIntensity = true;
            if (isActiveAndEnabled) WriteLight(false, 0f);
        }

        private void Start() => visuals = new C3HintVisuals(transform);
        private void OnEnable() => visuals?.SetCue(Color.black, 0f);

        public void RegisterVisuals(Transform root)
        {
            if (!root || !root.IsChildOf(transform)) return;
            // Before Start, leave initialization to the normal whole-owner scan.
            // After ReleaseVisuals, do not recreate a disposed hint.
            visuals?.RegisterRenderers(root);
        }

        internal static bool VisibleFrom(Transform player, Transform target, Vector3 point, RaycastHit[] buffer)
        {
            if (!player || !target) return false;
            Vector3 eye=player.position+Vector3.up*1.4f,delta=point-eye;
            if(delta.sqrMagnitude<.0001f)return true;
            int n=Physics.RaycastNonAlloc(eye,delta.normalized,buffer,delta.magnitude,~0,QueryTriggerInteraction.Ignore);
            if(n==buffer.Length)return false; // conservative when the bounded buffer saturates
            for(int i=0;i<n;i++)
                if(buffer[i].collider && !buffer[i].transform.IsChildOf(player) && !buffer[i].transform.IsChildOf(target))return false;
            return true;
        }

        private void Update()
        {
            if(visuals==null)return;
            if(!player){var c=FindObjectOfType<C3FirstPersonController>();if(c)player=c.transform;}
            float distance=player?Vector3.Distance(player.position,transform.position):float.PositiveInfinity;
            float fade=distance<3.6f && VisibleFrom(player,transform,transform.position+Vector3.up*.25f,hits)
                ?Mathf.SmoothStep(0,1,Mathf.Clamp01((3.6f-distance)/1.4f)):0;
            float strength=fade*(.60f+.20f*Mathf.Sin(Time.time*2f));
            visuals.SetCue(color, strength);
            WriteLight(fade > 0f, strength * .45f);
        }

        private void ObserveLightWriters()
        {
            if (!existingLight) return;
            // Relinquish a field when another system changes it. Do not overwrite
            // that change in Update, OnDisable, or OnDestroy.
            if (existingLight.enabled != (lightWritten ? lastEnabled : originalEnabled)) ownsEnabled = false;
            if (existingLight.intensity != (lightWritten ? lastIntensity : originalIntensity)) ownsIntensity = false;
        }

        private void WriteLight(bool enabledValue, float intensity)
        {
            if (!existingLight) return;
            ObserveLightWriters();
            if (ownsEnabled) existingLight.enabled = lastEnabled = enabledValue;
            if (ownsIntensity) existingLight.intensity = lastIntensity = intensity;
            lightWritten = true;
        }

        private void RestoreLight()
        {
            if (!existingLight) return;
            ObserveLightWriters();
            if (ownsEnabled) existingLight.enabled = originalEnabled;
            if (ownsIntensity) existingLight.intensity = originalIntensity;
            lightWritten = false;
        }

        private void OnDisable()
        {
            visuals?.SetCue(Color.black, 0f);
            RestoreLight();
        }

        // Owners can release before scheduling their visual root/materials for Destroy.
        internal void ReleaseVisuals()
        {
            visuals?.Dispose();
            visuals = null;
            RestoreLight();
        }

        private void OnDestroy() => ReleaseVisuals();
    }
}
