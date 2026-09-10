using System;
using System.Collections.Generic;
using DetectiveGame.Recall;
using UnityEngine;

namespace DetectiveGame.Evidence
{
    [DisallowMultipleComponent]
    public sealed class EvidenceBoardService : MonoBehaviour
    {
        private readonly List<EvidenceDefinition> discoveredEvidence = new List<EvidenceDefinition>();
        private readonly Dictionary<string, EvidenceDefinition> definitions = new Dictionary<string, EvidenceDefinition>();
        private readonly Dictionary<string, RecallObjectData> recallData = new Dictionary<string, RecallObjectData>();
        private readonly Dictionary<string, string> selectedChoices = new Dictionary<string, string>();
        private readonly HashSet<string> reviewedPeriods = new HashSet<string>();

        public static EvidenceBoardService Current { get; private set; }
        public IReadOnlyList<EvidenceDefinition> DiscoveredEvidence => discoveredEvidence;
        public event Action<EvidenceDefinition> EvidenceDiscovered;
        public event Action<EvidenceDefinition> EvidenceUpdated;

        public static EvidenceBoardService GetOrCreate()
        {
            if (Current != null) return Current;
            EvidenceBoardService existing = FindAnyObjectByType<EvidenceBoardService>();
            if (existing != null) return existing;
            return new GameObject("Evidence Board Service (Runtime)").AddComponent<EvidenceBoardService>();
        }

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                Destroy(this);
                return;
            }
            Current = this;
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        public bool RecordReviewedEvidence(
            EvidenceDefinition definition,
            RecallObjectData sourceRecallData,
            int periodIndex)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.EvidenceId)) return false;

            string id = definition.EvidenceId;
            definitions[id] = definition;
            if (sourceRecallData != null) recallData[id] = sourceRecallData;
            reviewedPeriods.Add($"{id}:{periodIndex}");

            bool isNew = !Contains(id);
            if (isNew)
            {
                discoveredEvidence.Add(definition);
                EvidenceDiscovered?.Invoke(definition);
            }
            else EvidenceUpdated?.Invoke(definition);
            return isNew;
        }

        public bool Contains(string evidenceId)
        {
            for (int i = 0; i < discoveredEvidence.Count; i++)
                if (discoveredEvidence[i] != null && discoveredEvidence[i].EvidenceId == evidenceId) return true;
            return false;
        }

        public RecallObjectData GetRecallData(EvidenceDefinition definition)
        {
            if (definition == null) return null;
            recallData.TryGetValue(definition.EvidenceId, out RecallObjectData result);
            return result;
        }

        public bool IsPeriodReviewed(string evidenceId, int periodIndex)
            => reviewedPeriods.Contains($"{evidenceId}:{periodIndex}");

        public void SetSelectedChoice(EvidenceDefinition definition, EvidenceMomentDefinition moment, string choiceId)
        {
            if (definition == null || moment == null || moment.FindChoice(choiceId) == null) return;
            selectedChoices[ChoiceKey(definition.EvidenceId, moment.Id)] = choiceId;
            EvidenceUpdated?.Invoke(definition);
        }

        public string GetSelectedChoiceId(EvidenceDefinition definition, EvidenceMomentDefinition moment)
        {
            if (definition == null || moment == null) return null;
            selectedChoices.TryGetValue(ChoiceKey(definition.EvidenceId, moment.Id), out string choiceId);
            return choiceId;
        }

        public EvidenceChoiceDefinition GetSelectedChoice(
            EvidenceDefinition definition,
            EvidenceMomentDefinition moment)
        {
            return moment?.FindChoice(GetSelectedChoiceId(definition, moment));
        }

        private static string ChoiceKey(string evidenceId, string momentId) => $"{evidenceId}:{momentId}";
    }
}
