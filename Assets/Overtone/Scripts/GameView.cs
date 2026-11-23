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
    public sealed class GameView : MonoBehaviour
    {
        [SerializeField] public TMP_FontAsset font;
        [SerializeField] public RectTransform stage;
        [SerializeField] public GameSession session;
        [SerializeField] RectTransform content;
        RectTransform modal, card;
        float shownViews;
        sealed class TransmissionVisual { public RectTransform dot,halo; public Vector2 from,to; }
        readonly List<TransmissionVisual> transmissions = new List<TransmissionVisual>();
        TextMeshProUGUI timerLabel;
        RectTransform timerFill;
        TextMeshProUGUI channelDetails;
        int inspectedChannel=-1;
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
            if(k.escapeKey.wasPressedThisFrame) session.TogglePause();
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
            Header("ФАЗА 01  /  ВХОДЯЩИЕ","Соберите папку для начальства",$"Соберите папку за {session.settings.sortingSeconds:0} секунд. Реальные новости — вправо, фейки — влево.");
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
        // Expand only the displayed map; channel data and spread rules stay in their original coordinates.
        static Vector2 MapPosition(Vector2 point) { return new Vector2(point.x,40+(point.y-40)*1.29f); }
        Vector2 Position(int index) { var n=session.Network[index]; return MapPosition(new Vector2(n.x,n.y)); }
        float Radius(int index) { return Mathf.Lerp(14,34,session.Network[index].audience/22000f); }
        RectTransform Circle(string name,Transform parent,Vector2 center,float radius,Color color,float ring=0,bool hit=false)
        {
            var rt=Rect(name,parent,center.x,center.y,radius*2,radius*2);
            rt.pivot=new Vector2(.5f,.5f); rt.anchoredPosition=new Vector2(center.x,-center.y);
            var graphic=rt.gameObject.AddComponent<CircleGraphic>();
            graphic.ringWidth=ring; graphic.color=color; graphic.raycastTarget=hit; graphic.SetVerticesDirty();
            return rt;
        }
        public void InspectChannel(int index)
        {
            inspectedChannel=index;
            if(channelDetails==null || index<0 || index>=session.Network.Length) return;
            var n=session.Network[index];
            channelDetails.text="№"+(index+1).ToString("00")+" / "+n.name+" / "+n.clusterName+" / "+n.topic+" / "+Count(n.audience)+" / "+Palette.CampName(n.camp);
        }
        void Network()
        {
            Header($"ФАЗА 02  /  ЗАДАНИЕ {session.Round+1:00} ИЗ 10","Управляйте распространением","Выберите продолжение, затем нажмите доступный канал с меткой «СТАРТ».");
            var left=Box("EditorPanel",content,40,190,500,686,Palette.Panel);
            Text("Opening",left,session.CurrentRound.opening,24,20,452,92,27,Palette.Paper,true);
            for(int i=0;i<3;i++) {
                int index=i; bool selected=session.SelectedOption==i;
                var b=Button("Option"+i,left,"",24,130+i*144,452,132,()=>session.ChooseOption(index),session.Phase==GamePhase.Editing,selected);
                Text("Number",b.transform,(i+1).ToString("00"),14,14,42,60,23,selected ? Palette.Ink : Palette.Accent,true);
                var optionText=Text("OptionText",b.transform,session.CurrentRound.options[i].text,58,10,378,112,20,selected ? Palette.Ink : Palette.Paper);
                optionText.enableAutoSizing=true; optionText.fontSizeMin=18; optionText.fontSizeMax=20;
            }
            if(session.SelectedChannel>=0) Text("SelectedSeed",left,"Старт: "+session.Network[session.SelectedChannel].name,24,576,452,24,17,Palette.Accent);
            bool ready=session.SelectedChannel>=0 && session.SelectedOption>=0 && session.Phase==GamePhase.Editing;
            Button("Publish",left,session.Phase==GamePhase.Spreading ? "РАСПРОСТРАНЕНИЕ…" : "ОПУБЛИКОВАТЬ  →",24,600,452,62,session.Publish,ready,true);
            var graph=Box("NetworkGraph",content,564,190,996,686,Palette.Panel);
            foreach(var community in MediaNetwork.Communities) {
                var cloudColor=Palette.Link;cloudColor.a=.055f;
                Circle("CommunityCloud",graph,MapPosition(community.center),82,cloudColor);
            }
            var drawn=new System.Collections.Generic.HashSet<int>();
            for(int i=0;i<session.Network.Length;i++) foreach(int j in session.Network[i].neighbors) {
                int key=GameSession.ConnectionKey(i,j); if(!drawn.Add(key)) continue;
                bool active=session.CheckingBatch.Any(a=>GameSession.ConnectionKey(a.source,a.target)==key);
                Color color=Palette.Link; color.a=.62f; float width=2.2f;
                if(session.CheckedConnections.TryGetValue(key,out bool passed)) {color=passed ? Palette.Good : Palette.Bad; color.a=passed ? 1 : .65f;}
                if(active) {color=Palette.Accent; width=4;}
                bool distant=session.Network[i].cluster!=session.Network[j].cluster;
                var direction=(Position(j)-Position(i)).normalized;
                Connection(graph,Position(i)+direction*(Radius(i)+5),Position(j)-direction*(Radius(j)+5),color,distant ? width*.75f : width,distant);
            }
            foreach(var community in MediaNetwork.Communities) {
                var center=MapPosition(community.center);
                var label=Text("CommunityLabel",graph,community.label,center.x-39,center.y-19,78,38,11,Palette.Muted,true);
                label.alignment=TextAlignmentOptions.Center;
            }
            for(int i=0;i<session.Network.Length;i++) {
                int index=i; var n=session.Network[i]; var center=Position(i); float radius=Radius(i);
                bool selected=session.SelectedChannel==i, reached=session.Reached.Contains(i);
                if(selected) Circle("SelectedRing"+i,graph,center,radius+6,Palette.Accent,3);
                else if(n.canPublish) Circle("StartRing"+i,graph,center,radius+5,Palette.CampColor(n.camp),1.5f);
                var rt=Circle("Channel"+i,graph,center,radius,reached ? Palette.Good : selected ? Palette.Accent : Palette.Background,0,true);
                var button=rt.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic=rt.GetComponent<CircleGraphic>();
                button.interactable=session.Phase==GamePhase.Editing;
                var colors=button.colors; colors.normalColor=colors.selectedColor=colors.disabledColor=Color.white;
                colors.highlightedColor=new Color(1.18f,1.18f,1.18f); colors.pressedColor=new Color(.8f,.8f,.8f); colors.fadeDuration=.12f; button.colors=colors;
                button.onClick.AddListener(()=> { InspectChannel(index); if(n.canPublish) session.ChooseChannel(index); });
                var pointer=rt.gameObject.AddComponent<ChannelPointer>(); pointer.view=this; pointer.channel=i;
                Circle("CampOutline",rt,new Vector2(radius,radius),radius,Palette.CampColor(n.camp),2);
                Color fg=reached||selected ? Palette.Ink : Palette.Paper;
                float labelScale=Mathf.Min(1,radius/22);
                var number=Text("NodeId",rt,(i+1).ToString("00"),0,radius-18*labelScale,radius*2,27*labelScale,22*labelScale,fg,true); number.alignment=TextAlignmentOptions.Center;
                var audience=Text("Audience",rt,(n.audience/1000f).ToString("0.#")+"к",0,radius*2-18*labelScale,radius*2,16*labelScale,12*labelScale,fg); audience.alignment=TextAlignmentOptions.Center;
                if(n.canPublish) {
                    var badge=Box("StartBadge"+i,graph,center.x-23,center.y-radius-18,46,15,Palette.CampColor(n.camp));
                    var label=Text("StartTag",badge,"СТАРТ",0,0,46,15,11,Palette.Ink,true); label.alignment=TextAlignmentOptions.Center;
                }
                if(session.NewWave.Contains(i)) StartCoroutine(PopNode(rt));
            }
            Text("GraphLegend",graph,"ЗОЛОТО — ВЛАСТЬ   /   СИНИЙ — ОППОЗИЦИЯ   /   ЗЕЛЁНЫЙ — НЕЙТРАЛЬНЫЕ",24,12,948,24,14,Palette.Muted);
            var detailPanel=Box("ChannelInfo",graph,24,580,948,66,Palette.Background);
            string status=session.Network.Length+" каналов / 9 сообществ • пунктир — узкие переходы";
            Color statusColor=Palette.Muted;
            if(session.CheckingBatch.Count>0) {
                int source=session.Checking.source;
                status="ПРОВЕРКА: "+session.Network[source].name+" → "+session.CheckingBatch.Count+" каналов одновременно";
                statusColor=Palette.Accent;
                Circle("CheckingSource",graph,Position(source),Radius(source)+6,Palette.Accent,1.5f);
                foreach(var attempt in session.CheckingBatch) {
                    var direction=(Position(attempt.target)-Position(source)).normalized;
                    var from=Position(source)+direction*(Radius(source)+6);
                    var to=Position(attempt.target)-direction*(Radius(attempt.target)+6);
                    var halo=Circle("CheckingTarget"+attempt.target,graph,Position(attempt.target),Radius(attempt.target)+8,Palette.Accent,2);
                    var dot=Circle("TransmissionPacket"+attempt.target,graph,Vector2.Lerp(from,to,session.CheckProgress),4.5f,Palette.Accent);
                    transmissions.Add(new TransmissionVisual {dot=dot,halo=halo,from=from,to=to});
                }
            } else if(session.LastBatch.Count>0) {
                int passed=session.LastBatch.Count(a=>a.passed);
                status="ПЕРЕДАНО: "+passed+" из "+session.LastBatch.Count+" / "+session.Network[session.LastAttempt.source].name;
                statusColor=passed>0 ? Palette.Good : Palette.Bad;
                foreach(var attempt in session.LastBatch) Circle("CheckResult"+attempt.target,graph,Position(attempt.target),Radius(attempt.target)+6,attempt.passed ? Palette.Good : Palette.Bad,3);
            }
            Text("CheckStatus",detailPanel,status,12,6,924,23,16,statusColor,true);
            channelDetails=Text("ChannelDetails",detailPanel,"Наведите курсор на узел, чтобы увидеть тематику и аудиторию.",12,34,924,26,16,Palette.Paper);
            InspectChannel(session.SelectedChannel>=0 ? session.SelectedChannel : inspectedChannel);
            if(session.Views==0) shownViews=0;
            var counter=Text("Reach",content,ReachText(session.Views),1200,122,360,60,20,Palette.Accent,true);
            Box("ProgressTrack",graph,24,658,948,12,Palette.Background);
            var fill=Box("ProgressFill",graph,24,658,948*Mathf.Clamp01(shownViews/session.Target),12,Palette.Accent);
            StartCoroutine(AnimateViews(counter,fill));
            if(session.Phase==GamePhase.RoundResult) ResultOverlay();
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
            Text("Reveal",panel,$"Параметры выбранного продолжения:\n{option.kind}  •  {option.topic}  •  {Palette.CampName(option.camp)}\nВиральность: {option.virality:0.00}  /  Правдоподобность: {option.plausibility:0.00}",36,221,1028,110,23,Palette.Ink);
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
