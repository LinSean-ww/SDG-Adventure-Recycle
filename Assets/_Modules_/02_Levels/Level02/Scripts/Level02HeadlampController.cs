using SDGAdventure.SharedPlayer;
using UnityEngine;

// 在角色動畫及視角更新後定位，光源位置不取攝影機位置。
[DefaultExecutionOrder(400)]
[DisallowMultipleComponent]
public sealed class Level02HeadlampController : MonoBehaviour
{
    private Transform player;
    private Transform head;
    private PlayerInputService input;
    private Light headlamp;
    private Camera boundCamera;
    private bool lampEnabled = true;

    public void Initialize(Transform playerTransform, PlayerInputService inputService)
    {
        player = playerTransform;
        input = inputService;
        if (input) input.HeadlampPressed += Toggle;
        FindAnchors();
    }

    private void LateUpdate()
    {
        if (!head || !boundCamera || !boundCamera.isActiveAndEnabled) FindAnchors();
        if (!head || !boundCamera || !headlamp) return;
        // 由視線方向偏移出頭盔，光源仍以角色頭部為原點。
        Vector3 direction = boundCamera.transform.forward;
        headlamp.transform.position = head.position + head.up * 0.08f + direction * 0.22f;
        headlamp.transform.rotation = Quaternion.LookRotation(direction, boundCamera.transform.up);
        headlamp.enabled = lampEnabled;
    }

    private void FindAnchors()
    {
        if (!player) return;
        head = player.Find("V11_RecyclingEngineer_Visual/WorkerPelvis/WorkerHead");
        boundCamera = null;
        foreach (Camera candidate in player.GetComponentsInChildren<Camera>(true))
            if (candidate.isActiveAndEnabled) { boundCamera = candidate; break; }
        if (!head || !boundCamera)
        {
            if (headlamp) headlamp.enabled = false;
            return;
        }
        if (headlamp) return;
        var host = new GameObject("Level02HeadlampMount");
        host.transform.SetParent(transform, false);
        headlamp = host.AddComponent<Light>();
        headlamp.type = LightType.Spot;
        headlamp.range = 15f;
        headlamp.spotAngle = 65f;
        headlamp.intensity = 2f;
        headlamp.color = new Color(1f, 0.94f, 0.82f);
        headlamp.shadows = LightShadows.None;
        headlamp.enabled = lampEnabled;
    }

    private void Toggle()
    {
        if (!input || input.Context != PlayerInputContext.Gameplay) return;
        lampEnabled = !lampEnabled;
        if (headlamp) headlamp.enabled = lampEnabled;
    }

    private void OnDestroy()
    {
        if (input) input.HeadlampPressed -= Toggle;
        if (headlamp) Destroy(headlamp.gameObject);
    }
}
