using AmazeingEvening;
using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace gRPCGuide
{
    class Program
    {
        private static CallOptions _options;
        private static Maze.MazeClient _mazeClient;

        /// MakeRequest is a helper function, that ensures the authorization header is sent in each communication with the server
        static Task<TResp> MakeRequest<TReq, TResp>(Func<TReq, CallOptions, AsyncUnaryCall<TResp> > requestFunc, TReq requestPayload) =>
            requestFunc(requestPayload, _options).ResponseAsync;
        static async Task Main(string[] args)
        {
            var channel = new Channel(host: "maze.hightechict.nl", port: 5001, ChannelCredentials.Insecure);

            _options = new CallOptions(headers: new Metadata { { "Authorization", args.FirstOrDefault() ?? throw new Exception("Key?") } });

            var playerClient = new Player.PlayerClient(channel);
            var mazesClient = new Mazes.MazesClient(channel);

            await MakeRequest(playerClient.ForgetMeAsync, new ForgetMeRequest());
            Console.WriteLine("Forgot myself");

            /// Register ourselves
            var ourName = args.Skip(1).FirstOrDefault() ?? "deBoerIsTroef";
            var registerResult = await MakeRequest(playerClient.RegisterAsync, new RegisterRequest { Name = ourName });
            Console.WriteLine($"Registration result: [{registerResult.Result}]");

            /// List all the available mazes
            var availableMazesResult = await MakeRequest(mazesClient.GetAllAvailableMazesAsync, new GetAllAvailableMazesRequest());
            foreach (var maze in availableMazesResult.AvailableMazes)
            {
                Console.WriteLine(
                    $"Maze [{maze.Name}] | Total tiles: [{maze.TotalTiles}] | Potential reward: [{maze.PotentialReward}]");
                await DoMaze(maze, channel);
                
            }

            await channel.ShutdownAsync();
        }

        private static bool didKonami = false;

        private static async Task DoMaze(MazeInfo maze, Channel channel)
        {
            _mazeClient = new Maze.MazeClient(channel);
            var response = await MakeRequest(_mazeClient.EnterMazeAsync, new EnterMazeRequest { MazeName = maze.Name});
            var options = response.PossibleActionsAndCurrentScore;

            Stack<MoveDirection> crawlCrumbs = new Stack<MoveDirection>();
            List<Stack<MoveDirection>> _collectCrumbs = new List<Stack<MoveDirection>>();
            List<Stack<MoveDirection>> _exitCrumbs = new List<Stack<MoveDirection>>();

            if (!didKonami)
            {
                options = (await MakeMove(MoveDirection.Up, _exitCrumbs, _collectCrumbs, crawlCrumbs)) ?? options;
                options = (await MakeMove(MoveDirection.Up, _exitCrumbs, _collectCrumbs, crawlCrumbs)) ?? options;
                options = (await MakeMove(MoveDirection.Down, _exitCrumbs, _collectCrumbs, crawlCrumbs)) ?? options;
                options = (await MakeMove(MoveDirection.Down, _exitCrumbs, _collectCrumbs, crawlCrumbs)) ?? options;
                options = (await MakeMove(MoveDirection.Left, _exitCrumbs, _collectCrumbs, crawlCrumbs)) ?? options;
                options = (await MakeMove(MoveDirection.Right, _exitCrumbs, _collectCrumbs, crawlCrumbs)) ?? options;
                options = (await MakeMove(MoveDirection.Left, _exitCrumbs, _collectCrumbs, crawlCrumbs)) ?? options;
                options = (await MakeMove(MoveDirection.Right, _exitCrumbs, _collectCrumbs, crawlCrumbs)) ?? options;
                didKonami = true;
            }

            while (true)
            {
                TrackExits(options, _exitCrumbs);
                if (options.CurrentScoreInHand == 0 && options.CurrentScoreInBag >= maze.PotentialReward)
                {
                    // Looking for exit!
                    if (options.CanExitMazeHere)
                    {
                        await MakeRequest(_mazeClient.ExitMazeAsync, new ExitMazeRequest());
                        return;
                    }
                    // Looking for collection point!
                    var stack = _exitCrumbs.OrderBy(st => st.Count).FirstOrDefault();
                    if (stack == null)
                    {
                        continue;
                    }

                    var dir = stack.Peek();
                    var step = ReverseDir(dir);
                    options = await MakeMove(step, _exitCrumbs, _collectCrumbs, crawlCrumbs);
                    continue;
                }

                TrackCollectionPoints(options, _collectCrumbs);
                if (options.CurrentScoreInHand + options.CurrentScoreInBag >= maze.PotentialReward)
                {
                    if (options.CanCollectScoreHere)
                    {
                        options = (await MakeRequest(_mazeClient.CollectScoreAsync, new CollectScoreRequest())).PossibleActionsAndCurrentScore;
                        continue;
                    }

                    // Looking for collection point!
                    var stack = FindBestCollectStack(_collectCrumbs, _exitCrumbs);
                    if (stack == null)
                    {
                        continue;
                    }

                    var dir = stack.Peek();
                    var step = ReverseDir(dir);
                    options = await MakeMove(step, _exitCrumbs, _collectCrumbs, crawlCrumbs);
                    continue;
                }

                // Collecting
                var mostUsefulDirection = MostUsefulDir(options, maze);
                if (mostUsefulDirection != null)
                {
                    options = await MakeMove(mostUsefulDirection.Direction, _exitCrumbs, _collectCrumbs, crawlCrumbs);
                    continue;
                }

                if (crawlCrumbs.Any())
                {
                    var dir = ReverseDir(crawlCrumbs.Peek());
                    options = await MakeMove(dir, _exitCrumbs, _collectCrumbs, crawlCrumbs);
                    continue;
                }

                Console.WriteLine("Stuck!");
                continue;
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

        private static MoveAction MostUsefulDir(PossibleActionsAndCurrentScore options, MazeInfo mazeInfo)
        {
            var mostUsefulDir = options
			    .MoveActions.Where(ma => !ma.HasBeenVisited) // Don't ever go where we've been before (backtracking will get us there if needed)
                .OrderBy(ma => ma.AllowsScoreCollection) // Un-prefer ScoreCollection Points, we'll get there later
                .ThenBy(ma => ma.AllowsExit) // Un-prefer exits, we'll get there later
                .ThenByDescending(ma => ma.Reward) // Prefer high reward directions. Gut feeling, no real reason...
                .FirstOrDefault();
            return mostUsefulDir;
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
