using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using LenovoLegionToolkit.Lib.Station.Core;
using LenovoLegionToolkit.Lib.Station.Services;
using Wpf.Ui.Controls.Interfaces;

namespace LenovoLegionToolkit.Plugin.QuickSettings;

public sealed class QuickSettingsProvider : IExtensionProvider
{
    internal static QuickSettingsProvider? Current { get; private set; }
    internal IExtensionContext Context { get; private set; } = null!;
    internal PluginSettings Settings { get; private set; } = new();
    internal Task Ready { get; private set; } = Task.CompletedTask;
    internal event EventHandler? Changed;
    internal string Warning { get; private set; } = "";
    internal bool IsDisposed { get; private set; }
    internal int ModalDepth { get; set; }
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private GlobalHotKey? _hotkey;
    private System.Windows.Threading.DispatcherTimer? _iconTimer;
    private QuickSettingsWindow? _window;
    private string _settingsPath = "";
    private readonly System.Collections.Generic.HashSet<string> _unsupported = new();
    internal void Unsupported(string title)
    {
        if (_unsupported.Add(title)) Context.Logger.Trace("Unsupported feature hidden: " + title);
    }

    public QuickSettingsProvider() { }
    public object? GetData(string key) => key switch
    {
        nameof(ExtensionDataKey.Capability) => "QuickSettings",
        nameof(ExtensionDataKey.Version) => "1.0.0",
        _ => null
    };
    public void SetData(string key, object? value) { }
    public Task ExecuteAsync(string action, params object[] args) => Task.CompletedTask;

    public void Initialize(IExtensionContext context)
    {
        Context = context;
        Current = this;
        try
        {
            context.Navigation.Register(new ExtensionNavigationItem
            {
                Id = "quick-settings-layout", Title = "Mini Layout", PageTag = "quickSettingsLayout",
                PageType = typeof(MiniLayoutPage), Icon = ExtensionIcon.Gauge
            });
            Ready = InitAsync();
            // Station exposes only Gauge/None. Set the existing WPF-UI item after host navigation loads.
            var attempts = 0;
            _iconTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _iconTimer.Tick += (_, _) =>
            {
                try
                {
                    if (Application.Current?.MainWindow?.FindName("_navigationStore") is INavigation navigation)
                        foreach (var item in navigation.Items.OfType<Wpf.Ui.Controls.NavigationItem>())
                            if (item.PageTag == "quickSettingsLayout")
                            {
                                PanelNavigationIcon.Apply(item);
                                _iconTimer.Stop();
                                return;
                            }
                }
                catch (Exception ex) { Error("Set Mini Layout icon", ex); _iconTimer.Stop(); }
                if (++attempts >= 60) _iconTimer.Stop();
            };
            _iconTimer.Start();
        }
        catch (Exception ex) { Error("Initialize", ex); }
    }

    private async Task InitAsync()
    {
        try
        {
            _settingsPath = Path.Combine(Context.GetPluginStoragePath("QuickSettings"), "settings.json");
            PluginSettings settings = new();
            try
            {
                if (File.Exists(_settingsPath))
                    settings = JsonSerializer.Deserialize<PluginSettings>(await File.ReadAllTextAsync(_settingsPath)) ?? new();
                settings.Normalize();
            }
            catch (Exception ex) { Error("Load settings; using defaults", ex); }
            await Context.UiDispatcher.InvokeAsync(() =>
            {
                if (IsDisposed) return;
                Settings = settings;
                _hotkey = new GlobalHotKey();
                _hotkey.Pressed += ToggleFlyout;
                RegisterHotkey();
                NotifyChanged();
            });
            Context.Logger.Trace("QuickSettings initialized; capability QuickSettings; Mini Layout registered.");
        }
        catch (Exception ex) { Error("Initialize hotkey", ex); Warning = "Quick Settings could not initialize. See the plugin log."; }
    }

    private void RegisterHotkey()
    {
        Warning = "";
        _hotkey?.Unregister();
        if (!Settings.HotkeyEnabled || IsDisposed) return;
        if (_hotkey is null || !_hotkey.Register(Settings.Modifiers, Settings.Key))
        {
            Warning = "This shortcut could not be registered. Close another app using this shortcut or choose a different shortcut.";
            Context.Logger.Trace(Warning);
        }
    }

    internal async Task UpdateAsync(Action<PluginSettings> update, bool hotkey = false)
    {
        try
        {
            await Ready;
            if (IsDisposed) return;
            update(Settings);
            Settings.Normalize();
            if (hotkey) RegisterHotkey();
            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            await _saveLock.WaitAsync();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
                await File.WriteAllTextAsync(_settingsPath + ".tmp", json);
                File.Move(_settingsPath + ".tmp", _settingsPath, true);
            }
            finally { _saveLock.Release(); }
            NotifyChanged();
        }
        catch (Exception ex) { Error("Save settings", ex); Warning = "Settings could not be saved. See the plugin log."; NotifyChanged(); }
    }

    private void NotifyChanged()
    {
        if (Changed is null) return;
        foreach (EventHandler handler in Changed.GetInvocationList())
            try { handler(this, EventArgs.Empty); } catch (Exception ex) { Error("Settings notification", ex); }
    }

    internal void ToggleFlyout()
    {
        try
        {
            if (IsDisposed) return;
            _window ??= new QuickSettingsWindow();
            _window.Toggle();
        }
        catch (Exception ex) { Error("Toggle flyout", ex); }
    }

    internal void OpenApp(bool layout = false)
    {
        try
        {
            _window?.Hide();
            var window = Application.Current?.MainWindow;
            if (window is null) return;
            window.Show();
            if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
            window.Activate();
            if (layout && window.FindName("_navigationStore") is INavigation navigation)
                navigation.Navigate("quickSettingsLayout");
        }
        catch (Exception ex) { Error("Open main window or Mini Layout", ex); }
    }

    internal static void Error(string operation, Exception exception)
    {
        try { Current?.Context.Logger.Error(operation, exception); } catch { /* Logging must never terminate LLT. */ }
    }

    public async ValueTask DisposeAsync()
    {
        IsDisposed = true;
        try
        {
            await Ready;
            await Context.UiDispatcher.InvokeAsync(() =>
            {
                _hotkey?.Dispose(); _hotkey = null;
                _iconTimer?.Stop(); _iconTimer = null;
                _window?.Close(); _window = null;
                NotifyChanged(); Changed = null;
            });
            Context.Logger.Trace("QuickSettings disposed; hotkey and flyout released.");
        }
        catch (Exception ex) { Error("Shutdown", ex); }
        finally { Current = null; }
    }
}
