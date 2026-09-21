using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using ValhallaMeter.Core;

namespace ValhallaMeter;

internal sealed class FightArchiveResult
{
    public List<Fight> Fights { get; } = new List<Fight>();
    public int Skipped { get; set; }
    public string Error { get; set; } = "";
}

internal static class FightArchive
{
    private const int MaxFights = 20;
    private const int MaxSessions = 100;
    private const int MaxCandidates = 1000;
    private const long MaxFileBytes = 8 * 1024 * 1024;
    private const long MaxLoadedBytes = 32 * 1024 * 1024;
    private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
    {
        TypeNameHandling = TypeNameHandling.None,
        MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
        MaxDepth = 16
    };

    public static FightArchiveResult Load(string logRoot, string excludeDirectory)
    {
        var result = new FightArchiveResult();
        try
        {
            if (!Directory.Exists(logRoot)) return result;
            string excluded = Path.GetFullPath(excludeDirectory).TrimEnd(Path.DirectorySeparatorChar);
            var files = new List<FileInfo>();
            foreach (var directory in new DirectoryInfo(logRoot).EnumerateDirectories()
                .Where(d => (d.Attributes & FileAttributes.ReparsePoint) == 0)
                .Where(d => !string.Equals(d.FullName.TrimEnd(Path.DirectorySeparatorChar), excluded, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(d => d.LastWriteTimeUtc).Take(MaxSessions))
            {
                try { files.AddRange(directory.EnumerateFiles("fight-*.json", SearchOption.TopDirectoryOnly)); }
                catch (Exception) { result.Skipped++; }
            }
            long loadedBytes = 0;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in files.OrderByDescending(f => f.LastWriteTimeUtc).Take(MaxCandidates))
            {
                if (result.Fights.Count >= MaxFights) break;
                try
                {
                    if (file.Length <= 0 || file.Length > MaxFileBytes || loadedBytes + file.Length > MaxLoadedBytes)
                    { result.Skipped++; continue; }
                    var fight = JsonConvert.DeserializeObject<Fight>(File.ReadAllText(file.FullName), Settings);
                    if (!Valid(fight) || !ids.Add(fight!.Id)) { result.Skipped++; continue; }
                    result.Fights.Add(fight);
                    loadedBytes += file.Length;
                }
                catch (Exception) { result.Skipped++; }
            }
        }
        catch (Exception ex) { result.Error = ex.GetType().Name + ": " + ex.Message; }
        return result;
    }

    private static bool Valid(Fight? fight)
    {
        if (fight == null || !fight.Complete || string.IsNullOrEmpty(fight.Id) || fight.Id.Length > 96 ||
            fight.Title == null || fight.Title.Length > 160 || fight.Coverage == null || fight.Coverage.Length > 500 ||
            !CombatEvent.Finite(fight.Start) || !CombatEvent.Finite(fight.End) || !CombatEvent.Finite(fight.LastCombat) ||
            fight.Start < 0 || fight.End < fight.Start || fight.LastCombat < fight.Start || fight.LastCombat > fight.End + 1 ||
            (fight.HasCombatPosition && (!CombatEvent.Finite(fight.LastX) || !CombatEvent.Finite(fight.LastY) || !CombatEvent.Finite(fight.LastZ))) ||
            fight.Events == null || fight.Events.Count == 0 || fight.Events.Count > 50000) return false;
        return fight.Events.All(e => e != null && e.Valid() && e.Time >= fight.Start - 2 && e.Time <= fight.End + 1);
    }
}
