using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Wpf.Ui.Common;
using Wpf.Ui.Controls;
namespace LenovoLegionToolkit.Plugin.QuickSettings;
public partial class MiniLayoutPage
{
    private QuickSettingsProvider? _provider;
    private uint _modifiers = 6, _key = 0x51;
    private bool _updating;
    private QuickSettingsItem? _moved;
    private int _moveDirection;
    private readonly List<Border> _statusRows = new(), _controlRows = new();
    private readonly List<QuickSettingsItem> _statusItems = new(), _controlItems = new();
    private readonly Dictionary<Border, CheckBox> _checks = new();
    private Border? _dragRow;
    private QuickSettingsItem? _dragItem;
    private ScrollViewer? _dragScroller;
    private double[] _dragTops = [], _dragBands = [];
    private double _dragStartY, _dragScroll, _dragViewportTop = -1, _dragViewportHeight;
    private int _dragFrom = -1, _dragTo = -1;
    private bool _dragging;
    private readonly System.Windows.Threading.DispatcherTimer _dragTimer = new() { Interval = TimeSpan.FromMilliseconds(30) };
    private const double SwapThreshold = 10; // px before a row's center that the dragged row swaps with it
    private bool _forwardingWheel;
    public MiniLayoutPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += (_, _) => { ClearDrag(); if (_provider is not null) _provider.Changed -= OnChanged; };
        _dragTimer.Tick += (_, _) =>
        {
            if (!_dragging || Mouse.LeftButton != MouseButtonState.Pressed) { ClearDrag(); return; }
            UpdateDrag(Mouse.GetPosition(this));
        };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape && _dragRow is not null) { ClearDrag(); e.Handled = true; } };
    }
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _provider = QuickSettingsProvider.Current;
            if (_provider is null) return;
            await _provider.Ready;
            if (!IsLoaded) return;
            _provider.Changed -= OnChanged; _provider.Changed += OnChanged;
            Refresh();
        }
        catch (Exception ex) { QuickSettingsProvider.Error("Load Mini Layout", ex); }
    }
    private void OnChanged(object? sender, EventArgs e) => Refresh();
    private void Refresh()
    {
        if (_provider is null || _provider.IsDisposed) return;
        ClearDrag();
        _updating = true;
        try
        {
            _enabled.IsChecked = _provider.Settings.HotkeyEnabled;
            _modifiers = _provider.Settings.Modifiers; _key = _provider.Settings.Key;
            _shortcut.Text = _provider.Settings.Shortcut;
            _warning.Text = _provider.Warning; _warning.Visibility = string.IsNullOrEmpty(_warning.Text) ? Visibility.Collapsed : Visibility.Visible;
            _items.Children.Clear();
            _statusRows.Clear(); _controlRows.Clear(); _statusItems.Clear(); _controlItems.Clear(); _checks.Clear();
            foreach (var status in new[] { true, false })
            {
                var rows = status ? _statusRows : _controlRows;
                var rowItems = status ? _statusItems : _controlItems;
                _items.Children.Add(new TextBlock { Text = status ? "Live information · read only" : "Adjustable controls", FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new(4,12,4,10) });
                var entries = _provider.Settings.Items.Where(entry => entry.Item.IsStatus() == status).ToArray();
                for (var index = 0; index < entries.Length; index++)
                {
                    var entry = entries[index];
                    var check = new CheckBox { Content = entry.Item.Title(), IsChecked = entry.Visible, VerticalAlignment = VerticalAlignment.Center, FontSize = 13, ToolTip = entry.Item.Title(), IsHitTestVisible = false };
                    // Mouse is handled by the row (drag), so this only serves keyboard Space/Enter.
                    check.Click += async (_, _) => { if (!_updating) await _provider.UpdateAsync(settings => settings.Items.First(x => x.Item == entry.Item).Visible = check.IsChecked == true); };
                    var grip = new SymbolIcon { Symbol = SymbolRegular.ReOrderDotsVertical24, Cursor = Cursors.SizeAll, Margin = new(-4, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center, Opacity = 0.55 };
                    var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                    panel.Children.Add(grip); panel.Children.Add(check);
                    var row = new Border { Child = panel, Tag = entry.Item, Cursor = Cursors.SizeAll, Padding = new(8,5,6,5), Margin = new(0,0,0,6), CornerRadius = new(6), BorderThickness = new(1) };
                    if (_moved == entry.Item)
                    {
                        var moveDirection = _moveDirection;
                        row.Loaded += (_, _) =>
                        {
                            QuickSettingsContentControl.PlayMoveAnimation(row, moveDirection);
                            row.SetResourceReference(Border.BorderBrushProperty, "SystemAccentColorPrimaryBrush");
                        };
                    }
                    row.SetResourceReference(Border.BackgroundProperty, "ControlFillColorDefaultBrush"); row.SetResourceReference(Border.BorderBrushProperty, "CardStrokeColorDefaultBrush");
                    row.MouseLeftButtonDown += Row_MouseLeftButtonDown;
                    row.MouseMove += Row_MouseMove;
                    row.MouseLeftButtonUp += Row_MouseLeftButtonUp;
                    row.LostMouseCapture += Row_LostMouseCapture;
                    _checks[row] = check; rows.Add(row); rowItems.Add(entry.Item); _items.Children.Add(row);
                }
            }
        }
        finally { _updating = false; _moved = null; }
    }
    private (List<Border> Rows, List<QuickSettingsItem> Items) Group(QuickSettingsItem item)
        => item.IsStatus() ? (_statusRows, _statusItems) : (_controlRows, _controlItems);
    private void Row_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_provider is null || _updating || sender is not Border row || row.Tag is not QuickSettingsItem item) return;
        ClearDrag();
        _dragRow = row; _dragItem = item; _dragFrom = _dragTo = -1; _dragging = false;
        _dragStartY = e.GetPosition(this).Y;
        row.CaptureMouse();
        e.Handled = true;
    }
    private void Row_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragRow is null) return;
        if (e.LeftButton != MouseButtonState.Pressed) { ClearDrag(); return; }
        var point = e.GetPosition(this);
        if (!_dragging)
        {
            if (Math.Abs(point.Y - _dragStartY) < SystemParameters.MinimumVerticalDragDistance) return;
            BeginDrag();
        }
        UpdateDrag(point);
    }
    private void Row_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var row = _dragRow;
        if (row is null) return;
        var item = _dragItem; var from = _dragFrom; var to = _dragTo; var dragging = _dragging;
        ClearDrag();
        if (row.IsMouseCaptured) row.ReleaseMouseCapture();
        if (!dragging)
        {
            if (item is not null && _checks.TryGetValue(row, out var check))
            {
                check.IsChecked = check.IsChecked != true;
                var visible = check.IsChecked == true;
                _ = _provider?.UpdateAsync(s => s.Items.First(x => x.Item == item).Visible = visible);
            }
            return;
        }
        if (item is null || to < 0 || to == from) return;
        var target = item.Value;
        _moved = target; _moveDirection = Math.Sign(to - from);
        _ = _provider?.UpdateAsync(s => Reorder(s, target, from, to));
        _preview.AnimateItem(target, _moveDirection);
    }
    private void Row_LostMouseCapture(object sender, MouseEventArgs e) { if (ReferenceEquals(sender, _dragRow)) ClearDrag(); }
    private void BeginDrag()
    {
        if (_dragRow is null || _dragItem is null) return;
        var (rows, items) = Group(_dragItem.Value);
        if (rows.Count == 0) return;
        _dragging = true;
        _dragFrom = items.IndexOf(_dragItem.Value);
        _dragTops = new double[rows.Count]; _dragBands = new double[rows.Count];
        for (var i = 0; i < rows.Count; i++)
        {
            _dragTops[i] = rows[i].TransformToAncestor(this).Transform(new Point(0, 0)).Y;
            _dragBands[i] = rows[i].ActualHeight + rows[i].Margin.Top + rows[i].Margin.Bottom;
        }
        _dragScroller = FindScrollViewer(_itemsCard);
        _dragScroll = _dragScroller?.VerticalOffset ?? 0;
        if (_dragScroller is not null)
        {
            _dragViewportTop = _dragScroller.TransformToAncestor(this).Transform(new Point(0, 0)).Y;
            _dragViewportHeight = _dragScroller.ActualHeight;
            _dragScroller.ScrollChanged += Drag_ScrollChanged;
        }
        _dragRow.Effect = new DropShadowEffect { BlurRadius = 14, ShadowDepth = 3, Direction = 270, Opacity = 0.4, Color = Colors.Black };
        _dragRow.SetResourceReference(Border.BorderBrushProperty, "SystemAccentColorPrimaryBrush");
        _dragRow.SetResourceReference(Border.BackgroundProperty, "SubtleFillColorSecondaryBrush");
        _dragRow.Opacity = 0.95;
        _dragRow.Cursor = Cursors.SizeAll;
        Panel.SetZIndex(_dragRow, 1);
        _dragTimer.Start();
    }
    private void Drag_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_dragging && e.VerticalChange != 0) UpdateDrag(Mouse.GetPosition(this), false);
    }
    private void UpdateDrag(Point point, bool autoScroll = true)
    {
        if (_dragRow is null || _dragItem is null || _dragFrom < 0) return;
        var (rows, _) = Group(_dragItem.Value);
        if (autoScroll) AutoScroll(point.Y);
        var shift = _dragScroll - (_dragScroller?.VerticalOffset ?? 0);
        var dy = point.Y - _dragStartY - shift;
        var center = _dragTops[_dragFrom] + shift + dy + _dragBands[_dragFrom] / 2;
        double BandCenter(int i) => _dragTops[i] + shift + _dragBands[i] / 2;
        _dragTo = _dragFrom;
        if (dy > 0) while (_dragTo + 1 < rows.Count && center > BandCenter(_dragTo + 1) - SwapThreshold) _dragTo++;
        else if (dy < 0) while (_dragTo - 1 >= 0 && center < BandCenter(_dragTo - 1) + SwapThreshold) _dragTo--;
        for (var i = 0; i < rows.Count; i++)
        {
            var y = i == _dragFrom ? dy
                : i > _dragFrom && i <= _dragTo ? -_dragBands[_dragFrom]
                : i < _dragFrom && i >= _dragTo ? _dragBands[_dragFrom]
                : 0;
            if (rows[i].RenderTransform is not TranslateTransform transform) { transform = new TranslateTransform(); rows[i].RenderTransform = transform; }
            transform.BeginAnimation(TranslateTransform.YProperty, null);
            transform.Y = y;
        }
    }
    private void AutoScroll(double y)
    {
        if (_dragScroller is null || _dragViewportTop < 0) return;
        var local = y - _dragViewportTop;
        if (local < 28) _dragScroller.ScrollToVerticalOffset(Math.Max(0, _dragScroller.VerticalOffset - 10));
        else if (local > _dragViewportHeight - 28) _dragScroller.ScrollToVerticalOffset(Math.Min(_dragScroller.ScrollableHeight, _dragScroller.VerticalOffset + 10));
    }
    private void ClearDrag()
    {
        _dragTimer.Stop();
        if (_dragScroller is not null) _dragScroller.ScrollChanged -= Drag_ScrollChanged;
        var capturedRow = _dragRow;
        _dragRow = null; _dragItem = null; _dragScroller = null; _dragging = false; _dragFrom = _dragTo = -1; _dragViewportTop = -1;
        foreach (var row in _statusRows.Concat(_controlRows))
        {
            if (row.RenderTransform is TranslateTransform transform)
            {
                transform.BeginAnimation(TranslateTransform.YProperty, null);
                transform.Y = 0;
            }
            row.Effect = null;
            row.Opacity = 1;
            Panel.SetZIndex(row, 0);
            row.SetResourceReference(Border.BorderBrushProperty, "CardStrokeColorDefaultBrush");
            row.SetResourceReference(Border.BackgroundProperty, "ControlFillColorDefaultBrush");
        }
        if (capturedRow?.IsMouseCaptured == true) capturedRow.ReleaseMouseCapture();
    }
    private static void Reorder(PluginSettings settings, QuickSettingsItem item, int from, int to)
    {
        var group = settings.Items.Where(x => x.Item.IsStatus() == item.IsStatus()).ToList();
        if (from < 0 || to < 0 || from >= group.Count || to >= group.Count) return;
        var entry = group[from];
        group.RemoveAt(from);
        group.Insert(to, entry);
        var slots = settings.Items.Select((x, i) => (x, i)).Where(p => p.x.Item.IsStatus() == item.IsStatus()).Select(p => p.i).ToArray();
        for (var i = 0; i < slots.Length; i++) settings.Items[slots[i]] = group[i];
    }
    private void Shortcut_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab) return;
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
        if (key == Key.Escape) { Refresh(); return; }
        var modifiers = Keyboard.Modifiers;
        _modifiers = (uint)(((modifiers & ModifierKeys.Alt) != 0 ? 1 : 0) | ((modifiers & ModifierKeys.Control) != 0 ? 2 : 0) | ((modifiers & ModifierKeys.Shift) != 0 ? 4 : 0) | ((modifiers & ModifierKeys.Windows) != 0 ? 8 : 0));
        if (_modifiers == 0) { _warning.Text = "Include Ctrl, Alt, Shift or Win."; _warning.Visibility = Visibility.Visible; return; }
        _key = (uint)KeyInterop.VirtualKeyFromKey(key); _shortcut.Text = PluginSettings.FormatHotkey(_modifiers, _key);
    }
    private async void ApplyHotkey_Click(object sender, RoutedEventArgs e) { if (_provider is not null && _modifiers != 0) await _provider.UpdateAsync(s => { s.Modifiers = _modifiers; s.Key = _key; }, true); }
    private async void ResetHotkey_Click(object sender, RoutedEventArgs e) { if (_provider is not null) await _provider.UpdateAsync(s => { s.Modifiers = 6; s.Key = 0x51; }, true); }
    private async void Enabled_Click(object sender, RoutedEventArgs e) { if (_provider is not null && !_updating) await _provider.UpdateAsync(s => s.HotkeyEnabled = _enabled.IsChecked == true, true); }
    private async void ResetLayout_Click(object sender, RoutedEventArgs e) { if (_provider is not null) await _provider.UpdateAsync(s => s.Items = PluginSettings.Defaults()); }
    private void Preview_SizeChanged(object sender, SizeChangedEventArgs e) => _preview.Clip = new RectangleGeometry(new Rect(e.NewSize), 13, 13);
    private void Preview_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_forwardingWheel) return;
        var scroller = FindScrollViewer(_preview);
        if (scroller is null) return;
        // ponytail: the re-raised event tunnels back through this Border; let it pass so the inner scroller handles it natively, exactly like the flyout list.
        _forwardingWheel = true;
        try { scroller.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = UIElement.MouseWheelEvent, Source = scroller }); }
        finally { _forwardingWheel = false; }
        e.Handled = true;
    }
    private static ScrollViewer? FindScrollViewer(DependencyObject node)
    {
        if (node is ScrollViewer scroller) return scroller;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            if (FindScrollViewer(VisualTreeHelper.GetChild(node, i)) is { } found) return found;
        return null;
    }
    private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_workspace is null) return;
        ClearDrag();
        var narrow = e.NewSize.Width < 1050;
        _workspace.ColumnDefinitions.Clear(); _workspace.RowDefinitions.Clear();
        _workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _workspace.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        if (narrow) _workspace.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        else _workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(460) });
        Grid.SetColumn(_previewCard, narrow ? 0 : 1); Grid.SetRow(_previewCard, narrow ? 1 : 0);
        _itemsCard.Margin = narrow ? new Thickness(0,0,0,8) : new Thickness(0,0,10,0);
        _previewCard.Margin = narrow ? new Thickness(0,8,0,0) : new Thickness(10,0,0,0);
    }
}
