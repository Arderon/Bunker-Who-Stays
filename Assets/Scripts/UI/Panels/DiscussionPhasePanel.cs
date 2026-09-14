using Bunker.Core;
using Bunker.Localization;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bunker.UI
{
    public class DiscussionPhasePanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _timeLeftLabel;
        [SerializeField] private Button _skipButton;

        private IGameSessionView _session;
        private Coroutine _timerRoutine;

        public void Bind(IGameSessionView session)
        {
            _session = session;
            _skipButton.onClick.AddListener(() => _session.StartVotingPhase());
        }

        private void OnEnable()
        {
            if (_session == null) return;

            // Bind() runs once at game start while every phase panel is bound
            // up front (GameScreen.Bind) — this panel is still inactive at
            // that point (the game starts in Reveal), and Unity can't start a
            // coroutine on an inactive GameObject. Deferred here instead,
            // same as the timer below.
            StartCoroutine(LocalizedTextService.GetTextCoroutine(
                LocalizationTableNames.UI, "ui_discussion_title", text => _titleLabel.text = text));

            if (_timerRoutine != null) StopCoroutine(_timerRoutine);
            _timerRoutine = StartCoroutine(CountdownRoutine(120)); // duration should match StartDiscussionPhase's value
        }

        private void OnDisable()
        {
            if (_timerRoutine != null) StopCoroutine(_timerRoutine);
        }

        private IEnumerator CountdownRoutine(int totalSeconds)
        {
            int remaining = totalSeconds;
            while (remaining > 0 && gameObject.activeSelf)
            {
                var timeText = $"{remaining / 60}:{remaining % 60:00}";
                yield return LocalizedTextService.GetTextCoroutine(
                    LocalizationTableNames.UI, "ui_discussion_time_left",
                    text => _timeLeftLabel.text = text, timeText);

                yield return new WaitForSeconds(1f);
                remaining--;
            }

            if (gameObject.activeSelf)
            {
                _session.StartVotingPhase();
            }
        }
    }
}