﻿using System;
using System.Linq;
using System.Threading.Tasks;
using HightechICT.Amazeing.Client.Rest;

namespace Maze
{
    static class Program
    {
        /// MakeRequest is a helper function, that ensures the authorization header is sent in each communication with the server
        static async Task Main(string[] args)
        {
            if (!args.Any())
            {
                await Console.Error.WriteLineAsync("Usage:");
                await Console.Error.WriteLineAsync("Maze \"<ApiKey>\" [PlayerName] [host] [Maze]*");
                return;
            }
			
			var mazeNames= args.Skip(3).Select(name => name.ToLowerInvariant()).ToHashSet();
            Global.IsInteractive = mazeNames.Any();
            
            var serverHost = args.Skip(2).FirstOrDefault() ?? "maze.hightechict.nl";
            var apiKey = args.FirstOrDefault() ?? throw new Exception("Key?");
            var ourName = args.Skip(1).FirstOrDefault() ?? "deBoerIsTroef";

            Console.Error.WriteLine($"Connecting to {serverHost} with key '{apiKey}'; nickname {ourName}");
            var httpClient = new System.Net.Http.HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", apiKey);
            var client = new CountingClient(new AmazeingClient(serverHost, httpClient));

            await client.ForgetPlayer();
            Console.Error.WriteLine("Forgot myself");

            // Register ourselves
            await client.RegisterPlayer(ourName);            
            
            // List all the available mazes
            var availableMazes = (await client.AllMazes())
				.OrderByDescending(maze => (double)maze.PotentialReward / maze.TotalTiles);
			
            var overhead = client.Invocations;
            
            foreach (var maze in availableMazes)
            {
                var doing = !mazeNames.Any() || mazeNames.Contains(maze.Name.ToLowerInvariant());
                Console.Error.WriteLine(
                    $"{(doing?"Doing":"Skipping")} maze [{maze.Name}] | Total tiles: [{maze.TotalTiles}] | Potential reward: [{maze.PotentialReward}]");
                if (!doing)
                    continue;
                var baseInvocations = client.Invocations;
                await new MazeSolver(client, maze).Solve();
                Console.WriteLine($"{maze.Name}, {client.Invocations - baseInvocations}");
            }
            
            // Do the Konami Move
            if (!Global.IsInteractive)
            {
                var baseInvocations = client.Invocations;
                await DoTheKonami(client);
                Console.WriteLine($"Easter Egg, {client.Invocations - baseInvocations}");
            }

            Console.WriteLine($"Registration, {overhead}");
            Console.WriteLine($"Total, {client.Invocations}");
        }

        private static async Task DoTheKonami(IAmazeingClient mazeClient)
        {
            try{ await mazeClient.Move(Direction.Up);} catch { }
            try{ await mazeClient.Move(Direction.Up);} catch { }
            try{ await mazeClient.Move(Direction.Down);} catch { }
            try{ await mazeClient.Move(Direction.Down);} catch { }
            try{ await mazeClient.Move(Direction.Left);} catch { }
            try{ await mazeClient.Move(Direction.Right);} catch { }
            try{ await mazeClient.Move(Direction.Left);} catch { }
            try{ await mazeClient.Move(Direction.Right);} catch { }
        }
    }
}
