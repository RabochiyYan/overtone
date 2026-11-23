using System;
using System.Collections.Generic;

namespace Overtone
{
    [Serializable]
    public sealed class MediaChannel
    {
        public string name;
        public float x,y;
        public string platform;
        public string topic;
        public string[] topics;
        public bool allTopics;
        public bool priority;
        public int camp;
        public int cluster;
        public string clusterName;
        public int audience;
        public bool canPublish;
        public int[] neighbors;

        public bool CoversTopic(string value)
        {
            return allTopics || topic==value || (topics!=null && Array.IndexOf(topics,value)>=0);
        }
    }

    public sealed class SpreadAttempt
    {
        public int source;
        public int target;
        public bool passed;
    }

    public sealed class SpreadWave
    {
        public readonly List<int> channels = new List<int>();
        public int views;
        public readonly List<SpreadAttempt> attempts = new List<SpreadAttempt>();
    }

    /// <summary>Each directed edge is tried once, when its source first receives the story.</summary>
    public sealed class SpreadSimulation
    {
        readonly MediaChannel[] network;
        readonly NewsOption news;
        readonly Random random;
        readonly double bridgeMultiplier;
        readonly HashSet<int> reached = new HashSet<int>();
        List<int> frontier = new List<int>();

        public int Views { get; private set; }
        public int ReachedCount { get { return reached.Count; } }
        public bool Complete { get { return frontier.Count == 0; } }

        public SpreadSimulation(MediaChannel[] network, NewsOption news, int seed, double bridgeMultiplier = .85)
        {
            this.network = network;
            this.news = news;
            random = new Random(seed);
            this.bridgeMultiplier = bridgeMultiplier;
        }

        public SpreadWave Publish(int channel)
        {
            if (reached.Count > 0) throw new InvalidOperationException("Story already published.");
            if (channel < 0 || channel >= network.Length || !network[channel].canPublish)
                throw new ArgumentOutOfRangeException(nameof(channel));
            var wave = new SpreadWave();
            Infect(channel, wave);
            frontier.Add(channel);
            return wave;
        }

        public SpreadWave Step()
        {
            var wave = new SpreadWave();
            var next = new List<int>();
            foreach (int source in frontier)
            {
                foreach (int target in network[source].neighbors)
                {
                    if (reached.Contains(target)) continue;
                    bool passed = random.NextDouble() < Probability(news, network[target]) * (network[source].cluster==network[target].cluster ? 1 : bridgeMultiplier);
                    wave.attempts.Add(new SpreadAttempt { source=source, target=target, passed=passed });
                    if (!passed) continue;
                    Infect(target, wave);
                    next.Add(target);
                }
            }
            frontier = next;
            return wave;
        }

        void Infect(int channel, SpreadWave wave)
        {
            reached.Add(channel);
            wave.channels.Add(channel);
            // The prototype counts each channel audience once; cross-channel overlap is not modeled.
            int views = network[channel].audience;
            wave.views += views;
            Views += views;
        }

        public static double Probability(NewsOption news, MediaChannel channel)
        {
            double topic = channel.CoversTopic(news.topic) ? 0.22 : -0.05;
            double camp = channel.camp == news.camp ? 0.20 :
                channel.camp == (int)Camp.Neutral || news.camp == (int)Camp.Neutral ? 0 : -0.20;
            double credibility = channel.camp == (int)Camp.Neutral ? 0.30 : 0.23;
            double value = 0.18 + news.virality * 0.34 + news.plausibility * credibility + topic + camp;
            return Math.Max(0.05, Math.Min(0.95, value));
        }
    }
}
