using System.Collections.Generic;

namespace ValhallaMeter.Core;

/// <summary>History selection follows a fight object, never a moving list index.</summary>
public sealed class FightSelection
{
    private Fight? pinned;
    public bool IsLive => pinned == null;
    public Fight? Resolve(Fight? current, IReadOnlyList<Fight> history) => pinned ?? current ?? (history.Count > 0 ? history[0] : null);
    public void Live() => pinned = null;
    public int Index(IReadOnlyList<Fight> history)
    {
        for (int i = 0; i < history.Count; i++) if (history[i].Id == pinned?.Id) return i;
        return -1;
    }
    public bool CanOlder(Fight? current, IReadOnlyList<Fight> history)
    {
        if (history.Count == 0) return false;
        if (IsLive) return current != null || history.Count > 1;
        int index = Index(history);
        return index >= 0 && index + 1 < history.Count;
    }
    public bool Older(Fight? current, IReadOnlyList<Fight> history)
    {
        if (!CanOlder(current, history)) return false;
        // Live already displays history[0] when idle: skip that duplicate selection.
        int next = IsLive ? (current == null ? 1 : 0) : Index(history) + 1;
        pinned = history[next];
        return true;
    }
    public bool Newer(IReadOnlyList<Fight> history)
    {
        if (IsLive) return false;
        int index = Index(history);
        if (index == 0 || history.Count == 0) Live();
        else pinned = history[index < 0 ? history.Count - 1 : index - 1];
        return true;
    }
}

/// <summary>Click semantics independent of Unity IMGUI's legacy input events.</summary>
public sealed class PointerClick
{
    private string? pressed;
    public string? Update(bool interactive, bool down, bool up, string? hovered)
    {
        if (!interactive) { pressed = null; return null; }
        if (down) pressed = hovered;
        if (!up) return null;
        string? activated = pressed != null && pressed == hovered ? pressed : null;
        pressed = null;
        return activated;
    }
}
