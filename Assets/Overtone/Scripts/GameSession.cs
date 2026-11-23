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
        public int Target { get { return settings.viewTargets[Mathf.Min(Round,settings.viewTargets.Length-1)]; } }
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
        public SortingNews CurrentCard { get { return sorting.cards[CardIndex % sorting.cards.Length]; } }
        public NewsRound CurrentRound { get { return news.rounds[Round]; } }
        public event Action Changed;
        int runSeed;
        double clockSample;
        SpreadSimulation simulation;
        public void Initialize()
        {
            news=JsonUtility.FromJson<PhaseTwoNews>(Resources.Load<TextAsset>("PhaseTwoNews").text);
            sorting=JsonUtility.FromJson<PhaseOneNews>(Resources.Load<TextAsset>("PhaseOneNews").text);
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
            StopAllCoroutines(); Paused=false; Busy=false; Lives=settings.startingLives;
            Folder=0; CardIndex=0; Round=0; SuccessfulRounds=0; runSeed=Environment.TickCount;
            SortingTimeLeft=settings.sortingSeconds; FailureReason=""; clockSample=Time.realtimeSinceStartupAsDouble;
            CheckingBatch.Clear(); LastBatch.Clear(); NewWave.Clear(); Reached.Clear(); CheckedConnections.Clear();
            Views=0; WaveNumber=0; CheckProgress=0; SelectedOption=-1; SelectedChannel=-1;
            Phase=GamePhase.Sorting; Feedback="Проверьте источник. Реальные новости — в папку, фейки — в корзину.";
            Save(); Notify();
        }
        public void ResumeRun()
        {
            if(!HasSave) return;
            try {
                var s=JsonUtility.FromJson<SessionSave>(PlayerPrefs.GetString(SaveKey));
                if(s==null || s.round<0 || s.round>=news.rounds.Length || s.lives<1) { StartRun(); return; }
                Lives=s.lives; Folder=s.folder; CardIndex=s.cardIndex; Round=s.round;
                SuccessfulRounds=s.successfulRounds; runSeed=s.runSeed; Busy=false; Paused=false; FailureReason="";
                SortingTimeLeft=s.version>=2 ? Mathf.Clamp(s.sortingSeconds,0,settings.sortingSeconds) : settings.sortingSeconds;
                clockSample=Time.realtimeSinceStartupAsDouble;
                if(s.phase==(int)GamePhase.Sorting) { Phase=GamePhase.Sorting; Feedback="Рабочий день продолжается."; if(SortingTimeLeft<=0) {FailureReason="Время на проверку истекло. Папка не собрана.";Finish(false);return;} }
                else PrepareRound();
                Notify();
            } catch { StartRun(); }
        }
        public void Sort(bool intoFolder)
        {
            if(Phase!=GamePhase.Sorting || Busy || Paused || SortingTimeLeft<=0) return;
            Busy=true; LastCorrect=intoFolder==CurrentCard.isReal;
            if(LastCorrect) { if(intoFolder) Folder++; Feedback=intoFolder ? "ПРОВЕРЕНО / Новость добавлена в папку." : "ОТКЛОНЕНО / Фейк отправлен в корзину."; }
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
        void PrepareRound()
        {
            SelectedOption=-1; SelectedChannel=-1; Views=0; WaveNumber=0; Reached.Clear(); NewWave.Clear();
            CheckingBatch.Clear(); LastBatch.Clear(); CheckProgress=0; CheckedConnections.Clear();
            Phase=GamePhase.Editing; Busy=false; Feedback="Выберите продолжение и один доступный канал для публикации."; Save(); Notify();
        }
        public void ChooseOption(int index)
        {
            if(Phase!=GamePhase.Editing || Paused || index<0 || index>=3) return;
            SelectedOption=index; view.PlayTone(420); Notify();
        }
        public void ChooseChannel(int index)
        {
            if(Phase!=GamePhase.Editing || Paused || index<0 || index>=Network.Length || !Network[index].canPublish) return;
            SelectedChannel=index; view.PlayTone(480); Notify();
        }
        public void Publish()
        {
            if(Phase!=GamePhase.Editing || Paused || SelectedOption<0 || SelectedChannel<0) return;
            simulation=new SpreadSimulation(Network,CurrentRound.options[SelectedOption],runSeed+Round*97,settings.crossClusterMultiplier);
            Phase=GamePhase.Spreading; Busy=true; ApplyWave(simulation.Publish(SelectedChannel));
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
                        if(!Paused) elapsed+=Time.unscaledDeltaTime;
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
            LastRoundWon=Views>=Target; if(LastRoundWon) SuccessfulRounds++; else Lives--;
            Busy=false; Phase=GamePhase.RoundResult;
            Feedback=LastRoundWon ? "ПЛАН ВЫПОЛНЕН / Информация дошла до нужной аудитории." : "ПЛАН НЕ ВЫПОЛНЕН / Руководство выносит предупреждение.";
            view.PlayTone(LastRoundWon ? 850 : 150); Notify();
        }
        IEnumerator WaitUnpaused(float duration)
        {
            for(float t=0;t<duration;) { if(!Paused) t+=Time.unscaledDeltaTime; yield return null; }
            while(Paused) yield return null;
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
            Round++; if(Round>=news.rounds.Length) { Round=news.rounds.Length-1; Finish(true); } else PrepareRound();
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
            if(Phase==GamePhase.Sorting && Lives>0 && SortingTimeLeft>0) Save();
            StopAllCoroutines(); Busy=false; Paused=false; Phase=GamePhase.Menu; Notify();
        }
        void Save()
        {
            bool phaseTwoReady=Phase==GamePhase.Briefing || Phase==GamePhase.Sorting && Folder>=settings.folderTarget;
            var phase=Phase==GamePhase.Sorting && !phaseTwoReady ? GamePhase.Sorting : GamePhase.Editing;
            // Briefing must resume with the new phase's three lives.
            var s=new SessionSave { phase=(int)phase,lives=phaseTwoReady ? settings.startingLives : Lives,
                folder=Folder,cardIndex=CardIndex+(Phase==GamePhase.Sorting && Busy ? 1 : 0),round=Round,successfulRounds=SuccessfulRounds,runSeed=runSeed,version=2,sortingSeconds=SortingTimeLeft };
            PlayerPrefs.SetString(SaveKey,JsonUtility.ToJson(s)); PlayerPrefs.Save();
        }
        void Notify() { Changed?.Invoke(); }
        static MediaChannel[] MakeNetwork() { return MediaNetwork.Create(); }
    }
}
