using UnityEngine;

namespace HeadingCompass;

internal sealed class CompassOverlay
{
    private GUIStyle? title;
    private GUIStyle? major;
    private GUIStyle? minor;

    internal void Draw(double heading, float horizontal, float top, float width, float scale, float opacity)
    {
        const float height = 70;
        float fit = Mathf.Max(0.01f, Mathf.Min(scale,
            Mathf.Min(Screen.width / (width + 16f), Screen.height / (height + 16f))));
        float screenWidth = Screen.width / fit;
        float screenHeight = Screen.height / fit;
        float x = Mathf.Clamp(horizontal * screenWidth - width / 2, 8, screenWidth - width - 8);
        float y = Mathf.Clamp(top / fit, 8, screenHeight - height - 8);
        Matrix4x4 oldMatrix = GUI.matrix;
        Color oldColor = GUI.color;
        int oldDepth = GUI.depth;
        try
        {
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * fit);
            GUI.depth = -400;
            if (title == null)
            {
                title = Style(18, FontStyle.Bold, new Color(0.96f, 0.82f, 0.48f));
                major = Style(13, FontStyle.Bold, new Color(0.90f, 0.94f, 0.95f));
                minor = Style(10, FontStyle.Normal, new Color(0.57f, 0.67f, 0.71f));
            }
            Paint(new Rect(x, y, width, height), new Color(0.04f, 0.07f, 0.09f, opacity));
            Paint(new Rect(x, y, width, 2), new Color(0.65f, 0.48f, 0.22f, opacity));
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y + 3, width, 26), Heading.Readout(heading), title);
            GUI.BeginGroup(new Rect(x + 12, y + 30, width - 24, 37));
            try
            {
                foreach (var tick in Heading.Ticks(heading))
                {
                    float tx = tick.Fraction * (width - 24);
                    Paint(new Rect(tx, 0, 1, tick.Major ? 10 : 5),
                        new Color(0.52f, 0.63f, 0.67f));
                    GUI.color = Color.white;
                    GUI.Label(new Rect(tx - 20, 12, 40, 20), tick.Label, tick.Major ? major! : minor!);
                }
            }
            finally { GUI.EndGroup(); }
            // Fixed center index; the heading scale scrolls beneath it.
            Paint(new Rect(x + width / 2 - 1, y + 28, 2, 13), new Color(0.96f, 0.82f, 0.48f));
        }
        finally
        {
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
            GUI.depth = oldDepth;
        }
    }

    private static GUIStyle Style(int size, FontStyle weight, Color color)
    {
        var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter,
            fontSize = size, fontStyle = weight, wordWrap = false, richText = false };
        style.normal.textColor = color;
        return style;
    }
    private static void Paint(Rect rect, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
    }
}
