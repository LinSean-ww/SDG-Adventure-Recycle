using System;
using UnityEngine;

namespace C3TreasurePuzzle
{
    public enum C3ProcessingStep { Sorting, Recycling, PollutionTreatment }

    // Scene-owned M4 state. A new C_Main scene creates a fresh instance.
    public sealed class C3PuzzleState : MonoBehaviour
    {
        [SerializeField] private C3TreasureProgress progress;
        public bool PuzzleSolved { get; private set; }
        // A submission is one press of Confirm with three selected steps, right or wrong.
        public int SubmissionCount { get; private set; }
        public event Action Solved;

        public void SetProgress(C3TreasureProgress value) => progress = value;

        private void Awake()
        {
            if (progress == null) progress = FindObjectOfType<C3TreasureProgress>();
        }

        public bool SubmitAnswer(C3ProcessingStep[] answer)
        {
            if (PuzzleSolved || progress == null || !progress.AllPartsCollected ||
                answer == null || answer.Length != 3) return false;
            SubmissionCount++;
            return TrySolve(answer);
        }

        public bool TrySolve(C3ProcessingStep[] answer)
        {
            if (PuzzleSolved || progress == null || !progress.AllPartsCollected ||
                answer == null || answer.Length != 3) return false;
            if (answer[0] != C3ProcessingStep.Sorting ||
                answer[1] != C3ProcessingStep.Recycling ||
                answer[2] != C3ProcessingStep.PollutionTreatment) return false;
            PuzzleSolved = true;
            Solved?.Invoke();
            return true;
        }
    }
}
