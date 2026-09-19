using System;
using System.Collections.Generic;
using Bunker.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bunker.UI.GameV2
{
    public enum CardMode
    {
        /// Base of the z-stack: whoever's turn it is. Accent top edge, plain
        /// border, no back bar — there is nothing behind it.
        CurrentTurn,

        /// Your own file. Solid white frame, and the only card with a special slot.
        Own,

        /// Someone else's file, opened from the roster. Dashed frame and a back
        /// bar naming the layer underneath — dashed reads as temporary.
        Browsed
    }

    public class CardContext
    {
        public PlayerData Player;
        public CardMode Mode = CardMode.CurrentTurn;
        public int FileIndex;          // 1-based position in the roster
        public int TotalFiles;
        public string BackTargetName;  // whose turn sits underneath a browsed card
        public int EliminatedRound;    // 0 while alive
        public bool ShowClose;
    }

    // The personnel dossier. Three framings, never ambiguous: you can name your
    // layer from the frame alone, without reading a word of it.
    //
    // Prefab: PlayerTraitCard. Used twice in the scene — once as the base
    // current-turn card, once inside the browse overlay.
    public class TraitCardViewV2 : MonoBehaviour
    {
        public static readonly CardCategory[] Order =
        {
            CardCategory.Gender, CardCategory.Age, CardCategory.Health, CardCategory.Profession,
            CardCategory.Baggage, CardCategory.Hobby, CardCategory.Fact
        };

        [Header("Frame")]
        [SerializeField] private Plate _plate;
        [SerializeField] private GameObject _accentTopEdge;
        [SerializeField] private GameObject _eliminatedHatch;
        [Tooltip("9-slice panel sprite used for the solid (own / current turn) frame.")]
        [SerializeField] private Sprite _solidFrameSprite;
        [Tooltip("9-slice panel sprite with the dash baked in, for the browsed frame.")]
        [SerializeField] private Sprite _dashedFrameSprite;

        [Header("Back bar")]
        [SerializeField] private GameObject _backBar;
        [SerializeField] private TMP_Text _backLabel;
        [SerializeField] private Button _backButton;

        [Header("Header")]
        [SerializeField] private Plate _badgePlate;
        [SerializeField] private TMP_Text _badgeLabel;
        [SerializeField] private TMP_Text _idLine;
        [SerializeField] private TMP_Text _nameLabel;
        [SerializeField] private TMP_Text _revealedCount;
        [SerializeField] private GameObject _ticksRow;
        [Tooltip("Seven progress ticks, left to right. Filled = revealed.")]
        [SerializeField] private Image[] _ticks;
        [SerializeField] private GameObject _expelledStamp;
        [SerializeField] private TMP_Text _expelledLabel;
        [SerializeField] private Button _closeButton;

        [Header("Slots")]
        [Tooltip("Seven rows in spec order: Gender, Age, Health, Profession, Baggage, Hobby, Fact.")]
        [SerializeField] private TraitSlotV2[] _slots;

        [Header("Special card — own file only")]
        [SerializeField] private GameObject _specialSection;
        [SerializeField] private Plate _specialPlate;
        [SerializeField] private TMP_Text _specialTitle;
        [SerializeField] private TMP_Text _specialDesc;
        [SerializeField] private GameObject _specialUseButton;
        [SerializeField] private TMP_Text _specialUseLabel;
        [SerializeField] private Button _specialButton;
        [SerializeField] private GameObject _specialUsedStamp;
        [SerializeField] private TMP_Text _specialUsedLabel;

        private readonly Dictionary<CardCategory, TraitSlotV2> _slotsByCategory = new();
        private CardContext _context = new();
        private CardCategory? _justRevealed;
        private Action _onClose;
        private Action _onBack;
        private Action _onUseSpecial;

        private void Awake()
        {
            foreach (var slot in _slots)
            {
                if (slot != null) _slotsByCategory[slot.Category] = slot;
            }

            if (_closeButton != null) _closeButton.onClick.AddListener(() => _onClose?.Invoke());
            if (_backButton != null) _backButton.onClick.AddListener(() => _onBack?.Invoke());
            if (_specialButton != null) _specialButton.onClick.AddListener(() => _onUseSpecial?.Invoke());
        }

        private void OnEnable()
        {
            if (_context.Player != null)
            {
                ApplyFraming();
                Refresh();
            }
        }

        public void SetCallbacks(Action onClose, Action onBack, Action onUseSpecial)
        {
            _onClose = onClose;
            _onBack = onBack;
            _onUseSpecial = onUseSpecial;
        }

        /// Tells the card which slot to play the reveal emphasis on. Call before
        /// Refresh; it is consumed by the next refresh and then cleared.
        public void MarkJustRevealed(CardCategory category) => _justRevealed = category;

        public void Bind(CardContext context)
        {
            _context = context ?? new CardContext();
            ApplyFraming();
            Refresh();
        }

        // --- Framing ----------------------------------------------------------

        private void ApplyFraming()
        {
            bool eliminated = _context.Player != null && _context.Player.IsEliminated;

            _accentTopEdge.SetActive(_context.Mode == CardMode.CurrentTurn);
            _backBar.SetActive(_context.Mode == CardMode.Browsed && !string.IsNullOrEmpty(_context.BackTargetName));
            _closeButton.gameObject.SetActive(_context.ShowClose);
            _eliminatedHatch.SetActive(eliminated);
            _specialSection.SetActive(_context.Mode == CardMode.Own && !eliminated);

            switch (_context.Mode)
            {
                case CardMode.Own:
                    _plate.SetBorderSprite(_solidFrameSprite);
                    _plate.SetBorderWidth(2f);
                    _plate.Set(BunkerTheme.Surface, BunkerTheme.TextPrimary);
                    _badgePlate.Set(BunkerTheme.TextPrimary, BunkerTheme.TextPrimary);
                    _badgeLabel.color = BunkerTheme.Bg;
                    LocText.Set(this, _badgeLabel, LocKeys.BadgeYourFile);
                    break;

                case CardMode.Browsed:
                    _plate.SetBorderSprite(_dashedFrameSprite);
                    _plate.SetBorderWidth(2f);
                    _plate.Set(eliminated ? BunkerTheme.PipWell : BunkerTheme.Surface,
                        eliminated ? BunkerTheme.StrokeDim : BunkerTheme.Stroke);
                    _badgePlate.Set(Color.clear, BunkerTheme.Stroke);
                    _badgeLabel.color = BunkerTheme.TextPrimary.WithAlpha(0.85f);
                    LocText.Set(this, _badgeLabel, LocKeys.BadgeBrowsing);
                    break;

                default:
                    _plate.SetBorderSprite(_solidFrameSprite);
                    _plate.SetBorderWidth(1f);
                    _plate.Set(BunkerTheme.Surface, BunkerTheme.Border);
                    _badgePlate.Set(Color.clear, Color.clear);
                    _badgeLabel.color = BunkerTheme.Accent;
                    LocText.Set(this, _badgeLabel, LocKeys.BadgeCurrentTurn);
                    break;
            }

            if (_backBar.activeSelf) LocText.Set(this, _backLabel, LocKeys.BackTo, _context.BackTargetName);
        }

        // --- Content ----------------------------------------------------------

        public void Refresh()
        {
            var player = _context.Player;
            if (player == null) return;

            bool eliminated = player.IsEliminated;

            _nameLabel.text = player.DisplayName ?? string.Empty;
            _nameLabel.color = eliminated ? BunkerTheme.Stroke : BunkerTheme.TextPrimary;
            _nameLabel.fontStyle = eliminated
                ? FontStyles.Bold | FontStyles.UpperCase | FontStyles.Strikethrough
                : FontStyles.Bold | FontStyles.UpperCase;

            int revealed = 0;
            foreach (var category in Order)
            {
                if (player.IsCategoryRevealed(category)) revealed++;
            }

            if (eliminated)
            {
                LocText.Set(this, _idLine, LocKeys.IdExpelled, _context.FileIndex, _context.EliminatedRound);
                LocText.Set(this, _revealedCount, LocKeys.FileDeclassified, Order.Length, Order.Length);
                LocText.Set(this, _expelledLabel, LocKeys.Expelled);
                _expelledStamp.SetActive(true);
                _ticksRow.SetActive(false);
            }
            else
            {
                LocText.Set(this, _idLine, LocKeys.IdAlive, _context.FileIndex);
                LocText.Set(this, _revealedCount, LocKeys.RevealedCount, revealed, Order.Length);
                _expelledStamp.SetActive(false);
                _ticksRow.SetActive(true);

                for (int i = 0; i < _ticks.Length; i++)
                {
                    if (_ticks[i] != null) _ticks[i].color = i < revealed ? BunkerTheme.Success : BunkerTheme.Border;
                }
            }

            // Your own file is the one card where unrevealed traits are legible:
            // you know your own character, and the server sends you your full
            // hand up front (dealtHand) precisely so you can plan what to give
            // away. Everyone else's unrevealed traits stay censored.
            bool ownFile = _context.Mode == CardMode.Own;

            foreach (var category in Order)
            {
                if (!_slotsByCategory.TryGetValue(category, out var slot) || slot == null) continue;

                TraitSlotV2.SlotState state;
                if (eliminated) state = TraitSlotV2.SlotState.Declassified;
                else if (!player.IsCategoryRevealed(category))
                    state = ownFile ? TraitSlotV2.SlotState.Private : TraitSlotV2.SlotState.Hidden;
                else if (_justRevealed == category) state = TraitSlotV2.SlotState.JustRevealed;
                else state = TraitSlotV2.SlotState.Revealed;

                slot.Apply(player.GetTrait(category), state, animateReveal: _justRevealed == category);
            }

            _justRevealed = null;

            if (_specialSection.activeSelf) RefreshSpecial(player);
        }

        private void RefreshSpecial(PlayerData player)
        {
            bool used = player.HasUsedSpecialCard;

            _specialUseButton.SetActive(!used);
            _specialUsedStamp.SetActive(used);

            LocText.Set(this, _specialTitle, LocKeys.SpecialTitle);

            if (used)
            {
                _specialPlate.Set(BunkerTheme.Sunken, BunkerTheme.SunkenBorder);
                _specialPlate.SetBorderWidth(1f);
                _specialTitle.color = BunkerTheme.Stroke;
                _specialTitle.fontStyle = FontStyles.Bold | FontStyles.UpperCase | FontStyles.Strikethrough;
                _specialDesc.color = BunkerTheme.Stroke;
                LocText.Set(this, _specialDesc, LocKeys.SpecialSpent);
                LocText.Set(this, _specialUsedLabel, LocKeys.SpecialUsedStamp);
            }
            else
            {
                _specialPlate.Set(BunkerTheme.SurfaceRaised, BunkerTheme.Accent);
                _specialPlate.SetBorderWidth(2f);
                _specialTitle.color = BunkerTheme.Accent;
                _specialTitle.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
                _specialDesc.color = BunkerTheme.TextPrimary.WithAlpha(0.78f);
                LocText.Set(this, _specialDesc, LocKeys.SpecialDesc);
                LocText.Set(this, _specialUseLabel, LocKeys.SpecialUse);
            }
        }

        /// A card opened before its state has arrived.
        public void ShowSkeleton()
        {
            foreach (var slot in _slots)
            {
                if (slot != null) slot.ApplySkeleton();
            }
            _specialSection.SetActive(false);
        }
    }
}
