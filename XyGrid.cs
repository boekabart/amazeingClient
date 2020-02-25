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
            public bool IsExit { get; }
            public bool IsCollectionPoint { get; }
            public bool IsVisited { get; }
            public ImmutableHashSet<Direction> PossibleDirections { get; }

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

        public (int X, int Y) CurrentLocation { get; private set; }
        public bool InvalidState { get; private set; }
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

        public int UnvisitedPotential(Direction dir) => UnvisitedPotential(Moved(dir));

        private int UnvisitedPotential((int X, int Y) pos) => InvalidState?0:Extensions.AllDirections.Select(d => pos.Moved(d)).Max(HowManyUnknownNeighbours);

        private int HowManyUnknownNeighbours((int X, int Y) pos)
        {
            return InvalidState ? 0 : Extensions.AllDirections
                .Select(d => pos.Moved(d))
                .Count(p => !_dick.ContainsKey(p));
        }

        public bool HasIslandNeighbor(Direction dir) => HasIslandNeighbor(Moved(dir));

        private bool HasIslandNeighbor((int X, int Y) pos)
        {
            return !InvalidState && Extensions.AllDirections
                       .Select(d => pos.Moved(d))
                       .Where(p => !_dick.ContainsKey(p)) // Unknown location
                       .Any(p => HowManyUnknownNeighbours(p) == 0); // Without any unknown neighbours
        }
        
        private readonly Dictionary<(int X, int Y), Tile> _dick = new Dictionary<(int X, int Y), Tile>();

        private bool UpdateCurrent(PossibleActionsAndCurrentScore options)
        {
            var newTile = _dick.TryGetValue(CurrentLocation, out Tile previousState)
                ? Tile.TryMerge(options, previousState)
                : new Tile(options);
            
            if (newTile == null)
            {
                if (InvalidState) return false;
                var message = $"XY-incompatible maze detected. Conflict with known state at {CurrentLocation.X},{CurrentLocation.Y}.";
                if (Global.IsInteractive)
                {
                    Draw(message);
                    Console.ReadKey();
                }

                Console.Error.WriteLine(message);
                InvalidState = true;
                return false;
            }

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
                if (InvalidState) return false;
                var message =
                    $"XY-incompatible maze detected. Conflict with known state at {location.X},{location.Y} {moveAction.Direction}.";
                if (Global.IsInteractive)
                {
                    Draw(message);
                    Console.ReadKey();
                }

                Console.Error.WriteLine(message);
                InvalidState = true;
                return false;
            }

            _dick[location] = newTile;
            return true;
        }

        private (int X, int Y) Moved(Direction dir) => CurrentLocation.Moved(dir);

        public void RegisterMove(Direction dir) => CurrentLocation = Moved(dir);
    }
}