using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace server
{
    public class Cronometro
    {
        private Stopwatch stopwatch;
        private int limiteSecondi;
        private CancellationToken token;
        public event Action Scaduto;

        public Cronometro(int limite, CancellationToken token)
        {
            stopwatch = new Stopwatch();
            limiteSecondi = limite;
            this.token = token;
        }

        public async Task Avvia()
        {
            stopwatch.Start();
            Console.WriteLine("Cronometro avviato...");

            while (stopwatch.Elapsed.TotalSeconds < limiteSecondi && !token.IsCancellationRequested)
            {
                await Task.Delay(100);  
            }

            stopwatch.Stop();
        }

        public TimeSpan GetTempoTrascorso()
        {
            return stopwatch.Elapsed;
        }
    }

}