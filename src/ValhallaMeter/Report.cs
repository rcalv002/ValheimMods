using System;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace ValhallaMeter;

internal static class Report
{
    public static string Create(string json, double now)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ValhallaMeter.report.html")!;
        using var reader = new StreamReader(stream);
        // JSON uses EscapeHtml, so names cannot terminate the script element.
        return reader.ReadToEnd().Replace("__FIGHT_DATA__", json.Replace("\u2028", "\\u2028").Replace("\u2029", "\\u2029"))
            .Replace("__REPORT_NOW__", now.ToString("R", CultureInfo.InvariantCulture));
    }
}
