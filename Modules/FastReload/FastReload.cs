using Satchel.BetterMenus;
using GodhomeQoL.Modules.QoL;

namespace GodhomeQoL.Modules;

public sealed class FastReload : Module {
    private const string WorkshopScene = "GG_Workshop";

    private static readonly Vector2[] teleportPoints = [
        new(11f, 36.4f),
        new(207f, 6.4f)
    ];

    [GlobalSetting]
    public static int teleportKeyCode = (int)KeyCode.None;

    [GlobalSetting]
    public static int reloadKeyCode = (int)KeyCode.None;

    private static MenuButton? teleportButton;
    private static MenuButton? reloadButton;

    private static bool waitingForTeleportRebind;
    private static bool waitingForReloadRebind;

    private static KeyCode teleportPrevKey;
    private static KeyCode reloadPrevKey;

    private static RebindListener? listener;
    private static int teleportIndex;
    private static BossSceneController.SetupEventDelegate? storedSetupEvent;

    public override bool DefaultEnabled => true;

    public override ToggleableLevel ToggleableLevel => ToggleableLevel.AnyTime;

    private protected override void Load() {
        ModHooks.HeroUpdateHook += OnHeroUpdate;
        On.BossSceneController.Awake += CaptureSetupEvent;
        EnsureListener();
    }

    private protected override void Unload() {
        ModHooks.HeroUpdateHook -= OnHeroUpdate;
        On.BossSceneController.Awake -= CaptureSetupEvent;
        DisposeListener();

        waitingForTeleportRebind = false;
        waitingForReloadRebind = false;
        storedSetupEvent = null;
    }

    private static void OnHeroUpdate() {
        if (Ref.GM == null || Ref.HC == null) {
            return;
        }

        if (waitingForReloadRebind || waitingForTeleportRebind) {
            return;
        }

        if (ReloadKey != KeyCode.None
            && Input.GetKeyUp(ReloadKey)
            && BossSceneController.IsBossScene
            && Ref.GM.sceneName.StartsWith("GG_", StringComparison.Ordinal)
            && !Ref.GM.IsInSceneTransition
            && Ref.HC.acceptingInput) {
            _ = Ref.HC.StartCoroutine(ReloadCurrentBoss());
        }

        if (TeleportKey != KeyCode.None
            && Input.GetKeyDown(TeleportKey)
            && Ref.GM.sceneName == WorkshopScene
            && Ref.HC.acceptingInput) {
            TeleportAroundWorkshop();
        }
    }

    private static IEnumerator ReloadCurrentBoss() {
        yield return null;

        if (Ref.GM.IsInSceneTransition) {
            yield break;
        }

        string scene = Ref.GM.sceneName;

        if (string.IsNullOrEmpty(scene)
            || scene == WorkshopScene
            || !scene.StartsWith("GG_", StringComparison.Ordinal)) {
            yield break;
        }

        Ref.HC.ClearMP();
        Ref.HC.ClearMPSendEvents();
        Ref.HC.EnterWithoutInput(true);
        Ref.HC.AcceptInput();

        if (storedSetupEvent != null) {
            BossSceneController.SetupEvent = storedSetupEvent;
        }

        if (ModuleManager.TryGetLoadedModule<CarefreeMelodyReset>(out _)) {
            CarefreeMelodyReset.TryResetNow(ignoreBossScene: true);
        }

        Ref.GM.BeginSceneTransition(new GameManager.SceneLoadInfo {
            SceneName = scene,
            EntryGateName = "door_dreamEnter",
            EntryDelay = 0f,
            Visualization = GameManager.SceneLoadVisualizations.GodsAndGlory,
            PreventCameraFadeOut = true
        });

        LogDebug($"FastReload: reloading {scene}");
    }

    private static void TeleportAroundWorkshop() {
        teleportIndex = (teleportIndex + 1) % teleportPoints.Length;

        Ref.HC.transform.SetPosition2D(teleportPoints[teleportIndex]);
        Ref.HC.SetHazardRespawn(Ref.HC.transform.position, true);

        LogDebug($"FastReload: teleported to point {teleportIndex}");
    }

    #region Key handling

    private static KeyCode TeleportKey => (KeyCode)teleportKeyCode;
    private static KeyCode ReloadKey => (KeyCode)reloadKeyCode;

    private static void EnsureListener() {
        if (listener != null) {
            return;
        }

        GameObject go = new("SGQOL_FastReload_RebindListener");
        UObject.DontDestroyOnLoad(go);
        listener = go.AddComponent<RebindListener>();
    }

    private static void DisposeListener() {
        if (listener != null) {
            UObject.Destroy(listener.gameObject);
        }

        listener = null;
    }

    private static void CaptureSetupEvent(On.BossSceneController.orig_Awake orig, BossSceneController self) {
        if (!BossSequenceController.IsInSequence) {
            storedSetupEvent = BossSceneController.SetupEvent;
        }

        orig(self);
    }

    private sealed class RebindListener : MonoBehaviour {
        private void Update() {
            HandleTeleportRebind();
            HandleReloadRebind();
        }
    }

    internal static MenuButton ReloadBindButton() =>
        reloadButton = new MenuButton(
            FormatButtonName("Settings/reloadBossKey".Localize(), ReloadKey),
            "Settings/FastReload/ReloadDesc".Localize(),
            _ => StartReloadRebind(),
            false
        );

    internal static MenuButton TeleportBindButton() =>
        teleportButton = new MenuButton(
            FormatButtonName("Settings/teleportHoGKey".Localize(), TeleportKey),
            "Settings/FastReload/TeleportDesc".Localize(),
            _ => StartTeleportRebind(),
            false
        );

    private static void StartReloadRebind() {
        waitingForReloadRebind = true;
        reloadPrevKey = ReloadKey;
        UpdateReloadButton("Settings/FastReload/SetKey".Localize());
    }

    private static void StartTeleportRebind() {
        waitingForTeleportRebind = true;
        teleportPrevKey = TeleportKey;
        UpdateTeleportButton("Settings/FastReload/SetKey".Localize());
    }

    private static void HandleReloadRebind() {
        if (!waitingForReloadRebind) {
            return;
        }

        foreach (KeyCode key in Enum.GetValues(typeof(KeyCode))) {
            if (!Input.GetKeyDown(key)) {
                continue;
            }

            if (key == KeyCode.Escape) {
                waitingForReloadRebind = false;
                UpdateReloadButton(FormatKeyLabel(ReloadKey));
                return;
            }

            reloadKeyCode = key == reloadPrevKey || IsKeyInUse(key, "reload")
                ? (int)KeyCode.None
                : (int)key;

            waitingForReloadRebind = false;
            UpdateReloadButton(FormatKeyLabel(ReloadKey));
            GodhomeQoL.MarkMenuDirty();
            return;
        }
    }

    private static void HandleTeleportRebind() {
        if (!waitingForTeleportRebind) {
            return;
        }

        foreach (KeyCode key in Enum.GetValues(typeof(KeyCode))) {
            if (!Input.GetKeyDown(key)) {
                continue;
            }

            if (key == KeyCode.Escape) {
                waitingForTeleportRebind = false;
                UpdateTeleportButton(FormatKeyLabel(TeleportKey));
                return;
            }

            teleportKeyCode = key == teleportPrevKey || IsKeyInUse(key, "teleport")
                ? (int)KeyCode.None
                : (int)key;

            waitingForTeleportRebind = false;
            UpdateTeleportButton(FormatKeyLabel(TeleportKey));
            GodhomeQoL.MarkMenuDirty();
            return;
        }
    }

    private static bool IsKeyInUse(KeyCode key, string except) {
        bool inTeleport = !string.Equals(except, "teleport", StringComparison.OrdinalIgnoreCase)
            && TeleportKey == key;
        bool inReload = !string.Equals(except, "reload", StringComparison.OrdinalIgnoreCase)
            && ReloadKey == key;

        return inTeleport || inReload;
    }

    private static void UpdateReloadButton(string value) {
        if (reloadButton == null) {
            return;
        }

        reloadButton.Name = FormatButtonName("Settings/reloadBossKey".Localize(), value);
        reloadButton.Update();
    }

    private static void UpdateTeleportButton(string value) {
        if (teleportButton == null) {
            return;
        }

        teleportButton.Name = FormatButtonName("Settings/teleportHoGKey".Localize(), value);
        teleportButton.Update();
    }

    private static string FormatButtonName(string title, string value) => $"{title}: {value}";

    private static string FormatButtonName(string title, KeyCode key) => $"{title}: {FormatKeyLabel(key)}";

    private static string FormatKeyLabel(KeyCode key) => key == KeyCode.None
        ? "Settings/FastReload/NotSet".Localize()
        : key.ToString();

    #endregion
}
