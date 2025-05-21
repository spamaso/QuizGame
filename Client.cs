using System;
using System.Net.Sockets;
using System.Text;

class Program
{
    static async Task Main()
    {
        var client = new QuizClient("127.0.0.1", 5000);
        await client.Start();
    }
}

public class QuizClient
{
    private readonly string serverIp;
    private readonly int port;
    private TcpClient client;
    private NetworkStream stream;
    private string nome;
    private CancellationTokenSource tokenSource = new CancellationTokenSource();
    private bool ricezioneAttiva = true;
    public QuizClient(string serverIp, int port)
    {
        this.serverIp = serverIp;
        this.port = port;
    }
    public async Task Start()
    {
        client = new TcpClient();
        await client.ConnectAsync(serverIp, port);
        stream = client.GetStream();

        Console.WriteLine("Inserisci il tuo nome:");
        nome = Console.ReadLine();
        await SendMessage(nome);

        await RiceviMessaggi(tokenSource.Token);
    }
    private async Task RiceviMessaggi(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (ricezioneAttiva && stream.DataAvailable)
                {
                    string messaggio = ReceiveMessage();
                    if (!string.IsNullOrEmpty(messaggio))
                    {
                        if (messaggio.Contains("|"))
                        {
                            ricezioneAttiva = false;
                            string[] parti = messaggio.Split('|');
                            Console.WriteLine(parti[0]);
                            Console.WriteLine($"1. {parti[1]} 2. {parti[2]} 3. {parti[3]}");

                            await AttendiRisposta(token);

                            ricezioneAttiva = true;
                        }
                        else
                        {
                            Console.WriteLine(messaggio);
                        }
                    }
                }
                else
                {
                    await Task.Delay(50, token); 
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore durante la ricezione: {ex.Message}");
                tokenSource.Cancel();
            }
        }
    }
    private async Task AttendiRisposta(CancellationToken token)
    {
        Console.WriteLine("Inserisci il numero della risposta:");

        CancellationTokenSource tokenWaiterSource = new CancellationTokenSource();

        var inputTask = Task.Run(() => Console.ReadLine(), token);
        var streamTask = WaitForServerMessage(tokenWaiterSource.Token);

        var completedTask = await Task.WhenAny(inputTask, streamTask);

        if (completedTask == inputTask)
        {
            tokenWaiterSource.Cancel();
            string input = await inputTask;

            if (int.TryParse(input, out int risposta) && risposta > 0 && risposta < 4)
            {
                await SendMessage(risposta.ToString());
            }
            else
            {
                Console.WriteLine("Risposta non valida. Sarai considerato assente.");
                await SendMessage("Assente");
            }
        }
        else if (completedTask == streamTask)
        {
            string messaggio = await streamTask;
            if (!string.IsNullOrEmpty(messaggio))
            {
                Console.WriteLine(messaggio);
            }
        }
    }
    private async Task<string> WaitForServerMessage(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (stream.DataAvailable)
            {
                string temp = ReceiveMessage();
                if(temp != null)
                {
                    return temp;
                }
            }
            await Task.Delay(50, token);
        }
        return null;
    }
    private async Task SendMessage(string message)
    {
        byte[] buffer = Encoding.UTF8.GetBytes(message);
        await stream.WriteAsync(buffer, 0, buffer.Length);
        await stream.FlushAsync();
    }
    private string ReceiveMessage()
    {
        byte[] buffer = new byte[1024];
        int byteCount = stream.Read(buffer, 0, buffer.Length);

        if (byteCount == 0)
        {
            Console.WriteLine("Connessione chiusa");
            tokenSource.Cancel();
            return null;
        }

        return Encoding.UTF8.GetString(buffer, 0, byteCount);
    }
}
