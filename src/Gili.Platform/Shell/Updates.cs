using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using Gili.Core.About;

namespace Gili.Platform.Shell;

/// <summary>The latest release on fhdyt/gili (F-PUB-04, PRD revision 11); only the GitHub copy asks, without any user data (PRD 8.2).</summary>
public static class GithubReleases
{
    public const string LatestUrl = "https://api.github.com/repos/fhdyt/gili/releases/latest";

    private static readonly HttpClient Http = CreateClient();

    /// <summary>Null when GitHub answers with something that is not a release; throws HttpRequestException when offline.</summary>
    public static async Task<ReleaseInfo?> LatestAsync(CancellationToken cancel)
    {
        using var response = await Http.GetAsync(LatestUrl, cancel).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return ReleaseInfo.Parse(await response.Content.ReadAsStringAsync(cancel).ConfigureAwait(false));
    }

    /// <summary>Downloads the installer into %TEMP%, replacing an earlier download of the same version.</summary>
    public static async Task<string> DownloadAsync(string url, Version version, CancellationToken cancel)
    {
        var path = Path.Combine(Path.GetTempPath(), $"Gili-Setup-{AboutInfo.Format(version)}.exe");
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancel).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using (var file = File.Create(path))
            await response.Content.CopyToAsync(file, cancel).ConfigureAwait(false);
        return path;
    }

    /// <summary>
    /// Runs a verified installer without its wizard; it quits Gili through --quit and starts it again (design 5b).
    /// False when Windows refused to start it.
    /// </summary>
    public static bool RunInstaller(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART") { UseShellExecute = true })?.Dispose();
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        // GitHub's API refuses requests without a User-Agent.
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Gili/" + AboutInfo.Format(typeof(GithubReleases).Assembly.GetName().Version!));
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }
}

/// <summary>
/// Authenticode checks for Install update (F-PUB-04, PRD 11): a downloaded installer runs only when Windows trusts its
/// signature and it is signed by the same publisher as the running Gili.exe.
/// </summary>
public static class Signatures
{
    /// <summary>Windows trusts the file's embedded signature (chain to a trusted root, not revoked as far as the cache knows).</summary>
    public static bool IsTrusted(string path)
    {
        var file = new WINTRUST_FILE_INFO
        {
            cbStruct = (uint)Marshal.SizeOf<WINTRUST_FILE_INFO>(),
            pcwszFilePath = path,
        };
        var filePtr = Marshal.AllocHGlobal(Marshal.SizeOf<WINTRUST_FILE_INFO>());
        try
        {
            Marshal.StructureToPtr(file, filePtr, false);
            var data = new WINTRUST_DATA
            {
                cbStruct = (uint)Marshal.SizeOf<WINTRUST_DATA>(),
                dwUIChoice = WTD_UI_NONE,
                fdwRevocationChecks = WTD_REVOKE_NONE,
                dwUnionChoice = WTD_CHOICE_FILE,
                pFile = filePtr,
                dwStateAction = WTD_STATEACTION_VERIFY,
                dwProvFlags = WTD_CACHE_ONLY_URL_RETRIEVAL,
            };
            var action = GenericVerifyV2;
            var result = WinVerifyTrust(-1, ref action, ref data);
            data.dwStateAction = WTD_STATEACTION_CLOSE;
            WinVerifyTrust(-1, ref action, ref data);
            return result == 0;
        }
        finally
        {
            Marshal.FreeHGlobal(filePtr);
        }
    }

    /// <summary>The signing certificate's subject, or null for an unsigned file.</summary>
    public static string? SignerSubject(string path)
    {
        try
        {
#pragma warning disable SYSLIB0057 // The signer of an Authenticode file, which X509CertificateLoader does not read.
            using var certificate = X509Certificate.CreateFromSignedFile(path);
#pragma warning restore SYSLIB0057
            return certificate.Subject;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }

    /// <summary>Gili.exe that is running is signed; the friends' build is not and never checks (PRD 9.4).</summary>
    public static bool IsSigned(string path) => SignerSubject(path) is not null && IsTrusted(path);

    /// <summary>
    /// Both files trusted and signed by the same subject. The subject, not the thumbprint, since an OV certificate is
    /// renewed every year (design 5d).
    /// </summary>
    public static bool SameSigner(string candidate, string reference) =>
        SignerSubject(candidate) is { } subject && subject == SignerSubject(reference) && IsTrusted(candidate) && IsTrusted(reference);

    private const uint WTD_UI_NONE = 2, WTD_REVOKE_NONE = 0, WTD_CHOICE_FILE = 1;
    private const uint WTD_STATEACTION_VERIFY = 1, WTD_STATEACTION_CLOSE = 2, WTD_CACHE_ONLY_URL_RETRIEVAL = 0x1000;
    private static readonly Guid GenericVerifyV2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WINTRUST_FILE_INFO
    {
        public uint cbStruct;
        public string pcwszFilePath;
        public nint hFile;
        public nint pgKnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINTRUST_DATA
    {
        public uint cbStruct;
        public nint pPolicyCallbackData;
        public nint pSIPClientData;
        public uint dwUIChoice;
        public uint fdwRevocationChecks;
        public uint dwUnionChoice;
        public nint pFile;
        public uint dwStateAction;
        public nint hWVTStateData;
        public nint pwszURLReference;
        public uint dwProvFlags;
        public uint dwUIContext;
        public nint pSignatureSettings;
    }

    [DllImport("wintrust.dll", CharSet = CharSet.Unicode)]
    private static extern int WinVerifyTrust(nint hwnd, ref Guid action, ref WINTRUST_DATA data);
}

/// <summary>The Store copy's updates through StoreContext (F-PUB-04); the Store installs them itself.</summary>
public static class StoreUpdates
{
    /// <summary>The newest waiting update's version, or null when there is none. Throws when the Store does not answer.</summary>
    public static async Task<Version?> WaitingAsync()
    {
        var updates = await Windows.Services.Store.StoreContext.GetDefault().GetAppAndOptionalStorePackageUpdatesAsync();
        return updates.Count == 0
            ? null
            : updates.Select(u => u.Package.Id.Version).Select(v => new Version(v.Major, v.Minor, v.Build)).Max();
    }

    /// <summary>Asks the Store to download and install, with <paramref name="owner"/> as the parent of its dialog.</summary>
    public static async Task<bool> InstallAsync(nint owner)
    {
        var context = Windows.Services.Store.StoreContext.GetDefault();
        WinRT.Interop.InitializeWithWindow.Initialize(context, owner);
        var updates = await context.GetAppAndOptionalStorePackageUpdatesAsync();
        if (updates.Count == 0) return false;
        var result = await context.RequestDownloadAndInstallStorePackageUpdatesAsync(updates);
        return result.OverallState == Windows.Services.Store.StorePackageUpdateState.Completed;
    }
}

/// <summary>
/// The Store copy starts again after Windows closes it for an update (PRD 11). Only for updates: Gili restarts itself
/// after a crash, and a hang or a reboot should not bring it back.
/// </summary>
public static class RestartRegistration
{
    private const uint RESTART_NO_CRASH = 1, RESTART_NO_HANG = 2, RESTART_NO_REBOOT = 8;

    public static void ForUpdates() => RegisterApplicationRestart(null, RESTART_NO_CRASH | RESTART_NO_HANG | RESTART_NO_REBOOT);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegisterApplicationRestart(string? commandLine, uint flags);
}
