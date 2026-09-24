using System;

namespace ChatTranslator;

internal static class ChatLine
{
    internal static string? WithTranslation(string line, string translation)
    {
        if (string.IsNullOrWhiteSpace(translation) || translation.Length > 1200) return null;
        int close = line.LastIndexOf("</color>", StringComparison.Ordinal);
        if (close < 0 || close != line.Length - "</color>".Length) return null;
        int open = line.LastIndexOf('>', close);
        if (open < 0 || open >= close) return null;
        string safe = translation.Trim().Replace('<', ' ').Replace('>', ' ')
            .Replace('\r', ' ').Replace('\n', ' ');
        return line.Substring(0, open + 1) + safe + line.Substring(close);
    }
}
