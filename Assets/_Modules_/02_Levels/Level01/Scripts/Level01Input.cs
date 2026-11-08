using SDGAdventure.SharedPlayer;
using UnityEngine;

// Level-specific consumers share the same action bindings as weapons.
public static class Level01Input
{
    private static scanball scanner;
    private static TreasureDetector detector;
    private static DoorInteraction powerDoor;
    private static NewDoor[] gates;
    public static void Configure(scanball scan, TreasureDetector treasure, DoorInteraction door, NewDoor[] levelGates)
    { scanner = scan; detector = treasure; powerDoor = door; gates = levelGates; }
    public static void Clear() { scanner = null; detector = null; powerDoor = null; gates = null; }
    public static bool IsBusy
    {
        get
        {
            if ((scanner != null && scanner.IsRunning) || (detector != null && detector.IsAnimating) ||
                (powerDoor != null && powerDoor.IsBusy)) return true;
            if (gates != null) foreach (NewDoor gate in gates) if (gate != null && gate.IsMoving) return true;
            return false;
        }
    }
    private static PlayerInputService Input => PlayerInputService.Instance;
    private static bool GameplayAllowed => Input != null &&
        Input.Context == PlayerInputContext.Gameplay && PlayerCursor.LockState == CursorLockMode.Locked &&
        (EcoAdventureRuntime.Instance == null || EcoAdventureRuntime.Instance.IsGameplayInputAllowed);
    public static bool CanUseTools => GameplayAllowed && !IsBusy &&
        (EcoAdventureRuntime.Instance == null || !EcoAdventureRuntime.Instance.IsQuantumWeaponEquipped);
    public static bool PrimaryPressed => Input != null && Input.PrimaryPressed;
    public static bool SecondaryPressed => Input != null && Input.SecondaryPressed;
    public static bool InteractPressed => GameplayAllowed && !IsBusy && Input.InteractWasPressed;
    public static string Binding(string action)
    {
        if (Input == null) return "";
        var binding = Input.Actions.FindAction("Gameplay/" + action);
        if (binding != null && binding.bindings.Count > 0)
        {
            string path = binding.bindings[0].effectivePath;
            if (string.Equals(path, "<Mouse>/leftButton", System.StringComparison.OrdinalIgnoreCase)) return "滑鼠左鍵";
            if (string.Equals(path, "<Mouse>/rightButton", System.StringComparison.OrdinalIgnoreCase)) return "滑鼠右鍵";
            if (string.Equals(path, "<Mouse>/middleButton", System.StringComparison.OrdinalIgnoreCase)) return "滑鼠中鍵";
        }
        return Input.Keybindings.BindingDisplay("Gameplay", action, 0);
    }

    public static bool IsLookingAtGate()
    {
        Camera camera = Camera.main;
        if (camera == null || !Physics.Raycast(camera.transform.position, camera.transform.forward,
                out RaycastHit hit, 3f)) return false;
        return hit.transform.GetComponent<NewDoor>() != null;
    }
}
