using System;

namespace Overtone
{
    // Values follow the supplied document: 0 = government, 1 = opposition, 2 = neutral.
    public enum Camp { Government, Opposition, Neutral }

    [Serializable]
    public sealed class NewsOption
    {
        public string text;
        public string kind;
        public string topic;
        public float virality;
        public float plausibility;
        public int camp;
    }

    [Serializable]
    public sealed class NewsRound
    {
        public string opening;
        public NewsOption[] options;
    }

    [Serializable]
    public sealed class PhaseTwoNews
    {
        public NewsRound[] rounds;
    }

    [Serializable]
    public sealed class SortingNews
    {
        public string id;
        public string headline;
        public string body;
        public bool isReal;
        public bool placeholder;
    }

    [Serializable]
    public sealed class PhaseOneNews
    {
        public SortingNews[] cards;
    }
}
