using System.Collections.Generic;
using UnityEngine;

namespace M6IntegrationTests
{
    // Fixture-owned contact evidence from the production CharacterController.
    // A blocked move alone does not prove that the intended guard stopped it.
    public sealed class C3V11CollisionRecorder : MonoBehaviour
    {
        public readonly HashSet<Collider> SideContacts = new HashSet<Collider>();
        // Opt-in evidence for one explicit Move at a time. Existing circulation
        // fixtures keep the original bounded SideContacts behaviour by default.
        public sealed class ContactEvidence
        {
            public Collider Collider;
            public Vector3 Normal,Point,MoveDirection;
            public float MoveLength;
        }
        public bool CaptureDetails;
        public readonly List<ContactEvidence> DetailedContacts = new List<ContactEvidence>();

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (CaptureDetails && hit.collider)
                DetailedContacts.Add(new ContactEvidence {Collider=hit.collider,Normal=hit.normal,Point=hit.point,
                    MoveDirection=hit.moveDirection,MoveLength=hit.moveLength});
            if (hit.collider && Mathf.Abs(hit.normal.y) < .6f)
                SideContacts.Add(hit.collider);
        }
    }
}
