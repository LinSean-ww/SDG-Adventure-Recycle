using UnityEngine;
using C3TreasurePuzzle;

// F-interactable crafting station / scattered material cache.
// Caches are placed far apart; sorting must finish before the material can be taken.
public sealed class EcoWorkshopInteractable : MonoBehaviour
{
    public enum StationKind { Workbench, PlasticMaterial, MetalMaterial, CircuitMaterial, InspectionWorkbench }

    private EcoFactoryWorkshop workshop;
    private StationKind kind;
    private bool claimed;

    // Material caches expose their identity to the HUD only when the player is nearby.
    // A collected or disabled cache is never a hint candidate.
    public bool IsMaterialCache => kind == StationKind.PlasticMaterial ||
        kind == StationKind.MetalMaterial || kind == StationKind.CircuitMaterial;

    public bool IsUnclaimedMaterialCache => IsMaterialCache && !claimed &&
        isActiveAndEnabled && gameObject.activeInHierarchy;

    public string MaterialDisplayName => kind == StationKind.PlasticMaterial ? "再生塑膠" :
        kind == StationKind.MetalMaterial ? "再生金屬" :
        kind == StationKind.CircuitMaterial ? "廢電路板" : string.Empty;

    public static EcoWorkshopInteractable FindNearestMaterial(Vector3 origin, float radius)
    {
        EcoWorkshopInteractable closest = null;
        float bestDistanceSquared = radius * radius;
        foreach (EcoWorkshopInteractable cache in UnityEngine.Object.FindObjectsOfType<EcoWorkshopInteractable>())
        {
            if (cache == null || !cache.IsUnclaimedMaterialCache) continue;
            Vector3 delta = cache.transform.position - origin;
            // Don't show a prompt through a floor above/below the player.
            if (Mathf.Abs(delta.y) > 1.7f) continue;
            delta.y = 0f;
            float sqrDistance = delta.sqrMagnitude;
            if (sqrDistance > bestDistanceSquared) continue;
            bestDistanceSquared = sqrDistance;
            closest = cache;
        }
        return closest;
    }

    public string Prompt
    {
        get
        {
            if (claimed) return "已取回材料";
            if (kind == StationKind.InspectionWorkbench)
                return "[F] 查看材料進度與尋找線索";
            if (workshop == null || !workshop.Powered)
                return "未啟動：請先修復並啟動工廠";
            EcoAdventureRuntime game = EcoAdventureRuntime.Instance;
            if (kind == StationKind.PlasticMaterial)
                return game != null && game.HasSortedMaterial(0)
                    ? "F 拾取再生塑膠" : "等待塑膠完成自動分類";
            if (kind == StationKind.MetalMaterial)
                return game != null && game.HasSortedMaterial(1)
                    ? "F 拾取再生金屬" : "等待金屬完成自動分類";
            if (kind == StationKind.CircuitMaterial)
                return "F 拾取廢棄電路板";
            return game != null && game.Has(EcoAdventureRuntime.MetalId) &&
                game.Has(EcoAdventureRuntime.PlasticId) &&
                game.Has(EcoAdventureRuntime.CircuitId)
                ? "[F] 組裝量子武器（材料已齊）"
                : "[F] 檢查組裝所缺材料（探索全廠找三種材料）";
        }
    }

    public void Configure(EcoFactoryWorkshop owner, StationKind stationKind)
    {
        workshop = owner;
        kind = stationKind;
    }

    public void Interact()
    {
        if (claimed || EcoAdventureRuntime.Instance == null || workshop == null) return;
        EcoAdventureRuntime game = EcoAdventureRuntime.Instance;
        if (kind == StationKind.InspectionWorkbench)
        {
            workshop.InspectMaterials();
            return;
        }
        if (!workshop.Powered)
        {
            game.Notify("請先啟動工廠設備");
            return;
        }
        bool collected = false;
        switch (kind)
        {
            case StationKind.PlasticMaterial: collected = game.RecoverSortedMaterial(0); break;
            case StationKind.MetalMaterial: collected = game.RecoverSortedMaterial(1); break;
            case StationKind.CircuitMaterial: collected = game.RecoverCircuit(); break;
            case StationKind.Workbench: game.CraftWeapon(); break;
        }
        if (collected)
        {
            claimed = true;
            gameObject.SetActive(false);
        }
    }
}
