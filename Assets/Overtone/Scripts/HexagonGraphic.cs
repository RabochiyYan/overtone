using UnityEngine;
using UnityEngine.UI;

namespace Overtone
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HexagonGraphic : MaskableGraphic, ICanvasRaycastFilter
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); var r=rectTransform.rect;
            mesh.AddVert(r.center,color,Vector2.zero);
            for(int i=0;i<6;i++) {
                float angle=i*Mathf.PI/3;
                var shade=Color.Lerp(color,Color.white,i<3 ? .12f : 0);
                mesh.AddVert(r.center+new Vector2(Mathf.Cos(angle)*r.width/2,Mathf.Sin(angle)*r.height/2),shade,Vector2.zero);
            }
            for(int i=0;i<6;i++) mesh.AddTriangle(0,(i+1)%6+1,i+1);
        }
        public bool IsRaycastLocationValid(Vector2 screen,Camera camera)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,screen,camera,out var p);
            var r=rectTransform.rect; p-=r.center;
            float x=Mathf.Abs(p.x)/(r.width/2), y=Mathf.Abs(p.y)/(r.height/2);
            return y<=.866026f && x+y/1.732051f<=1;
        }
    }
}
