# Birthday Buddy — Birthday celebration popup

**Date:** 15 November 2026
**Status:** Approved by the owner on 15 November 2026
**Builds on:** `2026-10-22-birthday-buddy-design.md`

## Intent

The owner must not miss a birthday. Today the day-of reminder is one Windows toast, which is easy to overlook. This change replaces that toast with a celebration popup in the bottom-right corner of the screen. The popup plays a loud fanfare and keeps coming back every 15 minutes until the owner clicks **Okay I got it!** for that person.

Being annoying is the point: the popup and the fanfare repeat until acknowledged.

## Goals

- A celebration check 1 second after the app starts.
- One popup per person whose birthday is today and not yet acknowledged, with confetti, trumpets and an **Okay I got it!** button.
- A loud fanfare on every appearance. Volume is configurable from 0.0 to 1.0 in steps of 0.1.
- A bundled fanfare, replaceable by a WAV or MP3 file the owner chooses.

## Non-goals

- Changing heads-up reminders. They stay Windows toasts, with the existing timing and rules.
- Detecting screen sharing (Teams, Zoom). Windows doesn't report it; the popup and fanfare appear during a screen share.
- Detecting Do Not Disturb / Focus. Windows doesn't report it to apps, and the owner wants the popup to appear during Do Not Disturb anyway.
- Multi-monitor placement. The popup uses the primary screen only.
- A popup preview button. Adding a person whose birthday is today triggers a real appearance.

## Anti-goals

- The popup must never take keyboard focus. The owner may be typing when it appears.
- Overlapping fanfares. One appearance plays the fanfare once, however many popups it shows.
- A popup that appears while nobody can see it (screen locked, screensaver, no input for `IdleMinutes`) and then counts as shown. For example: a PC left on and unlocked overnight must not play the fanfare every 15 minutes until morning.

## Constraints

- .NET 10 WinForms, `net10.0-windows`, no third-party runtime packages (unchanged from the base design).
- Distributed as one self-contained `.exe` (`publish.ps1`); the change must not require changing the target framework.
- Volume 1.0 is the app's full volume. It can't exceed the Windows volume; a muted PC plays nothing.
- At login (`--tray`), the heads-up check keeps its 15-second delay. That delay is a technical requirement: a toast sent before the tray area is ready can be dropped while its "sent" marker is still saved (commit `dc63495`).

## Approaches

### Sound playback (chosen: MCI)

| Approach | Plays | Volume | Cost |
|---|---|---|---|
| **MCI (`winmm.dll` `mciSendString`, `mpegvideo` device)** | WAV and MP3 | `setaudio … volume to 0–1000` | P/Invoke only; built into every Windows 10/11. Plays only from a file on disk. |
| WinRT `Windows.Media.Playback.MediaPlayer` | WAV, MP3, more | `Volume` 0.0–1.0 | Target framework must become `net10.0-windows10.0.19041.0`; the `.exe` grows by the WinRT projection. |
| `System.Media.SoundPlayer` with the samples scaled in memory | WAV only | Sample scaling | Rejected: the owner requires MP3. |

MCI is chosen: it covers both formats without changing the framework or the size of the `.exe`.

### Popup (chosen: custom WinForms window)

| Approach | Trade-off |
|---|---|
| **Custom borderless WinForms window** | Full control over size, position, animation, repeat and sound. |
| Windows toast | Rejected: no control over size or position, no confetti, its own sound at the Windows notification volume, and it follows Do Not Disturb, which the owner doesn't want. |

## Behaviour

### Terms

- **Due person:** a person whose next birthday occurrence is today and whose `LastBirthdayAck` is not today. The Feb 29 rule from the base design applies.
- **Appearance:** one showing of the popup stack: all popups slide in, the fanfare plays once, and the popups slide out.
- **Acknowledge:** the owner clicks **Okay I got it!** on one popup.
- **Blocking state:** a Windows state in which an appearance must wait (see Pause rules).

### Schedule

1. 1 second after `TrayApp` finishes starting (both launch modes), the app checks for due people. If any exist, an appearance starts.
2. An appearance shows one popup per due person, plays the fanfare once, and keeps the popups on screen for 4 seconds after they finish sliding in. Then all popups slide out together.
3. While the mouse pointer is over any popup in the stack, the stack stays on screen. The 4-second countdown restarts when the pointer leaves the stack.
4. The next appearance is due 15 minutes after the previous appearance started.
5. Acknowledging a popup:
   - sets `LastBirthdayAck = today` for that person and saves `birthdays.json`;
   - closes that popup immediately; the remaining popups move down to close the gap;
   - stops the fanfare if no popup is left on screen.
6. The loop stops when no due people remain. It starts again when a new due person appears.
7. When a new due person appears during the day (the owner adds a person, edits a date, or local midnight passes), an appearance starts immediately with all due people, and the 15-minute countdown restarts from that appearance.
8. At local midnight, people whose birthday was yesterday stop being due, acknowledged or not.
9. If the app restarts before a person is acknowledged, that person is still due, and the first appearance happens at the 1-second startup check.

For example: Sam's and Alex's birthdays are today. At 09:00:01 both popups appear and the fanfare plays. At 09:00:30 the owner acknowledges Sam. At 09:15:01 only Alex's popup appears, with the fanfare. At 10:00 the owner adds Kim (birthday today): Alex and Kim appear immediately, and the next appearance is at 10:15.

### Pause rules

Before each appearance, the app calls `SHQueryUserNotificationState`:

| Result | Windows state | Rule |
|---|---|---|
| `QUNS_ACCEPTS_NOTIFICATIONS` | Normal | Show |
| `QUNS_QUIET_TIME` | First hour after the first sign-in on a new Windows install | Show |
| `QUNS_BUSY` | Full-screen app (video, browser in F11) | Wait |
| `QUNS_RUNNING_D3D_FULL_SCREEN` | Full-screen game | Wait |
| `QUNS_APP` | Full-screen Store app | Wait |
| `QUNS_PRESENTATION_MODE` | Presentation mode (PowerPoint slideshow, presentation settings) | Wait |
| `QUNS_NOT_PRESENT` | Screen locked, screensaver, or another user's session active | Wait |
| Call fails | Unknown | Show, and log the failure |

The app also checks how long ago the last keyboard or mouse input was (`GetLastInputInfo`):

| Last input | Rule |
|---|---|
| `IdleMinutes` minutes ago or longer | Wait |
| Less than `IdleMinutes` minutes ago | Apply the table above |
| `IdleMinutes` is 0 | Idle check off; apply the table above |

`IdleMinutes` is a setting, default 5 (see Settings). Watching a video in a normal (not full-screen) window without touching keyboard or mouse counts as idle after `IdleMinutes`; the owner accepted this.

- While an appearance waits, the app re-checks the state every 5 seconds. As soon as no blocking state remains, the appearance starts immediately, and the 15-minute countdown restarts from it.
- A blocking state that starts while popups are on screen doesn't remove them; they slide out on their normal schedule.
- After sleep, the existing resume handler runs the celebration check. An appearance that became due during sleep starts immediately, subject to the pause rules (usually after the owner unlocks the screen).

### Fanfare

- Plays at the start of each appearance, at `FanfareVolume`. Volume 0.0 plays nothing.
- One fanfare per appearance; a new appearance stops any fanfare still playing before it starts its own.
- The fanfare plays to its end, up to 30 seconds, even after the popups slide out. Acknowledging the last popup on screen stops it.
- The sound file is the owner's chosen file, or the bundled fanfare when none is chosen. If the chosen file is missing or can't be played, the app logs the error and plays the bundled fanfare.

## Popup

### Size and position

- Width: the larger of 1/6 of the primary screen's width and 320 px × display scale.
- Height: the larger of 1/8 of the primary screen's height and 128 px × display scale.
- Display scale is the primary screen's Windows scale setting (1.0 at 100%, 1.5 at 150%).
- For example: 320×135 px on 1920×1080 at 100%; 320×128 px on 1366×768 at 100%; 480×192 px on 2560×1440 at 150%.
- The first popup sits at the bottom-right of the primary screen's working area (above the taskbar), 12 px × display scale from both edges. Further popups stack upward, 8 px × display scale apart.
- Stack order, bottom to top: the existing order of `BirthdayCalendar.Upcoming` (by name, case-insensitive, current culture).
- When the working area can't hold all popups, the topmost slot shows a "+N more birthdays today" popup instead, where N is the number of due people not shown. That popup has no **Okay I got it!** button; clicking it opens the main window. Acknowledging a shown popup frees a slot for the next due person at the next appearance.

### Window behaviour

- Borderless, always on top, no taskbar button (`WS_EX_TOOLWINDOW`), never activated (`WS_EX_NOACTIVATE`, `ShowWithoutActivation`). The **Okay I got it!** button works without the popup taking focus.
- Slides in from the right edge and slides out to the right edge.
- The **Okay I got it!** button ignores clicks until 0.5 seconds after the slide-in ends, so a click aimed at another app's button in the same corner can't acknowledge a birthday the owner hasn't seen.

### Content

- The person's name, large: "It's Sam's birthday!".
- "Turns 34" when the birth year is known. Round ages (`BirthdayCalendar.IsRoundBirthday`: multiples of 10) get the accent highlight the main window's people list already uses.
- Animated confetti across the popup for as long as it is on screen.
- Two trumpets, drawn as vector shapes (GDI+ draws emoji in one colour, so emoji are not used for the art).
- The **Okay I got it!** button, bottom-right, in the theme's accent colour.

## Settings

New controls in the main window's footer, below "Remind me N days before":

- **Pause when idle for [N] min:** a number box from 0 to 60, default 5; 0 turns the idle check off. Saved with the existing `Debouncer`, like the heads-up spinner, and laid out on one row the same way as "Remind me N days before".
- **Fanfare volume:** a slider from 0.0 to 1.0 with 11 positions (steps of 0.1), showing the current value. Saved once the slider settles, with the existing `Debouncer` used by the heads-up spinner.
- **Fanfare sound:** the name of the chosen file, or "Default fanfare"; buttons **Choose…** (WAV and MP3 files), **Default** (back to the bundled fanfare) and **Test** (plays the current sound at the current slider volume).
- **Choose…** copies the file, under its original name, into `%AppData%\BirthdayBuddy\sounds\`, after deleting the previous copy. Moving or deleting the original doesn't break the setting, and the footer shows the original name (for example "trumpets.mp3").

## Data

`birthdays.json` gains four optional fields; `version` stays 1:

```json
{
  "settings": { "headsUpDays": 3, "startWithWindows": true, "idleMinutes": 5,
                "fanfareVolume": 1.0, "fanfareSound": "trumpets.mp3" },
  "people": [ { "...": "...", "lastBirthdayAck": "2026-11-15" } ]
}
```

- `idleMinutes`: default 5. On load, `BirthdayStore.Sanitize` clamps it to 0–60.
- `fanfareVolume`: default 1.0. On load, `BirthdayStore.Sanitize` clamps it to 0.0–1.0 and rounds it to one decimal.
- `fanfareSound`: a file name inside `%AppData%\BirthdayBuddy\sounds\`, or null for the bundled fanfare.
- `lastBirthdayAck`: the date the owner acknowledged this person's birthday popup. Editing a person's day or month clears it, as already happens for the other markers.
- `lastBirthdayNotice` is no longer written. Existing values are kept on load and ignored.
- Files written by the current version load with the defaults above.

## Changes to existing parts

| Part | Change |
|---|---|
| `ReminderPlanner` | No longer produces day-of reminders. Heads-ups unchanged. |
| `ReminderService` | Unchanged timing for heads-ups (15 s at `--tray`, immediately otherwise). Also triggers the celebration check on its existing occasions (midnight, resume, time change, library change). |
| `Notifier`, `Wording.Notification` | Only heads-ups reach them. |
| `TrayApp` | Creates the celebration parts and starts the 1-second check. Tray tooltip unchanged. |
| `MainWindow` | Footer gains the settings above; minimum window height grows to fit them. |

## New parts

| Part | Responsibility |
|---|---|
| `Core/CelebrationPlanner` | Pure: people + today → due people, in stack order. |
| `Core/CelebrationLoop` | The schedule and pause rules above, driven by `TimeProvider`. Raises "show these people" and "retract". Depends on the presence and sound interfaces, so tests use fakes. |
| `Core/IUserPresence` + `Ui/UserPresence` | Wraps `SHQueryUserNotificationState` and `GetLastInputInfo`; given `IdleMinutes`, returns Show or Wait. |
| `Core/IFanfarePlayer` + `Ui/MciFanfarePlayer` | Play at a volume, stop. Writes the bundled fanfare to `%AppData%\BirthdayBuddy\default-fanfare.wav` when the file is missing or differs, because MCI only plays files. |
| `Ui/CelebrationPopup` | One popup window: drawing, confetti animation, slide in and out, button. |
| `Ui/PopupStack` | Size and position calculation (pure, testable) and the "+N more" popup. |
| `tools/SoundGen` | Generates the bundled fanfare: a brass-like synthesized fanfare of about 3 seconds, 44.1 kHz 16-bit WAV, peak-normalised to −1 dBFS. Embedded in the app as a resource, like `app.ico`. |

## Error handling

- Sound failure (MCI error, missing file): log to `log.txt`, fall back to the bundled fanfare; if that fails too, show the popups without sound.
- `birthdays.json` read-only (the existing `Unavailable` state): acknowledgements are kept in memory only, so after a restart the person is due again.
- Exceptions in the loop or the popups are logged; the tray app stays alive.

## Testing

- `CelebrationPlanner`: due today; acknowledged today; acknowledged last year; Feb 29 in a non-leap year; edit clears the acknowledgement.
- `CelebrationLoop` with a fake clock, presence and player: first appearance at 1 s; retract at 4 s; hover holds the stack; next appearance at 15 min; acknowledge removes one person and stops the fanfare when none remain; new due person starts an appearance and restarts the countdown; each blocking state, including idle for `IdleMinutes` (and not with `IdleMinutes` = 0), delays the appearance until it clears, then shows immediately; one fanfare per appearance; midnight drops yesterday's people.
- `PopupStack`: the three size examples above; stacking; "+N more" overflow.
- `Settings`: volume rounding and clamping; `IdleMinutes` clamping; loading a file without the new fields.
- Manual smoke test of the published `.exe`: real fanfare at several volumes, custom WAV and MP3, popup never takes focus while typing, full-screen video and locked screen hold the popup, popups stack for two people.

## Designer's calls

The owner decided: 1-second startup check, 15-second heads-up delay kept, 4-second display, 15-minute repeat, fanfare on every appearance, one fanfare per appearance, one popup and button per person, WAV and MP3, volume 0.0–1.0 in steps of 0.1, full-screen and presentation mode pause, Do Not Disturb ignored, a paused appearance shows immediately when the pause ends, the loop pauses after 5 minutes without keyboard or mouse input, and those minutes are a setting with a concise label.

These were proposed by the designer and accepted without objection or not yet reviewed; the owner may change them:

- Locked screen and screensaver also pause.
- Hover holds the stack; the 4-second countdown restarts when the pointer leaves.
- After a restart, unacknowledged people are due again.
- Heads-ups stay toasts.
- "+N more" popup on overflow.
- Minimum size 320×128 px × display scale; 12 px edge margin, 8 px gap.
- Default volume 1.0.
- Re-check every 5 seconds while paused.
- The fanfare plays up to 30 seconds; acknowledging the last popup on screen stops it.
- Primary screen only; no popup preview button.
- Bundled fanfare synthesized by `tools/SoundGen` rather than a licensed recording.
- The **Okay I got it!** button ignores clicks for 0.5 seconds after slide-in; no undo for an acknowledgement.
- A chosen sound file keeps its original name in `sounds\`.
- Idle setting label "Pause when idle for [N] min", range 0–60, 0 turns the idle check off.

## Left to the builder

- Colours, fonts, confetti density and motion, trumpet drawing, and slide durations, within the content rules above.
- The fanfare's composition, within the format above.
- Class and method names beyond those listed, and how the loop's events reach the UI thread.
