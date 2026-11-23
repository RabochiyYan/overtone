using UnityEngine;

namespace Overtone
{
    public sealed partial class GameView
    {
        static readonly string[] MusicResources={"OvertoneMusic","UnknownTheme"};
        RectTransform musicModal;
        public void OpenMusicSettings()
        {
            session.MusicWindowOpen=true;
            DrawMusicSettings();
        }
        public void CloseMusicSettings()
        {
            session.MusicWindowOpen=false;
            Clear(ref musicModal);
        }
        public void SelectMusic(int index)
        {
            if(index<0 || index>=MusicResources.Length) return;
            var clip=Resources.Load<AudioClip>(MusicResources[index]);
            if(clip==null) return;
            if(musicSource==null) InitializeMusic();
            if(musicSource==null) return;
            if(musicSource.clip!=clip) {
                musicSource.Stop(); musicSource.clip=clip; musicSource.Play();
            }
            musicTrack=index;
            PlayerPrefs.SetInt("Overtone.MusicTrack",index); PlayerPrefs.Save();
            if(session.MusicWindowOpen) DrawMusicSettings();
        }
        void DrawMusicSettings()
        {
            Clear(ref musicModal);
            musicModal=Box("MusicOverlay",stage,0,0,1600,900,new Color(.025f,.035f,.04f,.97f),true);
            var panel=Box("MusicPanel",musicModal,340,205,920,490,Palette.Panel,true);
            Text("MusicTitle",panel,"ФОНОВАЯ МУЗЫКА",36,24,848,52,34,Palette.Paper,true);
            Text("MusicSubtitle",panel,"Выберите трек. Настройка сохранится для следующих запусков.",36,94,848,44,21,Palette.Muted);
            Button("OriginalTrack",panel,(musicTrack==0 ? "ВЫБРАНО  /  " : "")+"ОВЕРТОН",36,166,848,64,()=>SelectMusic(0),true,musicTrack==0);
            Button("UnknownTrack",panel,(musicTrack==1 ? "ВЫБРАНО  /  " : "")+"UNKNOWN THEME",36,246,848,64,()=>SelectMusic(1),true,musicTrack==1);
            Text("MusicPauseHint",panel,"Игра приостановлена на время выбора музыки.",36,344,848,40,20,Palette.Muted);
            Button("MusicMute",panel,muted ? "ВКЛЮЧИТЬ ЗВУК" : "ВЫКЛЮЧИТЬ ЗВУК",36,406,408,54,()=>{SetMuted(!muted);DrawMusicSettings();});
            Button("CloseMusic",panel,"ГОТОВО  /  ESC",464,406,420,54,CloseMusicSettings,true,true);
        }
    }
}
