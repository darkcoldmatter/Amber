using Amber.Views;

namespace Amber.Tests;

public class UpdateNoticeTests
{
    [Fact]
    public void A_version_on_offer_is_announced_with_Update_and_skip()
    {
        Assert.Equal(("A new version is available", (string?)null, true), UpdateNotice.Look(new(UpdateStage.Available, "1.1.0"), announce: true));
    }

    [Fact]
    public void A_skipped_version_is_not_announced()
    {
        Assert.Null(UpdateNotice.Look(new(UpdateStage.Available, "1.1.0"), announce: false));
    }

    [Fact]
    public void While_downloading_the_button_counts_up_and_skip_goes()
    {
        Assert.Equal(("A new version is available", "42%", false), UpdateNotice.Look(new(UpdateStage.Downloading, "1.1.0", 42), announce: false));
    }

    [Fact]
    public void After_a_failed_download_the_note_says_so_and_Update_can_be_tried_again()
    {
        Assert.Equal(("Couldn't download the update", (string?)null, true),
            UpdateNotice.Look(new(UpdateStage.Available, "1.1.0", DownloadFailed: true), announce: true));
    }

    [Fact]
    public void Restarting_shows_until_Amber_quits()
    {
        Assert.Equal(("Restarting…", "100%", false), UpdateNotice.Look(new(UpdateStage.Restarting, "1.1.0"), announce: false));
    }

    [Theory]
    [InlineData(UpdateStage.NotInstalled)]
    [InlineData(UpdateStage.Idle)]
    [InlineData(UpdateStage.Checking)]
    [InlineData(UpdateStage.UpToDate)]
    [InlineData(UpdateStage.CheckFailed)]
    [InlineData(UpdateStage.UpdateFailed)]
    public void Otherwise_there_is_no_notice(UpdateStage stage)
    {
        Assert.Null(UpdateNotice.Look(new(stage), announce: true));
    }
}
