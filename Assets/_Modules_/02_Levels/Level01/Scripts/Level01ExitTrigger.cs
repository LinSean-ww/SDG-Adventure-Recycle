using System;
using SDGAdventure.SharedPlayer;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class Level01ExitTrigger : MonoBehaviour
{
    private TreasureDetector detector;
    private DoorInteraction door;
    private SharedFirstPersonController player;
    private Action onExit;
    private Vector3 exitDirection;
    private Vector3 doorwayOrigin;
    private bool approachedFromInside;
    private bool reported;

    public void Initialize(TreasureDetector progress, DoorInteraction powerDoor,
        SharedFirstPersonController sharedPlayer, Action exitCallback)
    {
        detector = progress; door = powerDoor; player = sharedPlayer; onExit = exitCallback;
        doorwayOrigin = door.doorObject.position;
        exitDirection = -door.doorObject.forward;
        exitDirection.y = 0f;
        exitDirection.Normalize();
        approachedFromInside = false;
        reported = false;
    }

    private void Update()
    {
        if (player != null && Vector3.Dot(player.transform.position - doorwayOrigin, exitDirection) < -0.25f)
            approachedFromInside = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (reported || detector == null || door == null || player == null || onExit == null ||
            detector.TotalCount != 10 || detector.CollectedCount != 10 || !door.IsOpen ||
            !approachedFromInside || other.GetComponentInParent<SharedFirstPersonController>() != player ||
            Vector3.Dot(player.transform.position - doorwayOrigin, exitDirection) <= 0.25f ||
            PlayerInputService.Instance == null || PlayerInputService.Instance.Context != PlayerInputContext.Gameplay)
            return;
        reported = true;
        onExit();
    }

    public void Clear() { onExit = null; detector = null; door = null; player = null; }
}
