using Satchel.BetterMenus;
using GodhomeQoL.Modules;
using GodhomeQoL.Modules.CollectorPhases;
using GodhomeQoL.Modules.QoL;

namespace GodhomeQoL;

public sealed partial class GodhomeQoL : ICustomMenuMod
{
    bool ICustomMenuMod.ToggleButtonInsideMenu => true;

    public MenuScreen GetMenuScreen(MenuScreen modListMenu, ModToggleDelegates? toggleDelegates) =>
        ModMenu.GetMenuScreen(modListMenu, toggleDelegates);

    private static class ModMenu
    {
        private static bool dirty = true;
        private static Menu? menu;

        internal static void MarkDirty() => dirty = true;

        static ModMenu() => On.Language.Language.DoSwitch += (orig, self) =>
        {
            dirty = true;
            orig(self);
        };

        internal static MenuScreen GetMenuScreen(MenuScreen modListMenu, ModToggleDelegates? toggleDelegates)
        {
            if (menu != null && !dirty)
            {
                return menu.GetMenuScreen(modListMenu);
            }

            menu = new Menu("ModName".Localize(), [
                toggleDelegates!.Value.CreateToggle(
                    "ModName".Localize(),
                    "ToggleButtonDesc".Localize()
                )
            ]);

            ModuleManager
                .Modules
                .Values
                .Filter(module =>
                    !module.Hidden
                    && module is not CollectorPhases
                    && module is not FastReload
                    && module.Category != "Bugfix"
                )
                .GroupBy(module => module.Category)
                .OrderBy(group => group.Key)
                .Map(group =>
                {
                    Func<MenuScreen> builder = group.Key switch
                    {
                        nameof(Modules.BossChallenge) => () => BossChallengeMenu(menu!.menuScreen),
                        nameof(Modules.QoL) => () =>
                        {
                            Menu qlMenu = new($"Categories/{group.Key}".Localize(), []);
                            // Base QoL elements
                            group
                                .Filter(ShouldShowInMainList)
                                .Map(module =>
                                    Blueprints.HorizontalBoolOption(
                                        $"Modules/{module.Name}".Localize(),
                                        module.Suppressed
                                            ? string.Format(
                                                "Suppression".Localize(),
                                                module.suppressorMap.Values.Distinct().Join(", ")
                                            )
                                            : $"ToggleableLevel/{module.ToggleableLevel}".Localize(),
                                        val => module.Enabled = val,
                                        () => module.Enabled
                                    )
                                )
                                .ForEach(qlMenu.AddElement);

                            Setting.Global.GetMenuElements(group.Key).ForEach(qlMenu.AddElement);
                            Setting.Local.GetMenuElements(group.Key).ForEach(qlMenu.AddElement);

                            // Add custom submenus; parent will be set after screen creation
                            var deferred = CustomMenuElements(group.Key, () => qlMenu.GetMenuScreen(menu!.menuScreen)).ToList();
                            deferred.ForEach(qlMenu.AddElement);

                            // Build screen once after all elements are in place
                            return qlMenu.GetMenuScreen(menu!.menuScreen);
                        },
                        _ => () => new Menu(
                            $"Categories/{group.Key}".Localize(),
                            [
                                ..group
                                    .Filter(ShouldShowInMainList)
                                    .Map(module =>
                                        Blueprints.HorizontalBoolOption(
                                            $"Modules/{module.Name}".Localize(),
                                            module.Suppressed
                                                ? string.Format(
                                                    "Suppression".Localize(),
                                                    module.suppressorMap.Values.Distinct().Join(", ")
                                                )
                                                : $"ToggleableLevel/{module.ToggleableLevel}".Localize(),
                                            val => module.Enabled = val,
                                            () => module.Enabled
                                        )
                                    ),
                                ..Setting.Global.GetMenuElements(group.Key),
                                ..Setting.Local.GetMenuElements(group.Key),
                                ..CustomMenuElements(group.Key, () => menu!.menuScreen)
                            ]
                        ).GetMenuScreen(menu!.menuScreen)
                    };

                    return Blueprints.NavigateToMenu(
                        $"Categories/{group.Key}".Localize(),
                        "",
                        builder
                    );
                })
                .ForEach(menu.AddElement);

            menu.AddElement(Blueprints.NavigateToMenu(
                "Tools".Localize(),
                "",
                () =>
                {
                    Menu toolsMenu = new("Tools".Localize(), []);
                    MenuScreen? toolsScreen = null;

                    toolsMenu.AddElement(Blueprints.NavigateToMenu("Modules/FastSuperDash".Localize(), "", () => FastSuperDash.GetMenu(toolsScreen ?? menu!.menuScreen)));
                    toolsMenu.AddElement(Blueprints.NavigateToMenu("CollectorPhases".Localize(), "", () => CollectorPhasesMenu.GetMenu(toolsScreen ?? menu!.menuScreen)));
                    toolsMenu.AddElement(Blueprints.NavigateToMenu("FastReload".Localize(), "", () =>
                    {
                        _ = ModuleManager.TryGetModule(typeof(FastReload), out Module? fastReloadModule);

                        Element toggle = Blueprints.HorizontalBoolOption(
                            "Modules/FastReload".Localize(),
                            $"ToggleableLevel/{(fastReloadModule?.ToggleableLevel ?? ToggleableLevel.AnyTime)}".Localize(),
                            val =>
                            {
                                if (fastReloadModule != null)
                                {
                                    fastReloadModule.Enabled = val;
                                }
                            },
                            () => fastReloadModule?.Enabled ?? false
                        );

                        Menu fastReloadMenu = new("FastReload".Localize(), [
                            toggle,
                            ..CustomMenuElements(nameof(FastReload), () => toolsScreen ?? menu!.menuScreen)
                        ]);

                        return fastReloadMenu.GetMenuScreen(toolsScreen ?? menu!.menuScreen);
                    }));
                    toolsMenu.AddElement(Blueprints.NavigateToMenu("DreamshieldSettings".Localize(), "", () => new Menu(
                        "DreamshieldSettings".Localize(),
                        [..CustomMenuElements("Dreamshield", () => toolsScreen ?? menu!.menuScreen)]
                    ).GetMenuScreen(toolsScreen ?? menu!.menuScreen)));
                    toolsMenu.AddElement(Blueprints.NavigateToMenu("TeleportKit".Localize(), "", () => TeleportKitMenu(toolsScreen ?? menu!.menuScreen)));
                    toolsMenu.AddElement(Blueprints.NavigateToMenu("SpeedChanger".Localize(), "", () => Modules.Tools.SpeedChanger.GetMenu(toolsScreen ?? menu!.menuScreen)));

                    toolsScreen = toolsMenu.GetMenuScreen(menu!.menuScreen);

                    return toolsScreen;
                }
            ));

            menu.AddElement(Blueprints.NavigateToMenu(
                "ResetModules".Localize(),
                "",
                () => ResetMenu(menu!.menuScreen)
            ));

            dirty = false;
            return menu.GetMenuScreen(modListMenu);
        }
    }

    private static IEnumerable<Element> CustomMenuElements(string category, Func<MenuScreen> parent)
    {
        List<Element> elements = [];

        if (category == nameof(FastReload))
        {
            elements.Add(FastReload.ReloadBindButton());
        }

        if (category == "CollectorPhases")
        {
            elements.AddRange(CollectorPhases.MenuElements());
        }

        if (category == "Dreamshield")
        {
            elements.AddRange(DreamshieldStartAngle.MenuElements());
        }

        if (category == nameof(Modules.QoL))
        {
            elements.Add(BossAnimationSkipping(parent));
            elements.Add(MenuAnimationSkipping(parent));
        }

        if (category == nameof(Modules.BossChallenge))
        {
            elements.Add(GreyPrinceEnterTypeOption(parent()));
            elements.AddRange(BossChallengeCustom(parent()));
        }

        return elements;
    }

    private static MenuScreen BossChallengeMenu(MenuScreen parent)
    {
        List<Element> elements = [];

        void AddModuleToggle<T>() where T : Module
        {
            _ = ModuleManager.TryGetModule(typeof(T), out Module? module);
            elements.Add(Blueprints.HorizontalBoolOption(
                $"Modules/{typeof(T).Name}".Localize(),
                $"ToggleableLevel/{(module?.ToggleableLevel ?? ToggleableLevel.AnyTime)}".Localize(),
                val =>
                {
                    if (module != null)
                    {
                        module.Enabled = val;
                    }
                },
                () => module?.Enabled ?? false
            ));
        }

        // Infinite Challenge and related options
        AddModuleToggle<Modules.BossChallenge.InfiniteChallenge>();
        elements.Add(Blueprints.HorizontalBoolOption(
            "Settings/restartFightOnSuccess".Localize(),
            "",
            val => Modules.BossChallenge.InfiniteChallenge.restartFightOnSuccess = val,
            () => Modules.BossChallenge.InfiniteChallenge.restartFightOnSuccess
        ));
        elements.Add(Blueprints.HorizontalBoolOption(
            "Settings/restartFightAndMusic".Localize(),
            "",
            val => Modules.BossChallenge.InfiniteChallenge.restartFightAndMusic = val,
            () => Modules.BossChallenge.InfiniteChallenge.restartFightAndMusic
        ));

        // P5 options
        AddModuleToggle<Modules.BossChallenge.P5Health>();
        AddModuleToggle<Modules.BossChallenge.SegmentedP5>();

        // Halve Damage (HoG)
        AddModuleToggle<Modules.BossChallenge.HalveDamageHoGAscendedOrAbove>();
        AddModuleToggle<Modules.BossChallenge.HalveDamageHoGAttuned>();

        // Lifeblood and Soul with manual input
        AddModuleToggle<Modules.BossChallenge.AddLifeblood>();
        elements.Add(Blueprints.IntInputField(
            "Settings/lifebloodAmount".Localize(),
            val =>
            {
                Modules.BossChallenge.AddLifeblood.lifebloodAmount = Math.Max(0, Math.Min(99, val));
                GodhomeQoL.MarkMenuDirty();
            },
            () => Modules.BossChallenge.AddLifeblood.lifebloodAmount,
            0,
            "",
            2
        ));

        AddModuleToggle<Modules.BossChallenge.AddSoul>();
        elements.Add(Blueprints.IntInputField(
            "Settings/soulAmount".Localize(),
            val =>
            {
                Modules.BossChallenge.AddSoul.soulAmount = Math.Max(0, Math.Min(999, val));
                GodhomeQoL.MarkMenuDirty();
            },
            () => Modules.BossChallenge.AddSoul.soulAmount,
            0,
            "",
            3
        ));

        // Grey Prince Zote enter type
        elements.Add(GreyPrinceEnterTypeOption(parent));

        return new Menu(
            "Categories/BossChallenge".Localize(),
            [..elements]
        ).GetMenuScreen(parent);
    }

    private static bool ShouldShowInMainList(Module module)
    {
        // Items moved to dedicated menus
        if (module is FastSuperDash
            || module is TeleportKit
            || module is Modules.QoL.FastDreamWarp
            || module is Modules.QoL.DoorDefaultBegin
            || module is Modules.QoL.MemorizeBindings
            || module is Modules.QoL.FasterLoads
            || module is Modules.QoL.FastMenus
            || module is Modules.QoL.FastText
            || module is Modules.QoL.ShortDeathAnimation)
        {
            return false;
        }

        if (module.Category == nameof(Modules.BossChallenge))
        {
            return false;
        }

        if (module is Modules.BossChallenge.ForceGreyPrinceEnterType
            || module is Modules.BossChallenge.AddLifeblood
            || module is Modules.BossChallenge.AddSoul)
        {
            return false;
        }

        return true;
    }

    private static Element MenuAnimationSkipping(Func<MenuScreen> parent)
    {
        List<Element> elements = [];

        void AddModuleToggle<T>() where T : Module
        {
            _ = ModuleManager.TryGetModule(typeof(T), out Module? module);
            elements.Add(Blueprints.HorizontalBoolOption(
                $"Modules/{typeof(T).Name}".Localize(),
                $"ToggleableLevel/{(module?.ToggleableLevel ?? ToggleableLevel.AnyTime)}".Localize(),
                val =>
                {
                    if (module != null)
                    {
                        module.Enabled = val;
                    }
                },
                () => module?.Enabled ?? false
            ));
        }

        AddModuleToggle<Modules.QoL.DoorDefaultBegin>();
        AddModuleToggle<Modules.QoL.MemorizeBindings>();
        AddModuleToggle<Modules.QoL.FasterLoads>();
        AddModuleToggle<Modules.QoL.FastMenus>();
        AddModuleToggle<Modules.QoL.FastText>();

        elements.Add(Blueprints.HorizontalBoolOption(
            "Settings/AutoSkipCinematics".Localize(),
            "",
            b => Modules.QoL.SkipCutscenes.AutoSkipCinematics = b,
            () => Modules.QoL.SkipCutscenes.AutoSkipCinematics
        ));
        elements.Add(Blueprints.HorizontalBoolOption(
            "Settings/AllowSkippingNonskippable".Localize(),
            "",
            b => Modules.QoL.SkipCutscenes.AllowSkippingNonskippable = b,
            () => Modules.QoL.SkipCutscenes.AllowSkippingNonskippable
        ));
        elements.Add(Blueprints.HorizontalBoolOption(
            "Settings/SkipCutscenesWithoutPrompt".Localize(),
            "",
            b => Modules.QoL.SkipCutscenes.SkipCutscenesWithoutPrompt = b,
            () => Modules.QoL.SkipCutscenes.SkipCutscenesWithoutPrompt
        ));

        return Blueprints.NavigateToMenu(
            "Categories/MenuAnimationSkipping".Localize(),
            "",
            () => new Menu("Categories/MenuAnimationSkipping".Localize(), [..elements]).GetMenuScreen(parent())
        );
    }

    private static Element BossAnimationSkipping(Func<MenuScreen> parent)
    {
        List<Element> elements = [];

        void AddModuleToggle<T>() where T : Module
        {
            _ = ModuleManager.TryGetModule(typeof(T), out Module? module);
            elements.Add(Blueprints.HorizontalBoolOption(
                $"Modules/{typeof(T).Name}".Localize(),
                $"ToggleableLevel/{(module?.ToggleableLevel ?? ToggleableLevel.AnyTime)}".Localize(),
                val =>
                {
                    if (module != null)
                    {
                        module.Enabled = val;
                    }
                },
                () => module?.Enabled ?? false
            ));
        }

        AddModuleToggle<Modules.QoL.FastDreamWarp>();
        AddModuleToggle<Modules.QoL.ShortDeathAnimation>();

        elements.Add(Blueprints.HorizontalBoolOption(
            "Settings/HallOfGodsStatues".Localize(),
            "",
            b => Modules.QoL.SkipCutscenes.HallOfGodsStatues = b,
            () => Modules.QoL.SkipCutscenes.HallOfGodsStatues
        ));
        elements.Add(Blueprints.HorizontalBoolOption(
            "Settings/AbsoluteRadiance".Localize(),
            "",
            b => Modules.QoL.SkipCutscenes.AbsoluteRadiance = b,
            () => Modules.QoL.SkipCutscenes.AbsoluteRadiance
        ));
        elements.Add(Blueprints.HorizontalBoolOption(
            "Settings/PureVesselRoar".Localize(),
            "",
            b => Modules.QoL.SkipCutscenes.PureVesselRoar = b,
            () => Modules.QoL.SkipCutscenes.PureVesselRoar
        ));
        elements.Add(Blueprints.HorizontalBoolOption(
            "Settings/GrimmNightmare".Localize(),
            "",
            b => Modules.QoL.SkipCutscenes.GrimmNightmare = b,
            () => Modules.QoL.SkipCutscenes.GrimmNightmare
        ));
        elements.Add(Blueprints.HorizontalBoolOption(
            "Settings/GreyPrinceZote".Localize(),
            "",
            b => Modules.QoL.SkipCutscenes.GreyPrinceZote = b,
            () => Modules.QoL.SkipCutscenes.GreyPrinceZote
        ));
        elements.Add(Blueprints.HorizontalBoolOption(
            "Settings/Collector".Localize(),
            "",
            b => Modules.QoL.SkipCutscenes.Collector = b,
            () => Modules.QoL.SkipCutscenes.Collector
        ));
        elements.Add(Blueprints.HorizontalBoolOption(
            "Settings/SoulMasterPhaseTransitionSkip".Localize(),
            "",
            b => Modules.QoL.SkipCutscenes.SoulMasterPhaseTransitionSkip = b,
            () => Modules.QoL.SkipCutscenes.SoulMasterPhaseTransitionSkip
        ));
        elements.Add(Blueprints.HorizontalBoolOption(
            "Settings/FirstTimeBosses".Localize(),
            "",
            b => Modules.QoL.SkipCutscenes.FirstTimeBosses = b,
            () => Modules.QoL.SkipCutscenes.FirstTimeBosses
        ));

        return Blueprints.NavigateToMenu(
            "Categories/BossAnimationSkipping".Localize(),
            "",
            () => new Menu("Categories/BossAnimationSkipping".Localize(), [..elements]).GetMenuScreen(parent())
        );
    }

    private static IEnumerable<Element> BossChallengeCustom(MenuScreen parent)
    {
        List<Element> elements = [];

        _ = ModuleManager.TryGetModule(typeof(Modules.BossChallenge.AddLifeblood), out Module? lifebloodModule);
        _ = ModuleManager.TryGetModule(typeof(Modules.BossChallenge.AddSoul), out Module? soulModule);

        elements.Add(Blueprints.HorizontalBoolOption(
            "Modules/AddLifeblood".Localize(),
            $"ToggleableLevel/{(lifebloodModule?.ToggleableLevel ?? ToggleableLevel.ChangeScene)}".Localize(),
            val =>
            {
                if (lifebloodModule != null)
                {
                    lifebloodModule.Enabled = val;
                }
            },
            () => lifebloodModule?.Enabled ?? false
        ));
        elements.Add(Blueprints.IntInputField(
            "Settings/lifebloodAmount".Localize(),
            val =>
            {
                Modules.BossChallenge.AddLifeblood.lifebloodAmount = Math.Max(0, Math.Min(99, val));
                GodhomeQoL.MarkMenuDirty();
            },
            () => Modules.BossChallenge.AddLifeblood.lifebloodAmount,
            0,
            "",
            2
        ));

        elements.Add(Blueprints.HorizontalBoolOption(
            "Modules/AddSoul".Localize(),
            $"ToggleableLevel/{(soulModule?.ToggleableLevel ?? ToggleableLevel.ChangeScene)}".Localize(),
            val =>
            {
                if (soulModule != null)
                {
                    soulModule.Enabled = val;
                }
            },
            () => soulModule?.Enabled ?? false
        ));
        elements.Add(Blueprints.IntInputField(
            "Settings/soulAmount".Localize(),
            val =>
            {
                Modules.BossChallenge.AddSoul.soulAmount = Math.Max(0, Math.Min(999, val));
                GodhomeQoL.MarkMenuDirty();
            },
            () => Modules.BossChallenge.AddSoul.soulAmount,
            0,
            "",
            3
        ));

        return elements;
    }

    private static Element GreyPrinceEnterTypeOption(MenuScreen parent)
    {
        _ = ModuleManager.TryGetModule(typeof(Modules.BossChallenge.ForceGreyPrinceEnterType), out Module? module);

        var options = new[]
        {
            new GpzOption("Settings/gpzEnterType/Off", null),
            new GpzOption("Settings/gpzEnterType/Long", Modules.BossChallenge.ForceGreyPrinceEnterType.EnterType.Long),
            new GpzOption("Settings/gpzEnterType/Short", Modules.BossChallenge.ForceGreyPrinceEnterType.EnterType.Short)
        };

        GpzOption Current()
        {
            if (module?.Enabled == true)
            {
                return options.First(o => o.Value == Modules.BossChallenge.ForceGreyPrinceEnterType.gpzEnterType);
            }

            return options[0];
        }

        return Blueprints.GenericHorizontalOption(
            "Modules/ForceGreyPrinceEnterType".Localize(),
            $"ToggleableLevel/{(module?.ToggleableLevel ?? ToggleableLevel.ChangeScene)}".Localize(),
            options,
            obj =>
            {
                if (obj is not GpzOption opt)
                {
                    return;
                }

                if (opt.Value is null)
                {
                    if (module != null)
                    {
                        module.Enabled = false;
                    }
                }
                else
                {
                    Modules.BossChallenge.ForceGreyPrinceEnterType.gpzEnterType = opt.Value.Value;
                    if (module != null)
                    {
                        module.Enabled = true;
                    }
                }
            },
            () => Current()
        );
    }

    private sealed record GpzOption(string Key, Modules.BossChallenge.ForceGreyPrinceEnterType.EnterType? Value) : IFormattable
    {
        public override string ToString() => Key.Localize();
        string IFormattable.ToString(string? format, IFormatProvider? formatProvider) => ToString();
    }

    private static MenuScreen TeleportKitMenu(MenuScreen parent)
    {
        _ = ModuleManager.TryGetModule(typeof(TeleportKit), out Module? module);

        Element toggle = Blueprints.HorizontalBoolOption(
            "Modules/TeleportKit".Localize(),
            $"ToggleableLevel/{(module?.ToggleableLevel ?? ToggleableLevel.AnyTime)}".Localize(),
            val =>
            {
                if (module != null)
                {
                    module.Enabled = val;
                }
            },
            () => module?.Enabled ?? false
        );

        return new Menu(
            "TeleportKit".Localize(),
            [toggle]
        ).GetMenuScreen(parent);
    }

    private static void ResetVisibleOptionsToOff()
    {
        // Disable all non-hidden modules (видимые тумблеры модулей)
        ModuleManager.Modules.Values
            .Filter(m => !m.Hidden)
            .ForEach(m => m.Enabled = false);

        // Видимые булевые опции (не связаны напрямую с Enabled)
        Modules.BossChallenge.InfiniteChallenge.restartFightOnSuccess = false;
        Modules.BossChallenge.InfiniteChallenge.restartFightAndMusic = false;

        Modules.BossChallenge.ForceGreyPrinceEnterType.gpzEnterType = Modules.BossChallenge.ForceGreyPrinceEnterType.EnterType.Off;

        // SkipCutscenes (видимые флаги)
        Modules.QoL.SkipCutscenes.AutoSkipCinematics = false;
        Modules.QoL.SkipCutscenes.AllowSkippingNonskippable = false;
        Modules.QoL.SkipCutscenes.SkipCutscenesWithoutPrompt = false;
        Modules.QoL.SkipCutscenes.HallOfGodsStatues = false;
        Modules.QoL.SkipCutscenes.AbsoluteRadiance = false;
        Modules.QoL.SkipCutscenes.PureVesselRoar = false;
        Modules.QoL.SkipCutscenes.GrimmNightmare = false;
        Modules.QoL.SkipCutscenes.GreyPrinceZote = false;
        Modules.QoL.SkipCutscenes.Collector = false;
        Modules.QoL.SkipCutscenes.SoulMasterPhaseTransitionSkip = false;
        Modules.QoL.SkipCutscenes.FirstTimeBosses = false;

        // FastSuperDash внутренние тумблеры (видимы в меню)
        Modules.QoL.FastSuperDash.instantSuperDash = false;
        Modules.QoL.FastSuperDash.fastSuperDashEverywhere = false;
    }

    private static MenuScreen ResetMenu(MenuScreen parent) =>
        new Menu(
            "ResetModules".Localize(),
            [
                Blueprints.NavigateToMenu(
                    "RESET ALL",
                    "",
                    () => ConfirmResetMenu(parent)
                )
            ]
        ).GetMenuScreen(parent);

    private static MenuScreen ConfirmResetMenu(MenuScreen parent) =>
        new Menu(
            "ResetModules".Localize(),
            [
                new MenuButton(
                    "Yes",
                    "",
                    _ =>
                    {
                        ResetVisibleOptionsToOff();
                        GodhomeQoL.MarkMenuDirty();
                    },
                    true
                ),
                new MenuButton(
                    "No",
                    "",
                    _ => { },
                    true
                )
            ]
        ).GetMenuScreen(parent);
}
