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

        public Task<ICollection<MazeInfo>> AllMazes(CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        private void IncreaseInvocationCount() => Invocations++;

        public async Task<PossibleActionsAndCurrentScore> CollectScore()
        {
            IncreaseInvocationCount();
            return await _amazeingClientImplementation.CollectScore();
        }

        public Task<PossibleActionsAndCurrentScore> CollectScore(CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public async Task<PossibleActionsAndCurrentScore> EnterMaze(string mazeName)
        {
            IncreaseInvocationCount();
            return await _amazeingClientImplementation.EnterMaze(mazeName);
        }

        public Task<PossibleActionsAndCurrentScore> EnterMaze(string mazeName, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public async Task ExitMaze()
        {
            IncreaseInvocationCount();
            await _amazeingClientImplementation.ExitMaze();
        }

        public Task ExitMaze(CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public async Task ForgetPlayer()
        {
            IncreaseInvocationCount();
            await _amazeingClientImplementation.ForgetPlayer();
        }

        public Task ForgetPlayer(CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public async Task<PlayerInfo> GetPlayerInfo()
        {
            IncreaseInvocationCount();
            return await _amazeingClientImplementation.GetPlayerInfo();
        }

        public Task<PlayerInfo> GetPlayerInfo(CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public async Task<PossibleActionsAndCurrentScore> Move(Direction direction)
        {
            IncreaseInvocationCount();
            return await _amazeingClientImplementation.Move(direction);
        }

        public Task<PossibleActionsAndCurrentScore> Move(Direction direction, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public async Task<PossibleActionsAndCurrentScore> PossibleActions()
        {
            IncreaseInvocationCount();
            return await _amazeingClientImplementation.PossibleActions();
        }

        public Task<PossibleActionsAndCurrentScore> PossibleActions(CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public async Task RegisterPlayer(string name)
        {
            IncreaseInvocationCount();
            await _amazeingClientImplementation.RegisterPlayer(name);
        }

        public Task RegisterPlayer(string name, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public string BaseUrl
        {
            get => _amazeingClientImplementation.BaseUrl;
            set => _amazeingClientImplementation.BaseUrl = value;
        }

        public bool ReadResponseAsString
        {
            get => _amazeingClientImplementation.ReadResponseAsString;
            set => _amazeingClientImplementation.ReadResponseAsString = value;
        }
    }
}