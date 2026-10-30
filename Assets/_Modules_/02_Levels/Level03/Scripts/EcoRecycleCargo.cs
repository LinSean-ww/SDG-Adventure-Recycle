
using UnityEngine;

public class EcoRecycleCargo : MonoBehaviour
{
    public enum Kind { Metal, Plastic, Electronics }
    public Kind materialType;
    [HideInInspector] public bool processed;
}
