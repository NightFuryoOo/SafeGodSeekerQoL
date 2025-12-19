using MonoMod.ModInterop;

using GodhomeQoL.ModInterop;
namespace GodhomeQoL
{
    public sealed partial class GodhomeQoL : Mod, ITogglableMod
    {
        public static GodhomeQoL? Instance { get; private set; }
        public static GodhomeQoL UnsafeInstance => Instance!;

        public static bool Active { get; private set; }

        public override string GetVersion() => ModInfo.Version;
        public override string GetMenuButtonText() =>
        "ModName".Localize() + ' ' + Lang.Get("MAIN_OPTIONS", "MainMenu");
        static GodhomeQoL()
        {
            try
            {
                typeof(Exports).ModInterop();
            }
            catch (Exception e)
            {
                Logger.Log($"Exception during static initialization: {e}");
                
            }
        }
        public GodhomeQoL() : base(ModInfo.Name) => Instance = this;

        internal static void MarkMenuDirty() => ModMenu.MarkDirty();

        public override void Initialize()
        {
            if (Active)
            {
                LogWarn("Attempting to initialize multiple times, operation rejected");
                return;
            }

            Active = true;

            ModuleManager.Load();
        }
        public void Unload()
        {
            ModuleManager.Unload();

            Active = false;
        }

    }
}
