using System.Net;
using System.Net.Sockets;
using System.Text;

namespace server
{
    public class Giocatore
    {
        public TcpClient Client { get; set; }
        public NetworkStream Stream => Client.GetStream();
        public string Nome { get; set; }
        public int Punteggio { get; set; } = 0;
        public TimeSpan TempoRisposta {get; set;}
        public bool CorrettezzaRisposta {get; set;} = false;
    }
    
    public class Party
    {
        private static Party instance = null;
        private static readonly object padlock = new object();
        private List<Giocatore> giocatori = new List<Giocatore>();
        private object lockGiocatori = new object();
        private bool tempoScaduto = false;
        private object lockTempo = new object();
        private DataBaseQuiz DataBase = DataBaseQuiz.GetInstance();
        private bool partitaIniziata = false;
        public static Party GetInstance()
        {
            lock (padlock)
            {
                if(instance == null)
                {
                    instance = new Party();
                }
                return instance;
            }
        }
        public Party() 
        {
            DataBase.LoadData();

            int port = 5000;
            var server = new TcpListener(IPAddress.Any, port);
            server.Start();
            
            Console.WriteLine($"Server in ascolto sulla porta {port}");

            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        await ControllaConsole();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Errore: {ex.Message}");
                    }
                    partitaIniziata = false;
                }
            });

            while (true)
            {
                TcpClient client = server.AcceptTcpClient();
                Task.Run( () => GestisciClient(client) );
            }
        }
        private async Task ControllaConsole()
        {
            while (!partitaIniziata)
            {
                Console.Write("Scegli un argomento: \t 1. Matematica 2. Geografia 3. Scienze 4. Cinema");
                string categoria = Console.ReadLine() ?? "0";
                int indexCategoria = 0;
                while(!int.TryParse(categoria, out indexCategoria) || !(indexCategoria > 0 && indexCategoria < 5))
                {
                    categoria = Console.ReadLine() ?? "0";
                }

                Console.Write("Scegli la difficoltà: \t 1. Facile 2. Media 3. Difficile");
                string difficolta = Console.ReadLine() ?? "0";
                int indexDifficolta = 0;
                while(!int.TryParse(difficolta, out indexDifficolta) || !(indexDifficolta > 0 && indexDifficolta < 4))
                {
                    difficolta = Console.ReadLine() ?? "0";
                }
                
                Console.WriteLine("Iniziare la partita? 0. Exit 1.Start ");
                string input = Console.ReadLine() ?? "0";
                if (input == "1")
                {
                    partitaIniziata = true;
                    Console.WriteLine("Partita iniziata!");
                    await IniziaTurno(indexCategoria, indexDifficolta);
                }
                else if(input == "0")
                {
                    return;
                }
            }
        }
        private async Task IniziaTurno(int categoria, int difficolta)
        {
            ResetGiocatori();

            string domanda = DataBase.GetRandomQuestion(categoria, difficolta);
            Console.Write(domanda);
            var invioDomande = giocatori.Select(g => SendMessage(g.Stream, domanda));
            await Task.WhenAll(invioDomande);

            var cancellationToken = new CancellationTokenSource();
            var cronometro = new Cronometro(15, cancellationToken.Token);
            var cronometroTask = cronometro.Avvia();

            var risposteTask = giocatori.Select(async g =>
            {
                try
                {
                    string risposta = await TimeoutRisposta(g.Stream, cancellationToken.Token);
                    if (risposta == "")
                    {
                        Console.WriteLine("Nope");
                        return;
                    }

                    Console.WriteLine("Risposta ricevuta: " + risposta);

                    lock (lockGiocatori)
                    {
                        g.TempoRisposta = cronometro.GetTempoTrascorso();

                        // Risposta potrebbe non essere un int
                        g.CorrettezzaRisposta = DataBase.CheckSolution(categoria, domanda, int.Parse(risposta));
                        if(g.CorrettezzaRisposta)
                        {
                            g.Punteggio++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Errore nella gestione della risposta: {ex.Message}");
                }
            }).ToArray();

            cronometroTask.Wait();
            await Task.WhenAll(risposteTask);
            await FineTurno();

            cancellationToken.Cancel();
        }

        private void ResetGiocatori()
        {
            lock (lockGiocatori)
            {
                foreach (var giocatore in giocatori)
                {
                    giocatore.TempoRisposta = TimeSpan.Zero;
                    giocatore.CorrettezzaRisposta = false;
                }
            }
        }
        private async Task<string> TimeoutRisposta(NetworkStream stream, CancellationToken token)
        {
            var tokenRisposta = new CancellationTokenSource(15000); 
            var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(token, tokenRisposta.Token);

            var readTask = ReadMessageAsync(stream, linkedToken.Token);

            try
            {
                await Task.WhenAny(readTask, Task.Delay(Timeout.Infinite, linkedToken.Token));
                if (readTask.IsCompletedSuccessfully)
                {
                    return await readTask;
                }
                else
                {
                    return "";
                }
            }
            catch
            {
                return "";
            }
        }
        private async Task<string> ReadMessageAsync(NetworkStream stream, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (stream.DataAvailable)
                {
                    return await ReciveMessage(stream);
                }
                await Task.Delay(50, token);
            }
            return "";
        }
        private async Task FineTurno()
        {
            List<Giocatore> classifica = (from g in giocatori orderby g.TempoRisposta ascending select g).ToList();

            bool primo = false;
            string esito;

            Console.WriteLine("Tempo scaduto! Fine turno!");
            Console.WriteLine("Stampa classifica:");

            int i = 1;
            foreach(Giocatore g in classifica)
            {
                
                if(g.TempoRisposta == TimeSpan.Zero)
                {
                    esito = "Tempo scaduto!";
                }
                else if(!primo)
                {
                    if(g.CorrettezzaRisposta)
                    {
                        g.Punteggio++;
                        esito = "Hai risposto per primo ed è CORRETTO!";
                        primo = true;
                    }
                    else
                    {
                        esito = "Hai risposto per primo ma è SBAGLIATO!";
                    }
                }
                else
                {
                    if(g.CorrettezzaRisposta)
                    {
                        esito = "Corretto ma sei stato troppo lento!";
                    }
                    else
                    {
                        esito = "Errato!";
                    }
                }

                Console.WriteLine($"{i}= " + g.Nome + $"\t Risposta: {g.CorrettezzaRisposta}" + "\t Punteggio:" + g.Punteggio + " \t Tempo Impiegato: " + g.TempoRisposta);

                await SendMessage(g.Stream, esito);
            };
        }
        private async void GestisciClient(TcpClient client)
        {
            if(partitaIniziata) 
            {
                client.Close();
                return;
            }

            Giocatore giocatore = new Giocatore();
            giocatore.Client = client;

            string nomeGiocatore = await ReciveMessage(client.GetStream());
            giocatore.Nome = nomeGiocatore;
            
            lock(lockGiocatori)
            {
                giocatori.Add(giocatore);
            }            
        }
        private async Task SendMessage(NetworkStream stream, string message)
        {
            byte[] messageBuffer = Encoding.UTF8.GetBytes(message);
            await stream.WriteAsync(messageBuffer, 0, messageBuffer.Length);
            await stream.FlushAsync();
        }
        private async Task<string> ReciveMessage(NetworkStream stream)
        {
            byte[] buffer = new byte[1024];
            int byteCount = await stream.ReadAsync(buffer, 0, buffer.Length);
            return Encoding.UTF8.GetString(buffer, 0, byteCount);
        }
    }
}
