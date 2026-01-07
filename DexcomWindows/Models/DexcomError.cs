namespace DexcomWindows.Models;

/// <summary>
/// Custom errors for Dexcom API operations - Complete port of DexcomError.swift
/// </summary>
public abstract class DexcomError : Exception
{
    public abstract string RecoverySuggestion { get; }
    public virtual string? DetailedHelp => null;
    public virtual bool ShouldReauthenticate => false;

    protected DexcomError(string message) : base(message) { }
}

public class InvalidCredentialsError : DexcomError
{
    public InvalidCredentialsError()
        : base("Invalid username or password") { }

    public override string RecoverySuggestion =>
        "Please check your Dexcom Share username and password.";

    public override string? DetailedHelp => """
        Make sure you're using:
        • The same email/username you use in the Dexcom G7 app
        • The correct password for your Dexcom account
        • If you use a phone number, include the country code (e.g., +1 for US)
        """;

    public override bool ShouldReauthenticate => true;
}

public class SessionExpiredError : DexcomError
{
    public SessionExpiredError()
        : base("Session expired. Please log in again.") { }

    public override string RecoverySuggestion =>
        "Your session has expired. The app will attempt to log in again.";

    public override bool ShouldReauthenticate => true;
}

public class ShareNotConfiguredError : DexcomError
{
    public ShareNotConfiguredError()
        : base("Dexcom Share is not fully configured") { }

    public override string RecoverySuggestion =>
        "Please ensure Dexcom Share is enabled and you have at least one follower who has ACCEPTED the invitation.";

    public override string? DetailedHelp => """
        To fix this:
        1. Open the Dexcom G7 app on your phone
        2. Go to Settings → Share
        3. Make sure Share is turned ON
        4. Add at least one follower (can be yourself with a different email)
        5. The follower MUST accept the invitation and set up the Dexcom Follow app
        6. Wait a few minutes, then try signing in again
        """;
}

public class NetworkError : DexcomError
{
    public Exception UnderlyingError { get; }

    public NetworkError(Exception underlying)
        : base($"Network error: {underlying.Message}")
    {
        UnderlyingError = underlying;
    }

    public override string RecoverySuggestion =>
        "Please check your internet connection and try again.";
}

public class InvalidResponseError : DexcomError
{
    public InvalidResponseError()
        : base("Invalid response from server") { }

    public override string RecoverySuggestion =>
        "There may be an issue with the Dexcom servers. Please try again later.";
}

public class NoDataError : DexcomError
{
    public NoDataError()
        : base("No glucose data available") { }

    public override string RecoverySuggestion =>
        "Make sure your Dexcom G7 is connected and sharing data.";
}

public class ParseError : DexcomError
{
    public ParseError(string details)
        : base($"Failed to parse data: {details}") { }

    public override string RecoverySuggestion =>
        "There may be an issue with the Dexcom servers. Please try again later.";
}

public class ServerError : DexcomError
{
    public int StatusCode { get; }

    public ServerError(int statusCode, string? message = null)
        : base(message != null ? $"Server error ({statusCode}): {message}" : $"Server error: {statusCode}")
    {
        StatusCode = statusCode;
    }

    public override string RecoverySuggestion =>
        "The Dexcom servers may be experiencing issues. Please try again later.";

    public override bool ShouldReauthenticate => StatusCode is 401 or 500;
}

public class UnknownError : DexcomError
{
    public UnknownError(string message) : base(message) { }

    public override string RecoverySuggestion =>
        "Please try again. If the problem persists, restart the app.";
}
