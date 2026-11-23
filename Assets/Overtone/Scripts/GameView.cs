using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace Overtone
{
    public sealed partial class GameView : MonoBehaviour
    {
        [SerializeField] public TMP_FontAsset font;
        [SerializeField] public RectTransform stage;
        [SerializeField] public GameSession session;
        [SerializeField] RectTransform content;
        RectTransform modal, card, networkSettingsModal;
        PhaseTwoTuning tuningDraft;
        float shownViews;
        sealed class TransmissionVisual { public RectTransform dot,halo; public Vector2 from,to; }
        readonly List<TransmissionVisual> transmissions = new List<TransmissionVisual>();
        TextMeshProUGUI timerLabel;
        RectTransform timerFill;
        TextMeshProUGUI channelDetails;
        int inspectedChannel=-1;
        Vector2 networkScrollPosition;
        float networkZoom;
        ScrollRect networkScroll;
        AudioSource audioSource;
        AudioClip tone, errorTone;
        bool muted;
        public void Start()
        {
            audioSource=gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake=false;
            float[] samples=new float[4410];
            for(int i=0;i<samples.Length;i++) samples[i]=Mathf.Sin(i*2*Mathf.PI*440/44100f)*0.13f*Mathf.Pow(1-i/(float)samples.Length,2);
            tone=AudioClip.Create("Paper terminal feedback",samples.Length,1,44100,false); tone.SetData(samples,0);
            var errorSamples=new float[22932];
            for(int i=0;i<errorSamples.Length;i++) {
                float t=i/44100f, local=t<.22f ? t : t-.26f;
                if(local<0||local>.20f)continue;
                float envelope=Mathf.Min(1,local/.012f)*Mathf.Clamp01((.20f-local)/.04f);
                float phase=2*Mathf.PI*(170*local-65*local*local);
                errorSamples[i]=(.11f*Mathf.Sin(phase)+.045f*Mathf.Sign(Mathf.Sin(phase*2)))*envelope;
            }
            errorTone=AudioClip.Create("Wrong answer double buzzer",errorSamples.Length,1,44100,false); errorTone.SetData(errorSamples,0);
            muted=PlayerPrefs.GetInt("Overtone.Muted",0)==1;
            session.Initialize();
        }
        void OnEnable() { if(session!=null) session.Changed+=Render; }
        void OnDisable() { if(session!=null) session.Changed-=Render; }
        void OnDestroy() { if(tone!=null) Destroy(tone); if(errorTone!=null) Destroy(errorTone); }
        void Update()
        {
            if(session.CheckingBatch.Count>0) foreach(var visual in transmissions) {
                if(visual.dot==null)continue;
                var point=Vector2.Lerp(visual.from,visual.to,session.CheckProgress);
                visual.dot.anchoredPosition=new Vector2(point.x,-point.y);
                if(visual.halo!=null) visual.halo.localScale=Vector3.one*(1+.10f*Mathf.Sin(session.CheckProgress*Mathf.PI));
            }
            if(timerLabel!=null && session.Phase==GamePhase.Sorting) {
                int seconds=Mathf.CeilToInt(session.SortingTimeLeft);
                timerLabel.text=$"{seconds/60:00}:{seconds%60:00}";
                timerLabel.color=session.SortingTimeLeft<=8 ? Palette.Bad : Palette.Accent;
                if(timerFill!=null)timerFill.sizeDelta=new Vector2(240*Mathf.Clamp01(session.SortingTimeLeft/Mathf.Max(1,session.settings.sortingSeconds)),4);
            }
            var k=Keyboard.current; if(k==null) return;
            if(k.escapeKey.wasPressedThisFrame) { if(session.UpgradeWindowOpen) CloseUpgrades(); else if(networkSettingsModal!=null) CloseNetworkSettings(); else session.TogglePause(); }
            if(session.Phase==GamePhase.Sorting && !session.Paused) {
                if(k.leftArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame) session.Sort(false);
                if(k.rightArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame) session.Sort(true);
            }
        }
        public void PlayTone(float frequency)
        {
            if(muted || audioSource==null || tone==null) return;
            audioSource.pitch=frequency/440; audioSource.PlayOneShot(tone,0.5f);
        }
        public void PlaySortingErrorSound()
        {
            if(muted||audioSource==null||errorTone==null)return;
            audioSource.pitch=1;audioSource.PlayOneShot(errorTone,.85f);
        }
        public IEnumerator AnimateSortingError()
        {
            var rt=card;if(rt==null)yield break;Vector2 origin=rt.anchoredPosition;
            var flash=Box("WrongFlash",content,0,0,1600,900,Palette.Bad).GetComponent<UnityEngine.UI.Image>();
            var banner=Box("WrongAnswer",content,440,718,700,88,Palette.Stamp);
            Text("WrongAnswerTitle",banner,"ОШИБКА  /  -1 ДОПУСК",20,8,660,34,28,Palette.Paper,true);
            Text("WrongAnswerDetail",banner,session.Feedback,20,48,660,34,19,Palette.Paper);
            for(float t=0;t<.68f;) {
                if(rt==null||flash==null)yield break;
                if(!session.Paused)t+=Time.unscaledDeltaTime;
                float fade=1-Mathf.Clamp01(t/.68f);
                rt.anchoredPosition=origin+Vector2.right*Mathf.Sin(t*72)*15*fade;
                var color=Palette.Bad;color.a=.19f*fade*(.35f+.65f*Mathf.Abs(Mathf.Sin(t*14)));flash.color=color;
                yield return null;
            }
            if(rt!=null)rt.anchoredPosition=origin;
            if(flash!=null)Destroy(flash.gameObject);
        }
        RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
        {
            var go=new GameObject(name,typeof(RectTransform)); var rt=(RectTransform)go.transform;
            rt.SetParent(parent,false); rt.anchorMin=rt.anchorMax=new Vector2(0,1); rt.pivot=new Vector2(0,1);
            rt.anchoredPosition=new Vector2(x,-y); rt.sizeDelta=new Vector2(w,h); return rt;
        }
        RectTransform Box(string name,Transform parent,float x,float y,float w,float h,Color color,bool hit=false)
        {
            var rt=Rect(name,parent,x,y,w,h); var im=rt.gameObject.AddComponent<UnityEngine.UI.Image>(); im.color=color; im.raycastTarget=hit; return rt;
        }
        TextMeshProUGUI Text(string name,Transform parent,string value,float x,float y,float w,float h,float size,Color color,bool bold=false)
        {
            var rt=Rect(name,parent,x,y,w,h); var t=rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font=font; t.text=value; t.fontSize=size; t.color=color; t.raycastTarget=false;
            t.fontStyle=bold ? FontStyles.Bold : FontStyles.Normal; t.textWrappingMode=TextWrappingModes.Normal;
            t.overflowMode=TextOverflowModes.Truncate; return t;
        }
        UnityEngine.UI.Button Button(string name,Transform parent,string label,float x,float y,float w,float h,Action action,bool active=true,bool selected=false)
        {
            var rt=Box(name,parent,x,y,w,h,selected ? Palette.Accent : Palette.Raised,true);
            var b=rt.gameObject.AddComponent<UnityEngine.UI.Button>(); b.interactable=active;
            var c=b.colors; c.normalColor=Color.white; c.highlightedColor=new Color(1.12f,1.12f,1.12f); c.pressedColor=new Color(.78f,.78f,.78f);
            c.selectedColor=c.highlightedColor; c.disabledColor=selected ? new Color(.8f,.8f,.8f,1f) : new Color(.6f,.6f,.6f,1f); c.fadeDuration=.12f; b.colors=c;
            b.onClick.AddListener(()=> {PlayTone(400); action();});
            var t=Text("Label",rt,label,16,8,w-32,h-16,20,selected ? Palette.Ink : Palette.Paper,true);
            t.alignment=TextAlignmentOptions.MidlineLeft;
            return b;
        }
        void Clear(ref RectTransform root)
        {
            if(root!=null) { root.gameObject.SetActive(false); if(Application.isPlaying) Destroy(root.gameObject); else DestroyImmediate(root.gameObject); } root=null;
        }
        public void Render()
        {
            CloseNetworkSettings();
            if(networkScroll!=null) networkScrollPosition=networkScroll.content.anchoredPosition;
            networkScroll=null;
            Clear(ref content); Clear(ref modal); card=null; transmissions.Clear(); channelDetails=null; timerLabel=null; timerFill=null;
            content=Rect("Screen",stage,0,0,1600,900);
            Box("Backdrop",content,0,0,1600,900,Palette.Background);
            //Text("Ministry",content,"МИНИСТЕРСТВО М  /  СТРАНА N",96,22,800,36,18,Palette.Muted,true);
            Button("Sound",content,muted ? "ЗВУК: ВЫКЛ" : "ЗВУК: ВКЛ",1390,20,170,42,()=>{muted=!muted; PlayerPrefs.SetInt("Overtone.Muted",muted?1:0); PlayerPrefs.Save(); Render();});
            //Text("Footer",content,"ВНУТРЕННЯЯ СИСТЕМА  •  УЧЕБНЫЙ ПРОТОТИП",40,856,900,30,16,Palette.Muted);
            //Text("Build",content,"ОВЕРТОН  /  01",1330,856,230,30,16,Palette.Muted);
            switch(session.Phase) {
                case GamePhase.Menu: Menu(); break;
                case GamePhase.Sorting: Sorting(); break;
                case GamePhase.Briefing: Briefing(); break;
                case GamePhase.Editing: case GamePhase.Spreading: case GamePhase.RoundResult: Network(); break;
                case GamePhase.Won: case GamePhase.Fired: End(); break;
            }
            RenderPause();
            if(session.UpgradeWindowOpen) DrawUpgrades();
        }
        void Menu()
        {
            Text("Index",content,"ЛИЧНОЕ ДЕЛО № 012",70,134,700,40,20,Palette.Accent,true);
            Text("Title",content,"ОКНО\nОВЕРТОНА",64,191,900,245,96,Palette.Paper,true);
            //Text("Intro",content,"Ваш первый день в Министерстве М.\nПроверяйте новости. Собирайте повестку.\nДобейтесь нужного охвата.",70,467,730,140,27,Palette.Muted);
            Button("Start",content,"НАЧАТЬ РАБОЧИЙ ДЕНЬ  →",70,658,550,66,session.StartRun,true,true);
            if(session.HasSave) Button("Continue",content,"Продолжить рабочий день",70,740,550,58,session.ResumeRun);
            var paper=Box("Dossier",content,1000,162,490,586,Palette.Paper);
            paper.localEulerAngles=new Vector3(0,0,-3);
            Text("DocumentMark",paper,"МИНИСТЕРСТВО М",36,36,420,38,22,Palette.Ink,true);
            Box("Rule",paper,36,92,418,2,Palette.Ink);
            Text("DocumentTitle",paper,"СТАЖЁР\nОТДЕЛА ИНФОРМАЦИИ",36,130,420,110,34,Palette.Ink,true);
            Text("Assignment",paper,"01 / Проверить входящие\n12 достоверных новостей\n\n02 / Распространить повестку\n10 редакционных заданий",36,280,420,180,24,Palette.Ink);
            Text("Stamp",paper,"ДОПУЩЕН К РАБОТЕ",36,505,420,44,25,Palette.Stamp,true);
        }
        void Header(string number,string title,string subtitle)
        {
            Text("Phase",content,number,40,22,1060,32,18,Palette.Accent,true);
            Text("Heading",content,title,40,76,1100,64,43,Palette.Paper,true);
            Text("Subtitle",content,subtitle,40,146,1100,44,21,Palette.Muted);
            Text("Lives",content,"ДОПУСКИ",1200,78,200,40,25,Palette.Bad,true);
            for(int i=0;i<session.settings.startingLives;i++) {
                var heart=Rect("Life"+i,content,1400+i*31,83,24,24).gameObject.AddComponent<LifeIcon>();
                heart.color=i<session.Lives ? Palette.Bad : Palette.Raised; heart.raycastTarget=false;
            }
            Button("Pause",content,"ПАУЗА / ESC",1160,20,210,42,session.TogglePause);
        }
        void Sorting()
        {
            Header("ФАЗА 01  /  ВХОДЯЩИЕ","Соберите папку для начальства",$"Старт: {session.settings.sortingSeconds:0} сек. Верный ответ: +10 сек. Новости — вправо, фейки — влево.");
            var clock=Box("SortingClock",content,1320,132,240,48,Palette.Panel);
            Text("ClockLabel",clock,"ВРЕМЯ",12,11,94,30,18,Palette.Muted,true);
            int seconds=Mathf.CeilToInt(session.SortingTimeLeft);
            timerLabel=Text("SortingTimer",clock,$"{seconds/60:00}:{seconds%60:00}",110,5,118,38,29,Palette.Accent,true);
            timerLabel.alignment=TextAlignmentOptions.Center;
            timerFill=Box("SortingTimerFill",clock,0,44,240*Mathf.Clamp01(session.SortingTimeLeft/Mathf.Max(1,session.settings.sortingSeconds)),4,Palette.Accent);
            Text("Progress",content,$"ПАПКА  {session.Folder:00} / {session.settings.folderTarget}",1180,188,380,46,29,Palette.Accent,true);
            Box("Bin",content,40,264,360,450,Palette.Panel);
            Text("BinIcon",content,"×",92,304,250,150,110,Palette.Bad,true);
            Text("BinTitle",content,"КОРЗИНА",92,478,270,50,31,Palette.Paper,true);
            Text("BinHint",content,"Для выдуманных новостей",92,540,270,62,21,Palette.Muted);
            Button("Discard",content,"←  ОТКЛОНИТЬ / A",40,738,360,65,()=>session.Sort(false),!session.Busy);
            Box("FolderTab",content,1200,242,170,24,Palette.Accent);
            Box("Folder",content,1200,264,360,450,Palette.Panel);
            Text("FolderIcon",content,"+",1232,304,250,150,110,Palette.Accent,true);
            Text("FolderTitle",content,"В ПАПКУ",1232,478,270,50,31,Palette.Paper,true);
            Text("FolderHint",content,"Для достоверных новостей",1232,540,270,62,21,Palette.Muted);
            Button("Keep",content,"В ПАПКУ / D  →",1200,738,360,65,()=>session.Sort(true),!session.Busy);
            Box("CardShadow",content,452,222,700,504,Palette.Ink);
            card=Box("NewsCard",content,440,210,700,504,Palette.Paper,true);
            Text("CardIndex",card,$"ВХОДЯЩЕЕ  /  {session.CardIndex+1:000}",32,24,636,36,18,Palette.Ink,true);
            Text("Placeholder",card,session.CurrentCard.placeholder ? "ВРЕМЕННАЯ НОВОСТЬ" : "НА ПРОВЕРКУ",32,70,636,36,18,Palette.Stamp,true);
            Box("CardRule",card,32,124,636,2,Palette.Ink);
            Text("Headline",card,session.CurrentCard.headline,32,150,636,84,34,Palette.Ink,true);
            Text("Body",card,session.CurrentCard.body,32,246,636,210,24,Palette.Ink);
            card.gameObject.AddComponent<NewsCardDrag>().Initialize(session,this);
            Text("Feedback",content,session.Feedback,440,735,700,74,21,session.Busy ? session.LastCorrect ? Palette.Good : Palette.Bad : Palette.Muted);
            Button("SkipPhaseOne",content,"ПРОПУСТИТЬ ФАЗУ 01 →",500,816,600,48,session.SkipPhaseOne,!session.Busy);
            if(!session.Busy) StartCoroutine(CardArrive(card));
        }
        IEnumerator CardArrive(RectTransform rt)
        {
            var target=rt.anchoredPosition;
            for(float t=0;t<.25f;t+=Time.unscaledDeltaTime) { if(rt==null) yield break; rt.anchoredPosition=target+Vector2.up*120*Mathf.Pow(1-t/.25f,3); yield return null; }
            if(rt!=null) rt.anchoredPosition=target;
        }
        public IEnumerator AnimateCardOut(bool right)
        {
            var rt=card; if(rt==null) yield break; Vector2 from=rt.anchoredPosition;
            for(float t=0;t<.32f;t+=Time.unscaledDeltaTime) {
                if(rt==null) yield break;
                float a=t/.32f; rt.anchoredPosition=from+new Vector2((right?1:-1)*900*a,-90*a);
                rt.localEulerAngles=new Vector3(0,0,(right?-1:1)*18*a); yield return null;
            }
        }
        void Briefing()
        {
            Header("ПЕРЕВОД В НОВЫЙ ОТДЕЛ","Папка принята","Начальство доверяет вам следующее задание.");
            var panel=Box("Briefing",content,180,290,1240,414,Palette.Paper);
            Text("Promotion",panel,"ОТ ПРОВЕРКИ — К РАСПРОСТРАНЕНИЮ",40,32,1160,58,35,Palette.Ink,true);
            Text("Rules",panel,"В каждом из 10 раундов выберите продолжение новости и стартовый канал.\nНовость распространяется по связям сети. Тематика и лагерь каналов открыты,\nпараметры новости скрыты до результата. Достигните плана просмотров.\n\nВы получаете 3 допуска. Провал раунда отнимает один.",40,116,1160,220,26,Palette.Ink);
            Button("BeginPhaseTwo",content,"ПЕРЕЙТИ В ОТДЕЛ РАСПРОСТРАНЕНИЯ  →",350,742,900,66,session.BeginPhaseTwo,true,true);
        }
        // Layout belongs to the network data; rendering and transmission use the same coordinates.
        static Vector2 MapPosition(Vector2 point) { return point; }
        Vector2 Position(int index) { var n=session.Network[index]; return MapPosition(new Vector2(n.x,n.y)); }
        float Radius(int index) { return MediaNetwork.Radius(session.Network[index].audience); }
        RectTransform Circle(string name,Transform parent,Vector2 center,float radius,Color color,float ring=0,bool hit=false)
        {
            var rt=Rect(name,parent,center.x,center.y,radius*2,radius*2);
            rt.pivot=new Vector2(.5f,.5f); rt.anchoredPosition=new Vector2(center.x,-center.y);
            var graphic=rt.gameObject.AddComponent<CircleGraphic>();
            graphic.ringWidth=ring; graphic.color=color; graphic.raycastTarget=hit; graphic.SetVerticesDirty();
            return rt;
        }
        public void ResetNetworkView()
        {
            networkZoom=0;networkScrollPosition=Vector2.zero;networkScroll=null;inspectedChannel=-1;
        }
        public void OpenNetworkSettings()
        {
            if(session.Phase!=GamePhase.Editing || session.Paused)return;
            tuningDraft=session.Tuning.Copy();DrawNetworkSettings();
        }
        void CloseNetworkSettings() { Clear(ref networkSettingsModal);tuningDraft=null; }
        void DrawNetworkSettings()
        {
            Clear(ref networkSettingsModal);
            networkSettingsModal=Box("NetworkSettingsOverlay",stage,0,0,1600,900,new Color(.025f,.045f,.05f,.94f),true);
            var panel=Box("NetworkSettingsPanel",networkSettingsModal,290,55,1020,790,Palette.Panel,true);
            Text("Title",panel,"НАСТРОЙКИ ВТОРОЙ ФАЗЫ",32,22,956,44,30,Palette.Paper,true);
            Text("Description",panel,"План — 20–70% аудитории сети. Повышается только после успешного задания.",32,76,956,30,18,Palette.Muted);
            TuningSlider(panel,"Difficulty",0,"Сложность",.1f,3,tuningDraft.targetMultiplier,false,
                v=>tuningDraft.targetMultiplier=v,()=>Count(session.TargetForMultiplier(tuningDraft.targetMultiplier))+" просмотров",
                "Меньше просмотров","Больше просмотров");
            TuningSlider(panel,"ChannelCount",1,"Количество каналов",1,Mathf.Max(300,session.DefaultChannelCount),tuningDraft.Count(session.DefaultChannelCount),true,
                v=>tuningDraft.channelCount=Mathf.RoundToInt(v),()=>tuningDraft.Count(session.DefaultChannelCount)+" каналов",
                "Меньше каналов","Больше каналов");
            TuningSlider(panel,"Connectivity",2,"Связность каналов",2,12,tuningDraft.meanLinks,false,
                v=>tuningDraft.meanLinks=v,()=>"≈ "+tuningDraft.meanLinks.ToString("0.0")+" связей / канал",
                "Редкая сеть","Плотная сеть");
            TuningSlider(panel,"TopicMix",3,"Перемешанность тематик",0,1,tuningDraft.topicMix,false,
                v=>tuningDraft.topicMix=v,()=>Mathf.RoundToInt(tuningDraft.topicMix*100)+" %",
                "Почти замкнутые темы","Полный хаос");
            TuningSlider(panel,"CampGrouping",4,"Сгруппированность по лагерю",0,1,tuningDraft.campGrouping,false,
                v=>tuningDraft.campGrouping=v,()=>Mathf.RoundToInt(tuningDraft.campGrouping*100)+" %",
                "Лагерь не влияет на связи","Связи преимущественно внутри лагеря");
            TuningSlider(panel,"AudienceSpread",5,"Разброс подписчиков",0,2,tuningDraft.audienceSpread,false,
                v=>tuningDraft.audienceSpread=v,()=>Mathf.Approximately(tuningDraft.audienceSpread,1) ? "Как в списке каналов" : tuningDraft.audienceSpread.ToString("0.00")+" ×",
                "Одинаковые аудитории","Небольшие каналы и крупные хабы");
            Text("ApplyHint",panel,"Разброс меняет аудитории каналов, сохраняя их общую сумму.\nПри увеличении числа каналов используются варианты из вашего списка.",32,650,956,46,15,Palette.Muted);
            Button("ResetTuning",panel,"ПО УМОЛЧАНИЮ",32,714,300,50,()=>{tuningDraft=new PhaseTwoTuning();DrawNetworkSettings();});
            Button("CancelTuning",panel,"ОТМЕНА",348,714,240,50,CloseNetworkSettings);
            Button("ApplyTuning",panel,"ПРИМЕНИТЬ",604,714,384,50,()=>{
                var next=tuningDraft.Copy();CloseNetworkSettings();session.ApplyTuning(next);
            },true,true);
        }
        void TuningSlider(Transform panel,string name,int row,string title,float min,float max,float value,bool whole,Action<float> change,Func<string> format,string low,string high)
        {
            float top=122+row*86;
            Text(name+"Title",panel,title,32,top,610,26,21,Palette.Paper,true);
            var readout=Text(name+"Value",panel,format(),646,top,342,26,19,Palette.Accent,true);
            readout.alignment=TextAlignmentOptions.Right;
            var root=Box(name+"Slider",panel,32,top+29,956,30,new Color(0,0,0,0),true);
            var track=Box("Track",root,0,12,956,6,Palette.Background);
            var fill=Box("Fill",track,0,0,0,0,Palette.Accent);
            fill.anchorMin=Vector2.zero;fill.anchorMax=Vector2.one;fill.offsetMin=fill.offsetMax=Vector2.zero;
            var handleArea=Rect("HandleArea",root,9,0,938,30);
            var handle=Box("Handle",handleArea,0,0,18,0,Palette.Paper,true);
            handle.pivot=new Vector2(.5f,.5f);handle.anchorMin=handle.anchorMax=new Vector2(0,.5f);handle.anchoredPosition=Vector2.zero;
            var slider=root.gameObject.AddComponent<Slider>();
            slider.fillRect=fill;slider.handleRect=handle;slider.targetGraphic=handle.GetComponent<UnityEngine.UI.Image>();
            slider.direction=Slider.Direction.LeftToRight;slider.minValue=min;slider.maxValue=max;slider.wholeNumbers=whole;
            slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(v=>{change(v);readout.text=format();});
            Text(name+"Low",panel,low,32,top+59,466,20,12,Palette.Muted);
            var right=Text(name+"High",panel,high,510,top+59,478,20,12,Palette.Muted);right.alignment=TextAlignmentOptions.Right;
        }
        void ZoomNetwork(float zoom)
        {
            networkZoom=zoom;networkScrollPosition=Vector2.zero;networkScroll=null;Render();
        }
        void CampMark(Transform parent,Vector2 center,int camp)
        {
            var plate=Circle("CampMark"+camp,parent,center,7.5f,new Color(.055f,.09f,.105f));
            var c=new Vector2(7.5f,7.5f);
            if(camp==2) {Circle("Neutral",plate,c,3.2f,Palette.Muted,1.2f);return;}
            float sign=camp==0 ? 1 : -1;
            var a=c+new Vector2(0,-3.5f*sign);
            var b=c+new Vector2(-3.5f,2.5f*sign);
            var d=c+new Vector2(3.5f,2.5f*sign);
            Line(plate,a,b,Palette.Muted,1.3f);Line(plate,b,d,Palette.Muted,1.3f);Line(plate,d,a,Palette.Muted,1.3f);
        }
        public void InspectChannel(int index)
        {
            inspectedChannel=index;
            if(channelDetails==null || index<0 || index>=session.Network.Length) return;
            var n=session.Network[index];
            channelDetails.text="№"+(index+1).ToString("00")+" / "+n.name+" / "+n.clusterName+" / "+(n.allTopics ? "все темы" : string.Join(" ",n.topics ?? new[]{n.topic}))+" / "+Count(n.audience)+" / "+Palette.CampName(n.camp);
        }
        void Network()
        {
            Header($"ФАЗА 02  /  ЗАДАНИЕ {session.Round+1:00} ИЗ 10","Управляйте распространением",session.ChoosingSecondStart ? "ВТОРОЙ СТАРТ: выберите неохваченный канал. Просмотры и пройденные каналы сохраняются." : "Выберите продолжение, затем нажмите доступный канал с меткой «СТАРТ».");
            var left=Box("EditorPanel",content,40,190,500,686,Palette.Panel);
            Text("Opening",left,session.CurrentRound.opening,24,16,452,82,25,Palette.Paper,true);
            for(int i=0;i<3;i++) {
                int index=i; bool selected=session.SelectedOption==i;
                var b=Button("Option"+i,left,"",24,108+i*130,452,114,()=>session.ChooseOption(index),session.Phase==GamePhase.Editing && !session.ChoosingSecondStart,selected);
                Text("Number",b.transform,(i+1).ToString("00"),14,14,42,60,23,selected ? Palette.Ink : Palette.Accent,true);
                var optionText=Text("OptionText",b.transform,session.CurrentRound.options[i].text,58,10,378,94,20,selected ? Palette.Ink : Palette.Paper);
                optionText.enableAutoSizing=true; optionText.fontSizeMin=18; optionText.fontSizeMax=20;
            }
            if(session.SelectedChannel>=0) Text("SelectedSeed",left,"Старт: "+session.Network[session.SelectedChannel].name,24,552,452,24,17,Palette.Accent);
            bool ready=session.SelectedChannel>=0 && session.SelectedOption>=0 && session.Phase==GamePhase.Editing;
            Button("Publish",left,session.Phase==GamePhase.Spreading ? "РАСПРОСТРАНЕНИЕ…" : session.ChoosingSecondStart ? "ЗАПУСТИТЬ ЕЩЁ КАНАЛ  →" : "ОПУБЛИКОВАТЬ  →",24,584,452,62,session.Publish,ready,true);
            Button("Upgrades",content,"УЛУЧШЕНИЯ  /  "+session.Upgrades.points+" Б.",850,20,290,42,OpenUpgrades);
            Button("NetworkSettings",left,"НАСТРОЙКИ СЕТИ И СЛОЖНОСТИ",24,494,452,44,OpenNetworkSettings,session.Phase==GamePhase.Editing && !session.ChoosingSecondStart);
            Text("SettingsHint",left,"Параметры можно изменить до публикации.",24,654,452,20,13,Palette.Muted);
            var graph=Box("NetworkGraph",content,564,190,996,686,new Color(.055f,.09f,.105f));
            var viewport=Box("GraphViewport",graph,24,80,948,480,new Color(.055f,.09f,.105f),true);
            viewport.gameObject.AddComponent<RectMask2D>();
            float zoom=networkZoom>0 ? networkZoom : Mathf.Min(1,Mathf.Min(948/MediaNetwork.Width,480/MediaNetwork.Height));
            var canvas=Rect("GraphCanvas",viewport,0,0,Mathf.Max(948,MediaNetwork.Width*zoom),Mathf.Max(480,MediaNetwork.Height*zoom));
            var map=Rect("GraphContent",canvas,(canvas.sizeDelta.x-MediaNetwork.Width*zoom)/2,(canvas.sizeDelta.y-MediaNetwork.Height*zoom)/2,MediaNetwork.Width,MediaNetwork.Height);
            map.localScale=Vector3.one*zoom;
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();
            networkScroll=scroll;
            scroll.viewport=viewport;scroll.content=canvas;scroll.horizontal=true;scroll.vertical=true;
            scroll.movementType=ScrollRect.MovementType.Clamped;scroll.inertia=false;scroll.scrollSensitivity=36;
            networkScrollPosition=new Vector2(Mathf.Clamp(networkScrollPosition.x,948-canvas.sizeDelta.x,0),Mathf.Clamp(networkScrollPosition.y,0,canvas.sizeDelta.y-480));
            canvas.anchoredPosition=networkScrollPosition;
            scroll.onValueChanged.AddListener(_=>networkScrollPosition=canvas.anchoredPosition);
            if(session.Network.Length==0) Text("EmptyNetwork",map,"Добавьте каналы в PhaseTwoChannels.json",24,24,900,48,22,Palette.Muted);
            var drawn=new System.Collections.Generic.HashSet<int>();
            for(int i=0;i<session.Network.Length;i++) foreach(int j in session.Network[i].neighbors) {
                int key=GameSession.ConnectionKey(i,j); if(!drawn.Add(key)) continue;
                bool active=session.CheckingBatch.Any(a=>GameSession.ConnectionKey(a.source,a.target)==key);
                Color color=Palette.Link; color.a=.36f; float width=1.2f;
                if(session.CheckedConnections.TryGetValue(key,out bool passed)) {color=passed ? Palette.Good : Palette.Bad; color.a=passed ? 1 : .65f;}
                if(active) {color=Palette.Accent; width=4;}
                var direction=(Position(j)-Position(i)).normalized;
                Connection(map,Position(i)+direction*(Radius(i)+5),Position(j)-direction*(Radius(j)+5),color,width,false);
            }
            for(int i=0;i<session.Network.Length;i++) {
                int index=i; var n=session.Network[i]; var center=Position(i); float radius=Radius(i);
                bool selected=session.SelectedChannel==i, reached=session.Reached.Contains(i);
                if(selected) Circle("SelectedRing"+i,map,center,radius+6,Palette.Accent,3);
                var rt=Circle("Channel"+i,map,center,radius,reached ? Palette.Good : selected ? Palette.Accent : Palette.Background,0,true);
                var button=rt.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic=rt.GetComponent<CircleGraphic>();
                button.interactable=session.CanSelectChannel(i);
                var colors=button.colors; colors.normalColor=colors.selectedColor=colors.disabledColor=Color.white;
                colors.highlightedColor=new Color(1.18f,1.18f,1.18f); colors.pressedColor=new Color(.8f,.8f,.8f); colors.fadeDuration=.12f; button.colors=colors;
                button.onClick.AddListener(()=> { InspectChannel(index); if(n.canPublish) session.ChooseChannel(index); });
                var pointer=rt.gameObject.AddComponent<ChannelPointer>(); pointer.view=this; pointer.channel=i;
                Circle("TopicOutline",rt,new Vector2(radius,radius),radius,MediaNetwork.Communities[n.cluster].color,2);
                CampMark(map,new Vector2(center.x,center.y+radius+10),n.camp);
                Color fg=reached||selected ? Palette.Ink : Palette.Paper;
                var number=Text("NodeId",rt,(i+1).ToString("00"),0,radius-9,radius*2,18,13,fg,true); number.alignment=TextAlignmentOptions.Center;
                if(n.canPublish) {
                    var badge=Rect("StartBadge"+i,map,center.x-23,center.y-radius-16,46,13);
                    var label=Text("StartTag",badge,"СТАРТ",0,0,46,13,9,Palette.Accent,true); label.alignment=TextAlignmentOptions.Center;
                }
                if(session.NewWave.Contains(i)) StartCoroutine(PopNode(rt));
            }
            var legendViewport=Box("TopicLegendViewport",graph,24,10,948,40,new Color(0,0,0,0),true);
            legendViewport.gameObject.AddComponent<RectMask2D>();
            float legendWidth=Mathf.Max(948,Mathf.Ceil(MediaNetwork.Communities.Length/2f)*158);
            var legend=Rect("TopicLegend",legendViewport,0,0,legendWidth,40);
            var legendScroll=legendViewport.gameObject.AddComponent<ScrollRect>();
            legendScroll.viewport=legendViewport;legendScroll.content=legend;legendScroll.horizontal=true;legendScroll.vertical=false;
            legendScroll.movementType=ScrollRect.MovementType.Clamped;legendScroll.scrollSensitivity=30;
            for(int i=0;i<MediaNetwork.Communities.Length;i++) {
                var topic=MediaNetwork.Communities[i];float x=(i/2)*158,y=(i%2)*20;
                Circle("TopicKey",legend,new Vector2(x+5,y+9),4,topic.color,1.5f);
                var label=Text("TopicName",legend,topic.label,x+16,y,138,19,12,topic.color);
                label.enableAutoSizing=true;label.fontSizeMin=9;label.fontSizeMax=12;
            }
            for(int camp=0;camp<3;camp++) {
                CampMark(graph,new Vector2(32+camp*192,64),camp);
                Text("CampKey",graph,Palette.CampName(camp),44+camp*192,54,170,20,12,Palette.Muted);
            }
            Text("NavigationHint",graph,"ПЕРЕТАСКИВАНИЕ / ПРОКРУТКА",598,54,230,20,10,Palette.Muted);
            Button("ZoomOut",graph,"−",830,52,38,24,()=>ZoomNetwork(Mathf.Clamp(zoom/1.3f,.2f,3)));
            Button("ZoomIn",graph,"+",872,52,38,24,()=>ZoomNetwork(Mathf.Clamp(zoom*1.3f,.2f,3)));
            Button("FitGraph",graph,"ОБЗОР",914,52,58,24,()=>ZoomNetwork(0));
            foreach(string control in new[]{"ZoomOut","ZoomIn","FitGraph"}) {
                var label=graph.Find(control+"/Label").GetComponent<TextMeshProUGUI>();
                label.rectTransform.offsetMin=Vector2.zero;label.rectTransform.offsetMax=Vector2.zero;
                label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;
                label.rectTransform.offsetMin=label.rectTransform.offsetMax=Vector2.zero;
                label.fontSize=control=="FitGraph" ? 10 : 18;label.alignment=TextAlignmentOptions.Center;
            }
            var detailPanel=Box("ChannelInfo",graph,24,580,948,66,Palette.Background);
            string status=session.Network.Length+" каналов / "+MediaNetwork.Communities.Length+" тематик • цвет — тема / значок снизу — лагерь";
            Color statusColor=Palette.Muted;
            if(session.CheckingBatch.Count>0) {
                int source=session.Checking.source;
                status="ПРОВЕРКА: "+session.Network[source].name+" → "+session.CheckingBatch.Count+" каналов одновременно";
                statusColor=Palette.Accent;
                Circle("CheckingSource",map,Position(source),Radius(source)+6,Palette.Accent,1.5f);
                foreach(var attempt in session.CheckingBatch) {
                    var direction=(Position(attempt.target)-Position(source)).normalized;
                    var from=Position(source)+direction*(Radius(source)+6);
                    var to=Position(attempt.target)-direction*(Radius(attempt.target)+6);
                    var halo=Circle("CheckingTarget"+attempt.target,map,Position(attempt.target),Radius(attempt.target)+8,Palette.Accent,2);
                    var dot=Circle("TransmissionPacket"+attempt.target,map,Vector2.Lerp(from,to,session.CheckProgress),4.5f,Palette.Accent);
                    transmissions.Add(new TransmissionVisual {dot=dot,halo=halo,from=from,to=to});
                }
            } else if(session.LastBatch.Count>0) {
                int passed=session.LastBatch.Count(a=>a.passed);
                status="ПЕРЕДАНО: "+passed+" из "+session.LastBatch.Count+" / "+session.Network[session.LastAttempt.source].name;
                statusColor=passed>0 ? Palette.Good : Palette.Bad;
                foreach(var attempt in session.LastBatch) Circle("CheckResult"+attempt.target,map,Position(attempt.target),Radius(attempt.target)+6,attempt.passed ? Palette.Good : Palette.Bad,3);
            }
            Text("CheckStatus",detailPanel,status,12,6,924,23,16,statusColor,true);
            channelDetails=Text("ChannelDetails",detailPanel,"Наведите курсор на узел, чтобы увидеть тематику и аудиторию.",12,34,924,26,16,Palette.Paper);
            InspectChannel(session.SelectedChannel>=0 ? session.SelectedChannel : inspectedChannel);
            if(session.Views==0) shownViews=0;
            var counter=Text("Reach",content,ReachText(session.Views),1200,122,360,60,20,Palette.Accent,true);
            Box("ProgressTrack",graph,24,658,948,12,Palette.Background);
            var fill=Box("ProgressFill",graph,24,658,948*Mathf.Clamp01(shownViews/session.Target),12,Palette.Accent);
            StartCoroutine(AnimateViews(counter,fill));
            if(session.Phase==GamePhase.RoundResult && !session.CanSecondStart) ResultOverlay();
        }
        IEnumerator PopNode(RectTransform rt)
        {
            for(float t=0;t<.25f;) {
                if(rt==null) yield break;
                if(!session.Paused) t+=Time.unscaledDeltaTime;
                rt.localScale=Vector3.one*(1+.12f*Mathf.Sin(Mathf.Clamp01(t/.25f)*Mathf.PI)); yield return null;
            }
            if(rt!=null) rt.localScale=Vector3.one;
        }
        void Connection(Transform parent,Vector2 a,Vector2 b,Color color,float width,bool dashed)
        {
            if(!dashed) {Line(parent,a,b,color,width); return;}
            var direction=(b-a).normalized; float distance=Vector2.Distance(a,b);
            for(float d=0;d<distance;d+=18) Line(parent,a+direction*d,a+direction*Mathf.Min(d+10,distance),color,width);
        }
        static string Count(int value) { return value.ToString("N0",System.Globalization.CultureInfo.GetCultureInfo("ru-RU")); }
        string ReachText(int views) { return Count(views)+" / "+Count(session.Target)+"\nПРОСМОТРОВ"+(session.Phase==GamePhase.Spreading ? " • ВОЛНА "+session.WaveNumber : ""); }
        IEnumerator AnimateViews(TextMeshProUGUI counter,RectTransform fill)
        {
            float initial=shownViews; int target=session.Views;
            for(float t=0;t<.3f;t+=Time.unscaledDeltaTime) {
                if(counter==null || fill==null) yield break;
                shownViews=Mathf.Lerp(initial,target,1-Mathf.Pow(1-t/.3f,3));
                counter.text=ReachText(Mathf.RoundToInt(shownViews));
                fill.sizeDelta=new Vector2(948*Mathf.Clamp01(shownViews/session.Target),12); yield return null;
            }
            if(counter!=null) {shownViews=target; counter.text=ReachText(target);}
            if(fill!=null) fill.sizeDelta=new Vector2(948*Mathf.Clamp01(target/(float)session.Target),12);
        }
        void Line(Transform parent,Vector2 a,Vector2 b,Color color,float width)
        {
            var delta=b-a; var rt=Box("Connection",parent,a.x,a.y,delta.magnitude,width,color);
            rt.pivot=new Vector2(0,.5f); rt.localEulerAngles=new Vector3(0,0,-Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
        }
        void ResultOverlay()
        {
            var overlay=Box("ResultDim",content,0,70,1600,770,new Color(.04f,.09f,.09f,.88f),true);
            var panel=Box("RoundResult",overlay,250,160,1100,450,Palette.Paper);
            var option=session.CurrentRound.options[session.SelectedOption];
            Text("Stamp",panel,session.LastRoundWon ? "ПЛАН ВЫПОЛНЕН" : "ПРЕДУПРЕЖДЕНИЕ",36,26,1028,60,43,session.LastRoundWon ? Palette.Ink : Palette.Stamp,true);
            Text("ResultViews",panel,$"{Count(session.Views)} просмотров  /  план {Count(session.Target)}\nКаналов охвачено: {session.Reached.Count} из {session.Network.Length}",36,109,1028,90,29,Palette.Ink,true);
            Text("Reveal",panel,$"Улучшения: +{session.RoundPoints} б. / остаток {Count(session.Upgrades.surplusViews)} из 10 000\nПараметры продолжения:\n{option.kind}  •  {option.topic}  •  {Palette.CampName(option.camp)}\nВиральность: {option.virality:0.00}  /  Правдоподобность: {option.plausibility:0.00}",36,211,1028,120,21,Palette.Ink);
            Button("ResultUpgrades",panel,"УЛУЧШЕНИЯ / "+session.Upgrades.points+" Б.",700,156,360,44,OpenUpgrades,session.Lives>0);
            Button("NextRound",panel,session.Lives<=0 ? "ПОЛУЧИТЬ ПРИКАЗ ОБ УВОЛЬНЕНИИ" : session.Round==9 ? "ЗАВЕРШИТЬ РАБОЧИЙ ДЕНЬ" : "СЛЕДУЮЩЕЕ ЗАДАНИЕ  →",36,354,1028,62,session.NextRound,true,true);
        }
        void End()
        {
            bool won=session.Phase==GamePhase.Won;
            Text("Order",content,"ПРИКАЗ ПО МИНИСТЕРСТВУ М",100,150,1400,48,22,Palette.Accent,true);
            Text("EndTitle",content,won ? "ВЫ ПОВЫШЕНЫ" : "ВЫ УВОЛЕНЫ",100,244,1400,110,78,won ? Palette.Paper : Palette.Bad,true);
            Text("EndBody",content,won ? $"Все 10 заданий завершены. План выполнен в {session.SuccessfulRounds} раундах.\nВаше личное дело передано в штатный отдел." : (string.IsNullOrEmpty(session.FailureReason) ? "Допуски исчерпаны. Ваш пропуск аннулирован." : session.FailureReason)+"\nМинистерство благодарит вас за непродолжительную службу.",100,402,1300,130,32,Palette.Muted);
            Button("Restart",content,"НАЧАТЬ ЗАНОВО  →",100,630,630,70,session.StartRun,true,true);
            Button("Menu",content,"Вернуться в главное меню",100,722,630,60,session.ReturnToMenu);
        }
        public void RenderPause()
        {
            Clear(ref modal); if(!session.Paused) return;
            modal=Box("PauseOverlay",stage,0,0,1600,900,new Color(.04f,.09f,.09f,.94f),true);
            Text("PauseTitle",modal,"ПЕРЕРЫВ",450,200,700,90,68,Palette.Paper,true);
            Text("PauseInfo",modal,"Рабочий день приостановлен.\nПри выходе сохранится начало текущего задания.",450,330,700,110,27,Palette.Muted);
            Button("Resume",modal,"ПРОДОЛЖИТЬ",450,480,700,66,session.TogglePause,true,true);
            Button("Exit",modal,"В главное меню",450,570,700,66,session.ReturnToMenu);
        }
    }
    public sealed class NewsCardDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        GameSession session; RectTransform rt; Vector2 origin; float offset;
        public void Initialize(GameSession s,GameView v) {session=s;rt=(RectTransform)transform;}
        public void OnBeginDrag(PointerEventData e) { if(session.Busy||session.Paused) return; origin=rt.anchoredPosition;offset=0; }
        public void OnDrag(PointerEventData e) {if(session.Busy||session.Paused) return;offset+=e.delta.x/GetComponentInParent<Canvas>().scaleFactor;rt.anchoredPosition=origin+new Vector2(offset,0);rt.localEulerAngles=new Vector3(0,0,-offset*.025f);}
        public void OnEndDrag(PointerEventData e) {if(session.Busy||session.Paused) return;if(Mathf.Abs(offset)>110)session.Sort(offset>0);else{rt.anchoredPosition=origin;rt.localEulerAngles=Vector3.zero;}}
    }
}
