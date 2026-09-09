using System;
using System.Collections.Generic;
using UnityEngine;

namespace DetectiveGame.Investigation
{
    public enum TestimonyCategory { Relationship, Action, Motive }

    [Serializable]
    public sealed class CharacterDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private string role;
        [SerializeField] private Sprite portrait;

        public string Id => id;
        public string DisplayName => displayName;
        public string Role => role;
        public Sprite Portrait => portrait;
    }

    [Serializable]
    public sealed class RelationshipDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private string fromCharacterId;
        [SerializeField] private string toCharacterId;
        [SerializeField] private string displayName;

        public string Id => id;
        public string FromCharacterId => fromCharacterId;
        public string ToCharacterId => toCharacterId;
        public string DisplayName => displayName;
    }

    [Serializable]
    public sealed class TestimonyDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private string characterId;
        [SerializeField] private TestimonyCategory category;
        [SerializeField, TextArea] private string displayText;

        public string Id => id;
        public string CharacterId => characterId;
        public TestimonyCategory Category => category;
        public string DisplayText => displayText;
    }

    [Serializable]
    public sealed class TimePeriodDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private string label;
        [SerializeField] private string startTime;
        [SerializeField] private string endTime;

        public string Id => id;
        public string Label => label;
        public string StartTime => startTime;
        public string EndTime => endTime;
    }

    [CreateAssetMenu(fileName = "InvestigationDatabase", menuName = "Detective Game/Investigation Database")]
    public sealed class InvestigationDatabase : ScriptableObject
    {
        [SerializeField] private List<CharacterDefinition> characters = new List<CharacterDefinition>();
        [SerializeField] private List<RelationshipDefinition> relationships = new List<RelationshipDefinition>();
        [SerializeField] private List<TestimonyDefinition> testimonies = new List<TestimonyDefinition>();
        [SerializeField] private List<TimePeriodDefinition> timePeriods = new List<TimePeriodDefinition>();
        [SerializeField] private List<string> initialKnownCharacterIds = new List<string>();

        public IReadOnlyList<CharacterDefinition> Characters => characters;
        public IReadOnlyList<RelationshipDefinition> Relationships => relationships;
        public IReadOnlyList<TestimonyDefinition> Testimonies => testimonies;
        public IReadOnlyList<TimePeriodDefinition> TimePeriods => timePeriods;
        public IReadOnlyList<string> InitialKnownCharacterIds => initialKnownCharacterIds;

        public bool TryGetCharacter(string id, out CharacterDefinition value)
            => TryFind(characters, item => item.Id == id, out value);

        public bool TryGetRelationship(string id, out RelationshipDefinition value)
            => TryFind(relationships, item => item.Id == id, out value);

        public bool TryGetTestimony(string id, out TestimonyDefinition value)
            => TryFind(testimonies, item => item.Id == id, out value);

        public bool TryGetTimePeriod(string id, out TimePeriodDefinition value)
            => TryFind(timePeriods, item => item.Id == id, out value);

        private static bool TryFind<T>(IReadOnlyList<T> items, Predicate<T> predicate, out T value)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (!predicate(items[i])) continue;
                value = items[i];
                return true;
            }

            value = default;
            return false;
        }

        private void OnValidate()
        {
            ValidateUniqueIds(characters, item => item.Id, "character");
            ValidateUniqueIds(relationships, item => item.Id, "relationship");
            ValidateUniqueIds(testimonies, item => item.Id, "testimony");
            ValidateUniqueIds(timePeriods, item => item.Id, "time period");
        }

        private void ValidateUniqueIds<T>(IReadOnlyList<T> items, Func<T, string> getId, string label)
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < items.Count; i++)
            {
                string id = getId(items[i]);
                if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
                    Debug.LogWarning($"{name} contains an empty or duplicate {label} ID: '{id}'.", this);
            }
        }
    }
}
