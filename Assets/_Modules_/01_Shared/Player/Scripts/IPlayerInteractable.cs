using UnityEngine;

namespace SDGAdventure.SharedPlayer
{
    public interface IPlayerInteractable
    {
        string Prompt { get; }
        bool CanInteract(GameObject actor);
        void Interact(GameObject actor);
    }
}
