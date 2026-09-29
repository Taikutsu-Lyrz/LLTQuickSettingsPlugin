using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace LenovoLegionToolkit.Plugin.QuickSettings;

public partial class QuickSettingsWindow
{
    private Rect _workArea = SystemParameters.WorkArea;
    private bool _positioning;
    public QuickSettingsWindow()
    {
        InitializeComponent();
        MaxHeight = _workArea.Height - 24;
        SizeChanged += (_, _) => Position();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; Hide(); } };
        Deactivated += (_, _) => Dispatcher.BeginInvoke(new Action(() =>
        {
            try { if (IsVisible && !IsActive && QuickSettingsProvider.Current?.ModalDepth == 0) Hide(); }
            catch (Exception ex) { QuickSettingsProvider.Error("Hide flyout", ex); }
        }));
        if (Content is Border { Child: FrameworkElement child })
            child.SizeChanged += (_, e) => child.Clip = new RectangleGeometry(new Rect(e.NewSize), 8, 8);
    }
    public void Toggle()
    {
        if (IsVisible) { Hide(); return; }
        ReadWorkArea();
        MaxHeight = Math.Max(100, _workArea.Height - 24);
        MaxWidth = Math.Max(100, _workArea.Width - 24);
        Position(); Show(); Topmost = false; Topmost = true; Activate(); Focus(); Position();
    }
    private void Position()
    {
        if (_positioning) return;
        _positioning = true;
        try
        {
            var height = ActualHeight > 0 ? ActualHeight : 500;
            Left = _workArea.Right - (ActualWidth > 0 ? ActualWidth : Width) - 12;
            Top = Math.Max(_workArea.Top + 12, _workArea.Bottom - height - 12);
        }
        finally { _positioning = false; }
    }
    private void ReadWorkArea()
    {
        try
        {
            if (!GetCursorPos(out var point)) return;
            var monitor = MonitorFromPoint(point, 2);
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(monitor, ref info)) return;
            double scale = 1;
            if (GetDpiForMonitor(monitor, 0, out var x, out _) == 0 && x != 0) scale = x / 96.0;
            _workArea = new Rect(info.Work.Left / scale, info.Work.Top / scale,
                (info.Work.Right - info.Work.Left) / scale, (info.Work.Bottom - info.Work.Top) / scale);
        }
        catch (Exception ex) { QuickSettingsProvider.Error("Position flyout", ex); _workArea = SystemParameters.WorkArea; }
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);
}
