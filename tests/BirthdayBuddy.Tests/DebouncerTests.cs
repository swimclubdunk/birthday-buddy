using BirthdayBuddy.Core;
using Microsoft.Extensions.Time.Testing;

namespace BirthdayBuddy.Tests;

// Final review I2: holding the spinner arrow must commit (and check) once, not once per step.
public class DebouncerTests
{
    readonly FakeTimeProvider _time = new();
    int _runs;

    Debouncer Create() => new(TimeSpan.FromMilliseconds(800), () => _runs++, _time, new InlineSynchronizationContext());

    [Fact]
    public void Rapid_triggers_run_the_action_once_after_the_delay()
    {
        using var debouncer = Create();
        for (int i = 0; i < 5; i++)
        {
            debouncer.Trigger();
            _time.Advance(TimeSpan.FromMilliseconds(300));
        }
        Assert.Equal(0, _runs);
        Assert.True(debouncer.Pending);

        _time.Advance(TimeSpan.FromMilliseconds(800));
        Assert.Equal(1, _runs);
        Assert.False(debouncer.Pending);
    }

    [Fact]
    public void Flush_runs_a_pending_action_now_and_only_once()
    {
        using var debouncer = Create();
        debouncer.Trigger();
        debouncer.Flush();
        _time.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(1, _runs);
    }

    [Fact]
    public void Flush_with_nothing_pending_does_nothing()
    {
        using var debouncer = Create();
        debouncer.Flush();
        Assert.Equal(0, _runs);
    }
}
