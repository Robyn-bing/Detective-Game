using System;
using UnityEngine;

namespace DetectiveGame.Evidence
{
    [Serializable]
    public sealed class EvidenceChoiceDefinition
    {
        [SerializeField] private string id;
        [SerializeField, TextArea] private string text;

        public string Id => id;
        public string Text => text;
    }

    [Serializable]
    public sealed class EvidenceMomentDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private string timeLabel;
        [SerializeField, Range(0f, 1f)] private float normalizedTime;
        [SerializeField, TextArea] private string observedFact;
        [SerializeField] private EvidenceChoiceDefinition[] choices = Array.Empty<EvidenceChoiceDefinition>();
        [SerializeField, Tooltip("Optional stable choice ID for later deduction validation. No correctness feedback is shown yet.")]
        private string correctChoiceId;

        public string Id => id;
        public string TimeLabel => timeLabel;
        public float NormalizedTime => normalizedTime;
        public string ObservedFact => observedFact;
        public int ChoiceCount => choices?.Length ?? 0;
        public string CorrectChoiceId => correctChoiceId;
        public EvidenceChoiceDefinition GetChoice(int index) => choices[index];

        public EvidenceChoiceDefinition FindChoice(string choiceId)
        {
            for (int i = 0; i < ChoiceCount; i++)
                if (choices[i] != null && choices[i].Id == choiceId) return choices[i];
            return null;
        }
    }

    [CreateAssetMenu(fileName = "Evidence", menuName = "Detective Game/Evidence Definition")]
    public sealed class EvidenceDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string evidenceId;
        [SerializeField] private string displayName = "Evidence";
        [SerializeField] private Sprite icon;
        [SerializeField, TextArea(3, 7)] private string description;

        [Header("Recall Review")]
        [SerializeField, Min(0)] private int previewPeriodIndex;
        [SerializeField] private EvidenceMomentDefinition[] moments = Array.Empty<EvidenceMomentDefinition>();

        public string EvidenceId => evidenceId;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public string Description => description;
        public int PreviewPeriodIndex => previewPeriodIndex;
        public int MomentCount => moments?.Length ?? 0;
        public EvidenceMomentDefinition GetMoment(int index) => moments[index];
    }
}
