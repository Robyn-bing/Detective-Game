using System;
namespace DetectiveGame.Dialogue
{
    [Serializable]
    public sealed class DialogueChoiceOption
    {
        public string Label;
        public bool Interactable = true;
        public DialogueChoiceOption(string label, bool interactable) { Label = label; Interactable = interactable; }
    }
}
