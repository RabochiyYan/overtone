using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;
namespace Overtone.Editor
{
    public static class PrototypeBuilder
    {
        [MenuItem("Overtone/Create prototype scene")]
        public static void Create()
        {
            // A new scene is additive until saved: the user's sample scene stays intact.
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener));
            var cam=camera.GetComponent<Camera>(); cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Palette.Background;camera.tag="MainCamera";
            camera.transform.position=new Vector3(0,0,-10); cam.orthographic=true;
            var canvasGo=new GameObject("MinistryCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            var canvas=canvasGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvasGo.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1600,900);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            var stageGo=new GameObject("Stage",typeof(RectTransform));var stage=(RectTransform)stageGo.transform;stage.SetParent(canvasGo.transform,false);stage.sizeDelta=new Vector2(1600,900);
            var system=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
            system.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            var manager=new GameObject("GameSession");var session=manager.AddComponent<GameSession>();var view=manager.AddComponent<GameView>();
            var settings=AssetDatabase.LoadAssetAtPath<PrototypeSettings>("Assets/Overtone/PrototypeSettings.asset");
            if(settings==null) {settings=ScriptableObject.CreateInstance<PrototypeSettings>();AssetDatabase.CreateAsset(settings,"Assets/Overtone/PrototypeSettings.asset");}
            var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/Overtone/Art/Arial.ttf");
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Overtone/Art/MinistryFont.asset");
            if(font==null) {
                font=TMP_FontAsset.CreateFontAsset(source,32,6,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);
                font.name="MinistryFont";AssetDatabase.CreateAsset(font,"Assets/Overtone/Art/MinistryFont.asset");
                AssetDatabase.AddObjectToAsset(font.material,font);
                foreach(var atlas in font.atlasTextures) if(atlas!=null) AssetDatabase.AddObjectToAsset(atlas,font);
                string chars=""; for(int c=32;c<127;c++) chars+=(char)c; for(int c=1024;c<1280;c++) chars+=(char)c; chars+="…—–→←●○•№×ёЁ";
                font.TryAddCharacters(chars,out string missing); EditorUtility.SetDirty(font);
            }
            session.settings=settings;session.view=view;view.session=session;view.stage=stage;view.font=font;
            EditorSceneManager.SaveScene(scene,"Assets/Scenes/OvertonePrototype.unity");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/OvertonePrototype.unity",true)};
            PlayerSettings.productName="Окно Овертона";PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;
            AssetDatabase.SaveAssets();
            Selection.activeGameObject=manager;
            EditorSceneManager.OpenScene("Assets/Scenes/OvertonePrototype.unity",OpenSceneMode.Single);
        }
    }
}
