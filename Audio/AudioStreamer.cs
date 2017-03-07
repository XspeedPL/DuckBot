using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using Discord.Audio;

namespace DuckBot.Audio
{
    public sealed class AudioStreamer : IDisposable
    {
        public IAudioClient AudioClient { get; private set; }

        private BufferedWaveProvider buffWave;
        private MediaFoundationResampler resampler;
        private Stream source;
        private bool end, complete;

        public AudioStreamer(IAudioClient ac)
        {
            AudioClient = ac;
            end = false;
            complete = false;
        }

        public void ProcessStream(Stream input)
        {
            byte[] buffer = new byte[ushort.MaxValue];
            IMp3FrameDecompressor decompressor = null;
            try
            {
                using (source = new ReadAheadStream(input))
                    do
                    {
                        if (buffWave != null && buffWave.BufferLength - buffWave.BufferedBytes < buffWave.WaveFormat.AverageBytesPerSecond / 4)
                            Thread.Sleep(500);
                        else
                        {
                            Mp3Frame frame;
                            try { frame = Mp3Frame.LoadFromStream(source); }
                            catch (EndOfStreamException) { break; }
                            if (frame == null) break;
                            else if (decompressor == null)
                            {
                                decompressor = new AcmMp3FrameDecompressor(new Mp3WaveFormat(frame.SampleRate, frame.ChannelMode == ChannelMode.Mono ? 1 : 2, frame.FrameLength, frame.BitRate));
                                buffWave = new BufferedWaveProvider(decompressor.OutputFormat);
                                buffWave.BufferDuration = TimeSpan.FromSeconds(20);
                            }
                            int decompressed = decompressor.DecompressFrame(frame, buffer, 0);
                            buffWave.AddSamples(buffer, 0, decompressed);
                        }
                    }
                    while (!end);
                decompressor.Dispose();
                complete = true;
            }
            catch (IOException) { }
            finally
            {
                if (decompressor != null) decompressor.Dispose();
                complete = true;
            }
        }

        public void Play(int channels)
        {
            end = false;
            while (buffWave == null) Thread.Sleep(500);
            var format = new WaveFormat(48000, 16, channels);
            using (resampler = new MediaFoundationResampler(buffWave, format))
            {
                resampler.ResamplerQuality = 59;
                int blockSize = format.AverageBytesPerSecond / 50;
                byte[] buffer = new byte[blockSize];
                int byteCount;
                
                while (!end && (!complete || buffWave.BufferDuration.Ticks > 0) && (byteCount = resampler.Read(buffer, 0, blockSize)) > 0)
                {
                    if (byteCount < blockSize)
                        for (int i = byteCount; i < blockSize; ++i) buffer[i] = 0;
                    AudioClient.Send(buffer, 0, blockSize);
                }
            }
        }

        public void Dispose()
        {
            end = true;
            if (resampler != null) resampler.Dispose();
            if (source != null) source.Dispose();
            buffWave = null;
        }
    }
}
