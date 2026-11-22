using System;
using UnityEngine;

namespace C3TreasurePuzzle
{
    public enum C3DevicePhase { Unrepaired, AwaitingPuzzle, ReadyToActivate, Activated }
    public enum C3FactoryState { Dormant, Starting, Running, Completed }

    // The sole scene-level authority for activation and independent level completion.
    public sealed class C3DeviceState : MonoBehaviour
    {
        [SerializeField] private C3TreasureProgress progress;
        [SerializeField] private C3PuzzleState puzzle;
        [Tooltip("Temporary fallback until the startup animation calls NotifyStartupFinished. Disable for the formal animation event.")]
        [SerializeField] private bool useTestStartupDelay = true;
        [SerializeField, Min(0f)] private float testStartupDelaySeconds = 3f;
        private float startedAt;
        private float startupElapsed;
        public C3FactoryState FactoryState { get; private set; } = C3FactoryState.Dormant;
        public bool IsActivated => FactoryState != C3FactoryState.Dormant;
        public bool LevelCompleted => FactoryState == C3FactoryState.Completed;
        public float CompletionSeconds { get; private set; }
        public float ElapsedSeconds => LevelCompleted ? CompletionSeconds : Mathf.Max(0f, Time.unscaledTime - startedAt);
        public event Action Activated;
        public event Action Running;
        public event Action Completed;

        public C3DevicePhase Phase => IsActivated ? C3DevicePhase.Activated :
            progress == null || !progress.AllPartsCollected ? C3DevicePhase.Unrepaired :
            puzzle == null || !puzzle.PuzzleSolved ? C3DevicePhase.AwaitingPuzzle : C3DevicePhase.ReadyToActivate;

        public void Configure(C3TreasureProgress owner, C3PuzzleState puzzleState)
        {
            progress = owner;
            puzzle = puzzleState;
        }

        private void Awake()
        {
            if (progress == null) progress = GetComponent<C3TreasureProgress>();
            if (puzzle == null) puzzle = GetComponent<C3PuzzleState>();
            startedAt = Time.unscaledTime;
        }

        public bool TryActivate()
        {
            if (Phase != C3DevicePhase.ReadyToActivate || IsActivated || LevelCompleted) return false;
            FactoryState = C3FactoryState.Starting;
            startupElapsed = 0f;
            Activated?.Invoke();
            return true;
        }

        private void Update()
        {
            if (FactoryState != C3FactoryState.Starting || !useTestStartupDelay) return;
            startupElapsed += Time.deltaTime;
            if (startupElapsed >= testStartupDelaySeconds) NotifyStartupFinished();
        }

        // Formal startup animation may call this; disable the test delay when connected.
        public void NotifyStartupFinished()
        {
            if (FactoryState != C3FactoryState.Starting) return;
            FactoryState = C3FactoryState.Running;
            Running?.Invoke();
            if (EcoAdventureRuntime.Instance != null)
                EcoAdventureRuntime.Instance.FactoryPoweredOn();
        }

        internal bool TryCompleteAtExit()
        {
            if (FactoryState != C3FactoryState.Running || progress == null || !progress.AllPartsCollected ||
                puzzle == null || !puzzle.PuzzleSolved) return false;
            FactoryState = C3FactoryState.Completed;
            CompletionSeconds = Mathf.Max(0f, Time.unscaledTime - startedAt);
            Completed?.Invoke();
            return true;
        }
    }
}
