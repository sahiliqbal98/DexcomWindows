using System.Runtime.InteropServices;
using System.Text;

namespace DexcomWindows.Services;

/// <summary>
/// Windows Credential Manager wrapper - Port of KeychainManager.swift
/// </summary>
public class CredentialManager
{
    private const string CredentialTargetUsername = "SteadySugar_Username";
    private const string CredentialTargetAccountId = "SteadySugar_AccountId";
    private const string CredentialTargetSession = "SteadySugar_Session";

    private readonly SettingsService _settings;

    public CredentialManager(SettingsService settings)
    {
        _settings = settings;
    }

    // MARK: - Credentials (Username-based)

    public void SaveCredentials(string username, string password)
    {
        WriteCredential(CredentialTargetUsername, username, password);
        DeleteCredential(CredentialTargetAccountId);
        _settings.AuthMethod = DexcomShareAPI.AuthMethod.Username;
        _settings.SaveSettings();
    }

    public void SaveCredentials(string accountId, string password, bool isAccountId)
    {
        if (isAccountId)
        {
            WriteCredential(CredentialTargetAccountId, accountId, password);
            DeleteCredential(CredentialTargetUsername);
            _settings.AuthMethod = DexcomShareAPI.AuthMethod.AccountId;
        }
        else
        {
            SaveCredentials(accountId, password);
        }
        _settings.SaveSettings();
    }

    public (string Username, string Password)? LoadCredentials()
    {
        var cred = ReadCredential(CredentialTargetUsername);
        if (cred == null) return null;
        return (cred.Value.Username, cred.Value.Password);
    }

    public (string AccountId, string Password)? LoadAccountIdCredentials()
    {
        var cred = ReadCredential(CredentialTargetAccountId);
        if (cred == null) return null;
        return (cred.Value.Username, cred.Value.Password);
    }

    public DexcomShareAPI.AuthMethod LoadAuthMethod() => _settings.AuthMethod;

    public bool HasCredentials
    {
        get
        {
            var method = LoadAuthMethod();
            return method == DexcomShareAPI.AuthMethod.Username
                ? LoadCredentials() != null
                : LoadAccountIdCredentials() != null;
        }
    }

    // MARK: - Session

    public void SaveSession(string sessionId)
    {
        WriteCredential(CredentialTargetSession, "session", sessionId);
        _settings.SessionTimestamp = DateTime.Now;
        _settings.SaveSettings();
    }

    public string? LoadSession()
    {
        var cred = ReadCredential(CredentialTargetSession);
        if (cred == null) return null;

        // Check if session is expired (23 hours)
        var timestamp = _settings.SessionTimestamp;
        if (timestamp != null && (DateTime.Now - timestamp.Value).TotalHours > 23)
        {
            ClearSession();
            return null;
        }

        return cred.Value.Password;
    }

    public void ClearSession()
    {
        DeleteCredential(CredentialTargetSession);
        _settings.SessionTimestamp = null;
        _settings.SaveSettings();
    }

    // MARK: - Server

    public void SaveServer(DexcomShareAPI.Server server)
    {
        _settings.Server = server;
        _settings.SaveSettings();
    }

    public DexcomShareAPI.Server LoadServer() => _settings.Server;

    // MARK: - Clear All

    public void ClearAll()
    {
        DeleteCredential(CredentialTargetUsername);
        DeleteCredential(CredentialTargetAccountId);
        DeleteCredential(CredentialTargetSession);
        _settings.ClearCredentialSettings();
    }

    // MARK: - Windows Credential Manager P/Invoke

    private void WriteCredential(string target, string username, string password)
    {
        var passwordBytes = Encoding.Unicode.GetBytes(password);

        var credential = new CREDENTIAL
        {
            Type = CRED_TYPE.GENERIC,
            TargetName = target,
            UserName = username,
            CredentialBlobSize = (uint)passwordBytes.Length,
            CredentialBlob = Marshal.AllocHGlobal(passwordBytes.Length),
            Persist = CRED_PERSIST.LOCAL_MACHINE
        };

        try
        {
            Marshal.Copy(passwordBytes, 0, credential.CredentialBlob, passwordBytes.Length);
            if (!CredWrite(ref credential, 0))
            {
                var error = Marshal.GetLastWin32Error();
                System.Diagnostics.Debug.WriteLine($"Failed to write credential: {error}");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(credential.CredentialBlob);
        }
    }

    private (string Username, string Password)? ReadCredential(string target)
    {
        if (!CredRead(target, CRED_TYPE.GENERIC, 0, out var credentialPtr))
        {
            return null;
        }

        try
        {
            var credential = Marshal.PtrToStructure<CREDENTIAL>(credentialPtr);
            var passwordBytes = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, passwordBytes, 0, (int)credential.CredentialBlobSize);
            var password = Encoding.Unicode.GetString(passwordBytes);

            return (credential.UserName ?? "", password);
        }
        finally
        {
            CredFree(credentialPtr);
        }
    }

    private void DeleteCredential(string target)
    {
        CredDelete(target, CRED_TYPE.GENERIC, 0);
    }

    // P/Invoke declarations
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredWrite(ref CREDENTIAL credential, uint flags);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredRead(string target, CRED_TYPE type, uint flags, out IntPtr credential);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool CredDelete(string target, CRED_TYPE type, uint flags);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree(IntPtr credential);

    private enum CRED_TYPE : uint
    {
        GENERIC = 1,
    }

    private enum CRED_PERSIST : uint
    {
        SESSION = 1,
        LOCAL_MACHINE = 2,
        ENTERPRISE = 3
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public uint Flags;
        public CRED_TYPE Type;
        public string TargetName;
        public string Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public CRED_PERSIST Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }
}
