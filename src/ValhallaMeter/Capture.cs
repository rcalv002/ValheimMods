using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using HarmonyLib;
using ValhallaMeter.Core;

namespace ValhallaMeter;

internal static class Capture
{
    internal sealed class HealthContext
    {
        public Character Character = null!;
        public HitData? Hit;
        public string Kind = "damage";
    }
    internal sealed class WeaponContext
    {
        public Character? Character;
        public string Weapon = "";
        public string Ability = "";
    }
    private sealed class Tag { public string Key = ""; }
    internal static readonly Stack<HealthContext> Health = new Stack<HealthContext>();
    internal static readonly Stack<WeaponContext> Weapons = new Stack<WeaponContext>();
    private static readonly ConditionalWeakTable<HitData, Tag> Tags = new ConditionalWeakTable<HitData, Tag>();
    private static readonly DamageFallbackMatcher Fallbacks = new DamageFallbackMatcher();

    internal static Actor ActorOf(Character? c)
    {
        if (!c) return new Actor();
        if (c is Player p) return new Actor { Id = "player:" + p.GetPlayerID(), Name = Clean(p.GetPlayerName()), Player = true };
        return new Actor { Id = "entity:" + c.GetZDOID(), Name = Clean(c.GetHoverName()) };
    }
    internal static string Clean(string? value) => string.IsNullOrEmpty(value) ? "Unknown" :
        new string(value!.Where(c => !char.IsControl(c) && c != '<' && c != '>').Take(150).ToArray());
    internal static string Localize(string value) => Clean(Localization.instance != null ? Localization.instance.Localize(value) : value);
    internal static string Fingerprint(Character target, HitData hit)
    {
        var package = new ZPackage();
        hit.Serialize(ref package);
        using var sha = SHA256.Create();
        return target.GetZDOID() + ":" + Convert.ToBase64String(sha.ComputeHash(package.GetArray()));
    }
    internal static void TagHit(Character target, HitData hit) => Tags.GetOrCreateValue(hit).Key = Fingerprint(target, hit);
    internal static void Outgoing(Character target, HitData hit)
    {
        if (Weapons.Count == 0) return;
        var origin = Weapons.Peek();
        if (!origin.Character || !origin.Character!.IsPlayer() || hit.GetAttacker() != origin.Character) return;
        var e = new CombatEvent { Kind = "metadata", Source = ActorOf(origin.Character), Target = ActorOf(target),
            Correlation = Fingerprint(target, hit), Weapon = origin.Weapon, Ability = origin.Ability };
        var plugin = Plugin.Instance;
        plugin?.Publish(e);
        if (plugin != null && plugin.LocalDamageFallbackEnabled && origin.Character == Player.m_localPlayer && !target.IsOwner())
        {
            var point = hit.m_point;
            Fallbacks.Add(new PendingDamage { Time = Plugin.Now, X = point.x, Y = point.y, Z = point.z,
                RemainingHealth = Math.Max(0, target.GetHealth()), Source = e.Source, Target = e.Target,
                Ability = e.Ability, Weapon = e.Weapon, Correlation = e.Correlation });
        }
    }
    internal static void DamageText(UnityEngine.Vector3 point, string text)
    {
        var plugin = Plugin.Instance;
        if (plugin == null || !plugin.LocalDamageFallbackEnabled ||
            !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double amount)) return;
        var hit = Fallbacks.Match(Plugin.Now, point.x, point.y, point.z, amount);
        if (hit != null) plugin.Publish(hit);
    }
    internal static void ClearFallbacks() => Fallbacks.Clear();
    internal static void HealthChanged(Character target, float before)
    {
        if (Health.Count == 0 || !target.IsOwner()) return;
        var context = Health.Peek();
        if (context.Character != target) return;
        double after = Math.Max(0, target.GetHealth());
        double amount = context.Kind == "damage" ? Math.Max(0, before - after) : Math.Max(0, after - before);
        if (amount <= 0) return;
        var hit = context.Hit;
        var e = new CombatEvent { Kind = context.Kind, Target = ActorOf(target), Amount = amount,
            Source = hit != null ? ActorOf(hit.GetAttacker()) : new Actor(),
            Ability = hit != null ? (hit.m_skill != Skills.SkillType.None ? hit.m_skill.ToString() : hit.m_hitType.ToString()) : "Healing / regeneration" };
        if (hit != null)
        {
            if (Tags.TryGetValue(hit, out var tag)) e.Correlation = tag.Key;
            var d = hit.m_damage;
            var parts = new Dictionary<string, double> { ["Blunt"] = d.m_blunt, ["Slash"] = d.m_slash,
                ["Pierce"] = d.m_pierce, ["Fire"] = d.m_fire, ["Frost"] = d.m_frost, ["Lightning"] = d.m_lightning,
                ["Poison"] = d.m_poison, ["Spirit"] = d.m_spirit, ["Chop"] = d.m_chop,
                ["Pickaxe"] = d.m_pickaxe, ["Generic"] = d.m_damage, ["NonPlayer"] = d.m_nonPlayer };
            double total = parts.Values.Where(v => v > 0).Sum();
            if (total > 0) foreach (var part in parts.Where(p => p.Value > 0)) e.Types[part.Key] = amount * part.Value / total;
        }
        Plugin.Instance?.Publish(e);
    }
}

[HarmonyPatch(typeof(DamageText), "RPC_DamageText")]
internal static class DamageTextPatch
{
    private static void Prefix(ZPackage pkg) => Plugin.Guard(() =>
    {
        var copy = new ZPackage(pkg.GetArray());
        int type = copy.ReadInt();
        var point = copy.ReadVector3();
        string text = copy.ReadString();
        copy.ReadBool();
        if (type >= (int)DamageText.TextType.Normal && type <= (int)DamageText.TextType.Immune)
            Capture.DamageText(point, text);
    });
}

[HarmonyPatch(typeof(Character), "RPC_Damage")]
internal static class IncomingHitPatch
{
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    private static void Prefix(Character __instance, HitData hit) => Plugin.Guard(() => Capture.TagHit(__instance, hit));
}

[HarmonyPatch(typeof(Character), nameof(Character.Damage))]
internal static class OutgoingHitPatch
{
    [HarmonyPrefix, HarmonyPriority(Priority.Last)]
    private static void Prefix(Character __instance, HitData hit) => Plugin.Guard(() => Capture.Outgoing(__instance, hit));
}

[HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
internal static class ApplyDamagePatch
{
    private static void Prefix(Character __instance, HitData hit, out Capture.HealthContext __state)
    {
        __state = new Capture.HealthContext { Character = __instance, Hit = hit };
        Capture.Health.Push(__state);
    }
    // Always unwind, including when another mod or the game throws. Never suppress exceptions.
    private static void Finalizer() { if (Capture.Health.Count > 0) Capture.Health.Pop(); }
}

[HarmonyPatch(typeof(Character), "RPC_Heal")]
internal static class HealPatch
{
    private static void Prefix(Character __instance) => Capture.Health.Push(new Capture.HealthContext { Character = __instance, Kind = "heal" });
    private static void Finalizer() { if (Capture.Health.Count > 0) Capture.Health.Pop(); }
}

[HarmonyPatch(typeof(Character), nameof(Character.SetHealth))]
internal static class HealthWritePatch
{
    [HarmonyPrefix, HarmonyPriority(Priority.Last)]
    private static void Prefix(Character __instance, out float __state)
    {
        __state = 0;
        if (Capture.Health.Count > 0) __state = Math.Max(0, __instance.GetHealth());
    }
    [HarmonyPostfix, HarmonyPriority(Priority.First)]
    private static void Postfix(Character __instance, float __state) => Plugin.Guard(() => Capture.HealthChanged(__instance, __state));
}

[HarmonyPatch(typeof(SE_Shield), nameof(SE_Shield.OnDamaged))]
internal static class ShieldPatch
{
    private static void Prefix(HitData hit, out float __state) => __state = hit.GetTotalDamage();
    private static void Postfix(SE_Shield __instance, HitData hit, Character attacker, float __state) => Plugin.Guard(() =>
    {
        if (!__instance.m_character || !__instance.m_character.IsOwner()) return;
        double absorbed = Math.Max(0, __state - hit.GetTotalDamage());
        if (absorbed > 0) Plugin.Instance?.Publish(new CombatEvent { Kind = "shield", Source = Capture.ActorOf(attacker),
            Target = Capture.ActorOf(__instance.m_character), Amount = absorbed, Ability = "Shield absorption (before armor)" });
    });
}

[HarmonyPatch(typeof(Player), nameof(Player.OnDeath))]
internal static class DeathPatch
{
    private static void Prefix(Player __instance) => Plugin.Guard(() =>
    {
        if (__instance.IsOwner()) Plugin.Instance?.Publish(new CombatEvent { Kind = "death", Target = Capture.ActorOf(__instance), Ability = "Death" });
    });
}

[HarmonyPatch]
internal static class AttackContextPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Attack), "OnAttackTrigger");
        yield return AccessTools.Method(typeof(Attack), "FireProjectileBurst");
    }
    private static void Prefix(Humanoid ___m_character, ItemDrop.ItemData ___m_weapon, Attack __instance)
    {
        var context = new Capture.WeaponContext { Character = ___m_character };
        Plugin.Guard(() =>
        {
            context.Weapon = ___m_weapon != null ? Capture.Localize(___m_weapon.m_shared.m_name) : "Unarmed";
            context.Ability = __instance.m_attackType.ToString();
        });
        Capture.Weapons.Push(context);
    }
    private static void Finalizer() { if (Capture.Weapons.Count > 0) Capture.Weapons.Pop(); }
}

[HarmonyPatch(typeof(Projectile), nameof(Projectile.OnHit))]
internal static class ProjectileContextPatch
{
    private static void Prefix(Character ___m_owner, ItemDrop.ItemData ___m_weapon)
    {
        var context = new Capture.WeaponContext { Character = ___m_owner, Ability = "Projectile" };
        Plugin.Guard(() => context.Weapon = ___m_weapon != null ? Capture.Localize(___m_weapon.m_shared.m_name) : "Unknown projectile");
        Capture.Weapons.Push(context);
    }
    private static void Finalizer() { if (Capture.Weapons.Count > 0) Capture.Weapons.Pop(); }
}

[HarmonyPatch(typeof(Player), nameof(Player.UseStamina))]
internal static class StaminaPatch
{
    private static void Prefix(Player __instance, out float __state) => __state = __instance.GetStamina();
    private static void Postfix(Player __instance, float __state) => Plugin.Guard(() =>
    {
        double spent = Math.Max(0, __state - __instance.GetStamina());
        if (__instance.IsOwner() && spent > 0) Plugin.Instance?.AccumulateResource("stamina", spent);
    });
}

[HarmonyPatch(typeof(Player), nameof(Player.UseEitr))]
internal static class EitrPatch
{
    private static void Prefix(Player __instance, out float __state) => __state = __instance.GetEitr();
    private static void Postfix(Player __instance, float __state) => Plugin.Guard(() =>
    {
        double spent = Math.Max(0, __state - __instance.GetEitr());
        if (__instance.IsOwner() && spent > 0) Plugin.Instance?.AccumulateResource("eitr", spent);
    });
}
