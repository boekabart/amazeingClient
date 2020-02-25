using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Maze
{
    internal class MazeSolver
    {
        private readonly IAmazeingClient _client;
        private readonly XyGrid _xyGrid = new XyGrid();

        public MazeSolver( IAmazeingClient client,MazeInfo maze )
        {
            _client = client;
            _maze = maze;
        }
        public async Task Solve()
        {
            var options = await _client.EnterMaze(_maze.Name);
            _xyGrid.Register(options);
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
                    if (false && Global.IsInteractive)
                    {
                        _xyGrid.Draw("Finished");
                        Console.ReadKey();
                    }
                    await _client.ExitMaze();
                    
                    return;
                }

                var shortestPath = _xyGrid.ShortestPathToExit();
                if (shortestPath.HasValue)
                {
                    if (false && Global.IsInteractive)
                    {
                        _xyGrid.DrawExit();
                        _xyGrid.Draw($"Direction to exit point: {shortestPath.Value}");
                        Console.ReadKey(true);
                    }
                    options = await MakeMove(shortestPath.Value);

                    continue;
                }

                // Looking for known route to exit point
                var stack = _exitCrumbs.OrderBy(st => st.Count).FirstOrDefault();
                if (stack != null)
                {
                    var dir = stack.Peek();
                    var step = dir.Reversed();
                    options = await MakeMove(step);
                    continue;
                }
                
                // No known EP. Find the most useful direction to hopefully find one 
                var mostUsefulDirection = MostUsefulDirForLocatingExitPoint(options, _lastDir);
                if (mostUsefulDirection != null)
                {
                    options = await MakeMove(mostUsefulDirection.Direction);
                    continue;
                }

                // Back-track if no new directions were found
                if (_crawlCrumbs.Any())
                {
                    var dir = _crawlCrumbs.Peek().Reversed();
                    options = await MakeMove(dir);
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

                var shortestPath = _xyGrid.ShortestPathToCollectionPoint();
                if (shortestPath.HasValue)
                {
                    if (false && Global.IsInteractive)
                    {
                        _xyGrid.Draw($"Direction to collection point: {shortestPath.Value}");
                        Console.ReadKey(true);
                    }
                    options = await MakeMove(shortestPath.Value);

                    continue;
                }

                // Looking for known route to collection point
                var stack = FindBestCollectStack(_collectCrumbs, _exitCrumbs);
                if (stack != null)
                {
                    var dir = stack.Peek();
                    var step = dir.Reversed();
                    options = await MakeMove(step);
                    continue;
                }
                
                // No known CP. Find the most useful direction to hopefully find one 
                var mostUsefulDirection = MostUsefulDirForLocatingCollectionPoint(options, _lastDir);
                if (mostUsefulDirection != null)
                {
                    options = await MakeMove(mostUsefulDirection.Direction);
                    continue;
                }

                // Back-track if no new directions were found
                if (_crawlCrumbs.Any())
                {
                    var dir = _crawlCrumbs.Peek().Reversed();
                    options = await MakeMove(dir);
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
                var mostUsefulDirection = MostUsefulDirForCollecting(options, _lastDir);
                options = await MakeMove(mostUsefulDirection.Direction);
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
                stack.Push(dir.Direction.Reversed());
                exitCrumbs.Add(stack);
            }
        }

        private static void TrackCollectionPoints(PossibleActionsAndCurrentScore options,
            List<Stack<Direction>> collectCrumbs)
        {
            foreach (var dir in options.PossibleMoveActions.Where(ma => ma.AllowsScoreCollection))
            {
                var stack = new Stack<Direction>();
                stack.Push(dir.Direction.Reversed());
                collectCrumbs.Add(stack);
            }
        }

        private async Task<PossibleActionsAndCurrentScore> MakeMove(Direction direction)
        {
            try
            {
                var newOptions = await _client.Move(direction);
                if (newOptions == null)
                    throw new ArgumentException();
                _xyGrid.RegisterMove(direction);
                _xyGrid.Register(newOptions);
                
                // Record the move in all crumbs
                foreach (var st in _exitCrumbs)
                    Push(st, direction);

                foreach (var st in _collectCrumbs)
                    Push(st, direction);

                Push(_crawlCrumbs, direction);

                _lastDir = direction;

                // Check for nearby exits and collectionPoints
                TrackExits(newOptions, _exitCrumbs);
                TrackCollectionPoints(newOptions, _collectCrumbs);
                
                //_xyGrid.Draw();
                //Console.ReadKey(true);

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
        private Direction? _lastDir;

        private MoveAction MostUsefulDirForCollecting(PossibleActionsAndCurrentScore options,
            Direction? lastDir)
        {
            var mostUsefulDir = options
                .PossibleMoveActions
                .OrderBy(_ => 1)
                //.OrderBy(ma => ma.HasBeenVisited) // Prefer mainly to boldly go where no-one, etc
                //.ThenBy(ma => ma.AllowsScoreCollection) // Un-prefer ScoreCollection Points, we'll get there later
                //.ThenBy(ma => ma.AllowsExit) // Un-prefer exits, we'll get there later
                .ThenBy(ma => ma.RewardOnDestination == 0) // Prefer reward directions over non-reward. It might be the last straw!
                .ThenBy(ma => _xyGrid.DistanceToReward(ma)) // Never helps for unvisited tiles. same as previous line...
                .ThenBy(ma => _xyGrid.DistanceToUnvisited(ma)) // Instead of backtracking?
                //.ThenByDescending(HasIslandNeighbour) // prefer tiles that will lead to completion of an unknown island - Useful for left/right hugging
                //.ThenByDescending(HowManyUnknownNeighbours) 
                .ThenByDescending(UnvisitedPotential) 
                //.ThenBy(ma => HugTheRightWall(ma.Direction, _lastDir))
                .ThenBy(GoStraight) // Go Straight!
                .ThenBy(ma => HugTheLeftWall(ma.Direction, _lastDir))
                //.ThenBy(ma => RandomGenerator.Next())
                .ThenByDescending(ma => ma.Direction) // Prefer Starting Left over Down over Right over Up... no real reason, just for predictability
                .FirstOrDefault();
            return mostUsefulDir;
        }

        private bool GoStraight(MoveAction ma)
        {
            return _lastDir.HasValue && _lastDir != ma.Direction;
        }

        private int HowManyUnknownNeighbours(MoveAction ma)
        {
            // This is useful for Sparse mazes. Between Scoring tiles that must be visited anyway, this potential shouldn't be a factor 
            return ma.RewardOnDestination != 0
                ? 0
                : _xyGrid.HowManyUnknownNeighbours(ma.Direction);
        }

        private int UnvisitedPotential(MoveAction ma)
        {
            // This is useful for Sparse mazes. Between Scoring tiles that must be visited anyway, this potential shouldn't be a factor 
            return ma.RewardOnDestination != 0
                ? 0
                : _xyGrid.UnvisitedPotential(ma.Direction);
        }

        private bool HasIslandNeighbour(MoveAction ma)
        {
            // This is useful for Sparse mazes. Between Scoring tiles that must be visited anyway, this potential shouldn't be a factor 
            return ma.RewardOnDestination != 0 || _xyGrid.HasIslandNeighbor(ma.Direction);
        }

        private static MoveAction MostUsefulDirForLocatingCollectionPoint(PossibleActionsAndCurrentScore options,
            Direction? lastDir)
        {
            var mostUsefulDir = options
                .PossibleMoveActions
                .Where(ma =>
                    !ma.HasBeenVisited) // Don't ever go where we've been before (backtracking will get us there if needed)
                .OrderBy(ma => ma.AllowsExit) // Un-prefer exits, we'll get there later
                .ThenBy(ma => HugTheLeftWall(ma.Direction, lastDir))
                .FirstOrDefault();
            return mostUsefulDir;
        }

        private static MoveAction MostUsefulDirForLocatingExitPoint(PossibleActionsAndCurrentScore options,
            Direction? lastDir)
        {
            var mostUsefulDir = options
                .PossibleMoveActions
                .Where(ma =>
                    !ma.HasBeenVisited) // Don't ever go where we've been before (backtracking will get us there if needed)
                .OrderBy(ma => HugTheLeftWall(ma.Direction, lastDir))
                .FirstOrDefault();
            return mostUsefulDir;
        }

        private static int HugTheLeftWall(Direction possibleDirection, Direction? incomingDirection)
        {
            return !incomingDirection.HasValue ? 0 : HugTheLeftWall(possibleDirection, incomingDirection.Value);
        }

        static int HugTheLeftWall(Direction possibleDirection, Direction incomingDirection)
        {
            return (5 + possibleDirection - incomingDirection) % 4;
        }

        private static int HugTheRightWall(Direction possibleDirection, Direction? incomingDirection)
        {
            return !incomingDirection.HasValue ? 0 : HugTheRightWall(possibleDirection, incomingDirection.Value);
        }

        static int HugTheRightWall(Direction possibleDirection, Direction incomingDirection)
        {
            return (5 + incomingDirection - possibleDirection) % 4;
        }

        static void Push(Stack<Direction> stack, Direction dir)
        {
            if (stack.Count == 0)
            {
                stack.Push(dir);
                return;
            }

            if (stack.Count != 0 && stack.Peek() == dir.Reversed())
                stack.Pop();
            else
                stack.Push(dir);
        }
    }
}