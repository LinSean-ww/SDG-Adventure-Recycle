
using UnityEngine;

public enum RecycleItemType
{
    Brick = 0,
    Tire = 1,
    GlassBottle = 2,
    Disc = 3,
    Motherboard = 4
}

public class RecycleItem : MonoBehaviour
{
    [Header("物品類型")]
    public RecycleItemType itemType;

    private Collider[] itemColliders;
    private Rigidbody rb;
    private bool isCarried;
    private Vector3 originalWorldScale;

    void Awake()
    {
        itemColliders = GetComponentsInChildren<Collider>();
        rb = GetComponent<Rigidbody>();
    }

    public void PickUp(Transform carryPoint)
    {
        if (isCarried || carryPoint == null) return;
        originalWorldScale = transform.lossyScale;
        isCarried = true;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        foreach (Collider col in itemColliders)
            col.enabled = false;

        transform.SetParent(carryPoint);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        // Scale only the held representation; the world object is restored on drop.
        var renderers = GetComponentsInChildren<Renderer>();
        bool hasBounds = false;
        Bounds bounds = default;
        foreach (var renderer in renderers)
        {
            if (!renderer.enabled || renderer.name.StartsWith("V11_HintOverlay_")) continue;
            if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        if (hasBounds)
        {
            float largest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (largest > 0.5f) transform.localScale *= 0.5f / largest;
        }
    }

    public void Drop(Vector3 position)
    {
        transform.SetParent(null);
        if (isCarried) transform.localScale = originalWorldScale;
        transform.position = position;

        isCarried = false;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        foreach (Collider col in itemColliders)
            col.enabled = true;
    }
}
