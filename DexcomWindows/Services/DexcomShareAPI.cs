using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DexcomWindows.Models;
using System.Diagnostics;

namespace DexcomWindows.Services;

/// <summary>
/// Dexcom Share API client
/// </summary>
public class DexcomShareAPI
{
    // Dexcom Share application ID (same as used by other apps like Sugarmate, xDrip, etc.)
    private const string ApplicationId = "d89443d2-327c-4a6f-89e5-496bbb0317db";

    // Null session ID returned when Share is not properly configured
    private const string NullSessionId = "00000000-0000-0000-0000-000000000000";

    private readonly HttpClient _httpClient;
    private Server _server;

    public enum Server
    {
        US,
        International
    }

    public enum AuthMethod
    {
        Username,
        AccountId
    }

    public DexcomShareAPI(Server server = Server.US)
    {
        _server = server;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        
        // Set default headers
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Dexcom Share/3.0.2.11");
    }

    [Conditional("DEBUG")]
    private static void DebugLog(string message)
    {
        Debug.WriteLine(message);
    }

    public void SetServer(Server server) => _server = server;

    public static string GetServerDisplayName(Server server) => server switch
    {
        Server.US => "United States",
        Server.International => "Outside US (International)",
        _ => "Unknown"
    };

    private string BaseUrl => _server switch
    {
        Server.US => "https://share2.dexcom.com/ShareWebServices/Services",
        Server.International => "https://shareous1.dexcom.com/ShareWebServices/Services",
        _ => throw new ArgumentException("Invalid server")
    };

    /// <summary>
    /// Login with username/email
    /// </summary>
    public async Task<string> LoginAsync(string username, string password)
    {
        var url = $"{BaseUrl}/General/LoginPublisherAccountByName";

        var body = new Dictionary<string, string>
        {
            ["accountName"] = username.Trim(),
            ["password"] = password,
            ["applicationId"] = ApplicationId
        };

        return await PerformLoginAsync(url, body);
    }

    /// <summary>
    /// Login with Account ID (UUID) - alternative method
    /// </summary>
    public async Task<string> LoginWithAccountIdAsync(string accountId, string password)
    {
        var url = $"{BaseUrl}/General/LoginPublisherAccountById";
        var formattedAccountId = FormatAccountId(accountId);

        var body = new Dictionary<string, string>
        {
            ["accountId"] = formattedAccountId,
            ["password"] = password,
            ["applicationId"] = ApplicationId
        };

        return await PerformLoginAsync(url, body);
    }

    /// <summary>
    /// Fetch glucose readings
    /// </summary>
    public async Task<List<GlucoseReading>> GetGlucoseReadingsAsync(
        string sessionId,
        int minutes = 1440,
        int maxCount = 288)
    {
        var url = $"{BaseUrl}/Publisher/ReadPublisherLatestGlucoseValues" +
                  $"?sessionId={sessionId}&minutes={minutes}&maxCount={maxCount}";

        try
        {
            var content = new StringContent("", Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            // Do NOT log raw response bodies (can contain sensitive data). Log only status + size in DEBUG.
            DebugLog($"GetGlucoseReadings response: {(int)response.StatusCode} ({responseBody.Length} bytes)");

            if (response.IsSuccessStatusCode)
            {
                return ParseGlucoseReadings(responseBody);
            }

            if ((int)response.StatusCode is 401 or 500)
            {
                if (responseBody.Contains("SessionIdNotFound") || responseBody.Contains("SessionNotValid"))
                {
                    throw new SessionExpiredError();
                }
            }

            // Try to parse a clean error message instead of returning the full raw payload.
            var errorMessage = responseBody;
            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                    doc.RootElement.TryGetProperty("Message", out var msg))
                {
                    errorMessage = msg.GetString() ?? responseBody;
                }
            }
            catch (JsonException) { }

            throw new ServerError((int)response.StatusCode, errorMessage);
        }
        catch (DexcomError)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new NetworkError(ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new NetworkError(new Exception("Request timed out", ex));
        }
        catch (Exception ex)
        {
            throw new NetworkError(ex);
        }
    }

    /// <summary>
    /// Check if string looks like valid Account ID (UUID)
    /// </summary>
    public static bool IsValidAccountId(string str)
    {
        var cleaned = str.Trim().Replace("-", "");
        if (cleaned.Length != 32) return false;
        return cleaned.All(c => Uri.IsHexDigit(c));
    }

    private async Task<string> PerformLoginAsync(string url, Dictionary<string, string> body)
    {
        try
        {
            var jsonBody = JsonSerializer.Serialize(body);
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            
            // Avoid logging credentials or raw bodies.
            DebugLog($"Login request to: {url}");

            var response = await _httpClient.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            // Do NOT log raw response bodies (can include session IDs). Log only status + size in DEBUG.
            DebugLog($"Login response: {(int)response.StatusCode} ({responseBody.Length} bytes)");

            if (response.IsSuccessStatusCode)
            {
                var sessionId = responseBody.Trim().Trim('"');

                if (string.IsNullOrEmpty(sessionId) || sessionId == "null")
                {
                    throw new InvalidCredentialsError();
                }

                if (sessionId == NullSessionId)
                {
                    throw new ShareNotConfiguredError();
                }

                // Check if the response contains an error message instead of session ID
                if (sessionId.Contains("Code"))
                {
                    // Parse the error
                    try
                    {
                        using var doc = JsonDocument.Parse(responseBody);
                        if (doc.RootElement.TryGetProperty("Message", out var msg))
                {
                            throw new ServerError(0, msg.GetString() ?? "Unknown error");
                        }
                    }
                    catch (JsonException) { }
                }

                return sessionId;
            }

            // Handle error responses
            var errorMessage = responseBody;
            
            // Try to parse as JSON error
            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                if (doc.RootElement.TryGetProperty("Message", out var msg))
                {
                    errorMessage = msg.GetString() ?? responseBody;
                }
            }
            catch (JsonException) { }

            // Check for specific error codes
            if (responseBody.Contains("AccountPasswordInvalid"))
            {
                throw new InvalidCredentialsError();
            }

            if (responseBody.Contains("AccountNotFound"))
            {
                throw new ServerError((int)response.StatusCode, 
                    "Account not found. Make sure you're using the correct region (US vs International).");
            }
            
            if (responseBody.Contains("SSO_AuthenticateMaxAttemptsExceeed"))
            {
                throw new ServerError((int)response.StatusCode,
                    "Too many login attempts. Please wait a few minutes and try again.");
            }

            // Return the actual error message
            throw new ServerError((int)response.StatusCode, errorMessage);
        }
        catch (DexcomError)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new NetworkError(ex);
        }
        catch (TaskCanceledException ex)
    {
            throw new NetworkError(new Exception("Request timed out", ex));
        }
        catch (Exception ex)
        {
            throw new UnknownError($"Login failed: {ex.Message}");
        }
    }

    private string FormatAccountId(string accountId)
    {
        var cleaned = accountId.Trim().ToLowerInvariant().Replace("-", "");

        if (accountId.Length == 36 && accountId.Contains('-'))
        {
            return accountId.ToLowerInvariant();
        }

        if (cleaned.Length == 32)
        {
            // Format as UUID: 8-4-4-4-12
            return $"{cleaned[..8]}-{cleaned[8..12]}-{cleaned[12..16]}-{cleaned[16..20]}-{cleaned[20..]}";
        }

        return cleaned;
    }

    private List<GlucoseReading> ParseGlucoseReadings(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<GlucoseReading>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Array)
            {
                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("Message", out var msg))
                {
                    throw new ServerError(0, msg.GetString() ?? "Unknown error");
                }
                throw new ParseError($"Expected array, got {root.ValueKind}");
            }

            var readings = new List<GlucoseReading>();

            foreach (var element in root.EnumerateArray())
            {
                var reading = GlucoseReading.FromDictionary(element);
                if (reading != null)
                {
                    readings.Add(reading);
                }
            }

            return readings.OrderByDescending(r => r.Timestamp).ToList();
        }
        catch (DexcomError)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ParseError(ex.Message);
        }
    }
}
