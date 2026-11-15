using System.Runtime.InteropServices;
using BirthdayBuddy.Core;

namespace BirthdayBuddy.Ui;

/// <summary>
/// Plays WAV and MP3 through Windows' Media Control Interface (winmm.dll), which every Windows 10/11 has.
/// One alias at a time: each play closes the previous one first, so fanfares never overlap.
/// </summary>
/// <param name="send">Sends one MCI command and returns its error code (0 = success). Tests pass a fake.</param>
public sealed class MciFanfarePlayer(Func<string, int>? send = null) : IFanfarePlayer
{
    public const string Alias = "bbfanfare";

    readonly Func<string, int> _send = send ?? (command => mciSendString(command, null, 0, IntPtr.Zero));

    public static string OpenCommand(string path) => $"open \"{path}\" type mpegvideo alias {Alias}";

    public bool TryPlay(string path, double volume)
    {
        _send($"close {Alias}"); // fails harmlessly when nothing is open
        int error = _send(OpenCommand(path));
        if (error != 0)
        {
            Log.Error($"MCI could not open {path} (error {error}).");
            return false;
        }

        _send($"setaudio {Alias} volume to {(int)Math.Round(Math.Clamp(volume, 0, 1) * 1000)}");
        error = _send($"play {Alias}");
        if (error == 0) return true;

        Log.Error($"MCI could not play {path} (error {error}).");
        _send($"close {Alias}");
        return false;
    }

    public void Stop() => _send($"close {Alias}");

    [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "mciSendStringW")]
    static extern int mciSendString(string command, System.Text.StringBuilder? returnValue, int returnLength, IntPtr callback);
}
