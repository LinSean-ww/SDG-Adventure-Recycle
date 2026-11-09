
using UnityEngine;
using System.Collections;

public class PlayerPlasmaGun : MonoBehaviour
{
    [Header("射擊設定")]
    public Camera playerCamera;
    public Transform muzzlePoint;
    public float fireInterval = 0.4f;
    public float shootDistance = 100f;

    [Header("電漿特效")]
    public GameObject projectileEffect;
    public GameObject lightningEffect;

    private float nextFireTime = 0f;

    void Update()
    {
        if (Input.GetMouseButtonDown(0) &&
            Time.time >= nextFireTime)
        {
            Shoot();
            nextFireTime = Time.time + fireInterval;
        }
    }

    void Shoot()
    {
        if (playerCamera == null || muzzlePoint == null)
            return;

        Ray ray = playerCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f)
        );

        RaycastHit hit;

        bool hasHit = Physics.Raycast(
            ray,
            out hit,
            shootDistance
        );

        Vector3 targetPoint = hasHit
            ? hit.point
            : ray.origin + ray.direction * shootDistance;

        // 槍口閃電特效
        if (lightningEffect != null)
        {
            GameObject lightning = Instantiate(
                lightningEffect,
                muzzlePoint.position,
                muzzlePoint.rotation
            );

            Destroy(lightning, 1.5f);
        }

        // 電漿投射物
        if (projectileEffect != null)
        {
            StartCoroutine(MoveProjectile(targetPoint));
        }

        // Boss傷害判定
        if (hasHit)
        {
            BossHealth boss =
                hit.collider.GetComponentInParent<BossHealth>();

            if (boss != null)
            {
                boss.TakeDamage(1);
                Debug.Log("電漿命中 Boss！");
            }
        }
    }

    IEnumerator MoveProjectile(Vector3 target)
    {
        Vector3 start = muzzlePoint.position;

        Vector3 direction = target - start;

        Quaternion rotation = direction.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(direction)
            : muzzlePoint.rotation;

        GameObject projectile = Instantiate(
            projectileEffect,
            start,
            rotation
        );

        float duration = 0.15f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (projectile == null)
                yield break;

            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);

            projectile.transform.position =
                Vector3.Lerp(start, target, t);

            yield return null;
        }

        if (projectile != null)
            Destroy(projectile, 2f);
    }
}
