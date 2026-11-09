
using UnityEngine;

public class PlayerRecycleController : MonoBehaviour
{
    [Header("玩家設定")]
    public Camera playerCamera;
    public Transform carryPoint;

    [Header("電漿槍")]
    public GameObject gunInHand;
    public GameObject droppedGunPrefab;

    [Header("互動距離")]
    public float interactDistance = 5f;
    public float dropDistance = 2f;

    private RecycleItem carriedItem;
    private GameObject droppedGun;

    private PlayerPlasmaGun plasmaGun;

    private bool recycleMode = false;

    void Awake()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true);

        if (gunInHand != null)
        {
            plasmaGun =
                gunInHand.GetComponentInChildren<PlayerPlasmaGun>(true);
        }
    }

    // Boss死亡後呼叫
    public void EnableRecycleMode()
    {
        recycleMode = true;

        if (plasmaGun != null)
            plasmaGun.enabled = false;

        Debug.Log("進入資源回收模式！");
    }

    void Update()
    {
        if (!recycleMode) return;

        if (Input.GetMouseButtonDown(0))
        {
            HandleLeftClick();
        }
    }

    void HandleLeftClick()
    {
        if (playerCamera == null) return;

        Ray ray = playerCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f)
        );

        RaycastHit hit;

        bool hasHit = Physics.Raycast(
            ray,
            out hit,
            interactDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Collide
        );

        // 1. 手上有回收物品
        if (carriedItem != null)
        {
            if (hasHit)
            {
                RecycleHouse house =
                    hit.collider.GetComponentInParent<RecycleHouse>();
                Debug.Log("目前瞄準的物件：" + hit.collider.name);


                if (house != null)
                {
                    bool success = house.TrySubmit(carriedItem);

                    if (success)
                    {
                        // 答對：物品已經被回收
                        carriedItem = null;

                        Debug.Log("物品回收成功！");
                    }
                    else
                    {
                        // 答錯：物品放回地面
                        DropItem();

                        Debug.Log("答案錯誤，物品已放回地面！");
                    }

                    return;
                }

                else
                {
                    Debug.LogWarning("沒有偵測到 RecycleHouse！");
                }
                if (house != null)
                {
                    bool success = house.TrySubmit(carriedItem);

                    if (success)
                        carriedItem = null;

                    return;
                }
            }

            DropItem();
            return;
        }

        // 2. 手上有電漿槍，左鍵放下
        if (gunInHand != null &&
            gunInHand.activeInHierarchy)
        {
            DropGun();
            return;
        }

        if (!hasHit) return;

        // 3. 撿回地上的槍
        if (droppedGun != null &&
            (hit.collider.transform == droppedGun.transform ||
             hit.collider.transform.IsChildOf(droppedGun.transform)))
        {
            gunInHand.SetActive(true);

            Destroy(droppedGun);
            droppedGun = null;

            Debug.Log("重新拿起電漿槍！");
            return;
        }

        // 4. 撿取回收物品
        RecycleItem item =
            hit.collider.GetComponentInParent<RecycleItem>();

        if (item != null)
        {
            TryPickItem(item);
        }
    }

    void TryPickItem(RecycleItem item)
    {
        if (item == null || carriedItem != null)
            return;

        if (gunInHand != null &&
            gunInHand.activeInHierarchy)
            return;

        if (carryPoint == null) return;

        carriedItem = item;

        item.PickUp(carryPoint);

        Debug.Log("拿起物品：" + item.itemType);
    }

    // 放下物品
    void DropItem()
    {
        if (carriedItem == null) return;

        Vector3 position =
            playerCamera.transform.position +
            playerCamera.transform.forward * dropDistance;

        RaycastHit hit;

        if (Physics.Raycast(
            position + Vector3.up,
            Vector3.down,
            out hit,
            10f,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore))
        {
            position = hit.point + Vector3.up * 0.25f;
        }

        carriedItem.Drop(position);
        carriedItem = null;

        Debug.Log("放下回收物品！");
    }

    // 放下槍
    void DropGun()
    {
        if (droppedGunPrefab == null)
        {
            Debug.LogError("尚未指定 Dropped Gun Prefab！");
            return;
        }

        Vector3 position =
            playerCamera.transform.position +
            playerCamera.transform.forward * 2.5f;

        RaycastHit hit;

        if (Physics.Raycast(
            position + Vector3.up,
            Vector3.down,
            out hit,
            10f,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore))
        {
            position = hit.point + Vector3.up * 0.3f;
        }

        droppedGun = Instantiate(
            droppedGunPrefab,
            position,
            Quaternion.identity
        );

        foreach (Collider col in
                 droppedGun.GetComponentsInChildren<Collider>(true))
        {
            col.enabled = false;
        }

        SphereCollider trigger =
            droppedGun.AddComponent<SphereCollider>();

        trigger.isTrigger = true;
        trigger.radius = 0.8f;

        Rigidbody rb =
            droppedGun.GetComponent<Rigidbody>();

        if (rb == null)
            rb = droppedGun.AddComponent<Rigidbody>();

        rb.isKinematic = true;
        rb.useGravity = false;

        gunInHand.SetActive(false);

        Debug.Log("已放下電漿槍！");
    }
}
