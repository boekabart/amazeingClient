using System.Threading.Tasks;
using AmazeingEvening;
using Grpc.Core;

namespace Maze
{
    internal class SimpleMazeClient : IMazeClient
    {
        private readonly AmazeingEvening.Maze.MazeClient _mazeClient;
        private readonly CallOptions _grpcCallOptions;

        public SimpleMazeClient(AmazeingEvening.Maze.MazeClient mazeClient, CallOptions grpcCallOptions)
        {
            _mazeClient = mazeClient;
            _grpcCallOptions = grpcCallOptions;
        }

        public Task<EnterMazeResponse> EnterMaze(MazeInfo maze) => _mazeClient.EnterMazeAsync(new EnterMazeRequest {MazeName = maze.Name}, _grpcCallOptions).ResponseAsync;
        public Task<ExitMazeResponse> ExitMaze() => _mazeClient.ExitMazeAsync(new ExitMazeRequest(), _grpcCallOptions).ResponseAsync;
        public Task<CollectScoreResponse> CollectScore() => _mazeClient.CollectScoreAsync(new CollectScoreRequest(), _grpcCallOptions).ResponseAsync;
        public Task<MoveResponse> Move(MoveDirection direction) => _mazeClient.MoveAsync(new MoveRequest {Direction = direction}, _grpcCallOptions).ResponseAsync;
    }
}