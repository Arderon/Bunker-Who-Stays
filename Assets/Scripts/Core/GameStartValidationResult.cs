namespace Bunker.Core
{
    // Result of pre-start checks, so the lobby UI can show a specific,
    // actionable message instead of a generic "cannot start" error.
    public class GameStartValidationResult
    {
        public bool CanStart;
        public string FailReason;

        public static GameStartValidationResult Ok() => new() { CanStart = true };
        public static GameStartValidationResult Fail(string reason) =>
            new() { CanStart = false, FailReason = reason };
    }
}