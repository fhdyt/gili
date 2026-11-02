using System.Runtime.InteropServices;
using System.Security.Principal;

namespace Gili.Platform.Power;

/// <summary>
/// When this Windows sign-in began (revision 20): the logon session's LogonTime from LSA, which needs no admin. A
/// shutdown with Fast Startup keeps the kernel's uptime but signs the user out, so a new sign-in is what "the laptop was
/// turned on" looks like; sleep, hibernate and lock keep it. Falls back to the boot time when LSA can't be read.
/// </summary>
public static class SignIn
{
    private const int TokenStatistics = 10;

    public static DateTimeOffset StartedAt()
    {
        try
        {
            if (LogonTime() is { } at) return at;
        }
        catch (Exception ex) when (ex is ExternalException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Fall through to the boot time.
        }
        return DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(Environment.TickCount64);
    }

    private static DateTimeOffset? LogonTime()
    {
        using var identity = WindowsIdentity.GetCurrent();
        // TOKEN_STATISTICS: TokenId then AuthenticationId, each a LUID of 8 bytes.
        var stats = Marshal.AllocHGlobal(64);
        try
        {
            if (!GetTokenInformation(identity.AccessToken, TokenStatistics, stats, 64, out _)) return null;
            var logonId = Marshal.ReadInt64(stats, 8);
            if (LsaGetLogonSessionData(ref logonId, out var data) != 0 || data == IntPtr.Zero) return null;
            try
            {
                var session = Marshal.PtrToStructure<SessionData>(data);
                return session.LogonTime > 0 ? DateTimeOffset.FromFileTime(session.LogonTime).ToUniversalTime() : null;
            }
            finally
            {
                _ = LsaFreeReturnBuffer(data);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(stats);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LsaString
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }

    /// <summary>SECURITY_LOGON_SESSION_DATA up to LogonTime; the fields after it are never read.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct SessionData
    {
        public uint Size;
        public long LogonId;
        public LsaString UserName;
        public LsaString LogonDomain;
        public LsaString AuthenticationPackage;
        public uint LogonType;
        public uint Session;
        public IntPtr Sid;
        public long LogonTime;
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(Microsoft.Win32.SafeHandles.SafeAccessTokenHandle token, int infoClass, IntPtr info, int length, out int returned);

    [DllImport("secur32.dll")]
    private static extern int LsaGetLogonSessionData(ref long logonId, out IntPtr data);

    [DllImport("secur32.dll")]
    private static extern int LsaFreeReturnBuffer(IntPtr buffer);
}
