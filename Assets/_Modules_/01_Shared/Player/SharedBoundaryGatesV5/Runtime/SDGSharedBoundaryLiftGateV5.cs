using UnityEngine;

namespace SDGAdventure.SharedBoundaryGates
{
    // Exactly one moving shutter per physical connection between two areas.
    [DisallowMultipleComponent]
    public sealed class SDGSharedBoundaryLiftGateV5 : MonoBehaviour
    {
        [SerializeField] private string boundaryId;
        [SerializeField] private Transform movingPanel;
        [SerializeField, Min(0.1f)] private float travelLocal = 3f;
        [SerializeField, Min(0.1f)] private float speedLocal = 2.6f;
        [SerializeField] private bool proximityAutoOpen = false;
        [SerializeField, Min(0.5f)] private float proximityRadius = 2.8f;
        [SerializeField] private bool initiallyOpen = false;
        [SerializeField] private Renderer[] originalRenderers;
        [SerializeField] private Collider[] originalColliders;

        private Vector3 closedLocalPosition;
        private float openFraction;
        private bool wantedOpen;
        private Transform player;
        private float nextPlayerCheck;

        public string BoundaryId { get { return boundaryId; } }
        public bool IsOpen { get { return wantedOpen; } }

        public void Configure(string id, Transform panel, float distance, bool autoOpen,
            Renderer[] renderers, Collider[] colliders)
        {
            boundaryId = id;
            movingPanel = panel;
            travelLocal = Mathf.Max(0.1f, distance);
            proximityAutoOpen = autoOpen;
            originalRenderers = renderers;
            originalColliders = colliders;
            if (panel != null) closedLocalPosition = panel.localPosition;
        }

        private void Awake()
        {
            if (movingPanel != null) closedLocalPosition = movingPanel.localPosition;
            HideOriginalDoor();
            SetInstantOpen(initiallyOpen);
        }

        private void OnEnable() { HideOriginalDoor(); }

        public void OpenDoor() { wantedOpen = true; }
        public void CloseDoor() { wantedOpen = false; }
        public void ToggleDoor() { wantedOpen = !wantedOpen; }

        public void SetInstantOpen(bool open)
        {
            wantedOpen = open;
            openFraction = open ? 1f : 0f;
            ApplyPosition();
        }

        private void Update()
        {
            if (proximityAutoOpen)
            {
                FindPlayerIfNeeded();
                if (player != null)
                {
                    Vector3 a = player.position;
                    Vector3 b = movingPanel != null ? movingPanel.parent.TransformPoint(closedLocalPosition) : transform.position;
                    a.y = b.y;
                    wantedOpen = Vector3.Distance(a, b) <= proximityRadius;
                }
            }
            float goal = wantedOpen ? 1f : 0f;
            openFraction = Mathf.MoveTowards(openFraction, goal,
                Time.deltaTime * speedLocal / Mathf.Max(travelLocal, 0.1f));
            ApplyPosition();
        }

        private void ApplyPosition()
        {
            if (movingPanel != null)
                movingPanel.localPosition = closedLocalPosition + Vector3.up * (travelLocal * openFraction);
        }

        private void HideOriginalDoor()
        {
            if (originalRenderers != null)
                foreach (Renderer r in originalRenderers) if (r != null) r.enabled = false;
            if (originalColliders != null)
                foreach (Collider c in originalColliders) if (c != null && !c.isTrigger) c.enabled = false;
        }

        private void FindPlayerIfNeeded()
        {
            if (player != null || Time.unscaledTime < nextPlayerCheck) return;
            nextPlayerCheck = Time.unscaledTime + 1.0f;
            MonoBehaviour[] scripts = FindObjectsOfType<MonoBehaviour>();
            foreach (MonoBehaviour script in scripts)
            {
                if (script != null && script.GetType().Name == "SharedFirstPersonController")
                {
                    player = script.transform;
                    return;
                }
            }
            if (Camera.main != null) player = Camera.main.transform;
        }
    }
}
