using UnityEngine;
using SDGAdventure.SharedPlayer;

namespace C3TreasurePuzzle
{
    /// <summary>
    /// The capsule protects its centre, but Q/E displaces the eye by .21m.
    /// A .1m near plane can then cross the thin FRONT_TOP/column panels while
    /// the .3m body remains correctly blocked. Sweep the eye's near-plane
    /// envelope, not the player's movement capsule. Only installed in C3.
    /// </summary>
    [DefaultExecutionOrder(350)]
    [DisallowMultipleComponent]
    public sealed class C3FactoryCameraClearance : MonoBehaviour
    {
        private Camera view;
        private CharacterController body;
        private PlayerPerspectiveRig rig;

        private void Awake()
        {
            view = GetComponentInChildren<Camera>();
            body = GetComponent<CharacterController>();
            rig = GetComponent<PlayerPerspectiveRig>();
        }

        public static float NearPlaneRadius(Camera camera)
        {
            float halfHeight = camera.nearClipPlane * Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad);
            float halfWidth = halfHeight * camera.aspect;
            return Mathf.Sqrt(camera.nearClipPlane * camera.nearClipPlane +
                halfHeight * halfHeight + halfWidth * halfWidth) + .015f;
        }

        private void LateUpdate()
        {
            if (!view || !body || !body.enabled || (rig && rig.IsThirdPerson)) return;
            Vector3 origin = transform.TransformPoint(body.center);
            Vector3 desired = view.transform.position;
            Vector3 delta = desired - origin;
            if (delta.sqrMagnitude < .000001f) return;
            float distance = delta.magnitude;
            float safe = distance;
            // Complete query: dense machine banks must not truncate the nearest hit.
            foreach (RaycastHit hit in Physics.SphereCastAll(origin, NearPlaneRadius(view),
                delta / distance, distance, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider && !hit.transform.IsChildOf(transform))
                    safe = Mathf.Min(safe, Mathf.Max(0f, hit.distance - .01f));
            view.transform.position = origin + delta.normalized * safe;
        }
    }
}
