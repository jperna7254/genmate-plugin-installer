using GenMate.PluginInstaller.Core.Channel;
using GenMate.PluginInstaller.Core.Diagnostics;

namespace GenMate.PluginInstaller.Core.SelfUpdate;

/// <summary>
/// Replaces the running installer with a newer published one, at launch, before anything else runs.
/// </summary>
/// <remarks>
/// <para>
/// This exists so that the copy a customer already has can be fixed remotely. Every fielded
/// installer that predates it has to be replaced by hand, which is why the parts most likely to
/// need fixing in the field - which CAD applications are detected, how a release is laid out, what
/// a signature must prove - all run behind this, never in front of it.
/// </para>
/// <para>
/// It therefore runs on every launch and can never be the reason the app does not start. Every
/// failure - unreachable feed, interrupted download, full disk, refused rename - is logged and
/// absorbed, and the app carries on at the current version. The one path that can leave a user
/// with no installer at all is the gap between the two renames, and that path rolls itself back.
/// </para>
/// </remarks>
public sealed class SelfUpdateService
{
    private const string DownloadSuffix = ".new";
    private const string SupersededSuffix = ".old";

    private static readonly Version UnknownVersion = new(0, 0, 0, 0);

    private readonly Version _currentVersion;
    private readonly IInstallerReleaseSource _releases;
    private readonly IInstallerVerifier _verifier;
    private readonly IUpdateEnvironment _environment;
    private readonly IUpdateLog _log;
    private readonly TimeSpan _checkTimeout;
    private readonly TimeSpan _downloadTimeout;

    public SelfUpdateService(
        Version currentVersion,
        IInstallerReleaseSource releases,
        IInstallerVerifier verifier,
        IUpdateEnvironment environment,
        IUpdateLog log,
        TimeSpan? checkTimeout = null,
        TimeSpan? downloadTimeout = null)
    {
        _currentVersion = ReleaseVersion.Normalize(currentVersion);
        _releases = releases;
        _verifier = verifier;
        _environment = environment;
        _log = log;

        // The check is bounded far tighter than the download because the user is waiting on it with
        // nothing to look at, and a feed that hangs must not hold the window shut.
        _checkTimeout = checkTimeout ?? TimeSpan.FromSeconds(15);
        _downloadTimeout = downloadTimeout ?? TimeSpan.FromMinutes(20);
    }

    /// <summary>
    /// Removes what a previous update left behind. Windows will not let a process delete its own
    /// running image, so the copy renamed aside during the last update can only be deleted by the
    /// process that replaced it - here, on its next start.
    /// </summary>
    public void CleanUpPreviousUpdate()
    {
        try
        {
            var executablePath = _environment.CurrentExecutablePath;
            var directory = Path.GetDirectoryName(executablePath);
            if (string.IsNullOrEmpty(directory))
                return;

            foreach (var path in LeftoverPaths(directory, Path.GetFileName(executablePath)))
                TryDelete(path);
        }
        catch (Exception ex)
        {
            _log.Write("could not clean up after the previous update", ex);
        }
    }

    public async Task<SelfUpdateOutcome> TryUpdateAsync(
        InstallerChannel channel,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        var downloadPath = _environment.CurrentExecutablePath + DownloadSuffix;

        try
        {
            if (_currentVersion == UnknownVersion)
            {
                // A build that cannot state its own version reads every release as newer,
                // downloads it, relaunches into a build that also cannot state its version, and
                // does the same again. Refusing is the only outcome that is not a loop.
                _log.Write("this build reports no version of its own; not updating");
                return SelfUpdateOutcome.NotApplied;
            }

            if (!ReleaseVersion.IsAtOrAboveFloor(_currentVersion, channel.MinimumVersion))
            {
                _log.Write($"this build ({_currentVersion}) is below the channel's minimum ({channel.MinimumVersion})");
            }

            InstallerRelease? latest;
            using (var check = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                check.CancelAfter(_checkTimeout);
                latest = await _releases.GetLatestAsync(channel, check.Token);
            }

            if (latest is null)
            {
                _log.Write($"no installer release this build can read in {channel.Repo}");
                return SelfUpdateOutcome.AlreadyCurrent;
            }

            var target = ReleaseVersion.Normalize(latest.Version);
            if (target <= _currentVersion)
                return SelfUpdateOutcome.AlreadyCurrent;

            if (HasAlreadyBeenApplied(target))
                return SelfUpdateOutcome.NotApplied;

            _log.Write($"updating from {_currentVersion} to {latest.Version}");

            using (var download = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                download.CancelAfter(_downloadTimeout);
                await _releases.DownloadAsync(latest, downloadPath, progress, download.Token);
            }

            if (!IsTrusted(downloadPath))
            {
                TryDelete(downloadPath);
                return SelfUpdateOutcome.NotApplied;
            }

            return Swap(downloadPath, target) ? SelfUpdateOutcome.RelaunchStarted : SelfUpdateOutcome.NotApplied;
        }
        catch (Exception ex)
        {
            _log.Write("update abandoned; carrying on with the current version", ex);
            TryDelete(downloadPath);
            return SelfUpdateOutcome.NotApplied;
        }
    }

    /// <summary>
    /// Whether this exact target has already been swapped in once while this build was running.
    /// </summary>
    /// <remarks>
    /// The 0.0.0.0 guard covers a build that cannot state its version at all. This covers the other
    /// way the same brick happens: a release tagged above the assembly version baked into its own
    /// asset. Every launch would then read it as newer, download 65 MB, swap, relaunch into a build
    /// that reads it as newer again, forever. Recording the target the moment a swap completes
    /// bounds that to one attempt per published version, because the second attempt is refused
    /// before anything is downloaded.
    /// </remarks>
    private bool HasAlreadyBeenApplied(Version target)
    {
        Version? lastApplied;
        try
        {
            lastApplied = _environment.ReadLastAppliedTarget();
        }
        catch (Exception ex)
        {
            // Best effort by contract: without the record the updater behaves exactly as it did
            // before the record existed, which is preferable to refusing to update at all.
            _log.Write("could not read what the last update aimed at; continuing without that guard", ex);
            return false;
        }

        if (lastApplied is null)
            return false;

        var floor = ReleaseVersion.Normalize(lastApplied);
        if (_currentVersion >= floor || target > floor)
            return false;

        _log.Write(
            $"{target} was already installed once and this build still reports {_currentVersion}; " +
            "not applying it again");
        return true;
    }

    private void RecordAppliedTarget(Version target)
    {
        try
        {
            _environment.WriteLastAppliedTarget(target);
        }
        catch (Exception ex)
        {
            _log.Write($"could not record {target} as applied; a mis-tagged release could be retried", ex);
        }
    }

    private bool IsTrusted(string downloadPath)
    {
        try
        {
            if (_verifier.IsTrusted(downloadPath, out var reason))
                return true;

            _log.Write($"downloaded installer rejected: {reason ?? "no reason given"}");
        }
        catch (Exception ex)
        {
            _log.Write("the verifier threw; treating the downloaded installer as untrusted", ex);
        }

        return false;
    }

    private bool Swap(string downloadPath, Version target)
    {
        var executablePath = _environment.CurrentExecutablePath;

        if (!TryChooseSupersededPath(executablePath, out var supersededPath))
        {
            _log.Write("no free name for the outgoing image; update abandoned");
            TryDelete(downloadPath);
            return false;
        }

        // Windows refuses to overwrite or delete a running image but does allow it to be renamed,
        // and allows a new file to be written at the freed path. That is why the swap is two
        // renames within one directory rather than a copy over the top.
        try
        {
            _environment.MoveFile(executablePath, supersededPath);
        }
        catch (Exception ex)
        {
            _log.Write("could not rename the running installer aside; update abandoned", ex);
            TryDelete(downloadPath);
            return false;
        }

        try
        {
            _environment.MoveFile(downloadPath, executablePath);
        }
        catch (Exception ex)
        {
            // The only window in which the user can be left with no installer at all, so it is the
            // one step that undoes its own work rather than merely reporting.
            _log.Write("could not move the downloaded installer into place; rolling the first rename back", ex);
            try
            {
                _environment.MoveFile(supersededPath, executablePath);
            }
            catch (Exception rollbackEx)
            {
                _log.Write($"rollback failed - the installer is at {supersededPath}, not at {executablePath}", rollbackEx);
            }

            TryDelete(downloadPath);
            return false;
        }

        // Recorded only once both renames have landed, so a swap that failed and rolled itself
        // back leaves no record to suppress the retry it deserves.
        RecordAppliedTarget(target);

        try
        {
            _environment.Launch(executablePath);
        }
        catch (Exception ex)
        {
            // Both renames landed, so the newer installer is in place and the next launch runs it.
            // Reporting failure here keeps this process alive on the old image rather than closing
            // a window that never comes back.
            _log.Write("the updated installer is in place but could not be started; carrying on", ex);
            return false;
        }

        return true;
    }

    private bool TryChooseSupersededPath(string executablePath, out string supersededPath)
    {
        supersededPath = executablePath + SupersededSuffix;
        if (!_environment.FileExists(supersededPath))
            return true;

        TryDelete(supersededPath);
        if (!_environment.FileExists(supersededPath))
            return true;

        // An image from an earlier update is still locked by a process that has not exited. Take a
        // different name rather than refusing to update until the machine is rebooted.
        for (var attempt = 1; attempt <= 99; attempt++)
        {
            var candidate = $"{executablePath}.{attempt}{SupersededSuffix}";
            if (_environment.FileExists(candidate))
                continue;

            supersededPath = candidate;
            return true;
        }

        return false;
    }

    private IEnumerable<string> LeftoverPaths(string directory, string executableName)
    {
        string[] patterns =
        [
            executableName + SupersededSuffix,
            executableName + ".*" + SupersededSuffix,
            executableName + DownloadSuffix
        ];

        return patterns
            .SelectMany(pattern => _environment.ListFiles(directory, pattern))
            .Where(path => IsLeftoverName(Path.GetFileName(path), executableName))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    // A search pattern whose extension is exactly three characters also matches longer extensions
    // on Windows, a survival of 8.3 name matching - ".old" matches ".older" too. The installer is
    // usually run straight out of the user's Downloads folder, so the results are filtered here
    // rather than trusted, and only names built from this executable's own are ever deleted.
    private static bool IsLeftoverName(string name, string executableName) =>
        name.StartsWith(executableName + ".", StringComparison.OrdinalIgnoreCase) &&
        (name.EndsWith(SupersededSuffix, StringComparison.OrdinalIgnoreCase) ||
         name.Equals(executableName + DownloadSuffix, StringComparison.OrdinalIgnoreCase));

    private void TryDelete(string path)
    {
        try
        {
            if (_environment.FileExists(path))
                _environment.DeleteFile(path);
        }
        catch (Exception ex)
        {
            _log.Write($"could not delete {path}", ex);
        }
    }
}
