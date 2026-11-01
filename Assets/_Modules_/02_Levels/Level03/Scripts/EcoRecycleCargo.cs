
using UnityEngine;

public class EcoRecycleCargo : MonoBehaviour
{
    // Existing serialized values MUST remain 0/1/2. Paper is appended.
    public enum Kind { Metal=0, Plastic=1, Electronics=2, Paper=3 }
    public Kind materialType;
    public string materialId;
    public int materialUnits=1;
    [HideInInspector] public bool processed;
    [HideInInspector] public bool lineOwned;
}
