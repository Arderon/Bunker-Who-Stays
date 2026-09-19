using System.Collections;
using System.Collections.Generic;
using Bunker.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bunker.UI.GameV2
{
    // Bottom-anchored turn surface: a 15-second countdown, who the turn belongs
    // to, and the seven category buttons.
    //
    // The panel's height is fixed at 560 across every state, and the 8th grid
    // cell is permanently reserved for "start discussion", so nothing on screen
    // reflows as the turn changes or the pass completes.
    //
    // The timer is client-side for now: IGameSessionView has no turn clock, so
    // this restarts its own countdown whenever CurrentTurnPlayerId changes and
    // auto-reveals on expiry. When the server owns the clock, feed the
    // authoritative remaining time into SetRemaining and drop TickRoutine.
    public class RevealPhasePanelV2 : MonoBehaviour
    {
        private const float TimeUpHold = 1.2f;

        [Header("Panel")]
        [SerializeField] private Image _panelFill;
        [Tooltip("1-2px line along the panel's top edge — the only structural state cue.")]
        [SerializeField] private Image _topEdge;
        [Tooltip("Radial glow behind the panel, alpha animated 0 -> 18% in the last 5s.")]
        [SerializeField] private Image _glow;

        [Header("Timer ring")]
        [SerializeField] private RectTransform _ringRoot;
        [SerializeField] private Image _ringTrack;
        [Tooltip("Image Type = Filled, Radial 360, Origin Top, Clockwise.")]
        [SerializeField] private Image _ringFill;
        [SerializeField] private Image _ringInner;
        [SerializeField] private TMP_Text _numerals;

        [Header("Turn indicator")]
        [SerializeField] private Image _turnGlyph;
        [SerializeField] private Sprite _glyphCaret;    // your turn
        [SerializeField] private Sprite _glyphSquare;   // critical
        [SerializeField] private Sprite _glyphCircle;   // waiting
        [SerializeField] private Sprite _glyphCheck;    // pass complete
        [SerializeField] private TMP_Text _headline;
        [SerializeField] private TMP_Text _hint;

        [Header("Grid")]
        [Tooltip("Seven cells in spec order. Cell 8 is the reserved slot below.")]
        [SerializeField] private RevealCategoryButton[] _buttons;
        [SerializeField] private GameObject _startDiscussionButton;
        [SerializeField] private TMP_Text _startDiscussionLabel;
        [SerializeField] private Button _startDiscussionButtonComponent;
        [SerializeField] private CanvasGroup _startDiscussionGroup;

        private IGameSessionView _session;
        private string _localPlayerId;

        private Coroutine _tick;
        private Coroutine _blink;
        private float _remaining;
        private string _timedTurnPlayerId;
        private bool _passComplete;
        private bool _lockedByTimeout;

        private void Awake()
        {
            foreach (var button in _buttons)
            {
                if (button != null) button.SetCallback(OnCategoryPressed);
            }

            if (_startDiscussionButtonComponent != null)
            {
                _startDiscussionButtonComponent.onClick.AddListener(
                    () => _session?.StartDiscussionPhase(durationSeconds: 120));
            }
        }

        // --- Session wiring ---------------------------------------------------

        public void Bind(IGameSessionView session, string localPlayerId)
        {
            if (_session != null) Unsubscribe();

            _session = session;
            _localPlayerId = localPlayerId;

            session.OnRoundStarted += OnRoundStarted;
            session.OnTraitRevealed += OnTraitRevealed;
            session.OnRevealPassCompleted += OnRevealPassCompleted;
            session.OnTurnChanged += OnTurnChanged;
        }

        private void Unsubscribe()
        {
            _session.OnRoundStarted -= OnRoundStarted;
            _session.OnTraitRevealed -= OnTraitRevealed;
            _session.OnRevealPassCompleted -= OnRevealPassCompleted;
            _session.OnTurnChanged -= OnTurnChanged;
        }

        private void OnDestroy()
        {
            if (_session != null) Unsubscribe();
        }

        private void OnEnable()
        {
            if (_session == null) return;
            _passComplete = false;
            _startDiscussionButton.SetActive(false);
            Refresh();
        }

        private void OnDisable() => StopTimer();

        private void OnRoundStarted(int round)
        {
            _passComplete = false;
            _startDiscussionButton.SetActive(false);
            Refresh();
        }

        private void OnTraitRevealed(PlayerData player, CharacterTrait trait) => Refresh();

        // The authoritative signal that the clock should restart. Relying on
        // OnTraitRevealed alone would miss a turn that moved without a reveal
        // (server-side timeout, a player leaving), and would race the state
        // patch that carries the new turn id.
        private void OnTurnChanged(string turnPlayerId)
        {
            _lockedByTimeout = false;
            Refresh();
        }

        private void OnRevealPassCompleted()
        {
            _passComplete = true;
            StopTimer();
            Refresh();
            if (isActiveAndEnabled) StartCoroutine(FadeInStartDiscussion());
        }

        private void OnCategoryPressed(CardCategory category)
        {
            if (_lockedByTimeout) return;
            _session?.RevealNextTrait(_localPlayerId, category);
        }

        // --- State ------------------------------------------------------------

        public void Refresh()
        {
            if (_session == null) return;

            if (_passComplete)
            {
                PaintPassComplete();
                return;
            }

            bool isMyTurn = _session.CurrentTurnPlayerId == _localPlayerId;
            RestartTimerIfTurnChanged();

            if (isMyTurn) PaintYourTurn();
            else PaintWaiting();

            RefreshButtons(isMyTurn);
        }

        private void RefreshButtons(bool isMyTurn)
        {
            var localPlayer = _session.GetPlayer(_localPlayerId);

            foreach (var button in _buttons)
            {
                if (button == null) continue;

                bool revealed = localPlayer != null && localPlayer.IsCategoryRevealed(button.Category);

                RevealCategoryButton.State state;
                if (revealed) state = RevealCategoryButton.State.Locked;
                else if (isMyTurn && !_passComplete && !_lockedByTimeout) state = RevealCategoryButton.State.Enabled;
                else state = RevealCategoryButton.State.Inert;

                button.Apply(state, dimmed: !isMyTurn || _passComplete);
            }
        }

        private void PaintYourTurn()
        {
            _panelFill.color = BunkerTheme.PanelPlate;
            _ringInner.color = BunkerTheme.PanelPlate;
            _turnGlyph.gameObject.SetActive(true);
            _headline.fontSize = BunkerTheme.SizeL;

            bool critical = _remaining <= BunkerTheme.TimerCriticalAt;
            _turnGlyph.sprite = critical ? _glyphSquare : _glyphCaret;

            LocText.Set(this, _headline, critical ? LocKeys.ChooseNow : LocKeys.YourTurn);
            LocText.Set(this, _hint, critical ? LocKeys.ChooseNowHint : LocKeys.YourTurnHint);
            ApplyEscalation();
        }

        private void PaintWaiting()
        {
            // The whole panel drops one step in luminance so it is visibly not
            // addressed to you, without hiding the timer you still need to see.
            _panelFill.color = BunkerTheme.PanelPlateInert;
            _ringInner.color = BunkerTheme.PanelPlateInert;
            _topEdge.color = BunkerTheme.SunkenBorder;
            _ringTrack.color = BunkerTheme.TrackDimmer;
            _ringFill.color = BunkerTheme.Stroke;
            _numerals.color = BunkerTheme.TextSecondary;
            _glow.color = BunkerTheme.Danger.WithAlpha(0f);

            _turnGlyph.gameObject.SetActive(true);
            _turnGlyph.sprite = _glyphCircle;
            _turnGlyph.color = BunkerTheme.Stroke;

            _headline.fontSize = BunkerTheme.SizeRowName;
            _headline.color = BunkerTheme.TextSecondary;
            _hint.color = BunkerTheme.Stroke;

            var current = _session.GetPlayer(_session.CurrentTurnPlayerId);
            LocText.Set(this, _headline, LocKeys.WaitingFor, current?.DisplayName ?? string.Empty);
            LocText.Set(this, _hint, LocKeys.WaitingHint);
        }

        private void PaintPassComplete()
        {
            _panelFill.color = BunkerTheme.PanelPlate;
            _ringInner.color = BunkerTheme.PanelPlate;
            _topEdge.color = BunkerTheme.Success;
            _ringTrack.color = BunkerTheme.TrackDimmer;
            _ringFill.color = BunkerTheme.TrackDimmer;
            _ringFill.fillAmount = 1f;
            _numerals.text = string.Empty;
            _glow.color = BunkerTheme.Danger.WithAlpha(0f);

            _turnGlyph.gameObject.SetActive(true);
            _turnGlyph.sprite = _glyphCheck;
            _turnGlyph.color = BunkerTheme.Success;

            _headline.fontSize = BunkerTheme.SizeL;
            _headline.color = BunkerTheme.TextPrimary;
            _hint.color = BunkerTheme.TextPrimary.WithAlpha(0.78f);

            LocText.Set(this, _headline, LocKeys.PassComplete);
            LocText.Set(this, _hint, LocKeys.PassCompleteHint);
            LocText.Set(this, _startDiscussionLabel, LocKeys.StartDiscussion);

            RefreshButtons(isMyTurn: false);
        }

        // Only the timer, top edge, glow and headline escalate. The buttons keep
        // their colours throughout, so the thing you are about to press never
        // changes under your finger.
        private void ApplyEscalation()
        {
            Color color;
            if (_remaining <= BunkerTheme.TimerCriticalAt) color = BunkerTheme.Danger;
            else if (_remaining <= BunkerTheme.TimerWarningAt) color = BunkerTheme.Warning;
            else color = BunkerTheme.Accent;

            bool critical = _remaining <= BunkerTheme.TimerCriticalAt;

            _ringFill.color = color;
            _numerals.color = color;
            _headline.color = color;
            _turnGlyph.color = color;
            _topEdge.color = critical ? BunkerTheme.Danger : BunkerTheme.Border;
            _ringTrack.color = BunkerTheme.TrackDim;
            _hint.color = critical ? BunkerTheme.TextPrimary : BunkerTheme.TextPrimary.WithAlpha(0.78f);
            _glow.color = BunkerTheme.Danger.WithAlpha(critical ? 0.18f : 0f);

            if (critical && _blink == null && isActiveAndEnabled) _blink = StartCoroutine(CriticalBlink());
            else if (!critical) StopBlink();
        }

        // --- Timer ------------------------------------------------------------

        /// Hook for an authoritative server clock: call this instead of letting
        /// TickRoutine run, and the whole panel reacts the same way.
        public void SetRemaining(float seconds)
        {
            _remaining = Mathf.Max(0f, seconds);
            _ringFill.fillAmount = Mathf.Clamp01(_remaining / BunkerTheme.TurnSeconds);
            _numerals.text = Mathf.CeilToInt(_remaining).ToString("00");
        }

        private void RestartTimerIfTurnChanged()
        {
            var turnPlayer = _session.CurrentTurnPlayerId;
            if (turnPlayer == _timedTurnPlayerId && _tick != null) return;

            _timedTurnPlayerId = turnPlayer;
            _lockedByTimeout = false;
            StopTimer();

            if (string.IsNullOrEmpty(turnPlayer) || _passComplete || !isActiveAndEnabled) return;

            _remaining = BunkerTheme.TurnSeconds;
            _tick = StartCoroutine(TickRoutine());
        }

        private IEnumerator TickRoutine()
        {
            while (_remaining > 0f)
            {
                // The ring fill is continuous; the numerals step. Never
                // interpolate the numerals.
                _ringFill.fillAmount = Mathf.Clamp01(_remaining / BunkerTheme.TurnSeconds);
                _numerals.text = Mathf.CeilToInt(_remaining).ToString("00");

                int before = Mathf.CeilToInt(_remaining);
                _remaining -= Time.deltaTime;
                int after = Mathf.CeilToInt(_remaining);

                bool crossedThreshold = after != before &&
                    (after == BunkerTheme.TimerWarningAt || after == BunkerTheme.TimerCriticalAt);

                if (crossedThreshold)
                {
                    if (_session.CurrentTurnPlayerId == _localPlayerId) PaintYourTurn();
                    yield return PulseRing();
                }

                yield return null;
            }

            _remaining = 0f;
            _ringFill.fillAmount = 0f;
            _numerals.text = "00";
            _tick = null;

            yield return HandleTimeout();
        }

        // At zero the grid locks and a random hidden trait is revealed, so a
        // silent player can never stall the round.
        private IEnumerator HandleTimeout()
        {
            StopBlink();

            if (_session.CurrentTurnPlayerId != _localPlayerId) yield break;

            var player = _session.GetPlayer(_localPlayerId);
            if (player == null) yield break;

            _lockedByTimeout = true;
            RefreshButtons(isMyTurn: false);

            var hidden = new List<CardCategory>();
            foreach (var category in TraitCardViewV2.Order)
            {
                if (!player.IsCategoryRevealed(category)) hidden.Add(category);
            }

            if (hidden.Count == 0) yield break;

            _session.RevealNextTrait(_localPlayerId, hidden[Random.Range(0, hidden.Count)]);

            _headline.color = BunkerTheme.Danger;
            LocText.Set(this, _headline, LocKeys.TimeUp);

            yield return new WaitForSeconds(TimeUpHold);

            _lockedByTimeout = false;
            Refresh();
        }

        private IEnumerator PulseRing()
        {
            const float duration = 0.3f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                _ringRoot.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(t / duration * Mathf.PI));
                yield return null;
            }
            _ringRoot.localScale = Vector3.one;
        }

        // A hard two-step blink, not a sine: it should read as a relay, not a breath.
        private IEnumerator CriticalBlink()
        {
            while (true)
            {
                SetRingAlpha(1f);
                yield return new WaitForSeconds(0.5f);
                SetRingAlpha(0.35f);
                yield return new WaitForSeconds(0.5f);
            }
        }

        private void SetRingAlpha(float alpha)
        {
            _ringFill.color = _ringFill.color.WithAlpha(alpha);
            _numerals.color = _numerals.color.WithAlpha(alpha);
        }

        private void StopBlink()
        {
            if (_blink == null) return;
            StopCoroutine(_blink);
            _blink = null;
            SetRingAlpha(1f);
        }

        private void StopTimer()
        {
            if (_tick != null)
            {
                StopCoroutine(_tick);
                _tick = null;
            }
            StopBlink();
        }

        // Fades into the reserved 8th cell, so the layout never moves.
        private IEnumerator FadeInStartDiscussion()
        {
            _startDiscussionButton.SetActive(true);

            const float duration = 0.24f;
            float t = 0f;
            var rect = (RectTransform)_startDiscussionButton.transform;

            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                if (_startDiscussionGroup != null) _startDiscussionGroup.alpha = k;
                rect.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, k);
                yield return null;
            }

            if (_startDiscussionGroup != null) _startDiscussionGroup.alpha = 1f;
            rect.localScale = Vector3.one;
        }
    }
}
