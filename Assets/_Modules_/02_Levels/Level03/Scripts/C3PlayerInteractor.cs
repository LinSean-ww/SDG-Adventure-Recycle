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
        public C3Interactable CurrentTarget => currentTarget;

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
                if (third && Vector3.Distance(transform.position + Vector3.up, hit.point) >
                    interactionDistance) return null;
                C3Interactable candidate = hit.collider.GetComponentInParent<C3Interactable>();
                return candidate != null &&
                    (!(candidate is C3CollectiblePart) || hit.distance <= partInteractionDistance) &&
                    candidate.isActiveAndEnabled && candidate.CanInteract(gameObject) ? candidate : null;
            }
            return null;
        }


    }
}
