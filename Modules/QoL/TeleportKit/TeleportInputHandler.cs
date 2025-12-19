using UnityEngine.UI;

namespace GodhomeQoL.Modules.QoL;

internal sealed class TeleportInputHandler : IDisposable
{
    private readonly TeleportKit mod;
    private string inputBuffer = string.Empty;
    private int currentPage = 1;
    private bool gameWasPaused;
    private bool pauseEverOpened;
    private static bool pauseHintActive;

    internal TeleportInputHandler(TeleportKit mod)
    {
        this.mod = mod;
        ModHooks.HeroUpdateHook += CheckInput;
        On.HeroController.Pause += OnPause;
    }

    internal bool IsRebindingMenuKey { get; private set; }
    internal bool IsRebindingSaveKey { get; private set; }
    internal bool IsRebindingTeleportKey { get; private set; }
    internal bool ShowMenu { get; set; }
    internal string InputBuffer => inputBuffer;
    internal int CurrentPage => currentPage;

    public void Dispose()
    {
        ModHooks.HeroUpdateHook -= CheckInput;
        On.HeroController.Pause -= OnPause;
        ShowMenu = false;
        inputBuffer = string.Empty;
        IsRebindingMenuKey = false;
        IsRebindingSaveKey = false;
        IsRebindingTeleportKey = false;
    }

    internal void ResetPauseFlag() => pauseEverOpened = false;

    private void CheckInput()
    {
        bool isGamePaused = GameManager.instance.IsGamePaused();

        if (isGamePaused && !gameWasPaused)
        {
            ShowMenu = false;
            inputBuffer = string.Empty;
            mod.Log.Write("Menu closed due to game pause");
        }

        gameWasPaused = isGamePaused;

        if (isGamePaused)
        {
            pauseEverOpened = true;
            return;
        }

        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(mod.MenuKey))
        {
            IsRebindingMenuKey = true;
            mod.Log.Write("Started rebinding menu hotkey");
            return;
        }

        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(mod.SaveTeleportKey))
        {
            IsRebindingSaveKey = true;
            mod.Log.Write("Started rebinding save teleport key");
            return;
        }

        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(mod.TeleportKey))
        {
            IsRebindingTeleportKey = true;
            mod.Log.Write("Started rebinding teleport key");
            return;
        }

        if (HandleRebinds())
        {
            return;
        }

        if (Input.GetKeyDown(mod.MenuKey))
        {
            if (!pauseEverOpened)
            {
                mod.Log.Write("Menu hotkey ignored because pause menu was not opened yet");
                ShowPauseHint();
                return;
            }

            ShowMenu = !ShowMenu;
            inputBuffer = string.Empty;
            currentPage = 1;
            mod.Log.Write($"Menu {(ShowMenu ? "opened" : "closed")}");
        }

        if (ShowMenu)
        {
            HandleMenuNavigation();
            HandleMenuInput();
        }

        if (!ShowMenu && !mod.Teleport.IsBusy)
        {
            if (Input.GetKeyDown(mod.SaveTeleportKey))
            {
                SetCustomTeleportPoint();
            }
            else if (Input.GetKeyDown(mod.TeleportKey))
            {
                TeleportToCustomPoint();
            }
        }
    }

    private bool HandleRebinds()
    {
        if (IsRebindingMenuKey)
        {
            if (TryBindKey(key => TeleportKit.MenuHotkey = key, () => IsRebindingMenuKey = false))
            {
                mod.Log.Write($"Menu hotkey rebound to: {mod.MenuKey}");
            }
            return true;
        }

        if (IsRebindingSaveKey)
        {
            if (TryBindKey(
                key => TeleportKit.SaveTeleportHotkey = key,
                () => IsRebindingSaveKey = false,
                key => key != KeyCode.LeftControl && key != KeyCode.RightControl
            ))
            {
                mod.Log.Write($"Save teleport key rebound to: {mod.SaveTeleportKey}");
            }
            return true;
        }

        if (IsRebindingTeleportKey)
        {
            if (TryBindKey(
                key => TeleportKit.TeleportHotkey = key,
                () => IsRebindingTeleportKey = false,
                key => key != KeyCode.LeftControl && key != KeyCode.RightControl
            ))
            {
                mod.Log.Write($"Teleport key rebound to: {mod.TeleportKey}");
            }
            return true;
        }

        return false;
    }

    private bool TryBindKey(Action<KeyCode> setter, Action onDone, Func<KeyCode, bool>? predicate = null)
    {
        if (!Input.anyKeyDown)
        {
            return false;
        }

        foreach (KeyCode keyCode in Enum.GetValues(typeof(KeyCode)))
        {
            if (!Input.GetKeyDown(keyCode) || keyCode == KeyCode.None)
            {
                continue;
            }

            if (predicate != null && !predicate.Invoke(keyCode))
            {
                continue;
            }

            setter.Invoke(keyCode);
            onDone.Invoke();
            return true;
        }

        return false;
    }

    private void HandleMenuNavigation()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            currentPage--;
            if (currentPage < 1)
            {
                currentPage = 4;
            }
            inputBuffer = string.Empty;
            mod.Log.Write($"Switched to page {currentPage} (left)");
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            currentPage++;
            if (currentPage > 4)
            {
                currentPage = 1;
            }
            inputBuffer = string.Empty;
            mod.Log.Write($"Switched to page {currentPage} (right)");
        }
    }

    private void HandleMenuInput()
    {
        for (int i = 0; i <= 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha0 + i) || Input.GetKeyDown(KeyCode.Keypad0 + i))
            {
                inputBuffer += i.ToString();
                mod.Log.Write($"Input buffer: {inputBuffer}");
            }
        }

        if (Input.GetKeyDown(KeyCode.Return) && inputBuffer.Length > 0)
        {
            if (int.TryParse(inputBuffer, out int bossId))
            {
                var boss = mod.Data.Bosses.FirstOrDefault(b => b.id == bossId);
                if (!string.IsNullOrEmpty(boss.scene))
                {
                    mod.Log.Write($"Attempting teleport to boss ID {bossId} ({boss.name})");
                    mod.Teleport.StartTeleport(boss.position, boss.scene);
                }
                else
                {
                    mod.Log.Write($"Invalid boss ID: {bossId}");
                }
            }
            else if (inputBuffer == "12151920815165")
            {
                mod.Log.Write("Attempting teleport to Dream_Room_Believer_Shrine");
                mod.Teleport.StartTeleport(new Vector3(56.486f, 40.388f, 0f), "Dream_Room_Believer_Shrine");
            }

            inputBuffer = string.Empty;
        }

        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            inputBuffer = inputBuffer.Length > 0
                ? inputBuffer.Substring(0, inputBuffer.Length - 1)
                : string.Empty;
        }
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            ShowMenu = false;
            inputBuffer = string.Empty;
            mod.Log.Write("Menu closed via Escape");
        }
    }

    private void SetCustomTeleportPoint()
    {
        if (HeroController.instance == null)
        {
            return;
        }

        mod.Data.CustomTeleportPosition = HeroController.instance.transform.position;
        mod.Data.CustomTeleportScene = GameManager.instance.sceneName;
        mod.Log.Write($"Custom teleport point set at {mod.Data.CustomTeleportPosition} in {mod.Data.CustomTeleportScene}");
    }

    private void TeleportToCustomPoint()
    {
        if (mod.Data.CustomTeleportPosition == null || string.IsNullOrEmpty(mod.Data.CustomTeleportScene))
        {
            mod.Log.Write("Attempted teleport to custom point but none was set");
            return;
        }

        if (GameManager.instance.sceneName != mod.Data.CustomTeleportScene)
        {
            mod.Log.Write($"Custom teleport point is in another scene ({mod.Data.CustomTeleportScene}), point cleared");
            mod.Data.CustomTeleportPosition = null;
            mod.Data.CustomTeleportScene = null;
            return;
        }

        mod.Log.Write($"Teleporting to custom point at {mod.Data.CustomTeleportPosition} in {mod.Data.CustomTeleportScene}");
        mod.Teleport.StartTeleport(mod.Data.CustomTeleportPosition.Value, mod.Data.CustomTeleportScene);
    }

    private void OnPause(On.HeroController.orig_Pause orig, HeroController self)
    {
        orig(self);
        ShowMenu = false;
        inputBuffer = string.Empty;
        IsRebindingMenuKey = false;
        IsRebindingSaveKey = false;
        IsRebindingTeleportKey = false;
        mod.Log.Write("Game paused, resetting menu state");
    }

    private static void ShowPauseHint()
    {
        if (pauseHintActive || GameManager.instance == null)
        {
            return;
        }

        pauseHintActive = true;
        _ = GameManager.instance.StartCoroutine(PauseHintRoutine());
    }

    private static IEnumerator PauseHintRoutine()
    {
        const float duration = 3f;

        GameObject canvasObj = new("TeleportKitPauseHint");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10000;

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        canvasObj.AddComponent<GraphicRaycaster>();

        GameObject textObj = new("Text");
        textObj.transform.SetParent(canvasObj.transform, false);

        var txt = textObj.AddComponent<UnityEngine.UI.Text>();
        txt.text = "Please, first open the pause menu. Press ESC.";
        txt.fontSize = 26;
        txt.alignment = TextAnchor.LowerCenter;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        txt.color = Color.white;
        txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        RectTransform rt = txt.rectTransform;
        rt.sizeDelta = new Vector2(900, 80);
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 40f);

        float elapsed = 0f;
        Color baseColor = txt.color;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float alpha = 1f - t;
            if (txt != null)
            {
                txt.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (canvasObj != null)
        {
            UObject.Destroy(canvasObj);
        }

        pauseHintActive = false;
    }
}
