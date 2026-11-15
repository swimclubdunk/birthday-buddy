# Birthday Buddy

A tiny Windows tray app that reminds you of birthdays.

- Add people with a name and birthday (year optional).
- On the day, a celebration popup with a fanfare appears in the bottom-right corner. It comes back every 15 minutes until you click **Okay I got it!**
- The popup waits while you're in full screen, presenting, locked, or away from the keyboard (you choose how many idle minutes count as away; 0 turns that check off).
- Set the fanfare's volume, or choose your own WAV or MP3.
- Get a Windows notification a few days before (you choose how many; 0 turns it off).
- Starts quietly with Windows and lives in the system tray. Left-click the cake to open it.

## Build

    ./publish.ps1

Produces `dist/BirthdayBuddy.exe` — a single file that runs on any 64-bit Windows 10/11 PC, no install and no .NET needed. Because it isn't code-signed, Windows SmartScreen asks the first time: **More info → Run anyway**.

## Develop

    dotnet test
    dotnet run --project src/BirthdayBuddy

Data lives in `%AppData%\BirthdayBuddy\birthdays.json`; errors are logged to `log.txt` next to it. Use **Show data file** (tray menu or window footer) to open that folder with the file selected.
A chosen fanfare is copied to `%AppData%\BirthdayBuddy\sounds\`.
To redraw the icon: `dotnet run --project tools/IconGen -- src/BirthdayBuddy/app.ico`.
To regenerate the bundled fanfare: `dotnet run --project tools/SoundGen -- src/BirthdayBuddy/fanfare.wav`.
