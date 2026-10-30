using System.Collections;
using UnityEngine;

namespace C3TreasurePuzzle
{
    // Startup lighting/audio only. Phase 2's line is the material/cycle authority.
    public sealed class C3FactoryPresentation : MonoBehaviour
    {
        [SerializeField] private C3DeviceState device;
        [SerializeField] private Transform sortingLine;
        [SerializeField, Min(.1f)] private float lightRestoreSeconds=2f;
        [SerializeField, Min(.1f)] private float motorStartSeconds=1f;
        [SerializeField] private Light[] factoryLights;
        private float[] lightLevels;
        private Light faultLight;
        private float faultIntensity;
        private Coroutine startup;
        private AudioSource motorAudio;
        private bool motorActive;
        public bool MotorActive => motorActive;
        public int CompletedCycles { get; private set; }
        public int CurrentWasteKind { get; private set; }

        private void Awake()
        {
            if (!device) device=GetComponent<C3DeviceState>();
            if (!sortingLine) { GameObject old=GameObject.Find("WasteSortingLine"); if (old) sortingLine=old.transform; }
            if (factoryLights==null || factoryLights.Length==0)
            {
                string[] names={"Warm_EntrySafety","Warm_WestService","Warm_CentralReactor","Warm_EastService","Warm_UpperWalkway"};
                factoryLights=new Light[names.Length];
                for (int i=0;i<names.Length;i++) { GameObject g=GameObject.Find(names[i]); if (g) factoryLights[i]=g.GetComponent<Light>(); }
            }
            lightLevels=new float[factoryLights.Length];
            for (int i=0;i<factoryLights.Length;i++) if (factoryLights[i])
            { lightLevels[i]=factoryLights[i].intensity; factoryLights[i].intensity*=.45f; }
            GameObject fault=GameObject.Find("Red_EntryFault");
            if (fault) faultLight=fault.GetComponent<Light>();
            if (faultLight) faultIntensity=faultLight.intensity;
            GameObject ambient=GameObject.Find("DormantFactoryAmbientHum");
            AudioSource source=ambient?ambient.GetComponent<AudioSource>():null;
            if (source && source.clip)
            {
                motorAudio=gameObject.AddComponent<AudioSource>(); motorAudio.clip=source.clip;
                motorAudio.loop=true; motorAudio.playOnAwake=false; motorAudio.spatialBlend=.8f;
                motorAudio.volume=.14f; motorAudio.pitch=1.35f; motorAudio.maxDistance=15f;
            }
            if (!GetComponent<EcoFactoryWorkshop>()) gameObject.AddComponent<EcoFactoryWorkshop>();
            if (!GetComponent<C3ReferenceFactoryEnvironment>()) gameObject.AddComponent<C3ReferenceFactoryEnvironment>();
            if (!GetComponent<C3RecyclingLineGeometry>()) gameObject.AddComponent<C3RecyclingLineGeometry>();
            if (!GetComponent<C3V3Circulation>()) gameObject.AddComponent<C3V3Circulation>();
        }

        private void OnEnable()
        {
            if (!device) return;
            device.Activated += BeginStartup;
            if (device.FactoryState == C3FactoryState.Starting && startup == null) BeginStartup();
            if (motorActive && motorAudio && motorAudio.clip && !motorAudio.isPlaying) motorAudio.Play();
        }
        private void OnDisable()
        {
            if (device) device.Activated -= BeginStartup;
            if (startup != null) StopCoroutine(startup);
            startup = null;
            if (motorAudio) motorAudio.Stop();
        }
        private void OnDestroy()
        {
            if (motorAudio) Destroy(motorAudio);
        }

        private void BeginStartup()
        {
            if (startup == null) startup = StartCoroutine(StartupSequence());
        }

        private IEnumerator StartupSequence()
        {
            float elapsed = 0f;
            while (elapsed < lightRestoreSeconds)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / lightRestoreSeconds);
                if (faultLight) faultLight.intensity = faultIntensity * (1f - progress);
                for (int i = 0; i < factoryLights.Length; i++)
                {
                    if (!factoryLights[i]) continue;
                    float local = Mathf.Clamp01(progress * 1.8f - i * 0.16f);
                    factoryLights[i].intensity = lightLevels[i] * Mathf.Lerp(0.45f, 1f, local);
                }
                yield return null;
            }
            for (int i = 0; i < factoryLights.Length; i++)
                if (factoryLights[i]) factoryLights[i].intensity = lightLevels[i];
            motorActive = true;
            if (motorAudio && motorAudio.clip && !motorAudio.isPlaying) motorAudio.Play();
            yield return new WaitForSeconds(motorStartSeconds);
            device.NotifyStartupFinished();
            startup = null;
        }

    }
}
