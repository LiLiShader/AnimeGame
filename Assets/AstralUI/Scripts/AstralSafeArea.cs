using UnityEngine;
namespace AstralUI
{
    public class AstralSafeArea : MonoBehaviour
    {
        Rect last; int width, height;
        void LateUpdate()
        {
            if (Screen.width < 1 || Screen.height < 1) return;
            Rect safe = Screen.safeArea;
            if (last == safe && width == Screen.width && height == Screen.height) return;
            last = safe; width = Screen.width; height = Screen.height;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(safe.xMin/width,safe.yMin/height);
            rt.anchorMax = new Vector2(safe.xMax/width,safe.yMax/height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
