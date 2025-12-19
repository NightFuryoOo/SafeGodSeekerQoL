using Vasi;
using static UnityEngine.UI.GridLayoutGroup;
using Random = UnityEngine.Random;

namespace GodhomeQoL.Modules.QoL
{

    [UsedImplicitly]
    public class SkipCutscenes : Module
    {
        #region Settings

        [GlobalSetting]
        public static bool AbsoluteRadiance = true;

        [GlobalSetting]
        public static bool HallOfGodsStatues = true;

        [GlobalSetting]
        public static bool PureVesselRoar = true;

        [GlobalSetting]
        public static bool GrimmNightmare = true;

        [GlobalSetting]
        public static bool GreyPrinceZote = true;

        [GlobalSetting]
        public static bool Collector = true;

        [GlobalSetting]
        public static bool FirstTimeBosses = true;

        [GlobalSetting]
        public static bool AutoSkipCinematics = true;

        [GlobalSetting]
        public static bool AllowSkippingNonskippable = true;

        [GlobalSetting]
        public static bool SkipCutscenesWithoutPrompt = true;

        [GlobalSetting]
        public static bool SoulMasterPhaseTransitionSkip = true;

        #endregion
        public override bool DefaultEnabled => true;
        public override bool Hidden => true;

        public override ToggleableLevel ToggleableLevel => ToggleableLevel.ChangeScene;

        private static readonly (Func<bool>, Func<Scene, IEnumerator>)[] FSM_SKIPS =
        {
            (() => AbsoluteRadiance, AbsRadSkip),
            (() => PureVesselRoar, HKPrimeSkip),
            (() => GrimmNightmare, GrimmNightmareSkip),
            (() => GreyPrinceZote, GreyPrinceZoteSkip),
            (() => Collector, CollectorSkip),
            (() => HallOfGodsStatues, StatueWait)
        };



        private static readonly string[] PD_BOOLS =
        {
            nameof(PlayerData.unchainedHollowKnight),
            nameof(PlayerData.encounteredMimicSpider),
            nameof(PlayerData.infectedKnightEncountered),
            nameof(PlayerData.mageLordEncountered),
            nameof(PlayerData.mageLordEncountered_2),
        };

        private static readonly HashSet<string> GodhomeHubScenes = new(StringComparer.Ordinal)
        {
            "GG_Workshop",
            "GG_Atrium",
            "GG_Atrium_Roof"
        };

        private static bool suppressAutoSkipForTransition;
        private static bool timeScaleOverrideActive;
        private static float previousTimeScale;

        private protected override void Load()
        {
            On.CinematicSequence.Begin += CinematicBegin;
            On.FadeSequence.Begin += FadeBegin;
            On.AnimatorSequence.Begin += AnimatorBegin;
            On.InputHandler.SetSkipMode += OnSetSkip;
            On.GameManager.BeginSceneTransitionRoutine += OnBeginSceneTransition;
            On.HutongGames.PlayMaker.Actions.EaseColor.OnEnter += FastEaseColor;
            On.GameManager.FadeSceneInWithDelay += NoFade;
            On.GGCheckIfBossScene.OnEnter += MageLordPhaseTransitionSkip;
            ModHooks.NewGameHook += OnNewGame;
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += FsmSkips;
        }

        private protected override void Unload()
        {
            On.CinematicSequence.Begin -= CinematicBegin;
            On.FadeSequence.Begin -= FadeBegin;
            On.AnimatorSequence.Begin -= AnimatorBegin;
            On.InputHandler.SetSkipMode -= OnSetSkip;
            On.GameManager.BeginSceneTransitionRoutine -= OnBeginSceneTransition;
            On.HutongGames.PlayMaker.Actions.EaseColor.OnEnter -= FastEaseColor;
            On.GameManager.FadeSceneInWithDelay -= NoFade;
            On.GGCheckIfBossScene.OnEnter -= MageLordPhaseTransitionSkip;
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= FsmSkips;
            ModHooks.NewGameHook -= OnNewGame;
        }

        private static void OnNewGame()
        {
            if (!FirstTimeBosses)
                return;

            foreach (string @bool in PD_BOOLS)
            {
                PlayerData.instance.SetBool(@bool, true);
            }
        }

        private static IEnumerator NoFade(On.GameManager.orig_FadeSceneInWithDelay orig, GameManager self, float delay) =>
            orig(self, delay);

        private static void FastEaseColor(On.HutongGames.PlayMaker.Actions.EaseColor.orig_OnEnter orig, EaseColor self) =>
            orig(self);

        private static void MageLordPhaseTransitionSkip(On.GGCheckIfBossScene.orig_OnEnter orig, GGCheckIfBossScene self)
        {
            if (
                !SoulMasterPhaseTransitionSkip
                || !self.Owner.transform.name.Contains("Corpse Mage")
                || !self.Fsm.ActiveStateName.Contains("Quick Death?")
                || self.Fsm.ActiveState.Actions[1] is not PlayerDataBoolTest p
            )
            {
                orig(self);
                return;
            }

            self.Fsm.Event(p.isTrue);
        }

        private static void FsmSkips(Scene arg0, Scene arg1)
        {
            var hc = HeroController.instance;

            if (hc == null) return;

            foreach (var (check, coro) in FSM_SKIPS)
            {
                if (check())
                    hc.StartCoroutine(coro(arg1));
            }
        }

        private static IEnumerator StatueWait(Scene arg1)
        {
            if (arg1.name != "GG_Workshop") yield break;

            foreach (PlayMakerFSM fsm in UObject.FindObjectsOfType<PlayMakerFSM>().Where(x => x.FsmName == "GG Boss UI"))
            {
                fsm.GetState("On Left").ChangeTransition("FINISHED", "Dream Box Down");
                fsm.GetState("On Right").ChangeTransition("FINISHED", "Dream Box Down");
                fsm.GetState("Dream Box Down").InsertAction(0, fsm.GetAction<SetPlayerDataString>("Impact"));
            }
        }

        private static IEnumerator HKPrimeSkip(Scene arg1)
        {
            if (arg1.name != "GG_Hollow_Knight") yield break;

            yield return null;

            PlayMakerFSM control = GameObject.Find("HK Prime").LocateMyFSM("Control");

            control.GetState("Init").ChangeTransition("FINISHED", "Intro Roar");

            control.GetAction<Wait>("Intro 2").time = 0.01f;
            control.GetAction<Wait>("Intro 1").time = 0.01f;
            control.GetAction<Wait>("Intro Roar").time = 1f;
        }
        private static IEnumerator GrimmNightmareSkip(Scene arg1)
        {
            if (arg1.name != "GG_Grimm_Nightmare") yield break;

            yield return null;

            PlayMakerFSM control = GameObject.Find("Grimm Control").LocateMyFSM("Control");

            if (control != null)
            {
                control.GetState("Pause").GetAction<Wait>().time.Value = 0.5f;
                control.GetState("Pan Over").GetAction<Wait>().time.Value = 0.5f;
                control.GetState("Eye 1").GetAction<Wait>().time.Value = 0.1f;
                control.GetState("Eye 2").GetAction<Wait>().time.Value = 0.1f;
                control.GetState("Pan Over 2").GetAction<Wait>().time.Value = 0.1f;
                control.GetState("Eye 3").GetAction<Wait>().time.Value = 0.1f;
                control.GetState("Eye 4").GetAction<Wait>().time.Value = 0.1f;
                control.GetState("Silhouette").GetAction<Wait>().time.Value = 0.1f;
                control.GetState("Silhouette 2").GetAction<Wait>().time.Value = 0.1f;
                control.GetState("Title Up").GetAction<Wait>().time.Value = 0.1f;
                control.GetState("Title Up 2").GetAction<Wait>().time.Value = 0.1f;
                control.GetState("Defeated Pause").GetAction<Wait>().time.Value = 0.1f;
                control.GetState("Defeated Start").GetAction<Wait>().time.Value = 0.1f;
                control.GetState("Explode Start").GetAction<Wait>().time.Value = 0.1f;
                control.GetState("Silhouette Up").GetAction<Wait>().time.Value = 0.1f;
                control.GetState("Ash Away").GetAction<Wait>().time.Value = 0.1f;
                control.GetState("Fade").GetAction<Wait>().time.Value = 0.1f;

            }

        }

        private static IEnumerator CollectorSkip(Scene scene)
        {
            if (!scene.name.Contains("GG_Collector")) yield break;

            yield return null;

            PlayMakerFSM control = GameObject.Find("Jar Collector").LocateMyFSM("Control");

            if (control != null)
            {
                control.GetState("Roar").GetAction<Wait>().time.Value = 0.5f;
                control.GetState("Roar").RemoveAction(6);
                control.GetState("Roar").RemoveAction(7);
            }
        }

        private static IEnumerator GreyPrinceZoteSkip(Scene scene)
        {
            if (scene.name != "GG_Grey_Prince_Zote") yield break;

            yield return null;

            PlayMakerFSM control = GameObject.Find("Grey Prince Title").LocateMyFSM("Control");

            if (control != null)
            {
                control.GetState("Get Level").GetAction<Wait>().time.Value = 0.1f;
                control.GetState("Main Title Pause").GetAction<Wait>().time.Value = 0.1f;
                control.GetState("Main Title").GetAction<Wait>().time.Value = 0.5f;
                control.GetState("Extra 1").GetAction<Wait>().time.Value = 0.01f;
                control.GetState("Extra 2").GetAction<Wait>().time.Value = 0.01f;
                control.GetState("Extra 3").GetAction<Wait>().time.Value = 0.01f;
                control.GetState("Extra 4").GetAction<Wait>().time.Value = 0.01f;
                control.GetState("Extra 5").GetAction<Wait>().time.Value = 0.01f;
                control.GetState("Extra 6").GetAction<Wait>().time.Value = 0.01f;
                control.GetState("Extra 7").GetAction<Wait>().time.Value = 0.01f;
                control.GetState("Extra 8").GetAction<Wait>().time.Value = 0.01f;
                control.GetState("Extra 9").GetAction<Wait>().time.Value = 0.01f;
                control.GetState("Extra 10").GetAction<Wait>().time.Value = 0.01f;
                control.GetState("Extra 11").GetAction<Wait>().time.Value = 0.01f;
                control.GetState("Extra 12").GetAction<Wait>().time.Value = 0.01f;
                control.GetState("Extra 13").GetAction<Wait>().time.Value = 0.01f;
            }
        }

        private static IEnumerator AbsRadSkip(Scene arg1)
        {
            if (arg1.name != "GG_Radiance") yield break;

            yield return null;
            try
            {
                PlayMakerFSM control = GameObject.Find("Boss Control").LocateMyFSM("Control");

                UObject.Destroy(GameObject.Find("Sun"));
                UObject.Destroy(GameObject.Find("feather_particles"));

                FsmState setup = Vasi.FsmUtil.GetState(control, "Setup");

                setup.GetAction<Wait>().time = 1.5f;
                setup.RemoveAction<SetPlayerDataBool>();
                
                setup.ChangeTransition("FINISHED", "Appear Boom");

                control.GetAction<Wait>("Title Up").time = 1f;

            }
            catch (Exception ex)
            {
                Log(ex.Message);
            }
        }

        private static IEnumerator OnBeginSceneTransition(On.GameManager.orig_BeginSceneTransitionRoutine orig, GameManager self, GameManager.SceneLoadInfo info) =>
            RunSceneTransition(orig(self, info), info);

        private static IEnumerator RunSceneTransition(IEnumerator routine, GameManager.SceneLoadInfo info)
        {
            bool suppress = ShouldSuppressAutoSkip(info);
            if (suppress)
            {
                suppressAutoSkipForTransition = true;
                _ = GlobalCoroutineExecutor.Start(SuppressTimeScaleDuringReturn(info));
            }

            try
            {
                while (routine.MoveNext())
                {
                    yield return routine.Current;
                }
            }
            finally
            {
                if (suppress)
                {
                    suppressAutoSkipForTransition = false;
                }
            }
        }

        private static bool ShouldSuppressAutoSkip(GameManager.SceneLoadInfo info) =>
            BossSequenceController.IsInSequence
            && !string.IsNullOrEmpty(info.SceneName)
            && GodhomeHubScenes.Contains(info.SceneName);

        private static IEnumerator SuppressTimeScaleDuringReturn(GameManager.SceneLoadInfo info)
        {
            if (timeScaleOverrideActive)
            {
                yield break;
            }

            if (!ShouldSuppressAutoSkip(info))
            {
                yield break;
            }

            if (!global::GodhomeQoL.Modules.Tools.SpeedChanger.TryBeginTimeScaleOverride(1f, out previousTimeScale))
            {
                yield break;
            }

            timeScaleOverrideActive = true;
            try
            {
                yield return new UnityEngine.WaitForSecondsRealtime(3f);
            }
            finally
            {
                global::GodhomeQoL.Modules.Tools.SpeedChanger.EndTimeScaleOverride(previousTimeScale);
                timeScaleOverrideActive = false;
            }
        }

        private static void OnSetSkip(On.InputHandler.orig_SetSkipMode orig, InputHandler self, SkipPromptMode newmode)
        {
            if (suppressAutoSkipForTransition)
            {
                orig(self, newmode);
                return;
            }

            if (AllowSkippingNonskippable && newmode is not (SkipPromptMode.SKIP_INSTANT or SkipPromptMode.SKIP_PROMPT))
            {
                newmode = SkipCutscenesWithoutPrompt ? SkipPromptMode.SKIP_INSTANT : SkipPromptMode.SKIP_PROMPT;
            }
            else if (SkipCutscenesWithoutPrompt && newmode == SkipPromptMode.SKIP_PROMPT)
            {
                newmode = SkipPromptMode.SKIP_INSTANT;
            }

            orig(self, newmode);
        }

        private static void AnimatorBegin(On.AnimatorSequence.orig_Begin orig, AnimatorSequence self)
        {
            if (AutoSkipCinematics && !BossSequenceController.IsInSequence && !suppressAutoSkipForTransition)
                self.Skip();
            else
                orig(self);
        }

        private static void FadeBegin(On.FadeSequence.orig_Begin orig, FadeSequence self)
        {
            if (AutoSkipCinematics && !BossSequenceController.IsInSequence && !suppressAutoSkipForTransition)
                self.Skip();
            else
                orig(self);
        }

        private static void CinematicBegin(On.CinematicSequence.orig_Begin orig, CinematicSequence self)
        {
            if (AutoSkipCinematics && !BossSequenceController.IsInSequence && !suppressAutoSkipForTransition)
                self.Skip();
            else
                orig(self);
        }
    }
}
