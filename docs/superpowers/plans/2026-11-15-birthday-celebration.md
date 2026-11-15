# Birthday Celebration Popup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the day-of birthday toast with a repeating bottom-right celebration popup stack that plays a configurable fanfare until each birthday is acknowledged.

**Architecture:** Pure, clock-driven logic in `Core` (`CelebrationPlanner`, `PresenceRules`, `CelebrationLoop`, `Fanfare`) is tested with `FakeTimeProvider` and fakes. Thin Windows wrappers in `Ui` (`UserPresence`, `MciFanfarePlayer`, `CelebrationPopup`, `CelebrationPresenter`) do the P/Invoke and drawing. `TrayApp` wires them; `ReminderService.Checked` drives `CelebrationLoop.Recheck`.

**Tech Stack:** .NET 10 WinForms (`net10.0-windows`), xUnit 2.9, `Microsoft.Extensions.TimeProvider.Testing`, P/Invoke to `winmm.dll`, `shell32.dll`, `user32.dll`.

**Spec:** `docs/superpowers/specs/2026-11-15-birthday-celebration-design.md` (read it before any task; it is the source of every rule below).

## Global Constraints

- No new NuGet packages in `src/BirthdayBuddy`. Target framework stays `net10.0-windows`.
- All timing goes through `TimeProvider` (production: `TimeProvider.System`; tests: `FakeTimeProvider` or the existing `FixedTimeProvider`).
- Timings (spec): startup check 1 s; popups visible 4 s after slide-in; repeat 15 min after the previous appearance started; pause re-check 5 s; fanfare max 30 s; button click guard 0.5 s after slide-in.
- Idle setting: `IdleMinutes` 0–60, default 5, 0 = idle check off. Volume: `FanfareVolume` 0.0–1.0 in steps of 0.1, default 1.0.
- Exact copy: button "Okay I got it!"; headline "It's {name}'s birthday!"; age line "Turns {age}"; overflow "+{n} more birthdays today" ("+1 more birthday today"); settings "Pause when idle for", "min", "Fanfare volume", "Default fanfare", "Choose…", "Default", "Test".
- Popups never take focus; one fanfare per appearance.
- Heads-up toasts keep their current timing (15 s at `--tray`, immediately otherwise).
- Every user-facing sentence lives in `Core/Wording.cs` (existing convention).
- Baseline before Task 1: `dotnet test BirthdayBuddy.slnx` → 145 passed. Every task ends with the full suite green.

## Review Focus

1. A custom sound whose path contains spaces or an uppercase extension (`C:\My Music\Party Horn.MP3`) must play: the MCI `open` command quotes the path. Test in Task 4.
2. Choosing the file that is already the current copy in `sounds\` must not delete it before copying. Test in Task 4.
3. A person deleted, or whose date is edited, while their popup is on screen must lose their popup at the next `Recheck`. Test in Task 5.
4. Two appearances close together (a new person added 2 s after an appearance) must not leave two MCI sessions open or two fanfares playing: each play closes the previous alias first. Tests in Tasks 4 and 5.
5. A hand-edited `birthdays.json` with `fanfareVolume: 7`, `idleMinutes: 500`, or a `fanfareSound` that contains a path (`..\x.mp3`) must load with clamped values and no custom sound. Test in Task 1.

---

### Task 1: Settings, acknowledgement marker and sanitizing

**Files:**
- Modify: `src/BirthdayBuddy/Core/Settings.cs`, `src/BirthdayBuddy/Core/Person.cs`, `src/BirthdayBuddy/Core/PersonRules.cs:46-54`, `src/BirthdayBuddy/Core/Library.cs`, `src/BirthdayBuddy/Core/BirthdayStore.cs` (`Sanitize`)
- Test: `tests/BirthdayBuddy.Tests/SettingsTests.cs` (new), `BirthdayStoreTests.cs`, `PersonRulesTests.cs`, `LibraryTests.cs`

**Interfaces:**
- Produces:
  - `Settings.MaxIdleMinutes = 60` (const int); `int Settings.IdleMinutes { get; init; } = 5`; `double Settings.FanfareVolume { get; init; } = 1.0`; `string? Settings.FanfareSound { get; init; }` (file name inside `sounds\`, null = bundled)
  - `static double Settings.SnapVolume(double volume)`: NaN or infinity → 1.0; otherwise clamp to [0, 1] and round to 1 decimal, `MidpointRounding.AwayFromZero`
  - `DateOnly? Person.LastBirthdayAck { get; init; }`
  - `void Library.Acknowledge(Guid personId, DateOnly today)`: sets that person's `LastBirthdayAck = today`, saves, does **not** raise `Changed` (same as `RecordSent`). Unknown id: no-op.

- [ ] **Step 1: Write the failing tests**

`SettingsTests`:
- `[Theory] SnapVolume`: `(0.74 → 0.7)`, `(0.75 → 0.8)`, `(-0.2 → 0.0)`, `(1.3 → 1.0)`, `(double.NaN → 1.0)`, `(double.PositiveInfinity → 1.0)`.
- `Defaults`: `new Settings()` has `IdleMinutes == 5`, `FanfareVolume == 1.0`, `FanfareSound == null`.

`BirthdayStoreTests`:
- `Old_file_without_new_fields_loads_defaults`: write `{"version":1,"settings":{"headsUpDays":3,"startWithWindows":true},"people":[]}` → loaded `IdleMinutes == 5`, `FanfareVolume == 1.0`, `FanfareSound == null`.
- `Sanitize_clamps_idle_and_snaps_volume`: settings `IdleMinutes = 90, FanfareVolume = 0.46` saved then loaded → `60`, `0.5`; `IdleMinutes = -3` → `0`.
- `Sanitize_drops_a_fanfare_sound_that_is_not_a_plain_file_name`: `"..\\x.mp3"`, `"C:\\x.mp3"`, `"a/b.mp3"`, `""` → `null`; `"Party Horn.MP3"` kept.
- `Ack_marker_round_trips`: person with `LastBirthdayAck = D(2026, 11, 15)` survives save/load.

`PersonRulesTests`:
- extend the existing date-change test: `LastBirthdayAck` set on the original → `null` after a day or month change; kept after a name-only change.

`LibraryTests`:
- `Acknowledge_saves_the_marker_without_raising_Changed`: Changed handler count stays 0; reloaded store has `LastBirthdayAck == today` for that id; other people untouched.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test BirthdayBuddy.slnx`
Expected: build errors for the missing members.

- [ ] **Step 3: Implement the members listed under Produces**

`Sanitize`: `IdleMinutes` clamped to `[0, MaxIdleMinutes]`; `FanfareVolume` through `SnapVolume`; `FanfareSound` kept only when non-empty and `Path.GetFileName(s) == s` and it contains no `/`. `PersonRules.Edit` clears `LastBirthdayAck` with the other markers.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test BirthdayBuddy.slnx` → all pass.

- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "feat: settings and acknowledgement marker for the celebration popup"
```

---

### Task 2: Who is due, wording, and day-of out of the toast

**Files:**
- Create: `src/BirthdayBuddy/Core/Celebration.cs`, `src/BirthdayBuddy/Core/CelebrationPlanner.cs`
- Modify: `src/BirthdayBuddy/Core/ReminderPlanner.cs`, `src/BirthdayBuddy/Core/Wording.cs`
- Test: `tests/BirthdayBuddy.Tests/CelebrationPlannerTests.cs` (new), `ReminderPlannerTests.cs`, `ReminderServiceTests.cs`, `WordingTests.cs`

**Interfaces:**
- Consumes: `Person.LastBirthdayAck` (Task 1).
- Produces:
  - `public sealed record Celebration(Guid PersonId, string Name, int? Age);`
  - `static IReadOnlyList<Celebration> CelebrationPlanner.Due(IEnumerable<Person> people, DateOnly today)`: people whose `BirthdayCalendar.NextOccurrence == today` and `LastBirthdayAck != today`, ordered by name, `StringComparer.CurrentCultureIgnoreCase`; `Age = BirthdayCalendar.AgeOn(person, today)`.
  - `Wording.AcknowledgeButton = "Okay I got it!"`, `Wording.DefaultFanfare = "Default fanfare"` (const string)
  - `static string Wording.CelebrationHeadline(string name)` → `"It's {name}'s birthday!"`
  - `static string? Wording.CelebrationAge(int? age)` → `"Turns {age}"` or `null`
  - `static string Wording.MoreBirthdays(int count)` → `"+1 more birthday today"` / `"+{count} more birthdays today"`

- [ ] **Step 1: Write the failing tests**

`CelebrationPlannerTests` (today `D(2026, 11, 15)`):
- `Due_today_and_not_acknowledged`: `P(15, 11, 1992)` → one `Celebration` with `Age == 34`.
- `Acknowledged_today_is_not_due`: `LastBirthdayAck = today` → empty.
- `Acknowledged_last_year_is_due`: `LastBirthdayAck = D(2025, 11, 15)` → one.
- `Leap_day_is_due_on_28_Feb_in_a_non_leap_year`: `P(29, 2)` on `D(2027, 2, 28)` → one.
- `Not_today_is_not_due`: `P(16, 11)` → empty.
- `Ordered_by_name_ignoring_case`: names `"bob"`, `"Alex"`, `"Cleo"` → `Alex, bob, Cleo`.

`WordingTests`: one `[Fact]` per new member with the exact strings above (including `MoreBirthdays(1)` and `MoreBirthdays(3)`, and `CelebrationAge(null) == null`).

`ReminderPlannerTests` — replace the five day-of tests (`Birthday_today_fires_with_age_and_is_marked`, `Birthday_already_noticed_today_does_not_fire_again`, `Birthday_noticed_last_year_fires_this_year`, `Birthday_today_does_not_also_send_a_heads_up`, `Leap_day_birthday_fires_on_28_Feb_in_non_leap_year`) with:
- `Birthday_today_sends_no_reminder_and_sets_no_marker`: `P(22, 10, 1992)` → `Reminders` empty; updated person has `LastBirthdayNotice == null` and `LastHeadsUp == null`.

`ReminderServiceTests`:
- `Check_notifies_once_and_persists_markers`: expect one batch with only Alex's heads-up (`Count == 1`); drop the Sam `LastBirthdayNotice` assertion.
- `Heads_up_then_the_birthday_itself_on_the_day` → rename `Heads_up_fires_but_the_day_itself_sends_no_toast`: after moving the clock to 24 Oct, `_sent` still has exactly one batch.
- Tests that use `P(22, 10)` only to provoke a reminder (`Start_*`, delay tests): switch to `P(24, 10)` and assert on `LastHeadsUp` instead of `LastBirthdayNotice`.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test BirthdayBuddy.slnx` → new tests fail to compile; updated reminder tests fail.

- [ ] **Step 3: Implement**

`ReminderPlanner.Plan`: when `days == 0`, add no reminder and leave the person unchanged. Keep `ReminderKind.Birthday` and its `Wording` branch (no longer produced; removing them is out of scope).

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test BirthdayBuddy.slnx` → all pass.

- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "feat: plan due celebrations and stop sending the day-of toast"
```

---

### Task 3: Presence rules and the Windows presence check

**Files:**
- Create: `src/BirthdayBuddy/Core/Presence.cs`, `src/BirthdayBuddy/Ui/UserPresence.cs`
- Test: `tests/BirthdayBuddy.Tests/PresenceRulesTests.cs`

**Interfaces:**
- Produces (all in `Core/Presence.cs`):
  - `public enum Presence { Show, Wait }`
  - `public enum NotificationState { Unknown = 0, NotPresent = 1, Busy = 2, RunningD3DFullScreen = 3, PresentationMode = 4, AcceptsNotifications = 5, QuietTime = 6, App = 7 }` (values match `QUERY_USER_NOTIFICATION_STATE`)
  - `public interface IUserPresence { Presence Check(int idleMinutes); }`
  - `static Presence PresenceRules.Decide(NotificationState state, TimeSpan sinceLastInput, int idleMinutes)`
- Produces (`Ui/UserPresence.cs`): `public sealed class UserPresence : IUserPresence`

- [ ] **Step 1: Write the failing test**

`PresenceRulesTests.Decide` `[Theory]` with `sinceLastInput = 1 min`, `idleMinutes = 5`:
`AcceptsNotifications → Show`, `QuietTime → Show`, `Unknown → Show`, `Busy → Wait`, `RunningD3DFullScreen → Wait`, `App → Wait`, `PresentationMode → Wait`, `NotPresent → Wait`.

`[Theory] Idle`: `(AcceptsNotifications, 5 min, 5) → Wait`, `(AcceptsNotifications, 4 min 59 s, 5) → Show`, `(AcceptsNotifications, 3 h, 0) → Show`, `(PresentationMode, 1 s, 0) → Wait`.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test BirthdayBuddy.slnx --filter PresenceRulesTests` → compile failure.

- [ ] **Step 3: Implement `PresenceRules.Decide`, then `UserPresence.Check`**

`UserPresence.Check`: `SHQueryUserNotificationState(out int state)` from `shell32.dll`; non-zero HRESULT → `Log.Error` once per failure and use `Unknown`. Idle time from `GetLastInputInfo` (`user32.dll`, `LASTINPUTINFO { uint cbSize; uint dwTime; }`) as `TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - info.dwTime))` (unsigned subtraction survives the 49.7-day wrap); if `GetLastInputInfo` fails, treat idle time as zero. Then `PresenceRules.Decide`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test BirthdayBuddy.slnx` → all pass.

- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "feat: presence rules for pausing the celebration popup"
```

---

### Task 4: Bundled fanfare, sound files and MCI playback

**Files:**
- Create: `src/BirthdayBuddy/Core/FanfareSynth.cs`, `src/BirthdayBuddy/Core/Fanfare.cs`, `src/BirthdayBuddy/Ui/MciFanfarePlayer.cs`, `tools/SoundGen/SoundGen.csproj`, `tools/SoundGen/Program.cs`, `src/BirthdayBuddy/fanfare.wav` (generated)
- Modify: `src/BirthdayBuddy/BirthdayBuddy.csproj` (embed `fanfare.wav` with `LogicalName="BirthdayBuddy.fanfare.wav"`), `BirthdayBuddy.slnx` (add `tools/SoundGen/SoundGen.csproj` under `/tools/`)
- Test: `tests/BirthdayBuddy.Tests/FanfareTests.cs`, `MciFanfarePlayerTests.cs`

**Interfaces:**
- Consumes: `Settings.FanfareVolume`, `Settings.FanfareSound` (Task 1).
- Produces:
  - `static byte[] FanfareSynth.CreateWav()`: deterministic (no randomness), 44 100 Hz, 16-bit PCM, mono, 2.5–3.5 s, peak |sample| exactly `29204` (−1 dBFS). The melody and timbre are the builder's choice (brass-like: a few harmonics with an attack/decay envelope).
  - `public interface IFanfarePlayer { bool TryPlay(string path, double volume); void Stop(); }`
  - `public interface IFanfare { void Play(); void Stop(); }`
  - `public sealed class Fanfare(IFanfarePlayer player, string dataDirectory, Func<Settings> settings) : IFanfare` with:
    - `string SoundsDirectory` = `<dataDirectory>\sounds`; `string DefaultPath` = `<dataDirectory>\default-fanfare.wav`
    - `void Play()`: uses `settings().FanfareVolume` and `settings().FanfareSound`
    - `void Play(double volume)`: same, at the given volume (the Test button)
    - `void Stop()`
    - `string Import(string sourcePath)`: returns the stored file name
    - `static byte[] BundledWav()`: reads the embedded resource
  - `public sealed class MciFanfarePlayer(Func<string, int>? send = null) : IFanfarePlayer` (default `send` = `mciSendString(cmd, null, 0, IntPtr.Zero)` from `winmm.dll`, `CharSet.Unicode`); `const string Alias = "bbfanfare"`; `static string OpenCommand(string path)` → `open "<path>" type mpegvideo alias bbfanfare`

- [ ] **Step 1: Write the failing tests**

`FanfareTests` (use `TempDirectory`; a `FakePlayer : IFanfarePlayer` that records `(path, volume)` calls and returns a configurable `bool`):
- `Synth_is_mono_16_bit_44100_and_about_three_seconds`: parse the RIFF header of `CreateWav()`; channels 1, rate 44100, bits 16; duration between 2.5 and 3.5 s.
- `Synth_peaks_at_minus_one_dBFS`: max |sample| == 29204.
- `Synth_is_deterministic`: two calls are byte-equal.
- `Bundled_resource_matches_the_synth`: `Fanfare.BundledWav()` equals `FanfareSynth.CreateWav()` (keeps the committed file in sync).
- `Play_writes_the_default_file_and_plays_it_at_the_setting_volume`: no custom sound, volume 0.6 → `default-fanfare.wav` exists with the bundled bytes; player got `(DefaultPath, 0.6)`.
- `Play_rewrites_a_default_file_that_differs`: pre-write junk bytes to `DefaultPath` → after `Play()` the file equals the bundled bytes.
- `Volume_zero_plays_nothing`: player receives no `TryPlay` call.
- `Custom_sound_is_played_from_the_sounds_directory`: `FanfareSound = "Party Horn.MP3"` and that file exists → player got `sounds\Party Horn.MP3`.
- `Missing_or_failing_custom_sound_falls_back_to_the_default`: custom file missing → player got `DefaultPath`; custom present but player returns false for it → second call with `DefaultPath`.
- `Import_copies_under_the_original_name_and_removes_the_previous_copy`: import `a.wav` then `C:\...\My Song.mp3` → returns `"My Song.mp3"`; `sounds\` contains only `My Song.mp3`.
- `Importing_the_current_copy_keeps_it` (Review Focus 2): import `x.mp3`, then import `sounds\x.mp3` itself → file still exists with the same bytes; returns `"x.mp3"`.

`MciFanfarePlayerTests` (inject a `send` that records commands and returns 0):
- `Play_closes_the_alias_then_opens_sets_volume_and_plays` (Review Focus 4): `TryPlay(@"C:\My Music\Party Horn.MP3", 0.7)` sends, in order: `close bbfanfare`, `open "C:\My Music\Party Horn.MP3" type mpegvideo alias bbfanfare`, `setaudio bbfanfare volume to 700`, `play bbfanfare`; returns true.
- `Second_play_closes_the_first`: two `TryPlay` calls → the second sequence starts with `close bbfanfare`.
- `Open_failure_returns_false`: `send` returns non-zero for `open …` → `TryPlay` false, no `play` sent.
- `Stop_closes_the_alias`.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test BirthdayBuddy.slnx` → compile failure.

- [ ] **Step 3: Implement `FanfareSynth.CreateWav`, then generate and embed the file**

Create `tools/SoundGen` as a copy of `tools/IconGen`'s csproj; `Program.cs` writes `FanfareSynth.CreateWav()` to `args[0]` (default `fanfare.wav`), mirroring `tools/IconGen/Program.cs`.
Run: `dotnet run --project tools/SoundGen -- src/BirthdayBuddy/fanfare.wav`
Expected: `Wrote …\src\BirthdayBuddy\fanfare.wav`. Add the `EmbeddedResource` line next to `app.ico`'s.

- [ ] **Step 4: Implement `Fanfare` and `MciFanfarePlayer`**

`Fanfare.Play(volume)`: `volume <= 0` → `player.Stop()` and return. Ensure `DefaultPath` holds the bundled bytes (write when missing or different). Try the custom file when `FanfareSound` is set and the file exists; on missing file or `TryPlay == false`, `Log.Error` and try `DefaultPath`. `Import`: if the source's full path is already inside `SoundsDirectory`, return its name unchanged; otherwise delete every file in `SoundsDirectory`, then copy. `MciFanfarePlayer` volume = `(int)Math.Round(volume * 1000)`.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test BirthdayBuddy.slnx` → all pass.

- [ ] **Step 6: Listen once**

Play `src/BirthdayBuddy/fanfare.wav` in Windows Media Player. Expected: a clearly audible, brass-like fanfare of about 3 s, no clicks at start or end.

- [ ] **Step 7: Commit**

```bash
git add src tests tools BirthdayBuddy.slnx
git commit -m "feat: bundled fanfare, custom sound files and MCI playback"
```

---

### Task 5: The celebration loop

**Files:**
- Create: `src/BirthdayBuddy/Core/CelebrationLoop.cs`
- Test: `tests/BirthdayBuddy.Tests/CelebrationLoopTests.cs`

**Interfaces:**
- Consumes: `Celebration` (Task 2), `IUserPresence`, `Presence` (Task 3), `IFanfare` (Task 4).
- Produces:
  ```csharp
  public sealed class CelebrationLoop(
      Func<IReadOnlyList<Celebration>> due, IUserPresence presence, Func<int> idleMinutes,
      IFanfare fanfare, TimeProvider time, SynchronizationContext context) : IDisposable
  {
      public static readonly TimeSpan StartupDelay, VisibleFor, RepeatEvery, PauseRecheck, MaxFanfare; // 1 s, 4 s, 15 min, 5 s, 30 s
      public event EventHandler<IReadOnlyList<Celebration>>? StackChanged; // the full set that should be on screen; empty = retract all
      public IReadOnlyList<Celebration> Visible { get; }
      public void Start();                    // first attempt after StartupDelay
      public void Recheck();                  // see Step 3
      public void SlideInFinished();          // starts the VisibleFor countdown unless the pointer is inside
      public void PointerEntered();           // cancels the countdown
      public void PointerLeft();              // restarts VisibleFor
      public void Acknowledge(Guid personId); // removes from Visible; Stop() the fanfare when Visible becomes empty
  }
  ```
  Timers: `time.CreateTimer`, callbacks posted to `context` (the `Debouncer` pattern).

- [ ] **Step 1: Write the failing tests**

Fixture: `FakeTimeProvider` starting `2026-11-15 09:00:00 UTC` with local zone UTC; `InlineSynchronizationContext`; a mutable `List<Person>` with `due = () => CelebrationPlanner.Due(people, DateOnly.FromDateTime(clock.GetLocalNow().DateTime))`; a list recording every `StackChanged` payload as names. Add to `tests/BirthdayBuddy.Tests/Helpers.cs` (Task 8 reuses them): `FakePresence : IUserPresence` (settable `Presence Result`, records the last `idleMinutes`) and `FakeFanfare : IFanfare` (counts `Plays` and `Stops`).

- `First_appearance_comes_one_second_after_start`: Advance 999 ms → no events; +1 ms → one event `[Sam]`, 1 play.
- `Stack_retracts_four_seconds_after_slide_in`: after appearing, `SlideInFinished()`; +3 999 ms → no new event; +1 ms → event `[]`.
- `Pointer_over_the_stack_holds_it_and_leaving_restarts_four_seconds`: `SlideInFinished`, +2 s, `PointerEntered`, +60 s → no retract; `PointerLeft`, +3 999 ms → none; +1 ms → `[]`.
- `Next_appearance_is_fifteen_minutes_after_the_previous_started`: appearance at 09:00:01 → next event `[Sam]` exactly at 09:15:01; 2 plays.
- `Spec_example_with_Sam_Alex_and_Kim` (spec "For example"): 09:00:01 `[Alex, Sam]`, 1 play; at 09:00:30 `Acknowledge(Sam)` (and set Sam's `LastBirthdayAck`) → event `[Alex]`; 09:15:01 `[Alex]`, play 2; at 10:00:00 add Kim, `Recheck()` → `[Alex, Kim]` immediately, play 3; next at 10:15:00.
- `Acknowledging_the_last_visible_person_stops_the_fanfare`: 1 `Stop` call; no further appearances after +1 h.
- `One_fanfare_per_appearance_however_many_people`: 3 due → 1 play.
- `Fanfare_is_stopped_after_thirty_seconds`: after appearing, +29 999 ms → 0 stops; +1 ms → 1 stop.
- `Waiting_presence_delays_until_clear_then_shows_immediately`: presence `Wait`; +1 s, +5 s, +5 s → no events; set `Show`; +5 s → `[Sam]` at 09:00:16; next appearance at 09:15:16.
- `Idle_minutes_are_passed_to_the_presence_check`: `idleMinutes = () => 7` → `FakePresence` saw 7.
- `Nobody_due_means_no_events_and_no_timers_left`: empty list → +1 h → no events, 0 plays.
- `Person_removed_while_visible_loses_their_popup` (Review Focus 3): `[Alex, Sam]` visible; remove Sam from the list; `Recheck()` → event `[Alex]`; no extra play.
- `Midnight_drops_yesterdays_people` (Review Focus 5): visible `[Sam]` at 23:59:58; advance to 00:00:05 next day; `Recheck()` → event `[]`; +1 h → no further events.
- `New_person_two_seconds_after_an_appearance_is_one_more_fanfare` (Review Focus 4): appearance at 09:00:01, +2 s, add Kim, `Recheck()` → event `[Kim, Sam]`, `Plays == 2` (closing the first playback is `MciFanfarePlayer`'s job, tested in Task 4); next appearance at 09:15:03.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test BirthdayBuddy.slnx --filter CelebrationLoopTests` → compile failure.

- [ ] **Step 3: Implement `CelebrationLoop`**

Rules the tests don't fully pin:
- **Attempt:** `due()` empty → cancel all timers; if `Visible` is non-empty, set it empty and raise `StackChanged([])`. Else `presence.Check(idleMinutes())`: `Wait` → schedule another attempt after `PauseRecheck`. `Show` → `Visible = due()`, add their ids to `_announced`, raise `StackChanged(Visible)`, `fanfare.Play()`, schedule `fanfare.Stop()` after `MaxFanfare`, schedule the next attempt after `RepeatEvery`, set `_nextAt = now + RepeatEvery`.
- **`_announced`:** the ids shown in any appearance on the current local date; emptied when the date differs from the date it was filled on.
- **Recheck:** attempt now when `due()` contains an id not in `_announced`, or when `now >= _nextAt` (overdue, e.g. after sleep). Otherwise, if `Visible` contains people not in `due()`, set `Visible` to the people in both and raise `StackChanged(Visible)`.
- Catch and `Log.Error` exceptions inside timer callbacks; the loop keeps running.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test BirthdayBuddy.slnx` → all pass.

- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "feat: celebration loop with repeat, pause and acknowledgement"
```

---

### Task 6: Popup size and stack layout

**Files:**
- Create: `src/BirthdayBuddy/Ui/PopupLayout.cs`
- Test: `tests/BirthdayBuddy.Tests/PopupLayoutTests.cs`

**Interfaces:**
- Produces:
  - `public sealed record PopupPlan(IReadOnlyList<Rectangle> PersonSlots, Rectangle? MoreSlot, int MoreCount);`
  - `static Size PopupLayout.PopupSize(Size screen, float scale)`: width `max(round(screen.Width / 6), round(320 * scale))`, height `max(round(screen.Height / 8), round(128 * scale))`
  - `static PopupPlan PopupLayout.Plan(Rectangle workingArea, Size popup, float scale, int people)`: margin `round(12 * scale)`, gap `round(8 * scale)`; slot 0 bottom-right of `workingArea` inset by margin; slot i moved up by `i * (popup.Height + gap)`; capacity = `max(1, (workingArea.Height - 2 * margin + gap) / (popup.Height + gap))`. `people <= capacity` → that many person slots, no more slot. Otherwise `capacity - 1` person slots, `MoreSlot` = slot `capacity - 1`, `MoreCount = people - (capacity - 1)`.

- [ ] **Step 1: Write the failing tests**

- `[Theory] PopupSize`: `(1920×1080, 1.0) → 320×135`; `(1366×768, 1.0) → 320×128`; `(2560×1440, 1.5) → 480×192`.
- `First_slot_is_bottom_right_inside_the_margin`: working area `(0, 0, 1920, 1040)`, popup 320×135, scale 1 → slot 0 = `(1588, 893, 320, 135)`.
- `Slots_stack_upward_with_an_eight_pixel_gap`: slot 1 = `(1588, 750, 320, 135)`.
- `Taskbar_on_the_left_is_respected`: working area `(48, 0, 1872, 1080)` → slot 0 x = `1588`, y = `933`.
- `Overflow_reserves_the_top_slot_for_more`: working area height 1040, 9 people → capacity 7; 6 person slots, `MoreSlot` = slot 6 = `(1588, 35, 320, 135)`, `MoreCount == 3`.
- `Exactly_capacity_needs_no_more_slot`: 7 people → 7 slots, `MoreSlot == null`.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test BirthdayBuddy.slnx --filter PopupLayoutTests` → compile failure.

- [ ] **Step 3: Implement `PopupLayout`**

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test BirthdayBuddy.slnx` → all pass.

- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "feat: popup size and stack layout"
```

---

### Task 7: The popup window

**Files:**
- Create: `src/BirthdayBuddy/Ui/CelebrationPopup.cs`, `src/BirthdayBuddy/Ui/TrumpetArt.cs`, `src/BirthdayBuddy/Ui/Confetti.cs`
- Test: `tests/BirthdayBuddy.Tests/CelebrationPopupTests.cs`

**Interfaces:**
- Consumes: `Celebration`, `Wording.*` (Task 2), `Theme`, `Shapes.RoundedRect` (existing).
- Produces:
  ```csharp
  public sealed class CelebrationPopup : Form
  {
      public static readonly TimeSpan ClickGuard;                     // 0.5 s
      public static CelebrationPopup ForPerson(Celebration person, TimeProvider time);
      public static CelebrationPopup ForMore(int count, TimeProvider time);
      public Guid? PersonId { get; }                                  // null for the "+N more" popup
      public event EventHandler? Acknowledged, MoreClicked, SlideInFinished, PointerEntered, PointerLeft;
      public void SlideIn(Rectangle target);                          // starts off-screen right, animates to target
      public void FinishAnimation();                                  // jumps a running slide to its end: SlideInFinished, or Close() + Dispose()
      public void MoveTo(Rectangle target);                           // restack
      public void SlideOut();                                         // animates right, then Close() + Dispose()
      public void SetMoreCount(int count);
  }
  ```

- [ ] **Step 1: Write the failing tests** (run inside `Sta.Run`, popups positioned at `(-32000, -32000)`)

- `Popup_never_activates_and_has_no_taskbar_button`: after `Show()`, `GetWindowLong(handle, GWL_EXSTYLE)` (P/Invoke in the test) has `WS_EX_NOACTIVATE (0x08000000)`, `WS_EX_TOOLWINDOW (0x80)` and `WS_EX_TOPMOST (0x8)`; `Form.ActiveForm` is not the popup.
- `Button_reads_Okay_I_got_it`: the person popup has one `Button` with text `"Okay I got it!"`; the more popup has none.
- `Click_is_ignored_until_half_a_second_after_slide_in`: `FakeTimeProvider`; `SlideIn(target)`, `PerformClick()` → no `Acknowledged`; `FinishAnimation()`, +499 ms, click → none; +1 ms, click → one.
- `More_popup_click_raises_MoreClicked`: invoke the protected `OnClick(EventArgs.Empty)` by reflection → one `MoreClicked`.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test BirthdayBuddy.slnx --filter CelebrationPopupTests` → compile failure.

- [ ] **Step 3: Implement the window**

`CreateParams.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST`; `ShowWithoutActivation => true`; `FormBorderStyle.None`; `ShowInTaskbar = false`; double-buffered; custom paint: rounded card, `Wording.CelebrationHeadline` large, `Wording.CelebrationAge` below (accent colour when `BirthdayCalendar.IsRoundBirthday`), two vector trumpets from `TrumpetArt` (no emoji), confetti particles from `Confetti` animated by a `System.Windows.Forms.Timer` while visible. Button: `Widgets.Button(Wording.AcknowledgeButton, Role.PrimaryButton)` anchored bottom-right. Pointer events: raise `PointerEntered`/`PointerLeft` when the cursor enters/leaves the window bounds (child controls included; check `Bounds.Contains(Cursor.Position)` on child `MouseLeave`). Colours, confetti, trumpet shapes and durations are the builder's choice (spec "Left to the builder").

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test BirthdayBuddy.slnx` → all pass.

- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "feat: celebration popup window"
```

---

### Task 8: Presenter and app wiring

**Files:**
- Create: `src/BirthdayBuddy/Ui/CelebrationPresenter.cs`
- Modify: `src/BirthdayBuddy/Ui/TrayApp.cs`
- Test: `tests/BirthdayBuddy.Tests/CelebrationPresenterTests.cs`

**Interfaces:**
- Consumes: `CelebrationLoop` (Task 5), `PopupLayout` (Task 6), `CelebrationPopup` (Task 7), `Library.Acknowledge` (Task 1), `Fanfare`, `MciFanfarePlayer` (Task 4), `UserPresence` (Task 3).
- Produces:
  ```csharp
  public sealed class CelebrationPresenter(
      CelebrationLoop loop, Library library, Func<DateOnly> today, Action openMainWindow,
      TimeProvider time, Func<(Rectangle Bounds, Rectangle WorkingArea, float Scale)>? screen = null) : IDisposable
  {
      public IReadOnlyList<CelebrationPopup> Popups { get; }   // open popups, person popups first, bottom to top
  }
  ```
  Default `screen`: `Screen.PrimaryScreen` bounds and working area; scale = effective DPI / 96 from `GetDpiForMonitor(MonitorFromPoint(bounds.Location, MONITOR_DEFAULTTOPRIMARY), MDT_EFFECTIVE_DPI, …)` (`shcore.dll`).
- `TrayApp` gains a `Fanfare` instance it later passes to `MainWindow` (Task 9).

- [ ] **Step 1: Write the failing tests** (`Sta.Run`, `FakeTimeProvider`, inline context, `FakePresence` and `FakeFanfare` from `Helpers.cs`, a real `CelebrationLoop`, `screen` returning bounds 1920×1080 and working area 1920×1040, both offset to x = −32000, scale 1)

- `Stack_change_opens_one_popup_per_person_in_the_planned_slots`: 2 due people, Advance 1 s → `Popups.Count == 2`; after `FinishAnimation()` on each, `Popups[0].Bounds` equals `PopupLayout.Plan(...).PersonSlots[0]`.
- `Acknowledge_saves_the_marker_and_closes_only_that_popup`: arm Sam's button (`FinishAnimation()`, +0.5 s), `PerformClick()` → Sam has `LastBirthdayAck == today` in `library.Data.People`; `Popups` holds only Alex's popup, moved to slot 0.
- `Overflow_shows_a_more_popup_that_opens_the_main_window`: working-area height 400 (capacity 2), 4 due → 1 person popup and 1 more popup with count 3; clicking the more popup calls `openMainWindow` once.
- `Retract_slides_every_popup_out`: after `FinishAnimation()` on each popup and +4 s, call `FinishAnimation()` on each popup again → `Popups` is empty.
- `Pointer_over_any_popup_holds_the_stack`: after slide-in, raise `PointerEntered` on popup 0; +10 s → still 2 popups; raise `PointerLeft` on popup 0; +4 s → retract started (finish animations → empty).

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test BirthdayBuddy.slnx --filter CelebrationPresenterTests` → compile failure.

- [ ] **Step 3: Implement `CelebrationPresenter`**

On `StackChanged(list)`: recompute `PopupSize` and `Plan` from `screen()` (every time: resolution, scale or taskbar may have changed). Popups whose `PersonId` is not in the shown part of `list` → `SlideOut()`. Remaining → `MoveTo(slot)`. New → `ForPerson`, `SlideIn(slot)`. More popup created, updated with `SetMoreCount`, or slid out as the plan requires. Forward the first new popup's `SlideInFinished` to `loop.SlideInFinished()` (all slide in together). Count popups with the pointer inside; 0→1 → `loop.PointerEntered()`, 1→0 → `loop.PointerLeft()`. `Acknowledged` → `library.Acknowledge(id, today())`, then `loop.Acknowledge(id)`. `MoreClicked` → `openMainWindow()`.

- [ ] **Step 4: Wire `TrayApp`**

In the constructor, after `_reminders.Start(...)`:
- `_fanfare = new Fanfare(new MciFanfarePlayer(), BirthdayStore.DefaultDirectory, () => _library.Data.Settings)`
- `_celebrations = new CelebrationLoop(() => CelebrationPlanner.Due(_library.Data.People, _reminders.Today), new UserPresence(), () => _library.Data.Settings.IdleMinutes, _fanfare, TimeProvider.System, SynchronizationContext.Current!)`
- `_presenter = new CelebrationPresenter(_celebrations, _library, () => _reminders.Today, ShowWindow, TimeProvider.System)`
- `_reminders.Checked += (_, _) => _celebrations.Recheck();`
- `_celebrations.Start();` (1 s in both launch modes; the heads-up delay line stays unchanged)

`ExitThreadCore`: dispose `_presenter`, `_celebrations`, then `_fanfare.Stop()`, before the existing disposals.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test BirthdayBuddy.slnx` → all pass.

- [ ] **Step 6: Run the app and see it**

Run: `dotnet run --project src/BirthdayBuddy`. Add a person with today's date. Expected: within 1 s, a popup slides in at the bottom-right with the fanfare; it slides out about 4 s later; clicking **Okay I got it!** after re-adding a second person closes only that popup. Typing in Notepad while it appears keeps focus in Notepad.

- [ ] **Step 7: Commit**

```bash
git add src tests
git commit -m "feat: show the celebration popups from the tray app"
```

---

### Task 9: Settings in the main window

**Files:**
- Modify: `src/BirthdayBuddy/Ui/MainWindow.cs`, `src/BirthdayBuddy/Ui/TrayApp.cs` (`ShowWindow` passes `_fanfare`)
- Test: `tests/BirthdayBuddy.Tests/MainWindowTests.cs`

**Interfaces:**
- Consumes: `Settings.IdleMinutes/FanfareVolume/FanfareSound`, `Settings.MaxIdleMinutes`, `Settings.SnapVolume` (Task 1); `Fanfare.Play(double)`, `Fanfare.Import(string)` (Task 4); `Wording.DefaultFanfare` (Task 2).
- Produces: `MainWindow(Library library, Func<bool, bool> setStartWithWindows, Func<DateOnly> today, TimeProvider? time = null, Fanfare? fanfare = null)`. With `fanfare == null`, **Choose…** and **Test** are disabled.

- [ ] **Step 1: Write the failing tests** (existing `InWindow` helper; extend it to pass an optional `Fanfare` built on the `FakePlayer`)

- `Idle_spinner_commits_once_it_settles`: the `NumericUpDown` with `Maximum == Settings.MaxIdleMinutes` set to 10; +799 ms → `IdleMinutes == 5`; +1 ms → `10`.
- `Idle_row_reads_Pause_when_idle_for_N_min`: labels `"Pause when idle for"` and `"min"` exist in the same row as that spinner.
- `Volume_slider_commits_tenths`: the `TrackBar` (`Minimum 0`, `Maximum 10`) set to 7; +800 ms → `FanfareVolume == 0.7`; value label reads `"0.7"`.
- `Default_button_clears_the_custom_sound`: settings `FanfareSound = "x.mp3"` → sound label `"x.mp3"`; click **Default** → `FanfareSound == null`, label `"Default fanfare"`.
- `Test_plays_at_the_slider_volume_before_it_is_saved`: slider to 3, click **Test** immediately → fake player got volume 0.3.
- `Footer_fits_at_minimum_size`: at `MinimumSize`, every footer control's bounds lie inside `ClientRectangle`.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test BirthdayBuddy.slnx --filter MainWindowTests` → failures.

- [ ] **Step 3: Implement**

Footer order: "Remind me [N] days before"; "Pause when idle for [N] min" (same row layout, `NumericUpDown` 0–60); "Fanfare volume" + `TrackBar` (11 positions, `TickFrequency 1`, `SmallChange 1`, `LargeChange 1`) + value label (`"0.0"` format, invariant culture); sound row: file name or `Wording.DefaultFanfare`, **Choose…** (`OpenFileDialog`, filter `Sound files (*.wav;*.mp3)|*.wav;*.mp3`; on OK `Fanfare.Import`, then `UpdateSettings` with the returned name), **Default**, **Test**; "Start with Windows". Idle and volume commits use two new `Debouncer`s (800 ms) like `_headsUpCommit`, flushed in `OnVisibleChanged` and `Dispose`. `RefreshContent` syncs them under `_syncing` and skips a control whose debouncer is pending. Raise `ClientSize` and `MinimumSize` height so the footer fits (test above).

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test BirthdayBuddy.slnx` → all pass.

- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "feat: idle, volume and sound settings in the main window"
```

---

### Task 10: README, publish and smoke test

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Update `README.md`**

Feature list: day-of birthdays now show a popup with a fanfare every 15 minutes until **Okay I got it!**; settings for idle pause, volume and sound. Develop section: `%AppData%\BirthdayBuddy\sounds\` holds a chosen sound; to regenerate the bundled sound: `dotnet run --project tools/SoundGen -- src/BirthdayBuddy/fanfare.wav`.

- [ ] **Step 2: Publish**

Run: `./publish.ps1`
Expected: tests pass, then `Built …\dist\BirthdayBuddy.exe (N MB)`.

- [ ] **Step 3: Manual smoke test of `dist/BirthdayBuddy.exe`** (spec "Testing")

Check each, and note any failure:
1. Fanfare at volume 1.0, 0.5 and 0.0 (0.0 silent, popup still shows).
2. Custom WAV and custom MP3 (one with a space in its name) play; **Default** restores the bundled sound.
3. Typing in Notepad while a popup appears: focus stays in Notepad.
4. Full-screen video (F11 in a browser) holds the popup; leaving full screen shows it at once.
5. Locked screen holds the popup; unlocking shows it at once.
6. "Pause when idle for 1 min": no input for 1 min, then a due appearance waits until the mouse moves.
7. Two people with today's birthday: two stacked popups, one fanfare; acknowledging one keeps the other.
8. Restart the app before acknowledging: the popup returns 1 s after start.

- [ ] **Step 4: Commit**

```bash
git add README.md
git commit -m "docs: describe the celebration popup and its settings"
```
