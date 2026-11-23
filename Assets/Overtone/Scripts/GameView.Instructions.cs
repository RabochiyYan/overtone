using UnityEngine;

namespace Overtone
{
    public sealed partial class GameView
    {
        RectTransform instructionsModal;
        bool IsPhaseTwo { get { return session.Phase==GamePhase.Editing || session.Phase==GamePhase.Spreading || session.Phase==GamePhase.RoundResult; } }
        public void OpenInstructions()
        {
            if(!IsPhaseTwo) return;
            CloseNetworkSettings();
            session.InstructionsWindowOpen=true;
            DrawInstructions();
        }
        public void CloseInstructions()
        {
            session.InstructionsWindowOpen=false;
            Clear(ref instructionsModal);
        }
        void DrawInstructions()
        {
            Clear(ref instructionsModal);
            instructionsModal=Box("InstructionsOverlay",stage,0,0,1600,900,new Color(.025f,.035f,.04f,.97f),true);
            var panel=Box("InstructionsPanel",instructionsModal,120,70,1360,760,Palette.Panel,true);
            Text("InstructionTitle",panel,"КАК РАСПРОСТРАНЯТЬ НОВОСТИ",36,24,1288,52,34,Palette.Paper,true);
            Text("InstructionPause",panel,"Пока инструкция открыта, распространение приостановлено. Закрыть — кнопкой или Esc.",36,88,1288,42,21,Palette.Muted);
            Text("ActionsTitle",panel,"01  /  ПУБЛИКАЦИЯ",36,155,600,36,25,Palette.Accent,true);
            Text("ActionsRules",panel,"Выберите одно из трёх продолжений новости, затем канал с меткой «СТАРТ». Нажмите «Опубликовать» и наблюдайте за перепостами.",36,205,600,140,23,Palette.Paper);
            Text("NetworkTitle",panel,"02  /  КАРТА И ПЕРЕДАЧА",36,365,600,36,25,Palette.Accent,true);
            Text("NetworkRules",panel,"Связи показывают пути передачи. Цвет узла — тематика, значок под ним — лагерь канала. Наведите курсор для подробностей.\n\nПередача зависит от тематики, лагеря и свойств новости. Свойства раскрываются после результата. Каждый канал добавляет свою аудиторию один раз.",36,415,600,220,22,Palette.Paper);
            Text("GoalTitle",panel,"03  /  ПЛАН ПРОСМОТРОВ",724,155,600,36,25,Palette.Accent,true);
            Text("GoalRules",panel,"Наберите указанный на экране план: 20–70% суммарной аудитории сети. Сложность растёт только после успеха. Между заданиями карта генерируется заново с выбранными настройками.",724,205,600,140,23,Palette.Paper);
            Text("UpgradesTitle",panel,"04  /  УЛУЧШЕНИЯ",724,365,600,36,25,Palette.Accent,true);
            Text("UpgradesRules",panel,"За каждые 10 000 просмотров сверх плана — 1 балл; остаток переносится. Покупайте уровни через «Улучшения». Либеральная и консервативная ветки исключают друг друга.\n\n«Второй шанс» после неудачи даёт ещё один старт с сохранением охвата и просмотров.",724,415,600,220,22,Palette.Paper);
            Box("InstructionFooter",panel,24,650,1312,94,Palette.Raised);
            Text("LivesRules",panel,"Допуски: "+session.Lives+" из "+session.settings.startingLives+". Окончательная неудача отнимает один; когда допуски закончатся, рабочий день завершится.",36,666,900,68,20,Palette.Muted);
            Button("CloseInstructions",panel,"ВЕРНУТЬСЯ  /  ESC",982,680,342,54,CloseInstructions,true,true);
        }
    }
}
