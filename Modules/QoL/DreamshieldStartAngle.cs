using System;
using System.Collections;
using System.Linq;
using HutongGames.PlayMaker.Actions;
using Satchel.BetterMenus;
using Satchel.BetterMenus.Config;

namespace GodhomeQoL.Modules.QoL;

public sealed class DreamshieldStartAngle : Module
{
    [LocalSetting]
    [BoolOption]
    public static bool startAngleEnabled = true;

    [LocalSetting]
    [FloatOption(0f, 5f, OptionType.Slider)]
    public static float rotationDelay = 0f;

    [LocalSetting]
    [FloatOption(0.1f, 10f, OptionType.Slider)]
    public static float rotationSpeed = 1f;

    public override ToggleableLevel ToggleableLevel => ToggleableLevel.ChangeScene;
    public override bool Hidden => true;

    private static readonly Dictionary<Rotate, (float x, float y, float z)> rotateOriginals = [];

    private protected override void Load()
    {
        On.PlayMakerFSM.OnEnable += OnFsmEnable;
        USceneManager.activeSceneChanged += OnSceneChanged;
    }

    private protected override void Unload()
    {
        On.PlayMakerFSM.OnEnable -= OnFsmEnable;
        USceneManager.activeSceneChanged -= OnSceneChanged;
    }

    private static void OnFsmEnable(On.PlayMakerFSM.orig_OnEnable orig, PlayMakerFSM self)
    {
        orig(self);

        if (!ShouldHandle(self))
        {
            return;
        }

        _ = HeroController.instance?.StartCoroutine(DelayRotate(self, rotationDelay));
        LogFollowActions(self, "OnEnable");
    }

    private static void OnSceneChanged(Scene previous, Scene next)
    {
        
        foreach (PlayMakerFSM fsm in UObject.FindObjectsOfType<PlayMakerFSM>(true))
        {
            if (!ShouldHandle(fsm))
            {
                continue;
            }

            HeroController.instance?.StartCoroutine(DelayRotate(fsm, rotationDelay));
            LogFollowActions(fsm, "SceneChange");
        }
    }

    private static bool ShouldHandle(PlayMakerFSM fsm)
    {
        if (!startAngleEnabled)
        {
            return false;
        }

        bool isControl = string.Equals(fsm.FsmName, "Control", StringComparison.Ordinal);
        string objName = fsm.gameObject.name ?? string.Empty;
        bool isShield = objName.IndexOf("Orbit Shield", StringComparison.OrdinalIgnoreCase) >= 0
            || objName.IndexOf("Dreamshield", StringComparison.OrdinalIgnoreCase) >= 0
            || objName.IndexOf("Shield", StringComparison.OrdinalIgnoreCase) >= 0;

        return isControl && isShield;
    }

    internal static IEnumerable<Element> MenuElements()
    {
        CustomSlider delaySlider = new(
            "Settings/DreamshieldStartAngle/rotationDelay".Localize(),
            val => rotationDelay = (float)Math.Round(val, 2),
            () => (float)Math.Round(rotationDelay, 2),
            0f,
            10f,
            false
        );
        delaySlider.isVisible = startAngleEnabled;

        CustomSlider speedSlider = new(
            "Settings/DreamshieldStartAngle/rotationSpeed".Localize(),
            val => rotationSpeed = val,
            () => rotationSpeed,
            0.0f,
            10f,
            false
        );
        speedSlider.isVisible = startAngleEnabled;

        Element toggle = Blueprints.HorizontalBoolOption(
            "Modules/DreamshieldStartAngle".Localize(),
            "ToggleableLevel/ChangeScene".Localize(),
            b =>
            {
                startAngleEnabled = b;
                if (b)
                {
                    delaySlider.Show();
                    speedSlider.Show();
                }
                else
                {
                    delaySlider.Hide();
                    speedSlider.Hide();
                }
            },
            () => startAngleEnabled
        );

        delaySlider.OnUpdate += (_, _) => delaySlider.isVisible = startAngleEnabled;
        speedSlider.OnUpdate += (_, _) => speedSlider.isVisible = startAngleEnabled;

        return new Element[] { toggle, delaySlider, speedSlider };
    }

    private static void LogFollowActions(PlayMakerFSM fsm, string tag)
    {
        FsmState? follow = fsm.Fsm.GetState("Follow");
        if (follow?.Actions == null)
        {
            return;
        }

        string types = follow.Actions
            .Select(a => a.GetType().FullName ?? a.GetType().Name)
            .Join(", ");
        LogDebug($"[DreamshieldStartAngle] {tag} Follow actions: {types}");
    }

    private static IEnumerator DelayRotate(PlayMakerFSM fsm, float delaySeconds)
    {
        FsmState? follow = fsm.Fsm.GetState("Follow");
        if (follow?.Actions == null)
        {
            yield break;
        }

        List<FsmStateAction> actions = follow.Actions.ToList();
        List<(int idx, Rotate? rot)> removed = actions
            .Select((a, i) => (idx: i, rot: a as Rotate))
            .Where(t => t.rot != null)
            .ToList();

        if (removed.Count == 0)
        {
            yield break;
        }

        removed.OrderByDescending(t => t.idx).ForEach(t => actions.RemoveAt(t.idx));
        follow.Actions = actions.ToArray();

        if (delaySeconds > 0f)
        {
            yield return new WaitForSeconds(delaySeconds);
        }
        else
        {
            yield return null;
        }

        if (!ShouldHandle(fsm))
        {
            yield break;
        }

        actions = follow.Actions.ToList();
        foreach ((int idx, Rotate? rot) in removed.OrderBy(t => t.idx))
        {
            ApplyRotationSpeed(rot!);
            int insertIdx = Mathf.Clamp(idx, 0, actions.Count);
            actions.Insert(insertIdx, rot!);
        }
        follow.Actions = actions.ToArray();

        
        fsm.Fsm.SetState("Follow");
    }

    private static void ApplyRotationSpeed(Rotate rotate)
    {
        if (!rotateOriginals.TryGetValue(rotate, out (float x, float y, float z) orig))
        {
            orig = (GetFloat(rotate, "xAngle"), GetFloat(rotate, "yAngle"), GetFloat(rotate, "zAngle"));
            rotateOriginals[rotate] = orig;
        }

        SetFloat(rotate, "xAngle", orig.x * rotationSpeed);
        SetFloat(rotate, "yAngle", orig.y * rotationSpeed);
        SetFloat(rotate, "zAngle", orig.z * rotationSpeed);
    }

    private static float GetFloat(object target, string fieldName)
    {
        FieldInfo? fi = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (fi == null)
        {
            return 0f;
        }

        if (fi.GetValue(target) is FsmFloat fsmFloat)
        {
            return fsmFloat.Value;
        }

        if (fi.GetValue(target) is float f)
        {
            return f;
        }

        return 0f;
    }

    private static void SetFloat(object target, string fieldName, float value)
    {
        FieldInfo? fi = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (fi == null)
        {
            return;
        }

        if (fi.GetValue(target) is FsmFloat fsmFloat)
        {
            fsmFloat.Value = value;
        }
        else if (fi.FieldType == typeof(float))
        {
            fi.SetValue(target, value);
        }
    }

}
