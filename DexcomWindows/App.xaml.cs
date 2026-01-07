using DexcomWindows.Models;
using DexcomWindows.Services;
using DexcomWindows.ViewModels;
using DexcomWindows.Views;
using H.NotifyIcon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Drawing;
using System.Drawing.Drawing2D;
using Grid = Microsoft.UI.Xaml.Controls.Grid;
using HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment;
using VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment;
using Thickness = Microsoft.UI.Xaml.Thickness;

namespace DexcomWindows;

public partial class App : Application
{
    private Window? _mainWindow;
    private TaskbarIcon? _trayIcon;
    private Window? _popupWindow;
    private TrayPopupView? _popupView;
    private SettingsWindow? _settingsWindow;

    // Services
    private SettingsService? _settings;
    private DexcomShareAPI? _api;
    private CredentialManager? _credentials;
    private NotificationService? _notifications;
    private GlucoseViewModel? _viewModel;

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
            var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", "app-icon.ico");
            if (System.IO.File.Exists(iconPath))
            {
                _trayIcon.Icon = new Icon(iconPath);
            }
            else
            {
                // Create a simple colored icon programmatically
                _trayIcon.Icon = CreateDefaultIcon();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load icon: {ex.Message}");
            _trayIcon.Icon = CreateDefaultIcon();
        }
    }

    private Icon CreateDefaultIcon()
    {
        // Create a simple 16x16 icon with a green circle (Dexcom-like)
        using var bitmap = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        
        // Draw a green circle
        using var brush = new SolidBrush(Color.FromArgb(52, 199, 89));
        g.FillEllipse(brush, 1, 1, 14, 14);
        
        // Draw border
        using var pen = new Pen(Color.FromArgb(40, 160, 70), 1);
        g.DrawEllipse(pen, 1, 1, 13, 13);

        return Icon.FromHandle(bitmap.GetHicon());
    }

    private void TogglePopupWindow()
    {
        try
        {
            if (_popupWindow != null)
            {
                try { _popupWindow.Close(); } catch { }
                _popupWindow = null;
                _popupView = null;
                return;
            }

            _popupWindow = new Window();
            _popupWindow.Title = "Dexcom";
            
            // Create the popup view
            _popupView = new TrayPopupView();
            _popupView.SettingsRequested += PopupView_SettingsRequested;
            _popupView.QuitRequested += PopupView_QuitRequested;
            
            _popupWindow.Content = _popupView;

            // Configure window
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_popupWindow);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            // Make window larger
            appWindow.Resize(new Windows.Graphics.SizeInt32(500, 700));

            // Position near system tray (bottom-right)
            var displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(windowId, Microsoft.UI.Windowing.DisplayAreaFallback.Primary);
            var workArea = displayArea.WorkArea;
            var x = workArea.X + workArea.Width - 520;
            var y = workArea.Y + workArea.Height - 720;
            appWindow.Move(new Windows.Graphics.PointInt32(x, y));

            // Make window resizable with title bar
            if (appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
            {
                presenter.IsResizable = true;
                presenter.IsMaximizable = true;
                presenter.IsMinimizable = true;
            }

            // Handle window closing
            _popupWindow.Closed += (_, _) => 
            {
                _popupView?.Cleanup();
                _popupView = null;
                _popupWindow = null;
            };

            _popupWindow.Activate();
            
            // Initialize AFTER window is activated
            _popupView.Initialize(_viewModel!, _settings!);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"TogglePopupWindow error: {ex.Message}");
            _popupView = null;
            _popupWindow = null;
        }
    }

    private void SetupContextMenu()
    {
        if (_trayIcon?.ContextFlyout is MenuFlyout flyout)
        {
            foreach (var item in flyout.Items)
            {
                if (item is MenuFlyoutItem menuItem)
                {
                    // Match by text content since x:Name may not be accessible from resources
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
                            menuItem.Click += (_, _) => OpenSettings();
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
            UpdateTrayTooltip();
        }
    }

    private void UpdateTrayTooltip()
    {
        if (_trayIcon == null || _viewModel == null) return;

        _trayIcon.ToolTipText = _viewModel.TrayTooltip;
    }

    private void PopupView_SettingsRequested(object? sender, EventArgs e)
    {
        _popupWindow?.Close();
        OpenSettings();
    }

    private void OpenSettings()
    {
        if (_settingsWindow != null)
        {
            // Bring existing window to front
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settings!, _notifications!, _viewModel!);
        _settingsWindow.SignOutRequested += SettingsWindow_SignOutRequested;
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Activate();
    }

    private void SettingsWindow_SignOutRequested(object? sender, EventArgs e)
    {
        _viewModel?.Logout();
    }

    private void PopupView_QuitRequested(object? sender, EventArgs e)
    {
        QuitApplication();
    }

    private void QuitApplication()
    {
        // Dispose tray icon
        _trayIcon?.Dispose();
        _trayIcon = null;

        // Close any open windows
        _popupWindow?.Close();
        _settingsWindow?.Close();
        _mainWindow?.Close();

        // Exit application
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
