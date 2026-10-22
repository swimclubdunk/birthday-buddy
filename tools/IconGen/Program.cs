// Regenerates src/BirthdayBuddy/app.ico from IconArt.
// Usage: dotnet run --project tools/IconGen -- src/BirthdayBuddy/app.ico
using BirthdayBuddy.Ui;

var output = args.Length > 0 ? args[0] : "app.ico";
int[] sizes = [16, 20, 24, 32, 40, 48, 64, 256];

var images = sizes.Select(IconArt.Draw).ToList();
using (var file = File.Create(output)) IcoWriter.Write(file, images);
foreach (var image in images) image.Dispose();

Console.WriteLine($"Wrote {Path.GetFullPath(output)}");
