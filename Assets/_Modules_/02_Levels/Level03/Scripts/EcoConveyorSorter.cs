using System.Linq;
using UnityEngine;
using C3TreasurePuzzle;

// In C_Main this trigger only transfers an upstream object into the line. It
// cannot award an independent sorting credit or destroy a line-owned item.
public class EcoConveyorSorter : MonoBehaviour
{
    private void OnTriggerEnter(Collider other) => TrySort(other.GetComponentInParent<EcoRecycleCargo>());
    public bool TrySort(EcoRecycleCargo cargo)
    {
        if (!cargo || cargo.processed || cargo.lineOwned) return false;
        C3RecyclingLine line=Object.FindObjectsByType<C3RecyclingLine>(FindObjectsSortMode.None)
            .FirstOrDefault(l=>l.gameObject.scene==gameObject.scene);
        if (line) return line.TryAcceptCargo(cargo);
        // Compatibility for levels without the authoritative C3 line.
        EcoAdventureRuntime game=EcoAdventureRuntime.Instance;
        int kind=C3RecyclingLine.LegacyKind(cargo.materialType);
        if (!game || !game.factoryPowered || kind<0) return false;
        game.ReceiveSortedMaterial(kind);
        cargo.processed=true;
        Destroy(cargo.gameObject);
        return true;
    }
}
