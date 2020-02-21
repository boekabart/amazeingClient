using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Maze
{
    public interface IAmazeingClient
    {
        string BaseUrl { get; set; }
        bool ReadResponseAsString { get; set; }

        Task<ICollection<MazeInfo>> AllMazes();
        Task<ICollection<MazeInfo>> AllMazes(CancellationToken cancellationToken);
        Task<PossibleActionsAndCurrentScore> CollectScore();
        Task<PossibleActionsAndCurrentScore> CollectScore(CancellationToken cancellationToken);
        Task<PossibleActionsAndCurrentScore> EnterMaze(string mazeName);
        Task<PossibleActionsAndCurrentScore> EnterMaze(string mazeName, CancellationToken cancellationToken);
        Task ExitMaze();
        Task ExitMaze(CancellationToken cancellationToken);
        Task ForgetPlayer();
        Task ForgetPlayer(CancellationToken cancellationToken);
        Task<PlayerInfo> GetPlayerInfo();
        Task<PlayerInfo> GetPlayerInfo(CancellationToken cancellationToken);
        Task<PossibleActionsAndCurrentScore> Move(Direction direction);
        Task<PossibleActionsAndCurrentScore> Move(Direction direction, CancellationToken cancellationToken);
        Task<PossibleActionsAndCurrentScore> PossibleActions();
        Task<PossibleActionsAndCurrentScore> PossibleActions(CancellationToken cancellationToken);
        Task RegisterPlayer(string name);
        Task RegisterPlayer(string name, CancellationToken cancellationToken);
    }
}