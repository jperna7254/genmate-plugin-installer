using GenMate.PluginInstaller.Core.Channel;
using GenMate.PluginInstaller.Core.SelfUpdate;

namespace GenMate.PluginInstaller.Tests;

public class SelfUpdateServiceTests
{
    private static readonly InstallerChannel Channel = ChannelDocument.Fallback.Installer;

    private static readonly Version Current = new(1, 0, 2);

    private const string NewImage = "updated-installer";

    [Fact]
    public async Task Nothing_happens_when_the_published_release_is_not_newer()
    {
        using var environment = new FakeUpdateEnvironment();
        var releases = new FakeReleaseSource { Latest = Release("1.0.2") };
        var service = Build(environment, releases, out _);

        var outcome = await service.TryUpdateAsync(Channel);

        Assert.Equal(SelfUpdateOutcome.AlreadyCurrent, outcome);
        Assert.Equal(0, releases.DownloadCalls);
        Assert.Equal([Path.GetFileName(environment.CurrentExecutablePath)], environment.FileNames());
    }

    [Fact]
    public async Task Nothing_happens_when_the_feed_carries_no_readable_release()
    {
        using var environment = new FakeUpdateEnvironment();
        var releases = new FakeReleaseSource { Latest = null };
        var service = Build(environment, releases, out _);

        Assert.Equal(SelfUpdateOutcome.AlreadyCurrent, await service.TryUpdateAsync(Channel));
        Assert.Equal(0, releases.DownloadCalls);
    }

    [Fact]
    public async Task A_newer_release_is_downloaded_swapped_in_and_relaunched()
    {
        using var environment = new FakeUpdateEnvironment();
        var releases = new FakeReleaseSource { Latest = Release("1.1.0") };
        var service = Build(environment, releases, out _);

        var outcome = await service.TryUpdateAsync(Channel);

        Assert.Equal(SelfUpdateOutcome.RelaunchStarted, outcome);
        Assert.Equal(NewImage, environment.ReadImage(environment.CurrentExecutablePath));
        Assert.Equal(FakeUpdateEnvironment.CurrentImage, environment.ReadImage(environment.WithSuffix(".old")));
        Assert.False(environment.FileExists(environment.WithSuffix(".new")));
        Assert.Equal([environment.CurrentExecutablePath], environment.Launched);
    }

    [Fact]
    public async Task An_interrupted_download_leaves_the_current_installer_in_place()
    {
        using var environment = new FakeUpdateEnvironment();
        var releases = new FakeReleaseSource
        {
            Latest = Release("1.1.0"),
            DownloadThrows = new HttpRequestException("connection reset")
        };
        var service = Build(environment, releases, out var log);

        var outcome = await service.TryUpdateAsync(Channel);

        Assert.Equal(SelfUpdateOutcome.NotApplied, outcome);
        Assert.Equal(FakeUpdateEnvironment.CurrentImage, environment.ReadImage(environment.CurrentExecutablePath));
        Assert.Equal([Path.GetFileName(environment.CurrentExecutablePath)], environment.FileNames());
        Assert.Empty(environment.Launched);
        Assert.True(log.Mentions("connection reset"));
    }

    [Fact]
    public async Task An_unreachable_feed_leaves_the_current_installer_in_place()
    {
        using var environment = new FakeUpdateEnvironment();
        var releases = new FakeReleaseSource { GetLatestThrows = new HttpRequestException("no such host") };
        var service = Build(environment, releases, out var log);

        Assert.Equal(SelfUpdateOutcome.NotApplied, await service.TryUpdateAsync(Channel));
        Assert.Equal([Path.GetFileName(environment.CurrentExecutablePath)], environment.FileNames());
        Assert.True(log.Mentions("no such host"));
    }

    [Fact]
    public async Task A_rejected_download_is_discarded_and_never_swapped_in()
    {
        using var environment = new FakeUpdateEnvironment();
        var releases = new FakeReleaseSource { Latest = Release("1.1.0") };
        var log = new RecordingLog();
        var service = new SelfUpdateService(Current, releases, new RejectingVerifier(), environment, log);

        var outcome = await service.TryUpdateAsync(Channel);

        Assert.Equal(SelfUpdateOutcome.NotApplied, outcome);
        Assert.Equal(FakeUpdateEnvironment.CurrentImage, environment.ReadImage(environment.CurrentExecutablePath));
        Assert.Equal([Path.GetFileName(environment.CurrentExecutablePath)], environment.FileNames());
        Assert.Empty(environment.Launched);
        Assert.True(log.Mentions("pinned identity"));
    }

    [Fact]
    public async Task A_verifier_that_throws_is_a_rejection()
    {
        using var environment = new FakeUpdateEnvironment();
        var releases = new FakeReleaseSource { Latest = Release("1.1.0") };
        var log = new RecordingLog();
        var service = new SelfUpdateService(Current, releases, new ThrowingVerifier(), environment, log);

        Assert.Equal(SelfUpdateOutcome.NotApplied, await service.TryUpdateAsync(Channel));
        Assert.Equal(FakeUpdateEnvironment.CurrentImage, environment.ReadImage(environment.CurrentExecutablePath));
        Assert.True(log.Mentions("untrusted"));
    }

    [Fact]
    public async Task A_refused_first_rename_leaves_the_current_installer_in_place()
    {
        using var environment = new FakeUpdateEnvironment();
        environment.FailMoveWhen = (_, destination) => destination.EndsWith(".old", StringComparison.Ordinal);
        var releases = new FakeReleaseSource { Latest = Release("1.1.0") };
        var service = Build(environment, releases, out var log);

        var outcome = await service.TryUpdateAsync(Channel);

        Assert.Equal(SelfUpdateOutcome.NotApplied, outcome);
        Assert.Equal(FakeUpdateEnvironment.CurrentImage, environment.ReadImage(environment.CurrentExecutablePath));
        Assert.Equal([Path.GetFileName(environment.CurrentExecutablePath)], environment.FileNames());
        Assert.Empty(environment.Launched);
        Assert.True(log.Mentions("rename the running installer aside"));
    }

    [Fact]
    public async Task A_refused_second_rename_rolls_the_first_one_back()
    {
        using var environment = new FakeUpdateEnvironment();
        environment.FailMoveWhen = (source, _) => source.EndsWith(".new", StringComparison.Ordinal);
        var releases = new FakeReleaseSource { Latest = Release("1.1.0") };
        var service = Build(environment, releases, out var log);

        var outcome = await service.TryUpdateAsync(Channel);

        Assert.Equal(SelfUpdateOutcome.NotApplied, outcome);
        Assert.Equal(FakeUpdateEnvironment.CurrentImage, environment.ReadImage(environment.CurrentExecutablePath));
        Assert.Equal([Path.GetFileName(environment.CurrentExecutablePath)], environment.FileNames());
        Assert.Empty(environment.Launched);
        Assert.True(log.Mentions("rolling the first rename back"));
    }

    [Fact]
    public async Task A_rollback_that_also_fails_is_recorded_rather_than_thrown()
    {
        using var environment = new FakeUpdateEnvironment();
        environment.FailMoveWhen = (source, _) =>
            source.EndsWith(".new", StringComparison.Ordinal) || source.EndsWith(".old", StringComparison.Ordinal);
        var releases = new FakeReleaseSource { Latest = Release("1.1.0") };
        var service = Build(environment, releases, out var log);

        var outcome = await service.TryUpdateAsync(Channel);

        Assert.Equal(SelfUpdateOutcome.NotApplied, outcome);
        Assert.True(log.Mentions("rollback failed"));
        Assert.Equal(FakeUpdateEnvironment.CurrentImage, environment.ReadImage(environment.WithSuffix(".old")));
    }

    [Fact]
    public async Task An_installer_that_cannot_be_relaunched_keeps_running_on_the_old_image()
    {
        using var environment = new FakeUpdateEnvironment { FailLaunch = true };
        var releases = new FakeReleaseSource { Latest = Release("1.1.0") };
        var service = Build(environment, releases, out var log);

        var outcome = await service.TryUpdateAsync(Channel);

        Assert.Equal(SelfUpdateOutcome.NotApplied, outcome);
        Assert.Equal(NewImage, environment.ReadImage(environment.CurrentExecutablePath));
        Assert.True(log.Mentions("could not be started"));
    }

    [Fact]
    public async Task An_outgoing_image_that_cannot_be_deleted_does_not_block_the_update()
    {
        using var environment = new FakeUpdateEnvironment();
        File.WriteAllText(environment.WithSuffix(".old"), "still running");
        environment.FailDeleteWhen = path => path.EndsWith(".exe.old", StringComparison.Ordinal);
        var releases = new FakeReleaseSource { Latest = Release("1.1.0") };
        var service = Build(environment, releases, out _);

        var outcome = await service.TryUpdateAsync(Channel);

        Assert.Equal(SelfUpdateOutcome.RelaunchStarted, outcome);
        Assert.Equal(NewImage, environment.ReadImage(environment.CurrentExecutablePath));
        Assert.Equal(FakeUpdateEnvironment.CurrentImage, environment.ReadImage(environment.WithSuffix(".1.old")));
    }

    [Fact]
    public async Task A_build_that_reports_no_version_of_its_own_never_updates()
    {
        using var environment = new FakeUpdateEnvironment();
        var releases = new FakeReleaseSource { Latest = Release("1.1.0") };
        var log = new RecordingLog();
        var service = new SelfUpdateService(
            new Version(0, 0, 0, 0), releases, new AcceptUnsignedInstallerVerifier(), environment, log);

        Assert.Equal(SelfUpdateOutcome.NotApplied, await service.TryUpdateAsync(Channel));
        Assert.Equal(0, releases.GetLatestCalls);
        Assert.True(log.Mentions("reports no version"));
    }

    // A release tagged above the assembly version baked into its own asset - a hand-made release or
    // a workflow that stops deriving both from the same source. Each launch reads it as newer than
    // itself, so without a record of what the last swap aimed at every launch downloads and swaps
    // again, forever.
    [Fact]
    public async Task A_release_that_stays_newer_after_it_is_installed_is_not_installed_twice()
    {
        using var environment = new FakeUpdateEnvironment();
        var releases = new FakeReleaseSource { Latest = Release("9.9.9") };

        var firstLaunch = Build(environment, releases, out _);
        Assert.Equal(SelfUpdateOutcome.RelaunchStarted, await firstLaunch.TryUpdateAsync(Channel));

        // The relaunched child: a new service over the same persisted record, still reporting the
        // version it reported before, because the release it just installed was mis-tagged.
        var secondLaunch = Build(environment, releases, out var log);
        var outcome = await secondLaunch.TryUpdateAsync(Channel);

        Assert.Equal(SelfUpdateOutcome.NotApplied, outcome);
        Assert.Equal(1, releases.DownloadCalls);
        Assert.Equal([environment.CurrentExecutablePath], environment.Launched);
        Assert.True(log.Mentions("not applying it again"));

        // The maintainer corrects the mis-tag with a release numbered below it. Refusing that too
        // would leave these copies unfixable except by numbering past the bogus tag.
        releases.Latest = Release("1.2.0");
        var thirdLaunch = Build(environment, releases, out _);

        Assert.Equal(SelfUpdateOutcome.RelaunchStarted, await thirdLaunch.TryUpdateAsync(Channel));
        Assert.Equal(2, releases.DownloadCalls);
        Assert.Equal(new Version(1, 2, 0, 0), environment.LastAppliedTarget);
    }

    [Fact]
    public async Task A_release_newer_than_the_one_already_installed_is_still_installed()
    {
        using var environment = new FakeUpdateEnvironment { LastAppliedTarget = new Version(9, 9, 9, 0) };
        var releases = new FakeReleaseSource { Latest = Release("10.0.0") };
        var service = Build(environment, releases, out _);

        Assert.Equal(SelfUpdateOutcome.RelaunchStarted, await service.TryUpdateAsync(Channel));
        Assert.Equal(new Version(10, 0, 0, 0), environment.LastAppliedTarget);
    }

    [Fact]
    public async Task A_swap_that_rolled_itself_back_leaves_nothing_to_suppress_the_retry()
    {
        using var environment = new FakeUpdateEnvironment();
        environment.FailMoveWhen = (source, _) => source.EndsWith(".new", StringComparison.Ordinal);
        var releases = new FakeReleaseSource { Latest = Release("1.1.0") };
        var service = Build(environment, releases, out _);

        Assert.Equal(SelfUpdateOutcome.NotApplied, await service.TryUpdateAsync(Channel));
        Assert.Null(environment.LastAppliedTarget);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task An_unusable_update_record_costs_the_guard_and_not_the_update(bool failRead, bool failWrite)
    {
        using var environment = new FakeUpdateEnvironment
        {
            FailReadLastAppliedTarget = failRead,
            FailWriteLastAppliedTarget = failWrite
        };
        var releases = new FakeReleaseSource { Latest = Release("1.1.0") };
        var service = Build(environment, releases, out var log);

        Assert.Equal(SelfUpdateOutcome.RelaunchStarted, await service.TryUpdateAsync(Channel));
        Assert.Equal(NewImage, environment.ReadImage(environment.CurrentExecutablePath));
        Assert.True(log.Mentions("update record"));
    }

    [Fact]
    public void The_relaunched_installer_deletes_what_the_previous_update_left_behind()
    {
        using var environment = new FakeUpdateEnvironment();
        File.WriteAllText(environment.WithSuffix(".old"), "outgoing");
        File.WriteAllText(environment.WithSuffix(".3.old"), "outgoing");
        File.WriteAllText(environment.WithSuffix(".new"), "half a download");
        File.WriteAllText(Path.Combine(environment.Root, "the user's notes.old"), "not ours");
        var service = Build(environment, new FakeReleaseSource(), out _);

        service.CleanUpPreviousUpdate();

        Assert.Equal(
            [Path.GetFileName(environment.CurrentExecutablePath), "the user's notes.old"],
            environment.FileNames().OrderBy(n => n, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void Cleanup_survives_an_outgoing_image_that_is_still_locked()
    {
        using var environment = new FakeUpdateEnvironment();
        File.WriteAllText(environment.WithSuffix(".old"), "still running");
        environment.FailDeleteWhen = _ => true;
        var service = Build(environment, new FakeReleaseSource(), out var log);

        service.CleanUpPreviousUpdate();

        Assert.True(environment.FileExists(environment.WithSuffix(".old")));
        Assert.True(log.Mentions("could not delete"));
    }

    private static InstallerRelease Release(string version) =>
        new(Version.Parse(version), new Uri("https://example.invalid/GenMate.PluginInstaller.exe"));

    private static SelfUpdateService Build(
        FakeUpdateEnvironment environment, FakeReleaseSource releases, out RecordingLog log)
    {
        log = new RecordingLog();
        releases.DownloadedImage = NewImage;
        return new SelfUpdateService(
            Current, releases, new AcceptUnsignedInstallerVerifier(), environment, log);
    }
}
