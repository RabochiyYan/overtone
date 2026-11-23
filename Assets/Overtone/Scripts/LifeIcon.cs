using UnityEngine;
using UnityEngine.UI;
namespace Overtone
{
    // A small native UI mesh avoids depending on a font's symbol coverage.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LifeIcon : MaskableGraphic
    {
        static readonly Vector2[] outline={new Vector2(.5f,0),new Vector2(.08f,.43f),new Vector2(.01f,.64f),new Vector2(.07f,.82f),new Vector2(.21f,.95f),new Vector2(.36f,.96f),new Vector2(.5f,.82f),new Vector2(.64f,.96f),new Vector2(.79f,.95f),new Vector2(.93f,.82f),new Vector2(.99f,.64f),new Vector2(.92f,.43f)};
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;
            vh.AddVert(new Vector3(r.x+r.width*.5f,r.y+r.height*.5f),color,Vector2.zero);
            foreach(var p in outline) vh.AddVert(new Vector3(r.x+p.x*r.width,r.y+p.y*r.height),color,Vector2.zero);
            for(int i=0;i<outline.Length;i++) vh.AddTriangle(0,i+1,(i+1)%outline.Length+1);
        }
    }
}
