using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MafiaGame.Core
{
    /// <summary>
    /// 게임 판정 로직(페이즈 진행, 투표 집계, 승패 판정)의 단일 진입점.
    /// 플레이어 입력/선택 로직(예: UI, 향후 AI 컨트롤러)은 이 클래스를 통해서만 게임 상태를 바꾼다.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public const int HumanPlayerId = 0;

        [SerializeField] private float dayDuration = 30f;
        [SerializeField] private float voteDuration = 15f;
        [SerializeField] private float nightDuration = 10f;

        private readonly GameStateMachine stateMachine = new GameStateMachine();
        private readonly List<PlayerData> players = new List<PlayerData>();
        private readonly Dictionary<int, int> votes = new Dictionary<int, int>();
        private readonly Dictionary<int, int> nightActions = new Dictionary<int, int>();

        private float remainingTime;
        private int lastReportedSeconds = -1;
        private bool phaseTimerActive;

        public GameState CurrentState => stateMachine.CurrentState;
        public IReadOnlyList<PlayerData> Players => players;

        public event Action<GameState> OnPhaseChanged;
        public event Action<int> OnTimerChanged;
        public event Action<IReadOnlyList<PlayerData>> OnPlayersChanged;
        public event Action<string> OnLogMessage;

        private void Start()
        {
            CreatePlayers();
            OnPlayersChanged?.Invoke(players);
            OnLogMessage?.Invoke("게임을 시작합니다. 역할이 배정되었습니다.");
            ChangePhase(GameState.Day);
        }

        private void Update()
        {
            if (!phaseTimerActive)
            {
                return;
            }

            remainingTime -= Time.deltaTime;
            int wholeSeconds = Mathf.Max(Mathf.CeilToInt(remainingTime), 0);
            if (wholeSeconds != lastReportedSeconds)
            {
                lastReportedSeconds = wholeSeconds;
                OnTimerChanged?.Invoke(wholeSeconds);
            }

            if (remainingTime <= 0f)
            {
                phaseTimerActive = false;
                HandlePhaseTimerExpired();
            }
        }

        /// <summary>
        /// UI/AI 컨트롤러가 투표를 제출하는 유일한 진입점. 유효성 판정은 여기서만 수행한다.
        /// </summary>
        public void SubmitVote(int voterId, int targetId)
        {
            if (CurrentState != GameState.Vote)
            {
                return;
            }

            PlayerData voter = GetPlayer(voterId);
            PlayerData target = GetPlayer(targetId);
            if (voter == null || !voter.IsAlive || target == null || !target.IsAlive)
            {
                return;
            }

            votes[voterId] = targetId;
            OnLogMessage?.Invoke($"{voter.Name} -> {target.Name} 투표");

            int aliveCount = players.Count(p => p.IsAlive);
            if (votes.Count >= aliveCount)
            {
                phaseTimerActive = false;
                ResolveVotePhase();
            }
        }

        /// <summary>
        /// 마피아(처치 대상)/경찰(조사 대상)이 밤 능력 대상을 제출하는 유일한 진입점.
        /// 시민은 밤에 수행할 능력이 없으므로 항상 무시된다.
        /// </summary>
        public void SubmitNightAction(int actorId, int targetId)
        {
            if (CurrentState != GameState.Night)
            {
                return;
            }

            PlayerData actor = GetPlayer(actorId);
            PlayerData target = GetPlayer(targetId);
            if (actor == null || !actor.IsAlive || target == null || !target.IsAlive)
            {
                return;
            }

            if (actor.Role != Role.Mafia && actor.Role != Role.Police)
            {
                return;
            }

            if (actor.Role == Role.Mafia && target.Role == Role.Mafia)
            {
                return;
            }

            if (actor.Role == Role.Police && target.Id == actor.Id)
            {
                return;
            }

            nightActions[actorId] = targetId;
            OnLogMessage?.Invoke($"{actor.Name}이(가) 밤 행동을 선택했습니다.");

            int requiredActors = players.Count(p => p.IsAlive && (p.Role == Role.Mafia || p.Role == Role.Police));
            if (requiredActors > 0 && nightActions.Count >= requiredActors)
            {
                phaseTimerActive = false;
                ResolveNightPhase();
            }
        }

        private void CreatePlayers()
        {
            var roles = new List<Role> { Role.Mafia, Role.Police, Role.Citizen, Role.Citizen };
            for (int i = roles.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (roles[i], roles[j]) = (roles[j], roles[i]);
            }

            players.Add(new PlayerData(HumanPlayerId, "Player", roles[0], isAI: false));
            for (int i = 1; i <= 3; i++)
            {
                players.Add(new PlayerData(i, $"AI {i}", roles[i], isAI: true));
            }
        }

        private PlayerData GetPlayer(int id)
        {
            return players.FirstOrDefault(p => p.Id == id);
        }

        private void ChangePhase(GameState nextState)
        {
            if (!stateMachine.TryChangeState(nextState))
            {
                return;
            }

            OnPhaseChanged?.Invoke(nextState);
            OnLogMessage?.Invoke($"[페이즈] {nextState} 시작");

            switch (nextState)
            {
                case GameState.Day:
                    StartPhaseTimer(dayDuration);
                    break;
                case GameState.Vote:
                    votes.Clear();
                    StartPhaseTimer(voteDuration);
                    break;
                case GameState.Night:
                    nightActions.Clear();
                    StartPhaseTimer(nightDuration);
                    break;
                case GameState.GameOver:
                    phaseTimerActive = false;
                    break;
            }
        }

        private void StartPhaseTimer(float duration)
        {
            remainingTime = duration;
            lastReportedSeconds = -1;
            phaseTimerActive = true;
        }

        private void HandlePhaseTimerExpired()
        {
            switch (CurrentState)
            {
                case GameState.Day:
                    ChangePhase(GameState.Vote);
                    break;
                case GameState.Vote:
                    ResolveVotePhase();
                    break;
                case GameState.Night:
                    ResolveNightPhase();
                    break;
            }
        }

        private void ResolveVotePhase()
        {
            AutoCastAIVotes();

            if (votes.Count > 0)
            {
                var tally = votes.Values
                    .GroupBy(id => id)
                    .Select(g => new { TargetId = g.Key, Count = g.Count() })
                    .OrderByDescending(g => g.Count)
                    .ToList();

                bool isTie = tally.Count > 1 && tally[0].Count == tally[1].Count;
                if (!isTie)
                {
                    PlayerData eliminated = GetPlayer(tally[0].TargetId);
                    eliminated?.Kill();
                    OnLogMessage?.Invoke($"{eliminated?.Name}이(가) 투표로 처형되었습니다.");
                }
                else
                {
                    OnLogMessage?.Invoke("투표가 동률이라 아무도 처형되지 않았습니다.");
                }
            }
            else
            {
                OnLogMessage?.Invoke("아무도 투표하지 않았습니다.");
            }

            OnPlayersChanged?.Invoke(players);

            if (!CheckWinCondition())
            {
                ChangePhase(GameState.Night);
            }
        }

        private void ResolveNightPhase()
        {
            AutoFillAINightActions();

            PlayerData mafia = players.FirstOrDefault(p => p.IsAlive && p.Role == Role.Mafia);
            if (mafia != null && nightActions.TryGetValue(mafia.Id, out int killTargetId))
            {
                PlayerData victim = GetPlayer(killTargetId);
                if (victim != null && victim.IsAlive)
                {
                    victim.Kill();
                    OnLogMessage?.Invoke($"밤 사이 {victim.Name}이(가) 마피아에게 살해당했습니다.");
                }
            }

            PlayerData police = players.FirstOrDefault(p => p.IsAlive && p.Role == Role.Police);
            if (police != null && nightActions.TryGetValue(police.Id, out int investigateTargetId))
            {
                PlayerData suspect = GetPlayer(investigateTargetId);
                if (suspect != null)
                {
                    OnLogMessage?.Invoke($"경찰이 {suspect.Name}을(를) 조사했습니다: {suspect.Role}");
                }
            }

            OnPlayersChanged?.Invoke(players);

            if (!CheckWinCondition())
            {
                ChangePhase(GameState.Day);
            }
        }

        private void AutoFillAINightActions()
        {
            List<PlayerData> alivePlayers = players.Where(p => p.IsAlive).ToList();

            PlayerData mafia = alivePlayers.FirstOrDefault(p => p.Role == Role.Mafia && p.IsAI);
            if (mafia != null && !nightActions.ContainsKey(mafia.Id))
            {
                List<PlayerData> candidates = alivePlayers.Where(p => p.Role != Role.Mafia).ToList();
                if (candidates.Count > 0)
                {
                    nightActions[mafia.Id] = candidates[UnityEngine.Random.Range(0, candidates.Count)].Id;
                }
            }

            PlayerData police = alivePlayers.FirstOrDefault(p => p.Role == Role.Police && p.IsAI);
            if (police != null && !nightActions.ContainsKey(police.Id))
            {
                List<PlayerData> candidates = alivePlayers.Where(p => p.Id != police.Id).ToList();
                if (candidates.Count > 0)
                {
                    nightActions[police.Id] = candidates[UnityEngine.Random.Range(0, candidates.Count)].Id;
                }
            }
        }

        private void AutoCastAIVotes()
        {
            List<PlayerData> aliveTargets = players.Where(p => p.IsAlive).ToList();
            foreach (PlayerData ai in players.Where(p => p.IsAlive && p.IsAI && !votes.ContainsKey(p.Id)))
            {
                List<PlayerData> candidates = aliveTargets.Where(p => p.Id != ai.Id).ToList();
                if (candidates.Count == 0)
                {
                    continue;
                }

                PlayerData choice = candidates[UnityEngine.Random.Range(0, candidates.Count)];
                votes[ai.Id] = choice.Id;
                OnLogMessage?.Invoke($"{ai.Name} -> {choice.Name} 투표");
            }
        }

        private bool CheckWinCondition()
        {
            int aliveMafia = players.Count(p => p.IsAlive && p.Role == Role.Mafia);
            int aliveOthers = players.Count(p => p.IsAlive && p.Role != Role.Mafia);

            string resultMessage = null;
            if (aliveMafia <= 0)
            {
                resultMessage = "시민 팀 승리! 마피아를 모두 찾아냈습니다.";
            }
            else if (aliveMafia >= aliveOthers)
            {
                resultMessage = "마피아 팀 승리! 마피아 수가 시민 수 이상이 되었습니다.";
            }

            if (resultMessage == null)
            {
                return false;
            }

            stateMachine.TryChangeState(GameState.GameOver);
            phaseTimerActive = false;
            OnPhaseChanged?.Invoke(GameState.GameOver);
            OnLogMessage?.Invoke(resultMessage);
            return true;
        }
    }
}
