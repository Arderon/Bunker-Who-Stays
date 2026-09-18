using System;
using TMPro;
using UnityEngine;

namespace Bunker.UI.GameV2
{
    // The two things a per-state visual struct almost always needs to say: how
    // a Plate looks, and how a label reads. Every state-driven element composes
    // its own struct out of these instead of repeating "fill, border, width" or
    // "color, font style" fields by hand.

    [Serializable]
    public struct PlateVisual
    {
        public Color fill;
        public Color border;
        [Range(0f, 8f)] public float borderWidth;

        public readonly void ApplyTo(Plate plate)
        {
            if (plate == null) return;
            plate.Set(fill, border);
            plate.SetBorderWidth(borderWidth);
        }
    }

    [Serializable]
    public struct LabelVisual
    {
        public Color color;
        public FontStyles fontStyle;

        public readonly void ApplyTo(TMP_Text label)
        {
            if (label == null) return;
            label.color = color;
            label.fontStyle = fontStyle;
        }
    }
}
