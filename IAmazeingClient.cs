using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Maze
{
    public interface IAmazeingClient
    {
        Task<ICollection<MazeInfo>> AllMazes();
        Task<PossibleActionsAndCurrentScore> CollectScore();
        Task<PossibleActionsAndCurrentScore> EnterMaze(string mazeName);
        Task ExitMaze();
        Task ForgetPlayer();
        Task<PlayerInfo> GetPlayerInfo();
        Task<PossibleActionsAndCurrentScore> Move(Direction direction);
        Task<PossibleActionsAndCurrentScore> PossibleActions();
        Task RegisterPlayer(string name);
    }
}