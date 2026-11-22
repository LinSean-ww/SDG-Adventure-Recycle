using UnityEngine;

namespace C3TreasurePuzzle
{
    /// <summary>
    /// Physical, vertical sliding industrial gate. Mission authority is the
    /// existing C3TreasureProgress / C3DeviceState. No debug key or proximity
    /// can open a locked gate. One gate per verified physical opening.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class C3MissionLockedVerticalShutter : MonoBehaviour
    {
        public enum UnlockMilestone
        {
            AllThreePartsCollected = 0,
            FactoryRunning = 1
        }

        [SerializeField] private UnlockMilestone requirement;
        [SerializeField] private C3TreasureProgress treasureProgress;
        [SerializeField] private C3DeviceState deviceState;
        [SerializeField] private Transform movingPanel;
        [SerializeField] private Vector3 closedLocalPosition;
        [SerializeField, Min(2f)] private float liftHeight = 3.12f;
        [SerializeField, Min(0.1f)] private float riseSpeed = 2.25f;
        [SerializeField, Min(1f)] private float detectionRadius = 3.5f;

        private CharacterController player;
        private float retryAt;
        private float panelLift;
        private bool requestedOpen;
        private bool permanentlyUnlocked;

        public bool IsUnlocked { get { return permanentlyUnlocked || RequirementSatisfied(); } }
        public bool IsOpen { get { return panelLift >= liftHeight - 0.04f; } }
        public UnlockMilestone Requirement { get { return requirement; } }

        public void Configure(UnlockMilestone milestone, C3TreasureProgress progress,
            C3DeviceState device, Transform panel, float height, float speed, float radius)
        {
            requirement = milestone;
            treasureProgress = progress;
            deviceState = device;
            movingPanel = panel;
            closedLocalPosition = panel != null ? panel.localPosition : Vector3.zero;
            liftHeight = Mathf.Max(2f, height);
            riseSpeed = Mathf.Max(0.1f, speed);
            detectionRadius = Mathf.Max(1f, radius);
        }

        private void Awake()
        {
            // Fail closed when the scene's mission authority is unavailable.
            if (treasureProgress == null) treasureProgress = FindObjectOfType<C3TreasureProgress>();
            if (deviceState == null) deviceState = FindObjectOfType<C3DeviceState>();
            panelLift = 0f;
            SetPhysicalPosition();
        }

        private bool RequirementSatisfied()
        {
            if (requirement == UnlockMilestone.AllThreePartsCollected)
                return treasureProgress != null && treasureProgress.AllPartsCollected;
            if (deviceState == null) return false;
            if (requirement == UnlockMilestone.FactoryRunning &&
                !C3TreasurePuzzle.C3StoryV2Hooks.ExitUnlocked) return false;
            return deviceState.FactoryState == C3FactoryState.Running ||
                   deviceState.FactoryState == C3FactoryState.Completed;
        }

        // Existing door events may call these; they NEVER override the mission lock.
        public void OpenDoor() { requestedOpen = true; }
        public void CloseDoor() { requestedOpen = false; }

        private void Update()
        {
            if (movingPanel == null) return;
            if (RequirementSatisfied()) permanentlyUnlocked = true;
            if (Time.unscaledTime >= retryAt)
            {
                retryAt = Time.unscaledTime + 0.6f;
                if (player == null || !player.enabled || !player.gameObject.activeInHierarchy)
                    FindPlayer();
            }
            bool shouldOpen = permanentlyUnlocked && (requestedOpen || IsPlayerNear());
            float target = shouldOpen ? liftHeight : 0f;
            float updated = Mathf.MoveTowards(panelLift, target, riseSpeed * Time.deltaTime);
            if (Mathf.Abs(updated - panelLift) < 0.00001f) return;
            panelLift = updated;
            SetPhysicalPosition();
        }

        private bool IsPlayerNear()
        {
            if (player == null || !player.enabled || !player.gameObject.activeInHierarchy) return false;
            Vector3 delta = player.transform.position - transform.position;
            return Mathf.Abs(delta.y) <= 3f &&
                   delta.x * delta.x + delta.z * delta.z <= detectionRadius * detectionRadius;
        }

        private void FindPlayer()
        {
            // Only a real active gameplay CharacterController can open the door.
            player = null;
            foreach (CharacterController candidate in FindObjectsOfType<CharacterController>())
            {
                if (candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy) continue;
                bool gameplayPlayer = candidate.GetComponent<C3FirstPersonController>() != null;
                if (!gameplayPlayer)
                {
                    foreach (MonoBehaviour component in candidate.GetComponents<MonoBehaviour>())
                    {
                        if (component != null && component.isActiveAndEnabled &&
                            component.GetType().Name == "SharedFirstPersonController")
                        {
                            gameplayPlayer = true;
                            break;
                        }
                    }
                }
                if (gameplayPlayer) { player = candidate; return; }
            }
        }

        private void SetPhysicalPosition()
        {
            if (movingPanel != null)
                movingPanel.localPosition = closedLocalPosition + Vector3.up * panelLift;
        }
    }
}
