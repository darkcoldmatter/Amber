#if DEBUG
using System.IO;

namespace Amber;

/// <summary>
/// Debug builds only, to preview the update notice in the terminal build: <c>dotnet run -- --pretend-update 1.0.1</c>
/// makes Amber act as if that version were on offer, at once. Its download counts up over about three seconds and stops
/// at "Restarting…" (there is nothing to install). Add <c>--pretend-fail</c> and the first download fails part-way.
/// </summary>
public sealed class PretendUpdateServer(string version, bool failFirst, TimeSpan step) : IUpdateServer
{
    public const string Argument = "--pretend-update", FailArgument = "--pretend-fail";

    bool _failed;

    /// <summary>The pretend server the command line asks for, or null for none. <paramref name="step"/>: the wait between 2 % steps.</summary>
    public static PretendUpdateServer? FromArgs(IReadOnlyList<string> args, TimeSpan? step = null)
    {
        var at = args.ToList().IndexOf(Argument);
        if (at < 0 || at + 1 >= args.Count) return null;
        return new PretendUpdateServer(args[at + 1], args.Contains(FailArgument), step ?? TimeSpan.FromMilliseconds(60));
    }

    public Task<string?> FindNewerAsync() => Task.FromResult<string?>(version);

    public async Task DownloadAsync(Action<int> progress)
    {
        for (var percent = 0; percent <= 100; percent += 2)
        {
            progress(percent);
            if (failFirst && !_failed && percent == 40)
            {
                _failed = true;
                throw new IOException("Pretend download failed.");
            }
            await Task.Delay(step);
        }
    }

    /// <summary>Never reached: Amber stops before handing a pretend update over (App.InstallUpdate).</summary>
    public void ApplyOnExit(string[] restartArgs) { }
}
#endif
