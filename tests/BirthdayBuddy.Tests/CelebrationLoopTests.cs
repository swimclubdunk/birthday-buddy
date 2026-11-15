using BirthdayBuddy.Core;
using Microsoft.Extensions.Time.Testing;
using static BirthdayBuddy.Tests.TestData;

namespace BirthdayBuddy.Tests;

public class CelebrationLoopTests : IDisposable
{
    readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 15, 9, 0, 0, TimeSpan.Zero));
    readonly List<Person> _people = [];
    readonly FakePresence _presence = new();
    readonly FakeFanfare _fanfare = new();
    readonly List<string[]> _stacks = [];
    int _idle = 5;
    CelebrationLoop _loop = null!;

    public CelebrationLoopTests() => _clock.SetLocalTimeZone(TimeZoneInfo.Utc);

    public void Dispose() => _loop?.Dispose();

    DateOnly Today => DateOnly.FromDateTime(_clock.GetLocalNow().DateTime);

    string[] Last => _stacks[^1];

    CelebrationLoop Start(params string[] names)
    {
        foreach (var name in names) Add(name);
        _loop = new CelebrationLoop(() => CelebrationPlanner.Due(_people, Today), _presence, () => _idle, _fanfare,
            _clock, new InlineSynchronizationContext());
        _loop.StackChanged += (_, stack) => _stacks.Add(stack.Select(c => c.Name).ToArray());
        _loop.Start();
        return _loop;
    }

    void Add(string name) => _people.Add(P(15, 11, name: name));

    Guid Id(string name) => _people.Single(p => p.Name == name).Id;

    /// <summary>What the presenter does: save the marker, then tell the loop.</summary>
    void Acknowledge(string name)
    {
        int i = _people.FindIndex(p => p.Name == name);
        _people[i] = _people[i] with { LastBirthdayAck = Today };
        _loop.Acknowledge(_people[i].Id);
    }

    // FakeTimeProvider moves the clock first and fires due timers after, so a timer created inside a callback
    // counts from the end of the jump. Stepping in seconds keeps chained timers (every 15 minutes) on time.
    void Advance(double milliseconds)
    {
        var left = TimeSpan.FromMilliseconds(milliseconds);
        while (left > TimeSpan.Zero)
        {
            var step = left < TimeSpan.FromSeconds(1) ? left : TimeSpan.FromSeconds(1);
            _clock.Advance(step);
            left -= step;
        }
    }

    void AdvanceTo(int day, int hour, int minute, int second) =>
        Advance((new DateTimeOffset(2026, 11, day, hour, minute, second, TimeSpan.Zero) - _clock.GetUtcNow()).TotalMilliseconds);

    [Fact]
    public void First_appearance_comes_one_second_after_start()
    {
        Start("Sam");
        Advance(999);
        Assert.Empty(_stacks);
        Advance(1);
        Assert.Equal(new[] { "Sam" }, Assert.Single(_stacks));
        Assert.Equal(1, _fanfare.Plays);
    }

    [Fact]
    public void Recheck_before_the_startup_delay_waits_for_it()
    {
        Start("Sam").Recheck();
        Assert.Empty(_stacks);
        Advance(1000);
        Assert.Single(_stacks);
    }

    [Fact]
    public void Stack_retracts_four_seconds_after_slide_in()
    {
        var loop = Start("Sam");
        Advance(1000);
        loop.SlideInFinished();
        Advance(3999);
        Assert.Single(_stacks);
        Advance(1);
        Assert.Empty(Last);
    }

    [Fact]
    public void Pointer_over_the_stack_holds_it_and_leaving_restarts_four_seconds()
    {
        var loop = Start("Sam");
        Advance(1000);
        loop.SlideInFinished();
        Advance(2000);
        loop.PointerEntered();
        Advance(60_000);
        Assert.Single(_stacks);
        loop.PointerLeft();
        Advance(3999);
        Assert.Single(_stacks);
        Advance(1);
        Assert.Empty(Last);
    }

    [Fact]
    public void Next_appearance_is_fifteen_minutes_after_the_previous_started()
    {
        var loop = Start("Sam");
        Advance(1000);
        loop.SlideInFinished();
        AdvanceTo(15, 9, 15, 0);
        Assert.Equal(1, _fanfare.Plays);
        Advance(1000);
        Assert.Equal(new[] { "Sam" }, Last);
        Assert.Equal(2, _fanfare.Plays);
    }

    // The spec's "For example", with the hover that keeps the stack up until 09:00:30.
    [Fact]
    public void Spec_example_with_Sam_Alex_and_Kim()
    {
        var loop = Start("Sam", "Alex");
        Advance(1000);
        Assert.Equal(new[] { "Alex", "Sam" }, Last);
        loop.SlideInFinished();
        loop.PointerEntered();

        AdvanceTo(15, 9, 0, 30);
        Acknowledge("Sam");
        Assert.Equal(new[] { "Alex" }, Last);
        loop.PointerLeft();

        AdvanceTo(15, 9, 15, 1);
        Assert.Equal(new[] { "Alex" }, Last);
        Assert.Equal(2, _fanfare.Plays);

        AdvanceTo(15, 10, 0, 0); // 09:30:01 and 09:45:01 showed Alex too
        Assert.Equal(4, _fanfare.Plays);
        Add("Kim");
        loop.Recheck();
        Assert.Equal(new[] { "Alex", "Kim" }, Last);
        Assert.Equal(5, _fanfare.Plays);

        AdvanceTo(15, 10, 14, 59);
        Assert.Equal(5, _fanfare.Plays);
        Advance(1000);
        Assert.Equal(6, _fanfare.Plays);
    }

    [Fact]
    public void Acknowledging_the_last_visible_person_stops_the_fanfare()
    {
        Start("Sam");
        Advance(1000);
        Acknowledge("Sam");
        Assert.Empty(Last);
        Assert.Equal(1, _fanfare.Stops);
        Advance(3_600_000);
        Assert.Equal(1, _fanfare.Plays);
    }

    [Fact]
    public void One_fanfare_per_appearance_however_many_people()
    {
        Start("Alex", "Kim", "Sam");
        Advance(1000);
        Assert.Equal(3, Last.Length);
        Assert.Equal(1, _fanfare.Plays);
    }

    [Fact]
    public void Fanfare_is_stopped_after_thirty_seconds()
    {
        Start("Sam");
        Advance(1000);
        Advance(29_999);
        Assert.Equal(0, _fanfare.Stops);
        Advance(1);
        Assert.Equal(1, _fanfare.Stops);
    }

    [Fact]
    public void Waiting_presence_delays_until_clear_then_shows_immediately()
    {
        _presence.Result = Presence.Wait;
        Start("Sam");
        Advance(1000);
        Advance(5000);
        Advance(5000);
        Assert.Empty(_stacks);

        _presence.Result = Presence.Show;
        Advance(5000);
        Assert.Equal(new[] { "Sam" }, Assert.Single(_stacks)); // 09:00:16

        AdvanceTo(15, 9, 15, 15);
        Assert.Equal(1, _fanfare.Plays);
        Advance(1000);
        Assert.Equal(2, _fanfare.Plays);
    }

    [Fact]
    public void Idle_minutes_are_passed_to_the_presence_check()
    {
        _idle = 7;
        Start("Sam");
        Advance(1000);
        Assert.Equal(7, _presence.LastIdleMinutes);
    }

    [Fact]
    public void Nobody_due_means_no_events()
    {
        Start();
        Advance(3_600_000);
        Assert.Empty(_stacks);
        Assert.Equal(0, _fanfare.Plays);
    }

    // Review Focus 3
    [Fact]
    public void Person_removed_while_visible_loses_their_popup()
    {
        var loop = Start("Sam", "Alex");
        Advance(1000);
        _people.RemoveAll(p => p.Name == "Sam");
        loop.Recheck();
        Assert.Equal(new[] { "Alex" }, Last);
        Assert.Equal(1, _fanfare.Plays);
    }

    [Fact]
    public void Recheck_with_nothing_new_changes_nothing()
    {
        var loop = Start("Sam");
        Advance(1000);
        loop.Recheck();
        Assert.Single(_stacks);
        Assert.Equal(1, _fanfare.Plays);
    }

    [Fact]
    public void Midnight_drops_yesterdays_people()
    {
        var loop = Start("Sam");
        AdvanceTo(16, 0, 0, 5);
        Assert.Empty(Last);
        loop.Recheck();
        int events = _stacks.Count;
        Advance(3_600_000);
        Assert.Equal(events, _stacks.Count);
    }

    // Review Focus 4
    [Fact]
    public void New_person_two_seconds_after_an_appearance_is_one_more_fanfare()
    {
        var loop = Start("Sam");
        Advance(1000);
        Advance(2000);
        Add("Kim");
        loop.Recheck();
        Assert.Equal(new[] { "Kim", "Sam" }, Last);
        Assert.Equal(2, _fanfare.Plays);

        AdvanceTo(15, 9, 15, 2);
        Assert.Equal(2, _fanfare.Plays);
        Advance(1000);
        Assert.Equal(3, _fanfare.Plays);
    }

    [Fact]
    public void Visible_follows_the_stack()
    {
        var loop = Start("Sam");
        Advance(1000);
        Assert.Equal(Id("Sam"), Assert.Single(loop.Visible).PersonId);
    }

    // Final review M2: a stale "pointer inside" from the previous stack must not hold the next one.
    [Fact]
    public void New_appearance_forgets_a_stale_pointer()
    {
        var loop = Start("Sam");
        Advance(1000);
        loop.SlideInFinished();
        Advance(4000);
        Assert.Empty(Last);
        loop.PointerEntered(); // reported by a popup that was already sliding out

        AdvanceTo(15, 9, 15, 1);
        Assert.Equal(new[] { "Sam" }, Last);
        loop.SlideInFinished();
        Advance(4000);
        Assert.Empty(Last);
    }
}
