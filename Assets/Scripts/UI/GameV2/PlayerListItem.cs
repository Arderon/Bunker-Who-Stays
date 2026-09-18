using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bunker.UI.GameV2
{
    /// <summary>
    /// Стан гравця в списку раунду. Один рядок — рівно один стан, тому вони
    /// пріоритезовані: Expelled &gt; Revealing &gt; NextUp &gt; You &gt; Revealed.
    /// Наприклад, коли зараз ваш хід, рядок показує Revealing, а не You —
    /// хто зараз ходить важливіше за те, що це саме ви.
    /// </summary>
    public enum PlayerState
    {
        Revealing,   // зараз розкриває картку
        NextUp,      // наступний у черзі
        You,         // це локальний гравець (коли жоден вищий стан не застосовний)
        Revealed,    // вже розкрив (або ще не розкрив, але хід не його і не наступний)
        Expelled     // вигнаний з бункера
    }

    /// <summary>
    /// Візуальний опис одного стану — заповнюється в інспекторі,
    /// без потреби лізти в код при зміні кольору чи макету.
    /// Текстові поля тримають КЛЮЧІ локалізації (таблиця UI-Table),
    /// а не самі рядки — переклад uk/en підтягується автоматично.
    /// </summary>
    [Serializable]
    public struct PlayerStateVisual : IStateVisual<PlayerState>
    {
        public PlayerState state;
        public PlayerState State => state;

        [Header("Рамка / фон")]
        public bool showBorder;
        public Color borderColor;

        [Header("Іконка зліва")]
        public Sprite iconSprite;

        [Header("Текст статусу")]
        [Tooltip("Ключ у таблиці UI-Table, а НЕ сам текст (напр. ui_v2_revealed_count). " +
                 "Може містити {0}, {1} — підставляються з Bind/ApplyState.")]
        public string statusKey;

        [Header("Бейдж справа (EXPELLED / YOU)")]
        public bool showBadge;
        [Tooltip("Ключ у таблиці UI-Table (напр. ui_v2_expelled, ui_v2_you).")]
        public string badgeKey;
        public Color badgeColor;

        [Header("Ім'я")]
        public bool strikethroughName;
        public Color nameColor;
    }

    /// <summary>
    /// Один рядок списку гравців. Логіка тонка — весь вигляд стану
    /// береться з таблиці PlayerStateVisual, налаштованої в інспекторі,
    /// тому додавання нового стану чи зміна кольору не вимагає правок коду.
    /// </summary>
    public class PlayerListItem : MonoBehaviour
    {
        [Header("UI references")]
        [SerializeField] private Image borderImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private GameObject badgeRoot;
        [SerializeField] private TMP_Text badgeText;
        [SerializeField] private Image badgeBackground;

        [Header("Клік по рядку — відкриває картку гравця")]
        [Tooltip("Необов'язково: без нього рядок просто не реагує на дотик.")]
        [SerializeField] private Button button;

        [Header("Конфігурація станів (заповнити в інспекторі)")]
        [SerializeField] private PlayerStateVisual[] stateVisuals;

        private PlayerState _currentState;
        private string _playerId;
        private object[] _statusArgs = Array.Empty<object>();
        private Action<string> _onSelected;
        private bool _bound;

        public string PlayerId => _playerId;

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(() => _onSelected?.Invoke(_playerId));
        }

        private void OnEnable()
        {
            // Localization is a coroutine and no-ops while this row is inactive
            // (see LocText.Set) — re-run it once the row, or its parent overlay,
            // comes back on screen, matching the pattern the rest of GameV2 uses.
            if (_bound) ApplyVisual(StateVisual.Find(stateVisuals, _currentState, this));
        }

        /// <summary>Хто отримує id гравця при тапі по рядку.</summary>
        public void SetCallback(Action<string> onSelected) => _onSelected = onSelected;

        /// <summary>Виклик при створенні/переспавні рядка (наприклад, в OnScrollView pooling).</summary>
        public void Bind(string playerId, string playerName, PlayerState state, params object[] statusArgs)
        {
            _playerId = playerId;
            nameText.text = playerName;
            _bound = true;
            ApplyState(state, statusArgs);
        }

        /// <summary>Викликати цей метод, коли з сервера (Colyseus state change) прийшов новий стан гравця.</summary>
        public void ApplyState(PlayerState state, params object[] statusArgs)
        {
            _currentState = state;
            _statusArgs = statusArgs ?? Array.Empty<object>();
            ApplyVisual(StateVisual.Find(stateVisuals, state, this));
        }

        private void ApplyVisual(PlayerStateVisual visual)
        {
            borderImage.gameObject.SetActive(visual.showBorder);
            borderImage.color = visual.borderColor;

            iconImage.sprite = visual.iconSprite;

            if (string.IsNullOrEmpty(visual.statusKey)) statusText.text = string.Empty;
            else LocText.Set(this, statusText, visual.statusKey, _statusArgs);

            badgeRoot.SetActive(visual.showBadge);
            if (visual.showBadge)
            {
                if (string.IsNullOrEmpty(visual.badgeKey)) badgeText.text = string.Empty;
                else LocText.Set(this, badgeText, visual.badgeKey);
                badgeBackground.color = visual.badgeColor;
            }

            nameText.color = visual.nameColor;
            nameText.fontStyle = visual.strikethroughName
                ? FontStyles.Strikethrough
                : FontStyles.Normal;
        }
    }
}
