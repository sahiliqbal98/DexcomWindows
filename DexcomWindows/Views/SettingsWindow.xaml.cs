using DexcomWindows.Models;
using DexcomWindows.Services;
using DexcomWindows.Themes;
using DexcomWindows.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using System.Collections.ObjectModel;

namespace DexcomWindows.Views;

public sealed partial class SettingsWindow : Window
{
    public event EventHandler? SignOutRequested;

    private readonly SettingsService _settings;
    private readonly NotificationService _notifications;
    private readonly GlucoseViewModel _viewModel;
    private bool _isInitializing = true;

    // Theme items for the list
    private ObservableCollection<ThemeItem> _themeItems = new();

    public SettingsWindow(SettingsService settings, NotificationService notifications, GlucoseViewModel viewModel)
    {
        InitializeComponent();
        _settings = settings;
        _notifications = notifications;
        _viewModel = viewModel;

        // Set window size per spec
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new Windows.Graphics.SizeInt32(360, 560));

        // Initialize theme list
        InitializeThemeList();

        // Load all settings
        LoadSettings();

        _isInitializing = false;
    }

    private void InitializeThemeList()
    {
        _themeItems = new ObservableCollection<ThemeItem>
        {
            new ThemeItem { Theme = ColorTheme.System, Name = "System", SwatchColor = Color.FromArgb(255, 128, 128, 128) },
            new ThemeItem { Theme = ColorTheme.Light, Name = "Light", SwatchColor = Color.FromArgb(255, 255, 255, 255) },
            new ThemeItem { Theme = ColorTheme.Dark, Name = "Dark", SwatchColor = Color.FromArgb(255, 26, 26, 26) },
            new ThemeItem { Theme = ColorTheme.Charcoal, Name = "Charcoal", SwatchColor = Color.FromArgb(255, 46, 46, 48) },
            new ThemeItem { Theme = ColorTheme.Rainbow, Name = "Rainbow", SwatchColor = Color.FromArgb(255, 38, 26, 51) },
            new ThemeItem { Theme = ColorTheme.DexcomGreen, Name = "Dexcom Green", SwatchColor = Color.FromArgb(255, 13, 38, 26) }
        };

        ThemeListView.ItemsSource = _themeItems;
    }

    private void LoadSettings()
    {
        // General settings
        // Select theme in list
        var currentTheme = _settings.ColorTheme;
        foreach (var item in _themeItems)
        {
            item.IsSelected = item.Theme == currentTheme ? Visibility.Visible : Visibility.Collapsed;
        }
        ThemeListView.SelectedIndex = (int)currentTheme;

        // Timer bar mode
        ElapsedRadio.IsChecked = _settings.TimerBarStyle == TimerBarStyle.Elapsed;
        RemainingRadio.IsChecked = _settings.TimerBarStyle == TimerBarStyle.Remaining;

        // Target range
        TargetLowSlider.Value = _settings.TargetLow;
        TargetHighSlider.Value = _settings.TargetHigh;
        TargetLowValue.Text = _settings.TargetLow.ToString();
        TargetHighValue.Text = _settings.TargetHigh.ToString();

        // Startup
        StartupToggle.IsOn = _settings.StartWithWindows;

        // Account status
        UpdateAccountStatus();

        // Alert settings
        var alerts = _settings.AlertSettings;
        AlertsEnabledToggle.IsOn = alerts.AlertsEnabled;
        ShowBannersCheck.IsChecked = _settings.ShowBanners;
        PlaySoundCheck.IsChecked = _settings.PlaySound;

        // Low alerts
        UrgentLowToggle.IsOn = alerts.UrgentLowEnabled;
        UrgentLowSlider.Value = alerts.UrgentLowThreshold;
        UrgentLowThresholdText.Text = $"Below {alerts.UrgentLowThreshold} mg/dL";

        LowToggle.IsOn = alerts.LowEnabled;
        LowSlider.Value = alerts.LowThreshold;
        LowThresholdText.Text = $"Below {alerts.LowThreshold} mg/dL";

        // High alerts
        HighToggle.IsOn = alerts.HighEnabled;
        HighSlider.Value = alerts.HighThreshold;
        HighThresholdText.Text = $"Above {alerts.HighThreshold} mg/dL";

        UrgentHighToggle.IsOn = alerts.UrgentHighEnabled;
        UrgentHighSlider.Value = alerts.UrgentHighThreshold;
        UrgentHighThresholdText.Text = $"Above {alerts.UrgentHighThreshold} mg/dL";

        // Rate of change
        RisingQuicklyToggle.IsOn = alerts.RapidChangeAlerts;
        RisingToggle.IsOn = alerts.RisingAlerts;
        FallingToggle.IsOn = alerts.FallingAlerts;
        FallingQuicklyToggle.IsOn = alerts.RapidChangeAlerts;

        // Stale data
        StaleDataToggle.IsOn = alerts.StaleDataAlertEnabled;
        StaleDataThresholdText.Text = $"After {alerts.StaleDataThreshold} minutes";
        SelectComboByTag(StaleDataIntervalCombo, alerts.StaleDataThreshold.ToString());

        // Cooldowns
        SelectComboByTag(NormalCooldownCombo, _settings.NormalAlertCooldown.ToString());
        SelectComboByTag(UrgentCooldownCombo, _settings.UrgentAlertCooldown.ToString());
    }

    private void UpdateAccountStatus()
    {
        if (_viewModel.IsAuthenticated)
        {
            AccountStatusText.Text = "Connected";
            ConnectionIndicator.Fill = new SolidColorBrush(Color.FromArgb(255, 52, 199, 89));
        }
        else
        {
            AccountStatusText.Text = "Not connected";
            ConnectionIndicator.Fill = new SolidColorBrush(Color.FromArgb(255, 255, 59, 48));
        }

        ServerText.Text = DexcomShareAPI.GetServerDisplayName(_settings.Server);

        if (_viewModel.NextRefreshTime.HasValue)
        {
            var remaining = _viewModel.NextRefreshTime.Value - DateTime.Now;
            if (remaining.TotalSeconds > 0)
            {
                NextRefreshText.Text = $"Next refresh in {remaining.Minutes}:{remaining.Seconds:D2}";
            }
        }
    }

    private void SelectComboByTag(ComboBox combo, string tag)
    {
        foreach (ComboBoxItem item in combo.Items)
        {
            if (item.Tag?.ToString() == tag)
            {
                combo.SelectedItem = item;
                return;
            }
        }
        if (combo.Items.Count > 0)
            combo.SelectedIndex = 0;
    }

    private int GetComboSelectedTag(ComboBox combo, int defaultValue)
    {
        if (combo.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out int value))
            return value;
        return defaultValue;
    }

    // Event Handlers

    private void TabButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;

        var tag = button.Tag?.ToString();
        var isGeneral = tag == "General";

        // Update button styles
        GeneralTabButton.Background = isGeneral
            ? (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"]
            : null;
        GeneralTabButton.Foreground = isGeneral
            ? new SolidColorBrush(Colors.White)
            : (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
        GeneralTabButton.Style = isGeneral ? null : (Style)Application.Current.Resources["SubtleButtonStyle"];

        AlertsTabButton.Background = !isGeneral
            ? (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"]
            : null;
        AlertsTabButton.Foreground = !isGeneral
            ? new SolidColorBrush(Colors.White)
            : (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
        AlertsTabButton.Style = !isGeneral ? null : (Style)Application.Current.Resources["SubtleButtonStyle"];

        // Show/hide panels
        GeneralPanel.Visibility = isGeneral ? Visibility.Visible : Visibility.Collapsed;
        AlertsPanel.Visibility = !isGeneral ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SignOutButton_Click(object sender, RoutedEventArgs e)
    {
        SignOutRequested?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private void ThemeListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        var selectedItem = ThemeListView.SelectedItem as ThemeItem;
        if (selectedItem == null) return;

        // Update selection visuals
        foreach (var item in _themeItems)
        {
            item.IsSelected = item.Theme == selectedItem.Theme ? Visibility.Visible : Visibility.Collapsed;
        }

        _settings.ColorTheme = selectedItem.Theme;
        _settings.SaveSettings();
    }

    private void TimerBarMode_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        _settings.TimerBarStyle = ElapsedRadio.IsChecked == true
            ? TimerBarStyle.Elapsed
            : TimerBarStyle.Remaining;
        _settings.SaveSettings();
    }

    private void TargetLowSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isInitializing || TargetLowValue == null) return;

        var value = (int)e.NewValue;
        TargetLowValue.Text = value.ToString();
        _settings.TargetLow = value;
        _settings.SaveSettings();
    }

    private void TargetHighSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isInitializing || TargetHighValue == null) return;

        var value = (int)e.NewValue;
        TargetHighValue.Text = value.ToString();
        _settings.TargetHigh = value;
        _settings.SaveSettings();
    }

    private void StartupToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        _settings.StartWithWindows = StartupToggle.IsOn;
    }

    private void AlertsEnabledToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        _settings.AlertSettings = _settings.AlertSettings with { AlertsEnabled = AlertsEnabledToggle.IsOn };
        _settings.SaveSettings();
        _viewModel.UpdateAlertSettings(_settings.AlertSettings);
    }

    private void NotificationStyle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        _settings.ShowBanners = ShowBannersCheck.IsChecked == true;
        _settings.PlaySound = PlaySoundCheck.IsChecked == true;
        _settings.SaveSettings();

        _notifications.ShowBanners = _settings.ShowBanners;
        _notifications.PlaySound = _settings.PlaySound;
    }

    private void TestNotification_Click(object sender, RoutedEventArgs e)
    {
        _notifications.SendTestNotification();
    }

    private void AlertToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        _settings.AlertSettings = _settings.AlertSettings with
        {
            UrgentLowEnabled = UrgentLowToggle.IsOn,
            LowEnabled = LowToggle.IsOn,
            HighEnabled = HighToggle.IsOn,
            UrgentHighEnabled = UrgentHighToggle.IsOn,
            RapidChangeAlerts = RisingQuicklyToggle.IsOn || FallingQuicklyToggle.IsOn,
            RisingAlerts = RisingToggle.IsOn,
            FallingAlerts = FallingToggle.IsOn,
            StaleDataAlertEnabled = StaleDataToggle.IsOn
        };
        _settings.SaveSettings();
        _viewModel.UpdateAlertSettings(_settings.AlertSettings);
    }

    private void UrgentLowSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isInitializing || UrgentLowThresholdText == null) return;

        var value = (int)e.NewValue;
        UrgentLowThresholdText.Text = $"Below {value} mg/dL";
        _settings.AlertSettings = _settings.AlertSettings with { UrgentLowThreshold = value };
        _settings.SaveSettings();
        _viewModel.UpdateAlertSettings(_settings.AlertSettings);
    }

    private void LowSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isInitializing || LowThresholdText == null) return;

        var value = (int)e.NewValue;
        LowThresholdText.Text = $"Below {value} mg/dL";
        _settings.AlertSettings = _settings.AlertSettings with { LowThreshold = value };
        _settings.SaveSettings();
        _viewModel.UpdateAlertSettings(_settings.AlertSettings);
    }

    private void HighSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isInitializing || HighThresholdText == null) return;

        var value = (int)e.NewValue;
        HighThresholdText.Text = $"Above {value} mg/dL";
        _settings.AlertSettings = _settings.AlertSettings with { HighThreshold = value };
        _settings.SaveSettings();
        _viewModel.UpdateAlertSettings(_settings.AlertSettings);
    }

    private void UrgentHighSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isInitializing || UrgentHighThresholdText == null) return;

        var value = (int)e.NewValue;
        UrgentHighThresholdText.Text = $"Above {value} mg/dL";
        _settings.AlertSettings = _settings.AlertSettings with { UrgentHighThreshold = value };
        _settings.SaveSettings();
        _viewModel.UpdateAlertSettings(_settings.AlertSettings);
    }

    private void StaleDataInterval_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        var value = GetComboSelectedTag(StaleDataIntervalCombo, 15);
        StaleDataThresholdText.Text = $"After {value} minutes";
        _settings.AlertSettings = _settings.AlertSettings with { StaleDataThreshold = value };
        _settings.SaveSettings();
        _viewModel.UpdateAlertSettings(_settings.AlertSettings);
    }

    private void NormalCooldown_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        var value = GetComboSelectedTag(NormalCooldownCombo, 15);
        _settings.NormalAlertCooldown = value;
        _settings.SaveSettings();
        _notifications.UpdateCooldowns(_settings.NormalAlertCooldown, _settings.UrgentAlertCooldown);
    }

    private void UrgentCooldown_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        var value = GetComboSelectedTag(UrgentCooldownCombo, 5);
        _settings.UrgentAlertCooldown = value;
        _settings.SaveSettings();
        _notifications.UpdateCooldowns(_settings.NormalAlertCooldown, _settings.UrgentAlertCooldown);
    }

    private void ResetAlerts_Click(object sender, RoutedEventArgs e)
    {
        _settings.AlertSettings = AlertSettings.Default;
        _settings.NormalAlertCooldown = 15;
        _settings.UrgentAlertCooldown = 5;
        _settings.SaveSettings();
        _viewModel.UpdateAlertSettings(_settings.AlertSettings);
        _notifications.UpdateCooldowns(_settings.NormalAlertCooldown, _settings.UrgentAlertCooldown);

        _isInitializing = true;
        LoadSettings();
        _isInitializing = false;
    }

    private void DoneButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

/// <summary>
/// Item for theme list
/// </summary>
public class ThemeItem
{
    public ColorTheme Theme { get; set; }
    public string Name { get; set; } = "";
    public Color SwatchColor { get; set; }
    public SolidColorBrush SwatchBrush => new SolidColorBrush(SwatchColor);
    public Visibility IsSelected { get; set; } = Visibility.Collapsed;
}
