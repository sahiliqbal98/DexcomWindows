using DexcomWindows.Models;
using DexcomWindows.Services;
using DexcomWindows.Themes;
using DexcomWindows.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using System.Threading.Tasks;

namespace DexcomWindows.Views;

public sealed partial class TrayPopupView : UserControl
{
    public event EventHandler? QuitRequested;
    public event EventHandler<bool>? AuthStateChanged;

    private GlucoseViewModel? _viewModel;
    private SettingsService? _settings;
    private NotificationService? _notifications;
    private bool _isInitialized = false;
    private bool _settingsOpen = false;
    private DispatcherTimer? _uiUpdateTimer;
    private TimeRange _selectedTimeRange = TimeRange.ThreeHours;


    public TrayPopupView()
    {
        InitializeComponent();
    }

    public void Initialize(GlucoseViewModel viewModel, SettingsService settings, NotificationService? notifications = null)
    {
        try
        {
            _viewModel = viewModel;
            _settings = settings;
            _notifications = notifications;
            _selectedTimeRange = settings.DefaultTimeRange;

            // Subscribe to property changes
            viewModel.PropertyChanged += ViewModel_PropertyChanged;

            // Apply saved theme on startup
            ThemeService.Instance.SetTheme(settings.ColorTheme);
            this.RequestedTheme = ThemeService.Instance.GetElementTheme();

            // Initial UI update - these are instant, use cached data from viewModel
            UpdateAuthenticationState();
            ApplyThemeColors();
            UpdateTimeRangeSelection();
            UpdateUI();
            UpdateAlertButton();
            UpdateLoadingState(); // Ensure spinner is hidden if not loading
            LoadSettingsValues();
            UpdateLastUpdatedTime();

            _isInitialized = true;

            // Start UI update timer for progress bar and times
            StartUIUpdateTimer();

            // Defer chart rendering slightly so popup appears instantly
            // Chart is heavier to render, but data is already in memory
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                if (_isInitialized)
                {
                    UpdateChart();
                    UpdateTimerBar();
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Initialize failed: {ex.Message}");
        }
    }

    public void Cleanup()
    {
        _isInitialized = false;
        _uiUpdateTimer?.Stop();
        _uiUpdateTimer = null;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }
    }

    private void StartUIUpdateTimer()
    {
        _uiUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _uiUpdateTimer.Tick += (_, _) =>
        {
            if (!_isInitialized) return;
            UpdateTimerBar();
            UpdateTimeAgo();
        };
        _uiUpdateTimer.Start();
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!_isInitialized) return;

        try
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                if (!_isInitialized) return;

                try
        {
            switch (e.PropertyName)
            {
                case nameof(GlucoseViewModel.CurrentReading):
                case nameof(GlucoseViewModel.Readings):
                case nameof(GlucoseViewModel.Statistics):
                    UpdateUI();
                    UpdateChart();
                    break;
                case nameof(GlucoseViewModel.IsAuthenticated):
                    UpdateAuthenticationState();
                    break;
                case nameof(GlucoseViewModel.IsLoading):
                            UpdateLoadingState();
                    break;
                case nameof(GlucoseViewModel.Error):
                    UpdateError();
                    break;
                        case nameof(GlucoseViewModel.LastRefreshTime):
                            UpdateLastUpdatedTime();
                    break;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"PropertyChanged handler error: {ex.Message}");
            }
        });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DispatcherQueue error: {ex.Message}");
        }
    }

    private void UpdateAuthenticationState()
    {
        try
    {
        if (_viewModel == null) return;

        var isAuthenticated = _viewModel.IsAuthenticated;
            LoginPanel.Visibility = isAuthenticated ? Visibility.Collapsed : Visibility.Visible;
        MainContent.Visibility = isAuthenticated ? Visibility.Visible : Visibility.Collapsed;

            // Notify parent about auth state change
            AuthStateChanged?.Invoke(this, isAuthenticated);

            // Update UI if authenticated
            if (isAuthenticated)
            {
                UpdateUI();
                UpdateChart();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UpdateAuthenticationState error: {ex.Message}");
        }
    }

    private void UpdateUI()
    {
        try
    {
        if (_viewModel == null) return;

        var reading = _viewModel.CurrentReading;

        if (reading != null)
        {
            GlucoseValueText.Text = reading.Value.ToString();
            TrendArrowText.Text = reading.Trend.Symbol();
            TrendDescriptionText.Text = reading.Trend.Description();

            var color = ColorThemes.GetGlucoseColor(reading.ColorCategory);
                GlucoseValueText.Foreground = new SolidColorBrush(color);
                TrendArrowText.Foreground = new SolidColorBrush(color);
                HeaderGradientStart.Color = Color.FromArgb(48, color.R, color.G, color.B);

                UpdateTimeAgo();
        }
        else
        {
            GlucoseValueText.Text = "--";
            TrendArrowText.Text = "";
                TrendDescriptionText.Text = "";
            TimeAgoText.Text = "";
                StaleWarningIcon.Visibility = Visibility.Collapsed;
                HeaderGradientStart.Color = Color.FromArgb(48, 128, 128, 128);
            }

            UpdateStatistics();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UpdateUI error: {ex.Message}");
        }
    }

    private void UpdateTimeAgo()
    {
        try
        {
            if (_viewModel?.CurrentReading == null) return;

            var reading = _viewModel.CurrentReading;
            TimeAgoText.Text = reading.TimeAgoString;

            var minutesAgo = reading.MinutesAgo;
            if (minutesAgo > 5)
            {
                StaleWarningIcon.Visibility = Visibility.Visible;
                TimeAgoText.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 149, 0));
            }
            else
            {
                StaleWarningIcon.Visibility = Visibility.Collapsed;
                TimeAgoText.Foreground = (SolidColorBrush)Resources["TextFillColorSecondaryBrush"]
                    ?? new SolidColorBrush(Colors.Gray);
        }
        }
        catch { }
    }

    private void UpdateStatistics()
    {
        try
        {
            var stats = _viewModel?.Statistics;
        if (stats != null)
        {
            AverageText.Text = stats.Average.ToString();
            StdDevText.Text = stats.FormattedStandardDeviation;
            TimeInRangeText.Text = stats.FormattedTimeInRange;
        }
        else
        {
            AverageText.Text = "--";
            StdDevText.Text = "--";
                TimeInRangeText.Text = "--%";
        }
        }
        catch { }
    }

    private void UpdateChart()
    {
        try
        {
            if (_viewModel == null || GlucoseChart == null) return;

            var readings = _viewModel.GetReadingsForRange(_selectedTimeRange);
            GlucoseChart.SetReadings(readings, _settings?.TargetLow ?? 80, _settings?.TargetHigh ?? 160);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UpdateChart error: {ex.Message}");
        }
    }

    private void UpdateTimerBar()
    {
        try
        {
            if (_viewModel == null || ProgressBarFill == null) return;

            var progress = _viewModel.RefreshProgress;
            var isRemainingMode = _settings?.TimerBarStyle == TimerBarStyle.Remaining;
            var displayProgress = isRemainingMode ? (1 - progress) : progress;

            var containerWidth = ((Border)ProgressBarFill.Parent).ActualWidth;
            if (containerWidth > 0)
        {
                ProgressBarFill.Width = containerWidth * Math.Min(1, Math.Max(0, displayProgress));
            }

            var seconds = _viewModel.SecondsSinceLastReading;
            var minutes = seconds / 60;
            var remainingSeconds = seconds % 60;
            TimerText.Text = $"{minutes}:{remainingSeconds:D2}";

            TimerDirectionArrow.Text = isRemainingMode ? "←" : "→";
            TimerLabel.Text = isRemainingMode ? "Time until update" : "Time since reading";

            if (progress >= 1.0)
            {
                TimerText.Foreground = new SolidColorBrush(Color.FromArgb(255, 52, 199, 89));
            }
            else
            {
                TimerText.Foreground = (SolidColorBrush)Resources["TextFillColorSecondaryBrush"]
                    ?? new SolidColorBrush(Colors.Gray);
            }
        }
        catch { }
    }

    private void UpdateLastUpdatedTime()
    {
        try
        {
            if (_viewModel?.LastRefreshTime != null)
            {
                LastUpdatedText.Text = $"Updated {_viewModel.LastRefreshTime.Value:h:mm tt}";
            }
            else
            {
                LastUpdatedText.Text = "Updated --";
            }
        }
        catch { }
    }

    private void UpdateLoadingState()
    {
        try
        {
            var isLoading = _viewModel?.IsLoading ?? false;
            RefreshSpinner.IsActive = isLoading;
            RefreshSpinner.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        }
        catch { }
    }

    private void UpdateAlertButton()
    {
        try
        {
            var alertsEnabled = _settings?.AlertSettings.AlertsEnabled ?? true;
            AlertIcon.Glyph = alertsEnabled ? "\uEA8F" : "\uE7ED";
            AlertIcon.Foreground = new SolidColorBrush(
                alertsEnabled ? Color.FromArgb(255, 52, 199, 89) : Color.FromArgb(255, 255, 59, 48));
        }
        catch { }
    }

    private static readonly Color SelectedColor = Color.FromArgb(70, 0, 120, 215);
    private static readonly Color HoverColor = Color.FromArgb(30, 0, 120, 215);

    private void UpdateTimeRangeSelection()
    {
        try
        {
            // Reset all buttons to transparent
            var allButtons = new[] { Range1H, Range3H, Range6H, Range12H, Range24H };
            foreach (var btn in allButtons)
            {
                btn.Background = new SolidColorBrush(Colors.Transparent);
            }

            Button? selectedButton = _selectedTimeRange switch
            {
                TimeRange.OneHour => Range1H,
                TimeRange.ThreeHours => Range3H,
                TimeRange.SixHours => Range6H,
                TimeRange.TwelveHours => Range12H,
                TimeRange.TwentyFourHours => Range24H,
                _ => Range3H
            };

            if (selectedButton != null)
            {
                // Set selected background immediately
                selectedButton.Background = new SolidColorBrush(SelectedColor);
        }
        }
        catch { }
    }

    private void TimeRangeButton_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is not Button button) return;
        
        // Don't change if this is the selected button
        if (IsSelectedTimeRangeButton(button)) return;
        
        // Light hover effect
        button.Background = new SolidColorBrush(HoverColor);
    }

    private void TimeRangeButton_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is not Button button) return;
        
        // Don't change if this is the selected button
        if (IsSelectedTimeRangeButton(button)) return;
        
        // Remove hover effect
        button.Background = new SolidColorBrush(Colors.Transparent);
    }

    private bool IsSelectedTimeRangeButton(Button button)
    {
        return _selectedTimeRange switch
        {
            TimeRange.OneHour => button == Range1H,
            TimeRange.ThreeHours => button == Range3H,
            TimeRange.SixHours => button == Range6H,
            TimeRange.TwelveHours => button == Range12H,
            TimeRange.TwentyFourHours => button == Range24H,
            _ => false
        };
    }

    private async void AnimateButtonClick(Button button)
    {
        try
        {
            // Quick scale down
            button.RenderTransform = new ScaleTransform();
            button.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
            
            var scaleTransform = (ScaleTransform)button.RenderTransform;
            
            // Animate scale down then back up
            scaleTransform.ScaleX = 0.95;
            scaleTransform.ScaleY = 0.95;
            
            await Task.Delay(80);
            
            scaleTransform.ScaleX = 1.0;
            scaleTransform.ScaleY = 1.0;
        }
        catch { }
    }

    private void UpdateError()
    {
        try
        {
        if (_viewModel?.Error != null)
        {
            ErrorBar.Title = _viewModel.Error.Message;
            ErrorBar.Message = _viewModel.Error.RecoverySuggestion;
            ErrorBar.IsOpen = true;
        }
        else
        {
            ErrorBar.IsOpen = false;
        }
    }
        catch { }
    }

    // ==================== Settings Panel ====================

    /// <summary>
    /// Opens the settings panel (public for external access)
    /// </summary>
    public void OpenSettingsPanel()
    {
        if (!_settingsOpen)
        {
            _settingsOpen = true;
            SettingsPanel.Visibility = Visibility.Visible;
            LoadSettingsValues();
        }
    }

    private void ToggleSettingsPanel()
    {
        _settingsOpen = !_settingsOpen;

        if (_settingsOpen)
        {
            SettingsPanel.Visibility = Visibility.Visible;
            LoadSettingsValues();
        }
        else
        {
            SettingsPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void LoadSettingsValues()
    {
        try
        {
            if (_settings == null) return;

            // Account info
            SettingsAccountStatus.Text = _viewModel?.IsAuthenticated == true ? "Signed in" : "Not signed in";
            SettingsServerName.Text = _settings.Server == DexcomShareAPI.Server.US ? "United States" : "International";

            // Theme - temporarily disable handler during load
            _isInitialized = false;
            for (int i = 0; i < SettingsThemeComboBox.Items.Count; i++)
            {
                var item = (ComboBoxItem)SettingsThemeComboBox.Items[i];
                if (item.Tag?.ToString() == _settings.ColorTheme.ToString())
                {
                    SettingsThemeComboBox.SelectedIndex = i;
                    break;
                }
            }

            // Timer bar style
            SettingsTimerBarComboBox.SelectedIndex = (int)_settings.TimerBarStyle;

            // Target range
            SettingsTargetLowSlider.Value = _settings.TargetLow;
            SettingsTargetHighSlider.Value = _settings.TargetHigh;
            SettingsTargetLowValue.Text = _settings.TargetLow.ToString();
            SettingsTargetHighValue.Text = _settings.TargetHigh.ToString();

            // Alerts
            var alerts = _settings.AlertSettings;
            SettingsAlertsEnabled.IsOn = alerts.AlertsEnabled;
            SettingsLowAlert.IsOn = alerts.LowEnabled;
            SettingsHighAlert.IsOn = alerts.HighEnabled;
            SettingsUrgentLowAlert.IsOn = alerts.UrgentLowEnabled;
            SettingsUrgentHighAlert.IsOn = alerts.UrgentHighEnabled;

            // Startup - check actual registry state
            SettingsStartWithWindows.IsOn = IsStartupEnabled();
            
            // Re-enable handlers
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadSettingsValues error: {ex.Message}");
            _isInitialized = true;
            }
    }
    
    private bool IsStartupEnabled()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
            
            return key?.GetValue("DexcomWindows") != null;
        }
        catch
        {
            return false;
        }
    }

    // ==================== Event Handlers ====================

    private void PasswordBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            LoginButton_Click(LoginButton, new RoutedEventArgs());
        }
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;

        var username = UsernameTextBox.Text.Trim();
        var password = PasswordBox.Password;

        if (string.IsNullOrEmpty(username))
        {
            LoginErrorBar.Title = "Please enter your username or email";
            LoginErrorBar.IsOpen = true;
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            LoginErrorBar.Title = "Please enter your password";
            LoginErrorBar.IsOpen = true;
            return;
        }

        var serverItem = (ComboBoxItem)ServerComboBox.SelectedItem;
        var server = serverItem.Tag?.ToString() == "International"
            ? DexcomShareAPI.Server.International
            : DexcomShareAPI.Server.US;

        LoginButton.IsEnabled = false;
        LoginProgress.IsActive = true;
        LoginProgress.Visibility = Visibility.Visible;
        LoginErrorBar.IsOpen = false;

        try
        {
            await _viewModel.LoginAsync(username, password, server);

            if (!_viewModel.IsAuthenticated)
            {
                if (_viewModel.Error != null)
                {
                    LoginErrorBar.Title = _viewModel.Error.Message;
                    LoginErrorBar.Message = _viewModel.Error.RecoverySuggestion ?? "";
                    LoginErrorBar.IsOpen = true;
                }
                else
                {
                    LoginErrorBar.Title = "Login failed";
                    LoginErrorBar.Message = "Unknown error occurred";
                    LoginErrorBar.IsOpen = true;
                }
            }
        }
        catch (Exception ex)
        {
            LoginErrorBar.Title = "Login error";
            LoginErrorBar.Message = ex.Message;
            LoginErrorBar.IsOpen = true;
        }
        finally
        {
            LoginButton.IsEnabled = true;
            LoginProgress.IsActive = false;
            LoginProgress.Visibility = Visibility.Collapsed;
        }
    }

    private void TimeRange_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;

        var tag = button.Tag?.ToString();
        _selectedTimeRange = tag switch
        {
            "OneHour" => TimeRange.OneHour,
            "ThreeHours" => TimeRange.ThreeHours,
            "SixHours" => TimeRange.SixHours,
            "TwelveHours" => TimeRange.TwelveHours,
            "TwentyFourHours" => TimeRange.TwentyFourHours,
            _ => TimeRange.ThreeHours
        };

        // Immediately update selection visuals
        UpdateTimeRangeSelection();
        
        // Animate the click
        AnimateButtonClick(button);

        if (_viewModel != null)
        {
            _viewModel.SelectedTimeRange = _selectedTimeRange;
        }

        UpdateChart();
    }

    private void AlertToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_settings == null) return;

        _settings.AlertSettings = _settings.AlertSettings with { AlertsEnabled = !_settings.AlertSettings.AlertsEnabled };
        _settings.SaveSettings();

        UpdateAlertButton();
        
        // Also update settings panel if open
        if (_settingsOpen)
        {
            SettingsAlertsEnabled.IsOn = _settings.AlertSettings.AlertsEnabled;
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        try
    {
        if (_viewModel != null)
            {
                await _viewModel.ForceRefreshAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RefreshButton_Click error: {ex.Message}");
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleSettingsPanel();
    }

    private void CloseSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _settingsOpen = false;
        SettingsPanel.Visibility = Visibility.Collapsed;
    }

    private void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        // Close settings panel if open
        if (_settingsOpen)
        {
            _settingsOpen = false;
            SettingsPanel.Visibility = Visibility.Collapsed;
        }

        _viewModel?.Logout();
    }

    private void QuitButton_Click(object sender, RoutedEventArgs e)
    {
        QuitRequested?.Invoke(this, EventArgs.Empty);
    }

    // ==================== Settings Event Handlers ====================

    private void SettingsTheme_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_settings == null || !_isInitialized) return;

        try
        {
            var selectedItem = (ComboBoxItem)SettingsThemeComboBox.SelectedItem;
            if (selectedItem?.Tag != null && Enum.TryParse<ColorTheme>(selectedItem.Tag.ToString(), out var theme))
            {
                _settings.ColorTheme = theme;
                _settings.SaveSettings();
                
            // Apply theme immediately
            ThemeService.Instance.SetTheme(theme);
            ApplyThemeColors(); // Add this
            
            // Update the UI element theme
            if (this.XamlRoot != null)
                {
                    this.RequestedTheme = ThemeService.Instance.GetElementTheme();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Theme change error: {ex.Message}");
        }
    }

    private void SettingsTimerBar_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_settings == null || !_isInitialized) return;

        try
        {
            _settings.TimerBarStyle = (TimerBarStyle)SettingsTimerBarComboBox.SelectedIndex;
            _settings.SaveSettings();
            UpdateTimerBar();
        }
        catch { }
    }

    private void SettingsTargetLow_Changed(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_settings == null || !_isInitialized) return;

        try
        {
            var value = (int)e.NewValue;
            SettingsTargetLowValue.Text = value.ToString();
            _settings.TargetLow = value;
            _settings.SaveSettings();
            UpdateChart();
        }
        catch { }
    }

    private void SettingsTargetHigh_Changed(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_settings == null || !_isInitialized) return;

        try
        {
            var value = (int)e.NewValue;
            SettingsTargetHighValue.Text = value.ToString();
            _settings.TargetHigh = value;
            _settings.SaveSettings();
            UpdateChart();
        }
        catch { }
    }

    private void SettingsAlerts_Toggled(object sender, RoutedEventArgs e)
    {
        if (_settings == null || !_isInitialized) return;

        try
        {
            _settings.AlertSettings = _settings.AlertSettings with
            {
                AlertsEnabled = SettingsAlertsEnabled.IsOn,
                LowEnabled = SettingsLowAlert.IsOn,
                HighEnabled = SettingsHighAlert.IsOn,
                UrgentLowEnabled = SettingsUrgentLowAlert.IsOn,
                UrgentHighEnabled = SettingsUrgentHighAlert.IsOn
            };
            _settings.SaveSettings();
            _viewModel?.UpdateAlertSettings(_settings.AlertSettings);
            UpdateAlertButton();
            
            System.Diagnostics.Debug.WriteLine($"Alerts updated: Enabled={_settings.AlertSettings.AlertsEnabled}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Alert toggle error: {ex.Message}");
        }
    }

    private void SettingsStartup_Toggled(object sender, RoutedEventArgs e)
    {
        if (_settings == null || !_isInitialized) return;

        try
        {
            _settings.StartWithWindows = SettingsStartWithWindows.IsOn;
            _settings.SaveSettings();
            
            // Actually set/remove the startup registry entry
            SetStartupRegistry(SettingsStartWithWindows.IsOn);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Startup toggle error: {ex.Message}");
        }
    }
    
    private void SetStartupRegistry(bool enable)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            
            if (key == null) return;
            
            const string appName = "DexcomWindows";
            
            if (enable)
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(appName, $"\"{exePath}\"");
                }
            }
            else
            {
                key.DeleteValue(appName, false);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Registry error: {ex.Message}");
        }
    }

    private void SettingsTestNotification_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_notifications != null)
            {
                // Get current glucose value and color if available
                var reading = _viewModel?.CurrentReading;
                if (reading != null)
                {
                    var color = ColorThemes.GetGlucoseColor(reading.ColorCategory);
                    var drawingColor = System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
                    _notifications.SendTestNotification(reading.Value, drawingColor);
                }
                else
                {
                    _notifications.SendTestNotification();
                }
                System.Diagnostics.Debug.WriteLine("Test notification sent");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Notifications service is null");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Test notification error: {ex.Message}");
        }
    }


    public void RefreshData()
    {
        try
    {
        UpdateUI();
        UpdateChart();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RefreshData error: {ex.Message}");
        }
    }

    private void ApplyThemeColors()
    {
        try
        {
            var colors = ThemeService.Instance.CurrentColors;
            
            // Root background
            if (this.Content is Grid rootGrid)
            {
                rootGrid.Background = new SolidColorBrush(colors.BackgroundColor);
            }

            // Settings panel background
            SettingsPanel.Background = new SolidColorBrush(colors.BackgroundColor);
            SettingsPanel.BorderBrush = new SolidColorBrush(colors.SecondaryBackgroundColor);

            // Chart backgrounds
            if (GlucoseChart.Parent is Border chartBorder)
            {
                chartBorder.Background = new SolidColorBrush(colors.SecondaryBackgroundColor);
            }

            // Stats row background
            if (AverageText.Parent is StackPanel avgPanel && avgPanel.Parent is Grid statsGrid && statsGrid.Parent is Border statsBorder)
            {
                statsBorder.Background = new SolidColorBrush(colors.SecondaryBackgroundColor);
            }

            // Time range picker background
            TimeRangePicker.Background = new SolidColorBrush(colors.SecondaryBackgroundColor);

            // Text colors
            GlucoseValueText.Foreground = new SolidColorBrush(colors.PrimaryTextColor);
            TrendArrowText.Foreground = new SolidColorBrush(colors.PrimaryTextColor);
            TrendDescriptionText.Foreground = new SolidColorBrush(colors.SecondaryTextColor);
            TimeAgoText.Foreground = new SolidColorBrush(colors.SecondaryTextColor);
            
            // Update header gradient
            // HeaderGradientStart.Color is updated in UpdateUI based on glucose value, 
            // but we might want to respect theme here too if needed.
            
            // Footer border
            if (LastUpdatedText.Parent is Grid footerGrid && footerGrid.Parent is Border footerBorder)
            {
                footerBorder.BorderBrush = new SolidColorBrush(colors.SecondaryBackgroundColor);
            }
            
            LastUpdatedText.Foreground = new SolidColorBrush(colors.SecondaryTextColor);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ApplyThemeColors error: {ex.Message}");
        }
    }
}
