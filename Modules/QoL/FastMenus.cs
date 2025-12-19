using System.Reflection;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace GodhomeQoL.Modules.QoL;

public sealed class FastMenus : Module
{
    private static readonly (Type type, string method, ILContext.Manipulator hook)[] ILHooks =
    {
        (typeof(UIManager), nameof(UIManager.HideSaveProfileMenu), DecreaseWait),
        (typeof(UIManager), nameof(UIManager.HideCurrentMenu), DecreaseWait),
        (typeof(UIManager), nameof(UIManager.HideMenu), DecreaseWait),
        (typeof(UIManager), nameof(UIManager.ShowMenu), DecreaseWait),
        (typeof(UIManager), nameof(UIManager.GoToProfileMenu), DecreaseWait),
        (typeof(GameManager), nameof(GameManager.PauseGameToggle), PauseGameToggle),
        (typeof(GameManager), nameof(GameManager.RunContinueGame), RunContinueGame),
        (typeof(SaveSlotButton), "AnimateToSlotState", DecreaseWait),
    };

    private readonly List<ILHook> hooks = new List<ILHook>();

    public override bool DefaultEnabled => true;

    public override ToggleableLevel ToggleableLevel => ToggleableLevel.ChangeScene;

    private protected override void Load()
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        foreach ((Type type, string method, ILContext.Manipulator hook) in ILHooks)
        {
            MethodInfo target = type.GetMethod(method, flags)!;

            hooks.Add(new ILHook(target.GetStateMachineTarget(), hook));
        }

        On.UIManager.FadeInCanvasGroupAlpha += FadeInCanvasGroupAlpha;
        On.UIManager.FadeOutCanvasGroup += FadeOutCanvasGroup;
        On.UIManager.FadeInSprite += FadeInSprite;
        On.UIManager.FadeOutSprite += FadeOutSprite;
        On.UnityEngine.UI.SaveSlotButton.FadeInCanvasGroupAfterDelay += FadeInAfterDelay;
    }

    private protected override void Unload()
    {
        foreach (ILHook hook in hooks)
        {
            hook?.Dispose();
        }

        hooks.Clear();

        On.UIManager.FadeInCanvasGroupAlpha -= FadeInCanvasGroupAlpha;
        On.UIManager.FadeOutCanvasGroup -= FadeOutCanvasGroup;
        On.UIManager.FadeInSprite -= FadeInSprite;
        On.UIManager.FadeOutSprite -= FadeOutSprite;
        On.UnityEngine.UI.SaveSlotButton.FadeInCanvasGroupAfterDelay -= FadeInAfterDelay;
    }

    private static void RunContinueGame(ILContext il)
    {
        ILCursor cursor = new ILCursor(il);
        cursor.Goto(0);

        while (cursor.TryGotoNext(i => i.MatchLdcR4(2.6f)))
        {
            cursor.Next.Operand = 0.05f;
        }
    }

    private static void PauseGameToggle(ILContext il)
    {
        ILCursor cursor = new ILCursor(il);
        cursor.Goto(0);

        while (cursor.TryGotoNext(i => i.MatchLdcR4(out float _)))
        {
            if (Mathf.Abs((float)cursor.Next.Operand - 1f) < Mathf.Epsilon)
            {
                continue;
            }

            cursor.Next.Operand = 0f;
        }
    }

    private static void DecreaseWait(ILContext il)
    {
        ILCursor cursor = new ILCursor(il);
        cursor.Goto(0);

        while (cursor.TryGotoNext(i => i.MatchLdcR4(out _)))
        {
            cursor.Next.Operand = 0f;
        }
    }

    private static IEnumerator FadeOutSprite(On.UIManager.orig_FadeOutSprite orig, UIManager self, SpriteRenderer sprite)
    {
        if (sprite == null)
        {
            yield break;
        }

        sprite.color = new Color(sprite.color.r, sprite.color.g, sprite.color.b, 0f);
    }

    private static IEnumerator FadeInSprite(On.UIManager.orig_FadeInSprite orig, UIManager self, SpriteRenderer sprite)
    {
        if (sprite == null)
        {
            yield break;
        }

        sprite.color = new Color(sprite.color.r, sprite.color.g, sprite.color.b, 1f);
    }

    private static IEnumerator FadeInAfterDelay(On.UnityEngine.UI.SaveSlotButton.orig_FadeInCanvasGroupAfterDelay orig, SaveSlotButton self, float delay, CanvasGroup cg)
    {
        if (cg == null)
        {
            yield break;
        }

        cg.gameObject.SetActive(true);
        cg.alpha = 1f;
        cg.interactable = true;
    }

    private static IEnumerator FadeOutCanvasGroup(On.UIManager.orig_FadeOutCanvasGroup orig, UIManager self, CanvasGroup cg)
    {
        if (cg == null)
        {
            yield break;
        }

        cg.interactable = false;
        cg.alpha = 0f;
        cg.gameObject.SetActive(false);
    }

    private static IEnumerator FadeInCanvasGroupAlpha(On.UIManager.orig_FadeInCanvasGroupAlpha orig, UIManager self, CanvasGroup cg, float end)
    {
        if (cg == null)
        {
            yield break;
        }

        cg.gameObject.SetActive(true);
        cg.alpha = end;
        cg.interactable = true;
    }
}
