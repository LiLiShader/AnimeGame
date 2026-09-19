using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AstralUI
{
    [RequireComponent(typeof(Button))]
    public class AstralAction : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public string action;
        public int argument;
        Vector3 target = Vector3.one;
        bool hovered;
        Button button;
        void Awake() { button = GetComponent<Button>(); }
        void OnEnable() { button = GetComponent<Button>(); target=Vector3.one; }
        void OnDisable() { target = Vector3.one; transform.localScale = Vector3.one; hovered = false; }
        void Update() { transform.localScale = Vector3.Lerp(transform.localScale, target, 1 - Mathf.Exp(-22 * Time.unscaledDeltaTime)); }
        public void OnPointerDown(PointerEventData e) { if (button.interactable) target = Vector3.one * .955f; }
        public void OnPointerUp(PointerEventData e) { target = Vector3.one * (hovered ? 1.025f : 1); }
        public void OnPointerEnter(PointerEventData e) { hovered = true; if (button.interactable) target = Vector3.one * 1.025f; }
        public void OnPointerExit(PointerEventData e) { hovered = false; target = Vector3.one; }
    }
}
