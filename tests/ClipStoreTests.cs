using Cliptray;
var older = new ClipItem("older", DateTime.UnixEpoch, "match older", false);
var newer = new ClipItem("newer", DateTime.UnixEpoch.AddMinutes(1), "match newer", false);
var beforeCapture = ClipListProjection.Build(new[] { newer, older }, " match ");
if (beforeCapture.Count != 2 || beforeCapture[1].Item.Id != "older") throw new Exception("Filtered list rows lost their clip identity.");
var afterCapture = ClipListProjection.Build(new[] { new ClipItem("captured", DateTime.UnixEpoch.AddMinutes(2), "match captured", false), newer, older }, "match");
var restoredSelection = afterCapture.FirstOrDefault(row => row.Item.Id == beforeCapture[1].Item.Id);
if (restoredSelection?.Item.Text != "match older" || afterCapture[1].Item.Id == "older") throw new Exception("Refreshing history changed the selected clip identity.");
Console.WriteLine("PASS: filtered clip rows retain stable identity after history reorder");
var root = Path.Combine(Path.GetTempPath(), "openclip-" + Guid.NewGuid().ToString("N")); var store = new ClipStore(Path.Combine(root, "clips.json"));
if (!store.Add("alpha") || store.Add("alpha")) throw new Exception("Duplicate clipboard content was accepted.");
var id = store.Items[0].Id; if (!store.TogglePin(id) || !store.Items[0].IsPinned) throw new Exception("Pin toggle failed.");
store.Add("beta"); if (store.Search("ALP").Count != 1) throw new Exception("Search failed."); if (store.RemoveUnpinned() != 1 || store.Items.Count != 1) throw new Exception("Unpinned cleanup failed.");
var reloaded = new ClipStore(Path.Combine(root, "clips.json")); reloaded.Load(); if (reloaded.Items.Count != 1 || !reloaded.Items[0].IsPinned) throw new Exception("Persistence failed."); Directory.Delete(root, true); Console.WriteLine("PASS: clipboard storage, search, pinning, cleanup, and persistence");
var privacyRoot = Path.Combine(Path.GetTempPath(), "openclip-privacy-" + Guid.NewGuid().ToString("N")); var privacyPath = Path.Combine(privacyRoot, "clips.json"); var privacy = new ClipStore(privacyPath); privacy.Add("pinned"); privacy.TogglePin(privacy.Items[0].Id); privacy.Add("private"); if (privacy.ClearAll(true) != 1 || privacy.Items.Count != 1 || !privacy.Items[0].IsPinned) throw new Exception("Privacy clear did not protect pinned content."); if (File.Exists(privacyPath + ".bak")) throw new Exception("Privacy clear left deleted text in a recovery copy."); if (privacy.ClearAll(false) != 1 || privacy.Items.Count != 0) throw new Exception("Privacy clear-all failed."); Directory.Delete(privacyRoot, true); Console.WriteLine("PASS: privacy clear actions protect pinned content and purge recovery copies");

var secureRoot = Path.Combine(Path.GetTempPath(), "openclip-secure-" + Guid.NewGuid().ToString("N")); var securePath = Path.Combine(secureRoot, "clips.json"); var secure = new ClipStore(securePath, 2);
if (secure.Add("password=hunter2") || secure.Add("-----BEGIN PRIVATE KEY-----\nsecret") || secure.Add("4111 1111 1111 1111")) throw new Exception("Sensitive clipboard content was persisted.");
secure.Add("first"); secure.TogglePin(secure.Items[0].Id); secure.Add("second"); secure.Add("third");
if (secure.Items.Count != 2 || secure.Items.All(x => x.Text != "first") || secure.Items.All(x => x.Text != "third")) throw new Exception("Bounded history did not preserve pinned content.");
secure.Add("fourth"); File.WriteAllText(securePath, "{ damaged json"); var recovered = new ClipStore(securePath, 3); recovered.Load();
if (recovered.Items.Count == 0 || recovered.Items.All(x => x.Text != "first")) throw new Exception("Backup recovery failed."); recovered.Add("after recovery"); var recoveredAgain = new ClipStore(securePath, 3); recoveredAgain.Load(); if (recoveredAgain.Items.All(x => x.Text != "first") || recoveredAgain.Items.All(x => x.Text != "after recovery")) throw new Exception("Saving after recovery destroyed valid history.");
Directory.Delete(secureRoot, true); Console.WriteLine("PASS: sensitive-content exclusion, bounded history, atomic save, and backup recovery");

var sharedRoot = Path.Combine(Path.GetTempPath(), "openclip-shared-" + Guid.NewGuid().ToString("N")); var sharedPath = Path.Combine(sharedRoot, "clips.json"); using var firstStore = new ClipStore(sharedPath); using var secondStore = new ClipStore(sharedPath); firstStore.Load(); secondStore.Load(); firstStore.Add("from first process"); secondStore.Add("from second process"); using var combined = new ClipStore(sharedPath); combined.Load();
if (combined.Items.Count != 2 || combined.Items.All(x => x.Text != "from first process") || combined.Items.All(x => x.Text != "from second process")) throw new Exception("Multi-instance save lost clipboard history.");
Directory.Delete(sharedRoot, true); Console.WriteLine("PASS: serialized multi-instance mutations preserve both histories");

var boundedRoot = Path.Combine(Path.GetTempPath(), "cliptray-bounded-" + Guid.NewGuid().ToString("N"));
var boundedPath = Path.Combine(boundedRoot, "clips.json");
try
{
    using var bounded = new ClipStore(boundedPath, 2);
    if (bounded.Add(new string('x', ClipStore.MaxTextLength + 1)) || File.Exists(boundedPath)) throw new Exception("Oversized text reached disk.");
    foreach (var secret in new[] { "-----BEGIN RSA PRIVATE KEY-----", "-----BEGIN EC PRIVATE KEY-----", "-----BEGIN PGP PRIVATE KEY BLOCK-----", "export API_KEY=private", "\"password\": \"private\"" })
        if (bounded.Add(secret)) throw new Exception("Common secret format was persisted.");
    bounded.Add("pinned one"); bounded.TogglePin(bounded.Items[0].Id);
    bounded.Add("pinned two"); bounded.TogglePin(bounded.Items[0].Id);
    if (bounded.Add("cannot fit") || bounded.Items.Count != 2 || bounded.Items.Any(x => !x.IsPinned)) throw new Exception("Pinned capacity was not respected.");
    var removeId = bounded.Items[0].Id;
    if (!bounded.Remove(removeId) || bounded.Items.Any(x => x.Id == removeId) || File.Exists(boundedPath + ".bak") || bounded.Remove("missing")) throw new Exception("Selected deletion or recovery purge failed.");
    using var other = new ClipStore(boundedPath, 2); other.Add("from other process");
    bounded.Remove(bounded.Items[0].Id); other.Load();
    if (other.Items.Count != 1 || other.Items[0].Text != "from other process") throw new Exception("Selected deletion lost concurrent history.");
    File.WriteAllText(boundedPath, "[null,{\"Id\":\"ok\",\"Text\":\"valid\",\"IsPinned\":false}]");
    bounded.Load(); if (bounded.Items.Count != 1 || bounded.Items[0].Id != "ok") throw new Exception("Null history row recovery failed.");
    bounded.Add("second");
    Directory.CreateDirectory(boundedPath + ".bak-blocked");
    File.Delete(boundedPath + ".bak"); Directory.Move(boundedPath + ".bak-blocked", boundedPath + ".bak");
    var beforeFailedClear = File.ReadAllBytes(boundedPath);
    try { bounded.ClearAll(false); throw new Exception("Recovery purge failure reported success."); } catch (UnauthorizedAccessException) { } catch (IOException) { }
    if (!File.ReadAllBytes(boundedPath).SequenceEqual(beforeFailedClear)) throw new Exception("Failed privacy clear changed the primary history.");
    Directory.Delete(boundedPath + ".bak");
    var largeEntries = Enumerable.Range(0, 131).Select(i => new ClipItem("bulk-" + i, DateTime.UtcNow, i.ToString("D3") + new string('x', 65_530), i == 130)).ToArray();
    File.WriteAllText(boundedPath, System.Text.Json.JsonSerializer.Serialize(largeEntries));
    using var byteBounded = new ClipStore(boundedPath);
    byteBounded.Load();
    if (byteBounded.Items.Sum(x => (long)System.Text.Encoding.UTF8.GetByteCount(x.Text)) > ClipStore.MaxHistoryTextBytes || byteBounded.Items.All(x => x.Id != "bulk-130")) throw new Exception("History text budget failed to protect pins.");
    File.WriteAllText(boundedPath + ".bak", System.Text.Json.JsonSerializer.Serialize(new[] { new ClipItem("recovery", DateTime.UtcNow, "safe history", false) }));
    using (var oversized = new FileStream(boundedPath, FileMode.Create)) oversized.SetLength(64L * 1024 * 1024 + 1);
    byteBounded.Load();
    if (byteBounded.Items.Count != 1 || byteBounded.Items[0].Id != "recovery") throw new Exception("Oversized database did not use bounded recovery.");
    Console.WriteLine("PASS: text and database limits, expanded secret detection, pinned capacity, selected deletion, concurrent history, malformed rows, and privacy failure handling");
}
finally { Directory.Delete(boundedRoot, true); }
