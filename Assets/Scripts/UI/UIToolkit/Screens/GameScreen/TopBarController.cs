using UnityEngine.UIElements;
using Bunker.Core;
using Bunker.Localization;

namespace Bunker.UI
{
    public class TopBarController
    {
        public VisualElement Root { get; }

        private readonly Label _roundLabel;
        private readonly VisualElement _statusStrip;
        private readonly VisualTreeAsset _statusIconAsset;
        private GameSession _session;

        public TopBarController(VisualElement root, VisualTreeAsset statusIconAsset)
        {
            Root = root;
            _statusIconAsset = statusIconAsset;
            _roundLabel = root.Q<Label>("round-label");
            _statusStrip = root.Q<VisualElement>("player-status-strip");
        }

        public void Bind(GameSession session)
        {
            _session = session;
            _session.OnRoundStarted += _ => RefreshRound();
            _session.OnPlayerEliminated += _ => RefreshStrip();

            RefreshRound();
            RefreshStrip();
        }

        private async void RefreshRound()
        {
            _roundLabel.text = await LocalizedTextService.GetTextAsync(
                LocalizationTableNames.UI, "ui_game_round_label", _session.CurrentRound);
        }

        private void RefreshStrip()
        {
            _statusStrip.Clear();
            foreach (var player in _session.Players)
            {
                var item = _statusIconAsset.CloneTree();
                BindStatusIcon(item, player);
                _statusStrip.Add(item);
            }
        }

        private async void BindStatusIcon(VisualElement element, PlayerData player)
        {
            element.Q<Label>("name-label").text = player.DisplayName;
            var tag = element.Q<Label>("eliminated-tag");
            tag.style.display = player.IsEliminated ? DisplayStyle.Flex : DisplayStyle.None;
            element.style.opacity = player.IsEliminated ? 0.4f : 1f;

            if (player.IsEliminated)
                tag.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, "ui_game_player_eliminated_tag");
        }
    }
}