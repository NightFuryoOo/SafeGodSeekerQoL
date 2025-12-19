namespace GodhomeQoL.Modules.QoL;

public sealed class TeleportKit : Module
{
    internal static TeleportKit? Instance { get; private set; }

    [GlobalSetting]
    public static KeyCode MenuHotkey = KeyCode.F6;

    [GlobalSetting]
    public static KeyCode SaveTeleportHotkey = KeyCode.R;

    [GlobalSetting]
    public static KeyCode TeleportHotkey = KeyCode.T;

    internal TeleportKitLogger Log { get; private set; } = null!;
    internal TeleportData Data { get; private set; } = null!;
    internal TeleportInputHandler Input { get; private set; } = null!;
    internal TeleportManager Teleport { get; private set; } = null!;
    internal TeleportMenuGUI GUI { get; private set; } = null!;

    internal KeyCode MenuKey => MenuHotkey;
    internal KeyCode SaveTeleportKey => IsValidHotkey(SaveTeleportHotkey) ? SaveTeleportHotkey : KeyCode.R;
    internal KeyCode TeleportKey => IsValidHotkey(TeleportHotkey) ? TeleportHotkey : KeyCode.T;

    public override bool DefaultEnabled => false;
    public override ToggleableLevel ToggleableLevel => ToggleableLevel.AnyTime;

    private static bool IsValidHotkey(KeyCode key) => key != KeyCode.None;

    private protected override void Load()
    {
        Instance = this;

        Log = new TeleportKitLogger();
        Data = new TeleportData();
        Input = new TeleportInputHandler(this);
        Teleport = new TeleportManager(this);
        GUI = new TeleportMenuGUI(this);

        ModHooks.AfterSavegameLoadHook += OnAfterSavegameLoad;

        Log.Write("Teleport kit initialized");
    }

    private protected override void Unload()
    {
        ModHooks.AfterSavegameLoadHook -= OnAfterSavegameLoad;

        GUI?.Dispose();
        Input?.Dispose();
        Teleport?.Dispose();

        Instance = null;
    }

    private void OnAfterSavegameLoad(SaveGameData _)
    {
        Input?.ResetPauseFlag();
    }
}
