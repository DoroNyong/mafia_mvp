using System.Collections.Generic;
using System.Linq;
using MafiaGame.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace MafiaGame.UI
{
    /// <summary>
    /// Canvas 하위에 붙이면 상태 텍스트 / 생존자 투표 리스트 / 로그 스크롤뷰를 코드로 구성하고
    /// GameManager 이벤트를 구독해 갱신하는 단일 진입점 UI 컨트롤러.
    /// 씬에 미리 만들어둔 프리팹에 의존하지 않는다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class MafiaUIController : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;

        private Text phaseText;
        private Text timerText;
        private Text roleText;
        private RectTransform playerListContent;
        private Text logText;
        private ScrollRect logScrollRect;

        private readonly Dictionary<int, (Text nameLabel, Button button, Text actionLabel)> playerRows =
            new Dictionary<int, (Text, Button, Text)>();
        private readonly List<string> logLines = new List<string>();

        private Role currentHumanRole = Role.Citizen;
        private bool currentHumanAlive = true;

        private void Awake()
        {
            EnsureEventSystem();
            BuildLayout();
            ResolveGameManager();
        }

        private void ResolveGameManager()
        {
            if (gameManager == null)
            {
                gameManager = FindObjectOfType<GameManager>();
            }

            if (gameManager == null)
            {
                Debug.LogWarning("MafiaUIController: 씬에서 GameManager를 찾을 수 없습니다. GameManager 컴포넌트를 씬에 배치했는지 확인하세요.");
            }
        }

        private void OnEnable()
        {
            ResolveGameManager();
            if (gameManager == null)
            {
                return;
            }

            gameManager.OnPhaseChanged += HandlePhaseChanged;
            gameManager.OnTimerChanged += HandleTimerChanged;
            gameManager.OnPlayersChanged += HandlePlayersChanged;
            gameManager.OnLogMessage += HandleLogMessage;

            HandlePhaseChanged(gameManager.CurrentState);
            HandlePlayersChanged(gameManager.Players);
        }

        private void OnDisable()
        {
            if (gameManager == null)
            {
                return;
            }

            gameManager.OnPhaseChanged -= HandlePhaseChanged;
            gameManager.OnTimerChanged -= HandleTimerChanged;
            gameManager.OnPlayersChanged -= HandlePlayersChanged;
            gameManager.OnLogMessage -= HandleLogMessage;
        }

        private void HandlePhaseChanged(GameState state)
        {
            phaseText.text = $"페이즈: {state}";

            bool actionable = IsHumanActionable(state);
            string label = GetActionLabel(state);
            foreach (var row in playerRows.Values)
            {
                row.button.interactable = actionable;
                row.actionLabel.text = label;
            }
        }

        private bool IsHumanActionable(GameState state)
        {
            if (!currentHumanAlive)
            {
                return false;
            }

            if (state == GameState.Vote)
            {
                return true;
            }

            if (state == GameState.Night)
            {
                return currentHumanRole == Role.Mafia || currentHumanRole == Role.Police;
            }

            return false;
        }

        private static string GetActionLabel(GameState state)
        {
            return state == GameState.Night ? "지목" : "투표";
        }

        private void HandleTimerChanged(int remainingSeconds)
        {
            timerText.text = $"남은 시간: {remainingSeconds}s";
        }

        private void HandlePlayersChanged(IReadOnlyList<PlayerData> players)
        {
            PlayerData human = players.FirstOrDefault(p => p.Id == GameManager.HumanPlayerId);
            if (human != null)
            {
                currentHumanRole = human.Role;
                currentHumanAlive = human.IsAlive;
                string aliveSuffix = human.IsAlive ? string.Empty : " (사망)";
                roleText.text = $"내 직업: {ToKoreanRole(human.Role)}{aliveSuffix}";
            }

            foreach (Transform child in playerListContent)
            {
                Destroy(child.gameObject);
            }
            playerRows.Clear();

            GameState state = gameManager != null ? gameManager.CurrentState : GameState.Setup;
            bool actionable = IsHumanActionable(state);
            string label = GetActionLabel(state);

            foreach (PlayerData player in players.Where(p => p.IsAlive))
            {
                CreatePlayerRow(player, actionable, label);
            }
        }

        private static string ToKoreanRole(Role role)
        {
            switch (role)
            {
                case Role.Mafia:
                    return "마피아";
                case Role.Police:
                    return "경찰";
                default:
                    return "시민";
            }
        }

        private void HandleLogMessage(string message)
        {
            logLines.Add(message);
            logText.text = string.Join("\n", logLines);

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(logScrollRect.content);
            logScrollRect.verticalNormalizedPosition = 0f;
        }

        private void OnPlayerRowClicked(int targetPlayerId)
        {
            if (gameManager == null)
            {
                return;
            }

            switch (gameManager.CurrentState)
            {
                case GameState.Vote:
                    gameManager.SubmitVote(GameManager.HumanPlayerId, targetPlayerId);
                    break;
                case GameState.Night:
                    gameManager.SubmitNightAction(GameManager.HumanPlayerId, targetPlayerId);
                    break;
            }
        }

        // ---------- UI construction ----------

        private void BuildLayout()
        {
            RectTransform root = (RectTransform)transform;

            RectTransform statusPanel = CreateStretchPanel(root, "StatusPanel", new Vector2(0f, 0.82f), new Vector2(1f, 1f));
            roleText = CreateText(statusPanel, "RoleText", "내 직업: -", 20, TextAnchor.MiddleLeft);
            AnchorStretch(roleText.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 1f));
            roleText.color = new Color(1f, 0.85f, 0.3f);
            phaseText = CreateText(statusPanel, "PhaseText", "페이즈: -", 22, TextAnchor.MiddleLeft);
            AnchorStretch(phaseText.rectTransform, new Vector2(0f, 0f), new Vector2(0.6f, 0.5f));
            timerText = CreateText(statusPanel, "TimerText", "남은 시간: -", 22, TextAnchor.MiddleRight);
            AnchorStretch(timerText.rectTransform, new Vector2(0.6f, 0f), new Vector2(1f, 0.5f));

            RectTransform playerPanel = CreateStretchPanel(root, "PlayerListPanel", new Vector2(0f, 0.3f), new Vector2(1f, 0.82f));
            (ScrollRect playerScrollRect, RectTransform playerContent) = CreateScrollRect(playerPanel, "PlayerListScrollView");
            playerListContent = playerContent;
            VerticalLayoutGroup layout = playerListContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = 4f;
            layout.padding = new RectOffset(6, 6, 6, 6);
            ContentSizeFitter fitter = playerListContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform logPanel = CreateStretchPanel(root, "LogPanel", new Vector2(0f, 0f), new Vector2(1f, 0.3f));
            (ScrollRect logRect, RectTransform logContent) = CreateScrollRect(logPanel, "LogScrollView");
            logScrollRect = logRect;

            VerticalLayoutGroup logLayout = logContent.gameObject.AddComponent<VerticalLayoutGroup>();
            logLayout.childControlHeight = true;
            logLayout.childControlWidth = true;
            logLayout.childForceExpandHeight = false;
            logLayout.childForceExpandWidth = true;
            logLayout.padding = new RectOffset(6, 6, 6, 6);
            ContentSizeFitter logContentFitter = logContent.gameObject.AddComponent<ContentSizeFitter>();
            logContentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            logText = CreateText(logContent, "LogText", string.Empty, 16, TextAnchor.UpperLeft);
            logText.horizontalOverflow = HorizontalWrapMode.Wrap;
            logText.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private void CreatePlayerRow(PlayerData player, bool actionable, string actionLabelText)
        {
            GameObject row = new GameObject($"Row_{player.Id}", typeof(RectTransform));
            row.transform.SetParent(playerListContent, false);
            RectTransform rowRect = (RectTransform)row.transform;
            rowRect.sizeDelta = new Vector2(0f, 44f);

            LayoutElement rowLayoutElement = row.AddComponent<LayoutElement>();
            rowLayoutElement.preferredHeight = 44f;

            HorizontalLayoutGroup rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;
            rowLayout.spacing = 8f;
            rowLayout.padding = new RectOffset(4, 4, 2, 2);

            Text nameText = CreateText(rowRect, "NameText", player.Name, 18, TextAnchor.MiddleLeft);
            LayoutElement nameLayoutElement = nameText.gameObject.AddComponent<LayoutElement>();
            nameLayoutElement.flexibleWidth = 1f;

            GameObject buttonGO = new GameObject("VoteButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGO.transform.SetParent(rowRect, false);
            LayoutElement buttonLayoutElement = buttonGO.AddComponent<LayoutElement>();
            buttonLayoutElement.preferredWidth = 90f;
            Image buttonImage = buttonGO.GetComponent<Image>();
            buttonImage.color = new Color(0.85f, 0.3f, 0.3f, 1f);
            Button button = buttonGO.GetComponent<Button>();
            button.targetGraphic = buttonImage;
            button.onClick.AddListener(() => OnPlayerRowClicked(player.Id));
            button.interactable = actionable;

            Text buttonText = CreateText(buttonGO.transform, "Label", actionLabelText, 16, TextAnchor.MiddleCenter);
            buttonText.color = Color.white;
            AnchorStretch(buttonText.rectTransform, Vector2.zero, Vector2.one);

            playerRows[player.Id] = (nameText, button, buttonText);
        }

        private RectTransform CreateStretchPanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private (ScrollRect scrollRect, RectTransform content) CreateScrollRect(Transform parent, string name)
        {
            GameObject scrollGO = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGO.transform.SetParent(parent, false);
            RectTransform scrollRectTransform = (RectTransform)scrollGO.transform;
            AnchorStretch(scrollRectTransform, Vector2.zero, Vector2.one);
            scrollGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.15f);

            GameObject viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportGO.transform.SetParent(scrollRectTransform, false);
            RectTransform viewportRect = (RectTransform)viewportGO.transform;
            AnchorStretch(viewportRect, Vector2.zero, Vector2.one);

            GameObject contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(viewportRect, false);
            RectTransform contentRect = (RectTransform)contentGO.transform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            ScrollRect scrollRect = scrollGO.GetComponent<ScrollRect>();
            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 24f;

            return (scrollRect, contentRect);
        }

        private Text CreateText(Transform parent, string name, string content, int fontSize, TextAnchor alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.font = GetDefaultFont();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.text = content;
            AnchorStretch(text.rectTransform, Vector2.zero, Vector2.one);
            return text;
        }

        private static void AnchorStretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Font GetDefaultFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            return font;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            GameObject eventSystemGO = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            eventSystemGO.AddComponent<InputSystemUIInputModule>();
#else
            eventSystemGO.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
