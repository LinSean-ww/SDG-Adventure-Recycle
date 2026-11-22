using UnityEngine;

namespace SDGAdventure.SharedPlayer
{
    /// <summary>Stores exact pre-conversion component states for one-click restoration.</summary>
    [DisallowMultipleComponent]
    public sealed class SDGTargetedGateRecordV3 : MonoBehaviour
    {
        [SerializeField] private Renderer[] renderers;
        [SerializeField] private bool[] rendererStates;
        [SerializeField] private Collider[] colliders;
        [SerializeField] private bool[] colliderStates;
        [SerializeField] private GameObject generatedHost;
        [SerializeField] private string gateId;

        public Renderer[] Renderers => renderers;
        public bool[] RendererStates => rendererStates;
        public Collider[] Colliders => colliders;
        public bool[] ColliderStates => colliderStates;
        public GameObject GeneratedHost => generatedHost;
        public string GateId => gateId;

        public void Capture(string id, Renderer[] rs, Collider[] cs, GameObject host)
        {
            gateId = id;
            renderers = rs;
            colliders = cs;
            generatedHost = host;
            rendererStates = new bool[rs.Length];
            colliderStates = new bool[cs.Length];
            for (int i = 0; i < rs.Length; i++) rendererStates[i] = rs[i] != null && rs[i].enabled;
            for (int i = 0; i < cs.Length; i++) colliderStates[i] = cs[i] != null && cs[i].enabled;
        }
    }
}
