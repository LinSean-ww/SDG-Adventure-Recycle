using UnityEngine;

/// <summary>Slides both static model leaves open, then shuts and locks after the player passes.</summary>
public sealed class SlidingStarGate : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float travelDistance = 2.4f;
    [SerializeField, Min(0.1f)] private float openDuration = 2f;
    [SerializeField] private Transform leftLeaf;
    [SerializeField] private Transform rightLeaf;
    [SerializeField] private BoxCollider returnBarrier;
    [SerializeField, Min(0.1f)] private float crossingMargin = 0.8f;

    private enum GateState { Closed, Opening, Open, Closing, Locked }
    private GateState state;
    private CharacterController player;
    private Vector3 leftClosedPosition;
    private Vector3 rightClosedPosition;
    private float leftOutwardSign;
    private float progress;
    private int approachSide;
    private AudioClip openSound;
    private AudioClip closeSound;

    public bool IsOpen => state == GateState.Open;
    public bool IsLocked => state == GateState.Locked;
    public event System.Action PlayerPassedAndGateLocked;
    public void SetPlayer(CharacterController value) => player = value;

    private void Awake()
    {
        if (leftLeaf == null) leftLeaf = FindLeaf("IndustrialStarGate_Left");
        if (rightLeaf == null) rightLeaf = FindLeaf("IndustrialStarGate_Right");
        // Model prefab root names can differ from the OBJ mesh names. The scene
        // groups the frame, left leaf and right leaf as children in that order.
        if (transform.childCount >= 3)
        {
            if (leftLeaf == null) leftLeaf = transform.GetChild(1);
            if (rightLeaf == null) rightLeaf = transform.GetChild(2);
        }
        if (returnBarrier == null) returnBarrier = GetComponent<BoxCollider>();
        if (leftLeaf == null || rightLeaf == null || returnBarrier == null)
        {
            Debug.LogError($"Sliding gate setup failed: left={leftLeaf != null}, right={rightLeaf != null}, barrier={returnBarrier != null}, children={transform.childCount}.", this);
            enabled = false;
            return;
        }

        leftClosedPosition = leftLeaf.position;
        rightClosedPosition = rightLeaf.position;
        // The OBJ vertices are baked into the meshes, so both leaf Transforms may
        // sit at the same position. Use the visible mesh centers to find each side.
        float leftX = transform.InverseTransformPoint(LeafCenter(leftLeaf)).x;
        float rightX = transform.InverseTransformPoint(LeafCenter(rightLeaf)).x;
        leftOutwardSign = leftX <= rightX ? -1f : 1f;
        returnBarrier.enabled = false;
        openSound = Resources.Load<AudioClip>("FactorySounds/GateOpen");
        closeSound = Resources.Load<AudioClip>("FactorySounds/GateClose");
    }

    public bool Open()
    {
        if (!enabled || state == GateState.Locked || state == GateState.Closing) return false;
        if (state != GateState.Closed) return true;
        state = GateState.Opening;
        if (openSound != null) AudioSource.PlayClipAtPoint(openSound, transform.position, 1f);
        ObserveApproach();
        return true;
    }

    private void Update()
    {
        if (state == GateState.Closed || state == GateState.Locked) return;

        ObserveApproach();
        if (state == GateState.Opening)
        {
            progress = Mathf.MoveTowards(progress, 1f, Time.deltaTime / openDuration);
            MoveLeaves();
            if (progress >= 1f) state = GateState.Open;
        }
        else if (state == GateState.Open && PlayerPassedThrough())
        {
            returnBarrier.enabled = true;
            state = GateState.Closing;
            if (closeSound != null) AudioSource.PlayClipAtPoint(closeSound, transform.position, 1f);
        }
        else if (state == GateState.Closing)
        {
            progress = Mathf.MoveTowards(progress, 0f, Time.deltaTime / openDuration);
            MoveLeaves();
            if (progress <= 0f) { state = GateState.Locked; PlayerPassedAndGateLocked?.Invoke(); }
        }
    }

    private void ObserveApproach()
    {
        if (player == null) return;
        Vector3 local = transform.InverseTransformPoint(player.transform.position);
        if (approachSide == 0 && Mathf.Abs(local.z) > crossingMargin &&
            Mathf.Abs(local.x) < 3.5f)
            approachSide = local.z > 0f ? 1 : -1;
    }

    private bool PlayerPassedThrough()
    {
        if (approachSide == 0 || player == null) return false;
        Vector3 local = transform.InverseTransformPoint(player.transform.position);
        return local.z * approachSide < -crossingMargin && Mathf.Abs(local.x) < 3.5f;
    }

    private Transform FindLeaf(string objectName)
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
            if (child.name == objectName) return child;
        GameObject found = GameObject.Find(objectName);
        if (found != null) return found.transform;
        foreach (MeshFilter filter in FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (filter.sharedMesh != null && filter.sharedMesh.name.Contains(objectName))
                return filter.transform;
        return null;
    }

    private void MoveLeaves()
    {
        float eased = progress * progress * (3f - 2f * progress);
        Vector3 offset = transform.right * (travelDistance * eased * leftOutwardSign);
        leftLeaf.position = leftClosedPosition + offset;
        rightLeaf.position = rightClosedPosition - offset;
    }

    private static Vector3 LeafCenter(Transform leaf)
    {
        Renderer[] renderers = leaf.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return leaf.position;
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds.center;
    }
}
