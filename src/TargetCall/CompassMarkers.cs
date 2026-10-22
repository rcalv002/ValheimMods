using System;
using System.Collections.Generic;
using UnityEngine;

namespace TargetCall;

public sealed partial class Plugin
{
    private readonly List<ZNet.PlayerInfo> compassPlayers = new List<ZNet.PlayerInfo>();

    /// <summary>Optional local compass API. Reports at most three active channels using shared Unity types.
    /// Icons are owned by Target Call; consumers must not modify or destroy them. Call on the main thread.</summary>
    public static void CollectCompassMarkers(Action<string, string, Vector3, Texture2D, Color> add)
    {
        var plugin = instance;
        if (add == null || plugin == null || !plugin || !Player.m_localPlayer || !ZNet.instance) return;
        plugin.EnsureStyle();
        plugin.compassPlayers.Clear();
        ZNet.instance.GetOtherPublicPlayers(plugin.compassPlayers);
        double now = Time.realtimeSinceStartupAsDouble;
        if (plugin.sync.State.Active(now))
            plugin.CompassTarget(plugin.sync.State.UserId, plugin.sync.State.ObjectId, "call", plugin.sync.State.Focused, add);
        if (plugin.leaderSync.State.Active(now))
            plugin.CompassTarget(plugin.leaderSync.State.UserId, plugin.leaderSync.State.ObjectId, "commander", false, add);
        if (plugin.personal.Active)
            plugin.CompassTarget(plugin.personal.UserId, plugin.personal.ObjectId, "personal", false, add);
    }

    private void CompassTarget(long user, uint id, string kind, bool focused,
        Action<string, string, Vector3, Texture2D, Color> add)
    {
        Character? creature = FindCharacter(user, id);
        bool isPlayer;
        Vector3 position;
        string name;
        if (creature != null && creature)
        {
            if (creature.IsDead()) return;
            isPlayer = creature.IsPlayer();
            position = creature.transform.position;
            name = Name(creature);
        }
        else
        {
            // Only use positions the player has explicitly shared through Valheim.
            int index = compassPlayers.FindIndex(p => p.m_characterID == new ZDOID(user, id));
            if (index < 0) return;
            var player = compassPlayers[index];
            isPlayer = true;
            position = player.m_position;
            name = player.m_name;
        }
        if (kind == "commander" && !isPlayer) return;
        if (kind == "call") kind = isPlayer ? "assist" : "attack";
        Texture2D icon = kind == "personal" ? personalIcon! : kind == "commander" ? leaderIcon! :
            isPlayer ? defendIcon! : attackIcon!;
        Color color = focused ? new Color32(246, 194, 88, 255) :
            kind == "commander" ? new Color32(249, 209, 82, 255) :
            kind == "personal" ? new Color32(231, 242, 248, 255) :
            isPlayer ? new Color32(89, 216, 220, 255) : new Color32(244, 113, 93, 255);
        add(kind, name, position, icon, color);
    }
}
