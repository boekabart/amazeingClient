using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Maze
{
    internal class MazeSolver
    {
        private readonly IAmazeingClient _client;

        public MazeSolver( IAmazeingClient client,MazeInfo maze )
        {
            _client = client;
            this._maze = maze;
        }
        public async Task Solve()
        {
            var options = await _client.EnterMaze(_maze.Name);
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
                    options = await _client.CollectScore();
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

        private static Stack<Direction> FindBestCollectStack(List<Stack<Direction>> collectCrumbs,
            List<Stack<Direction>> exitCrumbs)
        {
            var stack = collectCrumbs.OrderBy(st => st.Count).FirstOrDefault();
            return stack;
        }

        private static void TrackExits(PossibleActionsAndCurrentScore options,
            List<Stack<Direction>> exitCrumbs)
        {
            foreach (var dir in options.PossibleMoveActions.Where(ma => ma.AllowsExit))
            {
                var stack = new Stack<Direction>();
                stack.Push(ReverseDir(dir.Direction));
                exitCrumbs.Add(stack);
            }
        }

        private static void TrackCollectionPoints(PossibleActionsAndCurrentScore options,
            List<Stack<Direction>> collectCrumbs)
        {
            foreach (var dir in options.PossibleMoveActions.Where(ma => ma.AllowsScoreCollection))
            {
                var stack = new Stack<Direction>();
                stack.Push(ReverseDir(dir.Direction));
                collectCrumbs.Add(stack);
            }
        }

        private async Task<PossibleActionsAndCurrentScore> MakeMove(Direction direction,
            List<Stack<Direction>> exitCrumbs, List<Stack<Direction>> collectCrumbs,
            Stack<Direction> crumbs)
        {
            try
            {
                var newOptions = await _client.Move(direction);
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
        private readonly Stack<Direction> _crawlCrumbs = new Stack<Direction>();
        private readonly List<Stack<Direction>> _collectCrumbs = new List<Stack<Direction>>();
        private readonly List<Stack<Direction>> _exitCrumbs = new List<Stack<Direction>>();

        private static MoveAction MostUsefulDirForCollecting(PossibleActionsAndCurrentScore options,
            Stack<Direction> crawlCrumbs)
        {
            var mostUsefulDir = options
                .PossibleMoveActions
                .Where(ma => !ma.HasBeenVisited) // Don't ever go where we've been before (backtracking will get us there if needed)
                .OrderBy(_ => 1)
                //.ThenBy(ma => ma.AllowsScoreCollection) // Un-prefer ScoreCollection Points, we'll get there later
                //.ThenBy(ma => ma.AllowsExit) // Un-prefer exits, we'll get there later
                .ThenBy(ma => ma.RewardOnDestination == 0) // Prefer reward directions over non-reward. It might be the last straw!
                .ThenBy(ma => HugTheLeftWall(ma.Direction, crawlCrumbs))
                //.ThenBy(ma => RandomGenerator.Next())
                .ThenByDescending(ma => ma.Direction) // Prefer Starting Left over Down over Right over Up... no real reason, just for predictability
                .FirstOrDefault();
            return mostUsefulDir;
        }

        private static MoveAction MostUsefulDirForLocatingCollectionPoint(PossibleActionsAndCurrentScore options,
            Stack<Direction> crawlCrumbs)
        {
            var mostUsefulDir = options
                .PossibleMoveActions
                .Where(ma => !ma.HasBeenVisited) // Don't ever go where we've been before (backtracking will get us there if needed)
                .OrderBy(ma => ma.AllowsExit) // Un-prefer exits, we'll get there later
                .ThenBy(ma => HugTheLeftWall(ma.Direction, crawlCrumbs))
                .FirstOrDefault();
            return mostUsefulDir;
        }

        private static MoveAction MostUsefulDirForLocatingExitPoint(PossibleActionsAndCurrentScore options,
            Stack<Direction> crawlCrumbs)
        {
            var mostUsefulDir = options
                .PossibleMoveActions
                .Where(ma => !ma.HasBeenVisited) // Don't ever go where we've been before (backtracking will get us there if needed)
                .OrderBy(ma => HugTheLeftWall(ma.Direction, crawlCrumbs))
                .FirstOrDefault();
            return mostUsefulDir;
        }

        private static int HugTheLeftWall(Direction possibleDirection, Stack<Direction> crawlCrumbs)
        {
            if (crawlCrumbs.Count == 0)
                return 0;
            var incomingDirection = crawlCrumbs.Peek();
            return HugTheLeftWall(possibleDirection, incomingDirection);
        }

        static int HugTheLeftWall(Direction possibleDirection, Direction incomingDirection)
        {
            return (5 + possibleDirection - incomingDirection) % 4;
        }

        static void Push(Stack<Direction> stack, Direction dir)
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

        private static Direction ReverseDir(Direction bestDir)
        {
            switch (bestDir)
            {
                case Direction.Down: return Direction.Up;
                case Direction.Up: return Direction.Down;
                case Direction.Left: return Direction.Right;
                case Direction.Right: return Direction.Left;
                default:
                    throw new ArgumentException("Bad dir");
            }
        }
    }
}