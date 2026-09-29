using System;
using System.Threading.Tasks;
using System.Windows;
namespace LenovoLegionToolkit.Plugin.QuickSettings;
internal static class Dialogs
{
    public static async Task<bool> ConfirmAsync(string title, string message, string primary = "Continue")
    {
        var provider = QuickSettingsProvider.Current;
        try
        {
            if (provider is null || provider.IsDisposed) return false;
            var result = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var dialog = new Wpf.Ui.Controls.MessageBox
            {
                Title = title, Content = message, ButtonLeftName = primary, ButtonRightName = "Cancel",
                Owner = Application.Current.MainWindow
            };
            provider.ModalDepth++;
            dialog.ButtonLeftClick += (_, _) => { result.TrySetResult(true); dialog.Close(); };
            dialog.ButtonRightClick += (_, _) => dialog.Close();
            dialog.Closed += (_, _) => result.TrySetResult(false);
            try { dialog.Show(); return await result.Task; }
            finally { provider.ModalDepth--; }
        }
        catch (Exception ex) { QuickSettingsProvider.Error("Show confirmation", ex); return false; }
    }
    public static async Task ErrorAsync(string title, string message) => await ConfirmAsync(title, message, "OK");
}
