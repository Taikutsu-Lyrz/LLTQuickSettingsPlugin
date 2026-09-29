using System;
using Wpf.Ui.Common;
namespace LenovoLegionToolkit.Plugin.QuickSettings;

public enum QuickSettingsItem
{
    PowerModeStatus, BatteryLevel, ChargeRate, CpuTemperature, GpuTemperature, CpuFanSpeed, GpuFanSpeed,
    ItsMode, PowerMode, BatteryMode, BatteryNightChargeMode, AlwaysOnUsb, InstantBoot, HybridMode,
    DiscreteGpu, OverclockDiscreteGpu, PanelLogoBacklight, PortsBacklight, Resolution, RefreshRate,
    DpiScale, Hdr, AutoColorManagement, OverDrive, TurnOffMonitors, Microphone, FlipToStart, TouchpadLock,
    FnLock, WinKeyLock, WhiteKeyboardBacklight, CpuUsage, GpuUsage
}
internal static class Catalog
{
    public static readonly QuickSettingsItem[] Defaults =
    [ QuickSettingsItem.PowerModeStatus, QuickSettingsItem.BatteryLevel, QuickSettingsItem.ChargeRate,
      QuickSettingsItem.CpuTemperature, QuickSettingsItem.GpuTemperature, QuickSettingsItem.CpuUsage, QuickSettingsItem.GpuUsage,
      QuickSettingsItem.CpuFanSpeed, QuickSettingsItem.GpuFanSpeed, QuickSettingsItem.ItsMode, QuickSettingsItem.PowerMode,
      QuickSettingsItem.BatteryMode, QuickSettingsItem.HybridMode, QuickSettingsItem.DiscreteGpu, QuickSettingsItem.RefreshRate, QuickSettingsItem.OverDrive ];
    public static bool IsStatus(this QuickSettingsItem item) => item <= QuickSettingsItem.GpuFanSpeed || item is QuickSettingsItem.CpuUsage or QuickSettingsItem.GpuUsage;
    public static string Title(this QuickSettingsItem item) => item switch
    {
        QuickSettingsItem.PowerModeStatus => "Power mode", QuickSettingsItem.BatteryLevel => "Battery",
        QuickSettingsItem.ChargeRate => "Charge / discharge rate", QuickSettingsItem.CpuTemperature => "CPU temperature",
        QuickSettingsItem.GpuTemperature => "GPU temperature", QuickSettingsItem.CpuFanSpeed => "CPU fan speed",
        QuickSettingsItem.GpuFanSpeed => "GPU fan speed", QuickSettingsItem.CpuUsage => "CPU usage", QuickSettingsItem.GpuUsage => "GPU usage",
        QuickSettingsItem.ItsMode => "ITS Mode", QuickSettingsItem.PowerMode => "Power Mode", QuickSettingsItem.BatteryMode => "Battery Mode",
        QuickSettingsItem.BatteryNightChargeMode => "Night charging", QuickSettingsItem.AlwaysOnUsb => "Always On USB",
        QuickSettingsItem.InstantBoot => "Instant Boot", QuickSettingsItem.HybridMode => "GPU working mode",
        QuickSettingsItem.DiscreteGpu => "Discrete GPU", QuickSettingsItem.OverclockDiscreteGpu => "GPU overclock",
        QuickSettingsItem.PanelLogoBacklight => "Panel logo backlight", QuickSettingsItem.PortsBacklight => "Ports backlight",
        QuickSettingsItem.Resolution => "Resolution", QuickSettingsItem.RefreshRate => "Refresh rate", QuickSettingsItem.DpiScale => "DPI",
        QuickSettingsItem.Hdr => "HDR", QuickSettingsItem.AutoColorManagement => "Auto Color Management", QuickSettingsItem.OverDrive => "Over Drive",
        QuickSettingsItem.TurnOffMonitors => "Turn off displays", QuickSettingsItem.Microphone => "Microphone", QuickSettingsItem.FlipToStart => "Flip To Start",
        QuickSettingsItem.TouchpadLock => "Touchpad Lock", QuickSettingsItem.FnLock => "Fn Lock", QuickSettingsItem.WinKeyLock => "Windows Key Lock",
        QuickSettingsItem.WhiteKeyboardBacklight => "Keyboard backlight", _ => item.ToString()
    };
    public static SymbolRegular Icon(this QuickSettingsItem item) => item switch
    {
        QuickSettingsItem.PowerMode or QuickSettingsItem.PowerModeStatus or QuickSettingsItem.ItsMode => SymbolRegular.Gauge24,
        QuickSettingsItem.BatteryLevel => SymbolRegular.Battery024,
        QuickSettingsItem.BatteryMode or QuickSettingsItem.BatteryNightChargeMode => SymbolRegular.BatteryCharge24,
        QuickSettingsItem.ChargeRate => SymbolRegular.Flash24,
        QuickSettingsItem.CpuFanSpeed or QuickSettingsItem.GpuFanSpeed => SymbolRegular.WeatherBlowingSnow24,
        QuickSettingsItem.HybridMode => SymbolRegular.LeafOne24,
        QuickSettingsItem.DiscreteGpu or QuickSettingsItem.GpuTemperature or QuickSettingsItem.GpuUsage or QuickSettingsItem.OverclockDiscreteGpu => SymbolRegular.DeveloperBoard24,
        QuickSettingsItem.Microphone => SymbolRegular.Mic24,
        QuickSettingsItem.RefreshRate => SymbolRegular.DesktopPulse24,
        QuickSettingsItem.FnLock or QuickSettingsItem.WinKeyLock or QuickSettingsItem.WhiteKeyboardBacklight => SymbolRegular.Keyboard24,
        QuickSettingsItem.AlwaysOnUsb or QuickSettingsItem.PortsBacklight => SymbolRegular.UsbPlug24,
        QuickSettingsItem.FlipToStart or QuickSettingsItem.InstantBoot => SymbolRegular.Power24,
        QuickSettingsItem.TouchpadLock => SymbolRegular.Tablet24,
        QuickSettingsItem.Hdr => SymbolRegular.Hdr24, QuickSettingsItem.AutoColorManagement => SymbolRegular.Color24,
        QuickSettingsItem.PanelLogoBacklight => SymbolRegular.LightbulbCircle24,
        _ => SymbolRegular.Desktop24
    };
}
