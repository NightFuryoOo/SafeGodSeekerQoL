using Satchel;
using Satchel.Futils;
using Satchel.BetterMenus;
using Osmi.FsmActions;
using Satchel.BetterMenus.Config;

namespace GodhomeQoL.Modules.QoL;

public sealed class FastSuperDash : Module {
	private static readonly GameObjectRef knightRef = new(GameObjectRef.DONT_DESTROY_ON_LOAD, "Knight");

	[GlobalSetting]
	[BoolOption]
	public static bool instantSuperDash = true;

	[GlobalSetting]
	[FloatOption(1.0f, 2.0f, 0.1f)]
	public static float fastSuperDashSpeedMultiplier = 1.5f;

	[GlobalSetting]
	[BoolOption]
	public static bool fastSuperDashEverywhere = false;

	public override bool DefaultEnabled => true;

	public FastSuperDash() =>
		On.PlayMakerFSM.Start += ModifySuperDashFSM;

	private void ModifySuperDashFSM(On.PlayMakerFSM.orig_Start orig, PlayMakerFSM self) {
		orig(self);

		if (self.FsmName == "Superdash" && knightRef.MatchGameObject(self.gameObject)) {
			ModifySuperDashFSM(self);

			LogDebug("Superdash FSM modified");
		}
	}

	private void ModifySuperDashFSM(PlayMakerFSM fsm) {
		bool shouldActivate() => Loaded && (
			fastSuperDashEverywhere
			|| Ref.GM.sceneName is "GG_Workshop" or "GG_Atrium" or "GG_Atrium_Roof"
			|| (PlayerDataR.bossRushMode && Ref.GM.sceneName == "Room_Colosseum_01")
		);
		bool shouldRemoveWinding() => shouldActivate() && instantSuperDash;

		FsmEvent skipEvent = FsmEvent.GetFsmEvent("WAIT");

		fsm.AddAction("Wall Charge", new InvokePredicate(shouldRemoveWinding) {
			trueEvent = skipEvent
		});
		fsm.AddAction("Ground Charge", new InvokePredicate(shouldRemoveWinding) {
			trueEvent = skipEvent
		});

		FsmFloat speed = fsm.GetVariable<FsmFloat>("Current SD Speed");
		fsm.Intercept(new TransitionInterceptor() {
			fromState = "Left",
			eventName = FsmEvent.Finished.Name,
			toStateDefault = "Dash Start",
			toStateCustom = "Dash Start",
			shouldIntercept = shouldActivate,
			onIntercept = (_, _) => speed.Value *= fastSuperDashSpeedMultiplier
		});
		fsm.Intercept(new TransitionInterceptor() {
			fromState = "Right",
			eventName = FsmEvent.Finished.Name,
			toStateDefault = "Dash Start",
			toStateCustom = "Dash Start",
			shouldIntercept = shouldActivate,
			onIntercept = (_, _) => speed.Value *= fastSuperDashSpeedMultiplier
		});

		fsm.AddAction("Dashing", new InvokePredicate(shouldRemoveWinding) {
			trueEvent = skipEvent
		});
		fsm.AddAction("Air Cancel", new InvokePredicate(shouldRemoveWinding) {
			trueEvent = FsmEvent.Finished
		});
		fsm.AddAction("Hit Wall", new InvokePredicate(shouldRemoveWinding) {
			trueEvent = FsmEvent.Finished
		});

		
		fsm.AddAction("Wall Charge", new InvokeMethod(() => {
			if (shouldRemoveWinding()) {
				fsm.SendEvent(skipEvent.Name);
			}
		}));
		fsm.AddAction("Ground Charge", new InvokeMethod(() => {
			if (shouldRemoveWinding()) {
				fsm.SendEvent(skipEvent.Name);
			}
		}));
	}

	internal static IEnumerable<Element> MenuElements() {
		_ = ModuleManager.TryGetModule(typeof(FastSuperDash), out Module? module);
		bool menuEnabled = module != null && module.Enabled;

		Element instantToggle = Blueprints.HorizontalBoolOption(
			"Settings/instantSuperDash".Localize(),
			"",
			b => instantSuperDash = b,
			() => instantSuperDash
		);

		Element everywhereToggle = Blueprints.HorizontalBoolOption(
			"Settings/fastSuperDashEverywhere".Localize(),
			"",
			b => fastSuperDashEverywhere = b,
			() => fastSuperDashEverywhere
		);

		CustomSlider speedSlider = new(
			"Settings/fastSuperDashSpeedMultiplier".Localize(),
			val => fastSuperDashSpeedMultiplier = (float) Math.Round(val, 2),
			() => (float) Math.Round(fastSuperDashSpeedMultiplier, 2),
			1f,
			10f,
			false
		);

		Element moduleToggle = Blueprints.HorizontalBoolOption(
			"Modules/FastSuperDash".Localize(),
			$"ToggleableLevel/{ToggleableLevel.ChangeScene}".Localize(),
			val => {
				if (module != null) {
					module.Enabled = val;
				}
			},
			() => module?.Enabled ?? menuEnabled
		);

		
		instantToggle.isVisible = true;
		everywhereToggle.isVisible = true;
		speedSlider.isVisible = true;

		moduleToggle.OnUpdate += (_, _) => { };
		instantToggle.OnUpdate += (_, _) => { };
		everywhereToggle.OnUpdate += (_, _) => { };
		speedSlider.OnUpdate += (_, _) => { };

		return new Element[] { moduleToggle, instantToggle, everywhereToggle, speedSlider };
	}

	internal static MenuScreen GetMenu(MenuScreen parent) {
		Menu m = new("Modules/FastSuperDash".Localize(), [..MenuElements()]);
		return m.GetMenuScreen(parent);
	}
}
