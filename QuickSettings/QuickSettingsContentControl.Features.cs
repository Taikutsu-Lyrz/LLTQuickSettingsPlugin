using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.Messaging;
using LenovoLegionToolkit.Lib.Messaging.Messages;
using Wpf.Ui.Common;
using Wpf.Ui.Controls;
using ToggleSwitch = Wpf.Ui.Controls.ToggleSwitch;
namespace LenovoLegionToolkit.Plugin.QuickSettings;
public partial class QuickSettingsContentControl
{
    private sealed class CompactFeatureControl<T> : UserControl where T : struct
    {
        private readonly IFeature<T> _feature = IoCContainer.Resolve<IFeature<T>>();
        private readonly Func<T, string> _displayName;
        private readonly bool _toggleMode;
        private readonly T _onState;
        private readonly T _offState;
        private readonly bool _isPreview;
        private readonly UniformGrid _segments = new();
        private readonly ToggleSwitch? _toggle;
        private readonly List<ToggleButton> _buttons = [];
        private bool _isLoaded;
        private bool _isBusy;
        private bool _refreshing;
        private readonly string _title;

        public CompactFeatureControl(string title, SymbolRegular icon, Func<T, string>? displayName = null, bool toggleMode = false, T onState = default, T offState = default, bool isPreview = false)
        {
            _title = title;
            _displayName = displayName ?? GetDisplayName;
            _toggleMode = toggleMode;
            _onState = onState;
            _offState = offState;
            _isPreview = isPreview;

            Margin = new(0, 0, 0, 12);
            HorizontalAlignment = HorizontalAlignment.Stretch;

            var content = new StackPanel();
            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var symbol = new SymbolIcon
            {
                Margin = new(0, 0, 8, 0),
                FontSize = 16,
                Symbol = icon,
                VerticalAlignment = VerticalAlignment.Center
            };
            symbol.SetResourceReference(SymbolIcon.ForegroundProperty, "TextFillColorSecondaryBrush");
            var titleText = new TextBlock
            {
                FontSize = 14,
                FontWeight = FontWeights.Medium,
                Text = title,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
                ToolTip = title
            };
            titleText.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
            Grid.SetColumn(titleText, 1);
            header.Children.Add(symbol);
            header.Children.Add(titleText);
            content.Children.Add(header);

            if (toggleMode)
            {
                _toggle = new ToggleSwitch
                {
                    Margin = new(0, 0, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                };
                _toggle.Click += Toggle_Click;
                Grid.SetColumn(_toggle, 2);
                header.Children.Add(_toggle);
            }
            else
            {
                _segments.Columns = 1;
                content.Children.Add(CreateSegmentContainer());
            }

            var card = new Border
            {
                Padding = new(10),
                BorderThickness = new(1),
                CornerRadius = new(7),
                Child = content
            };
            card.SetResourceReference(Border.BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
            card.SetResourceReference(Border.BorderBrushProperty, "CardStrokeColorDefaultBrush");
            Content = card;

            AutomationProperties.SetName(this, title);
            if (_toggle is not null)
                AutomationProperties.SetName(_toggle, title);

            Loaded += CompactFeatureControl_Loaded;
            Unloaded += CompactFeatureControl_Unloaded;
            IsVisibleChanged += CompactFeatureControl_IsVisibleChanged;
        }

        private FrameworkElement CreateSegmentContainer()
        {
            var border = new Border
            {
                Margin = new(0, 14, 0, 0),
                Padding = new(0),
                BorderThickness = new(0),
                CornerRadius = new(6),
                Child = _segments
            };
            border.SetResourceReference(Border.BackgroundProperty, "ControlFillColorDefaultBrush");
            border.SetResourceReference(Border.BorderBrushProperty, "ControlStrokeColorDefaultBrush");
            return border;
        }

        private void CompactFeatureControl_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = true;
            MessagingCenter.Unsubscribe(this);
            MessagingCenter.Subscribe<FeatureStateMessage<T>>(this, () => Dispatcher.InvokeAsync(async () =>
            {
                if (_isLoaded && IsVisible)
                    await RefreshAsync();
            }));
            _ = RefreshAsync();
        }

        private void CompactFeatureControl_Unloaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = false;
            MessagingCenter.Unsubscribe(this);
        }

        private async void CompactFeatureControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_isLoaded && IsVisible)
                await RefreshAsync();
        }

        public async Task RefreshAsync()
        {
            if (_refreshing || _isBusy) return;
            _refreshing = true;
            try { await RefreshCoreAsync(); }
            finally { _refreshing = false; }
        }

        private async Task RefreshCoreAsync()
        {
            if (!_isLoaded)
                return;

            try
            {
                if (!await _feature.IsSupportedAsync())
                {
                    QuickSettingsProvider.Current?.Unsupported(_title);
                    Visibility = Visibility.Collapsed;
                    return;
                }

                var states = await _feature.GetAllStatesAsync();
                var current = await _feature.GetStateAsync();

                if (_toggleMode)
                {
                    if (!states.Contains(_onState) || !states.Contains(_offState))
                    {
                        Visibility = Visibility.Collapsed;
                        return;
                    }

                    if (_toggle is not null)
                        _toggle.IsChecked = EqualityComparer<T>.Default.Equals(current, _onState);
                }
                else
                {
                    if (states.Length < 2)
                    {
                        Visibility = Visibility.Collapsed;
                        return;
                    }

                    _segments.Children.Clear();
                    _buttons.Clear();
                    _segments.Columns = states.Length;
                    foreach (var state in states)
                    {
                        var button = new ToggleButton
                        {
                            Content = new TextBlock
                            {
                                Text = _displayName(state),
                                TextAlignment = TextAlignment.Center,
                                TextTrimming = TextTrimming.CharacterEllipsis,
                                TextWrapping = TextWrapping.NoWrap,
                                ToolTip = _displayName(state)
                            },
                            IsChecked = EqualityComparer<T>.Default.Equals(current, state),
                            Tag = state
                        };
                        button.Style = TryFindResource("QuickSettingsSegmentButtonStyle") as Style;
                        button.Click += Segment_Click;
                        _buttons.Add(button);
                        _segments.Children.Add(button);
                    }

                    SetBusy(_isBusy);
                }

                Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                QuickSettingsProvider.Error($"Failed to refresh compact Quick settings control. [feature={typeof(T).Name}]", ex);
                QuickSettingsProvider.Current?.Unsupported(_title);
                Visibility = Visibility.Collapsed;
            }
        }

        private async void Toggle_Click(object sender, RoutedEventArgs e)
        {
            if (_isPreview)
                return;

            if (_toggle?.IsChecked is not bool isChecked)
                return;

            await ChangeStateAsync(isChecked ? _onState : _offState);
        }

        private async void Segment_Click(object sender, RoutedEventArgs e)
        {
            if (!_isPreview && sender is ToggleButton { Tag: T state })
                await ChangeStateAsync(state);
        }

        private async Task ChangeStateAsync(T state)
        {
            if (_isBusy)
                return;

            _isBusy = true;
            SetBusy(true);
            try
            {
                if (EqualityComparer<T>.Default.Equals(state, await _feature.GetStateAsync()))
                {
                    await RefreshAsync();
                    return;
                }

                if ((object)state is HybridModeState next && (object)(await _feature.GetStateAsync()) is HybridModeState current
                    && (next is HybridModeState.Off or HybridModeState.UMA || current is HybridModeState.Off or HybridModeState.UMA))
                {
                    if (!await Dialogs.ConfirmAsync("Restart required", "This GPU mode change requires a restart to take full effect. Apply it now and restart later?", "Apply"))
                        return;
                }
                await _feature.SetStateAsync(state);
                await RefreshCoreAsync();
            }
            catch (Exception ex)
            {
                QuickSettingsProvider.Error($"Failed to change compact Quick settings state. [feature={typeof(T).Name}]", ex);
                await Dialogs.ErrorAsync("Could not change this setting", ex.Message);
                await RefreshAsync();
            }
            finally
            {
                _isBusy = false;
                SetBusy(false);
                await RefreshAsync();
            }
        }

        private void SetBusy(bool busy)
        {
            if (_toggle is not null)
                _toggle.IsEnabled = !busy;

            foreach (var button in _buttons)
                button.IsEnabled = !busy;
        }

        private static string GetDisplayName(T value) => value switch
        {
            IDisplayName displayName => displayName.DisplayName,
            Enum enumValue => enumValue.GetDisplayName(),
            _ => value.ToString() ?? string.Empty
        };
    }
}

