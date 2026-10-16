using MarelliIawEcuStudio.Core.Logging;
using MarelliIawEcuStudio.Core.Session;

namespace MarelliIawEcuStudio.Tests;

public class LogPlayerTests
{
    private static DataLog Log(params long[] times) => new()
    {
        Columns = [new LogColumn("PERIODE", "Engine Speed", "rpm")],
        Samples = [.. times.Select((t, i) => new LogSample(t, [i * 100.0]))],
    };

    [Fact]
    public void PlaysAtTheChosenSpeedAndStopsAtTheEnd()
    {
        var player = new LogPlayer(Log(1000, 1200, 1400, 3000));
        var finished = 0;
        player.Finished += _ => finished++;
        Assert.Equal(2000, player.DurationMs);

        player.Play();
        player.Advance(TimeSpan.FromMilliseconds(300));
        Assert.Equal(300, player.PositionMs);
        Assert.Equal(1, player.CurrentIndex);

        player.Speed = 5;
        player.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Equal(800, player.PositionMs);
        Assert.Equal(2, player.CurrentIndex);

        player.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(2000, player.PositionMs);
        Assert.False(player.IsPlaying);
        Assert.True(player.IsAtEnd);
        Assert.Equal(3, player.CurrentIndex);
        Assert.Equal(1, finished);
    }

    [Fact]
    public void PausedPlayerDoesNotMoveAndSeekIsClamped()
    {
        var player = new LogPlayer(Log(0, 100, 200));
        player.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(0, player.PositionMs);
        player.PositionMs = 150;
        Assert.Equal(1, player.CurrentIndex);
        player.PositionMs = -5;
        Assert.Equal(0, player.PositionMs);
        player.PositionMs = 999;
        Assert.Equal(200, player.PositionMs);
    }

    [Fact]
    public void PlayAtTheEndRestartsFromTheBeginning()
    {
        var player = new LogPlayer(Log(0, 100));
        player.PositionMs = 100;
        player.Play();
        Assert.Equal(0, player.PositionMs);
        Assert.True(player.IsPlaying);
    }

    [Fact]
    public void EmptyLogIsHandled()
    {
        var player = new LogPlayer(Log());
        player.Play();
        Assert.False(player.IsPlaying);
        Assert.Equal(-1, player.CurrentIndex);
        Assert.Null(player.CurrentSample);
    }

    [Fact]
    public void SpeedMustBePositive() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new LogPlayer(Log(0)).Speed = 0);

    [Fact]
    public void RateMeterCountsCyclesInTheWindow()
    {
        var meter = new RateMeter(TimeSpan.FromSeconds(2));
        for (var i = 0; i <= 10; i++)
            meter.Tick(TimeSpan.FromMilliseconds(i * 200)); // 5 Hz for 2 s
        Assert.Equal(5, meter.RatePerSecond(TimeSpan.FromMilliseconds(2000)), 1);
        Assert.Equal(0, meter.RatePerSecond(TimeSpan.FromSeconds(10)));
    }
}
