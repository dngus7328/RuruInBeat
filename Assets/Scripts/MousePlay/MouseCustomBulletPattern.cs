using System;
using System.Collections.Generic;
using UnityEngine;

// Store these assets in Assets/Resources/MouseBulletPatterns. The ID is stable
// when the file is renamed. All distances/speeds are in playfield-height units.
[CreateAssetMenu(menuName = "Lulu In Beat/Mouse Mode/Custom Bullet Pattern", fileName = "NewBulletPattern")]
public sealed class MouseCustomBulletPattern : ScriptableObject
{
    [Tooltip("Unique ID. Do not change after using in a chart.")]
    [SerializeField, HideInInspector] private string patternId = "";
    public string displayName = "새 탄막";
    [Tooltip("On: chart beat is first danger crossing. Off: chart beat is firing time.")]
    public bool alignFirstImpact;
    public string Id => "custom_" + patternId;
    [Serializable]
    public sealed class Volley
    {
        [Min(0f)] public float startBeat;
        [Range(1,64)] public int repeat = 3;
        [Min(0f)] public float intervalBeats = 1f;
        public Vector2 origin = new Vector2(.5f,.08f);
        public Vector2 originStep;
        [Range(1,64)] public int bullets = 5;
        [Tooltip("-90 = down, 0 = right, 90 = up, 180 = left.")]
        public float direction = -90f;
        [Range(0f,360f)] public float spread = 60f;
        public float rotationPerRepeat;
        [Min(.001f)] public float speed = .12f;
        public float acceleration;
        public float waveAmplitude;
        [Min(0f)] public float waveCyclesPerBeat;
        public float wavePhaseRadians;
        [Min(0f)] public float turnAfterBeats;
        public float turnAngle;
        [Min(0f)] public float pauseAfterBeats;
        [Min(0f)] public float pauseBeats;
        [Min(.1f)] public float lifetime = 12f;
        [Range(0,3)] public int color;
    }
    [Header("발사 묶음 / 반복 / 부채꼴")]
    public List<Volley> volleys = new List<Volley>{ new Volley() };
    [Header("개별 탄환 (기존 패턴 복제 시 사용)")]
    public List<MouseModeBulletPatternLibrary.Pellet> individualBullets =
        new List<MouseModeBulletPatternLibrary.Pellet>();
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(patternId))
            patternId = Guid.NewGuid().ToString("N");
    }
    public void AssignNewId() { patternId = Guid.NewGuid().ToString("N"); }
    public List<MouseModeBulletPatternLibrary.Pellet> Build()
    {
        var result = new List<MouseModeBulletPatternLibrary.Pellet>();
        if (volleys != null)
            foreach (var v in volleys)
            {
                if (v == null) continue;
                int count = Mathf.Clamp(v.bullets,1,64);
                for (int row=0;row<Mathf.Clamp(v.repeat,1,64);row++)
                    for (int col=0;col<count;col++)
                    {
                        float fraction = count == 1 ? 0f :
                            v.spread >= 359.99f ? col/(float)count-.5f : col/(float)(count-1)-.5f;
                        Vector2 origin = v.origin + row*v.originStep;
                        result.Add(new MouseModeBulletPatternLibrary.Pellet {
                            atBeat=Mathf.Max(0,v.startBeat+row*Mathf.Max(0,v.intervalBeats)),
                            lifeBeats=Mathf.Max(.1f,v.lifetime),
                            origin= new Vector2(Mathf.Clamp01(origin.x),Mathf.Clamp01(origin.y)),
                            angle=v.direction+row*v.rotationPerRepeat+fraction*v.spread,
                            speed=Mathf.Max(.001f,v.speed),acceleration=v.acceleration,
                            wave=v.waveAmplitude,waveCycles=Mathf.Max(0,v.waveCyclesPerBeat),
                            wavePhase=v.wavePhaseRadians,turnBeat=Mathf.Max(0,v.turnAfterBeats),
                            turnDegrees=v.turnAngle,pauseStart=Mathf.Max(0,v.pauseAfterBeats),
                            pauseLength=Mathf.Max(0,v.pauseBeats),color=Mathf.Clamp(v.color,0,3)
                        });
                    }
            }
        if (individualBullets != null) result.AddRange(individualBullets);
        return result;
    }
}
