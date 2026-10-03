using System.Collections;
using DetectiveGame.Dialogue;
using DetectiveGame.Interaction;
using DetectiveGame.Player;
using Ink.UnityIntegration;
using UnityEngine;
using UnityEngine.Events;

namespace DetectiveGame.Tutorial
{
    [DisallowMultipleComponent, RequireComponent(typeof(TelephoneDialController))]
    public sealed class TutorialPhoneCallController : MonoBehaviour
    {
        public enum CallState { Idle, Connecting, Dialogue, FadingOut, Returning, Completed }
        [SerializeField] private TelephoneDialController telephone;
        [SerializeField] private DialogueController dialogue;
        [SerializeField] private InkFile phoneDialogue;
        [SerializeField] private PhoneCallVisual phoneCallVisual;
        [SerializeField, Min(0f)] private float connectionPauseSeconds = 0.8f;
        [SerializeField, Min(0f)] private float phoneCallFadeOutDuration = 0.7f;
        [SerializeField, Min(0f)] private float phoneCallFadeInDuration = 0.7f;
        [SerializeField] private UnityEvent onTutorialPhoneCallCompleted = new UnityEvent();
        public UnityEvent OnTutorialPhoneCallCompleted => onTutorialPhoneCallCompleted;
        public CallState State { get; private set; }
        public bool Completed => State == CallState.Completed;
        private Coroutine sequence;
        private bool shuttingDown;
        private void Awake() { if (telephone == null) telephone = GetComponent<TelephoneDialController>(); }
        private void OnEnable()
        {
            shuttingDown = false;
            telephone.OnDialSuccess.AddListener(OnConnected);
        }
        private void OnDisable()
        {
            shuttingDown = true;
            telephone.OnDialSuccess.RemoveListener(OnConnected);
            if (sequence != null) StopCoroutine(sequence);
            sequence = null;
            if (dialogue != null) { dialogue.AbortHostedDialogue(telephone); dialogue.SetPresentationBlocked(false); }
            if (phoneCallVisual != null) phoneCallVisual.HideImmediate();
            if (State != CallState.Idle && !Completed)
            {
                if (ScreenFade.Current != null && !ScreenFade.Current.IsLoading) ScreenFade.Current.SetAlpha(0f);
                telephone.FinishStoryReturn(false);
                State = CallState.Idle;
            }
        }
        private void OnConnected()
        {
            if (State != CallState.Idle) return;
            telephone.BeginStoryPresentation(false);
            State = CallState.Connecting;
            sequence = StartCoroutine(Connect());
        }
        private IEnumerator Connect()
        {
            var fade = ScreenFade.Current;
            if (dialogue == null || phoneCallVisual == null || phoneDialogue == null || !phoneDialogue.isCompiled || fade == null)
            {
                Debug.LogError("Configure phone Ink, shared Dialogue, PhoneCallVisual and ScreenFade.", this);
                telephone.FinishStoryReturn(false); State = CallState.Idle; yield break;
            }
            yield return fade.FadeTo(1f, phoneCallFadeOutDuration);
            // All UI swaps happen behind an opaque screen. PhoneCallVisual stays independently opaque.
            telephone.BeginStoryPresentation();
            phoneCallVisual.Show();
            dialogue.SetPresentationBlocked(true);
            if (!dialogue.BeginHostedDialogue(phoneDialogue, telephone, OnDialogueFinished, true))
            { yield return Finish(false); yield break; }
            yield return new WaitForSecondsRealtime(connectionPauseSeconds);
            yield return fade.FadeTo(0f, phoneCallFadeInDuration);
            dialogue.SetPresentationBlocked(false);
            State = CallState.Dialogue;
        }
        private void OnDialogueFinished(bool completed)
        {
            if (shuttingDown) return;
            dialogue.SetPresentationBlocked(true);
            sequence = StartCoroutine(Finish(completed));
        }
        private IEnumerator Finish(bool completed)
        {
            State = CallState.FadingOut;
            var fade = ScreenFade.Current;
            if (fade != null) yield return fade.FadeTo(1f, phoneCallFadeOutDuration);
            dialogue.HidePresentation();
            phoneCallVisual.HideImmediate();
            telephone.PrepareStoryReturn();
            State = CallState.Returning;
            yield return null;
            if (fade != null) yield return fade.FadeTo(0f, phoneCallFadeInDuration);
            dialogue.SetPresentationBlocked(false);
            telephone.FinishStoryReturn(completed);
            State = completed ? CallState.Completed : CallState.Idle;
            if (completed) onTutorialPhoneCallCompleted.Invoke();
        }
    }
}
