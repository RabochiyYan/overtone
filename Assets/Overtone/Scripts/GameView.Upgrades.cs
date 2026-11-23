using UnityEngine;
using TMPro;

namespace Overtone
{
    public sealed partial class GameView
    {
        RectTransform upgradesModal;
        int upgradeSelection;
        static readonly string[] UpgradeTitles={"МЕЖДУ ТЕМАМИ","ЛИБЕРАЛЬНЫЕ НОВОСТИ","КОНСЕРВАТИВНЫЕ НОВОСТИ","СИЛЬНЫЙ СТАРТ","ВТОРОЙ ШАНС"};
        static readonly string[] UpgradeMarks={"↔","Л","К","→","II"};
        static readonly Color UpgradeRed=new Color(.66f,.16f,.18f);
        public void OpenUpgrades()
        {
            if(session.Paused) return;
            session.UpgradeWindowOpen=true; DrawUpgrades();
        }
        public void CloseUpgrades()
        {
            session.UpgradeWindowOpen=false; Clear(ref upgradesModal);
        }
        string UpgradeEffect(UpgradeKind kind,int level)
        {
            if(kind==UpgradeKind.CrossTopic) return "Число гарантированных передач между каналами разной тематики: "+level+". Вероятность — 100%. Лимит действует на всё задание.";
            if(kind==UpgradeKind.StartingChannel) return level==3 ? "Все передачи от стартового канала проходят с вероятностью 100%." : "Вероятность передачи от стартового канала увеличена на "+(level==1 ? "50" : "100")+"%.";
            if(kind==UpgradeKind.SecondStart) return "Если план не выполнен, можно запустить новость ещё из одного неохваченного канала. Пройденные каналы и просмотры сохраняются. Допуск теряется только если второй старт тоже не помог.";
            return "Вероятность передачи "+(kind==UpgradeKind.Liberal ? "либеральной" : "консервативной")+" новости увеличена на "+(level==1 ? "25" : level==2 ? "50" : "100")+"%.";
        }
        void DrawUpgrades()
        {
            Clear(ref upgradesModal);
            upgradesModal=Box("UpgradesOverlay",stage,0,0,1600,900,new Color(.025f,.035f,.04f,.97f),true);
            var panel=Box("UpgradePanel",upgradesModal,110,65,1380,770,Palette.Panel,true);
            Text("UpgradeTitle",panel,"ЭВОЛЮЦИЯ РАСПРОСТРАНЕНИЯ",36,24,1000,52,34,Palette.Paper,true);
            Text("UpgradePoints",panel,session.Upgrades.points+" БАЛЛОВ",1040,24,300,52,30,Palette.Accent,true);
            Text("UpgradeEconomy",panel,"1 балл за каждые 10 000 просмотров сверх плана. Остаток: "+Count(session.Upgrades.surplusViews)+" / 10 000. Баллы выдаются по итогам задания.",36,90,1300,56,20,Palette.Muted);
            for(int branch=0;branch<5;branch++) {
                var kind=(UpgradeKind)branch;
                bool locked=session.Upgrades.IsLocked(kind);
                Color foreground=locked ? Color.gray : Palette.Paper;
                float y=167+branch*100;
                if(branch==upgradeSelection) Box("SelectedBranch",panel,24,y+8,340,84,Palette.Raised);
                Text("Branch"+branch,panel,UpgradeTitles[branch],36,y+27,340,64,20,foreground,true);
                int level=session.Upgrades.Level(kind), max=PhaseTwoUpgrades.MaxLevel(kind);
                for(int tier=1;tier<=max;tier++) {
                    int selected=branch, selectedTier=tier;
                    float x=400+(tier-1)*100;
                    bool owned=tier<=level, next=tier==level+1;
                    if(tier>1) Line(panel,new Vector2(x-13,y+44),new Vector2(x+12,y+44),!locked && owned ? Palette.Accent : Palette.Muted,2);
                    var rt=Rect("Hex"+branch+"_"+tier,panel,x,y,100,100);
                    var graphic=rt.gameObject.AddComponent<HexagonGraphic>();
                    graphic.color=locked ? new Color(.26f,.26f,.26f) : owned ? UpgradeRed : next ? new Color(.49f,.19f,.21f) : Palette.Raised;
                    graphic.raycastTarget=true;
                    var button=rt.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic=graphic; button.interactable=!locked;
                    var colors=button.colors; colors.highlightedColor=new Color(1.35f,1.2f,1.2f); colors.selectedColor=colors.highlightedColor; button.colors=colors;
                    if(!locked) button.onClick.AddListener(()=>{upgradeSelection=selected; DrawUpgrades();});
                    var icon=Text("Mark",rt,UpgradeMarks[branch],22,10,56,35,28,foreground,true);icon.alignment=TextAlignmentOptions.Center;
                    var label=Text("Tier",rt,(selectedTier==1 ? "I" : selectedTier==2 ? "II" : "III"),18,44,64,25,19,foreground,true);label.alignment=TextAlignmentOptions.Center;
                    var cost=Text("Cost",rt,locked ? "ЗАКР." : owned ? "ВЗЯТО" : PhaseTwoUpgrades.Cost(kind,tier)+" Б.",18,68,64,24,16,locked ? Color.gray : owned ? Palette.Paper : Palette.Accent);cost.alignment=TextAlignmentOptions.Center;
                }
            }
            var selectedKind=(UpgradeKind)upgradeSelection;
            int current=session.Upgrades.Level(selectedKind), maximum=PhaseTwoUpgrades.MaxLevel(selectedKind);
            int nextLevel=Mathf.Min(current+1,maximum);
            var details=Box("UpgradeDetails",panel,790,171,550,473,Palette.Background);
            Text("DetailTitle",details,UpgradeTitles[upgradeSelection],28,24,494,66,27,Palette.Accent,true);
            Text("CurrentLevel",details,"УРОВЕНЬ "+current+" / "+maximum,28,98,494,30,20,Palette.Muted,true);
            Text("Effect",details,(current==maximum ? "Действует:\n" : "Следующий уровень:\n")+UpgradeEffect(selectedKind,nextLevel),28,148,494,220,23,Palette.Paper);
            bool allowed=session.Phase==GamePhase.Editing || session.Phase==GamePhase.RoundResult;
            if(session.Phase==GamePhase.RoundResult && !session.LastRoundWon && selectedKind==UpgradeKind.SecondStart) allowed=false;
            bool selectedLocked=session.Upgrades.IsLocked(selectedKind);
            bool canBuy=!selectedLocked && current<maximum && session.Upgrades.points>=PhaseTwoUpgrades.Cost(selectedKind,nextLevel) && allowed && session.Lives>0;
            string buy=selectedLocked ? "ВЕТКА ЗАБЛОКИРОВАНА" : current==maximum ? "МАКСИМАЛЬНЫЙ УРОВЕНЬ" : !allowed ? "ПОКУПКА ДО ПУБЛИКАЦИИ" : "УЛУЧШИТЬ / "+PhaseTwoUpgrades.Cost(selectedKind,nextLevel)+" Б.";
            Button("BuyUpgrade",details,buy,28,385,494,60,()=>session.BuyUpgrade(selectedKind),canBuy,true);
            Text("UpgradeHint",panel,"Либеральная и консервативная ветки исключают друг друга. Выбор действует до конца рабочего дня. Бонусы умножаются, предел — 100%.",36,662,640,96,18,Palette.Muted);
            Button("UpgradeInstructions",panel,"ИНСТРУКЦИЯ",704,683,286,54,OpenInstructions);
            Button("CloseUpgrades",panel,"ВЕРНУТЬСЯ  /  ESC",1010,683,330,54,CloseUpgrades);
        }
    }
}
