using System;
using System.Collections.Generic;
using SDGAdventure.SharedPlayer;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Maps the current star-box search area to the green room's floor plan.</summary>
public sealed class StarDetectorMap : MonoBehaviour, IPlayerInteractable
{
    public const int Columns = 20;
    public const int Rows = 25;
    private const int CellPixels = 20;
    private const float MinX = 0.496f;
    private const float MaxX = 24.496f;
    private const float MinZ = 5.021f;
    private const float MaxZ = 28.482f;
    private const float CooldownSeconds = 5f;

    [SerializeField] private StarGoalPlate platform;

    private readonly HashSet<Rigidbody> seenBodies = new HashSet<Rigidbody>();
    private readonly List<Rigidbody> availableStars = new List<Rigidbody>();
    private Rigidbody selectedStar;
    private GameObject actor;
    private PlayerInputService input;
    private InputAction closeInteract;
    private Canvas canvas;
    private RectTransform mapViewport;
    private RectTransform mapPanel;
    private ScrollRect mapScroll;
    private EcoAdventureRuntime suppressedQuickbar;
    private int layoutWidth = -1, layoutHeight = -1;
    private RawImage mapImage;
    private Text headingText;
    private Text statusText;
    private Texture2D mapTexture;
    private float nextOpenTime;
    private float previousTimeScale;
    private int areaSize;
    private int areaColumn;
    private int areaRow;
    private int openedFrame;
    private bool isOpen;
    private bool closeRequested;

    private int RemainingStars => platform != null
        ? Mathf.Max(0, platform.TotalStars - platform.PlacedStars)
        : availableStars.Count;

    public string Prompt => Time.realtimeSinceStartup < nextOpenTime
        ? "星星偵測器冷卻中 " + Mathf.CeilToInt(nextOpenTime - Time.realtimeSinceStartup) +
          " 秒 · 尚差 " + RemainingStars + " 顆"
        : "查看星星偵測圖 · 尚差 " + RemainingStars + " 顆";

    public bool CanInteract(GameObject player)
    {
        Level02BoxCarryController interactor = player != null ? player.GetComponent<Level02BoxCarryController>() : null;
        return !isOpen && (platform == null || !platform.IsComplete) &&
            interactor != null && interactor.HeldBox == null;
    }

    public void Interact(GameObject player)
    {
        if (!CanInteract(player) || Time.realtimeSinceStartup < nextOpenTime) return;
        input = PlayerInputService.Instance;
        if (input == null || input.Context != PlayerInputContext.Gameplay) return;
        if (platform == null) platform = FindAnyObjectByType<StarGoalPlate>();
        actor = player;
        SelectAreaForThisOpening();
        BuildView();
        DrawMap();
        previousTimeScale = Time.timeScale;
        isOpen = true;
        openedFrame = Time.frameCount;
        canvas.gameObject.SetActive(true);
        suppressedQuickbar = EcoAdventureRuntime.Instance;
        if (suppressedQuickbar) suppressedQuickbar.SuppressQuickbar(this);
        ResizeView();
        input.UiCancelPressed += Close;
        input.SetContext(PlayerInputContext.UI);
        closeInteract = new InputAction("Level02CloseDetector", InputActionType.Button);
        var interaction = input.Actions.FindAction("Gameplay/Interact", true);
        foreach (var binding in interaction.bindings)
            if (!binding.isComposite && !binding.isPartOfComposite && !string.IsNullOrEmpty(binding.effectivePath))
                closeInteract.AddBinding(binding.effectivePath);
        closeInteract.performed += OnCloseInteract;
        closeInteract.Enable();
        Time.timeScale = 0f;
        PlayerCursor.LockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {
        // Input System callbacks finish before Update; dispose only after the callback returns.
        if (closeRequested) { closeRequested = false; Close(); }
        if (isOpen && (layoutWidth != Screen.width || layoutHeight != Screen.height)) ResizeView();
    }

    private void OnCloseInteract(InputAction.CallbackContext context)
    {
        if (isOpen && Time.frameCount > openedFrame) closeRequested = true;
    }

    private void OnDisable()
    {
        if (isOpen) Close();
    }

    private void OnDestroy()
    {
        if (isOpen) Close();
        if (mapTexture != null) Destroy(mapTexture);
        if (canvas != null) Destroy(canvas.gameObject);
    }

    private void Close()
    {
        if (!isOpen) return;
        closeRequested = false;
        isOpen = false;
        if (suppressedQuickbar) suppressedQuickbar.RestoreQuickbar(this);
        suppressedQuickbar = null;
        if (closeInteract != null) { closeInteract.performed -= OnCloseInteract; closeInteract.Dispose(); closeInteract = null; }
        if (input != null) input.UiCancelPressed -= Close;
        if (canvas != null) canvas.gameObject.SetActive(false);
        Time.timeScale = previousTimeScale;
        if (input != null && input.Context == PlayerInputContext.UI)
            input.SetContext(PlayerInputContext.Gameplay);
        PlayerCursor.LockState = CursorLockMode.Locked;
        Cursor.visible = false;
        nextOpenTime = Time.realtimeSinceStartup + CooldownSeconds;
    }

    private void SelectAreaForThisOpening()
    {
        // Rebuild the available set each time. A box that reached the plate must
        // never keep the previous search window on the next opening.
        FindAvailableStars();
        if (selectedStar == null || !availableStars.Contains(selectedStar))
        {
            if (availableStars.Count == 0)
            {
                selectedStar = null;
                areaSize = 0;
                return;
            }
            selectedStar = availableStars[UnityEngine.Random.Range(0, availableStars.Count)];
            areaSize = 5;
            PlaceArea(false);
            return;
        }

        if (areaSize == 5)
        {
            areaSize = 3;
            PlaceArea(true);
        }
        else if (areaSize == 3 && !AreaContains(selectedStar))
        {
            // A carried star can move; keep its actual position inside the hint.
            PlaceArea(false);
        }
    }

    private void FindAvailableStars()
    {
        availableStars.Clear();
        seenBodies.Clear();
        foreach (Transform node in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (!node.gameObject.activeInHierarchy || !IsStarName(node.name)) continue;
            Rigidbody body = FindBody(node);
            if (body == null || !seenBodies.Add(body)) continue;
            if (!IsInsideRoom(StarGoalPlate.GetBoxCenter(node, body))) continue;
            if (platform != null && platform.IsStarOnPlatform(body)) continue;
            availableStars.Add(body);
        }
    }

    private void PlaceArea(bool nestInPreviousArea)
    {
        Vector3 position = StarGoalPlate.GetBoxCenter(selectedStar.transform, selectedStar);
        int column = WorldColumn(position.x);
        int row = WorldRow(position.z);
        int minColumn = Mathf.Max(0, column - areaSize + 1);
        int maxColumn = Mathf.Min(column, Columns - areaSize);
        int minRow = Mathf.Max(0, row - areaSize + 1);
        int maxRow = Mathf.Min(row, Rows - areaSize);
        if (nestInPreviousArea)
        {
            int nestedMinColumn = Mathf.Max(minColumn, areaColumn);
            int nestedMaxColumn = Mathf.Min(maxColumn, areaColumn + 5 - areaSize);
            int nestedMinRow = Mathf.Max(minRow, areaRow);
            int nestedMaxRow = Mathf.Min(maxRow, areaRow + 5 - areaSize);
            if (nestedMinColumn <= nestedMaxColumn)
            {
                minColumn = nestedMinColumn;
                maxColumn = nestedMaxColumn;
            }
            if (nestedMinRow <= nestedMaxRow)
            {
                minRow = nestedMinRow;
                maxRow = nestedMaxRow;
            }
        }
        areaColumn = UnityEngine.Random.Range(minColumn, maxColumn + 1);
        areaRow = UnityEngine.Random.Range(minRow, maxRow + 1);
    }

    private bool AreaContains(Rigidbody body)
    {
        Vector3 position = StarGoalPlate.GetBoxCenter(body.transform, body);
        int column = WorldColumn(position.x);
        int row = WorldRow(position.z);
        return column >= areaColumn && column < areaColumn + areaSize &&
               row >= areaRow && row < areaRow + areaSize;
    }

    public static bool IsInsideRoom(Vector3 point) => point.x >= MinX && point.x <= MaxX &&
        point.z >= MinZ && point.z <= MaxZ;

    private static int WorldColumn(float x) => Mathf.Clamp(
        Mathf.FloorToInt((x - MinX) / (MaxX - MinX) * Columns), 0, Columns - 1);

    private static int WorldRow(float z) => Mathf.Clamp(
        Mathf.FloorToInt((z - MinZ) / (MaxZ - MinZ) * Rows), 0, Rows - 1);

    private static bool IsStarName(string name) => name == "StarBox" ||
        name.StartsWith("StarBox (", StringComparison.Ordinal);

    private static bool IsBoxName(string name) => IsStarName(name) || name == "NormalBox" ||
        name.StartsWith("NormalBox (", StringComparison.Ordinal);

    private static Rigidbody FindBody(Transform node)
    {
        Rigidbody body = node.GetComponentInParent<Rigidbody>();
        return body != null ? body : node.GetComponentInChildren<Rigidbody>();
    }

    private void BuildView()
    {
        if (canvas != null) return;
        GameObject root = new GameObject("StarDetectorMapCanvas", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        Image backdrop = CreateImage("Backdrop", root.transform, new Color(0.015f, 0.025f, 0.03f, 0.94f));
        Stretch(backdrop.rectTransform);
        Image viewport = CreateImage("MapViewport", backdrop.transform, new Color(0, 0, 0, 0.01f));
        mapViewport = viewport.rectTransform;
        SetCentered(mapViewport, new Vector2(580f, 720f), Vector2.zero);
        viewport.gameObject.AddComponent<RectMask2D>();
        mapScroll = viewport.gameObject.AddComponent<ScrollRect>();
        mapScroll.viewport = mapViewport;
        mapScroll.movementType = ScrollRect.MovementType.Clamped;
        mapScroll.scrollSensitivity = 36f;
        Image panel = CreateImage("MapPanel", viewport.transform, new Color(0.07f, 0.11f, 0.12f, 1f));
        SetCentered(panel.rectTransform, new Vector2(580f, 720f), Vector2.zero);
        mapPanel = panel.rectTransform;
        mapScroll.content = mapPanel;
        headingText = CreateText("Title", panel.transform, "20 × 25 星星箱偵測圖", 24,
            new Vector2(540f, 38f), new Vector2(0f, 329f), TextAnchor.MiddleCenter);
        CreateText("WorldCoordinates", panel.transform,
            "X 0.496 → 24.496    ·    Z 5.021 ↑ 28.482", 16,
            new Vector2(530f, 27f), new Vector2(0f, 295f), TextAnchor.MiddleCenter);

        GameObject mapObject = new GameObject("RoomGrid", typeof(RectTransform), typeof(RawImage));
        mapObject.transform.SetParent(panel.transform, false);
        mapImage = mapObject.GetComponent<RawImage>();
        SetCentered(mapImage.rectTransform, new Vector2(400f, 500f), new Vector2(0f, 17f));
        for (int column = 0; column <= Columns; column += 5)
            CreateText("XGrid" + column, panel.transform, column.ToString(), 14,
                new Vector2(34f, 23f), new Vector2(-200f + column * CellPixels, -247f),
                TextAnchor.MiddleCenter);
        for (int row = 0; row <= Rows; row += 5)
            CreateText("ZGrid" + row, panel.transform, row.ToString(), 14,
                new Vector2(34f, 23f), new Vector2(-226f, -233f + row * CellPixels),
                TextAnchor.MiddleCenter);
        statusText = CreateText("Status", panel.transform, "", 19,
            new Vector2(540f, 28f), new Vector2(0f, -292f), TextAnchor.MiddleCenter);
        CreateText("Instructions", panel.transform,
            "黃色：搜尋範圍　青色：玩家　綠色：平台　紫色：偵測器\n灰色：箱子　橘色：大門　互動鍵 / Esc 關閉 · 冷卻 5 秒", 15,
            new Vector2(550f, 40f), new Vector2(0f, -334f), TextAnchor.MiddleCenter);

        mapTexture = new Texture2D(Columns * CellPixels, Rows * CellPixels,
            TextureFormat.RGBA32, false);
        mapTexture.filterMode = FilterMode.Point;
        mapTexture.wrapMode = TextureWrapMode.Clamp;
        mapImage.texture = mapTexture;
        root.SetActive(false);
    }

    private void ResizeView()
    {
        if (!mapViewport || !mapPanel) return;
        layoutWidth = Screen.width; layoutHeight = Screen.height;
        float availableWidth = Mathf.Max(1f, layoutWidth * 0.9f);
        float availableHeight = Mathf.Max(1f, layoutHeight * 0.9f);
        float fit = Mathf.Min(availableWidth / 580f, availableHeight / 720f);
        // 小視窗維持可讀字級；若仍放不下，使用捲動檢視完整內容。
        float scale = Mathf.Clamp(fit, 0.65f, 1f);
        mapPanel.localScale = Vector3.one * scale;
        mapViewport.sizeDelta = new Vector2(Mathf.Min(availableWidth, 580f * scale),
            Mathf.Min(availableHeight, 720f * scale));
        mapScroll.StopMovement();
        mapPanel.anchoredPosition = Vector2.zero;
    }

    private void DrawMap()
    {
        int width = Columns * CellPixels;
        int height = Rows * CellPixels;
        Color32[] pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            int row = y / CellPixels;
            for (int x = 0; x < width; x++)
            {
                int column = x / CellPixels;
                bool highlighted = selectedStar != null && column >= areaColumn &&
                    column < areaColumn + areaSize && row >= areaRow && row < areaRow + areaSize;
                bool border = highlighted && (column == areaColumn && x % CellPixels < 3 ||
                    column == areaColumn + areaSize - 1 && x % CellPixels >= CellPixels - 3 ||
                    row == areaRow && y % CellPixels < 3 ||
                    row == areaRow + areaSize - 1 && y % CellPixels >= CellPixels - 3);
                bool majorGridLine = x % (5 * CellPixels) < 2 ||
                    y % (5 * CellPixels) < 2 || x == width - 1 || y == height - 1;
                bool gridLine = x % CellPixels == 0 || y % CellPixels == 0;
                bool wall = x < 3 || x >= width - 3 || y < 3 || y >= height - 3;
                byte tile = (byte)(38 + (column * 7 + row * 11) % 4 * 3);
                pixels[y * width + x] = border ? new Color32(252, 207, 55, 255) :
                    wall ? new Color32(182, 197, 189, 255) :
                    majorGridLine ? new Color32(123, 174, 174, 255) :
                    gridLine ? new Color32(82, 124, 123, 255) :
                    highlighted ? new Color32(99, 82, 31, 255) :
                    new Color32((byte)(tile - 12), tile, tile, 255);
            }
        }

        if (platform != null)
        {
            foreach (BoxCollider collider in platform.GetComponents<BoxCollider>())
                if (!collider.isTrigger && collider.enabled)
                {
                    DrawWorldRectangle(pixels, width, height, collider.bounds,
                        new Color32(30, 82, 60, 255), new Color32(73, 226, 137, 255));
                    break;
                }
        }

        // All boxes use one marker color, so the map does not reveal which one is the target.
        seenBodies.Clear();
        foreach (Transform node in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (!node.gameObject.activeInHierarchy || !IsBoxName(node.name)) continue;
            Rigidbody body = FindBody(node);
            if (body == null || !seenBodies.Add(body)) continue;
            DrawBoxMarker(pixels, width, height, StarGoalPlate.GetBoxCenter(node, body));
        }
        SlidingStarGate gate = FindAnyObjectByType<SlidingStarGate>();
        if (gate != null)
            DrawDot(pixels, width, height, gate.transform.position,
                7, new Color32(240, 144, 55, 255));
        DrawDot(pixels, width, height, transform.position,
            6, new Color32(176, 112, 225, 255));
        if (platform != null)
            DrawDot(pixels, width, height, platform.transform.position,
                8, new Color32(65, 224, 130, 255));
        if (actor != null)
        {
            DrawDot(pixels, width, height, actor.transform.position,
                5, new Color32(63, 221, 248, 255));
            for (int step = 6; step <= 17; step += 2)
                DrawDot(pixels, width, height,
                    actor.transform.position + actor.transform.forward * (step * 0.06f),
                    1, new Color32(63, 221, 248, 255));
        }

        mapTexture.SetPixels32(pixels);
        mapTexture.Apply(false);
        headingText.text = "星星箱尚差 " + RemainingStars + " / " +
            (platform != null ? platform.TotalStars : availableStars.Count) + " 顆 · 20 × 25 地圖";
        statusText.text = selectedStar == null
            ? RemainingStars == 0 ? "全部就位 · 關閉地圖後大門會開啟" : "尚有星星箱未被地圖找到"
            : "搜尋 " + areaSize + "×" + areaSize + " 格 · X " + areaColumn + "–" +
            (areaColumn + areaSize) + " / Z " + areaRow + "–" + (areaRow + areaSize);
    }

    private static void DrawWorldRectangle(Color32[] pixels, int width, int height,
        Bounds bounds, Color32 fill, Color32 outline)
    {
        int left = Mathf.Clamp(Mathf.RoundToInt((bounds.min.x - MinX) / (MaxX - MinX) * width), 0, width - 1);
        int right = Mathf.Clamp(Mathf.RoundToInt((bounds.max.x - MinX) / (MaxX - MinX) * width), 0, width - 1);
        int bottom = Mathf.Clamp(Mathf.RoundToInt((bounds.min.z - MinZ) / (MaxZ - MinZ) * height), 0, height - 1);
        int top = Mathf.Clamp(Mathf.RoundToInt((bounds.max.z - MinZ) / (MaxZ - MinZ) * height), 0, height - 1);
        if (right <= left || top <= bottom) return;
        for (int y = bottom; y <= top; y++)
            for (int x = left; x <= right; x++)
            {
                bool edge = x - left < 3 || right - x < 3 || y - bottom < 3 || top - y < 3;
                if (edge) pixels[y * width + x] = outline;
                else if (x % CellPixels > 1 && y % CellPixels > 1)
                    pixels[y * width + x] = fill;
            }
    }

    private static void DrawBoxMarker(Color32[] pixels, int width, int height, Vector3 world)
    {
        if (!IsInsideRoom(world)) return;
        int centerX = WorldPixelX(world.x, width);
        int centerY = WorldPixelY(world.z, height);
        for (int y = Mathf.Max(0, centerY - 4); y <= Mathf.Min(height - 1, centerY + 4); y++)
            for (int x = Mathf.Max(0, centerX - 4); x <= Mathf.Min(width - 1, centerX + 4); x++)
                pixels[y * width + x] = Mathf.Abs(x - centerX) == 4 ||
                    Mathf.Abs(y - centerY) == 4
                    ? new Color32(219, 223, 209, 255)
                    : new Color32(111, 122, 115, 255);
    }

    private static void DrawDot(Color32[] pixels, int width, int height, Vector3 world,
        int radius, Color32 color)
    {
        if (!IsInsideRoom(world)) return;
        int centerX = WorldPixelX(world.x, width);
        int centerY = WorldPixelY(world.z, height);
        for (int y = Mathf.Max(0, centerY - radius); y <= Mathf.Min(height - 1, centerY + radius); y++)
            for (int x = Mathf.Max(0, centerX - radius); x <= Mathf.Min(width - 1, centerX + radius); x++)
                if ((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY) <= radius * radius)
                    pixels[y * width + x] = color;
    }

    private static int WorldPixelX(float x, int width) => Mathf.Clamp(
        Mathf.FloorToInt((x - MinX) / (MaxX - MinX) * width), 0, width - 1);

    private static int WorldPixelY(float z, int height) => Mathf.Clamp(
        Mathf.FloorToInt((z - MinZ) / (MaxZ - MinZ) * height), 0, height - 1);

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static Text CreateText(string name, Transform parent, string content, int size,
        Vector2 dimensions, Vector2 position, TextAnchor alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        Text label = go.GetComponent<Text>();
        label.font = PlayerGuiFont.Load();
        label.text = content;
        label.fontSize = size;
        label.color = Color.white;
        label.alignment = alignment;
        label.raycastTarget = false;
        SetCentered(label.rectTransform, dimensions, position);
        return label;
    }

    private static void SetCentered(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
