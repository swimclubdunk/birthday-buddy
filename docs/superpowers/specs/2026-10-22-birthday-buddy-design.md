# Birthday Buddy — Design

**Date:** 2026-10-22
**Status:** Approved in conversation, pending written-spec review

## Purpose

A tiny Windows tray app. The user keeps a library of names and birthdates; Birthday Buddy quietly sits in the system tray and shows a Windows notification on each person's birthday, plus a configurable heads-up beforehand. Lightweight, elegant, simple. Shareable with friends and family as a single `.exe` that runs on any Windows 10/11 x64 PC with no install and no .NET runtime required.

## Scope

In scope:
- One user, one PC, local data only (no accounts, no sync).
- Add / edit / delete people (name, day, month, optional year).
- Day-of notification and one heads-up notification per birthday.
- One global heads-up setting (0–30 days; 0 = heads-ups off). Default 3.
- "Start with Windows" toggle, on by default.

Out of scope (YAGNI): per-person heads-up overrides, import/export, contacts sync, multiple profiles, code signing, auto-update, ARM64-native build, localization (UI is English; dates follow the system culture).

## User experience

### Tray icon
- Custom cake icon.
- Left-click opens the Birthday Buddy window.
- Right-click menu: **Open Birthday Buddy** · preview of the next 3 upcoming birthdays (disabled, display-only items) · **Start with Windows** (check item) · **Exit**.
- Tooltip: "Birthday Buddy", or "🎂 Sam's birthday today" (comma-joined names if several) when someone has a birthday today.

### Main window (~420×520, fixed size, centered)
- List of people sorted by next upcoming birthday. Each row: name, date (e.g. "24 Oct"), friendly status (**Today 🎉** / Tomorrow / in N days), and "turns N" when a year is known.
- **Add** button opens an inline editor: Name, Day, Month, optional Year. Clicking a row opens the same editor for that person, with **Delete**.
- Validation is inline (no modal errors): name required (trimmed, ≤ 60 chars); day must exist in that month, with 29 Feb allowed; year optional, between 1900 and the current year, and the full date must not be in the future.
- Settings strip at the bottom: "Remind me [N] days before" (numeric 0–30) and a **Start with Windows** checkbox.
- Closing the window hides it to the tray. Only the tray **Exit** quits.
- Empty state on first run: friendly "Add your first birthday" prompt.
- Follows the Windows light/dark app theme, including a dark title bar via DWM.

### Notifications
Shown with `NotifyIcon.ShowBalloonTip` (on Windows 10/11 this renders as a native toast in Notification Center).
- Day-of: "🎂 It's Sam's birthday today! Turning 34." ("Turning N" omitted if no year).
- Heads-up: "Sam's birthday is in 3 days (Fri 24 Oct)." ("tomorrow" when 1 day away).
- Multiple items in one check are combined into a single notification (title summarizes, body lists them).
- Clicking the notification opens the main window.

### Launch behaviour
- Single instance (named mutex). Launching a second copy signals the running instance (named event) to show its window, then exits.
- Launched with `--tray` (used by the startup registry entry): starts silently in the tray.
- Launched manually (no args): opens the window.

## Reminder rules

Let `next(p, today)` be the person's next birthday occurrence on or after `today`.

- **Feb 29 rule:** in non-leap years a 29 Feb birthday occurs on 28 Feb.
- **Day-of:** if `next(p, today) == today` and `p.lastBirthdayNotice != today`, notify and set `lastBirthdayNotice = today`.
- **Heads-up:** if `headsUpDays > 0` and `1 ≤ daysUntil ≤ headsUpDays` and `p.lastHeadsUp != next(p, today)`, notify and set `lastHeadsUp = next(p, today)`. This fires once per occurrence, the first time the app checks within the window, so a PC that was off on the exact day still gets a (shorter) heads-up, and a person added close to their birthday gets one right away.
- Missed day-of notifications on past days are not backfilled.
- **Age:** if year known, age = occurrence year − birth year.
- **Editing a person's day or month** clears both `lastHeadsUp` and `lastBirthdayNotice` for that person.

### When checks run
- On startup.
- At local midnight (one-shot timer re-armed after each check for the next midnight + 5 s).
- On resume from sleep (`SystemEvents.PowerModeChanged`, `Resume`).
- On system time / time zone change (`SystemEvents.TimeChanged`).
- After the library or heads-up setting changes (so a newly added person can be reminded immediately).

## Architecture

.NET 10, WinForms, C#, no third-party runtime dependencies.

| Unit | Responsibility | Depends on |
|---|---|---|
| `Person`, `Settings`, `AppData` | Plain data records | — |
| `BirthdayCalendar` | Pure date logic: next occurrence, days until, age, Feb 29 rule, upcoming ordering | — |
| `ReminderPlanner` | Pure: given people, settings, today → list of due reminders, and the updated sent-markers | `BirthdayCalendar` |
| `BirthdayStore` | Load/save JSON, atomic writes, corrupt-file recovery | `System.Text.Json` |
| `ReminderService` | Decides *when* to check (startup, midnight timer, resume, time change, data change); runs planner, notifies, persists markers | planner, store, notifier |
| `Notifier` | Formats and shows the combined notification; click → open window | `NotifyIcon` |
| `StartupRegistration` | HKCU `Software\Microsoft\Windows\CurrentVersion\Run` value `BirthdayBuddy` = `"<exe path>" --tray`; refreshes the path on each launch if enabled | `Microsoft.Win32.Registry` |
| `TrayApp` (`ApplicationContext`) | Owns tray icon, menu, window lifecycle, wiring | all above |
| `MainWindow`, `PersonEditor` | UI; theme-aware | store, calendar |
| `Theme` | Reads `AppsUseLightTheme`, provides palette, applies DWM dark title bar | — |
| `Log` | Appends errors to `log.txt` | — |

Clock access goes through an injectable `Func<DateOnly>` (or `TimeProvider`) so the planner and service are testable.

## Data

File: `%AppData%\BirthdayBuddy\birthdays.json`

```json
{
  "version": 1,
  "settings": { "headsUpDays": 3, "startWithWindows": true },
  "people": [
    { "id": "guid", "name": "Sam", "day": 24, "month": 10, "year": 1992,
      "lastHeadsUp": "2026-10-24", "lastBirthdayNotice": "2025-10-24" }
  ]
}
```

- `year`, `lastHeadsUp`, `lastBirthdayNotice` are nullable.
- Saves are atomic: write `birthdays.json.tmp`, then `File.Replace`/`File.Move(overwrite)`.
- Missing file → fresh defaults.

## Error handling

- Corrupt or unreadable JSON → rename to `birthdays.corrupt-<yyyyMMdd-HHmmss>.json`, start with defaults, show a notification explaining it.
- Form validation inline; never a modal error dialog for user input.
- Registry write failure → toggle reverts, inline note shown.
- Unhandled exceptions (`Application.ThreadException`, `AppDomain.UnhandledException`) and timer/event failures → logged to `%AppData%\BirthdayBuddy\log.txt`; the tray app stays alive where possible.

## Testing

xUnit project `tests/BirthdayBuddy.Tests`:
- `BirthdayCalendar`: next occurrence (same day, later this year, wraps to next year), Feb 29 in leap and non-leap years, age with/without year, upcoming ordering.
- `ReminderPlanner`: day-of fires once; heads-up fires once per occurrence; heads-up window across new year (30 Dec → 2 Jan); heads-up 0 disables; mid-window first check; no duplicates on repeated checks; date edit resets markers; multiple people combined.
- `BirthdayStore`: round trip; missing file; corrupt file recovery; atomic save leaves no `.tmp` behind.
- Manual smoke test of the published single-file exe: tray icon, window, add/edit/delete, real notification, startup toggle, second-instance activation.

## Build & distribution

- `publish.ps1` produces `dist/BirthdayBuddy.exe`: `win-x64`, self-contained, `PublishSingleFile`, `EnableCompressionInSingleFile`, `IncludeNativeLibrariesForSelfExtract`. Expected size roughly 45–60 MB. Runs on Windows 10/11 x64 (and ARM64 via emulation) with no runtime installed.
- Unsigned: first run on other PCs shows a SmartScreen prompt (More info → Run anyway).

## Project layout

```
BirthdayBuddy/
  BirthdayBuddy.sln
  src/BirthdayBuddy/          WinForms app (net10.0-windows)
  tests/BirthdayBuddy.Tests/  xUnit
  docs/superpowers/specs/
  publish.ps1
```
