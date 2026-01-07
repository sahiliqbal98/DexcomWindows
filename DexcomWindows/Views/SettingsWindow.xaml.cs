using DexcomWindows.Models;
using DexcomWindows.Services;
using DexcomWindows.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DexcomWindows.Views;

public sealed partial class SettingsWindow : Window
{
    public event EventHandler? SignOutRequested;

    private readonly SettingsService _settings;
    private readonly NotificationService _notifications;
    private readonly GlucoseViewModel _viewModel;
    private bool _isInitializing = true;

    public SettingsWindow(SettingsService settings, NotificationService notifications, GlucoseViewModel viewModel)
    {
        InitializeComponent();
        _settings = settings;
        _notifications = notifications;
        _viewModel = viewModel;

        // Set window size and center
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new Windows.Graphics.SizeInt32(500, 650));

        LoadSettings();
        _isInitializing = false;

        // Select first nav item
        SettingsNav.SelectedItem = SettingsNav.MenuItems[0];
    }

    private void LoadSettings()
    {
        // General settings
        ThemeComboBox.SelectedIndex = (int)_settings.ColorTheme;
        TimerBarComboBox.SelectedIndex = (int)_settings.TimerBarStyle;

        TargetLowSlider.Value = _settings.TargetLow;
        TargetHighSlider.Value = _settings.TargetHigh;
        TargetLowValue.Text = _settings.TargetLow.ToString();
        TargetHighValue.Text = _settings.TargetHigh.ToString();

        StartupToggle.IsOn = _settings.StartWithWindows;

        // Alert settings
        var alerts = _settings.AlertSettings;
        AlertsEnabledToggle.IsOn = alerts.AlertsEnabled;
        ShowBannersCheck.IsChecked = _settings.ShowBanners;
        PlaySoundCheck.IsChecked = _settings.PlaySound;

        UrgentLowToggle.IsOn = alerts.UrgentLowEnabled;
        LowToggle.IsOn = alerts.LowEnabled;
        HighToggle.IsOn = alerts.HighEnabled;
        UrgentHighToggle.IsOn = alerts.UrgentHighEnabled;
        RapidChangeToggle.IsOn = alerts.RapidChangeAlerts;
        StaleDataToggle.IsOn = alerts.StaleDataAlertEnabled;

        UrgentLowThresholdText.Text = $"Below {alerts.UrgentLowThreshold} mg/dL";
        LowThresholdText.Text = $"Below {alerts.LowThreshold} mg/dL";
        HighThresholdText.Text = $"Above {alerts.HighThreshold} mg/dL";
        UrgentHighThresholdText.Text = $"Above {alerts.UrgentHighThreshold} mg/dL";
        StaleDataThresholdText.Text = $"No new data for {alerts.StaleDataThreshold}+ minutes";

        // Account status
        AccountStatusText.Text = _viewModel.IsAuthenticated ? "Signed in" : "Not signed in";
        ServerText.Text = DexcomShareAPI.GetServerDisplayName(_settings.Server);
    }

    private void SettingsNav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            GeneralPanel.Visibility = tag == "General" ? Visibility.Visible : Visibility.Collapsed;
            AlertsPanel.Visibility = tag == "Alerts" ? Visibility.Visible : Visibility.Collapsed;
            AboutPanel.Visibility = tag == "About" ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void SignOutButton_Click(object sender, RoutedEventArgs e)
    {
        SignOutRequested?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        _settings.ColorTheme = (ColorTheme)ThemeComboBox.SelectedIndex;
        _settings.SaveSettings();
    }

    private void TimerBarComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        _settings.TimerBarStyle = (TimerBarStyle)TimerBarComboBox.SelectedIndex;
        _settings.SaveSettings();
    }

    private void TargetLowSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isInitializing) return;

        var value = (int)e.NewValue;
        TargetLowValue.Text = value.ToString();
        _settings.TargetLow = value;
        _settings.SaveSettings();
    }

    private void TargetHighSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isInitializing) return;

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

        _settings.AlertSettings = _settings.AlertSettings.With(
            urgentLowEnabled: UrgentLowToggle.IsOn,
            lowEnabled: LowToggle.IsOn,
            highEnabled: HighToggle.IsOn,
            urgentHighEnabled: UrgentHighToggle.IsOn,
            rapidChangeAlerts: RapidChangeToggle.IsOn,
            staleDataAlertEnabled: StaleDataToggle.IsOn
        );
        _settings.SaveSettings();
        _viewModel.UpdateAlertSettings(_settings.AlertSettings);
    }

    private void ResetAlerts_Click(object sender, RoutedEventArgs e)
    {
        _settings.AlertSettings = AlertSettings.Default;
        _settings.SaveSettings();
        _viewModel.UpdateAlertSettings(_settings.AlertSettings);

        _isInitializing = true;
        LoadSettings();
        _isInitializing = false;
    }
}
