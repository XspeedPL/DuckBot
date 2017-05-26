using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace DuckBot
{
    class Logger : IDisposable
    {
        private readonly ConcurrentQueue<string> queue;
        private TextWriter[] outputs;

        public Logger(params TextWriter[] outputs)
        {
            queue = new ConcurrentQueue<string>();
            this.outputs = outputs;
        }

        public void Log(string message) => queue.Enqueue(message);

        public async Task Output()
        {
            while (queue.TryDequeue(out string msg))
                foreach (StreamWriter sw in outputs)
                    await sw.WriteLineAsync(msg);
            foreach (StreamWriter sw in outputs)
                await sw.FlushAsync();
        }

        public void Dispose()
        {
            Output().GetAwaiter().GetResult();
            foreach (StreamWriter sw in outputs)
                sw.Close();
            outputs = null;
        }
    }
}
