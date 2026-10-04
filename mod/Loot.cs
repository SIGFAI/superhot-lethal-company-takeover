// Scrap: Lethal Company loot that spills out of dead employees and lies around the facility. Walk into it to bag it.
using System.Collections.Generic;
using Sigf.Kit;
using UnityEngine;

public class LootDef
{
    public string key, title; public int value; public float height; public float weight;
    public LootDef(string key, string title, int value, float height, float weight) { this.key = key; this.title = title; this.value = value; this.height = height; this.weight = weight; }
}

public class Scrap : MonoBehaviour
{
    public LootDef def;
    public int value;
    public bool collected;
    public bool thrown;      // flying as a weapon: can't be bagged until it lands
    public Transform homing; // slight aim assist toward what it was thrown at
    float spawned;
    Vector3 last;
    void Awake() { spawned = Time.unscaledTime; last = transform.position; }
    void Land() { thrown = false; var rb = GetComponent<Rigidbody>(); if (rb != null) rb.useGravity = true; }
    public bool CanPick => !thrown && Time.unscaledTime - spawned > 0.8f;

    // A thrown scrap piece kills the first crystal man it touches (swept check, so fast throws never tunnel).
    void FixedUpdate()
    {
        if (!thrown) { last = transform.position; return; }
        if (homing != null)
        {
            var rbh = GetComponent<Rigidbody>();
            var want = (homing.position + Vector3.up * 0.3f - transform.position);
            if (want.magnitude > 0.5f) rbh.velocity = Vector3.Lerp(rbh.velocity, want.normalized * rbh.velocity.magnitude, 0.12f);
        }
        var d = transform.position - last;
        if (d.magnitude > 0.001f)
        {
            foreach (var h in Physics.SphereCastAll(last, 0.45f, d.normalized, d.magnitude + 0.1f, ~0, QueryTriggerInteraction.Ignore))
            {
                var bug = h.collider.GetComponentInParent<Bug>();
                if (bug != null && !bug.dead) { bug.Hurt(h.point); Mix.Burst(h.point, new Color(1f, 0.7f, 0.2f), 10, 5f, 0.08f, 1f); Land(); break; }
                var e = h.collider.GetComponentInParent<PejAiController>();
                if (e == null || e.IsDead) continue;
                Loot.HitEnemy(this, e, h.point);
                Land();
                break;
            }
        }
        last = transform.position;
        if (thrown && (Time.unscaledTime - spawned > 6f || (Time.unscaledTime - spawned > 0.3f && GetComponent<Rigidbody>().velocity.magnitude < 4f))) Land();
    }
}

public static class Loot
{
    public static readonly List<Scrap> All = new List<Scrap>();
    public static readonly LootDef[] Defs =
    {
        new LootDef("gold_bar", "Gold Bar", 110, 0.22f, 1f),
        new LootDef("cash_register", "Cash Register", 85, 0.55f, 1.2f),
        new LootDef("clown_horn", "Clown Horn", 45, 0.4f, 1.5f),
        new LootDef("yield_sign", "Yield Sign", 65, 1.1f, 1f),
        new LootDef("teapot", "Teapot", 55, 0.36f, 1.4f),
        new LootDef("big_bolt", "Big Bolt", 75, 0.7f, 1.2f),
    };

    public static readonly List<KeyValuePair<LootDef, int>> Bag = new List<KeyValuePair<LootDef, int>>();

    /// <summary>Throws the last piece of the bag (its value comes off the quota). free: throws a spare piece instead (the demo bot).</summary>
    public static bool Throw(Vector3? target = null, float speed = 30f, LootDef free = null, Transform homing = null)
    {
        if (G.Cam == null) return false;
        KeyValuePair<LootDef, int> item;
        if (free != null) item = new KeyValuePair<LootDef, int>(free, 0);
        else
        {
            if (Bag.Count == 0) return false;
            item = Bag[Bag.Count - 1];
            Bag.RemoveAt(Bag.Count - 1);
            Hud.Banked = Mathf.Max(0, Hud.Banked - item.Value);
            Hud.Carried = Bag.Count;
        }
        var cam = G.Cam.transform;
        var dir0 = target.HasValue ? (target.Value - cam.position).normalized : cam.forward;
        var from = cam.position + dir0 * 0.7f;
        if (Physics.Raycast(cam.position, dir0, out var wall, 0.8f, ~0, QueryTriggerInteraction.Ignore)) from = cam.position + dir0 * Mathf.Max(0.05f, wall.distance - 0.2f);
        var dir = target.HasValue ? (target.Value - from).normalized : cam.forward;
        var s = Spawn(from, item.Key, dir * speed);
        s.value = item.Value > 0 ? item.Value : Mathf.Max(20, s.value / 2);
        s.thrown = true;
        s.homing = homing != null ? homing : AimAssist(dir);
        var rb = s.GetComponent<Rigidbody>();
        rb.angularVelocity = Random.onUnitSphere * 14f;
        rb.useGravity = false;   // flies straight like a bullet until it hits or lands
        Hud.Pop3D(from + dir, (item.Value > 0 ? "-$" + item.Value + "  " : "") + "THROWN", new Color(1f, 0.5f, 0.3f));
        Mix.Play(Mix.Sound("sfx/scrap_drop.wav"), null, 0.7f, 1.6f);
        return true;
    }

    /// <summary>The employee, Coil-Head or bug closest to the crosshair (within 8 degrees, in sight): thrown scrap curves toward it a little.</summary>
    static Transform AimAssist(Vector3 dir)
    {
        var cam = G.Cam.transform;
        Transform best = null; float bestAng = 8f;
        foreach (var e in G.Enemies())
        {
            float a = Vector3.Angle(dir, e.transform.position + Vector3.up * 1.2f - cam.position);
            if (a >= bestAng) continue;
            bool blocked = Physics.Linecast(cam.position, e.transform.position + Vector3.up * 1.2f, out var h, ~0, QueryTriggerInteraction.Ignore);
            if (!blocked || h.collider.GetComponentInParent<PejAiController>() != null) { bestAng = a; best = e.transform; }
        }
        foreach (var b in Bug.All)
            if (b != null && !b.dead && Vector3.Angle(dir, b.transform.position - cam.position) < bestAng) { bestAng = Vector3.Angle(dir, b.transform.position - cam.position); best = b.transform; }
        return best;
    }

    public static void HitEnemy(Scrap s, PejAiController e, Vector3 at)
    {
        Mix.Burst(at, new Color(1f, 0.7f, 0.2f), 18, 7f, 0.1f, 1.5f);
        var l = Mix.Glow(at, new Color(1f, 0.7f, 0.2f), 6f, 4f);
        Object.Destroy(l.gameObject, 0.5f);
        Mix.Play(Mix.Sound("sfx/enemy_hit.wav"), at, 1f, 0.8f);
        Hud.Terminal("DIRECT HIT. " + s.def.title.ToUpper() + " WORKS AS AMMO.", Hud.Orange);
        G.Shatter(e);
        G.Shake(1f);
    }

    public static LootDef Pick()
    {
        float t = 0; foreach (var d in Defs) t += d.weight;
        float r = Random.value * t;
        foreach (var d in Defs) { r -= d.weight; if (r <= 0) return d; }
        return Defs[0];
    }

    public static Scrap Spawn(Vector3 pos, LootDef def = null, Vector3? toss = null)
    {
        def = def ?? Pick();
        var go = Props.MakeBody(def.key, def.height, "Scrap_" + def.key);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
        var rb = go.GetComponent<Rigidbody>();
        rb.mass = 2f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        if (toss.HasValue) { rb.velocity = toss.Value; rb.angularVelocity = Random.insideUnitSphere * 6f; }
        var s = go.AddComponent<Scrap>();
        s.def = def;
        s.value = Mathf.RoundToInt(def.value * Random.Range(0.8f, 1.25f) / 5f) * 5;
        All.Add(s);
        Mix.Glow(pos, Hud.Orange, 3f, 1.2f, go.transform);
        return s;
    }

    /// <summary>Scatter loot on the floor around the player, out of the dark.</summary>
    public static void Scatter(int count, float minDist, float maxDist, Vector3? facing = null)
    {
        for (int i = 0, tries = 0; i < count && tries < count * 12; tries++)
        {
            var from = G.Pos + Vector3.up * 1.5f;
            var fwd = facing ?? (G.Ahead(1f) - G.Pos); fwd.y = 0;
            Vector3 dir;
            dir = Quaternion.Euler(0, Random.Range(-65f, 65f), 0) * fwd.normalized;
            var p = from + dir * Random.Range(minDist, maxDist);
            if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out var hit, 8f) && hit.collider.GetComponentInParent<PejAiController>() == null)
            {
                if (Physics.Raycast(from, (p - from), out var blk, (p - from).magnitude) && blk.distance < (p - from).magnitude - 1f) continue;
                if (Physics.Raycast(hit.point + Vector3.up * 0.2f, Vector3.up, 2.2f)) continue;   // not under a shelf or desk
                if (Physics.CheckSphere(hit.point + Vector3.up * 0.6f, 0.45f)) continue;           // not wedged in furniture
                Spawn(hit.point + Vector3.up * 0.1f);
                i++;
            }
        }
    }

    public static void Update()
    {
        for (int i = All.Count - 1; i >= 0; i--)
        {
            var s = All[i];
            if (s == null) { All.RemoveAt(i); continue; }
            if (s.collected || !s.CanPick) continue;
            var d = s.transform.position - (G.Pos + Vector3.up * 0.9f);
            if (d.magnitude < 2.4f) Collect(s);
        }
    }

    public static void Collect(Scrap s)
    {
        s.collected = true;
        Hud.Banked += s.value;
        Bag.Add(new KeyValuePair<LootDef, int>(s.def, s.value));
        Hud.Carried = Bag.Count;
        Hud.Pop3D(s.transform.position + Vector3.up * 0.5f, "+$" + s.value + "  " + s.def.title, Hud.Green);
        Hud.QuotaPulse();
        Hud.Flash();
        Mix.Play(Mix.Sound("sfx/cash_pickup.wav"), null, 0.8f, Random.Range(0.95f, 1.1f));
        Mix.Burst(s.transform.position + Vector3.up * 0.3f, new Color(1f, 0.8f, 0.2f), 12, 4f, 0.05f, 0.9f);
        All.Remove(s);
        Object.Destroy(s.gameObject);
        if (Hud.Banked >= Hud.Quota) QuotaMet();
    }

    static void QuotaMet()
    {
        Mix.Play(Mix.Sound("sfx/quota_done.wav"), null, 1f);
        Mix.After(1.2f, () => Mix.Play(Mix.Sound("sfx/v_quota.wav"), null, 1f));
        Mix.Say("QUOTA MET", 3.5f, Hud.Green, 0.3f, 90);
        Hud.Terminal("QUOTA MET. THE COMPANY IS PLEASED.");
        G.Words("QUOTA;MET");
        G.Shake(1.2f);
        Hud.Day++;
        Hud.Quota += 90 + 40 * Hud.Day;
        Hud.Banked = 0;
        Bag.Clear();
        Hud.Carried = 0;
        Hud.Terminal("DAY " + Hud.Day + ". NEW QUOTA: $" + Hud.Quota);
    }
}
