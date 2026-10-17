namespace Amber.Tests;

public class StartupRegistrationTests
{
    [Fact]
    public void An_installed_copy_keeps_its_sign_in_entry_pointing_at_itself()
    {
        Assert.True(StartupRegistration.ShouldRefresh(installed: true, enabled: true));
    }

    [Fact]
    public void A_build_run_from_source_leaves_the_sign_in_entry_alone()
    {
        Assert.False(StartupRegistration.ShouldRefresh(installed: false, enabled: true));
    }

    [Fact]
    public void Nothing_is_added_while_start_with_windows_is_off()
    {
        Assert.False(StartupRegistration.ShouldRefresh(installed: true, enabled: false));
        Assert.False(StartupRegistration.ShouldRefresh(installed: false, enabled: false));
    }
}
