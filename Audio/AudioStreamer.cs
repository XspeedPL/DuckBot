using System;
using System.Threading.Tasks;
using Discord.Audio;
using CSCore;
using CSCore.Codecs;
using Nito.AsyncEx;

namespace DuckBot.Audio
{
    public sealed class AudioStreamer : IDisposable
    {
        public IAudioClient AudioClient { get; internal set; }
        
        private TaskCompletionSource<bool> awaiter;
        private bool end;
        private AsyncMonitor stopSync = new AsyncMonitor(), playSync = new AsyncMonitor();

        public AudioStreamer(IAudioClient ac)
        {
            AudioClient = ac;
            end = true;
        }

        public async Task PlayAsync(Uri input)
        {
            using (await playSync.EnterAsync())
            {
                end = false;
                using (IWaveSource source = CodecFactory.Instance.GetCodec(input).ChangeSampleRate(Discord.Audio.Streams.OpusEncodeStream.SampleRate))
                {
                    int size = source.WaveFormat.BytesPerSecond / 50;
                    byte[] buffer = new byte[size];
                    int read;
                    using (AudioOutStream output = AudioClient.CreatePCMStream(AudioApplication.Music))
                    {
                        while (!end && (read = source.Read(buffer, 0, size)) > 0)
                        {
                            if (read < size)
                                for (int i = read; i < size; ++i) buffer[i] = 0;
                            output.Write(buffer, 0, size);
                        }
                        output.Flush();
                    }
                    end = true;
                    awaiter?.TrySetResult(source.Length <= source.Position);
                }
            }
        }

        public async Task<bool> StopAsync()
        {
            using (await stopSync.EnterAsync())
                if (!end)
                {
                    end = true;
                    awaiter = new TaskCompletionSource<bool>();
                    bool result = await awaiter.Task;
                    awaiter = null;
                    return result;
                }
                else return true;
        }

        public void Dispose()
        {
            StopAsync().GetAwaiter().GetResult();
            AudioClient.Dispose();
        }
    }
}
