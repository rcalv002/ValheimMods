// Headless host for the production MeterWindow. Records draw calls and supplies
// pointer input without Unity native code, a running game, or a browser.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security;
using ValhallaMeter.Core;

namespace UnityEngine
{
    public struct Vector2
    {
        public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;}
        public static Vector2 operator -(Vector2 a,Vector2 b)=>new(a.x-b.x,a.y-b.y);
        public static Vector2 operator /(Vector2 a,float b)=>new(a.x/b,a.y/b);
    }
    public struct Vector3
    {
        public float x,y,z;public Vector3(float x,float y,float z=0){this.x=x;this.y=y;this.z=z;}
        public static Vector3 zero=>new();public static Vector3 one=>new(1,1,1);
        public static Vector3 operator *(Vector3 v,float n)=>new(v.x*n,v.y*n,v.z*n);
    }
    public struct Rect
    {
        public float x,y,width,height;public Rect(float x,float y,float w,float h){this.x=x;this.y=y;width=w;height=h;}
        public Vector2 position {get=>new(x,y);set{x=value.x;y=value.y;}}
        public float yMax=>y+height;
        public bool Contains(Vector2 p)=>p.x>=x&&p.x<x+width&&p.y>=y&&p.y<y+height;
    }
    public struct Color
    {
        public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}
        public static Color white=>new(1,1,1);
        public string Hex=>$"#{(int)Math.Round(r*255):X2}{(int)Math.Round(g*255):X2}{(int)Math.Round(b*255):X2}";
    }
    public struct Color32
    {
        byte r,g,b,a;public Color32(byte r,byte g,byte b,byte a){this.r=r;this.g=g;this.b=b;this.a=a;}
        public static implicit operator Color(Color32 c)=>new(c.r/255f,c.g/255f,c.b/255f,c.a/255f);
    }
    public struct Quaternion {public static Quaternion identity=>new();}
    public struct Matrix4x4 {public float Scale,Rotation;public Vector2 Pivot;public static Matrix4x4 TRS(Vector3 p,Quaternion q,Vector3 scale)=>new(){Scale=scale.x};}
    public static class GUIUtility
    {
        public static void RotateAroundPivot(float angle,Vector2 pivot){var m=GUI.matrix;m.Rotation=angle;m.Pivot=pivot;GUI.matrix=m;}
    }
    public static class Screen {public static int width=1920,height=1080;}
    public static class Mathf {public static float Clamp(float v,float min,float max)=>Math.Clamp(v,min,max);}
    public enum EventType {Repaint}
    public sealed class Event {public static Event current=new();public EventType type=EventType.Repaint;}
    public enum TextAnchor {MiddleLeft,MiddleRight,MiddleCenter}
    public enum FontStyle {Normal,Bold}
    public enum TextClipping {Clip}
    public sealed class RectOffset {public RectOffset(int a,int b,int c,int d){}}
    public sealed class GUIStyleState {public Color textColor;}
    public sealed class GUIStyle
    {
        public int fontSize;public FontStyle fontStyle;public TextAnchor alignment;public bool richText,wordWrap;
        public TextClipping clipping;public RectOffset? padding;public GUIStyleState normal=new();
        public GUIStyle(){} public GUIStyle(GUIStyle other){}
    }
    public sealed class Texture2D {public static Texture2D whiteTexture=new();}
    public sealed class GUISkin {public GUIStyle label=new();}
    public static class GUI
    {
        public record Draw(Rect Area,string? Text,Color Color,int Size=0,bool Bold=false,TextAnchor Anchor=TextAnchor.MiddleLeft,float Rotation=0,float PivotX=0,float PivotY=0);
        public static readonly List<Draw> Commands=new();
        public static Matrix4x4 matrix=new(){Scale=1};public static Color color=Color.white;
        public static bool enabled=true;public static int depth;public static GUISkin skin=new();private static Rect group;
        public static void BeginGroup(Rect r)=>group=r;public static void EndGroup()=>group=new();
        private static Rect Map(Rect r)=>new((group.x+r.x)*matrix.Scale,(group.y+r.y)*matrix.Scale,r.width*matrix.Scale,r.height*matrix.Scale);
        public static void DrawTexture(Rect r,Texture2D texture)
        {
            float px=(group.x+matrix.Pivot.x)*matrix.Scale,py=(group.y+matrix.Pivot.y)*matrix.Scale;
            Commands.Add(new(Map(r),null,color,Rotation:matrix.Rotation,PivotX:px,PivotY:py));
        }
        public static void Label(Rect r,string text,GUIStyle style)=>Commands.Add(new(Map(r),text,style.normal.textColor,(int)(style.fontSize*matrix.Scale),style.fontStyle==FontStyle.Bold,style.alignment));
        public static string Svg()
        {
            var culture=CultureInfo.InvariantCulture;
            string N(float n)=>n.ToString("0.##",culture);
            float right=Commands.Max(c=>c.Area.x+c.Area.width),bottom=Commands.Max(c=>c.Area.yMax);
            var lines=new List<string>{$"<svg xmlns='http://www.w3.org/2000/svg' width='{N(right+24)}' height='{N(bottom+24)}'><rect width='100%' height='100%' fill='#080e13'/>"};
            foreach(var c in Commands)
            {
                var r=c.Area;
                if(c.Text==null){string transform=Math.Abs(c.Rotation)>.01?$" transform='rotate({N(c.Rotation)} {N(c.PivotX)} {N(c.PivotY)})'":"";lines.Add($"<rect x='{N(r.x)}' y='{N(r.y)}' width='{N(r.width)}' height='{N(r.height)}' fill='{c.Color.Hex}'{transform}/>");}
                else
                {
                    string anchor=c.Anchor==TextAnchor.MiddleRight?"end":c.Anchor==TextAnchor.MiddleCenter?"middle":"start";
                    float x=r.x+(anchor=="end"?r.width:anchor=="middle"?r.width/2:0);
                    lines.Add($"<text x='{N(x)}' y='{N(r.y+r.height/2)}' dominant-baseline='central' text-anchor='{anchor}' fill='{c.Color.Hex}' font-family='Segoe UI, sans-serif' font-size='{c.Size}' font-weight='{(c.Bold?600:400)}'>{SecurityElement.Escape(c.Text)}</text>");
                }
            }
            lines.Add("</svg>");return string.Join("\n",lines);
        }
    }
}
public static class ZInput
{
    public static UnityEngine.Vector3 pointerPosition;public static bool Down,Up,Held;
    public static bool GetMouseButtonDown(int i)=>Down;public static bool GetMouseButtonUp(int i)=>Up;
    public static bool GetMouseButton(int i)=>Held;public static float GetMouseScrollWheel()=>0;
}
namespace ValhallaMeter
{
    internal sealed class Setting<T> {public Setting(T data){Value=data;}public T Value { get; set; }}
    internal sealed class Plugin
    {
        public readonly CombatTracker Tracker=new();public readonly Setting<float> WindowX=new(30),WindowY=new(30),WindowScale=new(1),WindowOpacity=new(.96f);
        public readonly Setting<bool> WindowLocked=new(false);
        public bool Interacting=true;public string Issue="";public string Coverage=>"Local capture";public static double Now=30;
        public int Exports,DiagnosticExports;public Fight? LastDiagnosticFight;
        public void Export(Fight fight)=>Exports++;
        public void ExportDiagnostics(Fight? fight){DiagnosticExports++;LastDiagnosticFight=fight;}
    }
}
