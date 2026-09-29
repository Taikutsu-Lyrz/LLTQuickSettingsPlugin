using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Controllers.Sensors;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.System;
using DpiScale = LenovoLegionToolkit.Lib.DpiScale;

namespace LenovoLegionToolkit.Plugin.QuickSettings;
public partial class QuickSettingsContentControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly Dictionary<QuickSettingsItem, (Border Tile, TextBlock Value, TextBlock Caption)> _values = new();
    private readonly List<Func<Task>> _refreshers = new();
    private IDisposable? _subscription;
    private QuickSettingsProvider? _provider;
    private bool _loaded, _refreshing, _prepared;
    public bool IsPreview { get; set; }

    public QuickSettingsContentControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += (_, _) => { if (_loaded) UpdateTimer(); };
        _timer.Tick += async (_, _) => await RefreshAsync();
    }
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _previewBody.IsHitTestVisible = !IsPreview;
            System.Windows.Input.KeyboardNavigation.SetTabNavigation(_previewBody, IsPreview ? System.Windows.Input.KeyboardNavigationMode.None : System.Windows.Input.KeyboardNavigationMode.Continue);
            _loaded = true;
            _provider = QuickSettingsProvider.Current;
            if (_provider is null) return;
            await _provider.Ready;
            if (!_loaded) return;
            _provider.Changed -= OnSettingsChanged;
            _provider.Changed += OnSettingsChanged;
            Rebuild();
            UpdateTimer();
        }
        catch (Exception ex) { QuickSettingsProvider.Error("Load content", ex); }
    }
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _loaded = false;
        if (_provider is not null) _provider.Changed -= OnSettingsChanged;
        StopTimer();
    }
    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (_provider?.IsDisposed == true) { StopTimer(); return; }
        try { Rebuild(); } catch (Exception ex) { QuickSettingsProvider.Error("Update layout", ex); }
    }
    private void Rebuild()
    {
        if (_provider is null) return;
        _hotKeyHint.Text = _provider.Settings.HotkeyEnabled ? _provider.Settings.Shortcut : "Hotkey disabled";
        _statusTiles.Children.Clear(); _values.Clear();
        _featureControlsPanel.Children.Clear(); _refreshers.Clear();
        foreach (var entry in _provider.Settings.Items.Where(entry => entry.Visible))
        {
            if (entry.Item.IsStatus()) { AddTile(entry.Item); continue; }
            try { AddFeature(entry.Item); }
            catch (Exception ex) { QuickSettingsProvider.Error("Create " + entry.Item, ex); _provider.Unsupported(entry.Item.Title()); }
        }
        _statusSection.Visibility = _values.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        _controlsSection.Visibility = _featureControlsPanel.Children.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        _ = RefreshAsync();
    }
    private void AddTile(QuickSettingsItem item)
    {
        var tile = new Border { Margin = new(0,0,8,8), MinHeight = 56, Padding = new(10,8,10,8), BorderThickness = new(1), CornerRadius = new(7) };
        tile.SetResourceReference(Border.BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
        tile.SetResourceReference(Border.BorderBrushProperty, "CardStrokeColorDefaultBrush");
        var caption = new TextBlock { Text = item.Title(), FontSize = 12, TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis };
        caption.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        var value = new TextBlock { Text = "—", Margin = new(0,3,0,0), FontSize = 16, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis };
        var panel = new StackPanel(); panel.Children.Add(caption); panel.Children.Add(value); tile.Child = panel;
        _values[item] = (tile, value, caption); _statusTiles.Children.Add(tile);
    }
    private void AddFeature(QuickSettingsItem item)
    {
        switch (item)
        {
            case QuickSettingsItem.PowerMode: Add<PowerModeState>(item); break;
            case QuickSettingsItem.BatteryMode: Add<BatteryState>(item); break;
            case QuickSettingsItem.ItsMode: Add<ITSMode>(item, state => IoCContainer.Resolve<ITSModeFeature>().GetITSModeDisplayName(state)); break;
            case QuickSettingsItem.HybridMode: Add<HybridModeState>(item, state => state switch { HybridModeState.On => "Hybrid", HybridModeState.Off => "dGPU", HybridModeState.OnIGPUOnly => "iGPU", HybridModeState.OnAuto => "Auto", _ => "UMA" }); break;
            case QuickSettingsItem.RefreshRate: Add<RefreshRate>(item); break;
            case QuickSettingsItem.Resolution: Add<Resolution>(item); break;
            case QuickSettingsItem.DpiScale: Add<DpiScale>(item); break;
            case QuickSettingsItem.AlwaysOnUsb: Add<AlwaysOnUSBState>(item); break;
            case QuickSettingsItem.InstantBoot: Add<InstantBootState>(item); break;
            case QuickSettingsItem.WhiteKeyboardBacklight:
                Add<WhiteKeyboardBacklightState>(item);
                Toggle(item, OneLevelWhiteKeyboardBacklightState.On, OneLevelWhiteKeyboardBacklightState.Off); break;
            case QuickSettingsItem.BatteryNightChargeMode: Toggle(item, BatteryNightChargeState.On, BatteryNightChargeState.Off); break;
            case QuickSettingsItem.Hdr: Toggle(item, HDRState.On, HDRState.Off); break;
            case QuickSettingsItem.AutoColorManagement: Toggle(item, AutoColorManagementState.On, AutoColorManagementState.Off); break;
            case QuickSettingsItem.OverDrive: Toggle(item, OverDriveState.On, OverDriveState.Off); break;
            case QuickSettingsItem.PanelLogoBacklight: Toggle(item, PanelLogoBacklightState.On, PanelLogoBacklightState.Off); break;
            case QuickSettingsItem.PortsBacklight: Toggle(item, PortsBacklightState.On, PortsBacklightState.Off); break;
            case QuickSettingsItem.Microphone: Toggle(item, MicrophoneState.On, MicrophoneState.Off); break;
            case QuickSettingsItem.FlipToStart: Toggle(item, FlipToStartState.On, FlipToStartState.Off); break;
            case QuickSettingsItem.TouchpadLock: Toggle(item, TouchpadLockState.On, TouchpadLockState.Off); break;
            case QuickSettingsItem.FnLock: Toggle(item, FnLockState.On, FnLockState.Off); break;
            case QuickSettingsItem.WinKeyLock: Toggle(item, WinKeyState.On, WinKeyState.Off); break;
            default:
                var action = new ActionFeatureControl(item, IsPreview);
                action.Tag = item;
                _featureControlsPanel.Children.Add(action); _refreshers.Add(action.RefreshAsync); break;
        }
    }
    private void Add<T>(QuickSettingsItem item, Func<T,string>? name = null) where T : struct
    {
        var control = new CompactFeatureControl<T>(item.Title(), item.Icon(), name, isPreview: IsPreview);
        control.Tag = item;
        _featureControlsPanel.Children.Add(control); _refreshers.Add(control.RefreshAsync);
    }
    private void Toggle<T>(QuickSettingsItem item, T on, T off) where T : struct
    {
        var control = new CompactFeatureControl<T>(item.Title(), item.Icon(), null, true, on, off, IsPreview);
        control.Tag = item;
        _featureControlsPanel.Children.Add(control); _refreshers.Add(control.RefreshAsync);
    }
    private void UpdateTimer()
    {
        if (IsVisible && _provider?.IsDisposed == false) { _timer.Start(); _ = RefreshAsync(); }
        else StopTimer();
    }
    private void StopTimer()
    {
        _timer.Stop();
        try { _subscription?.Dispose(); } catch (Exception ex) { QuickSettingsProvider.Error("Stop sensor subscription", ex); }
        _subscription = null;
    }
    private async Task RefreshAsync()
    {
        if (_refreshing || !_loaded || !IsVisible || _provider?.IsDisposed != false) return;
        _refreshing = true;
        try
        {
            foreach (var refresh in _refreshers.ToArray()) await refresh();
            await ReadStatusAsync();
        }
        catch (Exception ex) { QuickSettingsProvider.Error("Refresh content", ex); }
        finally { _refreshing = false; }
    }
    private async Task ReadStatusAsync()
    {
        try
        {
            var battery = Battery.GetBatteryInformation();
            Set(QuickSettingsItem.BatteryLevel, $"{battery.BatteryPercentage}%");
            Set(QuickSettingsItem.ChargeRate, $"{Math.Abs((double)battery.DischargeRate)/1000.0:0.00} W");
            if (_values.TryGetValue(QuickSettingsItem.ChargeRate, out var rate)) rate.Caption.Text = battery.IsCharging ? "Charging" : battery.DischargeRate < 0 ? "Discharging" : "Battery power";
        }
        catch (Exception ex) { QuickSettingsProvider.Error("Battery status", ex); Set(QuickSettingsItem.BatteryLevel, null); Set(QuickSettingsItem.ChargeRate, null); }
        try
        {
            var power = IoCContainer.Resolve<IFeature<PowerModeState>>();
            if (await power.IsSupportedAsync()) Set(QuickSettingsItem.PowerModeStatus, (await power.GetStateAsync()).GetDisplayName());
            else
            {
                var its = IoCContainer.Resolve<ITSModeFeature>();
                Set(QuickSettingsItem.PowerModeStatus, await its.IsSupportedAsync() ? its.GetITSModeDisplayName(await its.GetStateAsync()) : null);
            }
        }
        catch (Exception ex) { QuickSettingsProvider.Error("Power status", ex); Set(QuickSettingsItem.PowerModeStatus, null); }
        var cpu = SensorData.Empty; var gpu = SensorData.Empty;
        try
        {
            var sensors = IoCContainer.Resolve<ISensorsController>();
            if (await sensors.IsSupportedAsync())
            {
                if (!_prepared) { await sensors.PrepareAsync(); _prepared = true; }
                var data = await sensors.GetDataAsync(); cpu = data.CPU; gpu = data.GPU;
            }
        }
        catch (Exception ex) { QuickSettingsProvider.Error("Read platform sensors", ex); }
        double ct = cpu.Temperature, gt = gpu.Temperature, cu = cpu.Utilization, gu = gpu.Utilization;
        var cpuFan = cpu.FanSpeed; var gpuFan = gpu.FanSpeed;
        try
        {
            var sensors = IoCContainer.Resolve<ISensorsController>();
            if ((cpuFan < 0 || gpuFan < 0) && await sensors.IsSupportedAsync())
            {
                var fans = await sensors.GetFanSpeedsAsync();
                cpuFan = fans.CpuFanSpeed; gpuFan = fans.GpuFanSpeed;
            }
        }
        catch (Exception ex) { QuickSettingsProvider.Error("Read fan speeds", ex); }
        if (!_loaded || !IsVisible || _provider?.IsDisposed != false) return;
        try
        {
            var settings = IoCContainer.Resolve<ApplicationSettings>();
            var group = IoCContainer.Resolve<SensorsGroupController>();
            if (settings.Store.EnableHardwareSensors && group.IsLibreHardwareMonitorInitialized())
            {
                _subscription ??= group.Subscribe(TimeSpan.FromSeconds(2), HardwareUpdateScope.Cpu | HardwareUpdateScope.Gpu);
                var snapshot = group.Snapshot;
                if (snapshot[SensorItem.CpuTemperature] >= 0) ct = snapshot[SensorItem.CpuTemperature];
                if (snapshot[SensorItem.GpuCoreTemperature] >= 0) gt = snapshot[SensorItem.GpuCoreTemperature];
            }
        }
        catch (Exception ex) { QuickSettingsProvider.Error("Read hardware snapshot", ex); }
        Set(QuickSettingsItem.CpuTemperature, Number(ct, "°C")); Set(QuickSettingsItem.GpuTemperature, Number(gt, "°C"));
        Set(QuickSettingsItem.CpuUsage, Number(cu, "%")); Set(QuickSettingsItem.GpuUsage, Number(gu, "%"));
        Set(QuickSettingsItem.CpuFanSpeed, Number(cpuFan, "RPM")); Set(QuickSettingsItem.GpuFanSpeed, Number(gpuFan, "RPM"));
    }
    private static string? Number(double value, string unit) => double.IsFinite(value) && value >= 0 ? $"{value:0} {unit}" : null;
    private void Set(QuickSettingsItem item, string? value)
    {
        if (!_values.TryGetValue(item, out var tile)) return;
        tile.Tile.Visibility = value is null ? Visibility.Collapsed : Visibility.Visible;
        tile.Value.Text = value ?? "—";
    }
    internal static void PlayMoveAnimation(FrameworkElement element, int direction)
    {
        element.BringIntoView();
        var transform = new TranslateTransform();
        element.RenderTransform = transform;
        transform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-direction * 36, 0, TimeSpan.FromMilliseconds(220)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        element.BeginAnimation(OpacityProperty, new DoubleAnimation(0.45, 1, TimeSpan.FromMilliseconds(450)));
    }
    internal void AnimateItem(QuickSettingsItem item, int direction)
    {
        // ponytail: deferred so the preview finishes rebuilding before the lookup runs.
        _ = Dispatcher.InvokeAsync(() =>
        {
            try
            {
                if (FindItemElement(item) is { Visibility: Visibility.Visible } element)
                    PlayMoveAnimation(element, direction);
            }
            catch (Exception ex) { QuickSettingsProvider.Error("Animate preview item", ex); }
        }, DispatcherPriority.Loaded);
    }
    private FrameworkElement? FindItemElement(QuickSettingsItem item)
    {
        if (_values.TryGetValue(item, out var tile)) return tile.Tile;
        foreach (var child in _featureControlsPanel.Children)
            if (child is FrameworkElement element && element.Tag is QuickSettingsItem tagged && tagged == item)
                return element;
        return null;
    }
    private void OpenMainWindow_Click(object sender, RoutedEventArgs e) => _provider?.OpenApp();
    private void OpenMiniLayout_Click(object sender, RoutedEventArgs e) => _provider?.OpenApp(true);
}
