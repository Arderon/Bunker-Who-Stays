using System;

namespace Bunker.Core
{
    [Serializable]
    public class CharacterTrait
    {
        public string Id;                 // unique id inside the category, e.g. "profession_surgeon"
        public CardCategory Category;
        public string LocalizationKey;    // key for Unity Localization String Table, NOT the actual text

        public CharacterTrait(string id, CardCategory category, string localizationKey)
        {
            Id = id;
            Category = category;
            LocalizationKey = localizationKey;
        }

        public override string ToString() => $"[{Category}] {Id}";
    }
}