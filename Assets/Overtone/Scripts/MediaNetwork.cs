using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Overtone
{
    [Serializable]
    public sealed class MediaChannelList
    {
        public MediaChannel[] channels;
        public int DefaultCount { get { int count=channels.Count(n=>n!=null && n.priority); return count>0 ? count : channels.Length; } }
    }

    public sealed class MediaCommunity
    {
        public string name,label;
        public Color color;
    }

    /// <summary>Random topic-biased graph and force-directed layout from PhaseTwoChannels.json.</summary>
    public static class MediaNetwork
    {
        public static MediaCommunity[] Communities { get; private set; } = new MediaCommunity[0];
        public static float Height { get; private set; } = 480;
        public static float Width { get; private set; } = 948;
        public static float Radius(int audience) { return Mathf.Lerp(12,21,Mathf.Clamp01(audience/22000f)); }

        public static MediaChannelList LoadCatalog()
        {
            var asset=Resources.Load<TextAsset>("PhaseTwoChannels");
            if(asset==null) throw new InvalidOperationException("Не найден Resources/PhaseTwoChannels.json.");
            return JsonUtility.FromJson<MediaChannelList>(asset.text);
        }
        public static MediaChannel[] Create() { return Create(LoadCatalog()); }
        public static MediaChannel[] Create(MediaChannelList data) { return Create(data,Guid.NewGuid().GetHashCode()); }
        public static MediaChannel[] Create(MediaChannelList data,int seed) { return Create(data,seed,new PhaseTwoTuning()); }

        static MediaChannel[] SelectChannels(MediaChannelList data,int count)
        {
            var preferred=data.channels.Where(n=>n!=null && n.priority).ToArray();
            var fallback=data.channels.Where(n=>n==null || !n.priority).ToArray();
            var ordered=preferred.Concat(fallback).ToArray();
            if(ordered.Length==0) return new MediaChannel[0];
            var repeats=fallback.Length>0 ? fallback : preferred;
            return Enumerable.Range(0,count).Select(i=>i<ordered.Length ? ordered[i] : repeats[(i-ordered.Length)%repeats.Length]).ToArray();
        }

        public static MediaChannel[] Create(MediaChannelList data,int seed,PhaseTwoTuning tuning)
        {
            if(data==null || data.channels==null)
                throw new ArgumentException("PhaseTwoChannels.json: нужен массив channels (можно пустой).");
            tuning=(tuning ?? new PhaseTwoTuning()).Copy();tuning.Clamp(data.channels.Length);
            int count=data.channels.Length==0 ? 0 : tuning.Count(data.DefaultCount);
            var selected=SelectChannels(data,count);
            int repeatCount=data.channels.Count(n=>n==null || !n.priority);
            if(repeatCount==0) repeatCount=data.channels.Length;
            var graph=new MediaChannel[count];
            for(int i=0;i<graph.Length;i++) {
                bool repeated=i>=data.channels.Length;
                var n=selected[i];
                if(n==null || string.IsNullOrWhiteSpace(n.name) || string.IsNullOrWhiteSpace(n.topic) || n.audience<0 || n.camp<0 || n.camp>2)
                    throw new ArgumentException("PhaseTwoChannels.json: проверьте name, topic, audience и camp у канала №"+(i+1));
                string topic=n.topic.Trim().TrimStart('#').Trim().ToLowerInvariant();
                if(topic.Length==0) throw new ArgumentException("Пустая тематика у канала №"+(i+1));
                var topics=(n.topics ?? new string[0]).Concat(new[]{topic})
                    .Where(t=>!string.IsNullOrWhiteSpace(t)).Select(t=>"#"+t.Trim().TrimStart('#').Trim().ToLowerInvariant()).Distinct().ToArray();
                graph[i]=new MediaChannel {name=n.name.Trim()+(repeated ? " · "+(2+(i-data.channels.Length)/repeatCount) : ""),topic="#"+topic,topics=topics,allTopics=n.allTopics,priority=n.priority,platform=n.platform ?? "",audience=n.audience,camp=n.camp,canPublish=n.canPublish};
            }
            AdjustAudiences(graph,tuning.audienceSpread);
            var groups=Enumerable.Range(0,graph.Length).GroupBy(i=>graph[i].topic).Select(g=>g.ToArray()).ToArray();
            Communities=groups.Select((g,index)=>new MediaCommunity {name=graph[g[0]].topic.Substring(1),label=graph[g[0]].topic,color=Color.HSVToRGB((.04f+index*.618034f)%1,.57f,.94f)}).ToArray();
            for(int group=0;group<groups.Length;group++) {
                var members=groups[group];
                if(!members.Any(i=>graph[i].canPublish)) graph[members[0]].canPublish=true;
                foreach(int i in members) {graph[i].cluster=group;graph[i].clusterName=Communities[group].name;}
            }
            var random=new System.Random(seed);
            var links=Enumerable.Range(0,graph.Length).Select(i=>new HashSet<int>()).ToArray();
            var components=Enumerable.Range(0,graph.Length).ToArray();
            Func<int,int> root=null;
            root=i=>components[i]==i ? i : components[i]=root(components[i]);
            Action<int,int> add=(a,b)=>{if(a==b)return;links[a].Add(b);links[b].Add(a);components[root(a)]=root(b);};
            // Normalize camp preference per node, so grouping does not simply multiply density.
            var campWeights=new double[graph.Length];
            for(int i=0;i<graph.Length;i++) {
                double baseline=0,weighted=0;
                for(int j=0;j<graph.Length;j++) if(i!=j) {
                    double p=LinkProbability(i,j,graph,groups,tuning);
                    baseline+=p;weighted+=p*CampWeight(graph[i],graph[j],tuning.campGrouping);
                }
                campWeights[i]=weighted>0 ? baseline/weighted : 1;
            }
            if(Mathf.Approximately(tuning.meanLinks,3.55f)) {
                // Similar topics strongly increase the probability, without prescribing blocks or gateways.
                for(int i=0;i<graph.Length;i++) for(int j=i+1;j<graph.Length;j++) {
                    double probability=LinkProbability(i,j,graph,groups,tuning);
                    probability*=CampWeight(graph[i],graph[j],tuning.campGrouping)*Math.Sqrt(campWeights[i]*campWeights[j]);
                    if(random.NextDouble()<probability) add(i,j);
                }
                // Repair isolated components with randomly weighted joins; never depend on file indices.
                var candidates=new List<Tuple<double,int,int>>();
                for(int i=0;i<graph.Length;i++) for(int j=i+1;j<graph.Length;j++) {
                    if(root(i)==root(j))continue;
                    double topicWeight=tuning.topicMix<=.1f ? 20+180*(1-tuning.topicMix/.1f) : 1+19*(1-tuning.topicMix)/.9f;
                    double weight=(graph[i].cluster==graph[j].cluster ? topicWeight : 1)*CampWeight(graph[i],graph[j],tuning.campGrouping);
                    candidates.Add(Tuple.Create(-Math.Log(Math.Max(.0000001,random.NextDouble()))/weight,i,j));
                }
                foreach(var edge in candidates.OrderBy(e=>e.Item1)) if(root(edge.Item2)!=root(edge.Item3)) add(edge.Item2,edge.Item3);
            } else {
                // Weighted sampling without replacement makes the requested average meaningful
                // even when small topic groups run out of possible internal edges.
                var ranked=new List<Tuple<double,int,int>>();
                for(int i=0;i<graph.Length;i++) for(int j=i+1;j<graph.Length;j++) {
                    double weight=LinkProbability(i,j,graph,groups,tuning)*CampWeight(graph[i],graph[j],tuning.campGrouping)*Math.Sqrt(campWeights[i]*campWeights[j]);
                    ranked.Add(Tuple.Create(-Math.Log(Math.Max(.0000001,random.NextDouble()))/Math.Max(.00000001,weight),i,j));
                }
                var ordered=ranked.OrderBy(e=>e.Item1).ToArray();
                int edges=0;
                foreach(var edge in ordered) if(root(edge.Item2)!=root(edge.Item3)) {add(edge.Item2,edge.Item3);edges++;}
                int target=Math.Min(graph.Length*(graph.Length-1)/2,Math.Max(graph.Length-1,Mathf.RoundToInt(tuning.meanLinks*graph.Length/2)));
                foreach(var edge in ordered) {
                    if(edges>=target)break;
                    if(links[edge.Item2].Contains(edge.Item3))continue;
                    add(edge.Item2,edge.Item3);edges++;
                }
            }
            for(int i=0;i<graph.Length;i++) graph[i].neighbors=links[i].OrderBy(j=>j).ToArray();
            Layout(graph,random);
            return graph;
        }

        static double CampWeight(MediaChannel a,MediaChannel b,float grouping)
        {
            return a.camp==b.camp ? 1+grouping*9 : 1-grouping*.98;
        }
        static double LinkProbability(int i,int j,MediaChannel[] graph,int[][] groups,PhaseTwoTuning tuning)
        {
            bool same=graph[i].cluster==graph[j].cluster;
            int peers=same ? groups[graph[i].cluster].Length-1 : graph.Length-groups[graph[i].cluster].Length;
            double original=same ? Math.Min(.8,3.2/Math.Max(1,peers)) : .35/Math.Max(1,peers);
            if(tuning.topicMix==.1f && tuning.meanLinks==3.55f)return original;
            double probability;
            if(tuning.topicMix<=.1f) probability=original*(same ? 1+(.1-tuning.topicMix)/.1*.35/3.2 : tuning.topicMix/.1);
            else probability=original+(3.55/Math.Max(1,graph.Length-1)-original)*(tuning.topicMix-.1)/.9;
            return probability*tuning.meanLinks/3.55;
        }
        static void AdjustAudiences(MediaChannel[] graph,float spread)
        {
            if(graph.Length==0 || Mathf.Approximately(spread,1))return;
            double total=graph.Sum(n=>(double)n.audience);
            if(total<=0)return;
            double mean=total/graph.Length;
            var weights=graph.Select(n=>Math.Pow(n.audience/mean,spread)).ToArray();
            double sum=weights.Sum();long allocated=0;
            var fractions=new double[graph.Length];
            for(int i=0;i<graph.Length;i++) {
                double value=weights[i]/sum*total;
                graph[i].audience=(int)Math.Floor(value);allocated+=graph[i].audience;
                fractions[i]=value-graph[i].audience;
            }
            int remaining=(int)(total-allocated);
            foreach(int i in Enumerable.Range(0,graph.Length).OrderByDescending(i=>fractions[i]).Take(remaining)) graph[i].audience++;
        }

        static void Layout(MediaChannel[] graph,System.Random random)
        {
            int count=graph.Length;
            Width=Mathf.Max(948,Mathf.Sqrt(count*8400f*948/480));
            Height=Mathf.Max(480,Width*480/948);
            var positions=new Vector2[count];var forces=new Vector2[count];
            var center=new Vector2(Width/2,Height/2);
            for(int i=0;i<count;i++) {
                float angle=(float)random.NextDouble()*Mathf.PI*2;
                float radius=Mathf.Sqrt((float)random.NextDouble())*.42f;
                positions[i]=center+new Vector2(Mathf.Cos(angle)*Width,Mathf.Sin(angle)*Height)*radius;
            }
            // Force-directed layout: edges attract, all nodes repel. No topic centers or fixed cells.
            const int iterations=480;
            for(int step=0;step<iterations;step++) {
                for(int i=0;i<count;i++) forces[i]=(center-positions[i])*.025f;
                for(int i=0;i<count;i++) for(int j=i+1;j<count;j++) {
                    var delta=positions[i]-positions[j];float distance=Mathf.Max(.1f,delta.magnitude);
                    var direction=distance>.11f ? delta/distance : Vector2.right;
                    float clearance=Radius(graph[i].audience)+Radius(graph[j].audience)+34;
                    float push=360/distance+Mathf.Max(0,clearance-distance)*1.2f;
                    var force=direction*push;forces[i]+=force;forces[j]-=force;
                }
                for(int i=0;i<count;i++) foreach(int j in graph[i].neighbors) {
                    if(j<=i)continue;
                    var delta=positions[j]-positions[i];float distance=Mathf.Max(.1f,delta.magnitude);
                    var force=delta/distance*(distance-46)*.24f;
                    forces[i]+=force;forces[j]-=force;
                }
                float temperature=12*(1-step/(float)iterations)+.15f;
                for(int i=0;i<count;i++) {
                    positions[i]+=Vector2.ClampMagnitude(forces[i],temperature);
                }
            }
            if(count==0)return;
            float minX=positions.Min(p=>p.x),minY=positions.Min(p=>p.y);
            float spanX=positions.Max(p=>p.x)-minX,spanY=positions.Max(p=>p.y)-minY;
            var scale=new Vector2((Width-100)/Mathf.Max(1,spanX),(Height-110)/Mathf.Max(1,spanY));
            for(int i=0;i<count;i++) positions[i]=Vector2.Scale(positions[i]-new Vector2(minX,minY),scale);
            // Resolve remaining badge collisions after relaxation, without moving nodes into a grid.
            for(int step=0;step<Mathf.Clamp(count*2,120,1000);step++) {
                float penetration=0;
                for(int i=0;i<count;i++) for(int j=i+1;j<count;j++) {
                var delta=positions[j]-positions[i];float distance=delta.magnitude;
                float clearance=Radius(graph[i].audience)+Radius(graph[j].audience)+34;
                if(distance>=clearance)continue;
                penetration=Mathf.Max(penetration,clearance-distance);
                var shift=(distance>.001f ? delta/distance : Vector2.right)*(clearance-distance)*.51f;
                positions[i]-=shift;positions[j]+=shift;
                }
                if(penetration<.05f)break;
            }
            minX=positions.Min(p=>p.x);minY=positions.Min(p=>p.y);
            spanX=positions.Max(p=>p.x)-minX;spanY=positions.Max(p=>p.y)-minY;
            Width=Mathf.Max(Width,spanX+84);Height=Mathf.Max(Height,spanY+96);
            var offset=new Vector2((Width-spanX)/2-minX,(Height-spanY)/2-minY);
            for(int i=0;i<count;i++) {graph[i].x=positions[i].x+offset.x;graph[i].y=positions[i].y+offset.y;}
        }
    }
}
