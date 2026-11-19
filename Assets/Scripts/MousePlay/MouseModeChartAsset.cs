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

        [Header("Song Selection")]
        [Tooltip("Hide old draft/test charts from the song selection screen without deleting them.")]
        public bool hideFromSongSelect = false;

        [Header("Song Audio")]
        public AudioClip song;

        [Header("Song-Specific PlayArea Background")]
        public Sprite backgroundImage;

        [Header("Song Select Jacket (optional; replace whenever art is ready)")]
        [Tooltip("Only used by song select. Does not change gameplay background or UI layout.")]
        public Sprite songSelectJacket;

        [Header("One Chart for This Song")]
        public MouseModeChartData data = new MouseModeChartData();
    }
}
