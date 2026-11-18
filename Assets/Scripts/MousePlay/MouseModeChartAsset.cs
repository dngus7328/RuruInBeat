using UnityEngine;

namespace LuluInBeat.MouseMode.ChartSystem
{
    [CreateAssetMenu(fileName = "NewMouseChart",
        menuName = "Lulu In Beat/Mouse Mode/Chart")]
    public class MouseModeChartAsset : ScriptableObject
    {
        [Header("Song Information")]
        public string displayTitle = "New Song";
        public string artist = "";

        [Header("Song Audio")]
        public AudioClip song;

        [Header("Song-Specific PlayArea Background")]
        public Sprite backgroundImage;

        [Header("One Chart for This Song")]
        public MouseModeChartData data = new MouseModeChartData();
    }
}
