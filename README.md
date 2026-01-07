# Dexcom Windows - System Tray Glucose Monitor

A Windows system tray application for monitoring Dexcom glucose readings in real-time. This is a pixel-perfect port of the macOS menu bar app.

## Features

- **System Tray Integration**: Glucose value and trend arrow displayed in the Windows system tray
- **Real-time Updates**: Smart refresh timing synced to Dexcom's 5-minute update cycle
- **Interactive Chart**: LiveCharts2-powered glucose chart with 3h/6h/12h/24h time ranges
- **Dual Authentication**: Login via username/email OR Account ID
- **Toast Notifications**: Windows native notifications for glucose alerts
- **6 Color Themes**: System, Light, Dark, Charcoal, Rainbow, Dexcom Green
- **Statistics**: Average, standard deviation, and time-in-range calculations
- **Secure Storage**: Credentials stored in Windows Credential Manager

## Requirements

- Windows 10 version 1809 (build 17763) or later
- Windows 11 supported
- .NET 8.0 Runtime (included in self-contained build)

## Building

### Prerequisites

1. **Visual Studio 2022** (version 17.8 or later)
   - Workload: ".NET Desktop Development"
   - Workload: "Windows application development"

2. **Windows App SDK** (installed via NuGet)

### Build Steps

1. Open `DexcomWindows.sln` in Visual Studio 2022

2. Restore NuGet packages (automatic on first build)

3. Build the solution:
   - Select configuration: `Release | x64`
   - Build > Build Solution (Ctrl+Shift+B)

4. Run the application:
   - Debug > Start Without Debugging (Ctrl+F5)

### Publish for Distribution

```bash
# Single-file self-contained executable (no .NET runtime required)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# Output will be in: bin/Release/net8.0-windows10.0.19041.0/win-x64/publish/
```

## Project Structure

```
DexcomWindows/
├── DexcomWindows.sln                    # Solution file
├── DexcomWindows/
│   ├── DexcomWindows.csproj             # Project configuration
│   ├── App.xaml / App.xaml.cs           # Application entry point
│   ├── MainWindow.xaml / .cs            # Hidden host window
│   │
│   ├── Models/                          # Data models
│   │   ├── GlucoseReading.cs            # Glucose reading with parsing
│   │   ├── TrendArrow.cs                # Trend arrow enumeration
│   │   ├── DexcomError.cs               # Custom error types
│   │   └── AlertSettings.cs             # Alert configuration
│   │
│   ├── Services/                        # Business logic
│   │   ├── DexcomShareAPI.cs            # Dexcom API client
│   │   ├── CredentialManager.cs         # Windows Credential Store
│   │   ├── NotificationService.cs       # Windows toast notifications
│   │   └── SettingsService.cs           # App settings persistence
│   │
│   ├── ViewModels/                      # MVVM view models
│   │   └── GlucoseViewModel.cs          # Main state management
│   │
│   ├── Views/                           # UI components
│   │   ├── TrayPopupView.xaml / .cs     # Main popup window
│   │   ├── LoginView.xaml / .cs         # Login form
│   │   └── SettingsWindow.xaml / .cs    # Settings window
│   │
│   ├── Themes/
│   │   └── ColorThemes.cs               # 6 color theme definitions
│   │
│   ├── Converters/
│   │   └── GlucoseColorConverter.cs     # XAML value converters
│   │
│   └── Assets/
│       └── Icons/
│           └── app-icon.ico             # System tray icon
```

## Configuration

### Dexcom Share Setup

1. Open the Dexcom G7 app on your phone
2. Go to Settings → Share
3. Enable Share
4. Add at least one follower (can be yourself with a different email)
5. **The follower MUST accept the invitation** and set up Dexcom Follow

### App Settings

Settings are stored in: `%LocalAppData%\DexcomWindows\settings.json`

Credentials are stored securely in Windows Credential Manager.

## Technology Stack

| Component | Technology |
|-----------|------------|
| Language | C# 12 / .NET 8 |
| UI Framework | WinUI 3 (Windows App SDK 1.5) |
| System Tray | H.NotifyIcon.WinUI |
| Charts | LiveCharts2 |
| MVVM | CommunityToolkit.Mvvm |
| Credentials | Windows Credential Manager |
| Notifications | Windows App Notifications |

## Known Issues

1. **Icon**: You need to create your own `app-icon.ico` file in `Assets/Icons/`
2. **First run**: The app may take a few seconds to appear in the system tray on first launch

## License

This is a personal project for glucose monitoring. Not affiliated with or endorsed by Dexcom, Inc.

## Disclaimer

This app is for informational purposes only and should not be used to make treatment decisions. Always consult your healthcare provider for medical advice.
