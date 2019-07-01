using AmazeingEvening;
using Grpc.Core;
using System;
using System.Linq;

namespace gRPCGuide
{
    class Program
    {
        static void Main(string[] args)
        {
            var channel = new Channel(host: "10.100.100.182", port: 5001, ChannelCredentials.Insecure);

            var options = new CallOptions(headers: new Metadata { { "Authorization", args.FirstOrDefault() ?? throw new Exception("Key?") } });

            var playerClient = new Player.PlayerClient(channel);
            var mazesClient = new Mazes.MazesClient(channel);
            var mazeClient = new Maze.MazeClient(channel);

            /// MakeRequest is a helper function, that ensures the authorization header is sent in each communication with the server
            TResp MakeRequest<TReq, TResp>(Func<TReq, CallOptions, TResp> requestFunc, TReq requestPayload) =>
                requestFunc(requestPayload, options);

            /// Register ourselves
            var ourName = $"gRPC guide ({Guid.NewGuid().ToString().Substring(0, 5)})";
            var registerResult = MakeRequest(playerClient.Register, new RegisterRequest { Name = ourName });
            Console.WriteLine($"Registration result: [{registerResult.Result}]");

            /// List all the available mazes
            var availableMazesResult = MakeRequest(mazesClient.GetAllAvailableMazes, new GetAllAvailableMazesRequest());
            foreach (var maze in availableMazesResult.AvailableMazes)
                Console.WriteLine($"Maze [{maze.Name}] | Total tiles: [{maze.TotalTiles}] | Potential reward: [{maze.PotentialReward}]");
        }
    }
}
