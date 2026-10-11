using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Cliptray;

public sealed record ClipItem(string Id, DateTime CapturedAt, string Text, bool IsPinned);
public sealed record ClipListRow(ClipItem Item, string DisplayText)
{
    public override string ToString() => DisplayText;
}

public static class ClipListProjection
{
    public static IReadOnlyList<ClipListRow> Build(IEnumerable<ClipItem> items, string? query)
    {
        ArgumentNullException.ThrowIfNull(items);
        var normalizedQuery = query?.Trim() ?? string.Empty;
        return items
            .Where(item => normalizedQuery.Length == 0 || item.Text.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .Select(item => new ClipListRow(item, $"{item.CapturedAt:g}    {item.Text.Replace(Environment.NewLine, " ")}"))
            .ToArray();
    }
}

public sealed class ClipStore : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string path;
    private readonly int maxItems;
    private readonly Mutex mutex;
    public List<ClipItem> Items { get; private set; } = new();

    public ClipStore(string? storagePath = null, int maxItems = 500)
    {
        if (maxItems < 1) throw new ArgumentOutOfRangeException(nameof(maxItems));
        path = Path.GetFullPath(storagePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenClip", "clips.json"));
        this.maxItems = maxItems;
        var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path.ToUpperInvariant())))[..24];
        mutex = new Mutex(false, $@"Local\OpenClip-{identity}");
    }

    public void Load() => WithLock(LoadUnlocked);
    public void Save() => WithLock(SaveUnlocked);

    public bool Add(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || IsSensitive(text)) return false;
        return WithLock(() =>
        {
            LoadUnlocked(); var existing = Items.FindIndex(x => x.Text == text);
            if (existing >= 0) { var item = Items[existing]; Items.RemoveAt(existing); Items.Insert(0, item with { CapturedAt = DateTime.Now }); SaveUnlocked(); return false; }
            Items.Insert(0, new ClipItem(Guid.NewGuid().ToString("N"), DateTime.Now, text, false)); ApplyLimit(); SaveUnlocked(); return true;
        });
    }

    public bool TogglePin(string id) => WithLock(() => { LoadUnlocked(); var index = Items.FindIndex(x => x.Id == id); if (index < 0) return false; Items[index] = Items[index] with { IsPinned = !Items[index].IsPinned }; ApplyLimit(); SaveUnlocked(); return true; });
    public int RemoveUnpinned() => WithLock(() => { LoadUnlocked(); var old = Items.Count; Items.RemoveAll(x => !x.IsPinned); SaveUnlocked(); DeleteRecoveryCopy(); return old - Items.Count; });
    public int ClearAll(bool keepPinned) => WithLock(() => { LoadUnlocked(); var old = Items.Count; if (keepPinned) Items.RemoveAll(x => !x.IsPinned); else Items.Clear(); SaveUnlocked(); DeleteRecoveryCopy(); return old - Items.Count; });
    public IReadOnlyList<ClipItem> Search(string query) => Items.Where(x => string.IsNullOrWhiteSpace(query) || x.Text.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();

    public static bool IsSensitive(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (text.Contains("-----BEGIN PRIVATE KEY-----", StringComparison.OrdinalIgnoreCase) || text.Contains("-----BEGIN OPENSSH PRIVATE KEY-----", StringComparison.OrdinalIgnoreCase)) return true;
        if (Regex.IsMatch(text, @"(?im)^\s*(password|passwd|pwd|api[_-]?key|secret|access[_-]?token)\s*[:=]\s*\S+")) return true;
        foreach (Match match in Regex.Matches(text, @"(?<!\d)(?:\d[ -]?){13,19}(?!\d)")) { var digits = new string(match.Value.Where(char.IsDigit).ToArray()); if (digits.Length is >= 13 and <= 19 && PassesLuhn(digits)) return true; }
        return false;
    }

    public void Dispose() => mutex.Dispose();

    private void LoadUnlocked()
    {
        Items = TryRead(path) ?? TryRead(path + ".bak") ?? new();
        Items = Items.Where(IsValid).GroupBy(x => x.Id, StringComparer.Ordinal).Select(x => x.First()).ToList(); ApplyLimit();
    }
    private void SaveUnlocked()
    {
        var directory = Path.GetDirectoryName(path)!; Directory.CreateDirectory(directory); var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(Items, JsonOptions));
            using (var stream = new FileStream(temporary, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.WriteThrough)) stream.Flush(true);
            if (TryRead(path) is not null) File.Copy(path, path + ".bak", true);
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private T WithLock<T>(Func<T> action)
    {
        var acquired = false;
        try
        {
            try { acquired = mutex.WaitOne(TimeSpan.FromSeconds(5)); } catch (AbandonedMutexException) { acquired = true; }
            if (!acquired) throw new IOException("Clipboard history is busy in another Cliptray process.");
            return action();
        }
        finally { if (acquired) mutex.ReleaseMutex(); }
    }
    private void WithLock(Action action) => WithLock(() => { action(); return true; });
    private void ApplyLimit() { while (Items.Count > maxItems) { var index = Items.FindLastIndex(x => !x.IsPinned); if (index < 0) break; Items.RemoveAt(index); } }
    private static List<ClipItem>? TryRead(string file) { try { return File.Exists(file) ? JsonSerializer.Deserialize<List<ClipItem>>(File.ReadAllText(file)) : null; } catch { return null; } }
    private void DeleteRecoveryCopy() { try { File.Delete(path + ".bak"); } catch { } }
    private static bool IsValid(ClipItem item) => !string.IsNullOrWhiteSpace(item.Id) && !string.IsNullOrWhiteSpace(item.Text) && !IsSensitive(item.Text);
    private static bool PassesLuhn(string digits) { var sum = 0; var alternate = false; for (var i = digits.Length - 1; i >= 0; i--) { var value = digits[i] - '0'; if (alternate && (value *= 2) > 9) value -= 9; sum += value; alternate = !alternate; } return sum % 10 == 0; }
}
