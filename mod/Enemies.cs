// The crystal men become Company employees: hazmat helmets with headlamps. Killing one spills scrap and pops the helmet off.
using HarmonyLib;
using Sigf.Kit;
using UnityEngine;

public class Skinned : MonoBehaviour
{
    public GameObject helmet;
    public Light lamp;
    public string title = "Employee";
    public GameObject coil;      // Coil-Head: the whole body is replaced by the mannequin model
    float lastBoing, yaw;
    public void SetYaw(float y) => yaw = y;
    Vector3 lastPos;

    void LateUpdate()
    {
        if (coil == null) return;
        // hide the crystal body every frame (the game re-enables its renderers)
        foreach (var r in GetComponentsInChildren<Renderer>())
            if (r is SkinnedMeshRenderer || (r.transform.IsChildOf(transform) && r.sharedMaterial != null && r.sharedMaterial.name.Contains("Crystal")))
                if (!r.transform.IsChildOf(coil.transform) && !r.name.Contains("Shatter") && !r.transform.parent.name.Contains("Shatter")) r.enabled = false;
        var pos = transform.position;
        var d = pos - lastPos; d.y = 0;
        float sp = d.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 face = d.sqrMagnitude > 1e-6f ? d.normalized : (G.Pos - pos).normalized;
        face.y = 0;
        if (face.sqrMagnitude > 0.01f) yaw = Mathf.LerpAngle(yaw, Quaternion.LookRotation(face).eulerAngles.y, Time.deltaTime * 6f);
        // stiff sway while it glides
        coil.transform.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(Time.time * 9f)) * 0.04f * Mathf.Clamp01(sp), 0);
        coil.transform.rotation = Quaternion.Euler(0, yaw, Mathf.Sin(Time.time * 7f) * 2f * Mathf.Clamp01(sp));
        if (sp > 0.8f && Time.unscaledTime - lastBoing > 0.9f && Time.timeScale > 0.3f)
        {
            lastBoing = Time.unscaledTime;
            Mix.Play(Mix.Sound("sfx/spring_boing.wav"), pos, 0.5f, Random.Range(0.85f, 1.2f));
        }
        lastPos = pos;
    }
}

public static class Enemies
{
    static readonly string[] Lines = { "EMPLOYEE LOST. SCRAP RECOVERED.", "THE COMPANY THANKS THEM FOR THEIR SERVICE.", "CAUSE OF DEATH: PROFIT.", "HR HAS BEEN NOTIFIED. HR DOES NOT CARE.", "EMPLOYEE LOST. THEIR HELMET WAS NOT INSURED." };
    static float nextScan;
    static int deaths;

    public static string TitleOf(PejAiController e)
    {
        var s = e != null ? e.GetComponent<Skinned>() : null;
        return s != null ? s.title : "Employee";
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t) { var r = FindDeep(c, name); if (r != null) return r; }
        return null;
    }

    public static void Update()
    {
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + 0.4f;
        foreach (var e in G.Enemies())
            if (e.GetComponent<Skinned>() == null) Skin(e);
    }

    static int skinned;

    public static void Skin(PejAiController e, bool? coil = null)
    {
        if (e == null || e.GetComponent<Skinned>() != null) return;
        var sk = e.gameObject.AddComponent<Skinned>();
        if (coil ?? (++skinned % 4 == 0)) { SkinCoil(e, sk); return; }
        var head = FindDeep(e.transform, "Head");
        if (head == null) { Mix.Warn("no Head bone"); return; }
        var h = Props.Make("hazmat_helmet", 0.42f, "SigfHelmet");
        h.transform.SetParent(head, false);
        h.transform.localPosition = new Vector3(0f, -0.10f, 0f);
        h.transform.localRotation = Quaternion.identity;
        sk.helmet = h;
        // headlamp: a warm spot cone from the helmet, aimed where the employee looks
        var lg = new GameObject("SigfHeadlamp");
        lg.transform.SetParent(head, false);
        lg.transform.localPosition = new Vector3(0f, 0.1f, 0f);
        lg.transform.localRotation = Quaternion.identity;
        var lamp = lg.AddComponent<Light>();
        lamp.type = LightType.Spot; lamp.spotAngle = 48f; lamp.range = 11f; lamp.intensity = 2.2f;
        lamp.color = new Color(1f, 0.9f, 0.65f);
        sk.lamp = lamp;
    }

    static void SkinCoil(PejAiController e, Skinned sk)
    {
        sk.title = "Coil-Head";
        var c = Props.Make("coilhead", 1.95f, "SigfCoilHead");
        c.transform.SetParent(e.transform, false);
        c.transform.localPosition = Vector3.zero;
        c.transform.rotation = e.transform.rotation;
        sk.coil = c;
        sk.SetYaw(e.transform.eulerAngles.y);
        var lamp = Mix.Glow(e.transform.position + Vector3.up * 2.1f, new Color(1f, 0.35f, 0.2f), 7f, 1.6f, c.transform);
        sk.lamp = lamp;
        Mix.Play(Mix.Sound("sfx/monster_growl.wav"), e.transform.position, 0.8f);
    }

    public static void OnKilled(PejAiController e)
    {
        deaths++;
        var p = e.transform.position;
        var sk = e.GetComponent<Skinned>();
        if (sk != null && sk.coil != null)
        {
            var c = sk.coil;
            c.transform.SetParent(null, true);
            var rb = c.AddComponent<Rigidbody>();
            var bc = c.AddComponent<BoxCollider>();
            var mesh = Props.MeshOf("coilhead");
            if (mesh != null) { bc.center = mesh.bounds.center; bc.size = mesh.bounds.size; }
            rb.mass = 6f;
            rb.velocity = Vector3.up * 2f + (p - G.Pos).normalized * 2f;
            rb.angularVelocity = Random.insideUnitSphere * 3f;
            Mix.Play(Mix.Sound("sfx/spring_boing.wav"), p, 1f, 0.8f);
            Object.Destroy(c, 10f);
        }
        // helmet pops off
        if (sk != null && sk.helmet != null)
        {
            var hp = sk.helmet.transform.position;
            sk.helmet.transform.SetParent(null, true);
            var rb = sk.helmet.AddComponent<Rigidbody>();
            var bc = sk.helmet.AddComponent<BoxCollider>();
            var mesh = Props.MeshOf("hazmat_helmet");
            if (mesh != null) { bc.center = mesh.bounds.center; bc.size = mesh.bounds.size; }
            rb.velocity = new Vector3(Random.Range(-2f, 2f), 5f, Random.Range(-2f, 2f));
            rb.angularVelocity = Random.insideUnitSphere * 12f;
            Object.Destroy(sk.helmet, 8f);
            if (sk.lamp != null) { sk.lamp.transform.SetParent(sk.helmet.transform, true); }
        }
        // loot spills
        int n = Random.value < 0.5f ? 2 : 1;
        for (int i = 0; i < n; i++)
            Loot.Spawn(p + Vector3.up * 1.2f, null, new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(3f, 5.5f), Random.Range(-2.5f, 2.5f)));
        Mix.Play(Mix.Sound("sfx/enemy_hit.wav"), p, 1f, Random.Range(0.9f, 1.15f));
        Mix.Play(Mix.Sound("sfx/scrap_drop.wav"), p, 0.8f);
        Hud.Terminal(Lines[deaths % Lines.Length], new Color(1f, 0.45f, 0.35f));
        if (deaths % 4 == 1) Mix.After(0.8f, () => Mix.Play(Mix.Sound("sfx/v_dead.wav"), null, 0.9f));
    }
}

[HarmonyPatch(typeof(PejAiController), nameof(PejAiController.Kill))]
static class EmployeeKilled
{
    static void Prefix(PejAiController __instance)
    {
        if (__instance == null || __instance.IsDead) return;
        Enemies.OnKilled(__instance);
    }
}
