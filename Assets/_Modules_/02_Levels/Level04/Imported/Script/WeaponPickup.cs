
using UnityEngine;

public class WeaponPickup : MonoBehaviour
{
    [Header("玩家物件")]
    public Transform playerRoot;

    [Header("玩家手上的武器")]
    public GameObject weaponInHand;

    private bool collected = false;

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("拾取碰撞觸發：" + other.name);

        if (collected) return;

        // 確認是否為指定的玩家或玩家子物件
        if (playerRoot == null)
        {
            Debug.LogError("尚未指定 Player！");
            return;
        }

        bool isPlayer =
            other.transform == playerRoot ||
            other.transform.IsChildOf(playerRoot);

        if (!isPlayer)
        {
            Debug.Log("不是玩家，忽略：" + other.name);
            return;
        }

        Debug.Log("成功偵測玩家！");

        // 確認玩家手上的槍
        if (weaponInHand == null)
        {
            Debug.LogError("尚未指定玩家手上的槍！");
            return;
        }

        // 顯示玩家手上的槍
        weaponInHand.SetActive(true);

        collected = true;

        Debug.Log("成功取得電漿槍！");

        // 移除地上的武器
        Destroy(gameObject);
    }
}
