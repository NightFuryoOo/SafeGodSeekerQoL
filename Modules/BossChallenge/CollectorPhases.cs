using System;
using System.Collections.Generic;
using System.Linq;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Satchel;
using Satchel.BetterMenus;
using Satchel.Futils;
using SFCore.Utils;

namespace GodhomeQoL.Modules.CollectorPhases;

public sealed class CollectorPhases : Module
{
    private const int DefaultCollectorHp = 1200;
    private const int DefaultBuzzerHp = 26;
    private const int DefaultRollerHp = 26;
    private const int DefaultSpitterHp = 26;

    [LocalSetting]
    internal static int collectorPhase = 3; // 1: stay in phase 1, 2: stay in phase 2, 3: default

    [LocalSetting]
    internal static bool CollectorImmortal = false;

    [LocalSetting]
    internal static bool IgnoreInitialJarLimit = false;

    [LocalSetting]
    internal static bool DisableSummonLimit = false;

    [LocalSetting]
    internal static int CustomSummonLimit = 20;

    [LocalSetting]
    internal static int collectorMaxHP = DefaultCollectorHp;

    [LocalSetting]
    internal static bool UseMaxHP = false;

    [LocalSetting]
    internal static bool UseCustomPhase2Threshold = false;

    [LocalSetting]
    internal static int CustomPhase2Threshold = 850;

    [LocalSetting]
    internal static int buzzerHP = DefaultBuzzerHp;

    [LocalSetting]
    internal static int rollerHP = DefaultRollerHp;

    [LocalSetting]
    internal static int spitterHP = DefaultSpitterHp;

    [LocalSetting]
    [BoolOption]
    internal static bool spawnBuzzer = true;

    [LocalSetting]
    [BoolOption]
    internal static bool spawnRoller = true;

    [LocalSetting]
    [BoolOption]
    internal static bool spawnSpitter = true;

    public override ToggleableLevel ToggleableLevel => ToggleableLevel.ChangeScene;

    private protected override void Load()
    {
        On.PlayMakerFSM.OnEnable += FsmChanges;
        On.HealthManager.Awake += OnHealthManagerAwake;
        On.HealthManager.Start += OnHealthManagerStart;
        On.HealthManager.Update += OnHealthManagerUpdate;
    }

    private protected override void Unload()
    {
        On.PlayMakerFSM.OnEnable -= FsmChanges;
        On.HealthManager.Awake -= OnHealthManagerAwake;
        On.HealthManager.Start -= OnHealthManagerStart;
        On.HealthManager.Update -= OnHealthManagerUpdate;
    }

    private static void FsmChanges(On.PlayMakerFSM.orig_OnEnable orig, PlayMakerFSM self)
    {
        if (self.gameObject.name == "Jar Collector" && self.FsmName == "Phase Control")
        {
            HandlePhaseControl(self);
        }
        else if (self.gameObject.name == "Jar Collector" && self.FsmName == "Control")
        {
            HandleControl(self);
            SetCollectorSummonHp(self);
            FilterCollectorSummonPool(self);
            ApplySummonCounts(self);
        }
        else
        {
            TryGateSpawnerFSM(self.gameObject);
        }

        orig(self);
    }

    private static void HandlePhaseControl(PlayMakerFSM fsm)
    {
        // Stay in phase 1
        if (collectorPhase == 1)
        {
            fsm.RemoveFsmTransition("Init", "FINISHED");
            return;
        }

        // Force start in phase 2
        if (collectorPhase == 2)
        {
            fsm.ChangeFsmTransition("Init", "FINISHED", "Phase 2");
            return;
        }

        ApplyPhaseThreshold(fsm);
        ApplyPhaseThresholdHook(fsm);
    }

    private static void ApplyPhaseThreshold(PlayMakerFSM fsm)
    {
        if (!UseCustomPhase2Threshold)
        {
            return;
        }

        try
        {
            foreach (FsmState state in fsm.FsmStates)
            {
                if (state?.Actions == null)
                {
                    continue;
                }

                foreach (IntCompare cmp in state.Actions.OfType<IntCompare>())
                {
                    if (cmp.integer2 != null)
                    {
                        cmp.integer2.Value = CustomPhase2Threshold;
                    }
                }
            }
        }
        catch
        {
            // ignore missing state/actions to stay safe on variants
        }
    }

    private static void ApplyPhaseThresholdHook(PlayMakerFSM fsm)
    {
        if (!UseCustomPhase2Threshold)
        {
            return;
        }

        try
        {
            FsmState? check = fsm.Fsm.GetState("Check");
            if (check == null)
            {
                return;
            }

            check.InsertCustomAction(() =>
            {
                foreach (IntCompare cmp in check.Actions.OfType<IntCompare>())
                {
                    if (cmp.integer2 != null)
                    {
                        cmp.integer2.Value = CustomPhase2Threshold;
                    }
                }
            }, 0);
        }
        catch
        {
            // ignore missing state/actions
        }
    }

    private static void OnHealthManagerAwake(On.HealthManager.orig_Awake orig, HealthManager self)
    {
        orig(self);

        if (IsCollector(self))
        {
            ApplyCollectorHealth(self.gameObject, self);
            return;
        }

        TryGateSpawnerFSM(self.gameObject);

        if (!IsCollectorScene(self.gameObject.scene.name))
        {
            return;
        }

        string name = self.gameObject.name;

        if (!spawnBuzzer && IsBuzzerName(name))
        {
            UObject.Destroy(self.gameObject);
            return;
        }

        if (!spawnRoller && IsRollerName(name))
        {
            UObject.Destroy(self.gameObject);
            return;
        }

        if (!spawnSpitter && IsSpitterName(name))
        {
            UObject.Destroy(self.gameObject);
            return;
        }
    }

    private static void OnHealthManagerStart(On.HealthManager.orig_Start orig, HealthManager self)
    {
        orig(self);

        if (IsCollector(self))
        {
            ApplyCollectorHealth(self.gameObject, self);
            _ = self.StartCoroutine(DeferredApply(self));
            return;
        }

        if (IsCollectorScene(self.gameObject.scene.name))
        {
            string name = self.gameObject.name;

            if (!spawnBuzzer && IsBuzzerName(name))
            {
                UObject.Destroy(self.gameObject);
                return;
            }

            if (!spawnRoller && IsRollerName(name))
            {
                UObject.Destroy(self.gameObject);
                return;
            }

            if (!spawnSpitter && IsSpitterName(name))
            {
                UObject.Destroy(self.gameObject);
                return;
            }
        }
    }

    private static void OnHealthManagerUpdate(On.HealthManager.orig_Update orig, HealthManager self)
    {
        orig(self);

        if (!CollectorImmortal || !IsCollector(self))
        {
            return;
        }

        if (self.hp < 100)
        {
            ApplyCollectorHealth(self.gameObject, self);
        }
    }

    private static void HandleControl(PlayMakerFSM fsm)
    {
        ApplyCollectorHealth(fsm.gameObject);
        fsm.AddCustomAction("Init", () => ApplyCollectorHealth(fsm.gameObject));
        GateSpawnStates(fsm);

        // Jar limits
        IntCompare? compare = GetFirstActionOfType<IntCompare>(fsm, "Resummon?");
        if (compare != null)
        {
            compare.integer2.Value = IgnoreInitialJarLimit ? 0 : 3;
        }

        // Immortality toggle
        if (CollectorImmortal)
        {
            fsm.RemoveFsmGlobalTransition("ZERO HP");
        }
    }

    private static bool IsCollector(HealthManager hm)
    {
        if (hm == null)
        {
            return false;
        }

        string scene = hm.gameObject.scene.name;

        return hm.gameObject.name == "Jar Collector"
            && (scene == "GG_Collector" || scene == "GG_Collector_V" || scene.StartsWith("Ruins2_11", StringComparison.Ordinal));
    }

    private static bool IsCollectorScene(string scene) =>
        scene == "GG_Collector" || scene == "GG_Collector_V" || scene.StartsWith("Ruins2_11", StringComparison.Ordinal);

    private static bool IsBuzzerName(string name) =>
        name.IndexOf("Buzzer", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("Mosquito", StringComparison.OrdinalIgnoreCase) >= 0;

    private static bool IsRollerName(string name) =>
        name.IndexOf("Roller", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("Baldur", StringComparison.OrdinalIgnoreCase) >= 0;

    private static bool IsSpitterName(string name) =>
        name.IndexOf("Spitter", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("Aspid", StringComparison.OrdinalIgnoreCase) >= 0;

    private static void ApplyCollectorHealth(GameObject collector, HealthManager? hm = null)
    {
        if (!UseMaxHP)
        {
            return;
        }

        collector.manageHealth(collectorMaxHP);

        hm ??= collector.GetComponent<HealthManager>();
        if (hm != null)
        {
            hm.hp = collectorMaxHP;
            TrySetMaxHp(hm, collectorMaxHP);
        }
    }

    private static IEnumerator DeferredApply(HealthManager hm)
    {
        yield return null;
        ApplyCollectorHealth(hm.gameObject, hm);

        yield return new WaitForSeconds(0.01f);
        ApplyCollectorHealth(hm.gameObject, hm);
    }

    private static void TrySetMaxHp(HealthManager hm, int value)
    {
        try
        {
            ReflectionHelper.SetField(hm, "maxHP", value);
        }
        catch
        {
            // Ignore if field not present in this version.
        }
    }

    private static TAction? GetFirstActionOfType<TAction>(PlayMakerFSM fsm, string state) where TAction : FsmStateAction =>
        fsm.Fsm.GetState(state)
            ?.Actions
            ?.OfType<TAction>()
            .FirstOrDefault();

    #region Menu

    internal static IEnumerable<Element> MenuElements()
    {
        return new Element[]
        {
            new CustomSlider(
                "Settings/CollectorPhases/CollectorPhase".Localize(),
                val =>
                {
                    collectorPhase = (int)val;
                },
                () => collectorPhase,
                1f,
                3f,
                true
            ),
            Blueprints.HorizontalBoolOption(
                "Settings/CollectorPhases/CollectorImmortal".Localize(),
                "",
                b => CollectorImmortal = b,
                () => CollectorImmortal
            ),
            Blueprints.HorizontalBoolOption(
                "Settings/CollectorPhases/IgnoreInitialJarLimit".Localize(),
                "",
                b => IgnoreInitialJarLimit = b,
                () => IgnoreInitialJarLimit
            ),
            Blueprints.HorizontalBoolOption(
                "Settings/CollectorPhases/UseCustomPhase2Threshold".Localize(),
                "",
                b => UseCustomPhase2Threshold = b,
                () => UseCustomPhase2Threshold
            ),
            Blueprints.IntInputField(
                "Settings/CollectorPhases/CustomPhase2Threshold".Localize(),
                val =>
                {
                    CustomPhase2Threshold = Mathf.Clamp(val, 1, 99999);
                    GodhomeQoL.MarkMenuDirty();
                },
                () => CustomPhase2Threshold,
                850,
                "HP",
                6
            ),
            Blueprints.HorizontalBoolOption(
                "Settings/CollectorPhases/UseMaxHP".Localize(),
                "",
                b => UseMaxHP = b,
                () => UseMaxHP
            ),
            Blueprints.IntInputField(
                "Settings/CollectorPhases/CollectorMaxHP".Localize(),
                val =>
                {
                    collectorMaxHP = Mathf.Max(val, 100);
                    GodhomeQoL.MarkMenuDirty();
                },
                () => collectorMaxHP,
                5,
                "HP",
                5
            ),
            Blueprints.IntInputField(
                "Settings/CollectorPhases/BuzzerHP".Localize(),
                val =>
                {
                    buzzerHP = Math.Max(val, 1);
                    GodhomeQoL.MarkMenuDirty();
                },
                () => buzzerHP,
                26,
                "HP",
                5
            ),
            Blueprints.HorizontalBoolOption(
                "Settings/CollectorPhases/SpawnBuzzer".Localize(),
                "",
                b => spawnBuzzer = b,
                () => spawnBuzzer
            ),
            Blueprints.IntInputField(
                "Settings/CollectorPhases/RollerHP".Localize(),
                val =>
                {
                    rollerHP = Math.Max(val, 1);
                    GodhomeQoL.MarkMenuDirty();
                },
                () => rollerHP,
                26,
                "HP",
                5
            ),
            Blueprints.HorizontalBoolOption(
                "Settings/CollectorPhases/SpawnRoller".Localize(),
                "",
                b => spawnRoller = b,
                () => spawnRoller
            ),
            Blueprints.IntInputField(
                "Settings/CollectorPhases/SpitterHP".Localize(),
                val =>
                {
                    spitterHP = Math.Max(val, 1);
                    GodhomeQoL.MarkMenuDirty();
                },
                () => spitterHP,
                26,
                "HP",
                5
            ),
            Blueprints.HorizontalBoolOption(
                "Settings/CollectorPhases/SpawnSpitter".Localize(),
                "",
                b => spawnSpitter = b,
                () => spawnSpitter
            ),
            Blueprints.HorizontalBoolOption(
                "Settings/CollectorPhases/DisableSummonLimit".Localize(),
                "",
                b => DisableSummonLimit = b,
                () => DisableSummonLimit
            ),
            Blueprints.IntInputField(
                "Settings/CollectorPhases/CustomSummonLimit".Localize(),
                val =>
                {
                    CustomSummonLimit = Mathf.Clamp(val, 2, 999);
                    GodhomeQoL.MarkMenuDirty();
                },
                () => CustomSummonLimit,
                20,
                "CNT",
                3
            ),
            new MenuButton(
                "Settings/CollectorPhases/Reset".Localize(),
                "",
                _ =>
                {
                    ResetDefaults();
                    GodhomeQoL.MarkMenuDirty();
                },
                true
            )
        };
    }

        private static void ResetDefaults()
        {
            collectorPhase = 3;
            collectorMaxHP = DefaultCollectorHp;
            UseMaxHP = true;
            UseCustomPhase2Threshold = false;
            CustomPhase2Threshold = 850;
            buzzerHP = DefaultBuzzerHp;
            rollerHP = DefaultRollerHp;
            spitterHP = DefaultSpitterHp;
            spawnBuzzer = true;
            spawnRoller = true;
            spawnSpitter = true;
            CollectorImmortal = false;
            IgnoreInitialJarLimit = false;
            DisableSummonLimit = false;
            CustomSummonLimit = 20;
    }

    private static void ApplySummonCounts(PlayMakerFSM fsm)
    {
        if (!DisableSummonLimit)
        {
            return;
        }

        try
        {
            FsmState? summon = fsm.Fsm.GetState("Summon?");
            FsmState? enemyCount = fsm.Fsm.GetState("Enemy Count");

            if (summon != null)
            {
                foreach (IntCompare cmp in summon.Actions.OfType<IntCompare>())
                {
                    cmp.integer2.Value = CustomSummonLimit;
                }
            }

            if (enemyCount != null)
            {
                foreach (IntCompare cmp in enemyCount.Actions.OfType<IntCompare>())
                {
                    cmp.integer2.Value = CustomSummonLimit;
                }
            }
        }
        catch
        {
            // ignore missing states/actions
        }
    }

    private static void SetCollectorSummonHp(PlayMakerFSM fsm)
    {
        try
        {
            SetIntVariable(fsm, "Buzzer HP", Math.Max(buzzerHP, 1));
            SetIntVariable(fsm, "Roller HP", Math.Max(rollerHP, 1));
            SetIntVariable(fsm, "Spitter HP", Math.Max(spitterHP, 1));
        }
        catch (Exception e)
        {
            LogWarn($"Failed to set collector summon HP: {e.Message}");
        }
    }

    private static void GateSpawnStates(PlayMakerFSM fsm)
    {
        TryGateState(fsm, "Spawn Buzzer", spawnBuzzer);
        TryGateState(fsm, "Spawn Roller", spawnRoller);
        TryGateState(fsm, "Spawn Spitter", spawnSpitter);
    }

    private static void TryGateState(PlayMakerFSM fsm, string stateName, bool enabled)
    {
        if (enabled)
        {
            return;
        }

        try
        {
            fsm.InsertCustomAction(stateName, () => fsm.SendEvent(FsmEvent.Finished.Name), 0);
        }
        catch
        {
            // Ignore if state not found; this prevents hard failures on variant FSMs.
        }
    }

    private static void FilterCollectorSummonPool(PlayMakerFSM fsm)
    {
        try
        {
            FsmInt? count = fsm.FsmVariables.FindFsmInt("Selection Count");
            FsmArray? pool = fsm.FsmVariables.FindFsmArray("Selection Pool");
            if (count == null || pool == null)
            {
                return;
            }

            object[] original = pool.Values ?? Array.Empty<object>();

            List<int> enabledTypes = [];
            if (spawnBuzzer) enabledTypes.Add(0);
            if (spawnRoller) enabledTypes.Add(1);
            if (spawnSpitter) enabledTypes.Add(2);

            if (enabledTypes.Count == 1)
            {
                int only = enabledTypes[0];
                object? candidate = original.FirstOrDefault(o => MatchesType(o, only));
                pool.Values = new object[] { candidate ?? only };
                count.Value = 1;
                return;
            }
            bool Keep(object obj)
            {
                switch (obj)
                {
                    case int i:
                        return i switch
                        {
                            0 => spawnBuzzer,
                            1 => spawnRoller,
                            2 => spawnSpitter,
                            _ => true
                        };
                    case float f:
                        return ((int)f) switch
                        {
                            0 => spawnBuzzer,
                            1 => spawnRoller,
                            2 => spawnSpitter,
                            _ => true
                        };
                    case GameObject go:
                        string n = go.name;
                        if (!spawnBuzzer && IsBuzzerName(n)) return false;
                        if (!spawnRoller && IsRollerName(n)) return false;
                        if (!spawnSpitter && IsSpitterName(n)) return false;
                        return true;
                    case string s when !spawnBuzzer && IsBuzzerName(s):
                        return false;
                    case string s when !spawnRoller && IsRollerName(s):
                        return false;
                    case string s when !spawnSpitter && IsSpitterName(s):
                        return false;
                    default:
                        return true;
                }
            }

            List<object> filtered = original.Where(Keep).ToList();

            if (filtered.Count == 0)
            {
                List<object> fallback = [];
                if (spawnBuzzer) fallback.Add(0);
                if (spawnRoller) fallback.Add(1);
                if (spawnSpitter) fallback.Add(2);

                if (fallback.Count == 0)
                {
                    // Nothing allowed: leave pool empty to avoid spawning.
                    pool.Values = Array.Empty<object>();
                    count.Value = 0;
                    return;
                }

                filtered.AddRange(fallback);
            }

            pool.Values = filtered.ToArray();
            count.Value = filtered.Count;
        }
        catch (Exception e)
        {
            LogWarn($"Failed to filter collector summon pool: {e.Message}");
        }
    }

    private static bool MatchesType(object obj, int typeIndex)
    {
        return obj switch
        {
            int i => i == typeIndex,
            float f => (int)f == typeIndex,
            GameObject go => typeIndex switch
            {
                0 => IsBuzzerName(go.name),
                1 => IsRollerName(go.name),
                2 => IsSpitterName(go.name),
                _ => false
            },
            string s => typeIndex switch
            {
                0 => IsBuzzerName(s),
                1 => IsRollerName(s),
                2 => IsSpitterName(s),
                _ => false
            },
            _ => false
        };
    }

    private static string? GetFirstEnabledState()
    {
        if (spawnBuzzer)
        {
            return "Buzzer";
        }

        if (spawnRoller)
        {
            return "Roller";
        }

        if (spawnSpitter)
        {
            return "Spitter";
        }

        return null;
    }

    private static void TryGateSpawnerFSM(GameObject go)
    {
        if (go == null || !IsCollectorScene(go.scene.name))
        {
            return;
        }

        PlayMakerFSM[] fsms = go.GetComponents<PlayMakerFSM>();
        if (fsms == null || fsms.Length == 0)
        {
            return;
        }

        foreach (PlayMakerFSM fsm in fsms)
        {
            if (fsm == null)
            {
                continue;
            }

            bool hasAny =
                HasState(fsm, "Buzzer") ||
                HasState(fsm, "Roller") ||
                HasState(fsm, "Spitter") ||
                HasState(fsm, "Spawn Buzzer") ||
                HasState(fsm, "Spawn Roller") ||
                HasState(fsm, "Spawn Spitter");

            if (!hasAny)
            {
                continue;
            }

            GateStateIfDisabled(fsm, "Buzzer", spawnBuzzer);
            GateStateIfDisabled(fsm, "Roller", spawnRoller);
            GateStateIfDisabled(fsm, "Spitter", spawnSpitter);

            GateStateIfDisabled(fsm, "Spawn Buzzer", spawnBuzzer);
            GateStateIfDisabled(fsm, "Spawn Roller", spawnRoller);
            GateStateIfDisabled(fsm, "Spawn Spitter", spawnSpitter);
        }
    }

    private static bool HasState(PlayMakerFSM fsm, string stateName) =>
        fsm.FsmStates.Any(s => s?.Name == stateName);

    private static void GateStateIfDisabled(PlayMakerFSM fsm, string stateName, bool enabled)
    {
        if (enabled || !HasState(fsm, stateName))
        {
            return;
        }

        string? fallback = GetFirstEnabledState();
        if (fallback == null)
        {
            // No types enabled; disable jar entirely.
            UObject.Destroy(fsm.gameObject);
            return;
        }

        try
        {
            fsm.InsertCustomAction(stateName, () => fsm.SetState(fallback), 0);
        }
        catch
        {
            // Ignore missing states.
        }
    }

    private static void SetIntVariable(PlayMakerFSM fsm, string name, int value)
    {
        FsmInt? v = fsm.FsmVariables.FindFsmInt(name);
        if (v != null)
        {
            v.Value = value;
        }
        else
        {
            LogWarn($"FSM int variable '{name}' not found on {fsm.gameObject.name}");
        }
    }

    #endregion

}
