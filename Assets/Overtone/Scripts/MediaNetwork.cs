using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Overtone
{
    public sealed class MediaCommunity
    {
        public string name,label;
        public Vector2 center;
    }
    /// <summary>Dense topic communities joined by a few single-edge gateways.</summary>
    public static class MediaNetwork
    {
        public static readonly MediaCommunity[] Communities = {
            new MediaCommunity {name="Официальная повестка",label="ВЛАСТЬ",center=new Vector2(140f,140f)},
            new MediaCommunity {name="Оппозиционная среда",label="ОППОЗИЦИЯ",center=new Vector2(860f,140f)},
            new MediaCommunity {name="Нейтральная лента",label="НЕЙТРАЛЫ",center=new Vector2(535f,215f)},
            new MediaCommunity {name="Наука и технологии",label="НАУКА /\nТЕХНО",center=new Vector2(385f,120f)},
            new MediaCommunity {name="Здоровье и психология",label="ЗДОРОВЬЕ",center=new Vector2(825f,345f)},
            new MediaCommunity {name="Дом и природа",label="ДОМ /\nПРИРОДА",center=new Vector2(145f,345f)},
            new MediaCommunity {name="Экономика и работа",label="ЭКОНОМИКА",center=new Vector2(280f,245f)},
            new MediaCommunity {name="Культура и развлечения",label="КУЛЬТУРА",center=new Vector2(705f,235f)},
            new MediaCommunity {name="Образование и общество",label="УЧЁБА /\nОБЩЕСТВО",center=new Vector2(485f,355f)}
        };
        static readonly Vector2[] positions = {
            new Vector2(204.6f,146.2f),
            new Vector2(177.9f,195.2f),
            new Vector2(116.5f,209.2f),
            new Vector2(75.8f,153.9f),
            new Vector2(108.7f,72.1f),
            new Vector2(175.1f,71.7f),
            new Vector2(925.8f,149.9f),
            new Vector2(905.3f,200.5f),
            new Vector2(817.7f,201.5f),
            new Vector2(794.6f,150.9f),
            new Vector2(822.3f,81.1f),
            new Vector2(901.7f,81.0f),
            new Vector2(603.0f,196.4f),
            new Vector2(567.5f,280.9f),
            new Vector2(486.3f,262.2f),
            new Vector2(464.2f,201.3f),
            new Vector2(501.4f,158.2f),
            new Vector2(560.9f,155.4f),
            new Vector2(449.1f,120.4f),
            new Vector2(415.5f,175.2f),
            new Vector2(354.6f,167.6f),
            new Vector2(310.6f,109.1f),
            new Vector2(357.6f,66.1f),
            new Vector2(420.5f,65.2f),
            new Vector2(900.9f,348.5f),
            new Vector2(864.1f,405.5f),
            new Vector2(781.6f,403.0f),
            new Vector2(760.6f,344.0f),
            new Vector2(787.6f,290.1f),
            new Vector2(857.1f,281.4f),
            new Vector2(216.6f,342.8f),
            new Vector2(191.5f,404.2f),
            new Vector2(103.2f,399.6f),
            new Vector2(75.3f,350.1f),
            new Vector2(96.8f,285.3f),
            new Vector2(168.8f,283.8f),
            new Vector2(352.2f,246.8f),
            new Vector2(317.7f,297.2f),
            new Vector2(261.3f,311.5f),
            new Vector2(215.0f,243.0f),
            new Vector2(242.6f,188.7f),
            new Vector2(303.0f,192.6f),
            new Vector2(773.4f,231.3f),
            new Vector2(727.7f,292.1f),
            new Vector2(657.6f,296.1f),
            new Vector2(640.3f,238.9f),
            new Vector2(672.6f,176.1f),
            new Vector2(748.4f,181.9f),
            new Vector2(555.7f,347.6f),
            new Vector2(508.5f,413.7f),
            new Vector2(444.0f,414.9f),
            new Vector2(409.0f,346.9f),
            new Vector2(441.7f,301.1f),
            new Vector2(518.2f,306.6f)
        };
        static readonly string[] names = {"Вестник N","Первый N","Региональный эфир","Новости округа","Столичный обзор","Общий эфир","Другой взгляд","Без купюр","Независимый кадр","Улица онлайн","Факт и мнение","Свободный микрофон","Лента дня","Пульс города","Городской дневник","Ежедневник","Новости без оценок","Общая лента","Научный отдел","Лаборатория","Техноразбор","Понятная наука","Цифровой вестник","Исследование","Здоровый смысл","Просто о жизни","Баланс","Доктор рядом","Психология дня","Пульс здоровья","Дом и сад","Мир рядом","Природный мир","Зелёный двор","Живая планета","Уютный дом","Деловая сводка","Экономика N","Разбор недели","Рынки и планы","Кошелёк","Рабочая среда","Культурный шум","Большой экран","После работы","Новый ракурс","Сцена и время","Афиша N","Классный час","Открытый город","Город говорит","Голос двора","Кампус","Учимся вместе"};
        static readonly string[] topics = {"#общество","#общество","#экономика","#образование","#технологии","#культура","#общество","#экономика","#общество","#психология","#наука","#культура","#общество","#общество","#образование","#здоровье","#технологии","#природа","#наука","#технологии","#технологии","#наука","#технологии","#наука","#здоровье","#психология","#психология","#здоровье","#психология","#здоровье","#дом","#природа","#природа","#дом","#природа","#дом","#экономика","#экономика","#экономика","#экономика","#экономика","#экономика","#культура","#шоубизнес","#культура","#культура","#культура","#шоубизнес","#образование","#общество","#общество","#образование","#образование","#образование"};
        static readonly int[] audiences = {1800,14500,8200,3400,17800,12000,2100,9000,4500,3200,11200,5800,2600,6300,3700,5600,8500,10400,2200,6100,7600,4800,13000,9200,2400,7800,5500,14000,6800,11500,2300,9400,10400,6600,12500,8600,2500,9500,8600,17000,7200,11000,2700,22000,12500,15600,7400,10500,2000,13800,4200,5400,9600,6500};
        static readonly int[,] gateways = {{0,40},{0,21},{8,42},{7,29},{15,19},{14,36},{12,45},{13,53},{20,41},{28,43},{30,38},{30,51},{44,48}};
        public static float Radius(int audience) { return 18+Mathf.Sqrt(audience/22000f)*8; }
        public static MediaChannel[] Create()
        {
            int count=positions.Length;var graph=new MediaChannel[count];var links=new HashSet<int>[count];
            string[] platforms={"Telegram","ВК","Блогер","Telegram","ТВ","ВК"};
            for(int i=0;i<count;i++) {
                int group=i/6,local=i%6;links[i]=new HashSet<int>();
                graph[i]=new MediaChannel {name=names[i],topic=topics[i],platform=platforms[local],audience=audiences[i],camp=group<3 ? group : (group+local)%3,cluster=group,clusterName=Communities[group].name,canPublish=local==0,x=positions[i].x,y=positions[i].y};
            }
            Action<int,int> add=(a,b)=>{links[a].Add(b);links[b].Add(a);};
            // Local triangles and chords keep each six-channel community cohesive.
            for(int i=0;i<count;i++) {
                var candidates=Enumerable.Range(i/6*6,6).Where(j=>j!=i).OrderBy(j=>Vector2.Distance(positions[i],positions[j]));
                foreach(int j in candidates) {
                    if(links[i].Count>=4)break;
                    if(Clear(i,j))add(i,j);
                }
            }
            // One gateway per community pair. No distance-based global links bypass these cuts.
            for(int p=0;p<gateways.GetLength(0);p++)add(gateways[p,0],gateways[p,1]);
            for(int i=0;i<count;i++)graph[i].neighbors=links[i].OrderBy(j=>j).ToArray();
            return graph;
        }
        static bool Clear(int a,int b)
        {
            var delta=positions[b]-positions[a];
            for(int k=0;k<positions.Length;k++) {
                if(k==a||k==b)continue;
                float t=Mathf.Clamp01(Vector2.Dot(positions[k]-positions[a],delta)/delta.sqrMagnitude);
                if(Vector2.Distance(positions[k],positions[a]+delta*t)<Radius(audiences[k])+3)return false;
            }
            return true;
        }
    }
}
