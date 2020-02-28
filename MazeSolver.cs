using System;
using System.Collections.Generic;
using System.Linq;
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

            options = await FindAllRewards(options);
            options = await GoToTheBank(options);
            await GoToExit(options);
            DrawMaze("Finished!");
        }

        private async Task GoToExit(PossibleActionsAndCurrentScore options)
        {
            DrawMaze("Going to the Exit", () => _xyGrid.DrawExit(), () => _xyGrid.DrawCollection());
            while (true)
            {
                // Looking for exit!
                if (options.CanExitMazeHere)
                {
                    await _client.ExitMaze();
                    return;
                }
                
                var mostUsefulDirection = MostUsefulDirForLocatingExitPoint(options);
                options = await MakeMove(mostUsefulDirection.Direction);
            }
        }

        private MoveAction MostUsefulDirForLocatingExitPoint(PossibleActionsAndCurrentScore options)
        {
            var mostUsefulDir = options
                .PossibleMoveActions
                .WithTheSmallest(DistanceToExit)
                .WithTheSmallest(DistanceToUnvisited)
                .ThenBy(PreferTurningRight)
                .FirstOrDefault();
            return mostUsefulDir;
        }


        private async Task<PossibleActionsAndCurrentScore> GoToTheBank(PossibleActionsAndCurrentScore options)
        {
            DrawMaze("Going to the Bank", () => _xyGrid.DrawCollection());

            while (options.CurrentScoreInHand != 0)
            {
                if (options.CanCollectScoreHere)
                {
                    options = await _client.CollectScore();
                    continue;
                }

                var mostUsefulDirection = MostUsefulDirForGoingToTheBank(options);
                options = await MakeMove(mostUsefulDirection.Direction);
            }

            return options;
        }

        private  MoveAction MostUsefulDirForGoingToTheBank(PossibleActionsAndCurrentScore options)
        {
            var mostUsefulDir = options
                .PossibleMoveActions
                .WithTheSmallest(DistanceToCollectionPoint)
                .WithTheSmallest(DistanceToUnvisited)
                .ThenBy(PreferTurningRight)
                .FirstOrDefault();
            return mostUsefulDir;
        }

        private async Task<PossibleActionsAndCurrentScore> FindAllRewards(PossibleActionsAndCurrentScore options)
        {
            while (options.CurrentScoreInHand + options.CurrentScoreInBag < _maze.PotentialReward)
            {
                var mostUsefulDirection = MostUsefulDirForGathering(options);
                options = await MakeMove(mostUsefulDirection.Direction);
            }

            return options;
            
        }

        private MoveAction MostUsefulDirForGathering(PossibleActionsAndCurrentScore options)
        {
            var mostUsefulDir = options
                .PossibleMoveActions
                .Prefer(HasReward) // Prefer reward directions over non-reward. It might be the last straw!
                .WithTheSmallest(DistanceToReward) // Never helps for unvisited tiles. same as previous line...
                .WithTheSmallest(DistanceToUnvisited) // Instead of backtracking?
                .Prefer(HasIslandNeighbour) // prefer tiles that will lead to completion of an unknown island - Useful for left/right 'wall' hugging
                .WithTheMost(UnvisitedPotential)
                .ThenBy(PreferTurningLeft)
                .ThenByDescending(LeftDownRightUp) // Prefer Starting Left over Down over Right over Up... no real reason, just for predictability
                .FirstOrDefault();
            return mostUsefulDir;
        }

        private void DrawMaze(string reason, params Action[] andThis)
        {
            if (Global.IsInteractive)
            {
                _xyGrid.Draw(reason);
                Console.ReadKey(true);
                foreach (var action in andThis)
                {
                    action.Invoke();
                    Console.ReadKey(true);
                }
            }
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

        private readonly MazeInfo _maze;
        private readonly Stack<Direction> _crawlCrumbs = new Stack<Direction>();
        private readonly List<Stack<Direction>> _collectCrumbs = new List<Stack<Direction>>();
        private readonly List<Stack<Direction>> _exitCrumbs = new List<Stack<Direction>>();
        private Direction? _lastDir;

        private Direction LeftDownRightUp(MoveAction ma) => ma.Direction;

        private int DistanceToUnvisited(MoveAction ma) => _xyGrid.DistanceToUnvisited(ma);

        private int DistanceToReward(MoveAction ma) => _xyGrid.DistanceToReward(ma);

        private int DistanceToCollectionPoint(MoveAction ma) => _xyGrid.DistanceToCollectionPoint(ma);

        private int DistanceToExit(MoveAction ma) => _xyGrid.DistanceToExit(ma);

        private bool HasReward(MoveAction ma) => ma.RewardOnDestination != 0;

        private bool GoStraight(MoveAction ma) => _lastDir.HasValue && _lastDir == ma.Direction;

        private int UnvisitedPotential(MoveAction ma)
        {
            // This is useful for Sparse mazes. Between Scoring tiles that must be visited anyway, this potential shouldn't be a factor 
            return HasReward(ma) ? 0 : _xyGrid.UnvisitedPotential(ma.Direction);
        }

        private bool HasIslandNeighbour(MoveAction ma)
        {
            // This is useful for Sparse mazes. Between Scoring tiles that must be visited anyway, this shouldn't be a factor 
            return HasReward(ma) || _xyGrid.HasIslandNeighbor(ma.Direction);
        }

        private int PreferTurningLeft(MoveAction moveAction)
        {
            return !_lastDir.HasValue ? 0 : PreferTurningLeft(moveAction.Direction, _lastDir.Value);
        }

        private int PreferTurningRight(MoveAction moveAction)
        {
            return !_lastDir.HasValue ? 0 : PreferTurningRight(moveAction.Direction, _lastDir.Value);
        }

        static int PreferTurningLeft(Direction possibleDirection, Direction incomingDirection)
        {
            return (5 + possibleDirection - incomingDirection) % 4;
        }

        static int PreferTurningRight(Direction possibleDirection, Direction incomingDirection)
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