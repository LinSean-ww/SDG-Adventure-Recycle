using UnityEngine;

namespace SDGAdventure.SharedPlayer
{
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private float interactionDistance = 3f;
        [SerializeField] private LayerMask raycastLayers = ~0;

        private PlayerInputService input;
        private PlayerHudCanvas hud;
        private IPlayerInteractable target;

        public IPlayerInteractable CurrentTarget => target;
        public void SetCamera(Camera value) => playerCamera = value;

        private void Awake()
        {
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
            hud = GetComponent<PlayerHudCanvas>();
            if (hud == null) hud = gameObject.AddComponent<PlayerHudCanvas>();
            hud.Build();
        }

        private void OnEnable()
        {
            input = PlayerInputService.Instance;
            if (input != null) input.InteractPressed += OnInteractPressed;
        }

        private void OnDisable()
        {
            if (input != null) input.InteractPressed -= OnInteractPressed;
            target = null;
        }

        private void Update()
        {
            RefreshTarget();
            string prompt = target == null ? "" : target.Prompt + "  [" +
                input.Keybindings.BindingDisplay("Gameplay", "Interact", 0) + "]";
            hud.SetPrompt(prompt);
        }

        private void RefreshTarget()
        {
            target = null;
            if (input == null || input.Context != PlayerInputContext.Gameplay || playerCamera == null ||
                Cursor.lockState != CursorLockMode.Locked) return;
            target = FindTarget();
        }

        public IPlayerInteractable FindTarget()
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
                MonoBehaviour[] components = hit.collider.GetComponentsInParent<MonoBehaviour>();
                foreach (MonoBehaviour component in components)
                    if (component is IPlayerInteractable candidate && component.isActiveAndEnabled &&
                        candidate.CanInteract(gameObject)) return candidate;
                return null; // a wall or non-interactable blocks the target
            }
            return null;
        }

        private void OnInteractPressed() => TryInteract();

        public bool TryInteract()
        {
            RefreshTarget();
            if (target == null) return false;
            target?.Interact(gameObject);
            RefreshTarget();
            return true;
        }

    }
}
