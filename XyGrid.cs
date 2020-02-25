using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Maze
{
    internal class XyGrid
    {
        internal class Tile
        {
            protected bool Equals(Tile other)
            {
                return IsExit == other.IsExit && IsCollectionPoint == other.IsCollectionPoint && IsVisited == other.IsVisited && Equals(PossibleDirections, other.PossibleDirections) && Reward == other.Reward;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((Tile) obj);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(IsExit, IsCollectionPoint, IsVisited, PossibleDirections, Reward);
            }

            public bool IsExit { get; }
            public bool IsCollectionPoint { get; }
            public bool IsVisited { get; }
            public ImmutableHashSet<Direction> PossibleDirections { get; }
            public bool Reward { get; private set; }

            public Tile(PossibleActionsAndCurrentScore currentLocation)
            {
                IsExit = currentLocation.CanExitMazeHere;
                IsCollectionPoint = currentLocation.CanCollectScoreHere;
                IsVisited = true;
                PossibleDirections =
                    currentLocation.PossibleMoveActions.Select(ma => ma.Direction).ToImmutableHashSet();
            }

            public Tile(MoveAction moveAction, ImmutableHashSet<Direction> alreadyKnownDirections = null)
            {
                alreadyKnownDirections = alreadyKnownDirections ?? ImmutableHashSet<Direction>.Empty;
                IsExit = moveAction.AllowsExit;
                IsCollectionPoint = moveAction.AllowsScoreCollection;
                IsVisited = moveAction.HasBeenVisited;
                PossibleDirections = alreadyKnownDirections.Add(moveAction.Direction.Reversed());
                Reward = moveAction.RewardOnDestination != 0;
            }

            public static Tile TryMerge(MoveAction moveAction, Tile previousKnownState)
            {
                if (moveAction.AllowsExit != previousKnownState.IsExit
                    || moveAction.AllowsScoreCollection != previousKnownState.IsCollectionPoint
                    /*|| moveAction.HasBeenVisited != previousKnownState.IsVisited*/) // HEY, loop detection
                    return null;
                return new Tile(moveAction, previousKnownState.PossibleDirections);
            }

            public static Tile TryMerge(PossibleActionsAndCurrentScore truth, Tile previousKnownState)
            {
                var newTile = new Tile(truth);
                if (previousKnownState.IsExit != newTile.IsExit
                    || previousKnownState.IsCollectionPoint != newTile.IsCollectionPoint)
                    return null;

                if (previousKnownState.IsVisited)
                {
                    return Match(previousKnownState.PossibleDirections, newTile.PossibleDirections)
                        ? previousKnownState
                        : null;
                }

                // From NOT visited to visited
                return previousKnownState.PossibleDirections.All(newTile.PossibleDirections.Contains) ? newTile : null;
            }

            public static bool Match(ISet<Direction> lhs, ISet<Direction> rhs)
            {
                return lhs.Count == rhs.Count && lhs.All(rhs.Contains);
            }
        }

        public void Draw(string status)
        {
            var offsetX = _dick.Keys.Min(xy => xy.X);
            var offsetY = _dick.Keys.Max(xy => xy.Y);
            Console.Clear();
            Console.Error.WriteLine(status);
            foreach (var kvp in _dick)
            {
                var x = 1 + kvp.Key.X - offsetX;
                var y = 1 + offsetY - kvp.Key.Y;
                
                Console.SetCursorPosition(x, y);
                var tile = kvp.Value;
                Console.ForegroundColor = tile.IsVisited ? ConsoleColor.White : ConsoleColor.DarkGray;
                var teken =tile.IsVisited ? "X": 
                    (HasIslandNeighbor(kvp.Key)?"!":UnvisitedPotential(kvp.Key).ToString());
                Console.Error.Write( $"{teken}");
            }
            Console.SetCursorPosition(1 + CurrentLocation.X - offsetX, 1 + offsetY - CurrentLocation.Y);
        }

        public bool DrawCollection() => Draw(UpdateCollectionRoutes());
        public bool DrawExit() => Draw(UpdateExitRoutes());
        public bool DrawReward() => Draw(UpdateRewardRoutes(CurrentLocation));
        public bool Draw(Dictionary<(int X, int Y),(int Distance, Direction dir)> dick)
        {
            Console.Clear();
            if (!dick.Any())
                return false;
            var offsetX = dick.Keys.Min(xy => xy.X);
            var offsetY = dick.Keys.Max(xy => xy.Y);
            foreach (var kvp in dick)
            {
                var x = 3*(kvp.Key.X - offsetX);
                var y = 1 + offsetY - kvp.Key.Y;
                
                Console.SetCursorPosition(x, y);
                var tile = kvp.Value;
                //Console.ForegroundColor = tile.IsVisited ? ConsoleColor.White : ConsoleColor.DarkGray;
                Console.Error.Write( $"{tile.Distance}");
            }
            Console.SetCursorPosition(0, 4 + offsetY - dick.Keys.Min(xy => xy.Y));
            return true;
        }

        public (int X, int Y) CurrentLocation { get; private set; }
        public bool HasInvalidState { get; private set; }
        
        public void Register(PossibleActionsAndCurrentScore options)
        {
            if (!UpdateCurrent(options))
                return; 

            foreach (var pma in options.PossibleMoveActions)
            {
                if (!UpdateNext(pma))
                    return;
            }
        }

        private Dictionary<(int X, int Y), (int Distance, Direction Direction)> _exitDictionary;
        private Dictionary<(int X, int Y), (int Distance, Direction Direction)> _collectionPointDictionary;
        private Dictionary<(int X, int Y), (int Distance, Direction Direction)> _unvisitedDictionary;
        private Dictionary<(int X, int Y), (int Distance, Direction Direction)> _rewardDictionary;

        public int DistanceToReward(MoveAction ma)
        {
            if (HasInvalidState)
                return int.MaxValue;

            if (ma.RewardOnDestination > 0)
                return 0;

            var location = Moved(ma.Direction);
            var rewardDictionary = UpdateRewardRoutes(CurrentLocation);

            if (!rewardDictionary.TryGetValue(location, value: out var info))
            {
                //Console.WriteLine($"No idea {ma.Direction} {location.X} {location.Y}");
                return int.MaxValue;
            }
            //Console.WriteLine($"{info.Distance} @ {ma.Direction} {location.X} {location.Y}");
                return info.Distance;
        }

        public int DistanceToUnvisited(MoveAction ma)
        {
            if (HasInvalidState)
                return int.MaxValue;

            if (!ma.HasBeenVisited)
                return 0;

            var location = Moved(ma.Direction);
            var unvisitedRoutes = UpdateUnvisitedRoutes(CurrentLocation);

            if (!unvisitedRoutes.TryGetValue(location, value: out var info))
            {
                //Console.WriteLine($"No idea {ma.Direction} {location.X} {location.Y}");
                return int.MaxValue;
            }
            //Console.WriteLine($"{info.Distance} @ {ma.Direction} {location.X} {location.Y}");
            return info.Distance;
        }

        public Direction? ShortestPathToReward()
        {
            if (HasInvalidState)
                return null;

            var rewardDictionary = UpdateRewardRoutes(CurrentLocation);

            return rewardDictionary.TryGetValue(CurrentLocation, out var info)? info.Direction:(Direction?) null;
        }

        private Dictionary<(int X, int Y), (int Distance, Direction Direction)> UpdateRewardRoutes((int X, int Y) location)
        {
            return _rewardDictionary = _rewardDictionary?.ContainsKey(location) ?? false
                ? _rewardDictionary
                : PopulateShortestPathDictionary(_dick.Where(d => d.Value.Reward)
                        .Select(d => d.Key)
                        .ToList());
        }

        private Dictionary<(int X, int Y), (int Distance, Direction Direction)> UpdateUnvisitedRoutes((int X, int Y) location)
        {
            return _unvisitedDictionary = _unvisitedDictionary?.ContainsKey(location) ?? false
                ? _unvisitedDictionary
                : PopulateShortestPathDictionary(_dick.Where(d => !d.Value.IsVisited)
                    .Select(d => d.Key)
                    .ToList());
        }

        private Dictionary<(int X, int Y), (int Distance, Direction Direction)> UpdateExitRoutes()
        {
            return _exitDictionary = _exitDictionary?.ContainsKey(CurrentLocation) ?? false
                ? _exitDictionary
                : PopulateShortestPathDictionary(_dick.Where(d=> d.Value.IsExit)
                    .Select(d => d.Key)
                    .ToList());
        }

        private Dictionary<(int X, int Y), (int Distance, Direction Direction)> UpdateCollectionRoutes()
        {
            return _collectionPointDictionary = _collectionPointDictionary?.ContainsKey(CurrentLocation) ?? false
                ? _collectionPointDictionary
                : PopulateShortestPathDictionary(_dick.Where(d=> d.Value.IsCollectionPoint)
                    .Select(d => d.Key)
                    .ToList());
        }

        public Direction? ShortestPathToCollectionPoint()
        {
            if (HasInvalidState)
                return null;

            var routes = UpdateCollectionRoutes();

            if (!routes.ContainsKey(CurrentLocation))
                return null;

            return routes[CurrentLocation].Direction;
        }

        public Direction? ShortestPathToExit()
        {
            if (HasInvalidState)
                return null;

            var routes = UpdateExitRoutes();

            if (!routes.ContainsKey(CurrentLocation))
                return null;

            return routes[CurrentLocation].Direction;
        }

        private Dictionary<(int X, int Y), (int Distance, Direction Direction)> PopulateShortestPathDictionary(IReadOnlyCollection<(int X, int Y)> targets)
        {
            var routes = InitRoutes(targets);
            var toDo = new Queue<(int X, int Y)>(targets);

            var bestSoFar = int.MaxValue - 1;
            while (toDo.Any())
            {
                var pos = toDo.Dequeue();
                var soFar = routes[pos].Distance;
                var tile = _dick[pos];
                var newDist = soFar + 1;
                if (newDist > bestSoFar + 1)
                    break;
                foreach (var dir in tile.PossibleDirections)
                {
                    var p2 = pos.Moved(dir);
                    if (routes.TryGetValue(p2, out var t2))
                    {
                        if (t2.Distance <= newDist)
                            continue;
                    }

                    routes[p2] = (newDist, dir.Reversed());
                    if (p2 == CurrentLocation)
                        bestSoFar = newDist;

                    toDo.Enqueue(p2);
                }
            }

            return routes;
        }

        private static Dictionary<(int X, int Y), (int Distance, Direction Direction)> InitRoutes(IReadOnlyCollection<(int X, int Y)> targets)
        {
            return targets.ToDictionary(pos => pos, _ => (0, Direction.Down));
        }

        private (int X, int Y) TrackBack(ImmutableStack<Direction> trail)
        {
            var whereTo = CurrentLocation;
            while (!trail.IsEmpty)
            {
                trail = trail.Pop(out var stepTaken);
                whereTo = whereTo.Moved(stepTaken.Reversed());
            }

            return whereTo;
        }

        public int UnvisitedPotential(Direction dir) => UnvisitedPotential(Moved(dir));

        private int UnvisitedPotential((int X, int Y) pos) => HasInvalidState?0:Extensions.AllDirections.Select(d => pos.Moved(d)).Max(HowManyUnknownNeighbours);

        private int HowManyUnknownNeighbours((int X, int Y) pos)
        {
            return HasInvalidState ? 0 : Extensions.AllDirections
                .Select(d => pos.Moved(d))
                .Count(p => !_dick.ContainsKey(p));
        }

        public bool HasIslandNeighbor(Direction dir) => HasIslandNeighbor(Moved(dir));

        private bool HasIslandNeighbor((int X, int Y) pos)
        {
            return !HasInvalidState && Extensions.AllDirections
                       .Select(d => pos.Moved(d))
                       .Where(p => !_dick.ContainsKey(p)) // Unknown location
                       .Any(p => HowManyUnknownNeighbours(p) == 0); // Without any unknown neighbours
        }
        
        private readonly Dictionary<(int X, int Y), Tile> _dick = new Dictionary<(int X, int Y), Tile>();

        private bool UpdateCurrent(PossibleActionsAndCurrentScore options)
        {
            Tile newTile;
            if (_dick.TryGetValue(CurrentLocation, out Tile previousState))
            {
                var newTile1 = new Tile(options);
                if (previousState.IsExit != newTile1.IsExit
                    || previousState.IsCollectionPoint != newTile1.IsCollectionPoint)
                    newTile = null;
                else
                {
                    if (previousState.IsVisited)
                    {
                        newTile = Tile.Match(previousState.PossibleDirections, newTile1.PossibleDirections)
                            ? previousState
                            : null;
                    }
                    else
                    {
                        newTile = previousState.PossibleDirections.All(newTile1.PossibleDirections.Contains) ? newTile1 : null;
                    }

                    // From NOT visited to visited
                }
            }
            else
                newTile = new Tile(options);

            if (newTile == null)
            {
                if (HasInvalidState) return false;
                var message = $"XY-incompatible maze detected. Conflict with known state at {CurrentLocation.X},{CurrentLocation.Y}.";
                if (Global.IsInteractive)
                {
                    Draw(message);
                    Console.ReadKey();
                }

                Console.Error.WriteLine(message);
                HasInvalidState = true;
                return false;
            }

            if (newTile.Equals(previousState))
            {
                //Console.Error.WriteLine($"Same {CurrentLocation.X},{CurrentLocation.Y}");
                return true;
            }
            //Console.Error.WriteLine($"Diff {CurrentLocation.X},{CurrentLocation.Y}");

            _rewardDictionary = _exitDictionary = _collectionPointDictionary = _unvisitedDictionary = null;

            _dick[CurrentLocation] = newTile;
            return true;
        }

        private bool UpdateNext(MoveAction moveAction)
        {
            var location = Moved(moveAction.Direction);
            var newTile = _dick.TryGetValue(location, out Tile previousState)
                ? Tile.TryMerge(moveAction, previousState)
                : moveAction.HasBeenVisited
                    ? new Tile(moveAction) // HEY! That was unexpected!? Portal detected!!
                    : new Tile(moveAction);
            
            if (newTile == null)
            {
                if (HasInvalidState) return false;
                var message =
                    $"XY-incompatible maze detected. Conflict with known state at {location.X},{location.Y} {moveAction.Direction}.";
                if (Global.IsInteractive)
                {
                    Draw(message);
                    Console.ReadKey();
                }

                Console.Error.WriteLine(message);
                HasInvalidState = true;
                return false;
            }

            if (newTile.Equals(previousState))
            {
                //Console.Error.WriteLine($"Same {moveAction.Direction} {location.X},{location.Y}");
                return true;
            }
            //Console.Error.WriteLine($"Diff {moveAction.Direction} {location.X},{location.Y}");

            _rewardDictionary = _exitDictionary = _collectionPointDictionary = _unvisitedDictionary = null;
            _dick[location] = newTile;
            return true;
        }

        private (int X, int Y) Moved(Direction dir) => CurrentLocation.Moved(dir);

        public void RegisterMove(Direction dir) => CurrentLocation = Moved(dir);
    }
}