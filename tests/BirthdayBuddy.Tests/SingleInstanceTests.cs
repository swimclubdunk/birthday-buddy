using BirthdayBuddy.Core;

namespace BirthdayBuddy.Tests;

public class SingleInstanceTests
{
    static string Unique(string kind) => $@"Local\BirthdayBuddyTests.{kind}.{Guid.NewGuid():N}";

    [Fact]
    public void Second_acquire_fails_until_the_first_is_released()
    {
        string mutex = Unique("mutex"), signal = Unique("event");

        using (var first = SingleInstance.TryAcquire(mutex, signal))
        {
            Assert.NotNull(first);
            Assert.Null(SingleInstance.TryAcquire(mutex, signal));
        }

        using var again = SingleInstance.TryAcquire(mutex, signal);
        Assert.NotNull(again);
    }

    [Fact]
    public void Signal_reaches_the_running_instance()
    {
        string mutex = Unique("mutex"), signal = Unique("event");
        using var first = SingleInstance.TryAcquire(mutex, signal)!;
        using var activated = new ManualResetEventSlim();

        first.OnActivationRequested(activated.Set, new SynchronizationContext());
        SingleInstance.SignalExisting(signal);

        Assert.True(activated.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void Signal_with_no_running_instance_is_harmless() =>
        SingleInstance.SignalExisting(Unique("event"));
}
