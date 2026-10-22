using System.Diagnostics;
using BirthdayBuddy.Core;

namespace BirthdayBuddy.Ui;

public static class DataFile
{
    /// <summary>Opens Explorer with the file selected, or at its folder if the file isn't written yet.
    /// Returns false (and logs) if Explorer couldn't be started.</summary>
    public static bool Reveal(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                return true;
            }
            var folder = Path.GetDirectoryName(filePath)!;
            Directory.CreateDirectory(folder);
            Process.Start("explorer.exe", $"\"{folder}\"");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Could not open the data file location.", ex);
            return false;
        }
    }
}
