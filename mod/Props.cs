// Builds the generated 3D models (mesh data in Model_*.cs, texture in assets/models) into Unity objects.
using System.Collections.Generic;
using Sigf.Kit;
using UnityEngine;

public static class Props
{
    static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

    static string Camel(string k)
    {
        var s = "";
        foreach (var p in k.Split('_')) s += char.ToUpper(p[0]) + p.Substring(1);
        return s;
    }

    public static Mesh MeshOf(string key)
    {
        if (meshes.TryGetValue(key, out var m)) return m;
        var f = typeof(ModelData).GetField(Camel(key));
        if (f == null) { Mix.Warn("no model data for " + key); return null; }
        m = Mix.MeshFromBase64((string)f.GetRawConstantValue(), key);
        meshes[key] = m;
        return m;
    }

    /// <summary>Lit material with the model texture, plus a faint self glow so it reads in the dark.</summary>
    public static Material MatOf(string key, float glow = 0.08f)
    {
        if (mats.TryGetValue(key, out var m)) return m;
        var tex = Mix.Texture("models/" + key + ".png");
        m = new Material(Shader.Find("Standard"));
        m.mainTexture = tex;
        m.color = new Color(0.72f, 0.72f, 0.72f);
        m.SetFloat("_Glossiness", 0.25f);
        m.SetFloat("_Metallic", 0.1f);
        if (glow > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.SetTexture("_EmissionMap", tex);
            m.SetColor("_EmissionColor", Color.white * glow);
        }
        mats[key] = m;
        return m;
    }

    /// <summary>A visual-only object of the given world height (origin at its base, facing +Z).</summary>
    public static GameObject Make(string key, float height, string name = null)
    {
        var go = new GameObject(name ?? ("Sigf_" + key));
        var mesh = MeshOf(key);
        if (mesh == null) { var c = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.Destroy(c.GetComponent<Collider>()); c.transform.SetParent(go.transform, false); c.transform.localPosition = Vector3.up * height / 2f; c.transform.localScale = Vector3.one * height; return go; }
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = MatOf(key);
        go.transform.localScale = Vector3.one * height;
        return go;
    }

    /// <summary>Same as Make but with a box collider fitted to the mesh and a rigidbody.</summary>
    public static GameObject MakeBody(string key, float height, string name = null)
    {
        var go = Make(key, height, name);
        var mesh = MeshOf(key);
        var bc = go.AddComponent<BoxCollider>();
        if (mesh != null) { bc.center = mesh.bounds.center; bc.size = mesh.bounds.size; }
        go.AddComponent<Rigidbody>().mass = 1f;
        return go;
    }
}
