using UnityEngine.UIElements;

namespace Bunker.UI
{
    // Base for every full-screen controller. Unlike the uGUI UIScreen (a
    // MonoBehaviour on a prefab), this wraps a VisualElement subtree cloned
    // from a UXML asset — there is no GameObject per screen.
    public abstract class ScreenController
    {
        public VisualElement Root { get; }

        protected ScreenController(VisualElement root)
        {
            Root = root;
            Hide();
        }

        public virtual void Show()
        {
            Root.style.display = DisplayStyle.Flex;
            OnShown();
        }

        public virtual void Hide()
        {
            OnHidden();
            Root.style.display = DisplayStyle.None;
        }

        protected virtual void OnShown() { }
        protected virtual void OnHidden() { }
    }
}