using System;
using C3TreasurePuzzle;
using SDGAdventure.SharedPlayer;
using UnityEngine;
using UnityEngine.UI;

namespace SDGAdventure.Level03GreenPulse
{
    // SDG 7 + SDG 12 optional power calibration micro-game.
    // The original collection, sorting puzzle, production line and exit remain authoritative.
    [DisallowMultipleComponent]
    public sealed class SDGLevel03GreenPulseChallenge : MonoBehaviour
    {
        private enum ScreenState { Closed, Calibrating, Result }
        private const string BestKey = "SDG_C3_GreenPulseBest_v1";
        private const float RailWidth = 610f;
        private static readonly string[] Stages =
        {
            "再生能源匯流", "產線用電平衡", "污染處理優先供電"
        };
        private static readonly string[] Education =
        {
            "SDG 7：善用較潔淨的能源供應。", "SDG 9：讓生產設備減少無效能耗。",
            "SDG 12：確保剩餘污染物得到妥善處理。"
        };
        private static readonly float[] TargetCenters = { 0.27f, 0.73f, 0.43f };
        private static readonly float[] HalfWidths = { 0.12f, 0.10f, 0.085f };
        private static readonly Color Dark = new Color(0.015f, 0.055f, 0.065f, .96f);
        private static readonly Color Cyan = new Color(.30f, .94f, .86f);
        private static readonly Color Green = new Color(.29f, .94f, .45f);
        private static readonly Color Gold = new Color(1.00f, .80f, .34f);

        private C3DeviceState device;
        private C3PuzzleConsole console;
        private Canvas canvas;
        private GameObject panel;
        private Text hint, title, stageText, instruction, status, explanation, bestText;
        private RectTransform needle, zone;
        private readonly Text[] resultDots = new Text[3];
        private GameObject[] lamps;
        private Material[] lampMaterials;
        private AudioSource audioSource;
        private AudioClip tickClip, successClip, missClip;
        private ScreenState screen = ScreenState.Closed;
        private CursorLockMode originalCursorLock;
        private bool originalCursorVisible;
        private int stage, mistakes, score;
        private bool rewardIssued;
        private float needlePhase, holdUntil;
        private string toast = "";
        private float toastUntil;
        private int lastBest = -1;
        private bool initialized;
        private bool lastAvailable;

        private void Start()
        {
            // One lookup on entry; no object searches in Update.
            device = FindObjectOfType<C3DeviceState>();
            console = FindObjectOfType<C3PuzzleConsole>();
            if (device == null || console == null)
            {
                Debug.LogWarning("[SDG C3 GreenPulse] Level03 console/device not found; addon disabled.");
                enabled = false;
                return;
            }
            BuildUI();
            CreateLamps();
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false; audioSource.spatialBlend = 0f; audioSource.volume = 0.18f;
            tickClip = Tone("GreenPulse_Tick", 690f, .085f);
            successClip = Tone("GreenPulse_Success", 880f, .19f);
            missClip = Tone("GreenPulse_Miss", 175f, .16f);
            initialized = true;
            RefreshStatus();
        }

        private void Update()
        {
            if (!initialized || device == null) return;
            bool available = device.FactoryState == C3FactoryState.Running && !device.LevelCompleted;
            if (lastAvailable != available)
            {
                lastAvailable = available;
                if (screen == ScreenState.Closed && string.IsNullOrEmpty(toast)) RefreshStatus();
            }
            if (Input.GetKeyDown(KeyCode.F7))
            {
                if (screen == ScreenState.Closed) TryOpen(); else Close();
                return;
            }
            if (screen != ScreenState.Closed && Input.GetKeyDown(KeyCode.Escape))
            {
                Close(); return;
            }
            if (screen == ScreenState.Closed)
            {
                // Cached Text only changes on state transition or short feedback.
                if (toastUntil > 0 && Time.unscaledTime >= toastUntil)
                { toastUntil = -1; toast = ""; RefreshStatus(); }
            }
            else if (screen == ScreenState.Calibrating)
            {
                if (device.LevelCompleted || Time.timeScale <= 0f)
                { Close(); return; }
                float speed = .58f + stage * .19f;
                needlePhase += Time.unscaledDeltaTime * speed;
                float current = Position();
                needle.anchoredPosition = new Vector2((current - .5f) * RailWidth, 0f);
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) Hit(current);
            }
            else if (screen == ScreenState.Result)
            {
                if (Input.GetKeyDown(KeyCode.R)) BeginRound();
                if (Time.unscaledTime >= holdUntil && Input.GetKeyDown(KeyCode.Space)) Close();
            }
            UpdateLamps();
        }

        private float Position()
        {
            float t = Mathf.Repeat(needlePhase, 2f);
            return t <= 1f ? t : 2f - t;
        }

        private void TryOpen()
        {
            if (!device.IsActivated || device.FactoryState != C3FactoryState.Running)
            {
                Toast("先收集三件零件、解謎並啟動工廠設備，再挑戰綠電校準。", 5f);
                return;
            }
            if (Time.timeScale <= 0f || C3PuzzleConsole.BlocksGameplayInputThisFrame ||
                C3CompletionScreen.BlocksGameplayInputThisFrame) return;
            var input = PlayerInputService.Instance;
            if (input != null && input.Context != PlayerInputContext.Gameplay) return;
            if (PlayerCursor.LockState != CursorLockMode.Locked) return;
            originalCursorLock = PlayerCursor.LockState;
            originalCursorVisible = Cursor.visible;
            PlayerCursor.LockState = CursorLockMode.None;
            Cursor.visible = true;
            panel.SetActive(true);
            BeginRound();
        }

        private void BeginRound()
        {
            screen = ScreenState.Calibrating;
            stage = mistakes = score = 0;
            needlePhase = .02f;
            for (int i = 0; i < resultDots.Length; i++)
                resultDots[i].text = "○";
            title.text = "綠能電網・脈衝校準";
            instruction.text = "觀察能量指針，進入綠色安全區時按【空白鍵 / Enter】\n容許失誤 2 次；F7 或 Esc 可退出，不影響原任務";
            UpdateStage();
        }

        private void UpdateStage()
        {
            stageText.text = string.Format("第 {0}/3 階段  ·  {1}", stage + 1, Stages[stage]);
            explanation.text = Education[stage];
            status.text = string.Format("校準得分 {0}  ·  失誤 {1}/3", score, mistakes);
            float width = RailWidth * HalfWidths[stage] * 2f;
            zone.sizeDelta = new Vector2(width, 54f);
            zone.anchoredPosition = new Vector2((TargetCenters[stage] - .5f) * RailWidth, 0f);
            lastBest = PlayerPrefs.GetInt(BestKey, 0);
            bestText.text = "本機最佳分數 " + lastBest;
        }

        private void Hit(float needlePos)
        {
            float delta = Mathf.Abs(needlePos - TargetCenters[stage]);
            if (delta <= HalfWidths[stage])
            {
                int points = delta <= HalfWidths[stage] * .32f ? 100 : 65;
                score += points;
                resultDots[stage].text = points == 100 ? "◎" : "●";
                resultDots[stage].color = points == 100 ? Gold : Green;
                if (audioSource) audioSource.PlayOneShot(tickClip);
                stage++;
                if (stage == 3) Complete(true);
                else
                {
                    needlePhase = .03f;
                    UpdateStage();
                }
            }
            else
            {
                mistakes++;
                if (audioSource) audioSource.PlayOneShot(missClip);
                if (mistakes >= 3) { Complete(false); return; }
                status.text = string.Format("未命中！剩餘機會 {0}  ·  得分 {1}", 3 - mistakes, score);
            }
        }

        private void Complete(bool won)
        {
            screen = ScreenState.Result;
            holdUntil = Time.unscaledTime + .25f;
            stageText.text = won ? "綠能校準成功！" : "電網失去同步，請再次嘗試";
            instruction.text = "按 R 重新挑戰 ｜ 按 F7 / Esc 返回遊戲";
            if (!won)
            {
                explanation.text = "安全區會隨階段縮小。指針進入綠色範圍時再按空白鍵。";
                return;
            }
            if (audioSource) audioSource.PlayOneShot(successClip);
            if (score > PlayerPrefs.GetInt(BestKey, 0))
            {
                PlayerPrefs.SetInt(BestKey, score); PlayerPrefs.Save();
            }
            bestText.text = "本機最佳分數 " + PlayerPrefs.GetInt(BestKey, 0);
            int restored = 0;
            if (!rewardIssued && EcoAdventureRuntime.Instance != null)
            {
                restored = EcoAdventureRuntime.Instance.AddEnergyFromSalvage("綠電校準", 12);
                if (restored > 0) rewardIssued = true;
            }
            explanation.text = "SDG 7／12：平衡再生能源、產線負載與污染處理。\n本次得分 " + score +
                "／300。" + (restored > 0 ? " 量子武器能量 +" + restored : " 本次紀錄已保存。") +
                "\n（補充能量須持有且裝備量子武器；每次進關最多獎勵一次）";
            status.text = "完成！綠色電力穩定輸出";
        }

        private void Close()
        {
            if (screen == ScreenState.Closed) return;
            screen = ScreenState.Closed;
            if (panel) panel.SetActive(false);
            // Do not fight another menu's input state during scene completion.
            if (device != null && !device.LevelCompleted && !C3CompletionScreen.BlocksGameplayInputThisFrame)
            {
                PlayerCursor.LockState = originalCursorLock;
                Cursor.visible = originalCursorVisible;
            }
            RefreshStatus();
        }

        private void Toast(string message, float duration)
        {
            toast = message;
            toastUntil = Time.unscaledTime + duration;
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (hint == null) return;
            if (!string.IsNullOrEmpty(toast)) hint.text = toast;
            else if (device == null || !device.IsActivated) hint.text = "綠電挑戰  F7  ·  設備啟動後解鎖";
            else if (device.LevelCompleted) hint.text = "綠電挑戰已結束";
            else hint.text = "綠能脈衝校準  F7  ·  選玩挑戰";
        }

        private void BuildUI()
        {
            var root = new GameObject("GreenPulse Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 670;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            RectTransform canvasRect = root.GetComponent<RectTransform>();
            var small = MakeImage("Guide BG", canvasRect, new Vector2(1, 1), new Vector2(1, 1), new Vector2(470, 52), new Vector2(-18, -18), Dark);
            small.raycastTarget = false;
            hint = Label("Guide", canvasRect, "", 19, Cyan, TextAnchor.MiddleCenter,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(458, 45), new Vector2(-24, -20));
            var main = MakeImage("Calibration Window", canvasRect, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(760, 455), Vector2.zero, Dark);
            main.raycastTarget = false;
            panel = main.gameObject;
            RectTransform p = main.rectTransform;
            var accent = MakeImage("Top Accent", p, new Vector2(.5f, 1f), new Vector2(.5f, 1f),
                new Vector2(750, 5), new Vector2(0, -3), Cyan); accent.raycastTarget = false;
            title = Label("Title", p, "", 31, Cyan, TextAnchor.MiddleCenter,
                new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(730, 55), new Vector2(0, -49));
            stageText = Label("Stage", p, "", 24, Color.white, TextAnchor.MiddleCenter,
                new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(720, 38), new Vector2(0, -108));
            var rail = MakeImage("Needle Track", p, new Vector2(.5f, 1f), new Vector2(.5f, .5f),
                new Vector2(RailWidth, 54), new Vector2(0, -177), new Color(.10f, .20f, .22f));
            rail.raycastTarget = false;
            Image target = MakeImage("Green Target", rail.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(110, 54), Vector2.zero, new Color(.13f, .78f, .31f, .93f));
            target.raycastTarget = false; zone = target.rectTransform;
            Image mark = MakeImage("Pulse Needle", rail.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(6, 73), Vector2.zero, Color.white);
            mark.raycastTarget = false; needle = mark.rectTransform;
            for (int i = 0; i < 3; i++)
            {
                resultDots[i] = Label("Step Dot " + i, p, "○", 27, Cyan, TextAnchor.MiddleCenter,
                    new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(65, 40), new Vector2((i - 1) * 72, -241));
            }
            status = Label("Status", p, "", 20, Gold, TextAnchor.MiddleCenter,
                new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(700, 34), new Vector2(0, -280));
            explanation = Label("SDG Education", p, "", 18, Cyan, TextAnchor.MiddleCenter,
                new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(700, 65), new Vector2(0, -337));
            instruction = Label("Help", p, "", 17, Color.white, TextAnchor.MiddleCenter,
                new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(710, 65), new Vector2(0, -396));
            bestText = Label("Best", p, "", 15, new Color(.7f,.83f,.81f), TextAnchor.MiddleRight,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(250, 30), new Vector2(-20, -16));
            panel.SetActive(false);
        }

        private static Image MakeImage(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 size, Vector2 position, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform r = go.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = anchor; r.pivot = pivot; r.sizeDelta = size; r.anchoredPosition = position;
            Image img = go.GetComponent<Image>(); img.color = color; return img;
        }

        private static Text Label(string name, Transform parent, string value, int size, Color color, TextAnchor align,
            Vector2 anchor, Vector2 pivot, Vector2 dimensions, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform r = go.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = anchor; r.pivot = pivot; r.sizeDelta = dimensions; r.anchoredPosition = position;
            Text t = go.GetComponent<Text>();
            t.font = PlayerGuiFont.Load();
            if (!t.font) t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = value; t.fontSize = size; t.color = color; t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private void CreateLamps()
        {
            // Only three small colliders-free visuals; no dynamic lights, physics or GI updates.
            lamps = new GameObject[3]; lampMaterials = new Material[3];
            Vector3 center = console.transform.position + Vector3.up * 1.45f;
            Vector3 x = console.transform.right;
            Shader shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return;
            for (int i = 0; i < 3; i++)
            {
                GameObject bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bulb.name = "GreenPulse EnergyNode " + (i + 1);
                bulb.transform.SetParent(transform, true);
                bulb.transform.position = center + x * ((i - 1) * .5f);
                bulb.transform.localScale = Vector3.one * .22f;
                Collider collider = bulb.GetComponent<Collider>(); if (collider) Destroy(collider);
                Material mat = new Material(shader) { color = Cyan };
                bulb.GetComponent<Renderer>().sharedMaterial = mat;
                lampMaterials[i] = mat; lamps[i] = bulb;
            }
        }

        private void UpdateLamps()
        {
            if (lamps == null || device == null) return;
            bool on = device.IsActivated && !device.LevelCompleted;
            for (int i = 0; i < lamps.Length; i++)
            {
                if (lamps[i] == null) continue;
                if (lamps[i].activeSelf != on) lamps[i].SetActive(on);
                if (!on) continue;
                bool lit = screen == ScreenState.Result && stage >= 3 ||
                           screen == ScreenState.Calibrating && i < stage;
                float pulse = 1f + .13f * Mathf.Sin(Time.unscaledTime * 4f + i);
                lamps[i].transform.localScale = Vector3.one * (.22f * pulse);
                if (lampMaterials[i]) lampMaterials[i].color = lit ? Green : Cyan;
            }
        }

        private static AudioClip Tone(string name, float hz, float seconds)
        {
            const int sampleRate = 22050;
            int count = Mathf.RoundToInt(sampleRate * seconds);
            float[] samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float fade = Mathf.Sin(Mathf.PI * i / count);
                samples[i] = Mathf.Sin(2f * Mathf.PI * hz * i / sampleRate) * fade * .35f;
            }
            AudioClip clip = AudioClip.Create(name, count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnDestroy()
        {
            if (screen != ScreenState.Closed)
            {
                // Avoid leaving unlocked cursor when returning from play mode.
                PlayerCursor.LockState = originalCursorLock;
                Cursor.visible = originalCursorVisible;
            }
            if (lampMaterials != null)
                foreach (Material mat in lampMaterials) if (mat) Destroy(mat);
            if (tickClip) Destroy(tickClip);
            if (successClip) Destroy(successClip);
            if (missClip) Destroy(missClip);
        }
    }
}
