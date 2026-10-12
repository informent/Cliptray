using System.Windows;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using StackPanel = System.Windows.Controls.StackPanel;
namespace Cliptray;
public partial class MainWindow : Window
{
    private readonly ClipStore store = new(); private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(700) }; private ClipItem? selected; private string lastClipboard = ""; private bool capturePaused;
    public MainWindow() { InitializeComponent(); store.Load(); Refresh(); timer.Tick += (_, _) => CaptureClipboard(); timer.Start(); if (ClipList.Parent is System.Windows.Controls.Panel panel) { var pin = new System.Windows.Controls.Button { Content = "Pin / unpin selected", Margin = new Thickness(0, 10, 8, 0) }; pin.Click += Pin_Click; var clear = new System.Windows.Controls.Button { Content = "Clear unpinned", Margin = new Thickness(0, 10, 8, 0) }; clear.Click += Clear_Click; var pause = new System.Windows.Controls.Button { Content = "Pause capture", Margin = new Thickness(0, 10, 8, 0) }; pause.Click += Pause_Click; var wipe = new System.Windows.Controls.Button { Content = "Clear all", Margin = new Thickness(0, 10, 0, 0) }; wipe.Click += Wipe_Click; var remove = new System.Windows.Controls.Button { Content = "Delete selected", Margin = new Thickness(0, 10, 8, 0) }; remove.Click += Remove_Click; panel.Children.Add(new System.Windows.Controls.WrapPanel { Children = { pin, clear, pause, wipe, remove } }); } }
    private void CaptureClipboard() { try { if (capturePaused || !Forms.Clipboard.ContainsText()) return; var text = Forms.Clipboard.GetText(); if (text.Length > ClipStore.MaxTextLength) { StatusText("Clipboard text exceeds the 65,536 character limit"); return; } if (text != lastClipboard) { lastClipboard = text; var added = store.Add(text); Refresh(); if (!added && ClipStore.IsSensitive(text)) StatusText("Sensitive clipboard text was excluded"); } } catch { } }
    private void Refresh()
    {
        var selectedId = selected?.Id;
        var rows = ClipListProjection.Build(store.Items, SearchBox?.Text);
        ClipList.ItemsSource = rows;
        var selectedRow = rows.FirstOrDefault(row => row.Item.Id == selectedId);
        ClipList.SelectedItem = selectedRow;
        selected = selectedRow?.Item;
        if (selected is null) { SelectedMeta.Text = "Nothing selected"; PreviewText.Text = ""; }
        CountText.Text = $"{store.Items.Count:N0} items";
    }
    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => Refresh();
    private void ClipList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ClipList.SelectedItem is not ClipListRow row) { selected = null; return; }
        selected = row.Item;
        SelectedMeta.Text = $"{(selected.IsPinned ? "Pinned · " : "")}{selected.CapturedAt:dddd, MMM d · h:mm tt}";
        PreviewText.Text = selected.Text;
    }
    private void Remove_Click(object sender, RoutedEventArgs e) { if (selected is null) return; try { if (store.Remove(selected.Id)) { selected = null; Refresh(); StatusText("Deleted selected clip and recovery copy"); } } catch (Exception) { StatusText("Could not remove the clip safely. Check history folder access and try again."); } }
    private void Pin_Click(object sender, RoutedEventArgs e) { if (selected is not null && store.TogglePin(selected.Id)) { Refresh(); StatusText("Pin updated"); } }
    private void Clear_Click(object sender, RoutedEventArgs e) { try { var removed = store.RemoveUnpinned(); selected = null; Refresh(); StatusText($"Removed {removed} unpinned items"); } catch (Exception) { StatusText("Could not clear history safely. Check history folder access and try again."); } }
    private void Pause_Click(object sender, RoutedEventArgs e) { capturePaused = !capturePaused; if (sender is System.Windows.Controls.Button button) button.Content = capturePaused ? "Resume capture" : "Pause capture"; StatusText(capturePaused ? "Capture paused" : "Capture active"); }
    private void Wipe_Click(object sender, RoutedEventArgs e) { var result = System.Windows.MessageBox.Show("Clear all unpinned clipboard history? Pinned items will remain.", "Cliptray privacy", MessageBoxButton.YesNo, MessageBoxImage.Warning); if (result == MessageBoxResult.Yes) { try { var removed = store.ClearAll(true); selected = null; Refresh(); StatusText($"Cleared {removed} unpinned items"); } catch (Exception) { StatusText("Could not clear history safely. Check history folder access and try again."); } } }
    private void Copy_Click(object sender, RoutedEventArgs e) { if (selected is null) return; Forms.Clipboard.SetText(selected.Text); StatusText("Copied to clipboard"); }
    private void StatusText(string text) => Title = $"Cliptray  ·  {text}";
}
