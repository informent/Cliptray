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
    public const int MaxTextLength = 65_536;
    public const long MaxHistoryTextBytes = 8 * 1024 * 1024;
    private const long MaxDatabaseBytes = 64 * 1024 * 1024;
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
    public void Save() => WithLock(() => SaveUnlocked());

    public bool Add(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > MaxTextLength || IsSensitive(text)) return false;
        return WithLock(() =>
        {
            LoadUnlocked(); var existing = Items.FindIndex(x => x.Text == text);
            if (existing >= 0) { var item = Items[existing]; Items.RemoveAt(existing); Items.Insert(0, item with { CapturedAt = DateTime.Now }); SaveUnlocked(); return false; }
            var added = new ClipItem(Guid.NewGuid().ToString("N"), DateTime.Now, text, false);
            Items.Insert(0, added); ApplyLimit(); SaveUnlocked(); return Items.Any(x => x.Id == added.Id);
        });
    }

    public bool TogglePin(string id) => WithLock(() => { LoadUnlocked(); var index = Items.FindIndex(x => x.Id == id); if (index < 0) return false; Items[index] = Items[index] with { IsPinned = !Items[index].IsPinned }; ApplyLimit(); SaveUnlocked(); return true; });
    public int RemoveUnpinned() => WithLock(() => { LoadUnlocked(); var old = Items.Count; Items.RemoveAll(x => !x.IsPinned); SaveUnlocked(false); return old - Items.Count; });
    public int ClearAll(bool keepPinned) => WithLock(() => { LoadUnlocked(); var old = Items.Count; if (keepPinned) Items.RemoveAll(x => !x.IsPinned); else Items.Clear(); SaveUnlocked(false); return old - Items.Count; });
    public bool Remove(string id) => WithLock(() => { LoadUnlocked(); var removed = Items.RemoveAll(x => x.Id == id); if (removed == 0) return false; SaveUnlocked(false); return true; });
    public IReadOnlyList<ClipItem> Search(string query) => Items.Where(x => string.IsNullOrWhiteSpace(query) || x.Text.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();

    public static bool IsSensitive(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        try
        {
        if (Regex.IsMatch(text, @"-----BEGIN (?:RSA |EC |DSA |OPENSSH |ENCRYPTED |PGP )?PRIVATE KEY(?: BLOCK)?-----", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))) return true;
        if (Regex.IsMatch(text, @"(?im)^\s*(?:export\s+)?[\""' ]*(password|passwd|pwd|api[_-]?key|secret|access[_-]?token)[\""' ]*\s*[:=]\s*\S+", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))) return true;
        foreach (Match match in Regex.Matches(text, @"(?<!\d)(?:\d[ -]?){13,19}(?!\d)")) { var digits = new string(match.Value.Where(char.IsDigit).ToArray()); if (digits.Length is >= 13 and <= 19 && PassesLuhn(digits)) return true; }
        return false;
        }
        catch (RegexMatchTimeoutException) { return true; }
    }

    public void Dispose() => mutex.Dispose();

    private void LoadUnlocked()
    {
        var loaded = TryRead(path) ?? TryRead(path + ".bak");
        if (loaded is null && (File.Exists(path) || File.Exists(path + ".bak"))) throw new InvalidDataException("Clipboard history cannot be read safely. Existing files have been preserved.");
        Items = loaded ?? new();
        Items = Items.Where(IsValid).GroupBy(x => x.Id, StringComparer.Ordinal).Select(x => x.First()).ToList(); ApplyLimit();
    }
    private void SaveUnlocked(bool preserveRecovery = true)
    {
        var directory = Path.GetDirectoryName(path)!; Directory.CreateDirectory(directory); var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(Items, JsonOptions));
            using (var stream = new FileStream(temporary, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.WriteThrough)) stream.Flush(true);
            if (preserveRecovery) { if (TryRead(path) is not null) File.Copy(path, path + ".bak", true); }
            else DeleteRecoveryCopy();
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
    private void ApplyLimit()
    {
        var bytes = Items.Sum(x => (long)Encoding.UTF8.GetByteCount(x.Text));
        while (Items.Count > maxItems || bytes > MaxHistoryTextBytes)
        {
            var index = Items.FindLastIndex(x => !x.IsPinned);
            if (index < 0) break;
            bytes -= Encoding.UTF8.GetByteCount(Items[index].Text); Items.RemoveAt(index);
        }
    }
    private static List<ClipItem>? TryRead(string file)
    {
        try
        {
            if (!File.Exists(file)) return null;
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > MaxDatabaseBytes) return null;
            return JsonSerializer.Deserialize<List<ClipItem>>(stream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }
    private void DeleteRecoveryCopy() => File.Delete(path + ".bak");
    private static bool IsValid(ClipItem? item) => item is not null && !string.IsNullOrWhiteSpace(item.Id) && item.Id.Length <= 128 && !string.IsNullOrWhiteSpace(item.Text) && item.Text.Length <= MaxTextLength && !IsSensitive(item.Text);
    private static bool PassesLuhn(string digits) { var sum = 0; var alternate = false; for (var i = digits.Length - 1; i >= 0; i--) { var value = digits[i] - '0'; if (alternate && (value *= 2) > 9) value -= 9; sum += value; alternate = !alternate; } return sum % 10 == 0; }
}
