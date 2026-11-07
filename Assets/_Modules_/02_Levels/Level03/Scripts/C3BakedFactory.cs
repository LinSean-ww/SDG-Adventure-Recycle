using UnityEngine;

namespace C3TreasurePuzzle
{
    // Authored layout provenance, never a saved gameplay session.
    [DisallowMultipleComponent]
    public sealed class C3BakedFactory : MonoBehaviour
    {
        public int version = 13;
        public GameObject[] generatedObjects;
        public C3ReferenceFactoryEnvironment environment;
        public C3RecyclingLineGeometry geometry;
        public C3V3Circulation circulation;
        public EcoFactoryWorkshop workshop;
        public C3WorkstationVisualUpgrade stations;
    }
}
