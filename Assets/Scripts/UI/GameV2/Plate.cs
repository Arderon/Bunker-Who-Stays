using System;
using Bunker.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bunker.UI.GameV2
{
    // Every surface in this design is a flat fill inside a 1-2px machined border,
    // and the two are tinted independently (amber border on a dark plate, for
    // one). A single uGUI Image cannot do that, so a plate is two stacked
    // Images: Border on the outside, Fill stretched inside it by the border
    // width. Author it once in a prefab, wire both here, and the code only ever
    // changes colours.
    [Serializable]
    public class Plate
    {
        public Image Border;
        public Image Fill;

        public void Set(Color fill, Color border)
        {
            if (Fill != null) Fill.color = fill;
            if (Border != null) Border.color = border;
        }

        public void SetBorderSprite(Sprite sprite)
        {
            if (Border != null && sprite != null) Border.sprite = sprite;
        }

        /// Widens or narrows the fill inset, which is how a 1px plate becomes a
        /// 2px one for the enabled/just-revealed states without a second prefab.
        public void SetBorderWidth(float width)
        {
            if (Fill == null) return;
            var rt = Fill.rectTransform;
            rt.offsetMin = new Vector2(width, width);
            rt.offsetMax = new Vector2(-width, -width);
        }
    }

    public static class LocText
    {
        /// Fire-and-forget localized text from the UI table.
        ///
        /// Resolving is a coroutine, and Unity refuses to start one on an
        /// inactive GameObject, so this no-ops while the host is hidden. Every
        /// caller re-runs its bind from OnEnable, which is what makes that safe.
        public static void Set(MonoBehaviour host, TMP_Text target, string key, params object[] args)
        {
            if (host == null || target == null || string.IsNullOrEmpty(key)) return;
            if (!host.gameObject.activeInHierarchy) return;

            host.StartCoroutine(LocalizedTextService.GetTextCoroutine(
                LocalizationTableNames.UI, key,
                text => { if (target != null) target.text = text; },
                args));
        }

        /// Trait and special-card names live in the CardContent table, keyed by
        /// the content asset's own localizationKey.
        public static void SetContent(MonoBehaviour host, TMP_Text target, string key)
        {
            if (host == null || target == null || string.IsNullOrEmpty(key)) return;
            if (!host.gameObject.activeInHierarchy) return;

            host.StartCoroutine(LocalizedTextService.GetTextCoroutine(
                LocalizationTableNames.CardContent, key,
                text => { if (target != null) target.text = text; }));
        }
    }
}
