using UnityEngine;

namespace C3TreasurePuzzle
{
    public sealed class C3DeviceVisual : MonoBehaviour
    {
        [SerializeField] private C3DeviceState state;
        [SerializeField] private Renderer display;
        [SerializeField] private Light statusLight;
        private MaterialPropertyBlock block;
        private static readonly Color Idle = new Color(0.04f, 0.65f, 0.72f);
        private static readonly Color Running = new Color(0.22f, 1f, 0.38f);

        public void Configure(C3DeviceState owner, Renderer target, Light lamp)
        {
            state = owner;
            display = target;
            statusLight = lamp;
        }

        private void Awake()
        {
            if (state == null) state = FindObjectOfType<C3DeviceState>();
            block = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            if (state != null) state.Activated += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (state != null) state.Activated -= Refresh;
        }

        public void Refresh()
        {
            bool running = state != null && state.IsActivated;
            Color color = running ? Running : Idle;
            if (display != null)
            {
                display.GetPropertyBlock(block);
                block.SetColor("_Color", color);
                block.SetColor("_EmissionColor", color * (running ? 1.8f : 0.55f));
                display.SetPropertyBlock(block);
            }
            if (statusLight != null)
            {
                statusLight.color = color;
                statusLight.intensity = running ? 3f : 0.45f;
            }
        }
    }
}
