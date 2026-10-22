using System.Collections.Generic;
using UnityEngine;
using TargetCall;

public record struct ZDOID(long UserID, uint ID);
public class Character
{
    public bool Dead, Player;
    public string Display = "Creature";
    public Transform transform = new();
    public bool IsDead() => Dead;
    public bool IsPlayer() => Player;
    public static implicit operator bool(Character? c) => c != null;
}
public sealed class Player : Character { public static Player? m_localPlayer; }

namespace UnityEngine
{
    public record struct Vector3(float x, float y, float z);
    public sealed class Transform { public Vector3 position; }
    public sealed class Texture2D { }
    public record struct Color(float r, float g, float b, float a = 1);
    public record struct Color32(byte r, byte g, byte b, byte a)
    {
        public static implicit operator Color(Color32 c) => new(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f);
    }
}
namespace TargetCall
{
    public sealed partial class Plugin
    {
        private static Plugin? instance;
        private TargetSync sync = null!, leaderSync = null!;
        private readonly PersonalMarker personal = new();
        private Texture2D attackIcon = new(), defendIcon = new(), leaderIcon = new(), personalIcon = new();
        internal static readonly Dictionary<ZDOID, Character> Characters = new();
        internal static Plugin StartCompass(TargetSync calls, TargetSync leader)
        {
            instance = new Plugin { sync = calls, leaderSync = leader };
            return instance;
        }
        internal PersonalMarker Personal => personal;
        private void EnsureStyle() { }
        private static Character? FindCharacter(long user, uint id) => Characters.GetValueOrDefault(new ZDOID(user, id));
        private static string Name(Character c) => c.Display;
        public static implicit operator bool(Plugin? p) => p != null;
    }
}
