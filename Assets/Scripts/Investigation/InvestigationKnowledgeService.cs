using System;
using System.Collections.Generic;
using UnityEngine;

namespace DetectiveGame.Investigation
{
    [DisallowMultipleComponent]
    public sealed class InvestigationKnowledgeService : MonoBehaviour
    {
        private readonly HashSet<string> knownCharacters = new HashSet<string>();
        private readonly HashSet<string> knownRelationships = new HashSet<string>();
        private readonly HashSet<string> unlockedTestimonies = new HashSet<string>();
        private readonly HashSet<string> unlockedTimePeriods = new HashSet<string>();

        public static InvestigationKnowledgeService Current { get; private set; }
        public InvestigationDatabase Database { get; private set; }
        public event Action KnowledgeChanged;
        public event Action DialogueCompleted;

        private void Awake()
        {
            Current = this;
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        public void Configure(InvestigationDatabase database)
        {
            Database = database;
            ResetRuntimeProgress();
        }

        public void ResetRuntimeProgress()
        {
            knownCharacters.Clear();
            knownRelationships.Clear();
            unlockedTestimonies.Clear();
            unlockedTimePeriods.Clear();

            if (Database != null)
            {
                IReadOnlyList<string> initial = Database.InitialKnownCharacterIds;
                for (int i = 0; i < initial.Count; i++) knownCharacters.Add(initial[i]);
            }

            KnowledgeChanged?.Invoke();
        }

        public bool IsCharacterKnown(string id) => knownCharacters.Contains(id);
        public bool IsRelationshipKnown(string id) => knownRelationships.Contains(id);
        public bool IsTestimonyUnlocked(string id) => unlockedTestimonies.Contains(id);
        public bool IsTimePeriodUnlocked(string id) => unlockedTimePeriods.Contains(id);

        public bool DiscoverCharacter(string id)
            => Unlock(id, knownCharacters, Database != null && Database.TryGetCharacter(id, out _), "character");

        public bool UnlockRelationship(string id)
            => Unlock(id, knownRelationships, Database != null && Database.TryGetRelationship(id, out _), "relationship");

        public bool UnlockTestimony(string id)
            => Unlock(id, unlockedTestimonies, Database != null && Database.TryGetTestimony(id, out _), "testimony");

        public bool UnlockTimePeriod(string id)
            => Unlock(id, unlockedTimePeriods, Database != null && Database.TryGetTimePeriod(id, out _), "time period");

        public void ApplyInkTag(string rawTag)
        {
            if (string.IsNullOrWhiteSpace(rawTag)) return;
            string tag = rawTag.Trim();

            if (TryReadValue(tag, "discover:", out string characterId)) DiscoverCharacter(characterId);
            else if (TryReadValue(tag, "relationship:", out string relationshipId)) UnlockRelationship(relationshipId);
            else if (TryReadValue(tag, "unlock:", out string testimonyId)) UnlockTestimony(testimonyId);
            else if (TryReadValue(tag, "unlock_time:", out string periodId)) UnlockTimePeriod(periodId);
        }

        public void NotifyDialogueCompleted() => DialogueCompleted?.Invoke();

        public static bool IsTimePeriodUnlockedGlobally(string id)
            => Current != null && Current.IsTimePeriodUnlocked(id);

        private bool Unlock(string id, HashSet<string> target, bool knownDefinition, string label)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            if (!knownDefinition)
            {
                Debug.LogWarning($"Ignoring unknown investigation {label} ID '{id}'.", this);
                return false;
            }

            if (!target.Add(id)) return false;
            Debug.Log($"Investigation unlocked {label}: {id}", this);
            KnowledgeChanged?.Invoke();
            return true;
        }

        private static bool TryReadValue(string tag, string prefix, out string value)
        {
            if (tag.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                value = tag.Substring(prefix.Length).Trim();
                return !string.IsNullOrWhiteSpace(value);
            }

            value = null;
            return false;
        }
    }
}
