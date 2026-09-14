using System.Collections;
using System.Collections.Generic;
using Bunker.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Bunker.UI.GameV2
{
    // The V2 game screen. Lives in the scene beside the original GameScreen and
    // never touches it.
    //
    // Layer story, bottom to top:
    //   sort  0  current-turn card + reveal panel   (no scrim, always present)
    //   sort 10  browsed player card                (scrim 72%, dashed frame, back bar)
    //   sort 20  expanded player list               (scrim 72%, anchored to its own bar)
    //   sort 30  special card modal                 (owned by the legacy screen for now)
    //
    // The reveal panel never leaves sort 0: your timer keeps running while you
    // browse, and you can see it running through the scrim.
    public class GameScreenV2 : UIScreen
    {
        private const float OverlayPushDuration = 0.26f;
        private const float OverlayDismissDuration = 0.18f;
        private const float OverlayRise = 48f;
        private const float BaseCardRecede = 0.97f;

        [Header("Base layer")]
        [SerializeField] private TraitCardViewV2 _turnCard;
        [SerializeField] private RevealPhasePanelV2 _revealPanel;
        [SerializeField] private PlayerListDropdownV2 _playerList;

        [Header("Browse overlay")]
        [SerializeField] private GameObject _cardOverlayRoot;
        [SerializeField] private CanvasGroup _cardOverlayGroup;
        [SerializeField] private TraitCardViewV2 _browsedCard;

        private IGameSessionView _session;
        private string _localPlayerId;

        private readonly Dictionary<string, int> _eliminationRound = new();
        private string _browsedPlayerId;
        private Coroutine _overlayAnimation;
        private Vector3 _browsedCardRest;
        private bool _restCaptured;

        public string LocalPlayerId => _localPlayerId;

        private void Awake()
        {
            _browsedCard.SetCallbacks(CloseBrowsedCard, CloseBrowsedCard, OnUseSpecialCard);
            _turnCard.SetCallbacks(null, null, OnUseSpecialCard);
            _cardOverlayRoot.SetActive(false);
        }

        // --- Session ----------------------------------------------------------

        public void Bind(IGameSessionView session, string localPlayerId = null)
        {
            if (_session != null) Unsubscribe();

            _session = session;
            _localPlayerId = localPlayerId
                ?? (session.Players.Count > 0 ? session.Players[0].PlayerId : null);

            session.OnTraitRevealed += OnTraitRevealed;
            session.OnPlayerEliminated += OnPlayerEliminated;
            session.OnRoundStarted += OnRoundStarted;
            session.OnSpecialCardUsed += OnSpecialCardUsed;

            _playerList.Bind(session, _localPlayerId, OpenPlayerCard, EliminationRoundOf);
            _revealPanel.Bind(session, _localPlayerId);

            RefreshTurnCard();
        }

        private void Unsubscribe()
        {
            _session.OnTraitRevealed -= OnTraitRevealed;
            _session.OnPlayerEliminated -= OnPlayerEliminated;
            _session.OnRoundStarted -= OnRoundStarted;
            _session.OnSpecialCardUsed -= OnSpecialCardUsed;
        }

        private void OnDestroy()
        {
            if (_session != null) Unsubscribe();
        }

        protected override void OnShown()
        {
            if (_session == null) return;
            RefreshTurnCard();
            _playerList.Refresh();
            _revealPanel.Refresh();
        }

        protected override void OnHidden()
        {
            if (_playerList != null) _playerList.Close();
            CloseBrowsedCard();
        }

        private int EliminationRoundOf(string playerId)
        {
            return _eliminationRound.TryGetValue(playerId, out var round) ? round : 0;
        }

        // --- Events -----------------------------------------------------------

        private void OnRoundStarted(int round)
        {
            RefreshTurnCard();
            _playerList.Refresh();
        }

        private void OnTraitRevealed(PlayerData player, CharacterTrait trait)
        {
            if (player.PlayerId == _session.CurrentTurnPlayerId) _turnCard.MarkJustRevealed(trait.Category);
            if (player.PlayerId == _browsedPlayerId) _browsedCard.MarkJustRevealed(trait.Category);

            RefreshTurnCard();
            if (_browsedPlayerId != null) RefreshBrowsedCard();
            _playerList.Refresh();
        }

        private void OnPlayerEliminated(PlayerData player)
        {
            _eliminationRound[player.PlayerId] = _session.CurrentRound;
            RefreshTurnCard();
            if (_browsedPlayerId == player.PlayerId) RefreshBrowsedCard();
            _playerList.Refresh();
        }

        private void OnSpecialCardUsed(PlayerData player, SpecialCard card)
        {
            RefreshTurnCard();
            if (_browsedPlayerId != null) RefreshBrowsedCard();
        }

        private void OnUseSpecialCard() => SpecialCardModal.RequestOpen?.Invoke();

        // --- Cards ------------------------------------------------------------

        private void RefreshTurnCard()
        {
            if (_session == null) return;

            var turnPlayer = _session.GetPlayer(_session.CurrentTurnPlayerId);
            if (turnPlayer == null)
            {
                _turnCard.ShowSkeleton();
                return;
            }

            bool isLocal = turnPlayer.PlayerId == _localPlayerId;

            _turnCard.Bind(new CardContext
            {
                Player = turnPlayer,
                // Your own card during your own turn keeps the Own framing: the
                // white frame and the special slot are what make it yours, and
                // the amber turn signal is already carried by the reveal panel.
                Mode = isLocal ? CardMode.Own : CardMode.CurrentTurn,
                FileIndex = _session.Players.IndexOf(turnPlayer) + 1,
                TotalFiles = _session.Players.Count,
                EliminatedRound = EliminationRoundOf(turnPlayer.PlayerId),
                ShowClose = false
            });
        }

        private void OpenPlayerCard(string playerId)
        {
            if (_session == null || string.IsNullOrEmpty(playerId)) return;

            // Tapping the player whose turn it already is would stack an overlay
            // showing exactly what is already underneath it.
            if (playerId == _session.CurrentTurnPlayerId)
            {
                CloseBrowsedCard();
                return;
            }

            _browsedPlayerId = playerId;
            RefreshBrowsedCard();

            _cardOverlayRoot.SetActive(true);

            if (_overlayAnimation != null) StopCoroutine(_overlayAnimation);
            _overlayAnimation = StartCoroutine(AnimateOverlay(true));
        }

        private void RefreshBrowsedCard()
        {
            var player = _session.GetPlayer(_browsedPlayerId);
            if (player == null) return;

            var turnPlayer = _session.GetPlayer(_session.CurrentTurnPlayerId);

            _browsedCard.Bind(new CardContext
            {
                Player = player,
                Mode = player.PlayerId == _localPlayerId ? CardMode.Own : CardMode.Browsed,
                FileIndex = _session.Players.IndexOf(player) + 1,
                TotalFiles = _session.Players.Count,
                BackTargetName = turnPlayer?.DisplayName,
                EliminatedRound = EliminationRoundOf(player.PlayerId),
                ShowClose = true
            });
        }

        private void CloseBrowsedCard()
        {
            if (_browsedPlayerId == null) return;
            _browsedPlayerId = null;

            if (_overlayAnimation != null) StopCoroutine(_overlayAnimation);
            _overlayAnimation = null;

            if (!gameObject.activeInHierarchy)
            {
                _cardOverlayGroup.alpha = 0f;
                _turnCard.transform.localScale = Vector3.one;
                _cardOverlayRoot.SetActive(false);
                return;
            }

            _overlayAnimation = StartCoroutine(AnimateOverlay(false));
        }

        // The base card scales back and dims under the scrim, so the new layer is
        // felt as depth rather than just seen as a lighter rectangle.
        private IEnumerator AnimateOverlay(bool opening)
        {
            var card = (RectTransform)_browsedCard.transform;
            var baseCard = _turnCard.transform;

            // The card is positioned by stretched anchors, so it slides with
            // localPosition — writing anchoredPosition would rewrite the insets
            // the layout depends on.
            if (!_restCaptured)
            {
                _browsedCardRest = card.localPosition;
                _restCaptured = true;
            }

            float duration = opening ? OverlayPushDuration : OverlayDismissDuration;
            float t = 0f;

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                float eased = opening ? 1f - Mathf.Pow(1f - k, 3f) : k * k;
                float progress = opening ? eased : 1f - eased;

                _cardOverlayGroup.alpha = progress;
                card.localPosition = _browsedCardRest + Vector3.up * Mathf.Lerp(-OverlayRise, 0f, progress);
                baseCard.localScale = Vector3.one * Mathf.Lerp(1f, BaseCardRecede, progress);

                yield return null;
            }

            _cardOverlayGroup.alpha = opening ? 1f : 0f;
            card.localPosition = _browsedCardRest + Vector3.up * (opening ? 0f : -OverlayRise);
            baseCard.localScale = Vector3.one * (opening ? BaseCardRecede : 1f);

            if (!opening) _cardOverlayRoot.SetActive(false);
            _overlayAnimation = null;
        }

        private void Update()
        {
            // Android back pops exactly one level. The project runs the Input
            // System package only (activeInputHandler: 1), so the legacy
            // Input.GetKeyDown would throw here.
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;

            if (_playerList != null && _playerList.IsOpen) _playerList.Close();
            else if (_browsedPlayerId != null) CloseBrowsedCard();
        }
    }
}
