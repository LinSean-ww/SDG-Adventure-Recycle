using UnityEngine;

namespace C3TreasurePuzzle
{
    // Environmental salvage charges the equipped weapon magazine on proximity.
    // This does not alter C3's original paper/metal/plastic conveyor simulation.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class EcoEnergyPickup : C3Interactable
    {
        public enum SalvageKind { PlasticBottle, MetalCan, WastePaper }

        [SerializeField] private SalvageKind salvageKind;
        [SerializeField, Min(0.6f)] private float automaticPickupRadius = 1.8f;
        [SerializeField, Min(0.6f)] private float maximumHeightDifference = 1.7f;
        private Transform playerRoot;
        private bool collected;
        public bool Collected => collected;
        public SalvageKind Kind => salvageKind;

        public int EnergyValue => salvageKind == SalvageKind.PlasticBottle ? 5 :
            salvageKind == SalvageKind.MetalCan ? 8 : 3;

        private string SalvageName => salvageKind == SalvageKind.PlasticBottle ? "塑膠瓶" :
            salvageKind == SalvageKind.MetalCan ? "金屬罐" : "廢紙";

        public override string DisplayName => SalvageName + "（手持量子武器時靠近補能 +" + EnergyValue + "）";

        public void Configure(SalvageKind kind) => salvageKind = kind;

        public override bool CanInteract(GameObject actor) =>
            isActiveAndEnabled && !collected && EcoAdventureRuntime.Instance != null &&
            EcoAdventureRuntime.Instance.IsQuantumWeaponEquipped;

        public override void Interact(GameObject actor) => TryCollect();

        private void Start() => FindPlayerRoot();

        private void FindPlayerRoot()
        {
            // The player root, not the camera, is used: third-person orbit distance
            // must never trigger remote pickups or prevent pickups near the feet.
            C3FirstPersonController c3 = FindObjectOfType<C3FirstPersonController>();
            if (c3 != null)
            {
                playerRoot = c3.transform;
                return;
            }
            SDGAdventure.SharedPlayer.SharedFirstPersonController shared =
                FindObjectOfType<SDGAdventure.SharedPlayer.SharedFirstPersonController>();
            if (shared != null) playerRoot = shared.transform;
        }

        private void Update()
        {
            if (collected || !isActiveAndEnabled) return;
            EcoAdventureRuntime game = EcoAdventureRuntime.Instance;
            if (game == null || !game.CanAutoRecharge) return;
            if (playerRoot == null) FindPlayerRoot();
            if (playerRoot == null) return;

            Vector3 playerPos = playerRoot.position;
            Vector3 itemPos = transform.position;
            if (Mathf.Abs(playerPos.y - itemPos.y) > maximumHeightDifference) return;
            Vector2 delta = new Vector2(playerPos.x - itemPos.x, playerPos.z - itemPos.z);
            if (delta.sqrMagnitude > automaticPickupRadius * automaticPickupRadius) return;

            // Charging is magazine-only: if loaded energy is 24/24, leave the
            // pickup in the world and leave reserve energy unchanged.
            TryCollect();
        }

        // Returns false at a full magazine; keeps the pickup for later use.
        public bool TryCollect()
        {
            if (!CanInteract(null)) return false;
            int actuallyAdded = EcoAdventureRuntime.Instance.AddEnergyFromSalvage(
                SalvageName, EnergyValue);
            if (actuallyAdded <= 0) return false;
            collected = true;
            gameObject.SetActive(false); // Prevent repeated collection this scene.
            return true;
        }
    }
}
