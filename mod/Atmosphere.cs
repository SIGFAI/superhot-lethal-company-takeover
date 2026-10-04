// Lethal Company mood: the white SUPERHOT facility turns into a dark, murky industrial interior lit by your flashlight.
using Sigf.Kit;
using UnityEngine;

public static class Atmosphere
{
    public static readonly Color Fog = new Color(0.04f, 0.05f, 0.04f);
    public static readonly Color Concrete = new Color(0.13f, 0.12f, 0.10f);
    static Light torch;
    static GameObject torchGo;
    public static bool TorchOn = true;

    public static void Apply()
    {
        var g = GlobalShaderValueControl.Instance;
        if (g != null)
        {
            g.GlobalConcreteAlbedo = Concrete;
            g.GlobalFogColor = Fog;
            g.GlobalFogColorOverridePower = 1f;
            g.GlobalFogPower = 1.6f;
        }
        RenderSettings.ambientLight = new Color(0.05f, 0.05f, 0.05f);
        RenderSettings.ambientIntensity = 0.3f;
        MakeTorch();
        Mix.Play(Mix.Sound("sfx/flashlight_click.wav"), null, 0.7f);
    }

    static void MakeTorch()
    {
        var cam = G.Cam;
        if (cam == null) return;
        if (torchGo != null) Object.Destroy(torchGo);
        torchGo = new GameObject("SigfTorch");
        torchGo.transform.SetParent(cam.transform, false);
        torchGo.transform.localPosition = new Vector3(0.25f, -0.2f, 0.2f);
        torch = torchGo.AddComponent<Light>();
        torch.type = LightType.Spot;
        torch.color = new Color(1f, 0.93f, 0.75f);
        torch.range = 40f;
        torch.spotAngle = 55f;
        torch.intensity = 3f;
        torch.shadows = LightShadows.Soft;
    }

    static AudioSource drone;
    /// <summary>A low looping facility hum under everything.</summary>
    public static void StartDrone()
    {
        if (drone != null) return;
        var go = new GameObject("SigfDrone");
        Object.DontDestroyOnLoad(go);
        drone = go.AddComponent<AudioSource>();
        drone.clip = Mix.Sound("sfx/drone.wav");
        drone.loop = true; drone.volume = 0.35f; drone.spatialBlend = 0f;
        drone.ignoreListenerPause = true;
        drone.Play();
    }

    public static void Toggle()
    {
        TorchOn = !TorchOn;
        if (torch != null) torch.enabled = TorchOn;
    }
}
