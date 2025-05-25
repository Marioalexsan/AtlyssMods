using NAudio.Wave;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Marioalexsan.ModAudio;

public static class AudioClipLoader
{
    public static readonly string[] SupportedLoadExtensions = [
        ".wav",
        ".ogg",
        ".mp3"
    ];

    public static readonly string[] SupportedStreamExtensions = [
        ".wav",
        ".ogg",
        ".mp3"
    ];

    public static readonly string[] SupportedExtensions = SupportedLoadExtensions.Concat(SupportedStreamExtensions).Distinct().ToArray();

    /// <summary>
    /// Creates an empty clip with the given name and duration.
    /// </summary>
    public static AudioClip GenerateEmptyClip(string name, int samples)
    {
        var clip = AudioClip.Create(name, samples, 1, 44100, false);
        clip.SetData(new float[samples], 0);
        return clip;
    }

    /// <summary>
    /// Loads an audio clip in its entirety from the disk.
    /// </summary>
    public static AudioClip LoadFromFile(string clipName, string path, float volumeModifier)
    {
        if (path.EndsWith(".ogg"))
        {
            using var stream = File.OpenRead(path);
            return LoadOgg(clipName, stream);
        }

        if (path.EndsWith(".mp3"))
        {
            using var stream = File.OpenRead(path);
            return LoadMp3(clipName, stream);
        }

        if (path.EndsWith(".wav"))
        {
            using var stream = File.OpenRead(path);
            return LoadWav(clipName, stream);
        }

        throw new NotImplementedException("The given file format isn't supported for loading.");
    }

    private static AudioClip LoadOgg(string clipName, Stream stream)
    {
        using var reader = new NVorbis.VorbisReader(stream);

        var clip = AudioClip.Create(clipName, (int)reader.TotalSamples, reader.Channels, reader.SampleRate, false);

        var samples = new float[reader.TotalSamples * reader.Channels];
        reader.ReadSamples(samples, 0, samples.Length);
        clip.SetData(samples, 0);

        return clip;
    }

    private static AudioClip LoadWav(string clipName, Stream stream)
    {
        using var reader = new WaveFileReader(stream);

        var clip = AudioClip.Create(clipName, (int)reader.SampleCount, reader.WaveFormat.Channels, reader.WaveFormat.SampleRate, false);

        var provider = reader.ToSampleProvider();

        var samples = new float[(int)reader.SampleCount * reader.WaveFormat.Channels];

        provider.Read(samples, 0, samples.Length);
        clip.SetData(samples, 0);

        return clip;
    }

    private static AudioClip LoadMp3(string clipName, Stream stream)
    {
        using var reader = new Mp3FileReader(stream);

        var totalSamples = (int)(reader.Length * 8 / reader.WaveFormat.BitsPerSample);

        var clip = AudioClip.Create(clipName, totalSamples, reader.WaveFormat.Channels, reader.WaveFormat.SampleRate, false);

        var provider = reader.ToSampleProvider();

        var samples = new float[totalSamples * reader.WaveFormat.Channels];

        provider.Read(samples, 0, samples.Length);
        clip.SetData(samples, 0);

        return clip;
    }

    /// <summary>
    /// Streams an audio clip from the disk.
    /// </summary>
    public static AudioClip StreamFromFile(string clipName, string path, float volumeModifier, out IAudioStream openedStream)
    {
        IAudioStream? stream = null;

        if (path.EndsWith(".ogg"))
        {
            stream = new OggStream(File.OpenRead(path)) { VolumeModifier = volumeModifier };
        }

        if (path.EndsWith(".mp3"))
        {
            stream = new Mp3Stream(File.OpenRead(path)) { VolumeModifier = volumeModifier };
        }

        if (path.EndsWith(".wav"))
        {
            stream = new WavStream(File.OpenRead(path)) { VolumeModifier = volumeModifier };
        }

        if (stream == null)
            throw new NotImplementedException("The given file format isn't supported for streaming.");

        openedStream = stream;
        return AudioClip.Create(clipName, stream.TotalFrames, stream.ChannelsPerFrame, stream.Frequency, true, stream.OnAudioRead, stream.OnAudioSetPosition);
    }

    public interface IAudioStream : IDisposable
    {
        int TotalFrames { get; }
        int ChannelsPerFrame { get; }
        int Frequency { get; }

        void OnAudioRead(float[] samples); // Unity seems to be calling this with float[4096]
        void OnAudioSetPosition(int newPosition);
    }

    private class OggStream(Stream stream) : IAudioStream
    {
        private readonly NVorbis.VorbisReader _reader = new NVorbis.VorbisReader(stream);

        public float VolumeModifier = 1f;

        public int TotalFrames => (int)_reader.TotalSamples;
        public int ChannelsPerFrame => _reader.Channels;
        public int Frequency => _reader.SampleRate;

        public void OnAudioRead(float[] samples)
        {
            _reader.ReadSamples(samples, 0, samples.Length);
            OptimizedMethods.MultiplyFloatArray(samples, VolumeModifier);
        }

        public void OnAudioSetPosition(int newPosition)
        {
            _reader.SamplePosition = newPosition;
        }

        public void Dispose()
        {
            _reader.Dispose();
        }
    }

    private class WavStream : IAudioStream
    {
        public WavStream(Stream stream)
        {
            _reader = new WaveFileReader(stream);
            _provider = _reader.ToSampleProvider();
        }
        private readonly WaveFileReader _reader;
        private readonly ISampleProvider _provider;

        public float VolumeModifier = 1f;

        public int TotalFrames => (int)_reader.SampleCount;
        public int ChannelsPerFrame => _reader.WaveFormat.Channels;
        public int Frequency => _reader.WaveFormat.SampleRate;

        public void OnAudioRead(float[] samples)
        {
            _provider.Read(samples, 0, samples.Length);
            OptimizedMethods.MultiplyFloatArray(samples, VolumeModifier);
        }

        public void OnAudioSetPosition(int newPosition)
        {
            _reader.Position = newPosition * _reader.BlockAlign;
        }

        public void Dispose()
        {
            _reader.Dispose();
        }
    }

    private class Mp3Stream : IAudioStream
    {
        public Mp3Stream(Stream stream)
        {
            _reader = new Mp3FileReader(stream);
            _provider = _reader.ToSampleProvider();
        }
        private readonly Mp3FileReader _reader;
        private readonly ISampleProvider _provider;

        public float VolumeModifier = 1f;

        public int TotalFrames => (int)(_reader.Length * 8 / ChannelsPerFrame / _reader.WaveFormat.BitsPerSample);
        public int ChannelsPerFrame => _reader.WaveFormat.Channels;
        public int Frequency => _reader.WaveFormat.SampleRate;

        public void OnAudioRead(float[] samples)
        {
            _provider.Read(samples, 0, samples.Length);
            OptimizedMethods.MultiplyFloatArray(samples, VolumeModifier);
        }

        public void OnAudioSetPosition(int newPosition)
        {
            _reader.Position = newPosition * _reader.BlockAlign;
        }

        public void Dispose()
        {
            _reader.Dispose();
        }
    }
}
