using SDGAdventure.SharedPlayer;
using UnityEngine;

namespace SDGAdventure.PlayerTest
{
    [RequireComponent(typeof(Collider))]
    public sealed class PlayerControlTestInteractable : MonoBehaviour, IPlayerInteractable
    {
        [SerializeField] private string prompt = "啟動測試裝置";
        private Renderer visual;
        public string Prompt => prompt;
        public int InteractionCount { get; private set; }
        public string LastFeedback { get; private set; } = "尚未互動";

        private void Awake() => visual = GetComponent<Renderer>();
        public bool CanInteract(GameObject actor) => isActiveAndEnabled && actor != null;

        public void Interact(GameObject actor)
        {
            if (!CanInteract(actor)) return;
            InteractionCount++;
            LastFeedback = "互動成功：" + InteractionCount + " 次";
            if (visual != null)
                visual.material.color = InteractionCount % 2 == 0
                    ? new Color(0.1f, 0.8f, 0.9f) : new Color(0.25f, 1f, 0.35f);
        }
    }
}
