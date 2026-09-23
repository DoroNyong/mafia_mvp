using System;
using System.Collections.Generic;

namespace MafiaGame.Core
{
    /// <summary>
    /// 게임 페이즈 전이만 담당하는 순수 C# 상태 머신.
    /// 승패 판정, 플레이어 입력 처리는 포함하지 않는다 (GameManager가 호출해 전이시킴).
    /// 향후 서버-권위 멀티플레이 전환 시에도 그대로 재사용 가능하도록 Unity API에 의존하지 않는다.
    /// </summary>
    public class GameStateMachine
    {
        private static readonly Dictionary<GameState, GameState[]> AllowedTransitions =
            new Dictionary<GameState, GameState[]>
            {
                { GameState.Setup, new[] { GameState.Day } },
                { GameState.Day, new[] { GameState.Vote } },
                { GameState.Vote, new[] { GameState.Night, GameState.GameOver } },
                { GameState.Night, new[] { GameState.Day, GameState.GameOver } },
                { GameState.GameOver, new GameState[0] }
            };

        public GameState CurrentState { get; private set; }

        public event Action<GameState, GameState> StateChanged;

        public GameStateMachine(GameState initialState = GameState.Setup)
        {
            CurrentState = initialState;
        }

        public bool CanTransitionTo(GameState nextState)
        {
            return Array.IndexOf(AllowedTransitions[CurrentState], nextState) >= 0;
        }

        /// <summary>
        /// 다음 페이즈로 전이를 시도한다. 허용되지 않는 전이면 false를 반환하고 상태를 바꾸지 않는다.
        /// </summary>
        public bool TryChangeState(GameState nextState)
        {
            if (!CanTransitionTo(nextState))
            {
                return false;
            }

            GameState previousState = CurrentState;
            CurrentState = nextState;
            StateChanged?.Invoke(previousState, nextState);
            return true;
        }
    }
}
