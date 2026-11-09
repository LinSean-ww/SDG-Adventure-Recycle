using UnityEngine;
using SDGAdventure.ModuleSystem;

namespace C3TreasurePuzzle
{
    // The player must physically reach the D doorway AFTER the factory enters Running.
    // The coordinator owns the transition when the next module is installed.
    [RequireComponent(typeof(Collider))]
    public sealed class C3ExitTrigger : MonoBehaviour
    {
        [SerializeField] private C3DeviceState device;
        [SerializeField] private C3FirstPersonController player;
        [SerializeField] private bool loadNextSceneWhenAvailable = true;
        private bool transferred;

        private void Awake()
        {
            if (device == null) device = FindObjectOfType<C3DeviceState>();
            if (player == null) player = FindObjectOfType<C3FirstPersonController>();
        }

        private void OnTriggerEnter(Collider other) => TryEnter(other);

        public bool TryEnter(Collider other)
        {
            if (device == null || player == null || other == null ||
                other != player.GetComponent<CharacterController>()) return false;
            if (transferred || !device.TryCompleteAtExit()) return false;
            // EcoAdventureRuntime is DontDestroyOnLoad: items/ammo survive when D is installed.
            transferred = true;
            // Existing scene has no Level04 yet. In that case the built-in completion UI
            // remains visible at the D exit instead of attempting to load a missing scene.
            var completion = FindFirstObjectByType<C3CompletionScreen>();
            if (loadNextSceneWhenAvailable && ModuleCoordinator.Instance != null &&
                (completion == null || !completion.Visible))
                ModuleCoordinator.Instance.TryLaunchNext(out _);
            return true;
        }
    }
}
