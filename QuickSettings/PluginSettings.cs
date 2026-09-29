using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace LenovoLegionToolkit.Plugin.QuickSettings;

public sealed class LayoutItem
{
    public QuickSettingsItem Item { get; set; }
    public bool Visible { get; set; }
}
public sealed class PluginSettings
{
    public bool HotkeyEnabled { get; set; } = true;
    public uint Modifiers { get; set; } = 0x0002 | 0x0004;
    public uint Key { get; set; } = 0x51;
    public List<LayoutItem> Items { get; set; } = Defaults();
    public static List<LayoutItem> Defaults() => Catalog.Defaults.Concat(Enum.GetValues<QuickSettingsItem>().Except(Catalog.Defaults))
        .Select(item => new LayoutItem { Item = item, Visible = Catalog.Defaults.Contains(item) }).ToList();
    public void Normalize()
    {
        Items = (Items ?? Defaults()).Where(item => item is not null && Enum.IsDefined(item.Item)).DistinctBy(item => item.Item).ToList();
        foreach (var item in Enum.GetValues<QuickSettingsItem>().Except(Items.Select(entry => entry.Item)))
            Items.Add(new LayoutItem { Item = item });
        if (Key == 0 || Key > 254 || (Modifiers & 0xF) == 0) { Key = 0x51; Modifiers = 6; }
        Modifiers &= 0xF;
    }
    public string Shortcut => FormatHotkey(Modifiers, Key);
    public static string FormatHotkey(uint modifiers, uint key)
    {
        var parts = new List<string>();
        if ((modifiers & 2) != 0) parts.Add("Ctrl");
        if ((modifiers & 1) != 0) parts.Add("Alt");
        if ((modifiers & 4) != 0) parts.Add("Shift");
        if ((modifiers & 8) != 0) parts.Add("Win");
        parts.Add(KeyInterop.KeyFromVirtualKey((int)key).ToString());
        return string.Join(" + ", parts);
    }
}
