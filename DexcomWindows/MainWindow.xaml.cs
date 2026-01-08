using Microsoft.UI.Xaml;

namespace DexcomWindows;

/// <summary>
/// Hidden main window - required for WinUI 3 to keep the app running.
/// The actual UI is shown in the system tray popup.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Set minimal title
        Title = "Dexcom Windows";

        // Hide this window immediately after activation
        // We need to defer this slightly to let the window initialize properly
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, HideWindow);
    }

    private void HideWindow()
    {
        try
        {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            // Make the window as small as possible
        appWindow.Resize(new Windows.Graphics.SizeInt32(1, 1));

            // Move it off-screen
            appWindow.Move(new Windows.Graphics.PointInt32(-10000, -10000));

            // Hide from taskbar and Alt+Tab
            var presenter = appWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
            if (presenter != null)
            {
                presenter.IsAlwaysOnTop = false;
                presenter.IsMinimizable = false;
                presenter.IsMaximizable = false;
                presenter.IsResizable = false;
                presenter.SetBorderAndTitleBar(false, false);
            }

            // Hide the window
        appWindow.Hide();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to hide main window: {ex.Message}");
        }
    }
}
