using System.Threading.Tasks;
using AmazeingEvening;

namespace Maze
{
    internal interface IMazeClient
    {
        Task<EnterMazeResponse> EnterMaze(MazeInfo maze);
        Task<ExitMazeResponse> ExitMaze();
        Task<CollectScoreResponse> CollectScore();
        Task<MoveResponse> Move(MoveDirection direction);
    }
}