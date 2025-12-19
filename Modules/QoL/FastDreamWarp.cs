//using GodhomeQoL.Modules.GodseekerMode;
using Satchel;
using Satchel.Futils;
using Osmi.FsmActions;
using GodhomeQoL.Modules.Tools;

namespace GodhomeQoL.Modules.QoL;

public sealed class FastDreamWarp : Module {
	private static readonly GameObjectRef knightRef = new(GameObjectRef.DONT_DESTROY_ON_LOAD, "Knight");
	private static bool timeScaleOverrideInFlight;
	private static float previousTimeScale;

	public override bool DefaultEnabled => true;

	public FastDreamWarp() =>
		On.PlayMakerFSM.Start += ModifyDreamNailFSM;

	private static bool ShouldActivate()
	{
		if (BossSceneController.IsBossScene)
		{
			return true;
		}

		if (Ref.GM != null && PlayerDataR.bossRushMode)
		{
			string scene = Ref.GM.sceneName;
			return scene is "Room_Colosseum_01" or "Room_Colosseum_Bronze" or "Room_Colosseum_Silver" or "Room_Colosseum_Gold";
		}

		return false;
	}

	private void ModifyDreamNailFSM(On.PlayMakerFSM.orig_Start orig, PlayMakerFSM self) {
		orig(self);

		if (self.FsmName == "Dream Nail" && knightRef.MatchGameObject(self.gameObject)) {
			ModifyDreamNailFSM(self);

			LogDebug("Dream Warp FSM modified");
		}
	}

	private void ModifyDreamNailFSM(PlayMakerFSM fsm) {
		fsm.Intercept(new TransitionInterceptor() {
			fromState = "Take Control",
			eventName = FsmEvent.Finished.Name,
			toStateDefault = "Start",
			toStateCustom = "Can Warp?",
			shouldIntercept = () => {
				HeroActions actions = InputHandler.Instance.inputActions;
				return Loaded
					&& true
					&& ShouldActivate()
					&& actions.dreamNail.IsPressed
					&& actions.up.IsPressed;
			},
			onIntercept = (_, _) => BeginTimeScaleNormalization()
		});

		fsm.Intercept(new TransitionInterceptor() {
			fromState = "Warp Charge Start",
			eventName = FsmEvent.Finished.Name,
			toStateDefault = "Warp Charge",
			toStateCustom = "Can Warp?",
			shouldIntercept = () => Loaded && ShouldActivate(),
			onIntercept = (_, _) => BeginTimeScaleNormalization()
		});

		fsm.GetAction("Warp End", 8).Enabled = false;

		fsm.AddAction("Warp End", new InvokePredicate(() => Loaded && ShouldActivate()) {
			trueEvent = FsmEvent.Finished
		});
	}

	private static void BeginTimeScaleNormalization() {
		if (timeScaleOverrideInFlight || Time.timeScale <= 1f) {
			return;
		}

		if (!SpeedChanger.TryBeginTimeScaleOverride(1f, out previousTimeScale)) {
			return;
		}

		timeScaleOverrideInFlight = true;
		_ = GlobalCoroutineExecutor.Start(RestoreTimeScaleAfterWarp());
	}

	private static IEnumerator RestoreTimeScaleAfterWarp() {
		try {
			yield return null;
			yield return new UnityEngine.WaitUntil(() => Ref.GM != null);

			float timeout = 5f;
			while (!Ref.GM.IsInSceneTransition && timeout > 0f) {
				timeout -= Time.unscaledDeltaTime;
				yield return null;
			}

			if (Ref.GM.IsInSceneTransition) {
				yield return new UnityEngine.WaitWhile(() => Ref.GM.IsInSceneTransition);
			}

			yield return new UnityEngine.WaitUntil(() => Ref.GM.gameState == GameState.PLAYING);
		}
		finally {
			SpeedChanger.EndTimeScaleOverride(previousTimeScale);
			timeScaleOverrideInFlight = false;
		}
	}
}
