using System.Text;
using BirthdayBuddy.Core;

namespace BirthdayBuddy.Tests;

public class FanfareTests : IDisposable
{
    readonly TempDirectory _dir = new();
    readonly FakePlayer _player = new();
    Settings _settings = new();

    public void Dispose() => _dir.Dispose();

    Fanfare Create() => new(_player, _dir.Path, () => _settings);

    static (int Channels, int Rate, int Bits, int DataBytes, short[] Samples) ParseWav(byte[] wav)
    {
        Assert.Equal("RIFF", Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal("WAVE", Encoding.ASCII.GetString(wav, 8, 4));
        Assert.Equal("fmt ", Encoding.ASCII.GetString(wav, 12, 4));
        Assert.Equal(1, BitConverter.ToInt16(wav, 20)); // PCM
        int channels = BitConverter.ToInt16(wav, 22), rate = BitConverter.ToInt32(wav, 24), bits = BitConverter.ToInt16(wav, 34);
        Assert.Equal("data", Encoding.ASCII.GetString(wav, 36, 4));
        int dataBytes = BitConverter.ToInt32(wav, 40);
        var samples = new short[dataBytes / 2];
        Buffer.BlockCopy(wav, 44, samples, 0, dataBytes);
        return (channels, rate, bits, dataBytes, samples);
    }

    [Fact]
    public void Synth_is_mono_16_bit_44100_and_about_three_seconds()
    {
        var wav = ParseWav(FanfareSynth.CreateWav());
        Assert.Equal(1, wav.Channels);
        Assert.Equal(44_100, wav.Rate);
        Assert.Equal(16, wav.Bits);
        Assert.InRange(wav.DataBytes / 2.0 / wav.Rate, 2.5, 3.5);
    }

    [Fact]
    public void Synth_peaks_at_minus_one_dBFS() =>
        Assert.Equal(29_204, ParseWav(FanfareSynth.CreateWav()).Samples.Max(s => Math.Abs((int)s)));

    [Fact]
    public void Synth_is_deterministic() => Assert.Equal(FanfareSynth.CreateWav(), FanfareSynth.CreateWav());

    [Fact]
    public void Bundled_resource_matches_the_synth() => Assert.Equal(FanfareSynth.CreateWav(), Fanfare.BundledWav());

    [Fact]
    public void Play_writes_the_default_file_and_plays_it_at_the_setting_volume()
    {
        _settings = new Settings { FanfareVolume = 0.6 };
        var fanfare = Create();

        fanfare.Play();

        Assert.Equal(Fanfare.BundledWav(), File.ReadAllBytes(fanfare.DefaultPath));
        Assert.Equal((fanfare.DefaultPath, 0.6), Assert.Single(_player.Played));
    }

    [Fact]
    public void Play_rewrites_a_default_file_that_differs()
    {
        var fanfare = Create();
        File.WriteAllBytes(fanfare.DefaultPath, [1, 2, 3]);
        fanfare.Play();
        Assert.Equal(Fanfare.BundledWav(), File.ReadAllBytes(fanfare.DefaultPath));
    }

    [Fact]
    public void Volume_zero_plays_nothing()
    {
        _settings = new Settings { FanfareVolume = 0 };
        Create().Play();
        Assert.Empty(_player.Played);
    }

    [Fact]
    public void Custom_sound_is_played_from_the_sounds_directory()
    {
        _settings = new Settings { FanfareSound = "Party Horn.MP3" };
        var fanfare = Create();
        Directory.CreateDirectory(fanfare.SoundsDirectory);
        var custom = Path.Combine(fanfare.SoundsDirectory, "Party Horn.MP3");
        File.WriteAllBytes(custom, [1]);

        fanfare.Play();

        Assert.Equal(custom, Assert.Single(_player.Played).Path);
    }

    [Fact]
    public void Missing_custom_sound_falls_back_to_the_default()
    {
        _settings = new Settings { FanfareSound = "gone.mp3" };
        var fanfare = Create();
        fanfare.Play();
        Assert.Equal(fanfare.DefaultPath, Assert.Single(_player.Played).Path);
    }

    [Fact]
    public void Failing_custom_sound_falls_back_to_the_default()
    {
        _settings = new Settings { FanfareSound = "broken.mp3" };
        var fanfare = Create();
        Directory.CreateDirectory(fanfare.SoundsDirectory);
        var broken = Path.Combine(fanfare.SoundsDirectory, "broken.mp3");
        File.WriteAllBytes(broken, [1]);
        _player.Succeeds = path => path == fanfare.DefaultPath;

        fanfare.Play();

        Assert.Equal(new[] { broken, fanfare.DefaultPath }, _player.Played.Select(p => p.Path));
    }

    [Fact]
    public void Play_with_a_volume_overrides_the_setting()
    {
        _settings = new Settings { FanfareVolume = 1.0 };
        Create().Play(0.3);
        Assert.Equal(0.3, Assert.Single(_player.Played).Volume);
    }

    [Fact]
    public void Import_copies_under_the_original_name_and_removes_the_previous_copy()
    {
        var fanfare = Create();
        var source = Path.Combine(_dir.Path, "source");
        Directory.CreateDirectory(source);
        File.WriteAllBytes(Path.Combine(source, "a.wav"), [1]);
        File.WriteAllBytes(Path.Combine(source, "My Song.mp3"), [2]);

        Assert.Equal("a.wav", fanfare.Import(Path.Combine(source, "a.wav")));
        Assert.Equal("My Song.mp3", fanfare.Import(Path.Combine(source, "My Song.mp3")));

        Assert.Equal(new[] { "My Song.mp3" }, Directory.GetFiles(fanfare.SoundsDirectory).Select(Path.GetFileName));
    }

    // Review Focus 2
    [Fact]
    public void Importing_the_current_copy_keeps_it()
    {
        var fanfare = Create();
        var source = Path.Combine(_dir.Path, "x.mp3");
        File.WriteAllBytes(source, [7, 7]);
        fanfare.Import(source);
        var copy = Path.Combine(fanfare.SoundsDirectory, "x.mp3");

        Assert.Equal("x.mp3", fanfare.Import(copy));
        Assert.Equal(new byte[] { 7, 7 }, File.ReadAllBytes(copy));
    }

    // Final review I1: MCI keeps the last played file open, so it must be closed before the copy is replaced.
    [Fact]
    public void Import_stops_playback_before_replacing_the_copy()
    {
        var fanfare = Create();
        var source = Path.Combine(_dir.Path, "source");
        Directory.CreateDirectory(source);
        File.WriteAllBytes(Path.Combine(source, "a.wav"), [1]);
        File.WriteAllBytes(Path.Combine(source, "b.mp3"), [2]);
        fanfare.Import(Path.Combine(source, "a.wav"));
        bool oldCopyStillThereAtStop = false;
        _player.OnStop = () => oldCopyStillThereAtStop = File.Exists(Path.Combine(fanfare.SoundsDirectory, "a.wav"));

        fanfare.Import(Path.Combine(source, "b.mp3"));

        Assert.True(oldCopyStillThereAtStop);
    }

    [Fact]
    public void Stop_stops_the_player()
    {
        Create().Stop();
        Assert.Equal(1, _player.Stops);
    }
}
