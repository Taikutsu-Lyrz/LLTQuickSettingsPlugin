using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using Wpf.Ui.Controls;

namespace LenovoLegionToolkit.Plugin.QuickSettings;

internal sealed class PanelNavigationIcon : IMultiValueConverter
{
    private static readonly Geometry Outline = LoadOutline();

    private static Geometry LoadOutline()
    {
        using var stream = typeof(PanelNavigationIcon).Assembly.GetManifestResourceStream("QuickSettings.MiniLayout.svg")
            ?? throw new InvalidOperationException("Panel icon resource missing.");
        var svg = XDocument.Load(stream);
        var path = svg.Descendants().First(element => element.Name.LocalName == "path");
        var geometry = Geometry.Parse((string?)path.Attribute("d") ?? "");
        geometry.Freeze();
        return geometry;
    }

    internal static void Apply(NavigationItem item)
    {
        item.Icon = Wpf.Ui.Common.SymbolRegular.Empty;
        var binding = new MultiBinding { Converter = new PanelNavigationIcon() };
        binding.Bindings.Add(new Binding(nameof(item.IsActive)) { Source = item });
        binding.Bindings.Add(new Binding(nameof(item.IconForeground)) { Source = item });
        binding.Bindings.Add(new Binding(nameof(item.Foreground)) { Source = item });
        BindingOperations.SetBinding(item, NavigationItem.ImageProperty, binding);
    }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            var brush = values.ElementAtOrDefault(1) as Brush ?? values.ElementAtOrDefault(2) as Brush ?? Brushes.Gray;
            if (values.ElementAtOrDefault(0) is true)
            {
                var accent = Application.Current?.TryFindResource("SystemAccentColorSecondary");
                if (accent is Color color) brush = new SolidColorBrush(color);
                else if (accent is Brush accentBrush) brush = accentBrush;
            }
            var visual = new DrawingVisual();
            using (var drawing = visual.RenderOpen())
            {
                drawing.PushTransform(new ScaleTransform(4, 4));
                drawing.DrawGeometry(null, new Pen(brush, 1.7) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }, Outline);
                drawing.Pop();
            }
            // NavigationItem.Image accepts BitmapSource; SVG remains embedded in the DLL.
            var image = new RenderTargetBitmap(96, 96, 96, 96, PixelFormats.Pbgra32);
            image.Render(visual);
            image.Freeze();
            return image;
        }
        catch (Exception ex) { QuickSettingsProvider.Error("Render panel navigation icon", ex); return DependencyProperty.UnsetValue; }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
}