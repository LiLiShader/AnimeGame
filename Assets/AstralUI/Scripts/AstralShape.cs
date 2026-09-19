using UnityEngine;
using UnityEngine.UI;
namespace AstralUI
{
    // Resolution-independent angled glass surfaces behind the generated sliced frames.
    [RequireComponent(typeof(CanvasRenderer))]
    public class AstralShape : MaskableGraphic
    {
        public float slant = 90;
        public Color bottomColor = new Color(.84f,.88f,1,.94f);
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect;
            vh.AddVert(new Vector3(r.xMin + slant, r.yMin), bottomColor, Vector2.zero);
            vh.AddVert(new Vector3(r.xMin, r.yMax), color, Vector2.zero);
            vh.AddVert(new Vector3(r.xMax, r.yMax), color, Vector2.zero);
            vh.AddVert(new Vector3(r.xMax, r.yMin), bottomColor, Vector2.zero);
            vh.AddTriangle(0,1,2); vh.AddTriangle(2,3,0);
        }
    }
}
