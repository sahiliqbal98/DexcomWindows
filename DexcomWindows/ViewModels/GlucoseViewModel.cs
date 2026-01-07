using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DexcomWindows.Models;
using DexcomWindows.Services;
using Microsoft.UI.Dispatching;

namespace DexcomWindows.ViewModels;

/// <summary>
/// Main view model for glucose data management - Complete port of GlucoseViewModel.swift
/// </summary>
public partial class GlucoseViewModel : ObservableObject
{
    private readonly DexcomShareAPI _api;
    private readonly CredentialManager _credentials;
    private readonly NotificationService _notifications;
    private readonly SettingsService _settings;
    private DispatcherQueue? _dispatcher;

    private DispatcherQueueTimer? _refreshTimer;
    private DispatcherQueueTimer? _countdownTimer;
    private DispatcherQueueTimer? _staleDataTimer;
    private string? _sessionId;
    private bool _isInitialized = false;

    private AlertSettings _alertSettings = AlertSettings.Default;

    // Dexcom updates every 5 minutes (300 seconds)
    private const int DexcomInterval = 300;
    // Small buffer after expected update
    private const int RefreshBuffer = 10;
    // Max retry attempts
    private const int MaxRetries = 3;

    private int _currentRetryCount;
    private int _consecutiveFailures;

    [ObservableProperty]
    private GlucoseReading? _currentReading;

    [ObservableProperty]
    private List<GlucoseReading> _readings = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private DexcomError? _error;

    [ObservableProperty]
    private bool _isAuthenticated;

    [ObservableProperty]
    private DateTime? _lastRefreshTime;

    [ObservableProperty]
    private DateTime? _nextRefreshTime;

    [ObservableProperty]
    private int _secondsUntilRefresh;

    [ObservableProperty]
    private int _secondsSinceLastReading;

    [ObservableProperty]
    private TimeRange _selectedTimeRange = TimeRange.ThreeHours;

    public GlucoseViewModel(
        DexcomShareAPI api,
        CredentialManager credentials,
        NotificationService notifications,
        SettingsService settings)
    {
        _api = api;
        _credentials = credentials;
        _notifications = notifications;
        _settings = settings;
        _selectedTimeRange = settings.DefaultTimeRange;
        
        // Don't start anything in constructor - wait for Initialize
    }

    /// <summary>
    /// Initialize the view model - must be called from UI thread after construction
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        
        try
        {
            _dispatcher = DispatcherQueue.GetForCurrentThread();
            _isInitialized = true;
            
            // Check for existing session on startup
            await CheckExistingSessionAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ViewModel InitializeAsync error: {ex.Message}");
        }
    }

    public void UpdateAlertSettings(AlertSettings settings)
    {
        _alertSettings = settings;
    }

    // Statistics
    public GlucoseStatistics? Statistics
    {
        get
        {
            try
            {
                return Readings.Count > 0
                    ? new GlucoseStatistics(GetReadingsForRange(SelectedTimeRange), _settings.TargetLow, _settings.TargetHigh)
                    : null;
            }
            catch
            {
                return null;
            }
        }
    }

    // Data staleness check
    public bool IsDataStale
    {
        get
        {
            if (CurrentReading == null) return true;
            var timeSinceReading = (DateTime.Now - CurrentReading.Timestamp).TotalSeconds;
            return timeSinceReading > (DexcomInterval * 2 + 60);
        }
    }

    // Progress for update bar (0.0 to 1.0)
    public double RefreshProgress
    {
        get
        {
            if (CurrentReading == null) return 0;
            var elapsed = (DateTime.Now - CurrentReading.Timestamp).TotalSeconds;
            return Math.Min(1.0, elapsed / DexcomInterval);
        }
    }

    // Get readings for time range
    public List<GlucoseReading> GetReadingsForRange(TimeRange range)
    {
        try
        {
            var cutoff = DateTime.Now.AddSeconds(-range.Seconds());
            return Readings.Where(r => r.Timestamp >= cutoff).ToList();
        }
        catch
        {
            return new List<GlucoseReading>();
        }
    }

    // Tray display text
    public string TrayText => CurrentReading?.TrayDisplayString ?? "--";

    // Tray tooltip
    public string TrayTooltip
    {
        get
        {
            if (CurrentReading == null) return "Dexcom - No data";
            return $"Dexcom: {CurrentReading.Value} {CurrentReading.Trend.Symbol()} - {CurrentReading.TimeAgoString}";
        }
    }

    public async Task LoginAsync(string username, string password, DexcomShareAPI.Server server)
    {
        IsLoading = true;
        Error = null;

        try
        {
            _api.SetServer(server);
            _credentials.SaveServer(server);

            var sessionId = await _api.LoginAsync(username, password);

            _credentials.SaveCredentials(username, password);
            _credentials.SaveSession(sessionId);

            _sessionId = sessionId;
            IsAuthenticated = true;
            _currentRetryCount = 0;
            _consecutiveFailures = 0;

            await RefreshDataAsync();
            StartSmartRefresh();
        }
        catch (DexcomError ex)
        {
            Error = ex;
            IsAuthenticated = false;
        }
        catch (Exception ex)
        {
            Error = new UnknownError(ex.Message);
            IsAuthenticated = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task LoginWithAccountIdAsync(string accountId, string password, DexcomShareAPI.Server server)
    {
        IsLoading = true;
        Error = null;

        try
        {
            _api.SetServer(server);
            _credentials.SaveServer(server);

            var sessionId = await _api.LoginWithAccountIdAsync(accountId, password);

            _credentials.SaveCredentials(accountId, password, isAccountId: true);
            _credentials.SaveSession(sessionId);

            _sessionId = sessionId;
            IsAuthenticated = true;
            _currentRetryCount = 0;
            _consecutiveFailures = 0;

            await RefreshDataAsync();
            StartSmartRefresh();
        }
        catch (DexcomError ex)
        {
            Error = ex;
            IsAuthenticated = false;
        }
        catch (Exception ex)
        {
            Error = new UnknownError(ex.Message);
            IsAuthenticated = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void Logout()
    {
        StopAllTimers();
        _credentials.ClearAll();
        _ = _notifications.ClearAllNotificationsAsync();

        _sessionId = null;
        IsAuthenticated = false;
        CurrentReading = null;
        Readings = new List<GlucoseReading>();
        LastRefreshTime = null;
        NextRefreshTime = null;
        SecondsUntilRefresh = 0;
        Error = null;
        _currentRetryCount = 0;
        _consecutiveFailures = 0;

        OnPropertyChanged(nameof(TrayText));
        OnPropertyChanged(nameof(TrayTooltip));
        OnPropertyChanged(nameof(Statistics));
    }

    [RelayCommand]
    public async Task RefreshDataAsync()
    {
        if (_sessionId == null)
        {
            await AttemptReauthenticationAsync();
            return;
        }

        IsLoading = true;

        try
        {
            var previousReading = CurrentReading;
            var newReadings = await _api.GetGlucoseReadingsAsync(_sessionId);

            Readings = newReadings;
            CurrentReading = newReadings.FirstOrDefault();
            LastRefreshTime = DateTime.Now;
            Error = null;
            _currentRetryCount = 0;
            _consecutiveFailures = 0;

            if (newReadings.Count == 0)
            {
                Error = new NoDataError();
            }

            // Check for alerts on new reading
            if (CurrentReading != null && previousReading?.Timestamp != CurrentReading.Timestamp)
            {
                _notifications.CheckAndAlert(CurrentReading, _alertSettings);
            }

            OnPropertyChanged(nameof(TrayText));
            OnPropertyChanged(nameof(TrayTooltip));
            OnPropertyChanged(nameof(Statistics));
            OnPropertyChanged(nameof(RefreshProgress));

            ScheduleNextRefresh();
        }
        catch (DexcomError ex)
        {
            HandleError(ex);
            _consecutiveFailures++;
            ScheduleRetryRefresh();
        }
        catch (Exception ex)
        {
            Error = new UnknownError(ex.Message);
            _consecutiveFailures++;
            ScheduleRetryRefresh();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task ForceRefreshAsync()
    {
        _consecutiveFailures = 0;
        await RefreshDataAsync();
    }

    partial void OnSelectedTimeRangeChanged(TimeRange value)
    {
        _settings.DefaultTimeRange = value;
        _settings.SaveSettings();
        OnPropertyChanged(nameof(Statistics));
    }

    // Smart refresh logic - matches Mac app exactly

    private void StartSmartRefresh()
    {
        if (_dispatcher == null) return;
        
        StopAllTimers();
        ScheduleNextRefresh();
        StartCountdownTimer();
        StartStaleDataMonitor();
    }

    private void ScheduleNextRefresh()
    {
        if (_dispatcher == null) return;
        
        if (CurrentReading == null)
        {
            ScheduleRefresh(TimeSpan.FromSeconds(30));
            return;
        }

        var lastReadingTime = CurrentReading.Timestamp;
        var now = DateTime.Now;
        var timeSinceLastReading = (now - lastReadingTime).TotalSeconds;

        if (timeSinceLastReading < 0)
        {
            ScheduleRefresh(TimeSpan.FromSeconds(DexcomInterval));
            return;
        }

        var intervalsPassed = Math.Floor(timeSinceLastReading / DexcomInterval);
        var nextExpectedReading = lastReadingTime.AddSeconds((intervalsPassed + 1) * DexcomInterval);
        var refreshTime = nextExpectedReading.AddSeconds(RefreshBuffer);

        var delay = (refreshTime - now).TotalSeconds;

        if (delay < 0) delay = 5;
        delay = Math.Min(delay, DexcomInterval);

        ScheduleRefresh(TimeSpan.FromSeconds(delay));
    }

    private void ScheduleRefresh(TimeSpan delay)
    {
        if (_dispatcher == null) return;
        
        try
        {
            _refreshTimer?.Stop();

            NextRefreshTime = DateTime.Now + delay;
            SecondsUntilRefresh = (int)delay.TotalSeconds;

            _refreshTimer = _dispatcher.CreateTimer();
            _refreshTimer.Interval = delay;
            _refreshTimer.IsRepeating = false;
            _refreshTimer.Tick += async (_, _) =>
            {
                _refreshTimer?.Stop();
                await RefreshDataAsync();
            };
            _refreshTimer.Start();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ScheduleRefresh error: {ex.Message}");
        }
    }

    private void ScheduleRetryRefresh()
    {
        var backoffMultiplier = Math.Min(Math.Pow(2, _consecutiveFailures), 8);
        var delay = 30 * backoffMultiplier;
        delay = Math.Min(delay, DexcomInterval);

        ScheduleRefresh(TimeSpan.FromSeconds(delay));
    }

    private void StartCountdownTimer()
    {
        if (_dispatcher == null) return;
        
        try
        {
            _countdownTimer?.Stop();

            _countdownTimer = _dispatcher.CreateTimer();
            _countdownTimer.Interval = TimeSpan.FromSeconds(1);
            _countdownTimer.IsRepeating = true;
            _countdownTimer.Tick += (_, _) =>
            {
                try
                {
                    if (NextRefreshTime != null)
                    {
                        var remaining = (NextRefreshTime.Value - DateTime.Now).TotalSeconds;
                        SecondsUntilRefresh = Math.Max(0, (int)remaining);
                    }

                    if (CurrentReading != null)
                    {
                        var elapsed = (DateTime.Now - CurrentReading.Timestamp).TotalSeconds;
                        SecondsSinceLastReading = Math.Max(0, (int)elapsed);
                    }
                    else
                    {
                        SecondsSinceLastReading = 0;
                    }

                    OnPropertyChanged(nameof(IsDataStale));
                    OnPropertyChanged(nameof(RefreshProgress));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Countdown tick error: {ex.Message}");
                }
            };
            _countdownTimer.Start();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"StartCountdownTimer error: {ex.Message}");
        }
    }

    private void StartStaleDataMonitor()
    {
        if (_dispatcher == null) return;
        
        try
        {
            _staleDataTimer?.Stop();

            _staleDataTimer = _dispatcher.CreateTimer();
            _staleDataTimer.Interval = TimeSpan.FromMinutes(1);
            _staleDataTimer.IsRepeating = true;
            _staleDataTimer.Tick += (_, _) =>
            {
                try
                {
                    if (_alertSettings.StaleDataAlertEnabled && CurrentReading != null)
                    {
                        var minutesSinceReading = (DateTime.Now - CurrentReading.Timestamp).TotalMinutes;
                        if (minutesSinceReading >= _alertSettings.StaleDataThreshold)
                        {
                            _notifications.SendStaleDataAlert(CurrentReading.Timestamp);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Stale data check error: {ex.Message}");
                }
            };
            _staleDataTimer.Start();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"StartStaleDataMonitor error: {ex.Message}");
        }
    }

    private void StopAllTimers()
    {
        try
        {
            _refreshTimer?.Stop();
            _refreshTimer = null;
            _countdownTimer?.Stop();
            _countdownTimer = null;
            _staleDataTimer?.Stop();
            _staleDataTimer = null;
            NextRefreshTime = null;
            SecondsUntilRefresh = 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"StopAllTimers error: {ex.Message}");
        }
    }

    private async Task CheckExistingSessionAsync()
    {
        try
        {
            var savedSession = _credentials.LoadSession();
            if (savedSession != null)
            {
                _sessionId = savedSession;
                IsAuthenticated = true;
                await RefreshDataAsync();
                StartSmartRefresh();
            }
            else if (_credentials.HasCredentials)
            {
                await AttemptReauthenticationAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CheckExistingSession error: {ex.Message}");
        }
    }

    private async Task AttemptReauthenticationAsync()
    {
        try
        {
            var authMethod = _credentials.LoadAuthMethod();
            var server = _credentials.LoadServer();

            if (authMethod == DexcomShareAPI.AuthMethod.Username)
            {
                var creds = _credentials.LoadCredentials();
                if (creds != null)
                {
                    await LoginAsync(creds.Value.Username, creds.Value.Password, server);
                }
            }
            else
            {
                var creds = _credentials.LoadAccountIdCredentials();
                if (creds != null)
                {
                    await LoginWithAccountIdAsync(creds.Value.AccountId, creds.Value.Password, server);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AttemptReauthentication error: {ex.Message}");
        }
    }

    private void HandleError(DexcomError error)
    {
        Error = error;

        if (error.ShouldReauthenticate)
        {
            _credentials.ClearSession();
            _sessionId = null;

            _currentRetryCount++;
            if (_currentRetryCount < MaxRetries && _dispatcher != null)
            {
                _ = Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, _currentRetryCount)))
                    .ContinueWith(_ => _dispatcher.TryEnqueue(async () => await AttemptReauthenticationAsync()));
            }
            else
            {
                IsAuthenticated = false;
            }
        }
    }
}

/// <summary>
/// Statistics for glucose readings
/// </summary>
public class GlucoseStatistics
{
    public int Average { get; }
    public int Min { get; }
    public int Max { get; }
    public double TimeInRange { get; }
    public double StandardDeviation { get; }
    public int ReadingCount { get; }

    public GlucoseStatistics(IReadOnlyList<GlucoseReading> readings, int targetLow = 70, int targetHigh = 180)
    {
        ReadingCount = readings.Count;
        var values = readings.Select(r => r.Value).ToList();

        Average = values.Count > 0 ? (int)values.Average() : 0;
        Min = values.Count > 0 ? values.Min() : 0;
        Max = values.Count > 0 ? values.Max() : 0;

        var inRangeCount = values.Count(v => v >= targetLow && v <= targetHigh);
        TimeInRange = values.Count > 0 ? (double)inRangeCount / values.Count * 100 : 0;

        if (values.Count > 1)
        {
            var mean = values.Average();
            var variance = values.Sum(v => Math.Pow(v - mean, 2)) / values.Count;
            StandardDeviation = Math.Sqrt(variance);
        }
        else
        {
            StandardDeviation = 0;
        }
    }

    public string FormattedTimeInRange => $"{TimeInRange:F0}%";
    public string FormattedStandardDeviation => $"{StandardDeviation:F1}";
}
