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

            while (true)
            {
                //await Task.Delay(10);
                TrackExits(options, _exitCrumbs);
                if (options.CurrentScoreInHand == 0 && options.CurrentScoreInBag >= _maze.PotentialReward)
                {
                    // Looking for exit!
                    if (options.CanExitMazeHere)
                    {
                        //Console.WriteLine("Enter please");
                        //Console.ReadLine();
                        await _client.ExitMaze();
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
                    options = await MakeMove(step, _exitCrumbs, _collectCrumbs, _crawlCrumbs);
                    continue;
                }

                TrackCollectionPoints(options, _collectCrumbs);
                if (options.CurrentScoreInHand + options.CurrentScoreInBag >= _maze.PotentialReward)
                {
                    if (options.CanCollectScoreHere)
                    {
                        options = (await _client.CollectScore())
                            .PossibleActionsAndCurrentScore;
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
                    options = await MakeMove(step, _exitCrumbs, _collectCrumbs, _crawlCrumbs);
                    continue;
                }

                // Collecting
                var mostUsefulDirection = MostUsefulDir(options, _crawlCrumbs);
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

                Console.WriteLine("Stuck!");
            }
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
                var retVal = (await _client.Move(direction))
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
        private readonly MazeInfo _maze;
        private readonly Stack<MoveDirection> _crawlCrumbs = new Stack<MoveDirection>();
        private readonly List<Stack<MoveDirection>> _collectCrumbs = new List<Stack<MoveDirection>>();
        private readonly List<Stack<MoveDirection>> _exitCrumbs = new List<Stack<MoveDirection>>();

        private static MoveAction MostUsefulDir(PossibleActionsAndCurrentScore options,
            Stack<MoveDirection> crawlCrumbs)
        {
            var mostUsefulDir = options
                .MoveActions
                .Where(ma =>
                    !ma.HasBeenVisited) // Don't ever go where we've been before (backtracking will get us there if needed)
                .OrderBy(_ => 1)
                .ThenBy(ma => ma.AllowsScoreCollection) // Un-prefer ScoreCollection Points, we'll get there later
                .ThenBy(ma => ma.AllowsExit) // Un-prefer exits, we'll get there later
                .ThenBy(ma => HugTheLeftWall(ma.Direction, crawlCrumbs))
                //.ThenBy(ma => RandomGenerator.Next())
                .ThenByDescending(ma =>
                    ma.Direction) // Prefer Left over Down over Right over Up... no real reason, just for predictability
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
                case MoveDirection.Left: return MoveDirection.Right;
                case MoveDirection.Right: return MoveDirection.Left;
                default:
                    throw new ArgumentException("Bad dir");
            }
        }
    }
}