using UnityEngine;
using Bunker.Core;

namespace Bunker.UI
{
    // Container that stays active for the whole game and switches between
    // phase-specific panels based on GameSession.OnPhaseChanged.
    // This is the "thin consumer" described in the architecture: it never
    // touches PlayerData directly, only subscribes to GameSession events
    // and calls its public methods.
    public class GameScreen : UIScreen
    {
        [SerializeField] private TopBarView _topBar;
        [SerializeField] private MyCardPanel _myCardPanel;
        [SerializeField] private RevealPhasePanel _revealPanel;
        [SerializeField] private DiscussionPhasePanel _discussionPanel;
        [SerializeField] private VotingPhasePanel _votingPanel;
        [SerializeField] private RoundResultPanel _roundResultPanel;
        [SerializeField] private SpecialCardModal _specialCardModal;

        private GameSession _session;
        public string LocalPlayerId { get; private set; }

        // Called by whoever transitions into this screen (LobbyScreen for
        // hot-seat/local testing now, network layer later).
        public void Bind(GameSession session, string localPlayerId = null)
        {
            _session = session;
            // For local hot-seat testing without network identity yet,
            // default to the first player so the screen has something to drive.
            LocalPlayerId = localPlayerId ?? session.Players[0].PlayerId;

            _session.OnPhaseChanged += OnPhaseChanged;
            _session.OnRoundStarted += _ => _topBar.RefreshRound(_session.CurrentRound);
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
            _revealPanel.gameObject.SetActive(phase == GamePhase.Reveal);
            _discussionPanel.gameObject.SetActive(phase == GamePhase.Discussion);
            _votingPanel.gameObject.SetActive(phase == GamePhase.Voting || phase == GamePhase.VotingTiebreaker);
            _roundResultPanel.gameObject.SetActive(phase == GamePhase.RoundResult);
        }

        private void OnGameOverResolved(GameOverResult result)
        {
            var screen = UIManager.Instance.ShowScreen<GameOverScreen>();
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