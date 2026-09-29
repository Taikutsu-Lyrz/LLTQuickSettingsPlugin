using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Listeners;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
namespace LenovoLegionToolkit.Plugin.QuickSettings;

internal sealed class ActionFeatureControl : UserControl
{
    private readonly QuickSettingsItem _item;
    private readonly bool _preview;
    private readonly TextBlock _status = new() { FontSize = 14, TextWrapping = TextWrapping.NoWrap };
    private readonly Button _action = new() { MinWidth = 150, HorizontalAlignment = HorizontalAlignment.Right };
    private readonly ToggleSwitch _toggle = new() { HorizontalAlignment = HorizontalAlignment.Right };
    private bool _busy;
    private readonly System.Windows.Controls.MenuItem _kill = new() { Header = "Close GPU apps" };
    public ActionFeatureControl(QuickSettingsItem item, bool preview)
    {
        _item = item; _preview = preview;
        Margin = new(0,0,0,8);
        var panel = new StackPanel();
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(new SymbolIcon { Symbol = item.Icon(), FontSize = 20, Margin = new(0,0,10,0) });
        header.Children.Add(new TextBlock { Text = item.Title(), FontSize = 14, FontWeight = FontWeights.Medium });
        panel.Children.Add(header);
        if (item == QuickSettingsItem.OverclockDiscreteGpu)
        { panel.Children.Add(_toggle); _toggle.Click += OnAction; }
        else
        {
            if (item == QuickSettingsItem.DiscreteGpu)
            {
                var description = new TextBlock { Text = "Turn off the discrete GPU when it is idle.", FontSize = 12, Margin = new(30,3,0,14), TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap };
                description.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
                panel.Children.Add(description); panel.Children.Add(_status);
                _action.Content = "Deactivate ▾";
                var menu = new ContextMenu();
                menu.Opened += (_, _) => { if (QuickSettingsProvider.Current is { } provider) provider.ModalDepth++; };
                menu.Closed += (_, _) => { if (QuickSettingsProvider.Current is { } provider) provider.ModalDepth = Math.Max(0, provider.ModalDepth - 1); };
                var restart = new System.Windows.Controls.MenuItem { Header = "Restart GPU" };
                menu.Items.Add(_kill); menu.Items.Add(restart);
                _kill.Click += async (_, _) => await GpuActionAsync(false);
                restart.Click += async (_, _) => await GpuActionAsync(true);
                _action.ContextMenu = menu;
            }
            else _action.Content = "Turn off";
            _action.Margin = new(0,10,0,0); _action.Click += OnAction; panel.Children.Add(_action);
        }
        var card = new Border { Padding = new(16), CornerRadius = new(8), BorderThickness = new(1), Child = panel };
        card.SetResourceReference(Border.BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
        card.SetResourceReference(Border.BorderBrushProperty, "CardStrokeColorDefaultBrush"); Content = card;
    }
    public async Task RefreshAsync()
    {
        if (_busy) return;
        try
        {
            if (_item == QuickSettingsItem.DiscreteGpu)
            {
                var gpu = IoCContainer.Resolve<GPUController>();
                if (!gpu.IsSupported()) { Unsupported(); return; }
                var status = await gpu.RefreshNowAsync();
                if (status.State is GPUState.Unknown or GPUState.NvidiaGpuNotFound) { Unsupported(); return; }
                _status.Text = status.State.ToString();
                _action.IsEnabled = status.State is GPUState.Active or GPUState.Inactive;
                _kill.IsEnabled = status.State == GPUState.Active;
            }
            else if (_item == QuickSettingsItem.OverclockDiscreteGpu)
            {
                var gpu = IoCContainer.Resolve<GPUOverclockController>();
                if (!await gpu.IsSupportedAsync()) { Unsupported(); return; }
                _toggle.IsChecked = gpu.GetState().Item1;
            }
            Visibility = Visibility.Visible;
        }
        catch (Exception ex) { QuickSettingsProvider.Error("Refresh " + _item, ex); Unsupported(); }
    }
    private void Unsupported() { Visibility = Visibility.Collapsed; QuickSettingsProvider.Current?.Unsupported(_item.Title()); }
    private async Task GpuActionAsync(bool restart)
    {
        if (_preview || _busy) return;
        _busy = true; IsEnabled = false;
        try
        {
            if (!await Dialogs.ConfirmAsync(restart ? "Restart GPU" : "Close GPU apps",
                restart ? "Restart the discrete GPU? Displays may flicker." : "Close applications using the GPU? Unsaved work may be lost.",
                restart ? "Restart" : "Close apps")) return;
            var gpu = IoCContainer.Resolve<GPUController>();
            if (restart) await gpu.RestartGPUAsync(); else await gpu.KillGPUProcessesAsync();
        }
        catch (Exception ex) { QuickSettingsProvider.Error("GPU action", ex); await Dialogs.ErrorAsync("GPU action failed", ex.Message); }
        finally { _busy = false; IsEnabled = true; await RefreshAsync(); }
    }
    private async void OnAction(object sender, RoutedEventArgs e)
    {
        if (_preview || _busy) return;
        if (_item == QuickSettingsItem.DiscreteGpu)
        {
            var menu = _action.ContextMenu;
            menu.PlacementTarget = _action;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
            return;
        }
        _busy = true; IsEnabled = false;
        try
        {
            if (_item == QuickSettingsItem.OverclockDiscreteGpu)
            {
                var gpu = IoCContainer.Resolve<GPUOverclockController>();
                var (_, info) = gpu.GetState(); gpu.SaveState(_toggle.IsChecked == true, info); await gpu.ApplyStateAsync(true);
            }
            else await IoCContainer.Resolve<NativeWindowsMessageListener>().TurnOffMonitorAsync();
        }
        catch (Exception ex) { QuickSettingsProvider.Error("Change " + _item, ex); await Dialogs.ErrorAsync("Could not change setting", ex.Message); }
        finally { _busy = false; IsEnabled = true; await RefreshAsync(); }
    }
}
