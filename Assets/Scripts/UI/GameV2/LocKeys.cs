using Bunker.Core;

namespace Bunker.UI.GameV2
{
    // Keys the V2 game screen adds on top of the existing UI table. Kept in one
    // place so the table and the code cannot drift apart silently — a missing
    // key surfaces as the raw key painted on screen.
    public static class LocKeys
    {
        // Player list
        public const string AliveLabel = "ui_v2_alive_label";
        public const string Syncing = "ui_v2_syncing";
        public const string RevealingNow = "ui_v2_revealing_now";
        public const string RevealedCount = "ui_v2_revealed_count";
        public const string NextUp = "ui_v2_next_up";
        public const string You = "ui_v2_you";
        public const string FileClosed = "ui_v2_file_closed";
        public const string Expelled = "ui_v2_expelled";
        public const string TapOutsideToClose = "ui_v2_tap_outside_close";
        public const string WaitingForSurvivors = "ui_v2_waiting_survivors";

        // Trait card
        public const string BadgeYourFile = "ui_v2_badge_your_file";
        public const string BadgeCurrentTurn = "ui_v2_badge_current_turn";
        public const string BadgeBrowsing = "ui_v2_badge_browsing";
        public const string IdAlive = "ui_v2_id_alive";
        public const string IdExpelled = "ui_v2_id_expelled";
        public const string Classified = "ui_v2_classified";
        public const string RevealedThisTurn = "ui_v2_revealed_this_turn";
        public const string FileDeclassified = "ui_v2_file_declassified";
        public const string BackTo = "ui_v2_back_to";
        public const string FileIndex = "ui_v2_file_index";
        public const string SpecialTitle = "ui_v2_special_title";
        public const string SpecialDesc = "ui_v2_special_desc";
        public const string SpecialUse = "ui_v2_special_use";
        public const string SpecialUsedStamp = "ui_v2_special_used_stamp";
        public const string SpecialSpent = "ui_v2_special_spent";

        // Reveal panel
        public const string YourTurn = "ui_v2_your_turn";
        public const string YourTurnHint = "ui_v2_your_turn_hint";
        public const string ChooseNow = "ui_v2_choose_now";
        public const string ChooseNowHint = "ui_v2_choose_now_hint";
        public const string WaitingFor = "ui_v2_waiting_for";
        public const string WaitingHint = "ui_v2_waiting_hint";
        public const string PassComplete = "ui_v2_pass_complete";
        public const string PassCompleteHint = "ui_v2_pass_complete_hint";
        public const string Done = "ui_v2_done";
        public const string StartDiscussion = "ui_v2_start_discussion";
        public const string TimeUp = "ui_v2_time_up";

        public static string Category(CardCategory category)
        {
            return "ui_game_category_" + category.ToString().ToLowerInvariant();
        }
    }
}
