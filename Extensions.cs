using System;

namespace Maze
{
    internal static class Extensions
    {
        public static Direction Reversed( this Direction bestDir)
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