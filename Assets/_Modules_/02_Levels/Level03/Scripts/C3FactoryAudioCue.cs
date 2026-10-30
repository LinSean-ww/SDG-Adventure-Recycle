using UnityEngine;

namespace C3TreasurePuzzle
{
    // An additive presentation hook. The device state remains the sole gameplay authority.
    [RequireComponent(typeof(AudioSource))]
    public sealed class C3FactoryAudioCue : MonoBehaviour
    {
        [SerializeField] private C3DeviceState device;
        private AudioSource source;

        public void Configure(C3DeviceState owner) => device = owner;

        private void OnEnable()
        {
            source = GetComponent<AudioSource>();
            if (device == null) device = FindObjectOfType<C3DeviceState>();
            if (device != null) device.Activated += PlayActivation;
        }

        private void OnDisable()
        {
            if (device != null) device.Activated -= PlayActivation;
        }

        private void PlayActivation()
        {
            if (source != null && source.clip != null) source.Play();
        }
    }
}
