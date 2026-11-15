// Regenerates src/BirthdayBuddy/fanfare.wav from FanfareSynth.
// Usage: dotnet run --project tools/SoundGen -- src/BirthdayBuddy/fanfare.wav
using BirthdayBuddy.Core;

var output = args.Length > 0 ? args[0] : "fanfare.wav";
File.WriteAllBytes(output, FanfareSynth.CreateWav());
Console.WriteLine($"Wrote {Path.GetFullPath(output)}");
