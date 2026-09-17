using System;
using System.Collections;
using System.Collections.Generic;
using Bunker.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bunker.UI.GameV2
{
    // The roster, collapsed into a bar that never scrolls and never pushes the
    // layout. Expanding unfolds an overlay that takes the bar's own x, width and
    // top, so the bar reads as the panel's header and nothing jumps.
    //
    // Scene objects, not a prefab: the bar lives in the content layer, the
    // overlay lives one layer above the browse overlay. One component owns both,
    // which is what keeps their geometry in sync.
    public class PlayerListDropdownV2 : MonoBehaviour
    {
        private const float OpenDuration = 0.22f;
        private const float CloseDuration = 0.16f;
        private const float PanelMaxHeight = 1200f;
        private const int PipCompactThreshold = 12;
        private const int PipDropThreshold = 16;

        [Header("Collapsed bar")]
        [SerializeField] private Plate _bar;
        [SerializeField] private Button _barButton;
        [SerializeField] private Image _accentTick;
        [SerializeField] private TMP_Text _aliveCount;
        [SerializeField] private TMP_Text _aliveLabel;
        [SerializeField] private RectTransform _pipContainer;
        [SerializeField] private HorizontalLayoutGroup _pipLayout;
        [SerializeField] private PlayerPip _pipPrefab;
        [Tooltip("Shown instead of the pips while syncing, and past 16 players.")]
        [SerializeField] private TMP_Text _pipFallbackLabel;
        [SerializeField] private RectTransform _chevron;

        [Header("Expanded overlay")]
        [SerializeField] private GameObject _overlayRoot;
        [SerializeField] private Image _scrim;
        [SerializeField] private Button _scrimButton;
        [SerializeField] private RectTransform _panel;
        [SerializeField] private CanvasGroup _panelGroup;
        [SerializeField] private ContentSizeFitter _panelFitter;
        [SerializeField] private TMP_Text _panelAliveCount;
        [SerializeField] private TMP_Text _panelAliveLabel;
        [SerializeField] private Button _panelCloseButton;
        [SerializeField] private RectTransform _rowsContent;
        [SerializeField] private PlayerListItem _rowPrefab;
        [SerializeField] private TMP_Text _footerLabel;

        [Header("Geometry")]
        [Tooltip("Distance from the top of the screen to the bar. The panel takes the same top.")]
        [SerializeField] private float _panelTopInset = BunkerTheme.BarTop;

        private readonly List<PlayerPip> _pips = new();
        private readonly List<PlayerListItem> _rows = new();

        private IGameSessionView _session;
        private string _localPlayerId;
        private Action<string> _onPlayerSelected;
        private Func<string, int> _eliminatedRound;

        private bool _open;
        private Coroutine _animation;

        public bool IsOpen => _open;

        private void Awake()
        {
            if (_barButton != null) _barButton.onClick.AddListener(Toggle);
            if (_scrimButton != null) _scrimButton.onClick.AddListener(Close);
            if (_panelCloseButton != null) _panelCloseButton.onClick.AddListener(Close);
            _overlayRoot.SetActive(false);
        }

        private void OnEnable()
        {
            if (_session != null) Refresh();
        }

        public void Bind(IGameSessionView session, string localPlayerId,
                         Action<string> onPlayerSelected, Func<string, int> eliminatedRound)
        {
            _session = session;
            _localPlayerId = localPlayerId;
            _onPlayerSelected = onPlayerSelected;
            _eliminatedRound = eliminatedRound;
            Refresh();
        }

        // --- Content ----------------------------------------------------------

        public void Refresh()
        {
            if (_session == null) return;

            var players = _session.Players;
            if (players == null || players.Count == 0)
            {
                PaintLoading();
                return;
            }

            int alive = 0;
            foreach (var p in players)
            {
                if (!p.IsEliminated) alive++;
            }

            // The total is dimmed so the alive number reads first.
            string counts = $"{alive}<color=#{ColorUtility.ToHtmlStringRGB(BunkerTheme.TextDisabled)}>/{players.Count}</color>";

            _accentTick.color = BunkerTheme.Accent;
            _aliveCount.text = counts;
            if (_panelAliveCount != null) _panelAliveCount.text = counts;

            LocText.Set(this, _aliveLabel, LocKeys.AliveLabel);
            LocText.Set(this, _panelAliveLabel, LocKeys.AliveLabel);
            LocText.Set(this, _footerLabel, LocKeys.TapOutsideToClose);

            RefreshPips(players);
            if (_open) RefreshRows(players);
        }

        private void PaintLoading()
        {
            _accentTick.color = BunkerTheme.Ghost;
            _aliveCount.text = string.Empty;
            _aliveLabel.text = string.Empty;
            _pipFallbackLabel.gameObject.SetActive(true);
            LocText.Set(this, _pipFallbackLabel, LocKeys.Syncing);

            EnsurePips(5);
            foreach (var pip in _pips)
            {
                pip.gameObject.SetActive(true);
                pip.ApplyWell();
            }
        }

        private void RefreshPips(List<PlayerData> players)
        {
            // Past sixteen players the row stops being legible as shapes, so it
            // degrades to the count alone rather than shrinking pips further.
            if (players.Count >= PipDropThreshold)
            {
                foreach (var pip in _pips) pip.gameObject.SetActive(false);
                _pipFallbackLabel.gameObject.SetActive(true);
                LocText.Set(this, _pipFallbackLabel, LocKeys.AliveLabel);
                return;
            }

            _pipFallbackLabel.gameObject.SetActive(false);
            if (_pipLayout != null) _pipLayout.spacing = players.Count >= PipCompactThreshold ? 6f : 10f;

            EnsurePips(players.Count);
            for (int i = 0; i < _pips.Count; i++)
            {
                bool used = i < players.Count;
                _pips[i].gameObject.SetActive(used);
                if (!used) continue;

                var player = players[i];
                _pips[i].Apply(
                    isCurrentTurn: player.PlayerId == _session.CurrentTurnPlayerId,
                    isEliminated: player.IsEliminated,
                    isLocal: player.PlayerId == _localPlayerId);
            }
        }

        private void EnsurePips(int count)
        {
            while (_pips.Count < count)
            {
                _pips.Add(Instantiate(_pipPrefab, _pipContainer));
            }
        }

        private void RefreshRows(List<PlayerData> players)
        {
            while (_rows.Count < players.Count)
            {
                var row = Instantiate(_rowPrefab, _rowsContent);
                row.SetCallback(OnRowSelected);
                _rows.Add(row);
            }

            string nextUpId = FindNextUp(players);

            for (int i = 0; i < _rows.Count; i++)
            {
                bool used = i < players.Count;
                _rows[i].gameObject.SetActive(used);
                if (!used) continue;

                var player = players[i];
                int revealed = 0;
                foreach (var category in TraitCardViewV2.Order)
                {
                    if (player.IsCategoryRevealed(category)) revealed++;
                }

                bool isCurrentTurn = player.PlayerId == _session.CurrentTurnPlayerId;
                bool isNextUp = player.PlayerId == nextUpId;
                bool isLocal = player.PlayerId == _localPlayerId;

                // One state per row, so these are priority-ordered: who is
                // eliminated or currently revealing matters more than "this is
                // you" — You only shows once nothing more urgent applies.
                if (player.IsEliminated)
                {
                    int round = _eliminatedRound?.Invoke(player.PlayerId) ?? 0;
                    _rows[i].Bind(player.PlayerId, player.DisplayName, PlayerState.Expelled, round);
                }
                else if (isCurrentTurn)
                {
                    _rows[i].Bind(player.PlayerId, player.DisplayName, PlayerState.Revealing,
                        revealed, TraitCardViewV2.Order.Length);
                }
                else if (isNextUp)
                {
                    _rows[i].Bind(player.PlayerId, player.DisplayName, PlayerState.NextUp,
                        revealed, TraitCardViewV2.Order.Length);
                }
                else if (isLocal)
                {
                    _rows[i].Bind(player.PlayerId, player.DisplayName, PlayerState.You,
                        revealed, TraitCardViewV2.Order.Length);
                }
                else
                {
                    _rows[i].Bind(player.PlayerId, player.DisplayName, PlayerState.Revealed,
                        revealed, TraitCardViewV2.Order.Length);
                }
            }
        }

        // Turn order is not exposed by IGameSessionView, so "next up" is derived
        // from roster order — the same order the server deals turns in.
        private string FindNextUp(List<PlayerData> players)
        {
            int currentIndex = players.FindIndex(p => p.PlayerId == _session.CurrentTurnPlayerId);
            if (currentIndex < 0) return null;

            for (int step = 1; step <= players.Count; step++)
            {
                var candidate = players[(currentIndex + step) % players.Count];
                if (!candidate.IsEliminated) return candidate.PlayerId;
            }
            return null;
        }

        private void OnRowSelected(string playerId)
        {
            // Opening a file from the list closes the list rather than stacking a
            // third panel — max depth is two overlays plus a modal.
            Close();
            _onPlayerSelected?.Invoke(playerId);
        }

        // --- Open / close -----------------------------------------------------

        public void Toggle()
        {
            if (_open) Close();
            else Open();
        }

        public void Open()
        {
            if (_open || _session == null) return;
            _open = true;

            // Activate before binding: localization resolves through a
            // coroutine that no-ops on an inactive GameObject, so binding rows
            // while the overlay (their parent) is still hidden would leave
            // status/badge text silently blank on the first open of a session.
            _overlayRoot.SetActive(true);
            _overlayRoot.transform.SetAsLastSibling();

            RefreshRows(_session.Players);
            AlignPanelToBar();

            if (_animation != null) StopCoroutine(_animation);
            _animation = StartCoroutine(Animate(true));
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;

            if (_animation != null) StopCoroutine(_animation);
            _animation = null;

            // Closing as part of hiding the whole screen: a coroutine started on
            // an object about to be deactivated never finishes, and the overlay
            // would be left open the next time the screen is shown.
            if (!gameObject.activeInHierarchy)
            {
                ApplyClosedState();
                return;
            }

            _animation = StartCoroutine(Animate(false));
        }

        private void AlignPanelToBar()
        {
            _panel.anchoredPosition = new Vector2(0f, -_panelTopInset);

            if (_panelFitter != null) _panelFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_panel);

            // Past its cap the row list scrolls instead of the panel growing off
            // the bottom of the screen; header and footer stay pinned.
            if (_panel.rect.height > PanelMaxHeight)
            {
                if (_panelFitter != null) _panelFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
                _panel.sizeDelta = new Vector2(_panel.sizeDelta.x, PanelMaxHeight);
            }
        }

        private void ApplyClosedState()
        {
            _panelGroup.alpha = 0f;
            _panel.localScale = new Vector3(1f, 0f, 1f);
            _chevron.localRotation = Quaternion.identity;
            _overlayRoot.SetActive(false);
        }

        private IEnumerator Animate(bool open)
        {
            float duration = open ? OpenDuration : CloseDuration;
            float t = 0f;

            float fromScale = open ? 0f : 1f;
            float toScale = open ? 1f : 0f;
            float fromChevron = open ? 0f : 180f;
            float toChevron = open ? 180f : 0f;

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                float eased = open
                    ? 1f - Mathf.Pow(1f - k, 3f)   // cubic-bezier(.16,1,.3,1)
                    : k * k;                        // cubic-bezier(.4,0,1,1)

                _panel.localScale = new Vector3(1f, Mathf.Lerp(fromScale, toScale, eased), 1f);
                _panelGroup.alpha = open ? Mathf.Clamp01(eased * 1.6f) : 1f - eased;
                _scrim.color = BunkerTheme.Scrim.WithAlpha(
                    BunkerTheme.Scrim.a * (open ? Mathf.Clamp01(k / 0.72f) : 1f - k));
                _chevron.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(fromChevron, toChevron, eased));

                yield return null;
            }

            _panel.localScale = new Vector3(1f, toScale, 1f);
            _panelGroup.alpha = open ? 1f : 0f;
            _chevron.localRotation = Quaternion.Euler(0f, 0f, toChevron);

            if (!open) _overlayRoot.SetActive(false);
            _animation = null;
        }
    }
}
