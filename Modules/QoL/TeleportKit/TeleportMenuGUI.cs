namespace GodhomeQoL.Modules.QoL;

internal sealed class TeleportMenuGUI : IDisposable
{
    private readonly TeleportKit mod;
    private Texture2D? glowTexture;
    private Texture2D? textGlowTexture;
    private Texture2D? subtleGlowTexture;
    private float menuAlpha;
    private bool wasMenuVisible;
    private GameObject? wrapper;

    private const float FadeInSpeed = 8f;
    private const float FadeOutSpeed = 6f;

    internal TeleportMenuGUI(TeleportKit mod)
    {
        this.mod = mod;
        CreateGlowTexture();
        CreateTextGlowTexture();
        CreateSubtleGlowTexture();
        wrapper = new GameObject("QoL_GUI_Wrapper");
        wrapper.AddComponent<GUIComponent>().Init(this);
        UObject.DontDestroyOnLoad(wrapper);
    }

    public void Dispose()
    {
        if (wrapper != null)
        {
            UObject.Destroy(wrapper);
            wrapper = null;
        }

        if (glowTexture != null)
        {
            UObject.Destroy(glowTexture);
            glowTexture = null;
        }

        if (textGlowTexture != null)
        {
            UObject.Destroy(textGlowTexture);
            textGlowTexture = null;
        }

        if (subtleGlowTexture != null)
        {
            UObject.Destroy(subtleGlowTexture);
            subtleGlowTexture = null;
        }

        menuAlpha = 0f;
        wasMenuVisible = false;
    }

    private void CreateGlowTexture()
    {
        glowTexture = new Texture2D(1, 1);
        glowTexture.SetPixel(0, 0, new Color(1, 1, 1, 0.15f));
        glowTexture.Apply();
    }

    private void CreateTextGlowTexture()
    {
        textGlowTexture = new Texture2D(1, 1);
        textGlowTexture.SetPixel(0, 0, new Color(1, 1, 1, 0.3f));
        textGlowTexture.Apply();
    }

    private void CreateSubtleGlowTexture()
    {
        subtleGlowTexture = new Texture2D(1, 1);
        subtleGlowTexture.SetPixel(0, 0, new Color(1, 1, 1, 0.05f));
        subtleGlowTexture.Apply();
    }

    internal void DrawMenu()
    {
        bool isMenuVisible = mod.Input.ShowMenu && !GameManager.instance.IsGamePaused();

        if (isMenuVisible)
        {
            if (!wasMenuVisible)
            {
                menuAlpha = Mathf.Max(0.1f, menuAlpha);
            }
            menuAlpha = Mathf.Min(1f, menuAlpha + Time.unscaledDeltaTime * FadeInSpeed);
        }
        else
        {
            if (wasMenuVisible)
            {
                menuAlpha = Mathf.Min(0.9f, menuAlpha);
            }
            menuAlpha = Mathf.Max(0f, menuAlpha - Time.unscaledDeltaTime * FadeOutSpeed);
        }

        wasMenuVisible = isMenuVisible;

        if (menuAlpha <= 0f)
        {
            return;
        }

        var originalStyle = GUI.skin.label.fontStyle;
        var originalBoxStyle = GUI.skin.box.fontStyle;

        GUI.skin.label.fontStyle = FontStyle.Bold;
        GUI.skin.box.fontStyle = FontStyle.BoldAndItalic;

        float screenHeight = Screen.height;
        float menuHeight = 390;
        float startY = (screenHeight - menuHeight) / 2;

        GUI.color = new Color(1f, 1f, 1f, menuAlpha);

        DrawGlow(new Rect(5, startY - 5, 610, menuHeight + 10), 5);

        GUI.Box(new Rect(10, startY, 600, menuHeight), $"[Page {mod.Input.CurrentPage}] - {GetPageTitle()}");

        DrawMenuContent(startY);
        DrawKeyBindInfo(startY);
        DrawRebindingPrompts();

        GUI.skin.label.fontStyle = originalStyle;
        GUI.skin.box.fontStyle = originalBoxStyle;
        GUI.color = Color.white;
    }

    private void DrawGlow(Rect rect, int thickness)
    {
        if (glowTexture == null)
        {
            return;
        }

        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), glowTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness), glowTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.y + thickness, thickness, rect.height - thickness * 2), glowTexture);
        GUI.DrawTexture(new Rect(rect.x + rect.width - thickness, rect.y + thickness, thickness, rect.height - thickness * 2), glowTexture);
    }

    private string GetPageTitle() =>
        mod.Input.CurrentPage switch
        {
            1 => "Hall of Gods [1 Floor]",
            2 => "Hall of Gods [2 Floor]",
            3 => "Pantheon's",
            4 => "PoP Segments",
            _ => string.Empty
        };

    private void DrawMenuContent(float startY)
    {
        int yPos = (int)startY + 40;
        int startIndex = (mod.Input.CurrentPage - 1) * 18;

        if (mod.Input.CurrentPage == 3)
        {
            startIndex = 36;
        }
        if (mod.Input.CurrentPage == 4)
        {
            startIndex = 43;
        }

        int endIndex = mod.Input.CurrentPage == 3 ? 43 :
                      mod.Input.CurrentPage == 4 ? 56 :
                      startIndex + 18;

        for (int i = startIndex; i < endIndex && i < mod.Data.Bosses.Length; i++)
        {
            int column = (i - startIndex) / 9;
            int row = (i - startIndex) % 9;
            var boss = mod.Data.Bosses[i];

            if (!string.IsNullOrEmpty(boss.scene))
            {
                Rect textRect = new Rect(20 + column * 280, yPos + row * 22, 260, 20);
                GUI.Label(textRect, $"{boss.id} - {boss.name}");
            }
        }
    }

    private void DrawKeyBindInfo(float startY)
    {
        Rect inputRect = new Rect(20, startY + 270, 560, 20);
        GUI.Label(inputRect, $"Input (1-{mod.Data.Bosses.Length}): {mod.Input.InputBuffer}");

        Rect navRect = new Rect(20, startY + 290, 560, 20);
        GUI.Label(navRect, "Q - Previous Page | E - Next Page | Enter - Confirm | Esc - Close");

        Rect hotkeyRect = new Rect(20, startY + 310, 560, 20);
        GUI.Label(hotkeyRect, $"Current Hotkey: {mod.MenuKey} (Press Ctrl+{mod.MenuKey} to rebind)");

        Rect saveRect = new Rect(20, startY + 330, 560, 20);
        GUI.Label(saveRect, $"Save Teleport Key: {mod.SaveTeleportKey} (Press Ctrl+{mod.SaveTeleportKey} to rebind)");

        Rect teleportRect = new Rect(20, startY + 350, 560, 20);
        GUI.Label(teleportRect, $"Teleport Key: {mod.TeleportKey} (Press Ctrl+{mod.TeleportKey} to rebind)");

        if (mod.Data.CustomTeleportPosition != null)
        {
            Rect customRect = new Rect(20, startY + 370, 560, 20);
            GUI.Label(customRect, $"Custom TP: Set in {mod.Data.CustomTeleportScene}");
        }
    }

    private void DrawRebindingPrompts()
    {
        if (mod.Input.IsRebindingMenuKey)
        {
            DrawCenteredPrompt("Press any key to rebind menu hotkey... (Esc to cancel)");
        }
        else if (mod.Input.IsRebindingSaveKey)
        {
            DrawCenteredPrompt("Press any key to rebind save teleport key... (Esc to cancel)");
        }
        else if (mod.Input.IsRebindingTeleportKey)
        {
            DrawCenteredPrompt("Press any key to rebind teleport key... (Esc to cancel)");
        }
    }

    private void DrawCenteredPrompt(string message)
    {
        GUI.Box(new Rect(Screen.width / 2 - 150, Screen.height / 2 - 25, 300, 50), string.Empty);
        Rect textRect = new Rect(Screen.width / 2 - 140, Screen.height / 2 - 10, 280, 20);
        GUI.Label(textRect, message);
    }

    private sealed class GUIComponent : MonoBehaviour
    {
        private TeleportMenuGUI? menu;

        internal GUIComponent Init(TeleportMenuGUI menu)
        {
            this.menu = menu;
            return this;
        }

        private void OnGUI() => menu?.DrawMenu();
    }
}
