using UnityEngine;
using SDGAdventure.SharedPlayer;

namespace C3TreasurePuzzle
{
    public sealed class C3PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField, Min(0.5f)] private float interactionDistance = 3f;
        [SerializeField, Min(0.5f)] private float partInteractionDistance = 2f;
        [SerializeField] private LayerMask raycastLayers = ~0;

        private C3Interactable currentTarget;
        private PlayerInputService sharedInput;
        private bool interactQueued;
        private readonly RaycastHit[] reachHits = new RaycastHit[32];
        public C3Interactable CurrentTarget => currentTarget;
        public bool HandlesInteractionInput => isActiveAndEnabled && playerCamera != null;
        public Vector3 ReachOrigin
        {
            get
            {
                CharacterController capsule = GetComponent<CharacterController>();
                return capsule ? transform.TransformPoint(capsule.center) : transform.position + Vector3.up;
            }
        }

        public void SetCamera(Camera value) => playerCamera = value;
        public void UseSharedInput(PlayerInputService service)
        {
            if (sharedInput != null) sharedInput.InteractPressed -= QueueInteract;
            sharedInput = service;
            if (isActiveAndEnabled && sharedInput != null) sharedInput.InteractPressed += QueueInteract;
        }

        private void OnEnable()
        {
            if (sharedInput != null) sharedInput.InteractPressed += QueueInteract;
        }

        private void OnDisable()
        {
            if (sharedInput != null) sharedInput.InteractPressed -= QueueInteract;
            interactQueued = false;
        }

        private void QueueInteract() => interactQueued = true;

        private void Awake()
        {
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
        }

        private void Update()
        {
            bool interact = sharedInput != null ? interactQueued : Input.GetKeyDown(KeyCode.F);
            interactQueued = false;
            currentTarget = null;
            if (playerCamera == null || Cursor.lockState != CursorLockMode.Locked ||
                (sharedInput != null && sharedInput.Context != PlayerInputContext.Gameplay) ||
                C3PuzzleConsole.BlocksGameplayInputThisFrame || C3CompletionScreen.BlocksGameplayInputThisFrame) return;

            // 只看第一個射線命中；牆或非互動物件會阻斷後方目標。
            currentTarget = FindTarget();

            // KeyDown 每次按鍵只觸發一次，不因停留或持續接觸重複執行。
            if (currentTarget != null && interact)
            {
                currentTarget.Interact(gameObject);
                // A collected target can disable itself in Interact; hide its prompt this frame.
                if (currentTarget != null && !currentTarget.CanInteract(gameObject)) currentTarget = null;
            }
            else if (interact && EcoAdventureRuntime.Instance != null)
            {
                // One input edge chooses ONE route. Never fall through to a
                // cache after the quest target has disabled itself this frame.
                EcoAdventureRuntime.Instance.InteractWorkshopFromPlayer(this);
            }
        }

        // Body reach and body-to-target sight are separate from the orbit
        // camera's selection ray. Only the player's and target owner's own
        // colliders are excluded; walls/equipment/floors still block reach.
        public bool CanReachPoint(Collider target, Vector3 point, float maximumDistance)
        {
            if (!target || !target.enabled || !target.gameObject.activeInHierarchy) return false;
            Vector3 origin = ReachOrigin, delta = point - origin;
            if (delta.sqrMagnitude > maximumDistance * maximumDistance) return false;
            if (delta.sqrMagnitude < .0001f) return true;
            C3Interactable quest = target.GetComponentInParent<C3Interactable>();
            EcoWorkshopInteractable station = target.GetComponentInParent<EcoWorkshopInteractable>();
            Transform owner = quest ? quest.transform : station ? station.transform : target.transform;
            int count = Physics.RaycastNonAlloc(origin, delta.normalized, reachHits,
                delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            if (count == reachHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                Collider blocker = reachHits[i].collider;
                if (blocker && !blocker.transform.IsChildOf(transform) && !blocker.transform.IsChildOf(owner)) return false;
            }
            return true;
        }

        public C3Interactable FindTarget()
        {
            if (playerCamera == null) return null;
            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            PlayerPerspectiveRig rig = GetComponent<PlayerPerspectiveRig>();
            bool third = rig != null && rig.IsThirdPerson;
            float maxRayDistance = third ? interactionDistance + 4.5f : interactionDistance;
            RaycastHit[] hits = Physics.RaycastAll(ray, maxRayDistance, raycastLayers,
                QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                C3Interactable candidate = hit.collider.GetComponentInParent<C3Interactable>();
                if (candidate == null) return null;
                float limit = candidate is C3CollectiblePart ? partInteractionDistance : interactionDistance;
                if (third)
                {
                    if (!CanReachPoint(hit.collider, hit.point, limit)) return null;
                }
                else if (candidate is C3CollectiblePart && hit.distance > partInteractionDistance) return null;
                return candidate.isActiveAndEnabled && candidate.CanInteract(gameObject) ? candidate : null;
            }
            return null;
        }


    }
}
