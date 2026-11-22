using UnityEngine;

namespace SDGAdventure.SharedPlayer
{
    /// <summary>
    /// Vertical gate attached only to a deliberately selected entrance door leaf.
    /// No name based object discovery and no scene-wide modifications.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SDGTargetedLiftGateV3 : MonoBehaviour
    {
        [SerializeField] private Transform movingPanel;
        [SerializeField, Min(0.25f)] private float liftHeight = 3f;
        [SerializeField, Min(0.1f)] private float speedMetresPerSecond = 2.8f;
        [SerializeField] private bool proximityOpen = true;
        [SerializeField, Min(0.5f)] private float proximityDistance = 2.75f;
        [SerializeField] private bool startsOpen;
        [SerializeField] private Renderer[] sourceRenderers;
        [SerializeField] private Collider[] sourceSolidColliders;

        private Vector3 closedPanelPosition;
        private bool openRequested;
        private float fraction;
        private Transform player;
        private float nextLookup;

        public bool IsOpen => openRequested;

        public void Configure(Transform panel, float height, bool nearPlayer, Renderer[] originalRenderers,
            Collider[] originalColliders)
        {
            movingPanel = panel;
            liftHeight = Mathf.Max(0.25f, height);
            proximityOpen = nearPlayer;
            sourceRenderers = originalRenderers;
            sourceSolidColliders = originalColliders;
            if (movingPanel != null) closedPanelPosition = movingPanel.localPosition;
        }

        private void Awake()
        {
            if (movingPanel != null) closedPanelPosition = movingPanel.localPosition;
            HideSourceGeometry();
            SetInstantOpen(startsOpen);
        }

        private void OnEnable() => HideSourceGeometry();

        public void OpenDoor() => openRequested = true;
        public void CloseDoor() => openRequested = false;
        public void ToggleDoor() => openRequested = !openRequested;
        public void SetInstantOpen(bool value)
        {
            openRequested = value;
            fraction = value ? 1f : 0f;
            PlacePanel();
        }

        private void Update()
        {
            if (proximityOpen)
            {
                if (player == null && Time.unscaledTime >= nextLookup)
                {
                    nextLookup = Time.unscaledTime + 1f;
                    SharedFirstPersonController fps = FindObjectOfType<SharedFirstPersonController>();
                    if (fps != null) player = fps.transform;
                }
                if (player != null && movingPanel != null)
                {
                    Vector3 p = player.position;
                    Vector3 door = movingPanel.parent.TransformPoint(closedPanelPosition);
                    p.y = door.y;
                    openRequested = Vector3.Distance(p, door) <= proximityDistance;
                }
            }
            float desired = openRequested ? 1f : 0f;
            if (!Mathf.Approximately(fraction, desired))
            {
                fraction = Mathf.MoveTowards(fraction, desired,
                    Time.deltaTime * speedMetresPerSecond / Mathf.Max(liftHeight, 0.25f));
                PlacePanel();
            }
        }

        private void PlacePanel()
        {
            if (movingPanel != null)
                movingPanel.localPosition = closedPanelPosition + Vector3.up * (liftHeight * fraction);
        }

        private void HideSourceGeometry()
        {
            if (sourceRenderers != null)
                foreach (Renderer r in sourceRenderers)
                    if (r != null) r.enabled = false;
            if (sourceSolidColliders != null)
                foreach (Collider c in sourceSolidColliders)
                    if (c != null && !c.isTrigger) c.enabled = false;
        }
    }
}
