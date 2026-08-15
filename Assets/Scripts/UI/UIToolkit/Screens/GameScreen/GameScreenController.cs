using UnityEngine.UIElements;
using Bunker.Core;

namespace Bunker.UI
{
    public class GameScreenController : ScreenController
    {
        private readonly TopBarController _topBar;
        private readonly MyCardPanelController _myCardPanel;
        private readonly RevealPanelController _revealPanel;
        private readonly DiscussionPanelController _discussionPanel;
        private readonly VotingPanelController _votingPanel;
        private readonly RoundResultPanelController _roundResultPanel;
        private readonly SpecialCardModalController _specialCardModal;

        private GameSession _session;
        public string LocalPlayerId { get; private set; }

        public GameScreenController(
            VisualElement root,
            VisualTreeAsset topBarAsset, VisualTreeAsset playerStatusIconAsset,
            VisualTreeAsset myCardAsset, VisualTreeAsset traitSlotAsset,
            VisualTreeAsset revealAsset,
            VisualTreeAsset discussionAsset,
            VisualTreeAsset votingAsset, VisualTreeAsset voteTargetItemAsset,
            VisualTreeAsset roundResultAsset,
            VisualTreeAsset specialCardModalAsset) : base(root)
        {
            _topBar = new TopBarController(Clone(root, "top-bar", topBarAsset), playerStatusIconAsset);
            _myCardPanel = new MyCardPanelController(Clone(root, "my-card-panel", myCardAsset), traitSlotAsset);
            _revealPanel = new RevealPanelController(Clone(root, "reveal-panel", revealAsset));
            _discussionPanel = new DiscussionPanelController(Clone(root, "discussion-panel", discussionAsset));
            _votingPanel = new VotingPanelController(Clone(root, "voting-panel", votingAsset), voteTargetItemAsset);
            _roundResultPanel = new RoundResultPanelController(Clone(root, "round-result-panel", roundResultAsset));

            var modalContainer = root.Q<VisualElement>("special-card-modal-container");
            var modalContent = specialCardModalAsset.CloneTree();
            modalContainer.Add(modalContent);
            _specialCardModal = new SpecialCardModalController(modalContainer, modalContent);

            _myCardPanel.SpecialCardRequested += () => _specialCardModal.Open();
        }

        private static VisualElement Clone(VisualElement parentRoot, string containerName, VisualTreeAsset asset)
        {
            var container = parentRoot.Q<VisualElement>(containerName);
            container.Add(asset.CloneTree());
            return container;
        }

        public void Bind(GameSession session, string localPlayerId = null)
        {
            _session = session;
            LocalPlayerId = localPlayerId ?? session.Players[0].PlayerId;

            _session.OnPhaseChanged += OnPhaseChanged;
            _session.OnGameOverResolved += OnGameOverResolved;

            _topBar.Bind(_session);
            _myCardPanel.Bind(_session, LocalPlayerId);
            _revealPanel.Bind(_session, LocalPlayerId);
            _discussionPanel.Bind(_session);
            _votingPanel.Bind(_session, LocalPlayerId);
            _roundResultPanel.Bind(_session);
            _specialCardModal.Bind(_session, LocalPlayerId);

            OnPhaseChanged(_session.Phase);
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            SetVisible(_revealPanel.Root, phase == GamePhase.Reveal);
            SetVisible(_discussionPanel.Root, phase == GamePhase.Discussion);
            SetVisible(_votingPanel.Root, phase == GamePhase.Voting || phase == GamePhase.VotingTiebreaker);
            SetVisible(_roundResultPanel.Root, phase == GamePhase.RoundResult);

            // VisualElements have no OnEnable/OnDisable — panels that need to
            // "do something" on becoming active are notified explicitly here.
            if (phase == GamePhase.Discussion)
                _discussionPanel.NotifyPhaseEntered(durationSeconds: 120);

            if (phase == GamePhase.Voting || phase == GamePhase.VotingTiebreaker)
                _votingPanel.NotifyPhaseEntered(isTiebreaker: phase == GamePhase.VotingTiebreaker);
        }

        private static void SetVisible(VisualElement element, bool visible) =>
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        private void OnGameOverResolved(GameOverResult result)
        {
            var screen = UIManager.Instance.ShowScreen<GameOverScreenController>();
            screen.Bind(result);
        }

        protected override void OnHidden()
        {
            if (_session == null) return;
            _session.OnPhaseChanged -= OnPhaseChanged;
            _session.OnGameOverResolved -= OnGameOverResolved;
        }
    }
}