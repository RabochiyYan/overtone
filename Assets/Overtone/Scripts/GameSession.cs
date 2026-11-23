using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Overtone
{
    public enum GamePhase { Menu, Sorting, Briefing, Editing, Spreading, RoundResult, Won, Fired }
    [Serializable] public sealed class SessionSave
    {
        public int phase, lives, folder, cardIndex, round, successfulRounds, runSeed;
        public int version;
        public float sortingSeconds;
        public PhaseTwoTuning phaseTwo;
        public int networkSeed;
        public PhaseTwoUpgrades upgrades;
        public bool retryUsed, choosingSecondStart, lastRoundWon;
        public int firstChannel=-1, selectedOption=-1, selectedChannel=-1, views, roundPoints;
        public int[] reached, checkedEdges, sortingOrder;
        public bool[] checkedPassed;
        public int crossTopicAttempts, waveNumber;
    }
    public sealed class GameSession : MonoBehaviour
    {
        public PrototypeSettings settings;
        public GameView view;
        public GamePhase Phase { get; private set; }
        public int Lives { get; private set; }
        public float SortingTimeLeft { get; private set; }
        public string FailureReason { get; private set; } = "";
        public int Folder { get; private set; }
        public int CardIndex { get; private set; }
        public int Round { get; private set; }
        public int SuccessfulRounds { get; private set; }
        public int SelectedOption { get; private set; } = -1;
        public int SelectedChannel { get; private set; } = -1;
        public int Views { get; private set; }
        public int TotalNetworkViews { get { return Network==null ? 0 : Network.Sum(channel=>channel.audience); } }
        public float BaseTargetShare
        {
            get {
                // A recorded win raises the next assignment, not the result or reward of this one.
                int wins=Mathf.Max(0,SuccessfulRounds-(Phase==GamePhase.RoundResult && LastRoundWon ? 1 : 0));
                return Mathf.Lerp(.20f,.70f,news==null || news.rounds.Length<=1 ? 0 : wins/(float)(news.rounds.Length-1));
            }
        }
        public int Target { get { return TargetForMultiplier(Tuning.targetMultiplier); } }
        public int TargetForMultiplier(float multiplier)
        {
            int total=TotalNetworkViews;
            int minimum=Mathf.CeilToInt(total*.20f), maximum=Mathf.FloorToInt(total*.70f);
            return Mathf.Max(1,Mathf.Clamp(Mathf.RoundToInt(total*BaseTargetShare*multiplier),minimum,Mathf.Max(minimum,maximum)));
        }
        public PhaseTwoTuning Tuning { get; private set; } = new PhaseTwoTuning();
        public int DefaultChannelCount { get { return channelCatalog.DefaultCount; } }
        public PhaseTwoUpgrades Upgrades { get; private set; } = new PhaseTwoUpgrades();
        public bool UpgradeWindowOpen { get; set; }
        public bool InstructionsWindowOpen { get; set; }
        public bool RetryUsed { get; private set; }
        public bool ChoosingSecondStart { get; private set; }
        public int RoundPoints { get; private set; }
        int firstChannel=-1;
        public bool CanSelectChannel(int index) { return Phase==GamePhase.Editing && index>=0 && index<Network.Length && Network[index].canPublish && (!ChoosingSecondStart || !Reached.Contains(index)); }
        public bool CanSecondStart { get { return Phase==GamePhase.RoundResult && !LastRoundWon && Upgrades.secondStart>0 && !RetryUsed && Network.Where((n,i)=>n.canPublish && !Reached.Contains(i)).Any(); } }
        public bool Paused { get; private set; }
        public bool Busy { get; private set; }
        public string Feedback { get; private set; } = "";
        public bool LastCorrect { get; private set; }
        public bool LastRoundWon { get; private set; }
        public bool HasSave { get { return PlayerPrefs.HasKey(SaveKey); } }
        public const string SaveKey="Overtone.Prototype.Save.v1";
        [NonSerialized] public PhaseTwoNews news;
        [NonSerialized] public PhaseOneNews sorting;
        public MediaChannel[] Network { get; private set; }
        public readonly HashSet<int> Reached = new HashSet<int>();
        public readonly HashSet<int> NewWave = new HashSet<int>();
        public int WaveNumber { get; private set; }
        public readonly List<SpreadAttempt> CheckingBatch = new List<SpreadAttempt>();
        public readonly List<SpreadAttempt> LastBatch = new List<SpreadAttempt>();
        public SpreadAttempt Checking { get { return CheckingBatch.Count>0 ? CheckingBatch[0] : null; } }
        public SpreadAttempt LastAttempt { get { return LastBatch.Count>0 ? LastBatch[0] : null; } }
        public float CheckProgress { get; private set; }
        public readonly Dictionary<int,bool> CheckedConnections = new Dictionary<int,bool>();
        public static int ConnectionKey(int a,int b) { return (Math.Min(a,b)<<16)|Math.Max(a,b); }
        public SortingNews CurrentCard { get { int index=CardIndex % sorting.cards.Length; return sorting.cards[sortingOrder!=null ? sortingOrder[index] : index]; } }
        public NewsRound CurrentRound { get { return news.rounds[Round]; } }
        public event Action Changed;
        int runSeed,networkSeed;
        int[] sortingOrder;
        MediaChannelList channelCatalog;
        double clockSample;
        SpreadSimulation simulation;
        public void Initialize()
        {
            news=JsonUtility.FromJson<PhaseTwoNews>(Resources.Load<TextAsset>("PhaseTwoNews").text);
            sorting=JsonUtility.FromJson<PhaseOneNews>(Resources.Load<TextAsset>("PhaseOneNews").text);
            channelCatalog=MediaNetwork.LoadCatalog();networkSeed=Guid.NewGuid().GetHashCode();
            Network=MakeNetwork(); Phase=GamePhase.Menu; Notify();
        }
        void Update()
        {
            double now=Time.realtimeSinceStartupAsDouble;
            float elapsed=(float)(now-clockSample);clockSample=now;
            if(Phase!=GamePhase.Sorting || Paused || Lives<=0 || Folder>=settings.folderTarget) return;
            SortingTimeLeft=Mathf.Max(0,SortingTimeLeft-elapsed);
            if(SortingTimeLeft>0) return;
            StopAllCoroutines(); Busy=false;
            FailureReason="Время на проверку истекло. Папка не собрана.";
            view.PlaySortingErrorSound(); Finish(false);
        }
        public void StartRun()
        {
            StopAllCoroutines(); UpgradeWindowOpen=false; InstructionsWindowOpen=false; Paused=false; Busy=false; Lives=settings.startingLives;
            Folder=0; CardIndex=0; Round=0; SuccessfulRounds=0; runSeed=Environment.TickCount;
            ShuffleSortingNews();
            networkSeed=Guid.NewGuid().GetHashCode();
            Network=MakeNetwork();view.ResetNetworkView();
            SortingTimeLeft=settings.sortingSeconds; FailureReason=""; clockSample=Time.realtimeSinceStartupAsDouble;
            CheckingBatch.Clear(); LastBatch.Clear(); NewWave.Clear(); Reached.Clear(); CheckedConnections.Clear();
            Views=0; WaveNumber=0; CheckProgress=0; SelectedOption=-1; SelectedChannel=-1;
            Upgrades=new PhaseTwoUpgrades(); RetryUsed=false; ChoosingSecondStart=false; RoundPoints=0; firstChannel=-1;
            Phase=GamePhase.Sorting; Feedback="Проверьте источник. Реальные новости — в папку, фейки — в корзину.";
            Save(); Notify();
        }
        void ShuffleSortingNews()
        {
            sortingOrder=Enumerable.Range(0,sorting.cards.Length).ToArray();
            var random=new System.Random(Guid.NewGuid().GetHashCode());
            for(int i=sortingOrder.Length-1;i>0;i--) {
                int j=random.Next(i+1);
                int previous=sortingOrder[i];sortingOrder[i]=sortingOrder[j];sortingOrder[j]=previous;
            }
        }
        public void ResumeRun()
        {
            if(!HasSave) return;
            try {
                var s=JsonUtility.FromJson<SessionSave>(PlayerPrefs.GetString(SaveKey));
                if(s==null || s.round<0 || s.round>=news.rounds.Length || (s.lives<1 && s.phase!=(int)GamePhase.RoundResult)) { StartRun(); return; }
                Tuning=s.phaseTwo ?? new PhaseTwoTuning();Tuning.Clamp(DefaultChannelCount);
                if(s.version>=3)networkSeed=s.networkSeed;
                Upgrades=s.upgrades ?? new PhaseTwoUpgrades();
                Network=MakeNetwork();view.ResetNetworkView();
                Lives=s.lives; Folder=s.folder; CardIndex=s.cardIndex; Round=s.round;
                sortingOrder=s.sortingOrder;
                if(sortingOrder==null || sortingOrder.Length!=sorting.cards.Length || sortingOrder.Distinct().Count()!=sorting.cards.Length || sortingOrder.Any(i=>i<0 || i>=sorting.cards.Length))
                    sortingOrder=Enumerable.Range(0,sorting.cards.Length).ToArray();
                SuccessfulRounds=s.successfulRounds; runSeed=s.runSeed; Busy=false; Paused=false; FailureReason="";
                SortingTimeLeft=s.version>=2 ? Mathf.Max(0,s.sortingSeconds) : settings.sortingSeconds;
                clockSample=Time.realtimeSinceStartupAsDouble;
                if(s.phase==(int)GamePhase.Sorting) { Phase=GamePhase.Sorting; Feedback="Рабочий день продолжается."; if(SortingTimeLeft<=0) {FailureReason="Время на проверку истекло. Папка не собрана.";Finish(false);return;} }
                else {
                    PrepareRound(false);
                    if(s.version>=4) {
                        RetryUsed=s.retryUsed; ChoosingSecondStart=s.choosingSecondStart; firstChannel=s.firstChannel;
                        if(ChoosingSecondStart) {
                            SelectedOption=s.selectedOption;
                            if(s.version>=5) {
                                foreach(int i in s.reached ?? new int[0]) Reached.Add(i);
                                Views=s.views; WaveNumber=s.waveNumber;
                                for(int i=0;i<(s.checkedEdges?.Length ?? 0) && i<(s.checkedPassed?.Length ?? 0);i++)
                                    CheckedConnections[s.checkedEdges[i]]=s.checkedPassed[i];
                                simulation=new SpreadSimulation(Network,CurrentRound.options[SelectedOption],runSeed+Round*97+7919,settings.crossClusterMultiplier,Upgrades);
                                simulation.RestoreCompleted(Reached,s.crossTopicAttempts);
                            }
                            Feedback="Выберите ещё один неохваченный стартовый канал. Просмотры и пройденные каналы сохраняются.";
                        }
                        if(s.phase==(int)GamePhase.RoundResult) {
                            Phase=GamePhase.RoundResult; LastRoundWon=s.lastRoundWon; Views=s.views; RoundPoints=s.roundPoints;
                            SelectedOption=s.selectedOption; SelectedChannel=s.selectedChannel;
                            foreach(int i in s.reached ?? new int[0]) Reached.Add(i);
                        }
                    }
                }
                if(CanSecondStart) ChooseSecondStart(); else Notify();
            } catch { StartRun(); }
        }
        public void Sort(bool intoFolder)
        {
            if(Phase!=GamePhase.Sorting || Busy || Paused || SortingTimeLeft<=0) return;
            Busy=true; LastCorrect=intoFolder==CurrentCard.isReal;
            if(LastCorrect) {
                SortingTimeLeft+=10f;
                if(intoFolder) Folder++;
                Feedback=intoFolder ? "ПРОВЕРЕНО / Новость добавлена в папку. +10 секунд." : "ОТКЛОНЕНО / Фейк отправлен в корзину. +10 секунд.";
            }
            else { Lives--; Feedback=intoFolder ? "ОШИБКА / В папку попал фейк." : "ОШИБКА / Реальная новость потеряна."; }
            Notify(); if(LastCorrect) view.PlayTone(650); else view.PlaySortingErrorSound(); StartCoroutine(AfterSort(intoFolder));
        }
        IEnumerator AfterSort(bool right)
        {
            if(!LastCorrect) yield return view.AnimateSortingError();
            yield return view.AnimateCardOut(right);
            CardIndex++; Busy=false;
            if(Lives<=0) Finish(false);
            else if(Folder>=settings.folderTarget) { Phase=GamePhase.Briefing; Save(); Notify(); }
            else { Save(); Notify(); }
        }
        public void SkipPhaseOne()
        {
            if(Phase!=GamePhase.Sorting || Busy || Paused || Lives<=0 || SortingTimeLeft<=0) return;
            Phase=GamePhase.Briefing;
            BeginPhaseTwo();
        }
        public void BeginPhaseTwo()
        {
            if(Phase!=GamePhase.Briefing) return;
            Lives=settings.startingLives; Round=0; PrepareRound();
        }
        void PrepareRound(bool save=true)
        {
            SelectedOption=-1; SelectedChannel=-1; Views=0; WaveNumber=0; Reached.Clear(); NewWave.Clear();
            RetryUsed=false; ChoosingSecondStart=false; firstChannel=-1; RoundPoints=0; LastRoundWon=false; simulation=null;
            CheckingBatch.Clear(); LastBatch.Clear(); CheckProgress=0; CheckedConnections.Clear();
            Phase=GamePhase.Editing; Busy=false; Feedback="Выберите продолжение и один доступный канал для публикации."; if(save) Save(); Notify();
        }
        public void ChooseOption(int index)
        {
            if(Phase!=GamePhase.Editing || Paused || ChoosingSecondStart || index<0 || index>=3) return;
            SelectedOption=index; view.PlayTone(420); Notify();
        }
        public void ChooseChannel(int index)
        {
            if(Paused || !CanSelectChannel(index)) return;
            SelectedChannel=index; view.PlayTone(480); Notify();
        }
        public void Publish()
        {
            if(Phase!=GamePhase.Editing || Paused || SelectedOption<0 || SelectedChannel<0) return;
            if(!CanSelectChannel(SelectedChannel)) return;
            bool additional=ChoosingSecondStart;
            SpreadWave initial;
            if(additional) {
                if(simulation==null) {
                    simulation=new SpreadSimulation(Network,CurrentRound.options[SelectedOption],runSeed+Round*97+7919,settings.crossClusterMultiplier,Upgrades);
                    simulation.RestoreCompleted(Reached,0);
                }
                initial=simulation.PublishAdditional(SelectedChannel);
            } else {
                firstChannel=SelectedChannel;
                simulation=new SpreadSimulation(Network,CurrentRound.options[SelectedOption],runSeed+Round*97,settings.crossClusterMultiplier,Upgrades);
                initial=simulation.Publish(SelectedChannel);
            }
            ChoosingSecondStart=false;
            Phase=GamePhase.Spreading; Busy=true; ApplyWave(initial);
            Feedback="Новость опубликована. Наблюдайте за цепочкой перепостов."; Notify(); StartCoroutine(Spread());
        }
        IEnumerator Spread()
        {
            while(!simulation.Complete) {
                yield return WaitUnpaused(settings.waveDelay);
                var wave=simulation.Step(); WaveNumber++;
                foreach(var batch in wave.attempts.GroupBy(attempt=>attempt.source)) {
                    CheckingBatch.Clear(); CheckingBatch.AddRange(batch); LastBatch.Clear();
                    CheckProgress=0; NewWave.Clear(); Notify();
                    float elapsed=0;
                    while(elapsed<settings.checkDuration) {
                        if(!Paused && !UpgradeWindowOpen && !InstructionsWindowOpen) elapsed+=Time.unscaledDeltaTime;
                        CheckProgress=Mathf.Clamp01(elapsed/Mathf.Max(.01f,settings.checkDuration));
                        yield return null;
                    }
                    foreach(var attempt in CheckingBatch) {
                        CheckedConnections[ConnectionKey(attempt.source,attempt.target)]=attempt.passed;
                        if(attempt.passed && Reached.Add(attempt.target)) {
                            Views+=Network[attempt.target].audience; NewWave.Add(attempt.target);
                        }
                    }
                    LastBatch.AddRange(CheckingBatch); CheckingBatch.Clear(); CheckProgress=1;
                    view.PlayTone(NewWave.Count>0 ? 620 : 240); Notify();
                    yield return WaitUnpaused(settings.checkDuration*.4f);
                }
            }
            yield return WaitUnpaused(.35f);
            LastRoundWon=Views>=Target;
            Busy=false; Phase=GamePhase.RoundResult;
            if(CanSecondStart) { ChooseSecondStart(); yield break; }
            if(LastRoundWon) SuccessfulRounds++; else Lives--;
            RoundPoints=Upgrades.Award(Views,Target);
            Feedback=LastRoundWon ? "ПЛАН ВЫПОЛНЕН / Информация дошла до нужной аудитории." : "ПЛАН НЕ ВЫПОЛНЕН / Руководство выносит предупреждение.";
            view.PlayTone(LastRoundWon ? 850 : 150); Save(); Notify();
        }
        IEnumerator WaitUnpaused(float duration)
        {
            for(float t=0;t<duration;) { if(!Paused && !UpgradeWindowOpen && !InstructionsWindowOpen) t+=Time.unscaledDeltaTime; yield return null; }
            while(Paused || UpgradeWindowOpen || InstructionsWindowOpen) yield return null;
        }
        void ApplyWave(SpreadWave wave)
        {
            NewWave.Clear(); foreach(int i in wave.channels) { Reached.Add(i); NewWave.Add(i); }
            Views=simulation.Views; if(wave.channels.Count>0) view.PlayTone(520+WaveNumber*35);
        }
        public void NextRound()
        {
            if(Phase!=GamePhase.RoundResult || Paused) return;
            if(Lives<=0) { Finish(false); return; }
            if(CanSecondStart) Lives--;
            if(Lives<=0) { Finish(false); return; }
            Round++; if(Round>=news.rounds.Length) { Round=news.rounds.Length-1; Finish(true); }
            else {
                networkSeed=Guid.NewGuid().GetHashCode();
                Network=MakeNetwork();view.ResetNetworkView();
                PrepareRound();
            }
        }
        public void ChooseSecondStart()
        {
            if(!CanSecondStart || Paused) return;
            RetryUsed=true; ChoosingSecondStart=true; SelectedChannel=-1;
            NewWave.Clear(); CheckingBatch.Clear(); LastBatch.Clear();
            Phase=GamePhase.Editing; Feedback="Выберите ещё один неохваченный стартовый канал. Просмотры и пройденные каналы сохраняются.";
            Save(); Notify();
        }
        public bool BuyUpgrade(UpgradeKind kind)
        {
            if(Paused || Phase!=GamePhase.Editing && Phase!=GamePhase.RoundResult || Lives<=0) return false;
            // A failed round is already settled; a second start must be acquired before publication.
            if(Phase==GamePhase.RoundResult && !LastRoundWon && kind==UpgradeKind.SecondStart) return false;
            if(!Upgrades.Buy(kind)) return false;
            Save(); Notify(); return true;
        }
        void Finish(bool won)
        {
            Phase=won ? GamePhase.Won : GamePhase.Fired; Busy=false;
            PlayerPrefs.DeleteKey(SaveKey); PlayerPrefs.Save(); Notify(); view.PlayTone(won ? 1000 : 120);
        }
        public void TogglePause()
        {
            if(Phase==GamePhase.Menu || Phase==GamePhase.Won || Phase==GamePhase.Fired) return;
            clockSample=Time.realtimeSinceStartupAsDouble; Paused=!Paused; view.RenderPause();
        }
        public void ReturnToMenu()
        {
            if(!(Phase==GamePhase.Spreading && RetryUsed) && Lives>0 && (Phase==GamePhase.Sorting && SortingTimeLeft>0 || Phase==GamePhase.Editing || Phase==GamePhase.Spreading || Phase==GamePhase.RoundResult)) Save();
            StopAllCoroutines(); UpgradeWindowOpen=false; InstructionsWindowOpen=false; Busy=false; Paused=false; Phase=GamePhase.Menu; Notify();
        }
        void Save()
        {
            bool phaseTwoReady=Phase==GamePhase.Briefing || Phase==GamePhase.Sorting && Folder>=settings.folderTarget;
            var phase=Phase==GamePhase.RoundResult ? GamePhase.RoundResult : Phase==GamePhase.Sorting && !phaseTwoReady ? GamePhase.Sorting : GamePhase.Editing;
            // Briefing must resume with the new phase's three lives.
            var s=new SessionSave { phase=(int)phase,lives=phaseTwoReady ? settings.startingLives : Lives,
                folder=Folder,cardIndex=CardIndex+(Phase==GamePhase.Sorting && Busy ? 1 : 0),round=Round,successfulRounds=SuccessfulRounds,runSeed=runSeed,version=6,sortingOrder=sortingOrder,sortingSeconds=SortingTimeLeft,phaseTwo=Tuning,networkSeed=networkSeed,
                crossTopicAttempts=simulation?.CrossTopicAttempts ?? 0,waveNumber=WaveNumber,checkedEdges=CheckedConnections.Keys.ToArray(),checkedPassed=CheckedConnections.Values.ToArray(),
                upgrades=Upgrades,retryUsed=RetryUsed,choosingSecondStart=ChoosingSecondStart || Phase==GamePhase.Spreading && RetryUsed,firstChannel=firstChannel,
                selectedOption=SelectedOption,selectedChannel=SelectedChannel,views=Views,reached=Reached.ToArray(),lastRoundWon=LastRoundWon,roundPoints=RoundPoints };
            PlayerPrefs.SetString(SaveKey,JsonUtility.ToJson(s)); PlayerPrefs.Save();
        }
        void Notify() { Changed?.Invoke(); }
        MediaChannel[] MakeNetwork() { return MediaNetwork.Create(channelCatalog,networkSeed,Tuning); }
        public void ApplyTuning(PhaseTwoTuning requested)
        {
            if(Phase!=GamePhase.Editing || Paused || ChoosingSecondStart || requested==null)return;
            var next=requested.Copy();next.Clamp(DefaultChannelCount);
            bool rebuild=!Tuning.SameNetwork(next,DefaultChannelCount);
            Tuning=next;
            if(rebuild) {
                Network=MakeNetwork();SelectedChannel=-1;
                Reached.Clear();NewWave.Clear();CheckingBatch.Clear();LastBatch.Clear();CheckedConnections.Clear();
                Views=0;WaveNumber=0;CheckProgress=0;view.ResetNetworkView();
            }
            Save();Notify();
        }
    }
}
