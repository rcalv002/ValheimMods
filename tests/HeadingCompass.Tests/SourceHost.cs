using System;
using System.Collections.Generic;
using UnityEngine;

public static class Game { public static bool m_noMap; }
public sealed class Minimap
{
    public static Minimap? instance;
    private List<PinData> m_pins = new();
    private bool[] m_visibleIconTypes = { true, true, true };
    private bool m_showSharedMapData = true;
    public List<PinData> Pins => m_pins;
    public bool[] Filters => m_visibleIconTypes;
    public void Shared(bool value) => m_showSharedMapData = value;
    public static implicit operator bool(Minimap? m) => m != null;
    public enum PinType { Icon0, Icon1, Icon2 }
    public sealed class PinData
    {
        public string m_name = "";
        public Vector3 m_pos;
        public PinType m_type;
        public bool m_save = true, m_checked;
        public long m_ownerID;
        public Sprite m_icon = new();
    }
}
public sealed class OptionalTargets
{
    public static bool Active;
    public static void CollectCompassMarkers(Action<string, string, Vector3, Texture2D, Color> add)
    {
        if (Active) add("attack", "Boar", new Vector3(10, 0, 10), new Texture2D(), new Color(1, 0, 0));
    }
}
namespace BepInEx.Bootstrap
{
    public sealed class PluginInfo { public object Instance = null!; }
    public static class Chainloader { public static readonly Dictionary<string, PluginInfo> PluginInfos = new(); }
}
