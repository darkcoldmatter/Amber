using Amber.Views;

namespace Amber.Tests;

public class UpdatesTests
{
    static readonly DateTime Start = new(2026, 10, 17, 9, 0, 0, DateTimeKind.Utc);

    /// <summary>Stands in for GitHub and the updater; each step does what the test set up, at once.</summary>
    sealed class FakeServer : IUpdateServer
    {
        public string? Newer;
        public bool CheckFails, DownloadFails, ApplyFails;
        public int[] Progress = [];
        public int Checks;
        public string[]? AppliedWith;

        public Task<string?> FindNewerAsync()
        {
            Checks++;
            return CheckFails ? Task.FromException<string?>(new HttpRequestException("offline")) : Task.FromResult(Newer);
        }

        public Task DownloadAsync(Action<int> progress)
        {
            foreach (var percent in Progress) progress(percent);
            return DownloadFails ? Task.FromException(new IOException("disk full")) : Task.CompletedTask;
        }

        public void ApplyOnExit(string[] restartArgs)
        {
            if (ApplyFails) throw new FileNotFoundException("Update.exe");
            AppliedWith = restartArgs;
        }
    }

    static Updates Installed(FakeServer server, Settings? settings = null) => new(server, settings ?? new Settings());

    static async Task<Updates> Offering(string version, Settings? settings = null)
    {
        var updates = Installed(new FakeServer { Newer = version }, settings);
        await updates.CheckAsync(userAsked: false, Start);
        return updates;
    }

    [Fact]
    public void A_copy_that_was_not_installed_never_checks()
    {
        var updates = new Updates(server: null, new Settings());

        Assert.False(updates.IsInstalled);
        Assert.Equal(UpdateStage.NotInstalled, updates.State.Stage);
        Assert.Null(updates.NextCheckIn(Start));
        Assert.Equal(("Not installed", false, (string?)null), SettingsView.UpdateRow(updates.State));
    }

    [Fact]
    public async Task A_copy_that_was_not_installed_ignores_requests_to_check_or_update()
    {
        var updates = new Updates(server: null, new Settings());

        await updates.CheckAsync(userAsked: true, Start);

        Assert.False(await updates.DownloadAsync());
        Assert.False(updates.ApplyOnExit([]));
        Assert.Equal(UpdateStage.NotInstalled, updates.State.Stage);
    }

    [Fact]
    public void An_installed_copy_starts_ready_to_check()
    {
        var updates = Installed(new FakeServer());
        var state = updates.State;

        Assert.True(updates.IsInstalled);
        Assert.Equal(UpdateStage.Idle, state.Stage);
        Assert.Equal(("Check", true, (string?)null), SettingsView.UpdateRow(state));
    }

    [Fact]
    public async Task Checking_shows_while_it_runs_then_up_to_date()
    {
        var updates = Installed(new FakeServer());
        var seen = new List<UpdateStage>();
        updates.Changed += () => seen.Add(updates.State.Stage);

        await updates.CheckAsync(userAsked: true, Start);

        Assert.Equal([UpdateStage.Checking, UpdateStage.UpToDate], seen);
        Assert.Equal(("Up to date", false, (string?)null), SettingsView.UpdateRow(updates.State));
    }

    [Fact]
    public async Task A_newer_version_is_offered_and_announced()
    {
        var updates = await Offering("1.1.0");

        Assert.Equal(new UpdateState(UpdateStage.Available, "1.1.0"), updates.State);
        Assert.True(updates.ShouldAnnounce);
        Assert.Equal(("Update to 1.1.0", true, (string?)null), SettingsView.UpdateRow(updates.State));
    }

    [Fact]
    public async Task A_check_the_user_asked_for_says_when_it_failed_and_can_be_tried_again()
    {
        var updates = Installed(new FakeServer { CheckFails = true });

        await updates.CheckAsync(userAsked: true, Start);

        Assert.Equal(UpdateStage.CheckFailed, updates.State.Stage);
        Assert.Equal(("Check", true, "Couldn't check"), SettingsView.UpdateRow(updates.State));
    }

    [Fact]
    public async Task An_automatic_check_fails_quietly_and_keeps_what_was_known()
    {
        var server = new FakeServer { Newer = "1.1.0" };
        var updates = Installed(server);
        await updates.CheckAsync(userAsked: false, Start);

        server.CheckFails = true;
        await updates.CheckAsync(userAsked: false, Start.AddDays(1));

        Assert.Equal(new UpdateState(UpdateStage.Available, "1.1.0"), updates.State);
    }

    [Fact]
    public async Task A_later_check_finds_a_newer_version_than_the_one_on_offer()
    {
        var server = new FakeServer { Newer = "1.1.0" };
        var updates = Installed(server);
        await updates.CheckAsync(userAsked: false, Start);

        server.Newer = "1.2.0";
        await updates.CheckAsync(userAsked: false, Start.AddDays(1));

        Assert.Equal("1.2.0", updates.State.Version);
    }

    [Fact]
    public async Task A_skipped_version_is_not_announced_but_settings_still_offers_it()
    {
        var updates = await Offering("1.1.0");
        var changes = 0;
        updates.Changed += () => changes++;

        updates.Skip();

        Assert.False(updates.ShouldAnnounce);
        Assert.Equal(1, changes);
        Assert.Equal(("Update to 1.1.0", true, (string?)null), SettingsView.UpdateRow(updates.State));
    }

    [Fact]
    public async Task A_skipped_version_stays_skipped_after_a_restart()
    {
        var settings = new Settings();
        (await Offering("1.1.0", settings)).Skip();

        var afterRestart = await Offering("1.1.0", settings);

        Assert.Equal("1.1.0", settings.SkippedVersion);
        Assert.False(afterRestart.ShouldAnnounce);
    }

    [Fact]
    public async Task A_version_newer_than_the_skipped_one_is_announced()
    {
        var settings = new Settings { SkippedVersion = "1.1.0" };

        var updates = await Offering("1.2.0", settings);

        Assert.True(updates.ShouldAnnounce);
    }

    [Fact]
    public async Task Only_a_version_on_offer_can_be_skipped()
    {
        var settings = new Settings();
        var updates = Installed(new FakeServer(), settings);
        await updates.CheckAsync(userAsked: true, Start);

        updates.Skip();

        Assert.Null(settings.SkippedVersion);
        Assert.False(updates.ShouldAnnounce);
    }

    [Fact]
    public void The_first_check_comes_a_minute_after_start()
    {
        Assert.Equal(TimeSpan.FromMinutes(1), Installed(new FakeServer()).NextCheckIn(Start));
    }

    [Fact]
    public async Task After_a_check_the_next_one_is_a_day_later_even_if_the_user_started_it()
    {
        var updates = Installed(new FakeServer());

        await updates.CheckAsync(userAsked: true, Start);

        Assert.Equal(TimeSpan.FromHours(24), updates.NextCheckIn(Start));
        Assert.Equal(TimeSpan.FromHours(14), updates.NextCheckIn(Start.AddHours(10)));
    }

    [Fact]
    public async Task A_failed_check_is_tried_again_an_hour_later()
    {
        var updates = Installed(new FakeServer { CheckFails = true });

        await updates.CheckAsync(userAsked: false, Start);

        Assert.Equal(TimeSpan.FromHours(1), updates.NextCheckIn(Start));
    }

    [Fact]
    public async Task The_wait_stays_between_a_minute_and_a_day()
    {
        var updates = Installed(new FakeServer());
        await updates.CheckAsync(userAsked: false, Start);

        Assert.Equal(TimeSpan.FromMinutes(1), updates.NextCheckIn(Start.AddDays(3)));    // long overdue (the PC slept)
        Assert.Equal(TimeSpan.FromHours(24), updates.NextCheckIn(Start.AddDays(-30)));   // the clock was set back
    }

    [Fact]
    public async Task Downloading_counts_up_then_restarts_into_the_new_version()
    {
        var server = new FakeServer { Newer = "1.1.0", Progress = [0, 42, 100] };
        var updates = Installed(server);
        await updates.CheckAsync(userAsked: true, Start);
        var seen = new List<UpdateState>();
        updates.Changed += () => seen.Add(updates.State);

        Assert.True(await updates.DownloadAsync());
        Assert.True(updates.ApplyOnExit(["--tray"]));

        Assert.Equal(
            [
                new(UpdateStage.Downloading, "1.1.0", 0), new(UpdateStage.Downloading, "1.1.0", 42),
                new(UpdateStage.Downloading, "1.1.0", 100), new(UpdateStage.Restarting, "1.1.0"),
            ],
            seen);
        Assert.Equal(("Downloading 42%", false, (string?)null), SettingsView.UpdateRow(seen[1]));
        Assert.Equal(("Restarting…", false, (string?)null), SettingsView.UpdateRow(updates.State));
        Assert.NotNull(server.AppliedWith);
        Assert.Equal(["--tray"], server.AppliedWith);
    }

    [Fact]
    public async Task A_skipped_version_can_still_be_installed()
    {
        var updates = await Offering("1.1.0");
        updates.Skip();

        Assert.True(await updates.DownloadAsync());
        Assert.Equal(UpdateStage.Restarting, updates.State.Stage);
    }

    [Fact]
    public async Task A_failed_download_keeps_the_version_on_offer_and_says_so()
    {
        var updates = Installed(new FakeServer { Newer = "1.1.0", DownloadFails = true });
        await updates.CheckAsync(userAsked: true, Start);

        Assert.False(await updates.DownloadAsync());

        Assert.Equal(new UpdateState(UpdateStage.Available, "1.1.0", DownloadFailed: true), updates.State);
        Assert.True(updates.ShouldAnnounce);
        Assert.Equal(("Update to 1.1.0", true, "Couldn't download"), SettingsView.UpdateRow(updates.State));
    }

    [Fact]
    public async Task After_a_failed_download_it_can_be_tried_again()
    {
        var server = new FakeServer { Newer = "1.1.0", DownloadFails = true, Progress = [0, 40] };
        var updates = Installed(server);
        await updates.CheckAsync(userAsked: true, Start);
        await updates.DownloadAsync();

        server.DownloadFails = false;
        var seen = new List<UpdateState>();
        updates.Changed += () => seen.Add(updates.State);

        Assert.True(await updates.DownloadAsync());
        Assert.Equal(new UpdateState(UpdateStage.Downloading, "1.1.0"), seen[0]); // the failure note goes
        Assert.Equal(new UpdateState(UpdateStage.Restarting, "1.1.0"), updates.State);
    }

    [Fact]
    public async Task A_new_check_clears_a_failed_download()
    {
        var updates = Installed(new FakeServer { Newer = "1.1.0", DownloadFails = true });
        await updates.CheckAsync(userAsked: true, Start);
        await updates.DownloadAsync();

        await updates.CheckAsync(userAsked: true, Start.AddMinutes(5));

        Assert.Equal(new UpdateState(UpdateStage.Available, "1.1.0"), updates.State);
    }

    [Fact]
    public async Task A_skipped_version_whose_download_failed_is_still_not_announced()
    {
        var updates = Installed(new FakeServer { Newer = "1.1.0", DownloadFails = true });
        await updates.CheckAsync(userAsked: true, Start);
        updates.Skip();

        await updates.DownloadAsync();

        Assert.True(updates.State.DownloadFailed);
        Assert.False(updates.ShouldAnnounce);
    }

    [Fact]
    public async Task If_the_updater_cannot_start_Amber_carries_on_and_says_so()
    {
        var updates = Installed(new FakeServer { Newer = "1.1.0", ApplyFails = true });
        await updates.CheckAsync(userAsked: true, Start);
        await updates.DownloadAsync();

        Assert.False(updates.ApplyOnExit([]));

        Assert.Equal(UpdateStage.UpdateFailed, updates.State.Stage);
    }

    [Fact]
    public async Task Nothing_downloads_before_an_update_was_found()
    {
        var updates = Installed(new FakeServer());

        Assert.False(await updates.DownloadAsync());
        Assert.False(updates.ApplyOnExit([]));
        Assert.Equal(UpdateStage.Idle, updates.State.Stage);
    }

    [Fact]
    public async Task No_check_starts_while_an_update_is_downloading_or_restarting()
    {
        var server = new FakeServer { Newer = "1.1.0" };
        var updates = Installed(server);
        await updates.CheckAsync(userAsked: true, Start);
        await updates.DownloadAsync();

        await updates.CheckAsync(userAsked: false, Start.AddDays(1));

        Assert.Equal(1, server.Checks);
        Assert.Equal(UpdateStage.Restarting, updates.State.Stage);
    }
}
