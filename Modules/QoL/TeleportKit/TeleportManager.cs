using GodhomeQoL.Modules.Tools;

namespace GodhomeQoL.Modules.QoL;

internal sealed class TeleportManager : IDisposable
{
    private readonly TeleportKit mod;
    private bool isBusy;
    private static bool pendingUiReset;
    private static readonly HashSet<string> GodhomeHubScenes = new(StringComparer.Ordinal)
    {
        "GG_Workshop",
        "GG_Atrium",
        "GG_Atrium_Roof"
    };

    internal TeleportManager(TeleportKit mod)
    {
        this.mod = mod;
        ModHooks.BeforeSceneLoadHook += OnSceneChange;
    }

    internal bool IsBusy => isBusy;

    public void Dispose()
    {
        ModHooks.BeforeSceneLoadHook -= OnSceneChange;
        isBusy = false;
    }

    internal void StartTeleport(Vector3 position, string scene)
    {
        if (isBusy || GameManager.instance.IsGamePaused())
        {
            return;
        }

        isBusy = true;
        mod.Input.ShowMenu = false;
        mod.Log.Write($"Starting teleport to {position} in {scene}");
        GameManager.instance.StartCoroutine(TeleportToBoss(position, scene));
    }

    private IEnumerator TeleportToBoss(Vector3 targetPos, string scene)
    {
        if (GameManager.instance.IsGamePaused())
        {
            isBusy = false;
            mod.Log.Write("Teleport aborted - game is paused");
            yield break;
        }

        bool timeScaleOverridden = SpeedChanger.TryBeginTimeScaleOverride(1f, out float previousTimeScale);

        try
        {
            mod.Log.Write($"Beginning teleport process to {targetPos} in {scene}");

            if (scene == "GG_Workshop" && !PlayerData.instance.GetBool("godseekerUnlocked"))
            {
                PlayerData.instance.SetBool("godseekerUnlocked", true);
                PlayerData.instance.SetBool("godseekerMet", true);
                mod.Log.Write("Unlocked Godhome access");
            }

            bool isSameScene = GameManager.instance.sceneName == scene;
            bool isDreamRoom = scene == "Dream_Room_Believer_Shrine";
            bool isPOP45 = scene == "White_Palace_06" && targetPos == new Vector3(9.735f, 7.408f, 0f);
            bool isPOP46 = scene == "White_Palace_20" && targetPos == new Vector3(19.5625f, 169.4081f, 0f);
            bool isPantheonI = (scene == "GG_Atrium") && targetPos == new Vector3(97.15343f, 35.40812f, 0f);
            bool isPantheonII = (scene == "GG_Atrium") && targetPos == new Vector3(108.4116f, 35.40812f, 0f);
            bool isPantheonIII = (scene == "GG_Atrium") && targetPos == new Vector3(120.2336f, 35.40812f, 0f);
            bool isPantheonIV = (scene == "GG_Atrium") && targetPos == new Vector3(147.3174f, 35.40812f, 0f);
            bool isPantheonV = (scene == "GG_Atrium_Roof") && targetPos == new Vector3(96.971f, 73.408f, 0f);
            bool isPantheonVSegmented = (scene == "GG_Atrium_Roof") && targetPos == new Vector3(53.81337f, 19.40812f, 0f);
            bool isPantheonBench = (scene == "GG_Atrium_Roof") && targetPos == new Vector3(120.97f, 42.40812f, 0f);
            bool shouldSetHazard = !(isPantheonV || isPantheonVSegmented || isPantheonBench);

            if (!isSameScene)
            {
                HeroController.instance.StopAnimationControl();
                HeroController.instance.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
                HeroController.instance.RegainControl();

                string targetScene = scene;
                Vector3 entryPos = targetPos;
                string entryGate = "left1";

                if (isDreamRoom)
                {
                    targetScene = "Dream_Room_Believer_Shrine";
                    entryPos = targetPos;
                }
                else if (isPOP46)
                {
                    targetScene = "White_Palace_20";
                    entryPos = targetPos;
                    entryGate = "bot1";
                }
                else if (isPantheonI || isPantheonII || isPantheonIII)
                {
                    targetScene = "GG_Atrium";
                    entryPos = targetPos;
                    entryGate = "top1";
                }
                else if (isPantheonIV)
                {
                    targetScene = "GG_Atrium";
                    entryPos = targetPos;
                    entryGate = "top1";
                }
                else if (isPantheonV || isPantheonVSegmented || isPantheonBench)
                {
                    entryGate = "bot1";
                    entryPos = targetPos;
                }
                else if (scene.StartsWith("White_Palace", StringComparison.Ordinal))
                {
                    targetScene = "White_Palace_06";
                    entryPos = new Vector3(0.1111f, 7.408124f, 0f);
                }

                mod.Log.Write($"Loading target scene: {targetScene}");

                if (isPantheonV || isPantheonVSegmented || isPantheonBench || isPantheonIV || isPantheonI || isPantheonII || isPantheonIII)
                {
                    CleanSceneState();
                }

                var loadInfo = new GameManager.SceneLoadInfo
                {
                    SceneName = targetScene,
                    EntryGateName = entryGate,
                    EntryDelay = 0f,
                    WaitForSceneTransitionCameraFade = false,
                    Visualization = GameManager.SceneLoadVisualizations.Default,
                    PreventCameraFadeOut = targetScene.StartsWith("White_Palace", StringComparison.Ordinal) || isDreamRoom
                };

                GameManager.instance.BeginSceneTransition(loadInfo);
                yield return new WaitWhile(() => GameManager.instance.IsInSceneTransition);

                if (targetScene.StartsWith("White_Palace", StringComparison.Ordinal) || isDreamRoom)
                {
                    yield return new WaitForSeconds(0.2f);
                    if (GameManager.instance.cameraCtrl != null)
                    {
                        GameManager.instance.cameraCtrl.FadeSceneIn();
                    }
                }

                HeroController.instance.transform.position = entryPos;
                HeroController.instance.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
                if (shouldSetHazard)
                {
                    HeroController.instance.SetHazardRespawn(entryPos, true);
                }

                if (isDreamRoom)
                {
                    yield return new WaitForSeconds(0.5f);
                    HeroController.instance.transform.position = targetPos;
                    HeroController.instance.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
                }
                else if (isPantheonI || isPantheonII || isPantheonIII)
                {
                    GameManager.instance.sceneName = "GG_Atrium";
                    HeroController.instance.transform.position = targetPos;
                    HeroController.instance.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
                    if (shouldSetHazard)
                    {
                        HeroController.instance.SetHazardRespawn(targetPos, true);
                    }
                    if (GameManager.instance.cameraCtrl != null)
                    {
                        GameManager.instance.cameraCtrl.FadeSceneIn();
                    }
                    yield return new WaitForSeconds(0.05f);
                    HeroController.instance.transform.position = targetPos;
                    HeroController.instance.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
                    if (shouldSetHazard)
                    {
                        HeroController.instance.SetHazardRespawn(targetPos, true);
                    }
                }
                else if (isPantheonV || isPantheonVSegmented || isPantheonBench)
                {
                    
                    GameManager.instance.sceneName = "GG_Atrium_Roof";
                    HeroController.instance.transform.position = targetPos;
                    HeroController.instance.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
                    if (GameManager.instance.cameraCtrl != null)
                    {
                        GameManager.instance.cameraCtrl.FadeSceneIn();
                    }
                    
                    yield return new WaitForSeconds(0.05f);
                    HeroController.instance.transform.position = targetPos;
                    HeroController.instance.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
                }
            }
            else
            {
                HeroController.instance.transform.position = targetPos;
                HeroController.instance.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
            }

            if (!isSameScene && (scene.StartsWith("White_Palace", StringComparison.Ordinal) || isDreamRoom))
            {
                yield return new WaitForEndOfFrame();
                if (GameManager.instance.cameraCtrl != null)
                {
                    GameManager.instance.cameraCtrl.FadeSceneIn();
                }
            }

            mod.Log.Write("Teleport completed successfully");
            isBusy = false;
            NotifyTeleportCompleteForUi();
        }
        finally
        {
            if (timeScaleOverridden)
            {
                _ = GameManager.instance.StartCoroutine(RestoreTimeScaleAfterTeleport(previousTimeScale));
            }
        }
    }

    private static IEnumerator RestoreTimeScaleAfterTeleport(float previousTimeScale)
    {
        yield return null;

        if (GameManager.instance != null && GameManager.instance.IsInSceneTransition)
        {
            yield return new WaitWhile(() => GameManager.instance.IsInSceneTransition);
        }

        yield return new WaitForSecondsRealtime(0.5f);

        SpeedChanger.EndTimeScaleOverride(previousTimeScale);
    }

    private static void CleanSceneState()
    {
        UIManager.instance.UIClosePauseMenu();
        Time.timeScale = 1f;
        GameManager.instance.FadeSceneIn();
        GameManager.instance.isPaused = false;
        GameCameras.instance.ResumeCameraShake();

        if (HeroController.SilentInstance != null)
        {
            HeroController.instance.UnPause();

            if (HeroController.instance.cState.onConveyor || HeroController.instance.cState.onConveyorV || HeroController.instance.cState.inConveyorZone)
            {
                HeroController.instance.GetComponent<ConveyorMovementHero>()?.StopConveyorMove();
                HeroController.instance.cState.inConveyorZone = false;
                HeroController.instance.cState.onConveyor = false;
                HeroController.instance.cState.onConveyorV = false;
            }

            HeroController.instance.cState.nearBench = false;
        }

        MenuButtonList.ClearAllLastSelected();
        TimeController.GenericTimeScale = 1f;
        GameManager.instance.actorSnapshotUnpaused.TransitionTo(0f);
        GameManager.instance.ui.AudioGoToGameplay(0.2f);
        PlayerData.instance.atBench = false;
    }

    internal static void TryResetUiInput(GameObject? preferredSelection)
    {
        if (!pendingUiReset)
        {
            return;
        }

        pendingUiReset = false;

        EventSystem? eventSystem = EventSystem.current;
        if (eventSystem == null || InputHandler.Instance == null)
        {
            return;
        }

        GameObject? target = preferredSelection ?? eventSystem.currentSelectedGameObject;
        if (target != null)
        {
            eventSystem.SetSelectedGameObject(null);
            eventSystem.SetSelectedGameObject(target);
        }

        InputHandler.Instance.StopUIInput();
        InputHandler.Instance.StartUIInput();
    }

    private static void NotifyTeleportCompleteForUi()
    {
        string? sceneName = GameManager.instance?.sceneName;
        pendingUiReset = !string.IsNullOrEmpty(sceneName) && GodhomeHubScenes.Contains(sceneName!);
    }

    private string OnSceneChange(string newSceneName)
    {
        if (mod.Data.CustomTeleportScene != null && newSceneName != mod.Data.CustomTeleportScene)
        {
            mod.Log.Write($"Scene changed from {mod.Data.CustomTeleportScene} to {newSceneName}, clearing custom teleport point");
            mod.Data.CustomTeleportPosition = null;
            mod.Data.CustomTeleportScene = null;
        }

        return newSceneName;
    }
}
