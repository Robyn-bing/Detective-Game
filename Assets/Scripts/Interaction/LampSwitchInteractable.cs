using DetectiveGame.Input;
using UnityEngine;

namespace DetectiveGame.Interaction
{
    [DisallowMultipleComponent]
    public sealed class LampSwitchInteractable : WorldInteractable
    {
        [SerializeField] private Light pointLight;
        public Light PointLight => pointLight;
        public bool IsOn => pointLight != null && pointLight.enabled;
        public override string PromptText => IsOn ? "【E 关灯】" : "【E 开灯】";
        public override bool IsInteractionAvailable => base.IsInteractionAvailable && pointLight != null &&
            (GameInputModeController.Current == null || GameInputModeController.Current.IsGameplay);

        public override void Interact(Transform interactor)
        {
            if (!IsInteractionAvailable || interactor == null) return;
            pointLight.enabled = !pointLight.enabled;
        }
    }
}
