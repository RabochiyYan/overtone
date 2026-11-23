using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Overtone
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CircleGraphic : MaskableGraphic, ICanvasRaycastFilter
    {
        public float ringWidth;
        const int Segments=72;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var rect=rectTransform.rect;Vector2 center=rect.center;
            float radius=Mathf.Min(rect.width,rect.height)*.5f;
            if(ringWidth<=0) {
                mesh.AddVert(center,color,Vector2.zero);
                for(int i=0;i<=Segments;i++) {float a=i*Mathf.PI*2/Segments;mesh.AddVert(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,color,Vector2.zero);}
                for(int i=0;i<Segments;i++)mesh.AddTriangle(0,i+2,i+1);
            } else {
                float inner=Mathf.Max(0,radius-ringWidth);
                for(int i=0;i<=Segments;i++) {float a=i*Mathf.PI*2/Segments;var v=new Vector2(Mathf.Cos(a),Mathf.Sin(a));mesh.AddVert(center+v*radius,color,Vector2.zero);mesh.AddVert(center+v*inner,color,Vector2.zero);}
                for(int i=0;i<Segments;i++){int n=i*2;mesh.AddTriangle(n,n+1,n+3);mesh.AddTriangle(n,n+3,n+2);}
            }
        }
        public bool IsRaycastLocationValid(Vector2 screen,Camera camera)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,screen,camera,out var point);
            return (point-rectTransform.rect.center).sqrMagnitude<=Mathf.Pow(Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*.5f,2);
        }
    }
    public sealed class ChannelPointer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public GameView view;
        public int channel;
        public void OnPointerEnter(PointerEventData e) {view.InspectChannel(channel);transform.localScale=Vector3.one*1.04f;}
        public void OnPointerExit(PointerEventData e) {transform.localScale=Vector3.one;}
    }
}
