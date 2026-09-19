using UnityEngine;
namespace AstralUI
{
    public class AstralAmbient : MonoBehaviour
    {
        public float amplitude = 5, speed = .7f;
        Vector2 origin;
        RectTransform rect;
        void OnEnable() { rect = (RectTransform)transform; origin = rect.anchoredPosition; }
        void Update()
        {
            if (AstralApp.Instance != null && AstralApp.Instance.State.reducedMotion) return;
            rect.anchoredPosition = origin + new Vector2(0, Mathf.Sin(Time.unscaledTime * speed) * amplitude);
        }
    }
}
