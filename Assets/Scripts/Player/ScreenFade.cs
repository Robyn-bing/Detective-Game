using System;
using System.Collections;
using DetectiveGame.Input;
using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace DetectiveGame.Player
{
    [DisallowMultipleComponent, RequireComponent(typeof(CanvasGroup))]
    public sealed class ScreenFade : MonoBehaviour
    {
        public static ScreenFade Current { get; private set; }
        private CanvasGroup group;
        private bool loading;
        private GameInputModeController arrivalInput;
        public float Alpha => group != null ? group.alpha : 0f;
        public bool IsLoading => loading;
        private void Awake()
        {
            if (Current != null && Current != this) { Destroy(gameObject); return; }
            Current = this; group = GetComponent<CanvasGroup>();
            DontDestroyOnLoad(gameObject); SetAlpha(0f);
        }
        public void SetAlpha(float alpha)
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            group.alpha = Mathf.Clamp01(alpha); group.blocksRaycasts = group.alpha > 0f;
        }
        public IEnumerator FadeTo(float alpha, float seconds)
        {
            float start = Alpha, elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(start, alpha, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / seconds))));
                yield return null;
            }
            SetAlpha(alpha);
        }
        public bool LoadSceneWithFade(string destination, float fadeOut, float fadeIn, Action failed)
        {
            if (loading || !Application.CanStreamedLevelBeLoaded(destination)) return false;
            loading = true; StartCoroutine(Load(destination, fadeOut, fadeIn, failed)); return true;
        }
        private IEnumerator Load(string destination, float fadeOut, float fadeIn, Action failed)
        {
            yield return FadeTo(1f, fadeOut);
            SceneManager.sceneLoaded += OnSceneLoaded;
            AsyncOperation operation = null;
            try { operation = SceneManager.LoadSceneAsync(destination, LoadSceneMode.Single); }
            catch (Exception exception) { Debug.LogException(exception, this); }
            if (operation == null)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                yield return FadeTo(0f, fadeIn); loading = false; failed?.Invoke(); yield break;
            }
            yield return operation;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            yield return null;
            yield return FadeTo(0f, fadeIn);
            if (arrivalInput != null) arrivalInput.Exit(this);
            arrivalInput = null; loading = false;
        }
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            var player = FindAnyObjectByType<PlayerInput>();
            arrivalInput = GameInputModeController.GetOrCreate(player, player != null ? player.GetComponent<StarterAssetsInputs>() : null);
            arrivalInput.TryEnter(this, GameInputMode.Cinematic);
        }
        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (arrivalInput != null) arrivalInput.Exit(this);
            if (Current == this) Current = null;
        }
    }
}
