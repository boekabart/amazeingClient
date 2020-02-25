using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Drawing;
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
                    || moveAction.HasBeenVisited != previousKnownState.IsVisited)
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

        public void Draw()
        {
            var offsetX = _dick.Keys.Min(xy => xy.X);
            var offsetY = _dick.Keys.Max(xy => xy.Y);
            Console.Clear();
            foreach (var kvp in _dick)
            {
                var x = 1 + kvp.Key.X - offsetX;
                var y = 1 + offsetY - kvp.Key.Y;
                
                Console.SetCursorPosition(x, y);
                var tile = kvp.Value;
                Console.ForegroundColor = tile.IsVisited ? ConsoleColor.White : ConsoleColor.DarkGray;
                Console.Write( "X");
            }
            Console.SetCursorPosition(1 + X - offsetX, 1 + offsetY - Y);
        }

        public (int X, int Y) CurrentLocation { get; private set; }
        public int X => CurrentLocation.X;
        public int Y => CurrentLocation.Y;
        public bool InvalidState { get; private set; }
        public void Register(PossibleActionsAndCurrentScore options)
        {
            if (InvalidState) return;
            if (!UpdateCurrent(options))
                return; 

            foreach (var pma in options.PossibleMoveActions)
            {
                if (!UpdateNext(pma))
                    return;
            }
        }

        public int SeenTile(Direction dir) => InvalidState ? 0 :
            _dick.TryGetValue(Moved(dir), out var tile) ? tile.PossibleDirections.Count : 0;
        
        private readonly Dictionary<(int X, int Y), Tile> _dick = new Dictionary<(int X, int Y), Tile>();

        private bool UpdateCurrent(PossibleActionsAndCurrentScore options)
        {
            var newTile = _dick.TryGetValue(CurrentLocation, out Tile previousState)
                ? Tile.TryMerge(options, previousState)
                : new Tile(options);
            
            if (newTile == null)
            {
//                Draw();
//                Console.ReadKey();

                Console.Error.WriteLine($"XY-incompatible maze detected. Conflict with known state at {CurrentLocation.X},{CurrentLocation.Y}.");
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
                    ? null
                    : new Tile(moveAction);
            
            if (newTile == null)
            {
//                Draw();
//                Console.ReadKey();

                Console.Error.WriteLine($"XY-incompatible maze detected. Conflict with known state at {location.X},{location.Y} {moveAction.Direction}.");
                InvalidState = true;
                return false;
            }

            _dick[location] = newTile;
            return true;
        }

        private (int X, int Y) Moved(Direction dir)
        {
            switch(dir)
            {
                case Direction.Up:
                    return (X, Y + 1);
                case Direction.Right:
                    return (X + 1, Y);
                case Direction.Down:
                    return (X, Y - 1);
                case Direction.Left:
                    return (X - 1, Y);
                default:
                    throw new ArgumentOutOfRangeException(nameof(dir), dir, null);
            }
        }

        public void RegisterMove(Direction dir) => CurrentLocation = Moved(dir);
    }
}