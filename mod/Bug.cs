// The Hoarding Bug: a yellow scrap thief. It runs for the nearest loot on the floor (or your bag), carries it off and flees.
// Shoot it, hit it with a thrown scrap piece, or let SUPERHOT's slow motion help you catch it: it drops the loot when it dies.
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Sigf.Kit;
using UnityEngine;
using UnityEngine.AI;

public class Bug : MonoBehaviour
{
    public static readonly List<Bug> All = new List<Bug>();
    public int health = 1;
    public bool dead;
    public int tries;
    public bool HasLoot => carryDef != null;
    public Vector3 Velocity => agent != null && agent.enabled ? agent.velocity : Vector3.zero;
    NavMeshAgent agent;
    GameObject body, carryObj;
    LootDef carryDef; int carryValue;
    float nextThink, nextChitter, born;
    Scrap target;

    public static Bug Spawn(Vector3 pos)
    {
        if (!NavMesh.SamplePosition(pos, out var nav, 3f, NavMesh.AllAreas)) return null;
        var go = new GameObject("HoardingBug");
        go.transform.position = nav.position;
        var b = go.AddComponent<Bug>();
        b.body = Props.Make("hoarder_bug", 0.75f, "BugBody");
        b.body.transform.SetParent(go.transform, false);
        b.body.transform.localPosition = new Vector3(0, 0.05f, 0);
        var bc = go.AddComponent<BoxCollider>();
        bc.center = new Vector3(0, 0.35f, 0); bc.size = new Vector3(0.7f, 0.6f, 0.9f);
        var rb = go.AddComponent<Rigidbody>(); rb.isKinematic = true;
        b.agent = go.AddComponent<NavMeshAgent>();
        b.agent.speed = 4.6f; b.agent.acceleration = 30f; b.agent.angularSpeed = 540f; b.agent.radius = 0.35f; b.agent.height = 0.8f;
        b.agent.Warp(nav.position);
        All.Add(b);
        b.born = Time.unscaledTime;
        Mix.Play(Mix.Sound("sfx/monster_growl.wav"), nav.position, 0.6f, 1.7f);
        Hud.Terminal("LIFE FORM DETECTED. IT WANTS YOUR SCRAP.", Hud.Orange);
        return b;
    }

    void OnDestroy() => All.Remove(this);

    void Update()
    {
        if (dead || agent == null || !agent.isOnNavMesh) return;
        // chittering scuttle: the body wobbles while it runs
        float sp = agent.velocity.magnitude;
        body.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 26f) * 4f * Mathf.Clamp01(sp));
        body.transform.localPosition = new Vector3(0, 0.05f + Mathf.Abs(Mathf.Sin(Time.time * 18f)) * 0.04f * Mathf.Clamp01(sp), 0);
        if (Time.unscaledTime > nextChitter && Time.timeScale > 0.2f)
        {
            nextChitter = Time.unscaledTime + Random.Range(2.5f, 5f);
            Mix.Play(Mix.Sound("sfx/alarm_beep.wav"), transform.position, 0.25f, Random.Range(2f, 2.6f));
        }
        if (Time.unscaledTime < nextThink) return;
        nextThink = Time.unscaledTime + 0.25f;
        if (carryDef != null) { Flee(); return; }
        // hunt: the nearest loot on the floor, else the player's bag
        if (target == null || target.collected)
            target = Loot.All.Where(s => s != null && !s.collected && !s.thrown).OrderBy(s => (s.transform.position - transform.position).sqrMagnitude).FirstOrDefault();
        if (target != null)
        {
            agent.SetDestination(target.transform.position);
            if ((target.transform.position - transform.position).magnitude < 0.9f) Steal(target);
        }
        else
        {
            agent.SetDestination(G.Pos);
            if ((G.Pos - transform.position).magnitude < 1.4f && Loot.Bag.Count > 0) StealFromBag();
        }
    }

    void Flee()
    {
        var away = (transform.position - G.Pos); away.y = 0;
        var dest = transform.position + away.normalized * 12f;
        if (NavMesh.SamplePosition(dest, out var nav, 6f, NavMesh.AllAreas)) agent.SetDestination(nav.position);
        // far enough away with the loot: it vanishes into the dark (the scrap is lost for good)
        if (away.magnitude > 38f) { Hud.Terminal("THE BUG ESCAPED WITH $" + carryValue + ".", new Color(1f, 0.45f, 0.35f)); Destroy(gameObject); }
    }

    void Steal(Scrap s)
    {
        Mix.Log("BUG stole " + s.def.key);
        carryDef = s.def; carryValue = s.value;
        Loot.All.Remove(s);
        s.collected = true;
        Destroy(s.gameObject);
        Carry();
        Hud.Pop3D(transform.position + Vector3.up, "STOLEN  $" + carryValue, new Color(1f, 0.4f, 0.3f));
        Mix.Play(Mix.Sound("sfx/v_bug.wav"), null, 0.8f);
    }

    void StealFromBag()
    {
        var item = Loot.Bag[Loot.Bag.Count - 1];
        Loot.Bag.RemoveAt(Loot.Bag.Count - 1);
        Hud.Banked = Mathf.Max(0, Hud.Banked - item.Value);
        Hud.Carried = Loot.Bag.Count;
        carryDef = item.Key; carryValue = item.Value;
        Carry();
        Hud.Pop3D(transform.position + Vector3.up, "STOLEN  $" + carryValue, new Color(1f, 0.4f, 0.3f));
        Hud.Flash();
    }

    void Carry()
    {
        carryObj = Props.Make(carryDef.key, Mathf.Min(carryDef.height, 0.4f), "BugLoot");
        carryObj.transform.SetParent(body.transform, false);
        carryObj.transform.localPosition = new Vector3(0, 0.55f, -0.05f);
        carryObj.transform.localScale *= 0.7f;
        agent.speed = 5.4f;
    }

    public void Hurt(Vector3 at)
    {
        if (dead) return;
        dead = true;
        Mix.Log("BUG squashed");
        Mix.Burst(at, new Color(0.7f, 0.9f, 0.1f), 22, 6f, 0.1f, 1.6f);
        var l = Mix.Glow(at, new Color(0.7f, 1f, 0.2f), 6f, 3f); Destroy(l.gameObject, 0.4f);
        Mix.Play(Mix.Sound("sfx/enemy_hit.wav"), at, 1f, 1.3f);
        G.Shake(0.5f);
        if (carryDef != null)
        {
            var s = Loot.Spawn(transform.position + Vector3.up * 0.4f, carryDef, new Vector3(Random.Range(-1.5f, 1.5f), 4f, Random.Range(-1.5f, 1.5f)));
            s.value = carryValue;
            Hud.Terminal("BUG SQUASHED. SCRAP RECOVERED.", Hud.Green);
        }
        else Hud.Terminal("BUG SQUASHED. NO SCRAP WAS HARMED.", Hud.Green);
        // it flips over and stays as a prop for a few seconds
        agent.enabled = false;
        body.transform.localRotation = Quaternion.Euler(180f, 0, 0);
        body.transform.localPosition = new Vector3(0, 0.45f, 0);
        if (carryObj != null) Destroy(carryObj);
        Destroy(GetComponent<Collider>());
        Destroy(gameObject, 6f);
    }
}

// SUPERHOT bullets (the player's and the employees') squash the bug on contact.
[HarmonyPatch(typeof(Bullet), "OnCollisionEnter")]
static class BugShot
{
    static void Postfix(Collision col)
    {
        var b = col.collider != null ? col.collider.GetComponentInParent<Bug>() : null;
        if (b != null) b.Hurt(col.contacts.Length > 0 ? col.contacts[0].point : b.transform.position);
    }
}
