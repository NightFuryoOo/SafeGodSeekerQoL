using Osmi.FsmActions;
using GodhomeQoL.Modules.Tools;
using Satchel;
using Satchel.Futils;

namespace GodhomeQoL.Modules.QoL;

public sealed class ShortDeathAnimation : Module {
	public override bool DefaultEnabled => true;

	private static bool timeScaleOverrideInFlight;
	private static float previousTimeScale;

	public ShortDeathAnimation() {
		On.HeroController.Start += ModifyHeroDeathFSM;
		ModHooks.TakeHealthHook += NormalizeOnFatalDamage;
	}

	private void ModifyHeroDeathFSM(On.HeroController.orig_Start orig, HeroController self) {
		orig(self);

		PlayMakerFSM? fsm = self.heroDeathPrefab?.LocateMyFSM("Hero Death Anim");
		if (fsm == null) {
			return;
		}

		ModifyHeroDeathFSM(fsm);

		LogDebug("Hero Death FSM modified");
	}

	private int NormalizeOnFatalDamage(int damage) {
		if (!Loaded) {
			return damage;
		}

		if (ShouldNormalizeOnDamage(damage)) {
			BeginTimeScaleNormalization();
			ClearBossReturnFlags();
		}

		return damage;
	}

	private static bool ShouldNormalizeOnDamage(int damage) {
		if (damage <= 0) {
			return false;
		}

		PlayerData pd = PlayerData.instance;
		if (pd == null) {
			return false;
		}

		int totalHealth = pd.health + pd.healthBlue;
		return totalHealth - damage <= 0;
	}

	private static void ClearBossReturnFlags() {
		if (!BossSequenceController.IsInSequence) {
			return;
		}

		try {
			StaticVariableList.SetValue("finishedBossReturning", false);
		}
		catch {
			// ignore if variable missing
		}

		try {
			PlayMakerFSM? fsm = Ref.HC?.gameObject?.LocateMyFSM("Dream Return");
			if (fsm == null) {
				return;
			}

			FsmBool? dreamReturning = fsm.GetVariable<FsmBool>("Dream Returning");
			if (dreamReturning != null) {
				dreamReturning.Value = false;
			}
		}
		catch {
			// ignore if FSM or variable missing
		}
	}

	private void ModifyHeroDeathFSM(PlayMakerFSM fsm) {
		TryInsertTimeNormalization(fsm);

		fsm.AddAction("Bursting",
			new InvokePredicate(() => Loaded && BossSceneController.IsBossScene && !BossSequenceController.IsInSequence)
			{
				trueEvent = FsmEvent.Finished
			}
		);
	}

	private static void TryInsertTimeNormalization(PlayMakerFSM fsm) {
		if (TryInsertAction(fsm, "Init")) {
			return;
		}

		if (TryInsertAction(fsm, "Start")) {
			return;
		}

		_ = TryInsertAction(fsm, "Bursting");
	}

	private static bool TryInsertAction(PlayMakerFSM fsm, string stateName) {
		try {
			fsm.InsertAction(stateName, new InvokeMethod(BeginTimeScaleNormalization), 0);
			return true;
		}
		catch {
			return false;
		}
	}

	private static void BeginTimeScaleNormalization() {
		if (timeScaleOverrideInFlight) {
			return;
		}

		if (Math.Abs(Time.timeScale - 1f) < 0.001f) {
			return;
		}

		if (!SpeedChanger.TryBeginTimeScaleOverride(1f, out previousTimeScale)) {
			return;
		}

		timeScaleOverrideInFlight = true;
		_ = GlobalCoroutineExecutor.Start(RestoreTimeScaleAfterDeath());
	}

	private static IEnumerator RestoreTimeScaleAfterDeath() {
		try {
			yield return null;
			yield return new UnityEngine.WaitUntil(() => Ref.GM != null);

			float elapsed = 0f;
			const float timeout = 20f;

			while (elapsed < timeout) {
				if (Ref.GM == null) {
					break;
				}

				if (Ref.GM.IsInSceneTransition) {
					elapsed += Time.unscaledDeltaTime;
					yield return null;
					continue;
				}

				if (Ref.GM.gameState == GameState.PLAYING && !IsDeathAnimationActive()) {
					break;
				}

				elapsed += Time.unscaledDeltaTime;
				yield return null;
			}

			SpeedChanger.EndTimeScaleOverride(previousTimeScale);
		}
		finally {
			timeScaleOverrideInFlight = false;
		}
	}

	private static bool IsDeathAnimationActive() {
		if (Ref.HC == null) {
			return false;
		}

		GameObject deathPrefab = Ref.HC.heroDeathPrefab;
		return deathPrefab != null && deathPrefab.activeSelf;
	}
}
