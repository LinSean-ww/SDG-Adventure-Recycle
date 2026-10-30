using UnityEngine;

// Optional pickup component. C3's existing quest items are handled separately
// by C3TreasureProgress to preserve main-quest completion logic.
public class EcoLootPickup : MonoBehaviour
{
    public string itemId = "Fuse";
    public string itemName = "保險絲";
    public int quantity = 1;
    public bool autoHotbar;

    public void Collect()
    {
        EcoAdventureRuntime game = EcoAdventureRuntime.Instance;
        if (game == null) return;

        game.AddItem(itemId, itemName, quantity);
        if (autoHotbar)
            game.EquipFirstAvailable(itemId);

        Destroy(gameObject);
    }
}
