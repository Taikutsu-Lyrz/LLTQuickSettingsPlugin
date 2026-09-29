using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace LenovoLegionToolkit.Plugin.QuickSettings;

internal sealed class GlobalHotKey : IDisposable
{
    private readonly HwndSource _source;
    private readonly int _id;
    private bool _registered;
    public event Action? Pressed;
    public GlobalHotKey()
    {
        _id = GlobalAddAtom("LLT.QuickSettings." + Guid.NewGuid());
        if (_id == 0) throw new InvalidOperationException("Could not allocate a hotkey identifier.");
        _source = new HwndSource(new HwndSourceParameters("LLT QuickSettings Hotkey")
        { ParentWindow = new IntPtr(-3), WindowStyle = 0, Width = 0, Height = 0 });
        _source.AddHook(WndProc);
    }
    public bool Register(uint modifiers, uint key)
    {
        Unregister();
        return _registered = RegisterHotKey(_source.Handle, _id, modifiers | 0x4000, key);
    }
    public void Unregister()
    {
        if (_registered) UnregisterHotKey(_source.Handle, _id);
        _registered = false;
    }
    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x0312 && wParam.ToInt32() == _id)
        {
            handled = true;
            try { Pressed?.Invoke(); } catch (Exception ex) { QuickSettingsProvider.Error("Hotkey callback", ex); }
        }
        return IntPtr.Zero;
    }
    public void Dispose() { Unregister(); _source.RemoveHook(WndProc); _source.Dispose(); GlobalDeleteAtom((ushort)_id); Pressed = null; }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern ushort GlobalAddAtom(string text);
    [DllImport("kernel32.dll")] private static extern ushort GlobalDeleteAtom(ushort atom);
}
