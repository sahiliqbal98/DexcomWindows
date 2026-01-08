using DexcomWindows.Helpers;
using DexcomWindows.Models;
using DexcomWindows.Services;
using DexcomWindows.ViewModels;
using DexcomWindows.Views;
using H.NotifyIcon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using System.Runtime.InteropServices;
using Grid = Microsoft.UI.Xaml.Controls.Grid;

namespace DexcomWindows;

public partial class App : Application
{
    // Window sizes - MASSIVE unified window (2x bigger, everything in one place)
    private const int PopupWidth = 900;
    private const int PopupHeight = 1100;

    private Window? _mainWindow;
    private TaskbarIcon? _trayIcon;
    private Window? _popupWindow;
    private TrayPopupView? _popupView;
    private Grid? _popupRootGrid;
    private bool _isPopupAnimating;

    // Services
    private SettingsService? _settings;
    private DexcomShareAPI? _api;
    private CredentialManager? _credentials;
    private NotificationService? _notifications;
    private GlucoseViewModel? _viewModel;

    // For click-away detection
    private DispatcherTimer? _clickAwayTimer;

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private const int VK_LBUTTON = 0x01;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Initialize services
        _settings = new SettingsService();
        _api = new DexcomShareAPI(_settings.Server);
        _credentials = new CredentialManager(_settings);
        _notifications = new NotificationService
        {
            ShowBanners = _settings.ShowBanners,
            PlaySound = _settings.PlaySound
        };
        _notifications.UpdateCooldowns(_settings.NormalAlertCooldown, _settings.UrgentAlertCooldown);

        // Create main window (required to keep WinUI 3 app running)
        _mainWindow = new MainWindow();

        // Create view model
        _viewModel = new GlucoseViewModel(_api, _credentials, _notifications, _settings);
        _viewModel.UpdateAlertSettings(_settings.AlertSettings);

        // Subscribe to view model changes to update tray icon
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        // Subscribe to theme changes to update popup window
        ThemeService.Instance.ThemeChanged += OnThemeChanged;

        // Initialize tray icon
        InitializeTrayIcon();

        // Activate window (required) then it will hide itself
        _mainWindow.Activate();

        // Initialize view model AFTER window is activated (ensures dispatcher is ready)
        _mainWindow.DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                await _viewModel.InitializeAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ViewModel init error: {ex.Message}");
            }
        });

        // Setup click-away detection timer
        SetupClickAwayDetection();
    }

    private void OnThemeChanged(object? sender, Services.ColorTheme theme)
    {
        // Update popup window theme if open
        if (_popupWindow != null && _popupRootGrid != null)
        {
            _popupWindow.DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    _popupRootGrid.RequestedTheme = ThemeService.Instance.GetElementTheme();
                    
                    // Also trigger manual color application if the view supports it
                    if (_popupView != null)
                    {
                        // We can't call ApplyThemeColors directly as it's private, 
                        // but setting the theme on ThemeService triggers the change handling in the view too?
                        // Actually the view subscribes to ThemeChanged event separately in its constructor/init
                        // So we mainly just need to update the root grid theme here
                    }
                }
                catch { }
            });
        }
    }

    private void InitializeTrayIcon()
    {
        // Get the TaskbarIcon from app resources
        _trayIcon = (TaskbarIcon)Resources["TrayIcon"];

        if (_trayIcon == null)
        {
            System.Diagnostics.Debug.WriteLine("Failed to get TrayIcon from resources");
            return;
        }

        // Set up the icon
        UpdateTrayIcon();

        // Handle left click to show popup window
        _trayIcon.LeftClickCommand = new RelayCommand(TogglePopupWindow);

        // Set up context menu handlers
        SetupContextMenu();

        // Show the tray icon
        _trayIcon.ForceCreate();

        // Update tooltip
        UpdateTrayTooltip();
    }

    private void UpdateTrayIcon()
    {
        if (_trayIcon == null) return;

        try
        {
            // Use TrayIconRenderer to create glucose-colored icon
            _trayIcon.Icon = TrayIconRenderer.CreateGlucoseTextIcon(_viewModel?.CurrentReading);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to update icon: {ex.Message}");
            _trayIcon.Icon = TrayIconRenderer.CreateNoDataIcon();
        }
    }

    private void TogglePopupWindow()
    {
        if (_isPopupAnimating) return;

        try
        {
            if (_popupWindow != null)
            {
                ClosePopupWithAnimation();
                return;
            }

            ShowPopupWithAnimation();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"TogglePopupWindow error: {ex.Message}");
            _popupView = null;
            _popupWindow = null;
            _isPopupAnimating = false;
        }
    }

    private void ShowPopupWithAnimation()
    {
        _isPopupAnimating = true;

        // Determine size based on auth state
        bool isAuthenticated = _viewModel?.IsAuthenticated ?? false;
        int width = PopupWidth;
        int height = PopupHeight;

        // Create the window
        _popupWindow = new Window();

        // Create popup view
        _popupView = new TrayPopupView();
        _popupView.QuitRequested += PopupView_QuitRequested;
        _popupView.AuthStateChanged += PopupView_AuthStateChanged;

        // Create a container grid for the popup content with rounded corners
        var rootGrid = new Grid
        {
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent)
        };

        // Apply saved theme to the root grid
        rootGrid.RequestedTheme = ThemeService.Instance.GetElementTheme();

        // Create a border for rounded corners and shadow effect
        var border = new Border
        {
            Background = (Microsoft.UI.Xaml.Media.Brush)Current.Resources["ApplicationPageBackgroundThemeBrush"],
            CornerRadius = new CornerRadius(12),
            Child = _popupView
        };

        rootGrid.Children.Add(border);
        _popupWindow.Content = rootGrid;
        _popupRootGrid = rootGrid;

        // Make window borderless and position near tray
        WindowHelper.MakeBorderless(_popupWindow);
        WindowHelper.HideFromTaskbar(_popupWindow);
        WindowHelper.MakeLayered(_popupWindow);
        
        // Start window fully transparent at Win32 level (prevents flicker)
        WindowHelper.SetWindowOpacity(_popupWindow, 0);
        
        WindowHelper.PositionNearTray(_popupWindow, width, height);

        // Handle window closing
        _popupWindow.Closed += (_, _) =>
        {
            _popupView?.Cleanup();
            _popupView = null;
            _popupWindow = null;
        };

        // Show window (it's invisible due to opacity 0)
        _popupWindow.Activate();

        // Initialize popup view after activation
        _popupView.Initialize(_viewModel!, _settings!, _notifications);

        // Animate the window in smoothly
        AnimateWindowOpen();
    }

    private async void AnimateWindowOpen()
    {
        if (_popupWindow == null) return;
        
        try
        {
            // Small delay to ensure window is ready
            await Task.Delay(10);
            
            // Animate opacity from 0 to 255 over ~200ms
            const int steps = 12;
            const int delayPerStep = 16; // ~60fps
            
            for (int i = 1; i <= steps; i++)
            {
                if (_popupWindow == null) break;
                
                // Ease-out curve: faster at start, slower at end
                double t = (double)i / steps;
                double eased = 1 - Math.Pow(1 - t, 3); // Cubic ease-out
                byte opacity = (byte)(eased * 255);
                
                WindowHelper.SetWindowOpacity(_popupWindow, opacity);
                await Task.Delay(delayPerStep);
            }
            
            // Ensure fully opaque at end
            if (_popupWindow != null)
            {
                WindowHelper.SetWindowOpacity(_popupWindow, 255);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Animation error: {ex.Message}");
            // Ensure window is visible even if animation fails
            if (_popupWindow != null)
            {
                WindowHelper.SetWindowOpacity(_popupWindow, 255);
            }
        }
        
        _isPopupAnimating = false;
    }

    private async void ClosePopupWithAnimation()
    {
        if (_popupWindow == null) return;

        _isPopupAnimating = true;

        try
        {
            // Animate opacity from 255 to 0 over ~150ms
            const int steps = 9;
            const int delayPerStep = 16; // ~60fps
            
            for (int i = 1; i <= steps; i++)
            {
                if (_popupWindow == null) break;
                
                // Ease-in curve: slower at start, faster at end
                double t = (double)i / steps;
                double eased = t * t; // Quadratic ease-in
                byte opacity = (byte)((1 - eased) * 255);
                
                WindowHelper.SetWindowOpacity(_popupWindow, opacity);
                await Task.Delay(delayPerStep);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Close animation error: {ex.Message}");
        }

        // Close the window
        try { _popupWindow?.Close(); } catch { }
        _popupWindow = null;
        _popupView = null;
        _isPopupAnimating = false;
    }

    private void PopupView_AuthStateChanged(object? sender, bool isAuthenticated)
    {
        // Resize popup when auth state changes
        if (_popupWindow == null) return;

        int width = PopupWidth;
        int height = PopupHeight;

        WindowHelper.SetSize(_popupWindow, width, height);
    }

    private void SetupClickAwayDetection()
    {
        _clickAwayTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };

        _clickAwayTimer.Tick += (_, _) =>
        {
            if (_popupWindow == null || _isPopupAnimating) return;

            // Check if left mouse button is pressed
            if ((GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0)
            {
                GetCursorPos(out POINT cursorPos);

                // Check if click is outside the popup
                if (!WindowHelper.IsPointInWindow(_popupWindow, cursorPos.X, cursorPos.Y))
                {
                    // Also check if not clicking on tray icon (give it some margin)
                    // Close the popup
                    ClosePopupWithAnimation();
                }
            }
        };

        _clickAwayTimer.Start();
    }

    private void SetupContextMenu()
    {
        if (_trayIcon?.ContextFlyout is MenuFlyout flyout)
        {
            foreach (var item in flyout.Items)
            {
                if (item is MenuFlyoutItem menuItem)
                {
                    switch (menuItem.Text)
                    {
                        case "Refresh Now":
                            menuItem.Click += async (_, _) =>
                            {
                                if (_viewModel != null)
                                {
                                    await _viewModel.ForceRefreshAsync();
                                }
                            };
                            break;
                        case "Settings...":
                            menuItem.Click += (_, _) => TogglePopupWindow(); // Settings is now in the popup
                            break;
                        case "Quit Dexcom":
                            menuItem.Click += (_, _) => QuitApplication();
                            break;
                    }
                }
            }
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GlucoseViewModel.TrayText) or nameof(GlucoseViewModel.TrayTooltip) or nameof(GlucoseViewModel.CurrentReading))
        {
            UpdateTrayIcon();
            UpdateTrayTooltip();
        }
    }

    private void UpdateTrayTooltip()
    {
        if (_trayIcon == null || _viewModel == null) return;
        _trayIcon.ToolTipText = _viewModel.TrayTooltip;
    }

    private void PopupView_QuitRequested(object? sender, EventArgs e)
    {
        QuitApplication();
    }

    private void QuitApplication()
    {
        _clickAwayTimer?.Stop();
        _trayIcon?.Dispose();
        _trayIcon = null;

        _popupWindow?.Close();
        _mainWindow?.Close();

        Environment.Exit(0);
    }
}

// Simple relay command for the tray icon click
public class RelayCommand : System.Windows.Input.ICommand
{
    private readonly Action _execute;

    public RelayCommand(Action execute)
    {
        _execute = execute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => _execute();
}
