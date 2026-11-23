using UnityEngine;
namespace Overtone
{
    [CreateAssetMenu(menuName="Overtone/Prototype settings")]
    public sealed class PrototypeSettings : ScriptableObject
    {
        public int folderTarget = 12;
        public int startingLives = 3;
        public float sortingSeconds = 30f;
        [Range(.2f,1f)] public float crossClusterMultiplier = .85f;
        public float waveDelay = 0.65f;
        [Range(.12f,1.2f)] public float checkDuration = 0.4f;
        public int[] viewTargets = {50000, 55000, 60000, 65000, 70000, 75000, 80000, 85000, 90000, 95000};
    }
    public static class Palette
    {
        static Color Hex(string value) { ColorUtility.TryParseHtmlString(value, out var c); return c; }
        public static readonly Color Background=Hex("#182A2B"), Panel=Hex("#243B3C"), Raised=Hex("#30494A"),
            Paper=Hex("#EEE5D1"), Ink=Hex("#223436"), Muted=Hex("#AFBDB4"), Accent=Hex("#D8B36A"),
            Good=Hex("#8FB89F"), Bad=Hex("#DB8479"), Stamp=Hex("#8D3D36"), Opp=Hex("#91B1D1"), Link=Hex("#8CA59D");
        public static Color CampColor(int camp) { return camp==0 ? Accent : camp==1 ? Opp : Good; }
        public static string CampName(int camp) { return camp==0 ? "Провластный" : camp==1 ? "Оппозиционный" : "Нейтральный"; }
    }
}
