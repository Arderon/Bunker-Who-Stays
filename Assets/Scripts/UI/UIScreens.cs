using UnityEngine;

namespace Bunker.UI
{
    // Base class for every full-screen UI panel (MainMenu, Lobby, Game, etc.).
    // Handles the common show/hide behavior so screens don't duplicate it.
    public abstract class UIScreen : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _canvasGroup;

        public virtual void Show()
        {
            gameObject.SetActive(true);
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
            }
            OnShown();
        }

        public virtual void Hide()
        {
            OnHidden();
            gameObject.SetActive(false);
        }

        // Called after the screen becomes visible. Override to refresh
        // dynamic content (e.g. re-subscribe to events, reset input fields).
        protected virtual void OnShown() { }

        // Called right before the screen is hidden. Override to unsubscribe
        // from events, cancel coroutines, etc.
        protected virtual void OnHidden() { }
    }
}