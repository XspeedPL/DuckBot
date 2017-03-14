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
        public IAudioClient AudioClient { get; set; }

        private BufferedWaveProvider buffWave;
        private bool end;

        public AudioStreamer(IAudioClient ac)
        {
            AudioClient = ac;
            end = false;
        }

        private void ProcessStream(Stream input)
        {
            byte[] buffer = new byte[1024 * 128];
            IMp3FrameDecompressor decompressor = null;
            try
            {
                using (WrapperStream source = new WrapperStream(input))
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
                                buffWave.BufferDuration = TimeSpan.FromSeconds(10);
                                buffWave.ReadFully = false;
                            }
                            int decompressed = decompressor.DecompressFrame(frame, buffer, 0);
                            buffWave.AddSamples(buffer, 0, decompressed);
                        }
                    }
                    while (!end);
            }
            //catch (Exception ex) { Program.Log(ex); }
            finally
            {
                if (decompressor != null) decompressor.Dispose();
            }
        }

        public async void Play(int channels, Stream source)
        {
            end = false;
            Task download = Utils.RunAsync(ProcessStream, source);
            while (buffWave == null) Thread.Sleep(500);
            var format = new WaveFormat(48000, 16, channels);
            using (MediaFoundationResampler resampler = new MediaFoundationResampler(buffWave, format))
            {
                resampler.ResamplerQuality = 60;
                int blockSize = format.AverageBytesPerSecond / 50;
                byte[] buffer = new byte[blockSize];
                int byteCount;

                while (!end && (byteCount = resampler.Read(buffer, 0, blockSize)) > 0)
                {
                    int i;
                    if (byteCount < blockSize)
                        for (i = byteCount; i < blockSize; ++i) buffer[i] = 0;
                    i = 0;
                SendAgain:
                    try { AudioClient.Send(buffer, 0, blockSize); }
                    catch (OperationCanceledException)
                    {
                        Thread.Sleep(500);
                        if (++i < 10) goto SendAgain;
                    }
                }
            }
            await download;
            Dispose();
        }

        public void Dispose()
        {
            end = true;
            buffWave = null;
        }
    }
}
