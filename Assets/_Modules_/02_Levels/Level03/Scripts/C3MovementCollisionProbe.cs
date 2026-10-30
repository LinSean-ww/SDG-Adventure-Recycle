using UnityEngine;
using UnityEngine.InputSystem;

namespace C3TreasurePuzzle
{
    /// <summary>
    /// User-verifiable collision evidence. Press F8 to expose position and the last
    /// wall/obstacle contacted by the real CharacterController. Does not alter physics.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class C3MovementCollisionProbe : MonoBehaviour
    {
        private string lastBlocker = "尚未偵測到阻擋";
        private string lastPath = "";
        private float lastHitTime;
        private float lastLogTime;
        private string lastLoggedName = "";
        private bool showOverlay;
        private CharacterController body;
        private string forwardCandidate = "無近距離障礙";
        private string forwardPath = "";

        private void Awake() { body = GetComponent<CharacterController>(); }
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame)
                showOverlay = !showOverlay;
            if (!showOverlay || body == null) return;
            // Exact live capsule cast in the player-facing direction. The previous
            // V6 overlay merely remembered a potentially stale collision event.
            Vector3 dir = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 basePoint = transform.position + body.center;
            float capsuleHalf = Mathf.Max(0f, body.height*.5f-body.radius);
            Vector3 a=basePoint+Vector3.up*capsuleHalf;
            Vector3 b=basePoint-Vector3.up*capsuleHalf;
            RaycastHit[] hits=Physics.CapsuleCastAll(a,b,body.radius*.92f,dir,1.10f,
                ~0,QueryTriggerInteraction.Ignore);
            float nearest=999f;
            forwardCandidate="面向前方 1.1m：無阻擋";forwardPath="";
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider==null || hit.collider==body ||
                    hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.normal.y > .55f || hit.distance >= nearest) continue;
                nearest=hit.distance;
                forwardCandidate=hit.collider.name+"（距離 "+hit.distance.ToString("F2")+"m）";
                forwardPath=GetPath(hit.collider.transform);
            }
        }
        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit == null || hit.collider == null || hit.normal.y > .55f) return;
            // CharacterController reports every physical contact; only note side impacts.
            // Unlike a broad collider cleaner, this never disables quest triggers.
            lastBlocker = hit.collider.gameObject.name;
            lastPath = GetPath(hit.collider.transform);
            lastHitTime = Time.time;
            if (lastLoggedName != lastPath || Time.time-lastLogTime > 3f)
            {
                lastLoggedName = lastPath;
                lastLogTime = Time.time;
                Debug.LogWarning("C3 movement blocked by: " + lastPath +
                    " | world=" + hit.point.ToString("F2") +
                    " | player=" + transform.position.ToString("F2"));
            }
        }
        private static string GetPath(Transform child)
        {
            string text=child.name;
            for (Transform current=child.parent;current!=null && text.Length<170;current=current.parent)
                text=current.name+"/"+text;
            return text;
        }
        private void OnGUI()
        {
            if (!showOverlay) return;
            GUI.Box(new Rect(12,80,780,143), "C3 碰撞診斷（F8 隱藏）");
            GUI.Label(new Rect(24,103,675,24), "玩家座標："+transform.position.ToString("F2")+
                " | 接地："+(body!=null && body.isGrounded));
            GUI.Label(new Rect(24,126,675,24), "最後橫向碰撞："+lastBlocker+
                " | "+(Time.time-lastHitTime).ToString("F1")+" 秒前");
            GUI.Label(new Rect(24,149,750,24), lastPath);
            GUI.Label(new Rect(24,174,750,24), "目前朝向："+forwardCandidate);
            GUI.Label(new Rect(24,195,750,24), forwardPath);
        }
    }
}
