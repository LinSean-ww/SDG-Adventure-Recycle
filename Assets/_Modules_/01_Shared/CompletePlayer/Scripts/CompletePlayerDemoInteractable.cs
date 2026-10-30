using SDGAdventure.SharedPlayer;
using UnityEngine;

namespace SDGAdventure.CompletePlayer
{
    [RequireComponent(typeof(Collider))]
    public sealed class CompletePlayerDemoInteractable : MonoBehaviour, IPlayerInteractable
    {
        public string Prompt => "啟動測試裝置";
        public int InteractionCount { get; private set; }
        public bool CanInteract(GameObject actor) => isActiveAndEnabled && actor;
        public void Interact(GameObject actor)
        {
            if (!CanInteract(actor)) return;
            InteractionCount++;
            Renderer renderer = GetComponent<Renderer>();
            if (renderer) renderer.material.color = InteractionCount % 2 == 0
                ? new Color(0.1f, 0.8f, 0.9f) : new Color(0.2f, 1f, 0.4f);
        }
    }

}
