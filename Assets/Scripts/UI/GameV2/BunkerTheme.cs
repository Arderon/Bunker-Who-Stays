using UnityEngine;

namespace Bunker.UI.GameV2
{
    // Design tokens. Prefabs carry the resting colours; this is what the code
    // reaches for when a state changes, so every runtime recolour comes from the
    // same palette the prefabs were authored against.
    //
    // Sprites are authored assets, not generated here — run
    // Tools > Bunker > Generate UI Sprites once to produce the full set.
    public static class BunkerTheme
    {
        // --- Colour -----------------------------------------------------------

        public static readonly Color Bg = Hex("0B0C0B");
        public static readonly Color Surface = Hex("17191A");
        public static readonly Color SurfaceRaised = Hex("212426");
        public static readonly Color Border = Hex("34383B");
        public static readonly Color TextPrimary = Hex("E8E4DC");
        public static readonly Color TextSecondary = Hex("9A9892");
        public static readonly Color TextDisabled = Hex("5E605E");
        public static readonly Color Accent = Hex("FFB02E");
        public static readonly Color Warning = Hex("E56B1F");
        public static readonly Color Danger = Hex("C8271F");
        public static readonly Color Success = Hex("4E8C55");
        public static readonly Color Scrim = new Color(0f, 0f, 0f, 0.72f);
        public static readonly Color ScrimModal = new Color(0f, 0f, 0f, 0.88f);

        // Derived surfaces the spec names directly.
        public static readonly Color Sunken = Hex("1B1D1E");        // hidden slot, eliminated plate
        public static readonly Color SunkenBorder = Hex("2C2F31");
        public static readonly Color AccentPlate = Hex("2B2318");   // enabled button, just-revealed slot
        public static readonly Color AccentValue = Hex("FFF3DF");
        public static readonly Color HeaderPlate = Hex("1E2021");
        public static readonly Color PanelPlate = Hex("141617");    // reveal panel ground
        public static readonly Color PanelPlateInert = Hex("121314");
        public static readonly Color InertPlate = Hex("191B1C");    // not-your-turn button
        public static readonly Color Stroke = Hex("6E7276");        // pip stroke, browsing frame
        public static readonly Color StrokeDim = Hex("4A4C4A");
        public static readonly Color PipWell = Hex("151717");
        public static readonly Color TrackDim = Hex("2A2D2F");
        public static readonly Color TrackDimmer = Hex("232526");
        public static readonly Color SuccessPlate = Hex("1E2A20");
        public static readonly Color SuccessBright = Hex("8FD39A");
        public static readonly Color SuccessDim = Hex("3E5B42");
        public static readonly Color Skeleton = Hex("2A2D2F");
        public static readonly Color Ghost = Hex("3E4142");

        // --- Type scale — five sizes -------------------------------------------

        public const float SizeXL = 96f;        // timer numerals, mono bold
        public const float SizeL = 48f;         // card name, turn call
        public const float SizeM = 34f;         // buttons, player names
        public const float SizeS = 30f;         // trait values, body
        public const float SizeXS = 22f;        // meta, counts, stamps
        public const float SizeRowName = 38f;   // roster row name, waiting headline
        public const float SizeSlotLabel = 26f;
        public const float SizeBadge = 24f;

        // --- Geometry ----------------------------------------------------------

        public const int Gutter = 32;
        public const int PanelPadding = 32;
        public const int SlotGap = 16;
        public const int MinTouch = 88;

        public const int RadiusChip = 2;
        public const int RadiusSlot = 4;
        public const int RadiusPanel = 8;

        /// Authoring resolution. Set the Canvas Scaler reference resolution to
        /// this and every number in the spec lands 1:1.
        public static readonly Vector2 Reference = new Vector2(1080f, 1920f);

        public const float StatusBarHeight = 110f;
        public const float HomeIndicator = 56f;
        public const float BarTop = StatusBarHeight + Gutter;   // 142
        public const float BarHeight = 112f;
        public const float RevealPanelHeight = 560f;

        // --- Timing ------------------------------------------------------------

        public const int TurnSeconds = 15;
        public const int TimerWarningAt = 10;
        public const int TimerCriticalAt = 5;

        public static Color WithAlpha(this Color c, float a)
        {
            c.a = a;
            return c;
        }

        private static Color Hex(string rgb)
        {
            return new Color(
                int.Parse(rgb.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255f,
                int.Parse(rgb.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255f,
                int.Parse(rgb.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255f,
                1f);
        }
    }
}
