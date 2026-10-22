// Runs the production overlay without Unity native calls, recording its screen geometry.
using System.Collections.Generic;

namespace UnityEngine;
public struct Vector3
{
    public float x, y, z;
    public Vector3(float a, float b, float c) { x = a; y = b; z = c; }
    public static Vector3 zero => new(0, 0, 0);
    public static Vector3 one => new(1, 1, 1);
    public static Vector3 operator *(Vector3 a, float b) => new(a.x*b, a.y*b, a.z*b);
}
public struct Quaternion { public static Quaternion identity => new(); }
public struct Matrix4x4
{
    public float Scale;
    public static Matrix4x4 TRS(Vector3 p, Quaternion q, Vector3 s) => new() { Scale = s.x };
}
public record struct Rect(float x, float y, float width, float height);
public record struct Color(float r, float g, float b, float a = 1)
{
    public static Color white => new(1, 1, 1);
}
public enum FontStyle { Normal, Bold }
public enum TextAnchor { MiddleCenter }
public enum TextClipping { Clip }
public sealed class GUIStyleState { public Color textColor; }
public sealed class GUIStyle
{
    public int fontSize;
    public FontStyle fontStyle;
    public TextAnchor alignment;
    public TextClipping clipping;
    public bool wordWrap, richText;
    public GUIStyleState normal = new();
    public GUIStyle() { }
    public GUIStyle(GUIStyle other) { }
}
public sealed class GUISkin { public GUIStyle label = new(); }
public sealed class Texture2D
{
    public static Texture2D whiteTexture = new();
    public int width = 100, height = 100;
    public static implicit operator bool(Texture2D? t) => t != null;
}
public sealed class Sprite
{
    public Texture2D texture = new();
    public Rect textureRect = new(10, 20, 30, 40);
    public static implicit operator bool(Sprite? s) => s != null;
}
public static class Time { public static float realtimeSinceStartup; }
public static class Screen { public static int width = 1920, height = 1080; }
public static class Mathf
{
    public static float Clamp(float v, float min, float max) => System.Math.Clamp(v, min, max);
    public static float Min(float a, float b) => System.Math.Min(a, b);
    public static float Max(float a, float b) => System.Math.Max(a, b);
}
public static class GUI
{
    public static Matrix4x4 matrix = new() { Scale = 1 };
    public static Color color = Color.white;
    public static int depth;
    public static GUISkin skin = new();
    public static bool ThrowLabel;
    public static bool ThrowTexture;
    public static readonly List<(Rect Rect, Rect UV)> Icons = new();
    public static readonly List<(Rect Rect, string Text)> Labels = new();
    public static readonly List<Rect> Panels = new();
    public static readonly Stack<Rect> Groups = new();
    private static Rect Map(Rect r)
    {
        float x = r.x, y = r.y;
        foreach (var g in Groups) { x += g.x; y += g.y; }
        return new(x * matrix.Scale, y * matrix.Scale, r.width * matrix.Scale, r.height * matrix.Scale);
    }
    public static void DrawTexture(Rect r, Texture2D texture) => Panels.Add(Map(r));
    public static void DrawTextureWithTexCoords(Rect r, Texture2D texture, Rect uv)
    {
        if (ThrowTexture) throw new System.InvalidOperationException("Icon failed");
        Icons.Add((Map(r), uv));
    }
    public static void Label(Rect r, string text, GUIStyle style)
    {
        if (ThrowLabel) throw new System.InvalidOperationException("Simulated drawing failure");
        Labels.Add((Map(r), text));
    }
    public static void BeginGroup(Rect r) => Groups.Push(r);
    public static void EndGroup() => Groups.Pop();
}
