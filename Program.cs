using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AmazeingEvening;
using Grpc.Core;

namespace Maze
{
    static class Program
    {
        private static CallOptions _grpcCallOptions;
        private static AmazeingEvening.Maze.MazeClient _mazeClient;

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
            _mazeClient = new AmazeingEvening.Maze.MazeClient(channel);

            await MakeRequest(playerClient.ForgetMeAsync, new ForgetMeRequest());
            Console.WriteLine("Forgot myself");

            // Register ourselves
            var registerResult = await MakeRequest(playerClient.RegisterAsync, new RegisterRequest { Name = ourName });
            Console.WriteLine($"Registration result: [{registerResult.Result}]");
            
            // Do the Konami Move
            await DoTheKonami();

            // List all the available mazes
            var availableMazesResult = await MakeRequest(mazesClient.GetAllAvailableMazesAsync, new GetAllAvailableMazesRequest());
            foreach (var maze in availableMazesResult.AvailableMazes.OrderByDescending(maze => maze.TotalTiles))
            {
                Console.WriteLine(
                    $"Maze [{maze.Name}] | Total tiles: [{maze.TotalTiles}] | Potential reward: [{maze.PotentialReward}]");
                await DoMaze(maze);
            }

            await channel.ShutdownAsync();
        }

        private static async Task DoTheKonami()
        {
            await _mazeClient.MoveAsync(new MoveRequest {Direction = MoveDirection.Up}, _grpcCallOptions);
            await _mazeClient.MoveAsync(new MoveRequest {Direction = MoveDirection.Up}, _grpcCallOptions);
            await _mazeClient.MoveAsync(new MoveRequest {Direction = MoveDirection.Down}, _grpcCallOptions);
            await _mazeClient.MoveAsync(new MoveRequest {Direction = MoveDirection.Down}, _grpcCallOptions);
            await _mazeClient.MoveAsync(new MoveRequest {Direction = MoveDirection.Left}, _grpcCallOptions);
            await _mazeClient.MoveAsync(new MoveRequest {Direction = MoveDirection.Right}, _grpcCallOptions);
            await _mazeClient.MoveAsync(new MoveRequest {Direction = MoveDirection.Left}, _grpcCallOptions);
            await _mazeClient.MoveAsync(new MoveRequest {Direction = MoveDirection.Right}, _grpcCallOptions);
        }

        private static async Task DoMaze(MazeInfo maze)
        {
            var response = await MakeRequest(_mazeClient.EnterMazeAsync, new EnterMazeRequest { MazeName = maze.Name});
            var options = response.PossibleActionsAndCurrentScore;

            var crawlCrumbs = new Stack<MoveDirection>();
            var collectCrumbs = new List<Stack<MoveDirection>>();
            var exitCrumbs = new List<Stack<MoveDirection>>();

            while (true)
            {
				//await Task.Delay(10);
                TrackExits(options, exitCrumbs);
                if (options.CurrentScoreInHand == 0 && options.CurrentScoreInBag >= maze.PotentialReward)
                {
                    // Looking for exit!
                    if (options.CanExitMazeHere)
                    {
						//Console.WriteLine("Enter please");
						//Console.ReadLine();
                        await MakeRequest(_mazeClient.ExitMazeAsync, new ExitMazeRequest());
                        return;
                    }
                    // Looking for collection point!
                    var stack = exitCrumbs.OrderBy(st => st.Count).FirstOrDefault();
                    if (stack == null)
                    {
                        continue;
                    }

                    var dir = stack.Peek();
                    var step = ReverseDir(dir);
                    options = await MakeMove(step, exitCrumbs, collectCrumbs, crawlCrumbs);
                    continue;
                }

                TrackCollectionPoints(options, collectCrumbs);
                if (options.CurrentScoreInHand + options.CurrentScoreInBag >= maze.PotentialReward)
                {
                    if (options.CanCollectScoreHere)
                    {
                        options = (await MakeRequest(_mazeClient.CollectScoreAsync, new CollectScoreRequest())).PossibleActionsAndCurrentScore;
                        continue;
                    }

                    // Looking for collection point!
                    var stack = FindBestCollectStack(collectCrumbs, exitCrumbs);
                    if (stack == null)
                    {
                        continue;
                    }

                    var dir = stack.Peek();
                    var step = ReverseDir(dir);
                    options = await MakeMove(step, exitCrumbs, collectCrumbs, crawlCrumbs);
                    continue;
                }

                // Collecting
                var mostUsefulDirection = MostUsefulDir(options, crawlCrumbs);
                if (mostUsefulDirection != null)
                {
                    options = await MakeMove(mostUsefulDirection.Direction, exitCrumbs, collectCrumbs, crawlCrumbs);
                    continue;
                }

                if (crawlCrumbs.Any())
                {
                    var dir = ReverseDir(crawlCrumbs.Peek());
                    options = await MakeMove(dir, exitCrumbs, collectCrumbs, crawlCrumbs);
                    continue;
                }

                Console.WriteLine("Stuck!");
            }
        }

        private static Stack<MoveDirection> FindBestCollectStack(List<Stack<MoveDirection>> collectCrumbs, List<Stack<MoveDirection>> exitCrumbs)
        {
            var stack = collectCrumbs.OrderBy(st => st.Count).FirstOrDefault();
            return stack;
        }

        private static void TrackExits(PossibleActionsAndCurrentScore options, List<Stack<MoveDirection>> exitCrumbs)
        {
            foreach (var dir in options.MoveActions.Where(ma => ma.AllowsExit))
            {
                var stack = new Stack<MoveDirection>();
                stack.Push(ReverseDir(dir.Direction));
                exitCrumbs.Add(stack);
            }
        }

        private static void TrackCollectionPoints(PossibleActionsAndCurrentScore options, List<Stack<MoveDirection>> collectCrumbs)
        {
            foreach (var dir in options.MoveActions.Where(ma => ma.AllowsScoreCollection))
            {
                var stack = new Stack<MoveDirection>();
                stack.Push(ReverseDir(dir.Direction));
                collectCrumbs.Add(stack);
            }
        }

        private static async Task<PossibleActionsAndCurrentScore> MakeMove(MoveDirection direction, List<Stack<MoveDirection>> exitCrumbs, List<Stack<MoveDirection>> collectCrumbs, Stack<MoveDirection> crumbs)
        {
            try
            {
                var retVal = (await MakeRequest(_mazeClient.MoveAsync, new MoveRequest {Direction = direction}))
                    .PossibleActionsAndCurrentScore;
                if (retVal == null)
                    throw new ArgumentException();

                foreach (var st in exitCrumbs)
                    Push(st, direction);

                foreach (var st in collectCrumbs)
                    Push(st, direction);

                Push(crumbs, direction);

                return retVal;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }

		static readonly Random RandomGenerator = new Random();
        private static MoveAction MostUsefulDir(PossibleActionsAndCurrentScore options,
            Stack<MoveDirection> crawlCrumbs)
        {
            var mostUsefulDir = options
				.MoveActions
				.Where(ma => !ma.HasBeenVisited) // Don't ever go where we've been before (backtracking will get us there if needed)
			    .OrderBy(_=>1)
                .ThenBy(ma => ma.AllowsScoreCollection) // Un-prefer ScoreCollection Points, we'll get there later
                .ThenBy(ma => ma.AllowsExit) // Un-prefer exits, we'll get there later
                .ThenBy(ma => HugTheLeftWall(ma.Direction, crawlCrumbs))
				//.ThenBy(ma => RandomGenerator.Next())
                .ThenByDescending(ma => ma.Direction) // Prefer Left over Down over Right over Up... no real reason, just for predictability
                .ThenByDescending(ma => ma.Reward) // Prefer high reward directions. Gut feeling, no real reason...
                .FirstOrDefault();
            return mostUsefulDir;
        }

        private static int HugTheLeftWall(MoveDirection possibleDirection, Stack<MoveDirection> crawlCrumbs)
        {
            if (crawlCrumbs.Count == 0)
                return 0;
            var incomingDirection = crawlCrumbs.Peek();
            return HugTheLeftWall(possibleDirection, incomingDirection);
        }

        static int HugTheLeftWall(MoveDirection possibleDirection, MoveDirection incomingDirection)
        {
            return (5 + possibleDirection - incomingDirection) % 4;
        }

        static void Push(Stack<MoveDirection> stack, MoveDirection dir)
        {
            if (stack.Count == 0)
            {
                stack.Push(dir);
                return;
            }

            if (stack.Count != 0 && stack.Peek() == ReverseDir(dir))
                stack.Pop();
            else
                stack.Push(dir);
        }

        private static MoveDirection ReverseDir(MoveDirection bestDir)
        {
            switch (bestDir)
            {
                case MoveDirection.Down: return MoveDirection.Up;
                case MoveDirection.Up: return MoveDirection.Down;
                case MoveDirection.Left :return MoveDirection.Right;
                case MoveDirection.Right: return MoveDirection.Left;
                default:
                    throw new ArgumentException("Bad dir");
            }
        }
    }
}
