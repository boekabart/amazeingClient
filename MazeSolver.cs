using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AmazeingEvening;

namespace Maze
{
    internal class MazeSolver
    {
        private readonly IMazeClient _client;

        public MazeSolver( IMazeClient client,MazeInfo maze )
        {
            _client = client;
            this._maze = maze;
        }
        public async Task Solve()
        {
            var response = await _client.EnterMaze(_maze);
            var options = response.PossibleActionsAndCurrentScore;
            TrackExits(options, _exitCrumbs);
            TrackCollectionPoints(options, _collectCrumbs);

            options = await CollectAllPoints(options);
            options = await CollectScoreInHand(options);
            await GoToExit(options);
        }

        private async Task GoToExit(PossibleActionsAndCurrentScore options)
        {
            while (true)
            {
                // Looking for exit!
                if (options.CanExitMazeHere)
                {
                    //Console.WriteLine("Enter please");
                    //Console.ReadLine();
                    await _client.ExitMaze();
                    return;
                }

                // Looking for known route to exit point
                var stack = _exitCrumbs.OrderBy(st => st.Count).FirstOrDefault();
                if (stack != null)
                {
                    var dir = stack.Peek();
                    var step = ReverseDir(dir);
                    options = await MakeMove(step, _exitCrumbs, _collectCrumbs, _crawlCrumbs);
                    continue;
                }
                
                // No known EP. Find the most useful direction to hopefully find one 
                var mostUsefulDirection = MostUsefulDirForLocatingExitPoint(options, _crawlCrumbs);
                if (mostUsefulDirection != null)
                {
                    options = await MakeMove(mostUsefulDirection.Direction, _exitCrumbs, _collectCrumbs, _crawlCrumbs);
                    continue;
                }

                // Back-track if no new directions were found
                if (_crawlCrumbs.Any())
                {
                    var dir = ReverseDir(_crawlCrumbs.Peek());
                    options = await MakeMove(dir, _exitCrumbs, _collectCrumbs, _crawlCrumbs);
                    continue;
                }

                Console.WriteLine("No possible route to Exit Point!");
            }
        }

        private async Task<PossibleActionsAndCurrentScore> CollectScoreInHand(PossibleActionsAndCurrentScore options)
        {
            while (options.CurrentScoreInHand != 0)
            {
                if (options.CanCollectScoreHere)
                {
                    options = (await _client.CollectScore()).PossibleActionsAndCurrentScore;
                    continue;
                }

                // Looking for known route to collection point
                var stack = FindBestCollectStack(_collectCrumbs, _exitCrumbs);
                if (stack != null)
                {
                    var dir = stack.Peek();
                    var step = ReverseDir(dir);
                    options = await MakeMove(step, _exitCrumbs, _collectCrumbs, _crawlCrumbs);
                    continue;
                }
                
                // No known CP. Find the most useful direction to hopefully find one 
                var mostUsefulDirection = MostUsefulDirForLocatingCollectionPoint(options, _crawlCrumbs);
                if (mostUsefulDirection != null)
                {
                    options = await MakeMove(mostUsefulDirection.Direction, _exitCrumbs, _collectCrumbs, _crawlCrumbs);
                    continue;
                }

                // Back-track if no new directions were found
                if (_crawlCrumbs.Any())
                {
                    var dir = ReverseDir(_crawlCrumbs.Peek());
                    options = await MakeMove(dir, _exitCrumbs, _collectCrumbs, _crawlCrumbs);
                    continue;
                }

                Console.WriteLine("No possible route to Collection Point!");
            }

            return options;
        }

        private async Task<PossibleActionsAndCurrentScore> CollectAllPoints(PossibleActionsAndCurrentScore options)
        {
            while (options.CurrentScoreInHand + options.CurrentScoreInBag < _maze.PotentialReward)
            {
                var mostUsefulDirection = MostUsefulDirForCollecting(options, _crawlCrumbs);
                if (mostUsefulDirection != null)
                {
                    options = await MakeMove(mostUsefulDirection.Direction, _exitCrumbs, _collectCrumbs, _crawlCrumbs);
                    continue;
                }
                
                if (_crawlCrumbs.Any())
                {
                    var dir = ReverseDir(_crawlCrumbs.Peek());
                    options = await MakeMove(dir, _exitCrumbs, _collectCrumbs, _crawlCrumbs);
                    continue;
                }

                Console.WriteLine("Stuck while collecting!");
            }

            return options;
        }

        private static Stack<MoveDirection> FindBestCollectStack(List<Stack<MoveDirection>> collectCrumbs,
            List<Stack<MoveDirection>> exitCrumbs)
        {
            var stack = collectCrumbs.OrderBy(st => st.Count).FirstOrDefault();
            return stack;
        }

        private static void TrackExits(PossibleActionsAndCurrentScore options,
            List<Stack<MoveDirection>> exitCrumbs)
        {
            foreach (var dir in options.MoveActions.Where(ma => ma.AllowsExit))
            {
                var stack = new Stack<MoveDirection>();
                stack.Push(ReverseDir(dir.Direction));
                exitCrumbs.Add(stack);
            }
        }

        private static void TrackCollectionPoints(PossibleActionsAndCurrentScore options,
            List<Stack<MoveDirection>> collectCrumbs)
        {
            foreach (var dir in options.MoveActions.Where(ma => ma.AllowsScoreCollection))
            {
                var stack = new Stack<MoveDirection>();
                stack.Push(ReverseDir(dir.Direction));
                collectCrumbs.Add(stack);
            }
        }

        private async Task<PossibleActionsAndCurrentScore> MakeMove(MoveDirection direction,
            List<Stack<MoveDirection>> exitCrumbs, List<Stack<MoveDirection>> collectCrumbs,
            Stack<MoveDirection> crumbs)
        {
            try
            {
                var newOptions = (await _client.Move(direction))
                    .PossibleActionsAndCurrentScore;
                if (newOptions == null)
                    throw new ArgumentException();
                
                // Record the move in all crumbs
                foreach (var st in exitCrumbs)
                    Push(st, direction);

                foreach (var st in collectCrumbs)
                    Push(st, direction);

                Push(crumbs, direction);

                // Check for nearby exits and collectionPoints
                TrackExits(newOptions, _exitCrumbs);
                TrackCollectionPoints(newOptions, _collectCrumbs);

                return newOptions;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }

        static readonly Random RandomGenerator = new Random();
        private readonly MazeInfo _maze;
        private readonly Stack<MoveDirection> _crawlCrumbs = new Stack<MoveDirection>();
        private readonly List<Stack<MoveDirection>> _collectCrumbs = new List<Stack<MoveDirection>>();
        private readonly List<Stack<MoveDirection>> _exitCrumbs = new List<Stack<MoveDirection>>();

        private static MoveAction MostUsefulDirForCollecting(PossibleActionsAndCurrentScore options,
            Stack<MoveDirection> crawlCrumbs)
        {
            var mostUsefulDir = options
                .MoveActions
                .Where(ma => !ma.HasBeenVisited) // Don't ever go where we've been before (backtracking will get us there if needed)
                .OrderBy(_ => 1)
                //.ThenBy(ma => ma.AllowsScoreCollection) // Un-prefer ScoreCollection Points, we'll get there later
                //.ThenBy(ma => ma.AllowsExit) // Un-prefer exits, we'll get there later
                .ThenBy(ma => ma.Reward == 0) // Prefer reward directions over non-reward. It might be the last straw!
                .ThenBy(ma => HugTheLeftWall(ma.Direction, crawlCrumbs))
                //.ThenBy(ma => RandomGenerator.Next())
                .ThenByDescending(ma => ma.Direction) // Prefer Starting Left over Down over Right over Up... no real reason, just for predictability
                .FirstOrDefault();
            return mostUsefulDir;
        }

        private static MoveAction MostUsefulDirForLocatingCollectionPoint(PossibleActionsAndCurrentScore options,
            Stack<MoveDirection> crawlCrumbs)
        {
            var mostUsefulDir = options
                .MoveActions
                .Where(ma => !ma.HasBeenVisited) // Don't ever go where we've been before (backtracking will get us there if needed)
                .OrderBy(ma => ma.AllowsExit) // Un-prefer exits, we'll get there later
                .ThenBy(ma => HugTheLeftWall(ma.Direction, crawlCrumbs))
                .FirstOrDefault();
            return mostUsefulDir;
        }

        private static MoveAction MostUsefulDirForLocatingExitPoint(PossibleActionsAndCurrentScore options,
            Stack<MoveDirection> crawlCrumbs)
        {
            var mostUsefulDir = options
                .MoveActions
                .Where(ma => !ma.HasBeenVisited) // Don't ever go where we've been before (backtracking will get us there if needed)
                .OrderBy(ma => HugTheLeftWall(ma.Direction, crawlCrumbs))
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
                case MoveDirection.Left: return MoveDirection.Right;
                case MoveDirection.Right: return MoveDirection.Left;
                default:
                    throw new ArgumentException("Bad dir");
            }
        }
    }
}