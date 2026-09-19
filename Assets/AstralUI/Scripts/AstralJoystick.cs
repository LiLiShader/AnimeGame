using UnityEngine;
using UnityEngine.EventSystems;
namespace AstralUI
{
    public class AstralJoystick:MonoBehaviour,IPointerDownHandler,IDragHandler,IPointerUpHandler
    {
        public Vector2 Value {get;private set;}
        int pointerId=int.MinValue;
        public void OnPointerDown(PointerEventData e){if(pointerId!=int.MinValue)return;pointerId=e.pointerId;OnDrag(e);}
        public void OnDrag(PointerEventData e){if(e.pointerId!=pointerId)return;var r=(RectTransform)transform;RectTransformUtility.ScreenPointToLocalPointInRectangle(r,e.position,e.pressEventCamera,out var p);Value=Vector2.ClampMagnitude((p-r.rect.center)/60,1);transform.GetChild(0).GetComponent<RectTransform>().anchoredPosition=new Vector2(59, -59)+Value*43;}
        public void OnPointerUp(PointerEventData e){if(e.pointerId==pointerId)Reset();}
        void OnDisable(){Reset();}
        void Reset(){pointerId=int.MinValue;Value=Vector2.zero;if(transform.childCount>0)transform.GetChild(0).GetComponent<RectTransform>().anchoredPosition=new Vector2(59,-59);}
    }
}
