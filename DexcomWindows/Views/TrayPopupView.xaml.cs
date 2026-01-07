using DexcomWindows.Models;
using DexcomWindows.Services;
using DexcomWindows.Themes;
using DexcomWindows.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DexcomWindows.Views;

public sealed partial class TrayPopupView : UserControl
{
    public event EventHandler? SettingsRequested;
    public event EventHandler? QuitRequested;

    private GlucoseViewModel? _viewModel;
    private SettingsService? _settings;
    private bool _isInitialized = false;

    public TrayPopupView()
    {
        InitializeComponent();
    }

    public void Initialize(GlucoseViewModel viewModel, SettingsService settings)
    {
        try
        {
            _viewModel = viewModel;
            _settings = settings;

            // Subscribe to property changes
            viewModel.PropertyChanged += ViewModel_PropertyChanged;

            // Initial UI update
            UpdateAuthenticationState();
            UpdateUI();
            
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Initialize failed: {ex.Message}");
        }
    }

    public void Cleanup()
    {
        _isInitialized = false;
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }
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
                            break;
                        case nameof(GlucoseViewModel.IsAuthenticated):
                            UpdateAuthenticationState();
                            break;
                        case nameof(GlucoseViewModel.IsLoading):
                            // Update loading state if needed
                            break;
                        case nameof(GlucoseViewModel.Error):
                            UpdateError();
                            break;
                        case nameof(GlucoseViewModel.RefreshProgress):
                            RefreshProgressBarValue();
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
            
            // Update UI if authenticated
            if (isAuthenticated)
            {
                UpdateUI();
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
                TimeAgoText.Text = reading.TimeAgoString;
                TrendDescriptionText.Text = reading.Trend.Description();

                // Update header color based on glucose value
                var color = ColorThemes.GetGlucoseColor(reading.ColorCategory);
                HeaderBackground.Color = color;
            }
            else
            {
                GlucoseValueText.Text = "--";
                TrendArrowText.Text = "";
                TimeAgoText.Text = "";
                TrendDescriptionText.Text = "";
                HeaderBackground.Color = Colors.Gray;
            }

            // Update statistics
            var stats = _viewModel.Statistics;
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
                TimeInRangeText.Text = "--";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UpdateUI error: {ex.Message}");
        }
    }

    private void RefreshProgressBarValue()
    {
        try
        {
            if (_viewModel == null) return;

            var progress = _viewModel.RefreshProgress * 100;
            UpdateProgressBar.Value = _settings?.TimerBarStyle == TimerBarStyle.Remaining
                ? 100 - progress
                : progress;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RefreshProgressBarValue error: {ex.Message}");
        }
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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UpdateError error: {ex.Message}");
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

        // Get selected server
        var serverItem = (ComboBoxItem)ServerComboBox.SelectedItem;
        var server = serverItem.Tag?.ToString() == "International"
            ? DexcomShareAPI.Server.International
            : DexcomShareAPI.Server.US;

        // Show loading
        LoginButton.IsEnabled = false;
        LoginProgress.IsActive = true;
        LoginProgress.Visibility = Visibility.Visible;
        LoginErrorBar.IsOpen = false;

        try
        {
            await _viewModel.LoginAsync(username, password, server);

            if (!_viewModel.IsAuthenticated && _viewModel.Error != null)
            {
                LoginErrorBar.Title = _viewModel.Error.Message;
                LoginErrorBar.Message = _viewModel.Error.RecoverySuggestion;
                LoginErrorBar.IsOpen = true;
            }
        }
        catch (Exception ex)
        {
            LoginErrorBar.Title = ex.Message;
            LoginErrorBar.IsOpen = true;
        }
        finally
        {
            LoginButton.IsEnabled = true;
            LoginProgress.IsActive = false;
            LoginProgress.Visibility = Visibility.Collapsed;
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
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void QuitButton_Click(object sender, RoutedEventArgs e)
    {
        QuitRequested?.Invoke(this, EventArgs.Empty);
    }

    public void RefreshData()
    {
        try
        {
            UpdateUI();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RefreshData error: {ex.Message}");
        }
    }
}
