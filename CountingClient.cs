using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HightechICT.Amazeing.Client.Rest;

namespace Maze
{
    internal class CountingClient : IAmazeingClient
    {
        private readonly IAmazeingClient _amazeingClientImplementation;

        public CountingClient(IAmazeingClient amazeingClientImplementation)
        {
            _amazeingClientImplementation = amazeingClientImplementation;
        }

        public int Invocations { get; private set;  }
        public async Task<ICollection<MazeInfo>> AllMazes()
        {
            IncreaseInvocationCount();
            return await _amazeingClientImplementation.AllMazes();
        }

        private void IncreaseInvocationCount() => Invocations++;

        public async Task<PossibleActionsAndCurrentScore> CollectScore()
        {
            IncreaseInvocationCount();
            return await _amazeingClientImplementation.CollectScore();
        }

        public async Task<PossibleActionsAndCurrentScore> EnterMaze(string mazeName)
        {
            IncreaseInvocationCount();
            return await _amazeingClientImplementation.EnterMaze(mazeName);
        }

        public async Task ExitMaze()
        {
            IncreaseInvocationCount();
            await _amazeingClientImplementation.ExitMaze();
        }

        public async Task ForgetPlayer()
        {
            IncreaseInvocationCount();
            await _amazeingClientImplementation.ForgetPlayer();
        }

        public async Task<PlayerInfo> GetPlayerInfo()
        {
            IncreaseInvocationCount();
            return await _amazeingClientImplementation.GetPlayerInfo();
        }

        public async Task<PossibleActionsAndCurrentScore> Move(Direction direction)
        {
            IncreaseInvocationCount();
            return await _amazeingClientImplementation.Move(direction);
        }

        public async Task<PossibleActionsAndCurrentScore> PossibleActions()
        {
            IncreaseInvocationCount();
            return await _amazeingClientImplementation.PossibleActions();
        }

        public async Task RegisterPlayer(string name)
        {
            IncreaseInvocationCount();
            await _amazeingClientImplementation.RegisterPlayer(name);
        }
   }
}