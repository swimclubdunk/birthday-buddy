using BirthdayBuddy.Ui;

namespace BirthdayBuddy.Tests;

public class MciFanfarePlayerTests
{
    readonly List<string> _sent = [];
    Func<string, int> _answer = _ => 0;

    MciFanfarePlayer Create() => new(command =>
    {
        _sent.Add(command);
        return _answer(command);
    });

    // Review Focus 1 and 4
    [Fact]
    public void Play_closes_the_alias_then_opens_sets_volume_and_plays()
    {
        Assert.True(Create().TryPlay(@"C:\My Music\Party Horn.MP3", 0.7));
        Assert.Equal(new[]
        {
            "close bbfanfare",
            "open \"C:\\My Music\\Party Horn.MP3\" type mpegvideo alias bbfanfare",
            "setaudio bbfanfare volume to 700",
            "play bbfanfare",
        }, _sent);
    }

    [Fact]
    public void Second_play_closes_the_first()
    {
        var player = Create();
        player.TryPlay(@"C:\a.wav", 1.0);
        _sent.Clear();
        player.TryPlay(@"C:\b.wav", 1.0);
        Assert.Equal("close bbfanfare", _sent[0]);
    }

    [Fact]
    public void Open_failure_returns_false()
    {
        _answer = command => command.StartsWith("open", StringComparison.Ordinal) ? 263 : 0;
        Assert.False(Create().TryPlay(@"C:\bad.mp3", 1.0));
        Assert.DoesNotContain(_sent, c => c.StartsWith("play", StringComparison.Ordinal));
    }

    [Fact]
    public void Stop_closes_the_alias()
    {
        Create().Stop();
        Assert.Equal(new[] { "close bbfanfare" }, _sent);
    }
}
