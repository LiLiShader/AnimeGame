using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AstralUI
{
    /// <summary>
    /// Frame animation driven by an ordered sprite list (uGUI Image).
    /// 51 frames over 5 seconds (~10.2 fps), looping. Respects the reducedMotion setting.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class AstralFrameAnimation : MonoBehaviour
    {
        public Sprite[] frames;
        [Tooltip("Total duration of one full loop in seconds.")]
        public float duration = 5f;
        public bool loop = true;
        public bool playOnEnable = true;
        [Tooltip("Optional: keep the image aspect matching the current sprite (frame art may differ from the placeholder).")]
        public bool preserveAspect = true;

        Image image;
        Coroutine routine;
        int shown = -1;

        void Awake()
        {
            image = GetComponent<Image>();
            image.preserveAspect = preserveAspect;
        }

        void OnEnable()
        {
            if (playOnEnable) Play();
        }

        void Start()
        {
            // Fallback: if no Animator is actually driving the sprite, take over playback.
            var animator = GetComponent<Animator>();
            if (animator == null || !animator.enabled || animator.runtimeAnimatorController == null) Play();
        }

        void OnDisable()
        {
            if (routine != null) { StopCoroutine(routine); routine = null; }
        }

        public void Play()
        {
            if (routine != null) StopCoroutine(routine);
            if (frames == null || frames.Length == 0 || duration <= 0f) return;
            if (ReducedMotion) { ShowFrame(0); routine = null; return; }
            routine = StartCoroutine(Animate());
        }

        public void Stop()
        {
            if (routine != null) { StopCoroutine(routine); routine = null; }
        }

        IEnumerator Animate()
        {
            var wait = new WaitForEndOfFrame();
            while (true)
            {
                float t = 0f;
                while (t < duration)
                {
                    t += Time.unscaledDeltaTime;
                    ShowFrame(Mathf.Clamp((int)(t / duration * frames.Length), 0, frames.Length - 1));
                    yield return wait;
                }
                if (!loop) { routine = null; yield break; }
            }
        }

        void ShowFrame(int index)
        {
            if (index == shown || frames[index] == null) return;
            image.sprite = frames[index];
            shown = index;
        }

        /// <summary>True when the reduced-motion accessibility option is on.</summary>
        public bool ReducedMotion =>
            AstralApp.Instance != null && AstralApp.Instance.State != null && AstralApp.Instance.State.reducedMotion;
    }
}
