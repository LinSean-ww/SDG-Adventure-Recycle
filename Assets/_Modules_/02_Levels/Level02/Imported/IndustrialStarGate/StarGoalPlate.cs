using System.Collections.Generic;
using UnityEngine;

/// <summary>Opens the gate after every active StarBox stays on the platform.</summary>
public sealed class StarGoalPlate : MonoBehaviour
{
    [SerializeField] private SlidingStarGate gate;
    [SerializeField, Min(0f)] private float settleTime = 0.2f;
    [SerializeField] private Vector2 deckHalfSize = new Vector2(2.1f, 2.1f);
    [SerializeField, Min(0f)] private float deckTopOffset = 0.49f;
    [SerializeField, Min(0f)] private float maxStackHeight = 20f;
    [SerializeField] private bool limitStarsToDetectorRoom;

    private readonly HashSet<Transform> countedBoxes = new HashSet<Transform>();
    private readonly HashSet<Transform> placedLastStep = new HashSet<Transform>();
    private readonly HashSet<Transform> placedThisStep = new HashSet<Transform>();
    private BoxCollider detectionZone;
    private AudioClip correctSound;
    private AudioClip wrongSound;
    private bool feedbackInitialized;
    private float nextCorrectSoundTime;
    private float nextWrongSoundTime;
    private float settledFor;
    private int lastStarCount = -1;
    private int lastPlacedCount = -1;
    private bool warnedGateUnavailable;
    private bool complete;

    public int TotalStars { get; private set; }
    public int PlacedStars { get; private set; }
    public bool IsComplete => complete;

    private void Awake()
    {
        correctSound = Resources.Load<AudioClip>("FactorySounds/PlatformCorrect");
        wrongSound = Resources.Load<AudioClip>("FactorySounds/PlatformWrong");
        BoxCollider surface = null;
        foreach (BoxCollider collider in GetComponents<BoxCollider>())
        {
            if (collider.isTrigger) detectionZone = collider;
            else surface = collider;
        }

        maxStackHeight = Mathf.Max(maxStackHeight, 20f);
        if (detectionZone == null) return;
        Vector3 size = detectionZone.size;
        size.x = Mathf.Max(size.x, surface != null ? surface.size.x : deckHalfSize.x * 2f);
        size.z = Mathf.Max(size.z, surface != null ? surface.size.z : deckHalfSize.y * 2f);
        size.y = Mathf.Max(size.y, maxStackHeight);
        detectionZone.size = size;
        detectionZone.center = new Vector3(detectionZone.center.x, deckTopOffset + size.y * 0.5f, detectionZone.center.z);
        detectionZone.enabled = true;
    }

    private void FixedUpdate()
    {
        if (complete) return;
        countedBoxes.Clear();
        placedThisStep.Clear();
        TotalStars = 0;
        PlacedStars = 0;
        bool playCorrect = false;
        bool playWrong = false;

        // Imported box models can put their visible name on a child and their Rigidbody
        // on the parent. Count the named box first, then find its physics body.
        foreach (Transform node in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            bool star = IsStarBoxName(node.name);
            if (!star && !IsNormalBoxName(node.name)) continue;
            Rigidbody body = node.GetComponentInParent<Rigidbody>();
            if (body == null) body = node.GetComponentInChildren<Rigidbody>();
            Transform starRoot = body != null ? body.transform : node;
            if (!countedBoxes.Add(starRoot)) continue;
            if (star && limitStarsToDetectorRoom && !StarDetectorMap.IsInsideRoom(GetBoxCenter(node, body))) continue;
            if (star) TotalStars++;
            // Use the same star placement test as the detector. Requiring the
            // whole collider to fit and the Rigidbody to stay slow made the
            // displayed remaining count disagree with the gate.
            if (!(star ? IsStarOnPlatform(body) : IsPlaced(node, body))) continue;
            placedThisStep.Add(starRoot);
            if (star) PlacedStars++;
            if (feedbackInitialized && !placedLastStep.Contains(starRoot))
            {
                if (star) playCorrect = true;
                else playWrong = true;
            }
        }

        if (playCorrect && correctSound != null && Time.time >= nextCorrectSoundTime)
        {
            AudioSource.PlayClipAtPoint(correctSound, transform.position, 1f);
            nextCorrectSoundTime = Time.time + 0.35f;
        }
        if (playWrong && wrongSound != null && Time.time >= nextWrongSoundTime)
        {
            AudioSource.PlayClipAtPoint(wrongSound, transform.position, 1f);
            nextWrongSoundTime = Time.time + 0.35f;
        }
        placedLastStep.Clear();
        placedLastStep.UnionWith(placedThisStep);
        feedbackInitialized = true;

        if (TotalStars != lastStarCount || PlacedStars != lastPlacedCount)
        {
            Debug.Log($"Star boxes on platform: {PlacedStars}/{TotalStars}", this);
            lastPlacedCount = PlacedStars;
        }

        if (TotalStars != lastStarCount)
        {
            settledFor = 0f;
            lastStarCount = TotalStars;
        }

        if (TotalStars == 0 || PlacedStars != TotalStars)
        {
            settledFor = 0f;
            return;
        }

        settledFor += Time.fixedDeltaTime;
        if (settledFor < settleTime) return;
        if (gate == null)
        {
            Debug.LogError("Star goal plate has no gate assigned.", this);
            return;
        }

        if (gate.Open()) complete = true;
        else if (!warnedGateUnavailable)
        {
            warnedGateUnavailable = true;
            Debug.LogError("All star boxes are placed, but the sliding gate could not open.", this);
        }
    }

    private bool IsPlaced(Transform node, Rigidbody body)
    {
        // The player box grabber makes held boxes kinematic.
        if (body != null && (body.isKinematic || body.velocity.sqrMagnitude > 4f)) return false;
        BoxCollider box = body != null ? body.GetComponent<BoxCollider>() : null;
        if (box == null) box = node.GetComponentInParent<BoxCollider>();
        if (box == null) box = node.GetComponentInChildren<BoxCollider>();
        if (box == null || !box.enabled || box.isTrigger) return false;

        Bounds bounds = box.bounds;
        if (detectionZone != null && detectionZone.enabled)
        {
            Bounds zone = detectionZone.bounds;
            return bounds.min.x >= zone.min.x && bounds.max.x <= zone.max.x &&
                   bounds.min.z >= zone.min.z && bounds.max.z <= zone.max.z &&
                   bounds.min.y >= zone.min.y - 0.35f && bounds.max.y <= zone.max.y;
        }

        float deckY = transform.position.y + deckTopOffset;
        float left = transform.position.x - deckHalfSize.x;
        float right = transform.position.x + deckHalfSize.x;
        float near = transform.position.z - deckHalfSize.y;
        float far = transform.position.z + deckHalfSize.y;

        return bounds.center.x >= left + 0.2f && bounds.center.x <= right - 0.2f &&
               bounds.center.z >= near + 0.2f && bounds.center.z <= far - 0.2f &&
               bounds.min.y >= deckY - 0.35f &&
               bounds.max.y <= deckY + maxStackHeight;
    }

    public bool IsStarPlaced(Rigidbody body) => body != null && IsPlaced(body.transform, body);

    // The detector and gate share this geometric test. The gate's settle timer
    // prevents an airborne box crossing the area from opening it immediately.
    public bool IsStarOnPlatform(Rigidbody body)
    {
        if (body == null || body.isKinematic) return false;
        BoxCollider box = body.GetComponent<BoxCollider>();
        if (box == null) box = body.GetComponentInChildren<BoxCollider>();
        if (box == null || !box.enabled || box.isTrigger) return false;

        Bounds bounds = box.bounds;
        if (detectionZone != null && detectionZone.enabled)
        {
            Bounds zone = detectionZone.bounds;
            const float edgeAllowance = 0.15f;
            return bounds.center.x >= zone.min.x - edgeAllowance &&
                   bounds.center.x <= zone.max.x + edgeAllowance &&
                   bounds.center.z >= zone.min.z - edgeAllowance &&
                   bounds.center.z <= zone.max.z + edgeAllowance &&
                   bounds.min.y >= zone.min.y - 0.5f && bounds.max.y <= zone.max.y;
        }

        float deckY = transform.position.y + deckTopOffset;
        return bounds.center.x >= transform.position.x - deckHalfSize.x &&
               bounds.center.x <= transform.position.x + deckHalfSize.x &&
               bounds.center.z >= transform.position.z - deckHalfSize.y &&
               bounds.center.z <= transform.position.z + deckHalfSize.y &&
               bounds.min.y >= deckY - 0.5f && bounds.max.y <= deckY + maxStackHeight;
    }

    public static Vector3 GetBoxCenter(Transform node, Rigidbody body)
    {
        BoxCollider box = body != null ? body.GetComponent<BoxCollider>() : null;
        if (box == null) box = node.GetComponentInParent<BoxCollider>();
        if (box == null) box = node.GetComponentInChildren<BoxCollider>();
        return box != null ? box.bounds.center : body != null ? body.worldCenterOfMass : node.position;
    }

    private static bool IsStarBoxName(string name)
    {
        return name == "StarBox" ||
               name.StartsWith("StarBox (", System.StringComparison.Ordinal);
    }

    private static bool IsNormalBoxName(string name)
    {
        return name == "NormalBox" ||
               name.StartsWith("NormalBox (", System.StringComparison.Ordinal);
    }
}
