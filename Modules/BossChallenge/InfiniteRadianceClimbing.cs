using Osmi.FsmActions;
using Satchel;
using Satchel.Futils;
using WaitUntil = UnityEngine.WaitUntil;

namespace GodhomeQoL.Modules.BossChallenge;

public sealed class InfiniteRadianceClimbing : Module {
    private static readonly float heroX = 60.4987f;
    private static readonly float heroY = 34.6678f;

    private static bool running = false;
    private static GameObject? bossCtrl;
    private static PlayMakerFSM? radCtrl;
    private static PlayMakerFSM? pitCtrl;
    private static Coroutine? rewindCoro;

    public override ToggleableLevel ToggleableLevel => ToggleableLevel.ChangeScene;

    private protected override void Load() =>
        OsmiHooks.SceneChangeHook += SetupScene;

    private protected override void Unload() {
        OsmiHooks.SceneChangeHook -= SetupScene;

        if (running) {
            Quit(true);
        }
    }

    private static void SetupScene(Scene prev, Scene next) {
        if (prev.name != "GG_Workshop" || next.name != "GG_Radiance") {
            if (running) {
                Quit();
            }

            return;
        }

        if (running) {
            Quit(true);
            throw new InvalidOperationException("Running multiple times at the same time");
        }

        running = true;
        bossCtrl = next.GetGameObjectByName("Boss Control");
        radCtrl = bossCtrl.Child("Absolute Radiance")!.LocateMyFSM("Control");
        pitCtrl = bossCtrl.Child("Abyss Pit")!.LocateMyFSM("Ascend");

        bossCtrl!
            .Child("Ascend Respawns", "Hazard Respawn Trigger v2 (15)")!
            .SetActive(false);

        bossCtrl!.LocateMyFSM("Control").RemoveAction("Battle Start", 3);

        ModifyAbsRadFSM(radCtrl);

        radCtrl!.gameObject.LocateMyFSM("Phase Control")
            .Fsm.SetState("Set Ascend");

        LogDebug("Scene setup finished");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ModifyAbsRadFSM(PlayMakerFSM fsm) {
        fsm.RemoveAction("Set Arena 1", 3);

        fsm.ChangeTransition("Set Arena 1", FsmEvent.Finished.Name, "Climb Plats1");

        fsm.InsertAction("Set Arena 1", new InvokeCoroutine(TeleportSetup), 0);

        FsmState spawnPlatsState = fsm.GetValidState("Climb Plats1");
        spawnPlatsState.Actions = [
            spawnPlatsState.Actions[2],
            new InvokeMethod(() => fsm.gameObject.manageHealth(int.MaxValue))
        ];
        (spawnPlatsState.Actions[0] as SendEventByName)!.delay = 0;

        FsmState screamState = fsm.GetValidState("Scream");
        screamState.Actions = [
            screamState.Actions[0],
            new InvokeMethod(() => rewindCoro ??= radCtrl!.StartCoroutine(Rewind())),
            new Wait() { time = 60f },
            screamState.Actions[7]
        ];
    }

    private static IEnumerator TeleportSetup() {
        SpriteFlash flasher = Ref.HC.GetComponent<SpriteFlash>();

        Ref.HC.RelinquishControl();
        flasher.FlashingSuperDash();

        yield return new WaitForSeconds(0.5f);

        Ref.HC.transform.SetPosition2D(heroX, heroY);
        Ref.HC.FaceRight();
        Ref.HC.SetHazardRespawn(Ref.HC.transform.position, true);

        bossCtrl!.Child("Intro Wall")!.SetActive(false);

        yield return new WaitForSeconds(3.75f);

        flasher.CancelFlash();
        Ref.HC.RegainControl();

        LogDebug("Hero teleported");
    }

    private static IEnumerator Rewind() {
        LogDebug("AbsRad final phase started, rewinding...");

        SpriteFlash flasher = Ref.HC.GetComponent<SpriteFlash>();
        GameObject beam = radCtrl!.gameObject.Child("Eye Beam Glow", "Ascend Beam")!;

        radCtrl!.gameObject.LocateMyFSM("Attack Commands").Fsm.SetState("Idle");
        beam.SetActive(false);

        pitCtrl!.GetVariable<FsmFloat>("Hero Y").Value = 33f;
        pitCtrl!.SendEvent("ASCEND");

        PlayerDataR.isInvincible = true;
        Ref.HC.RelinquishControl();
        Ref.HC.transform.SetPosition2D(heroX, heroY);
        Ref.HC.FaceRight();
        Ref.HC.SetHazardRespawn(Ref.HC.transform.position, true);
        flasher.FlashingSuperDash();

        yield return new WaitUntil(() => pitCtrl!.transform.position.y == 30f);

        bossCtrl!
            .Child("Ascend Respawns")!
            .GetChildren()
            .Filter(go => go.name.StartsWith("Hazard Respawn Trigger v2"))
            .ForEach(go => {
                go.GetComponent<HazardRespawnTrigger>().Reflect().inactive = false;
                go.LocateMyFSM("raise_abyss_pit").Fsm.SetState("Idle");
            });

        flasher.CancelFlash();
        Ref.HC.RegainControl();
        PlayerDataR.isInvincible = false;

        yield return new WaitForSeconds(1.5f);

        radCtrl!.Fsm.SetState("Ascend Cast");
        radCtrl!.transform.SetPositionX(62.94f);
        beam.transform.parent.gameObject.SetActive(true);
        beam.SetActive(true);

        rewindCoro = null;
    }

    private static void Quit(bool killPlayer = false) {
        running = false;
        bossCtrl = null;
        radCtrl = null;
        pitCtrl = null;

        if (killPlayer) {
            _ = Ref.HC.StartCoroutine(DelayedKill());
        }
    }

    private static IEnumerator DelayedKill() {
        yield return new WaitUntil(() => Ref.GM.gameState == GameState.PLAYING);
        _ = Ref.HC.StartCoroutine(HeroControllerR.Die());
    }
}
