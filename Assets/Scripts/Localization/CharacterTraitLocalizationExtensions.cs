using System.Threading.Tasks;
using Bunker.Core;

namespace Bunker.Localization
{
    // Extension methods so game-logic data classes (kept Unity-free in
    // section 1.1) can still be localized conveniently from UI code,
    // without CharacterTrait itself depending on the Localization package.
    public static class CharacterTraitLocalizationExtensions
    {
        public static Task<string> GetLocalizedTextAsync(this CharacterTrait trait)
        {
            return LocalizedTextService.GetTextAsync(LocalizationTableNames.CardContent, trait.LocalizationKey);
        }
    }

    public static class SpecialCardLocalizationExtensions
    {
        public static Task<string> GetLocalizedTextAsync(this SpecialCard card)
        {
            return LocalizedTextService.GetTextAsync(LocalizationTableNames.CardContent, card.LocalizationKey);
        }
    }
}