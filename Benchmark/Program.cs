using System.Diagnostics;
using System.Text;
using Newtonsoft.Json;
using SharedLogic;
using SpecificGamePlugin;

namespace Benchmark;

class Program
{
    private const int ParallelWorkers = 32; // 32 "потока"
    private const int ClientsPerWorker = 100; // каждый выполняет 100 клиентов
    private const int MovesPerGame = 10;
    private const string ServerUrl = "http://localhost:5000/api/command";
    private static readonly HttpClient Client = new();
    private static int _totalRequests;
    private static readonly object Lock = new();

    // Предсериализованные команды
    private static List<StringContent> _moveCommands = new List<StringContent>();
    private static StringContent _newGameCommand = new StringContent("");

    static async Task Main()
    {
        PrepareSerializedCommands();

        var tasks = new List<Task>();
        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < ParallelWorkers; i++)
        {
            tasks.Add(RunWorker());
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        Console.WriteLine("Benchmark completed.");
        Console.WriteLine($"Total requests: {_totalRequests}");
        Console.WriteLine($"Elapsed time: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
        Console.WriteLine($"Requests per second: {_totalRequests / stopwatch.Elapsed.TotalSeconds:F2}");
    }

    static async Task RunWorker()
    {
        for (int i = 0; i < ClientsPerWorker; i++)
        {
            var userId = Guid.NewGuid();
            await RunScenario(userId);
        }
    }

    static void PrepareSerializedCommands()
    {
        _moveCommands = new List<StringContent>();
        for (int i = 0; i < MovesPerGame; i++)
        {
            var move = new ExecuteCommandRequestDTO
            {
                CommandType = nameof(DropDiscCommand),
                CommandData = new DropDiscCommand { Column = i % 7 },
                StateHash = 0
            };
            var json = JsonConvert.SerializeObject(move);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            _moveCommands.Add(content);
        }

        var newGame = new ExecuteCommandRequestDTO
        {
            CommandType = nameof(NewGameCommand),
            CommandData = new NewGameCommand(),
            StateHash = 0
        };
        var newGameJson = JsonConvert.SerializeObject(newGame);
        _newGameCommand = new StringContent(newGameJson, Encoding.UTF8, "application/json");
    }

    static async Task RunScenario(Guid userId)
    {
        try
        {
            await LoadProfile(userId);
            foreach (var move in _moveCommands)
                await SendPreparedCommand(userId, move);

            await SendPreparedCommand(userId, _newGameCommand);

            foreach (var move in _moveCommands)
                await SendPreparedCommand(userId, move);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{userId}] ERROR: {ex.Message}");
        }
    }

    static async Task LoadProfile(Guid userId)
    {
        var url = $"{ServerUrl}/{userId}";
        var response = await Client.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[{userId}] LoadProfile error {response.StatusCode}: {error}");
            return;
        }
        CountRequest();
    }

    static async Task SendPreparedCommand(Guid userId, StringContent content)
    {
        var clone = new StringContent(await content.ReadAsStringAsync(), Encoding.UTF8, "application/json");
        clone.Headers.Add("X-User-Id", userId.ToString());

        var response = await Client.PostAsync(ServerUrl, clone);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[{userId}] SendPreparedCommand error {response.StatusCode}: {error}");
        }
        CountRequest();
    }

    static void CountRequest()
    {
        lock (Lock)
        {
            _totalRequests++;
        }
    }
}