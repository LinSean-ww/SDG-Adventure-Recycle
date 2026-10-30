using UnityEngine;

// Optional trigger-based sorter. The existing C3 paper/metal/plastic sorting
// animation is owned by C3FactoryPresentation and remains unchanged.
public class EcoConveyorSorter : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        EcoAdventureRuntime game = EcoAdventureRuntime.Instance;
        if (game == null || !game.factoryPowered) return;

        EcoRecycleCargo cargo = other.GetComponentInParent<EcoRecycleCargo>();
        if (cargo == null || cargo.processed) return;

        // EcoAdventureRuntime.ReceiveSortedMaterial uses 0=plastic, 1=metal,
        // 2=paper; EcoRecycleCargo.Kind instead uses Metal,Plastic,Electronics.
        // Map by enum names to avoid incorrectly treating metal as plastic.
        switch (cargo.materialType)
        {
            case EcoRecycleCargo.Kind.Metal:
                game.ReceiveSortedMaterial(1);
                break;
            case EcoRecycleCargo.Kind.Plastic:
                game.ReceiveSortedMaterial(0);
                break;
            case EcoRecycleCargo.Kind.Electronics:
                // Electronic scrap is now recovered from the separate factory cache.
                // Sorting it must not auto-grant the crafting item.
                break;
            default:
                return;
        }

        cargo.processed = true;
        Destroy(cargo.gameObject);
    }
}
