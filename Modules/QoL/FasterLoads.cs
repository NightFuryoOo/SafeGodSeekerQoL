using System.Linq;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

namespace GodhomeQoL.Modules.QoL;

public sealed class FasterLoads : Module
{
    private static readonly float[] skipDurations = { 0.4f, 0.165f };

    private ILHook? hook;

    public override bool DefaultEnabled => true;

    public override ToggleableLevel ToggleableLevel => ToggleableLevel.ChangeScene;

    private protected override void Load()
    {
        hook?.Dispose();

        hook = new ILHook(
            typeof(HeroController).GetMethod(nameof(HeroController.EnterScene))!.GetStateMachineTarget(),
            EnterScene
        );
    }

    private protected override void Unload()
    {
        hook?.Dispose();
        hook = null;
    }

    private static void EnterScene(ILContext il)
    {
        ILCursor cursor = new ILCursor(il);
        cursor.Goto(0);

        while (cursor.TryGotoNext(
            i => i.OpCode == OpCodes.Ldc_R4,
            i => i.OpCode == OpCodes.Newobj && i.MatchNewobj<WaitForSeconds>()
        ))
        {
            if (cursor.Instrs[cursor.Index].Operand is not float duration)
            {
                continue;
            }

            if (!skipDurations.Contains(duration))
            {
                continue;
            }

            cursor.Remove();
            cursor.Remove();

            cursor.Emit(OpCodes.Ldnull);
        }
    }
}
