using System;

namespace Overtone
{
    public enum UpgradeKind { CrossTopic, Liberal, Conservative, StartingChannel, SecondStart }

    [Serializable]
    public sealed class PhaseTwoUpgrades
    {
        public int points, surplusViews;
        public int crossTopic, liberal, conservative, startingChannel, secondStart;
        public int Level(UpgradeKind kind)
        {
            switch(kind) {
                case UpgradeKind.CrossTopic: return crossTopic;
                case UpgradeKind.Liberal: return liberal;
                case UpgradeKind.Conservative: return conservative;
                case UpgradeKind.StartingChannel: return startingChannel;
                default: return secondStart;
            }
        }
        public static int MaxLevel(UpgradeKind kind) { return kind==UpgradeKind.SecondStart ? 1 : 3; }
        public static int Cost(UpgradeKind kind,int level) { return kind==UpgradeKind.SecondStart ? 10 : level==1 ? 2 : level==2 ? 4 : 6; }
        public bool IsLocked(UpgradeKind kind)
        {
            return kind==UpgradeKind.Liberal && conservative>0 || kind==UpgradeKind.Conservative && liberal>0;
        }
        public bool Buy(UpgradeKind kind)
        {
            if(!Enum.IsDefined(typeof(UpgradeKind),kind) || IsLocked(kind)) return false;
            int level=Level(kind)+1;
            if(level>MaxLevel(kind) || points<Cost(kind,level)) return false;
            points-=Cost(kind,level);
            switch(kind) {
                case UpgradeKind.CrossTopic: crossTopic=level; break;
                case UpgradeKind.Liberal: liberal=level; break;
                case UpgradeKind.Conservative: conservative=level; break;
                case UpgradeKind.StartingChannel: startingChannel=level; break;
                case UpgradeKind.SecondStart: secondStart=level; break;
            }
            return true;
        }
        public int Award(int views,int target)
        {
            int total=surplusViews+Math.Max(0,views-target);
            int earned=total/10000;
            points+=earned; surplusViews=total%10000;
            return earned;
        }
        public static double CampBonus(int level) { return level==1 ? .25 : level==2 ? .5 : level>=3 ? 1 : 0; }
    }
}
