# Birthday Buddy

A Windows tray app that reminds the owner of birthdays. See `README.md` for what it does.

## Stack and commands

- .NET 10 WinForms (`net10.0-windows`), xUnit 2.9, `Microsoft.Extensions.TimeProvider.Testing` (FakeTimeProvider). Needs the .NET 10 SDK.
- Test: `dotnet test BirthdayBuddy.slnx`
- Run: `dotnet run --project src/BirthdayBuddy`
- Build the release exe: `./publish.ps1` (runs the tests first, writes `dist/BirthdayBuddy.exe`).
- End-to-end check on the real desktop: `powershell -ExecutionPolicy Bypass -File tools/e2e.ps1` after `./publish.ps1`. Read the header of `tools/e2e.ps1` first: it refuses to run while `%AppData%\BirthdayBuddy` exists, and it moves the mouse and types for about 25 s.

## Layout

- `src/BirthdayBuddy/Core/`: logic, testable without windows. Timers take a `TimeProvider` and post to a `SynchronizationContext`.
- `src/BirthdayBuddy/Ui/`: WinForms windows, P/Invoke wrappers (`UserPresence`, `MciFanfarePlayer`) and drawing.
- `src/BirthdayBuddy/Core/Wording.cs`: every text the owner sees. Change wording there, not in the windows.
- `tools/IconGen`, `tools/SoundGen`: generate `app.ico` and the bundled `fanfare.wav` (both committed).
- `docs/superpowers/specs/`: approved designs. `docs/superpowers/plans/`: the implementation plans.

## Conventions

- Test first: write the failing test, watch it fail, then implement. Run the whole suite before committing.
- Tests with FakeTimeProvider step the clock in 1 s increments: a timer created inside a timer callback counts from the end of a larger jump.
- `CelebrationPopup` tests: `Button.PerformClick` does nothing on its non-focusable button, so tests call `Control.OnClick` through reflection; `Form.DrawToBitmap` hangs on the popup, so tests call `OnPaint` through reflection.
- Commit on a feature branch, never directly on `main`.

## Current state (15 November 2026)

- Branch `feat/celebration-popup` holds the celebration popup feature (spec: `docs/superpowers/specs/2026-11-15-birthday-celebration-design.md`). It is pushed to GitHub, all 251 tests pass and `tools/e2e.ps1` passed on the exe. It is not merged into `main` and has no pull request yet.
- Open decision: open a pull request, or merge into `main` directly.
- Not yet checked by a person at the PC:
  1. How the bundled fanfare sounds at full volume.
  2. Choosing a custom MP3 and a custom WAV with **Choose…**, then **Test** and **Default**.
  3. The popup waits during full screen, a presentation, a locked screen, and after the minutes set in **Pause when idle for** without input. It shows as soon as the wait ends.
  4. The popup comes back after 15 minutes in real time until **Okay I got it!** is clicked.
