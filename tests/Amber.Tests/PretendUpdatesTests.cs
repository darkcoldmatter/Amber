#if DEBUG
namespace Amber.Tests;

/// <summary>The Debug-only preview of the update notice (dotnet run -- --pretend-update 1.0.1).</summary>
public class PretendUpdatesTests
{
    static readonly DateTime Start = new(2026, 10, 18, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Only_the_pretend_argument_with_a_version_makes_a_pretend_server()
    {
        Assert.Null(PretendUpdateServer.FromArgs(["Amber.exe"]));
        Assert.Null(PretendUpdateServer.FromArgs(["Amber.exe", "--pretend-update"]));
        Assert.NotNull(PretendUpdateServer.FromArgs(["Amber.exe", "--pretend-update", "1.0.1"]));
    }

    [Fact]
    public async Task The_pretend_version_is_on_offer_and_downloads_to_the_end()
    {
        var updates = new Updates(PretendUpdateServer.FromArgs(["Amber.exe", "--pretend-update", "1.0.1"], TimeSpan.Zero), new Settings());
        await updates.CheckAsync(userAsked: false, Start);
        Assert.Equal(new UpdateState(UpdateStage.Available, "1.0.1"), updates.State);

        var percents = new List<int>();
        updates.Changed += () => percents.Add(updates.State.Percent);
        Assert.True(await updates.DownloadAsync());

        Assert.Equal(100, percents.Max());
        Assert.Equal(UpdateStage.Restarting, updates.State.Stage);
    }

    [Fact]
    public async Task With_pretend_fail_the_first_download_fails_part_way_and_the_next_one_works()
    {
        var updates = new Updates(PretendUpdateServer.FromArgs(["Amber.exe", "--pretend-update", "1.0.1", "--pretend-fail"], TimeSpan.Zero), new Settings());
        await updates.CheckAsync(userAsked: false, Start);

        Assert.False(await updates.DownloadAsync());
        Assert.Equal(new UpdateState(UpdateStage.Available, "1.0.1", DownloadFailed: true), updates.State);
        Assert.True(await updates.DownloadAsync());
    }
}
#endif
