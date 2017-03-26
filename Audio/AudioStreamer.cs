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

        private async Task ProcessStream(Stream input)
        {
            byte[] buffer = new byte[1024 * 128];
            IMp3FrameDecompressor decompressor = null;
            try
            {
                using (WrapperStream source = new WrapperStream(input))
                    do
                    {
                        if (buffWave != null && buffWave.BufferLength - buffWave.BufferedBytes < buffWave.WaveFormat.AverageBytesPerSecond / 4)
                            await Task.Delay(500);
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
                                buffWave.BufferDuration = TimeSpan.FromSeconds(7);
                                buffWave.ReadFully = false;
                            }
                            int decompressed = decompressor.DecompressFrame(frame, buffer, 0);
                            buffWave.AddSamples(buffer, 0, decompressed);
                        }
                    }
                    while (!end);
            }
            catch (IOException ex) { Program.Log(ex); }
            finally
            {
                if (decompressor != null) decompressor.Dispose();
            }
        }

        public async void Play(int channels, Stream source)
        {
            end = false;
            Task download = ProcessStream(source);
            while (buffWave == null) Thread.Sleep(500);
            WaveFormat format = buffWave.WaveFormat;
            int blockSize = format.AverageBytesPerSecond / 50;
            byte[] buffer = new byte[blockSize];
            int byteCount;
            lock (this)
                using (Stream output = AudioClient.CreatePCMStream(AudioApplication.Music, 1920, format.Channels, format.SampleRate))
                    while (!end && (byteCount = buffWave.Read(buffer, 0, blockSize)) > 0)
                    {
                        if (byteCount < blockSize)
                            for (int i = byteCount; i < blockSize; ++i) buffer[i] = 0;
                        output.Write(buffer, 0, buffer.Length);
                    }
            await download;
            End();
        }

        public void End()
        {
            end = true;
            lock (this) buffWave = null;
        }

        public void Dispose()
        {
            End();
            if (AudioClient != null) AudioClient.Dispose();
        }
    }
}
