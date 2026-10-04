// Lethal Company style interface: quota panel, the scanner (ping ring + orange value tags over scrap), company terminal ticker.
using System.Collections.Generic;
using System.Linq;
using Sigf.Kit;
using UnityEngine;

public static class Hud
{
    public static readonly Color Orange = new Color(1f, 0.55f, 0.1f);
    public static readonly Color Green = new Color(0.45f, 1f, 0.45f);
    static readonly Color Panel = new Color(0.04f, 0.03f, 0.02f, 0.78f);

    public static int Quota = 130, Banked, Day = 1, Carried;
    public static float ScanStart = -99f, ScanEnd = -99f;
    static float flash, quotaFlash;

    class Pop { public Vector3 pos; public string text; public float born; public Color color; }
    static readonly List<Pop> pops = new List<Pop>();
    class Line { public string text; public float born; public Color color; }
    static readonly List<Line> lines = new List<Line>();
    static GUIStyle label, big, small;
    static Texture2D ring;

    public static void Pop3D(Vector3 pos, string text, Color c) => pops.Add(new Pop { pos = pos, text = text, born = Time.unscaledTime, color = c });
    public static void Terminal(string text, Color? c = null) { lines.Add(new Line { text = text, born = Time.unscaledTime, color = c ?? Green }); if (lines.Count > 5) lines.RemoveAt(0); }
    public static void Flash() => flash = Time.unscaledTime;

    public static void Scan()
    {
        ScanStart = Time.unscaledTime;
        ScanEnd = ScanStart + 5f;
        Mix.Play(Mix.Sound("sfx/scan_ping.wav"), null, 0.9f);
    }

    static void Init()
    {
        if (label != null) return;
        label = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, richText = false };
        big = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
        small = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
        int n = 256;
        ring = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.94f) / 0.05f) + Mathf.Clamp01(1f - d) * 0.05f;
                ring.SetPixel(x, y, new Color(1, 1, 1, d > 1f ? 0 : a));
            }
        ring.Apply();
    }

    static void Text(Rect r, string t, GUIStyle st, Color c, int size)
    {
        st.fontSize = size;
        var p = GUI.color;
        GUI.color = new Color(0, 0, 0, 0.9f * c.a);
        GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), t, st);
        GUI.color = c;
        GUI.Label(r, t, st);
        GUI.color = p;
    }

    static void Box(Rect r, Color c)
    {
        var p = GUI.color; GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = p;
    }

    public static void Draw()
    {
        Init();
        float k = Screen.height / 1080f;
        float now = Time.unscaledTime;

        // damage / pickup flash at the screen edges
        if (now - flash < 0.4f) Box(new Rect(0, 0, Screen.width, Screen.height), new Color(1f, 0.5f, 0.1f, 0.25f * (1f - (now - flash) / 0.4f)));

        // quota panel (top left)
        var pr = new Rect(30 * k, 30 * k, 420 * k, 150 * k);
        Box(pr, Panel);
        Box(new Rect(pr.x, pr.y, 6 * k, pr.height), Orange);
        Text(new Rect(pr.x + 22 * k, pr.y + 8 * k, 380 * k, 34 * k), "DAY " + Day + "   //   THE COMPANY", label, Orange, Mathf.RoundToInt(24 * k));
        Text(new Rect(pr.x + 22 * k, pr.y + 44 * k, 380 * k, 40 * k), "QUOTA  $" + Banked + " / $" + Quota, label, quotaFlash > now ? Color.white : Green, Mathf.RoundToInt(32 * k));
        float frac = Mathf.Clamp01((float)Banked / Quota);
        Box(new Rect(pr.x + 22 * k, pr.y + 92 * k, 376 * k, 18 * k), new Color(1, 1, 1, 0.12f));
        Box(new Rect(pr.x + 22 * k, pr.y + 92 * k, 376 * k * frac, 18 * k), frac >= 1f ? Green : Orange);
        Text(new Rect(pr.x + 22 * k, pr.y + 112 * k, 380 * k, 30 * k), "BAG: " + Carried + "   [Q] SCAN  [G] THROW SCRAP", label, new Color(1, 1, 1, 0.75f), Mathf.RoundToInt(20 * k));

        // terminal ticker (bottom left)
        for (int i = lines.Count - 1; i >= 0; i--) if (now - lines[i].born > 7f) lines.RemoveAt(i);
        for (int i = 0; i < lines.Count; i++)
        {
            var l = lines[i];
            float a = Mathf.Clamp01(7f - (now - l.born));
            var c = l.color; c.a = Mathf.Clamp01(a);
            Text(new Rect(40 * k, Screen.height - (60 + (lines.Count - i) * 36) * k, 900 * k, 34 * k), "> " + l.text, label, c, Mathf.RoundToInt(26 * k));
        }

        var cam = G.Cam;
        if (cam == null) return;

        // scanner ring + tags
        if (now < ScanEnd)
        {
            float t = now - ScanStart;
            float fade = Mathf.Clamp01((ScanEnd - now) / 1.5f);
            float rr = Mathf.Min(t / 0.9f, 1.3f) * Screen.height * 0.9f;
            if (t < 1.2f)
            {
                var pc = GUI.color; GUI.color = new Color(Orange.r, Orange.g, Orange.b, 0.9f * (1f - t / 1.2f));
                GUI.DrawTexture(new Rect(Screen.width / 2f - rr, Screen.height / 2f - rr, rr * 2, rr * 2), ring);
                GUI.color = pc;
            }
            int shown = 0;
            foreach (var s in Loot.All.OrderBy(x => x == null ? 1e9f : (x.transform.position - cam.transform.position).sqrMagnitude))
            {
                if (s == null || s.collected || shown >= 7) continue;
                if ((s.transform.position - cam.transform.position).magnitude > 22f) continue;
                shown++;
                DrawTag(cam, s.transform.position + Vector3.up * (s.def.height * 0.6f), s.def.title, "Value: $" + s.value, k, fade, Orange, t * 25f);
            }
            foreach (var b in Bug.All)
                if (b != null && !b.dead) DrawTag(cam, b.transform.position + Vector3.up * 1.0f, "Hoarding Bug", "Steals scrap", k, fade, new Color(0.95f, 0.85f, 0.15f), t * 25f);
            foreach (var e in G.Enemies())
            {
                if (e == null) continue;
                DrawTag(cam, e.transform.position + Vector3.up * 2.2f, Enemies.TitleOf(e), "Threat: lethal", k, fade, new Color(1f, 0.25f, 0.2f), t * 25f);
            }
        }

        // (bugs are tagged with the rest of the scan)
        // floating +$ pops
        for (int i = pops.Count - 1; i >= 0; i--)
        {
            var p = pops[i];
            float age = now - p.born;
            if (age > 1.8f) { pops.RemoveAt(i); continue; }
            var sp = cam.WorldToScreenPoint(p.pos + Vector3.up * age * 0.8f);
            if (sp.z <= 0) continue;
            var c = p.color; c.a = Mathf.Clamp01(1.8f - age);
            Text(new Rect(sp.x - 200 * k, Screen.height - sp.y - 30 * k, 400 * k, 60 * k), p.text, big, c, Mathf.RoundToInt(40 * k));
        }
    }

    static void DrawTag(Camera cam, Vector3 wp, string title, string sub, float k, float fade, Color col, float reveal)
    {
        var sp = cam.WorldToScreenPoint(wp);
        if (sp.z <= 0.5f || sp.z > 60f) return;
        float dist = (cam.transform.position - wp).magnitude;
        float sc = Mathf.Clamp(1.4f - dist * 0.03f, 0.55f, 1.2f) * k;
        float w = 200 * sc, h = 66 * sc;
        var r = new Rect(sp.x - w / 2f, Screen.height - sp.y - h - 24 * sc, w, h);
        var bg = new Color(0.04f, 0.03f, 0.02f, 0.85f * fade);
        Box(r, bg);
        Box(new Rect(r.x, r.y, w, 3 * sc), new Color(col.r, col.g, col.b, fade));
        Box(new Rect(sp.x - 1.5f * sc, r.yMax, 3 * sc, 24 * sc), new Color(col.r, col.g, col.b, fade));
        var c1 = new Color(col.r, col.g, col.b, fade);
        Text(new Rect(r.x, r.y + 6 * sc, w, 34 * sc), title, big, c1, Mathf.RoundToInt(23 * sc));
        Text(new Rect(r.x, r.y + 33 * sc, w, 30 * sc), sub, small, new Color(1, 1, 1, fade), Mathf.RoundToInt(20 * sc));
    }

    public static void QuotaPulse() => quotaFlash = Time.unscaledTime + 0.6f;
}
