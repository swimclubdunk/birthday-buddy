# Birthday Buddy

A tiny Windows tray app that reminds you of birthdays.

- Add people with a name and birthday (year optional).
- Get a Windows notification on the day, plus a heads-up a few days before (you choose how many; 0 turns it off).
- Starts quietly with Windows and lives in the system tray. Left-click the cake to open it.

## Build

    ./publish.ps1

Produces `dist/BirthdayBuddy.exe` — a single file that runs on any 64-bit Windows 10/11 PC, no install and no .NET needed. Because it isn't code-signed, Windows SmartScreen asks the first time: **More info → Run anyway**.

## Develop

    dotnet test
    dotnet run --project src/BirthdayBuddy

Data lives in `%AppData%\BirthdayBuddy\birthdays.json`; errors are logged to `log.txt` next to it.
To redraw the icon: `dotnet run --project tools/IconGen -- src/BirthdayBuddy/app.ico`.
