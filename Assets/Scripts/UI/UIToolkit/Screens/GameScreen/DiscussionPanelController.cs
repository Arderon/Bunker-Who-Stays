using UnityEngine.UIElements;
using Bunker.Core;
using Bunker.Localization;

namespace Bunker.UI
{
    // Uses VisualElement.schedule (UI Toolkit's built-in scheduler) instead
    // of a MonoBehaviour Coroutine, since this controller is a plain C# class
    // with no Unity lifecycle of its own.
    public class DiscussionPanelController
    {
        public VisualElement Root { get; }

        private readonly Label _titleLabel, _timeLeftLabel;
        private readonly Button _skipButton;
        private GameSession _session;
        private int _remainingSeconds;
        private IVisualElementScheduledItem _scheduledTick;

        public DiscussionPanelController(VisualElement root)
        {
            Root = root;
            _titleLabel = root.Q<Label>("title-label");
            _timeLeftLabel = root.Q<Label>("time-left-label");
            _skipButton = root.Q<Button>("skip-button");

            _skipButton.clicked += () => _session.StartVotingPhase();
            RefreshStaticText();
        }

        private async void RefreshStaticText()
        {
            _titleLabel.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, "ui_discussion_title");
            _skipButton.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, "ui_discussion_skip");
        }

        public void Bind(GameSession session) => _session = session;

        // Called explicitly by GameScreenController when the phase becomes
        // Discussion — UI Toolkit VisualElements have no OnEnable equivalent,
        // so timer start/stop has to be triggered from the outside.
        public void NotifyPhaseEntered(int durationSeconds)
        {
            _remainingSeconds = durationSeconds;
            _scheduledTick?.Pause();
            _scheduledTick = Root.schedule.Execute(Tick).Every(1000);
        }

        private async void Tick()
        {
            if (_remainingSeconds <= 0)
            {
                _scheduledTick?.Pause();
                _session.StartVotingPhase();
                return;
            }

            var timeText = $"{_remainingSeconds / 60}:{_remainingSeconds % 60:00}";
            _timeLeftLabel.text = await LocalizedTextService.GetTextAsync(LocalizationTableNames.UI, "ui_discussion_time_left", timeText);
            _remainingSeconds--;
        }
    }
}