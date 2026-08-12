using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Bunker.Content;
using Bunker.Core;

public static class TestContentSeeder
{
    private const string TraitsFolder = "Assets/Content/Traits";
    private const string SpecialCardsFolder = "Assets/Content/SpecialCards";

    private struct TraitData
    {
        public string Id;
        public string LocKey;
        public TraitData(string id, string locKey) { Id = id; LocKey = locKey; }
    }

    [MenuItem("Tools/Bunker/Generate Test Content")]
    public static void GenerateTestContent()
    {
        EnsureFolder(TraitsFolder);
        EnsureFolder(SpecialCardsFolder);

        var data = new Dictionary<CardCategory, List<TraitData>>
        {
            [CardCategory.Gender] = new()
            {
                new("gender_male", "trait_gender_male"),
                new("gender_female", "trait_gender_female"),
            },
            [CardCategory.Age] = new()
            {
                new("age_5", "trait_age_5"), new("age_12", "trait_age_12"),
                new("age_19", "trait_age_19"), new("age_27", "trait_age_27"),
                new("age_35", "trait_age_35"), new("age_45", "trait_age_45"),
                new("age_58", "trait_age_58"), new("age_67", "trait_age_67"),
                new("age_74", "trait_age_74"), new("age_82", "trait_age_82"),
            },
            [CardCategory.Health] = new()
            {
                new("health_perfect", "trait_health_perfect"),
                new("health_mild_cold", "trait_health_mild_cold"),
                new("health_allergy", "trait_health_allergy"),
                new("health_asthma", "trait_health_asthma"),
                new("health_diabetes", "trait_health_diabetes"),
                new("health_insomnia", "trait_health_insomnia"),
                new("health_poor_vision", "trait_health_poor_vision"),
                new("health_heart_condition", "trait_health_heart_condition"),
                new("health_mild_anxiety", "trait_health_mild_anxiety"),
                new("health_hiv_remission", "trait_health_hiv_remission"),
            },
            [CardCategory.Profession] = new()
            {
                new("profession_surgeon", "trait_profession_surgeon"),
                new("profession_teacher", "trait_profession_teacher"),
                new("profession_engineer", "trait_profession_engineer"),
                new("profession_programmer", "trait_profession_programmer"),
                new("profession_cook", "trait_profession_cook"),
                new("profession_police", "trait_profession_police"),
                new("profession_firefighter", "trait_profession_firefighter"),
                new("profession_farmer", "trait_profession_farmer"),
                new("profession_psychologist", "trait_profession_psychologist"),
                new("profession_lawyer", "trait_profession_lawyer"),
                new("profession_electrician", "trait_profession_electrician"),
                new("profession_soldier", "trait_profession_soldier"),
            },
            [CardCategory.Baggage] = new()
            {
                new("baggage_medkit", "trait_baggage_medkit"),
                new("baggage_seeds", "trait_baggage_seeds"),
                new("baggage_generator", "trait_baggage_generator"),
                new("baggage_weapon", "trait_baggage_weapon"),
                new("baggage_survival_books", "trait_baggage_survival_books"),
                new("baggage_radio", "trait_baggage_radio"),
                new("baggage_tools", "trait_baggage_tools"),
                new("baggage_canned_food", "trait_baggage_canned_food"),
                new("baggage_water_pump", "trait_baggage_water_pump"),
                new("baggage_tent", "trait_baggage_tent"),
            },
            [CardCategory.Hobby] = new()
            {
                new("hobby_painting", "trait_hobby_painting"),
                new("hobby_guitar", "trait_hobby_guitar"),
                new("hobby_chess", "trait_hobby_chess"),
                new("hobby_fishing", "trait_hobby_fishing"),
                new("hobby_yoga", "trait_hobby_yoga"),
                new("hobby_cooking", "trait_hobby_cooking"),
                new("hobby_archery", "trait_hobby_archery"),
                new("hobby_gardening", "trait_hobby_gardening"),
                new("hobby_coding", "trait_hobby_coding"),
                new("hobby_hunting", "trait_hobby_hunting"),
            },
            [CardCategory.Fact] = new()
            {
                new("fact_criminal_record", "trait_fact_criminal_record"),
                new("fact_claustrophobia", "trait_fact_claustrophobia"),
                new("fact_army_veteran", "trait_fact_army_veteran"),
                new("fact_psych_ward_history", "trait_fact_psych_ward_history"),
                new("fact_red_cross_volunteer", "trait_fact_red_cross_volunteer"),
                new("fact_gambling_addiction", "trait_fact_gambling_addiction"),
                new("fact_saved_a_life", "trait_fact_saved_a_life"),
                new("fact_former_cult_member", "trait_fact_former_cult_member"),
                new("fact_single_parent_of_three", "trait_fact_single_parent_of_three"),
                new("fact_rare_blood_type", "trait_fact_rare_blood_type"),
            },
        };

        foreach (var (category, entries) in data)
        {
            var pool = ScriptableObject.CreateInstance<TraitPoolSO>();
            pool.Category = category;
            pool.Entries = new List<TraitEntrySO>();

            // Gender, Age and Health are naturally shared between players.
            pool.AllowRepeatsWithinGame = category is CardCategory.Gender
                or CardCategory.Age
                or CardCategory.Health;

            foreach (var entry in entries)
            {
                var traitAsset = ScriptableObject.CreateInstance<TraitEntrySO>();
                traitAsset.Id = entry.Id;
                traitAsset.Category = category;
                traitAsset.LocalizationKey = entry.LocKey;
                traitAsset.Weight = 1;

                var path = $"{TraitsFolder}/{entry.Id}.asset";
                AssetDatabase.CreateAsset(traitAsset, path);
                pool.Entries.Add(traitAsset);
            }

            AssetDatabase.CreateAsset(pool, $"{TraitsFolder}/Pool_{category}.asset");
        }

        GenerateSpecialCards();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[TestContentSeeder] Test content generated successfully.");
    }

    private static void GenerateSpecialCards()
    {
        var specials = new (string id, string effect, string locKey)[]
        {
            ("special_peek", SpecialCardEffectType.RevealHiddenTrait, "special_peek"),
            ("special_immunity", SpecialCardEffectType.VoteImmunity, "special_immunity"),
            ("special_swap", SpecialCardEffectType.SwapTrait, "special_swap"),
            ("special_expose", SpecialCardEffectType.ForceRevealAll, "special_expose"),
        };

        var pool = ScriptableObject.CreateInstance<SpecialCardPoolSO>();
        pool.Entries = new List<SpecialCardEntrySO>();

        foreach (var (id, effect, locKey) in specials)
        {
            var cardAsset = ScriptableObject.CreateInstance<SpecialCardEntrySO>();
            cardAsset.Id = id;
            cardAsset.EffectType = effect;
            cardAsset.LocalizationKey = locKey;

            AssetDatabase.CreateAsset(cardAsset, $"{SpecialCardsFolder}/{id}.asset");
            pool.Entries.Add(cardAsset);
        }

        AssetDatabase.CreateAsset(pool, $"{SpecialCardsFolder}/Pool_SpecialCards.asset");
    }

    private static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            var parent = Path.GetDirectoryName(path).Replace("\\", "/");
            var folderName = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}