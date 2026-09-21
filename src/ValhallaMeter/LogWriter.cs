using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ValhallaMeter.Core;

namespace ValhallaMeter;

internal sealed class LogWriter : IDisposable
{
    private readonly BlockingCollection<(string Path, string Text, bool Append)> queue = new BlockingCollection<(string, string, bool)>(8192);
    private readonly Task worker;
    private readonly string eventsPath;
    private volatile string error = "";
    internal string Error => error;
    internal string Directory { get; }
    internal string LogRoot { get; }
    private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
    {
        StringEscapeHandling = StringEscapeHandling.EscapeHtml,
        TypeNameHandling = TypeNameHandling.None
    };
    public LogWriter(string root)
    {
        LogRoot = Path.Combine(root, "ValhallaMeter", "logs");
        Directory = Path.Combine(LogRoot, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        System.IO.Directory.CreateDirectory(Directory);
        eventsPath = Path.Combine(Directory, "events.jsonl");
        worker = Task.Run(WriteLoop);
    }
    public void Event(CombatEvent e) => Enqueue(eventsPath, JsonConvert.SerializeObject(e, Settings) + Environment.NewLine, true);
    public void Export(Fight fight, double now)
    {
        string json = JsonConvert.SerializeObject(fight, Settings);
        string prefix = Path.Combine(Directory, "fight-" + fight.Id);
        // Serialize on the game thread; only plain text crosses to the IO worker.
        string report = Report.Create(json, now);
        Enqueue(prefix + ".json", json, false);
        Enqueue(prefix + ".html", report, false);
    }
    private void Enqueue(string path, string text, bool append)
    {
        if (!queue.IsAddingCompleted && !queue.TryAdd((path, text, append)))
            error = "Log queue full: some events were not saved";
    }
    private void WriteLoop()
    {
        StreamWriter? stream = null;
        try
        {
            stream = new StreamWriter(eventsPath, true, new UTF8Encoding(false));
            int unflushed = 0;
            while (!queue.IsCompleted)
            {
                if (!queue.TryTake(out var item, 500)) { stream.Flush(); unflushed = 0; continue; }
                if (item.Append)
                {
                    stream.Write(item.Text);
                    if (++unflushed >= 64) { stream.Flush(); unflushed = 0; }
                }
                else
                {
                    string temporary = item.Path + ".tmp";
                    File.WriteAllText(temporary, item.Text, new UTF8Encoding(false));
                    if (File.Exists(item.Path)) File.Delete(item.Path);
                    File.Move(temporary, item.Path);
                }
            }
        }
        catch (Exception ex) { error = "Log write failed: " + ex.Message; }
        finally { stream?.Dispose(); }
    }
    public void Dispose()
    {
        queue.CompleteAdding();
        if (!worker.Wait(3000)) error = "Log writer did not finish before shutdown";
    }
}
