using System;
using UnityEngine;
namespace Overtone
{
    [Serializable]
    public sealed class PhaseTwoTuning
    {
        public float targetMultiplier=1;
        // Zero follows the editable catalog instead of hard-coding its current size.
        public int channelCount;
        public float meanLinks=3.55f;
        public float topicMix=.1f;
        public float campGrouping;
        public float audienceSpread=1;

        public PhaseTwoTuning Copy() { return (PhaseTwoTuning)MemberwiseClone(); }
        public int Count(int catalogCount) { return channelCount==0 ? catalogCount : channelCount; }
        public void Clamp(int catalogCount)
        {
            targetMultiplier=Mathf.Clamp(targetMultiplier,.1f,3);
            channelCount=channelCount==0 ? 0 : Mathf.Clamp(channelCount,1,Math.Max(300,catalogCount));
            meanLinks=Mathf.Clamp(meanLinks,2,12);
            topicMix=Mathf.Clamp01(topicMix);campGrouping=Mathf.Clamp01(campGrouping);
            audienceSpread=Mathf.Clamp(audienceSpread,0,2);
        }
        public bool SameNetwork(PhaseTwoTuning other,int catalogCount)
        {
            return Count(catalogCount)==other.Count(catalogCount) && Mathf.Approximately(meanLinks,other.meanLinks)
                && Mathf.Approximately(topicMix,other.topicMix) && Mathf.Approximately(campGrouping,other.campGrouping)
                && Mathf.Approximately(audienceSpread,other.audienceSpread);
        }
    }
}
