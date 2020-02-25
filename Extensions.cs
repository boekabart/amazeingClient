using System;
using System.Collections;
using System.Collections.Generic;

namespace Maze
{
    internal static class Extensions
    {
        public static Direction Reversed(this Direction bestDir)
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

        internal static (int X, int Y) Moved(this (int X, int Y) location, Direction dir)
        {
            switch (dir)
            {
                case Direction.Up:
                    return (location.X, location.Y + 1);
                case Direction.Right:
                    return (location.X + 1, location.Y);
                case Direction.Down:
                    return (location.X, location.Y - 1);
                case Direction.Left:
                    return (location.X - 1, location.Y);
                default:
                    throw new ArgumentOutOfRangeException(nameof(dir), dir, null);
            }
        }

        internal static IEnumerable<Direction> AllDirections
        {
            get
            {
                yield return Direction.Up;
                yield return Direction.Down;
                yield return Direction.Left;
                yield return Direction.Right;
            }
        }
    }
}
