using UnityEngine;

namespace C3TreasurePuzzle
{
    // M3、M4 可繼承此元件，覆寫名稱、可互動條件及互動行為。
    public abstract class C3Interactable : MonoBehaviour
    {
        public abstract string DisplayName { get; }
        public virtual bool CanInteract(GameObject actor) => true;
        public abstract void Interact(GameObject actor);
    }
}
