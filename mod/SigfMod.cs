using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Sigf.Kit;
using UnityEngine;

public class SigfMod : MixMod
{
    public override void OnLoad() => G.StartLevel = "wareHouse3";

    // the direction with the most open floor in front of the player: the show happens there
    public static Vector3 Open = Vector3.forward, Stage;

    static void FindOpen()
    {
        G.FindOpenSpot(0f, out var spot, out var dir, 1);
        Stage = spot;
        Open = dir;
        var flat = spot - G.Pos; flat.y = 0;
        // the open aisle: face along it from where we stand (the demo walks to the stage spot)
        G.LookAt(G.Pos + Open * 10f + Vector3.up * 1.4f, 0.2f);
    }

    public override void OnReady()
    {
        G.CachePrefab();
        FindOpen();
        Atmosphere.Apply();
        if (!SigfPlugin.Flags.demo) Intro();
        Mix.After(0.5f, () => Loot.Scatter(9, 4f, 14f, Open));
        Mix.After(2f, () => Hud.Scan());
        Mix.Every(14f, () => Hud.Scan());
        Mix.Every(8f, () => { if (!Mix.DemoStarted) Restock(); });
        Mix.Every(24f, () => { if (!Mix.DemoStarted && Bug.All.Count == 0 && Loot.All.Count > 0 && ViewSpot(out var sp)) Bug.Spawn(sp); });
        if (!SigfPlugin.Flags.demo) Atmosphere.StartDrone();
    }

    // the opening: title, terminal line, ship horn, the Company's welcome (at ready in normal play, at the start of the demo clip)
    static void Intro()
    {
        Mix.Say("LETHAL COMPANY TAKEOVER", 4f, Hud.Orange, 0.18f, 72);
        Mix.Say("collect scrap. meet the quota. do not ask questions.", 4f, Color.white, 0.27f, 34);
        Hud.Terminal("SYSTEMS ONLINE. COLLECT SCRAP. MEET THE QUOTA.");
        Mix.Play(Mix.Sound("sfx/ship_horn.wav"), null, 0.9f);
        Mix.After(1.2f, () => Mix.Play(Mix.Sound("sfx/v_welcome.wav"), null, 1f));
        Atmosphere.StartDrone();
    }

    static string Phase = "";
    public override void OnUpdate()
    {
        Loot.Update();
        Enemies.Update();
        if (Input.GetKeyDown(KeyCode.Q)) Hud.Scan();
        if (Input.GetKeyDown(KeyCode.F)) { Atmosphere.Toggle(); Mix.Play(Mix.Sound("sfx/flashlight_click.wav"), null, 0.7f); }
        if (Input.GetKeyDown(KeyCode.G)) Loot.Throw();
    }

    public override void OnGUI() => Hud.Draw();

    static void Look(Vector3 p, float t = 0.6f) => G.LookAt(p, t);

    static PejAiController NearestAlive(params PejAiController[] list) =>
        list.Where(x => x != null && !x.IsDead).OrderBy(x => (x.transform.position - G.Pos).sqrMagnitude).FirstOrDefault();

    static bool Clear(Vector3 to)
    {
        var from = G.Cam.transform.position;
        return !Physics.Linecast(from, to + Vector3.up * 0.3f, out var h, ~0, QueryTriggerInteraction.Ignore)
            || h.collider.GetComponentInParent<Scrap>() != null || h.collider.GetComponentInParent<PejAiController>() != null || h.collider.GetComponentInParent<Bug>() != null;
    }

    static Scrap NearestLoot() => Loot.All.Where(x => x != null && !x.collected && !x.thrown && Mathf.Abs(x.transform.position.y - G.Pos.y) < 1.5f
            && (x.transform.position - G.Pos).magnitude < 14f && Clear(x.transform.position))
        .OrderBy(x => (x.transform.position - G.Pos).sqrMagnitude).FirstOrDefault();

    // walks to the nearest loot in plain sight on the floor, looking at it
    static IEnumerator Gather(int max)
    {
        for (int i = 0; i < max; i++)
        {
            Phase += " g" + i;
            var s = NearestLoot();
            for (float w = 0; s == null && w < 1.2f; w += 0.25f) { yield return Mix.Wait(0.25f); s = NearestLoot(); }
            if (s == null) yield break;
            Phase += " walk";
            Look(s.transform.position + Vector3.up * 0.3f, 0.35f);
            yield return Mix.Wait(0.4f);
            yield return G.WalkTo(s.transform.position, 6f, 0.6f, 4f);
            yield return Mix.Wait(0.25f);
        }
    }

    // a spot 7-13 m away inside the camera's view cone, on the navmesh, with a clear line of sight
    static bool ViewSpot(out Vector3 spot)
    {
        spot = G.Pos;
        var fwd = G.Cam.transform.forward; fwd.y = 0; fwd.Normalize();
        for (int i = 0; i < 24; i++)
        {
            var d = Quaternion.Euler(0, Random.Range(-45f, 45f), 0) * fwd;
            var p = G.Pos + d * Random.Range(7f, 13f);
            if (!UnityEngine.AI.NavMesh.SamplePosition(p, out var nav, 4f, UnityEngine.AI.NavMesh.AllAreas)) continue;
            if (i < 16 && Physics.Linecast(G.Cam.transform.position, nav.position + Vector3.up * 1.2f, ~0, QueryTriggerInteraction.Ignore)) continue;   // later tries: any spot
            spot = nav.position;
            return true;
        }
        return false;
    }

    // keeps four employees in view, one of them the Coil-Head
    static void Restock()
    {
        var alive = G.Enemies().Where(e => e != null && !e.IsDead && (e.transform.position - G.Pos).magnitude < 25f && Clear(e.transform.position + Vector3.up * 1.2f)).ToList();
        int coils = alive.Count(e => e.GetComponent<Skinned>() != null && e.GetComponent<Skinned>().coil != null);
        for (int n = alive.Count; n < 3; n++)
        {
            if (!ViewSpot(out var sp)) return;
            var e = G.SpawnEnemy(sp);
            if (e == null) continue;
            bool coil = coils == 0; if (coil) coils++;
            Enemies.Skin(e, coil);
        }
    }

    static PejAiController Target(bool coil)
    {
        return G.Enemies().Where(e => e != null && !e.IsDead && (e.GetComponent<Skinned>() != null && e.GetComponent<Skinned>().coil != null) == coil
                && (e.transform.position - G.Pos).magnitude < 32f && Clear(e.transform.position + Vector3.up * 1.2f))
            .OrderBy(e => (e.transform.position - G.Pos).sqrMagnitude).FirstOrDefault();
    }

    const bool InspectBug = false;

    public override IEnumerator Demo()
    {
        float t0 = Time.unscaledTime;
        if (InspectBug)
        {
            G.ForceTime(0.02f);
            if (ViewSpot(out var isp)) { var ib = Bug.Spawn(G.Pos + Open * 3.5f); var ic = G.SpawnEnemy(G.Pos + Open * 5f + Vector3.Cross(Vector3.up, Open) * 1.2f); Enemies.Skin(ic, true); yield return Mix.Wait(1f); G.ForceTime(0.02f); Look(G.Pos + Open * 4f + Vector3.up * 0.5f, 0.1f); }
            yield return Mix.Wait(40f);
        }
        Intro();
        G.ForceTime(0.4f);
        Restock();
        Mix.After(2f, () => Hud.Scan());
        yield return Mix.Wait(3f);

        for (int round = 0; Time.unscaledTime - t0 < 68f; round++)
        {
            Phase = "round" + round + " top";
            Restock();
            if (round == 2 && Bug.All.Count == 0)
            {
                // the Hoarding Bug runs in from the aisle and grabs scrap: wait until it has a piece in its arms
                ViewSpot(out var bsp);
                var lt = NearestLoot();
                if (lt != null) bsp = lt.transform.position + Vector3.Cross(Vector3.up, lt.transform.position - G.Pos).normalized * 3.5f;   // beside a piece of loot: it grabs it at once
                var nb = Bug.Spawn(bsp);
                Phase = "bug spawn " + (nb != null); Mix.Log("DEMO bug spawn " + (nb != null));
                for (float w = 0; nb != null && !nb.dead && w < 1.5f && !nb.HasLoot; w += 0.25f) { Look(nb.transform.position + Vector3.up * 0.3f, 0.3f); yield return Mix.Wait(0.25f); }
            }
            var bug = Bug.All.FirstOrDefault(b => b != null && !b.dead && b.tries < 2 && (Clear(b.transform.position + Vector3.up * 0.3f) || (round == 2 && b.HasLoot)));
            if (round == 2) Mix.Log("DEMO round2 bugs=" + Bug.All.Count + " found=" + (bug != null) + " loot=" + Bug.All.Any(x => x != null && x.HasLoot));
            if (bug != null)
            {
                Phase = "bug round"; Mix.Log("DEMO bug round start");
                for (int shot = 0; shot < 2 && bug != null && !bug.dead; shot++)
                {
                    Look(bug.transform.position + Vector3.up * 0.3f, 0.3f);
                    G.ForceTime(0.12f);
                    yield return Mix.Wait(0.5f);
                    if (bug == null || bug.dead) break;
                    bug.tries++;
                    var dist = (bug.transform.position - G.Cam.transform.position).magnitude;
                    Loot.Throw(bug.transform.position + bug.Velocity * (dist / 22f) + Vector3.up * 0.3f, 22f, Loot.Defs[4]);   // led shot: the bug keeps running
                    yield return Mix.Wait(2.5f);
                }
                G.ForceTime(0.45f);
                Phase = "bug gather";
                yield return Gather(2);
                continue;
            }
            bool coilRound = round % 2 == 1;
            var close = G.Enemies().Where(e => e != null && !e.IsDead && (e.transform.position - G.Pos).magnitude < 3.5f).OrderBy(e => (e.transform.position - G.Pos).sqrMagnitude).FirstOrDefault();
            var t = close ?? Target(coilRound) ?? Target(!coilRound);
            for (float w = 0; t == null && w < 3f; w += 0.5f)
            {
                var any = G.Enemies().OrderBy(e => (e.transform.position - G.Pos).sqrMagnitude).FirstOrDefault();
                G.LookAt(any != null ? any.transform.position + Vector3.up * 1.3f : G.Pos + Open * 10f + Vector3.up * 1.4f, 0.5f);
                if (w >= 1.5f && any != null && (any.transform.position - G.Pos).magnitude > 7f) yield return G.WalkTo(any.transform.position, 4.5f, 6f, 1.5f);
                yield return Mix.Wait(0.5f); Restock(); t = Target(coilRound) ?? Target(!coilRound);
            }
            if (t == null) continue;
            bool isCoil = t.GetComponent<Skinned>() != null && t.GetComponent<Skinned>().coil != null && t != close;

            Phase = "round" + round + " aim coil=" + isCoil;
            Look(t.transform.position + Vector3.up * 1.2f, 0.4f);
            G.ForceTime(0.12f);
            yield return Mix.Wait(0.7f);
            if (isCoil)
            {
                // scrap is ammo: the bag's last piece (a cash register if the bag is empty) is thrown in bullet time
                Loot.Throw(t.transform.position + Vector3.up * 1.3f, 26f, Loot.Defs[1]);
                yield return Mix.Wait(3f);
            }
            else
            {
                G.ForceTime(0.03f);
                G.Shatter(t);
                G.Shake(0.8f);
                yield return Mix.Wait(3f);
            }
            G.ForceTime(0.45f);
            Phase = "round" + round + " gather";
            yield return Gather(isCoil ? 3 : 2);
            if (round % 3 == 2) Hud.Scan();
        }
        G.ForceTime(0.45f);
        yield return Mix.Wait(3f);
    }
}

// the game's first-time tutorial hints ("press E to HOTSWITCH...") would sit over the show
[HarmonyPatch(typeof(TutorialManager), nameof(TutorialManager.RequestTutorialMessage))]
static class NoTutorialHints
{
    static bool Prefix() => false;
}

// every subtitle line of the game ("press E to HOTSWITCH...") is muted: the Company's terminal is the only voice on screen
[HarmonyPatch]
static class NoLevelSubtitles
{
    static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods() =>
        typeof(TextManager).GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
            .Where(m => m.Name.StartsWith("SetSubtitle") || m.Name == "SetStaticUptitle" || m.Name == "SetStaticSubtitle").Cast<System.Reflection.MethodBase>();

    static bool Prefix() => false;
}
