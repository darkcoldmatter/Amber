using Velopack;
using Velopack.Sources;

namespace Amber;

/// <summary>Where updating is.</summary>
public enum UpdateStage
{
    /// <summary>Running from a build folder (dotnet run, a test copy) rather than an install: it never checks or updates.</summary>
    NotInstalled,
    /// <summary>Installed, and nothing checked yet since Amber started.</summary>
    Idle,
    Checking,
    UpToDate,
    /// <summary>
    /// A newer version is on offer (<see cref="UpdateState.Version"/>). After a failed download it stays on offer, with
    /// <see cref="UpdateState.DownloadFailed"/> set, so it can be tried again.
    /// </summary>
    Available,
    /// <summary>Downloading the version on offer (<see cref="UpdateState.Percent"/>).</summary>
    Downloading,
    /// <summary>Downloaded: Amber quits, the updater puts the new version in place and starts it.</summary>
    Restarting,
    /// <summary>A check the user asked for failed.</summary>
    CheckFailed,
    /// <summary>Handing the downloaded update over to the updater failed; Amber carries on as it was.</summary>
    UpdateFailed,
}

/// <summary>
/// The stage, the version on offer (Available, Downloading, Restarting), how much of it has downloaded, and whether the
/// last try to download the version on offer failed.
/// </summary>
public sealed record UpdateState(UpdateStage Stage, string? Version = null, int Percent = 0, bool DownloadFailed = false);

/// <summary>The part of updating that talks to GitHub and to the updater. Tests use a stand-in.</summary>
public interface IUpdateServer
{
    /// <summary>The newer version on offer, or null when this one is the newest.</summary>
    Task<string?> FindNewerAsync();

    /// <summary>Downloads the version <see cref="FindNewerAsync"/> found, reporting progress from 0 to 100.</summary>
    Task DownloadAsync(Action<int> progress);

    /// <summary>Starts the updater, which waits for Amber to quit, puts the new version in place and starts it with these arguments.</summary>
    void ApplyOnExit(string[] restartArgs);
}

/// <summary>
/// Looks for a new version a minute after Amber starts, then once a day, and whenever the user clicks Check.
/// A check Amber started itself fails quietly (offline, say) and is tried again an hour later; one the user asked for
/// says it failed. An update is downloaded only when the user asks for it. Knows nothing about how it is shown:
/// views read <see cref="State"/> and <see cref="ShouldAnnounce"/> and follow <see cref="Changed"/>.
/// </summary>
public sealed class Updates
{
    public static readonly TimeSpan FirstCheck = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan Daily = TimeSpan.FromHours(24);
    public static readonly TimeSpan Retry = TimeSpan.FromHours(1);

    readonly IUpdateServer? _server;
    readonly Settings _settings;
    DateTime? _lastCheck;
    bool _lastCheckFailed;

    /// <param name="server">null when Amber isn't installed</param>
    /// <param name="settings">where the skipped version is remembered</param>
    public Updates(IUpdateServer? server, Settings settings)
    {
        _server = server;
        _settings = settings;
        State = new(server is null ? UpdateStage.NotInstalled : UpdateStage.Idle);
    }

    public UpdateState State { get; private set; }

    /// <summary>False for a copy run from a build folder (dotnet run, a test copy) rather than installed by Setup.</summary>
    public bool IsInstalled => _server is not null;

    /// <summary>Raised on the UI thread whenever <see cref="State"/> or <see cref="ShouldAnnounce"/> changes.</summary>
    public event Action? Changed;

    /// <summary>
    /// True while a version is on offer that the user hasn't skipped: the main window announces it. Settings offers
    /// any version on offer, skipped or not.
    /// </summary>
    public bool ShouldAnnounce => State.Stage == UpdateStage.Available && State.Version != _settings.SkippedVersion;

    /// <summary>Stops announcing the version on offer, also after a restart. Settings still offers it; a newer one is announced.</summary>
    public void Skip()
    {
        if (State.Stage != UpdateStage.Available || _settings.SkippedVersion == State.Version) return;
        _settings.SkippedVersion = State.Version;
        Changed?.Invoke();
    }

    /// <summary>
    /// How long until the next automatic check; null when there is nothing to check. Never less than
    /// <see cref="FirstCheck"/> (no tight loop when a check is skipped) nor more than a day (the clock may have been set back).
    /// </summary>
    public TimeSpan? NextCheckIn(DateTime now)
    {
        if (_server is null) return null;
        if (_lastCheck is not { } last) return FirstCheck;
        var wait = last + (_lastCheckFailed ? Retry : Daily) - now;
        return wait < FirstCheck ? FirstCheck : wait > Daily ? Daily : wait;
    }

    /// <param name="userAsked">true when the user asked: a failure then becomes <see cref="UpdateStage.CheckFailed"/>; otherwise the state goes back to what it was</param>
    public async Task CheckAsync(bool userAsked, DateTime now)
    {
        if (_server is null || State.Stage is UpdateStage.Checking or UpdateStage.Downloading or UpdateStage.Restarting) return;

        var before = State;
        _lastCheck = now;
        Show(new(UpdateStage.Checking));
        try
        {
            var version = await _server.FindNewerAsync();
            _lastCheckFailed = false;
            Show(version is null ? new(UpdateStage.UpToDate) : new(UpdateStage.Available, version));
        }
        catch (Exception)
        {
            // No network, GitHub unreachable or rate-limited, a bad release file: nothing Amber can fix.
            _lastCheckFailed = true;
            Show(userAsked ? new(UpdateStage.CheckFailed) : before);
        }
    }

    /// <summary>
    /// Downloads the version on offer; true once it is ready for <see cref="ApplyOnExit"/>. If the download fails, the
    /// version stays on offer, marked <see cref="UpdateState.DownloadFailed"/>, and can be tried again.
    /// </summary>
    public async Task<bool> DownloadAsync()
    {
        if (_server is null || State.Stage != UpdateStage.Available) return false;

        var version = State.Version;
        Show(new(UpdateStage.Downloading, version));
        try
        {
            await _server.DownloadAsync(percent => Show(new(UpdateStage.Downloading, version, percent)));
        }
        catch (Exception)
        {
            // Offline, out of disk space, a broken file: nothing Amber can fix, but the next try may work.
            Show(new(UpdateStage.Available, version, DownloadFailed: true));
            return false;
        }
        Show(new(UpdateStage.Restarting, version));
        return true;
    }

    /// <summary>Hands the downloaded update to the updater; true if Amber should now quit so it can be installed.</summary>
    public bool ApplyOnExit(string[] restartArgs)
    {
        if (_server is null || State.Stage != UpdateStage.Restarting) return false;
        try
        {
            _server.ApplyOnExit(restartArgs);
            return true;
        }
        catch (Exception)
        {
            Show(new(UpdateStage.UpdateFailed));
            return false;
        }
    }

    void Show(UpdateState state)
    {
        if (state == State) return;
        State = state;
        Changed?.Invoke();
    }
}

/// <summary>Velopack's update manager, reading the releases of Amber's GitHub repository (stable ones only).</summary>
sealed class VelopackServer : IUpdateServer
{
    const string RepositoryUrl = "https://github.com/darkcoldmatter/Amber";

    readonly UpdateManager _manager;
    UpdateInfo? _found;

    VelopackServer(UpdateManager manager) => _manager = manager;

    /// <summary>The server for this copy of Amber, or null when it wasn't installed by Setup. Needs VelopackApp to have run.</summary>
    public static VelopackServer? ForThisCopy()
    {
        var manager = new UpdateManager(new GithubSource(RepositoryUrl, accessToken: null, prerelease: false));
        return manager.IsInstalled ? new VelopackServer(manager) : null;
    }

    public async Task<string?> FindNewerAsync()
    {
        // Velopack reads its local files before it first awaits, so all of it runs off the UI thread.
        _found = await Task.Run(() => _manager.CheckForUpdatesAsync());
        return _found?.TargetFullRelease.Version.ToString();
    }

    public Task DownloadAsync(Action<int> progress)
    {
        var found = _found ?? throw new InvalidOperationException("No update was found to download.");
        // Progress comes from a background thread; pass it on to the UI thread, in order.
        var ui = SynchronizationContext.Current;
        return Task.Run(() => _manager.DownloadUpdatesAsync(found, percent => ui?.Post(_ => progress(percent), null)));
    }

    public void ApplyOnExit(string[] restartArgs) =>
        _manager.WaitExitThenApplyUpdates(_found?.TargetFullRelease, silent: false, restart: true, restartArgs);
}
