using System.Collections.Generic;
using UnityEngine;

namespace M6IntegrationTests
{
    // Fixture-owned contact evidence from the production CharacterController.
    // A blocked move alone does not prove that the intended guard stopped it.
    public sealed class C3V11CollisionRecorder : MonoBehaviour
    {
        public readonly HashSet<Collider> SideContacts = new HashSet<Collider>();

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.collider && Mathf.Abs(hit.normal.y) < .6f)
                SideContacts.Add(hit.collider);
        }
    }
}
