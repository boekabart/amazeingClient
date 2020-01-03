using System;
using System.Linq;
using System.Threading.Tasks;
using AmazeingEvening;
using Grpc.Core;

namespace Maze
{
    static class Program
    {
        private static CallOptions _grpcCallOptions;

        /// MakeRequest is a helper function, that ensures the authorization header is sent in each communication with the server
        static Task<TResp> MakeRequest<TReq, TResp>(Func<TReq, CallOptions, AsyncUnaryCall<TResp> > requestFunc, TReq requestPayload) =>
            requestFunc(requestPayload, _grpcCallOptions).ResponseAsync;
        static async Task Main(string[] args)
        {
            if (!args.Any() || !int.TryParse(args.Skip(3).FirstOrDefault() ?? "5005", out var serverPort))
            {
                await Console.Error.WriteLineAsync("Usage:");
                await Console.Error.WriteLineAsync("Maze \"<ApiKey>\" [PlayerName] [host] [port]");
                return;
            }
            var serverHost = args.Skip(2).FirstOrDefault() ?? "maze.hightechict.nl";
            var apiKey = args.FirstOrDefault() ?? throw new Exception("Key?");
            var ourName = args.Skip(1).FirstOrDefault() ?? "deBoerIsTroef";

            Console.WriteLine($"Connecting to {serverHost}:{serverPort} with key '{apiKey}'; nickname {ourName}");
            var channel = new Channel(serverHost, serverPort, ChannelCredentials.Insecure);

            _grpcCallOptions = new CallOptions(new Metadata { { "Authorization", apiKey } });

            var playerClient = new Player.PlayerClient(channel);
            var mazesClient = new Mazes.MazesClient(channel);
            var mazeClient = new AmazeingEvening.Maze.MazeClient(channel);
            await MakeRequest(playerClient.ForgetMeAsync, new ForgetMeRequest());
            Console.WriteLine("Forgot myself");

            // Register ourselves
            var registerResult = await MakeRequest(playerClient.RegisterAsync, new RegisterRequest { Name = ourName });
            Console.WriteLine($"Registration result: [{registerResult.Result}]");
            
            // Do the Konami Move
            await DoTheKonami(mazeClient);

            var mazeProxy = new SimpleMazeClient(mazeClient, _grpcCallOptions);
            
            // List all the available mazes
            var availableMazesResult = await MakeRequest(mazesClient.GetAllAvailableMazesAsync, new GetAllAvailableMazesRequest());
            foreach (var maze in availableMazesResult.AvailableMazes.OrderByDescending(maze => maze.TotalTiles))
            {
                Console.WriteLine(
                    $"Maze [{maze.Name}] | Total tiles: [{maze.TotalTiles}] | Potential reward: [{maze.PotentialReward}]");
                await new MazeSolver(mazeProxy, maze).Solve();
            }

            await channel.ShutdownAsync();
        }

        private static async Task DoTheKonami(AmazeingEvening.Maze.MazeClient mazeClient)
        {
            await mazeClient.MoveAsync(new MoveRequest {Direction = MoveDirection.Up}, _grpcCallOptions);
            await mazeClient.MoveAsync(new MoveRequest {Direction = MoveDirection.Up}, _grpcCallOptions);
            await mazeClient.MoveAsync(new MoveRequest {Direction = MoveDirection.Down}, _grpcCallOptions);
            await mazeClient.MoveAsync(new MoveRequest {Direction = MoveDirection.Down}, _grpcCallOptions);
            await mazeClient.MoveAsync(new MoveRequest {Direction = MoveDirection.Left}, _grpcCallOptions);
            await mazeClient.MoveAsync(new MoveRequest {Direction = MoveDirection.Right}, _grpcCallOptions);
            await mazeClient.MoveAsync(new MoveRequest {Direction = MoveDirection.Left}, _grpcCallOptions);
            await mazeClient.MoveAsync(new MoveRequest {Direction = MoveDirection.Right}, _grpcCallOptions);
        }
    }
}
