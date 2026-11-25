#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using LuluInBeat.MouseMode.ChartSystem;

namespace LuluInBeat.MouseMode.ChartSystem.EditorTools
{
    // AudioUtil can preview a single clip. Mix the existing game hitsounds
    // into a temporary PCM clip at exact sample offsets instead of trying
    // to start independent sounds from Editor update callbacks.
    // No imported AudioClip or chart asset is modified.
    internal static class RuruChartPreviewMix
    {
        internal struct Cue
        {
            public double seconds;
            public int kind; // 0 tap, 1 wheel start, 2 wheel tick, 3 wheel end
            public Cue(double t,int k){seconds=t;kind=k;}
        }

        internal static List<Cue> BuildCues(MouseModeChartData data)
        {
            var cues=new List<Cue>();
            foreach(var note in data.notes)
            {
                if(note==null)continue;
                double start=MouseModeChartTiming.BeatToSeconds(data,note.beat);
                bool wheel=note.kind==MouseModeNoteKind.Wheel;
                cues.Add(new Cue(start,wheel?1:0));
                if(!wheel||note.endBeat<=note.beat)continue;
                double end=MouseModeChartTiming.BeatToSeconds(data,note.endBeat);
                double last=start;
                // A musical half-beat scroll rehearsal, not actual player
                // input; keep the runtime's 90ms minimum sound interval.
                for(float beat=note.beat+.5f;beat<note.endBeat-.0001f;beat+=.5f)
                {
                    double t=MouseModeChartTiming.BeatToSeconds(data,beat);
                    if(t-last<.09||end-t<.09)continue;
                    cues.Add(new Cue(t,2));last=t;
                }
                cues.Add(new Cue(end,3));
            }
            cues.Sort((a,b)=>a.seconds.CompareTo(b.seconds));
            return cues;
        }

        internal static AudioClip Create(AudioClip music,MouseModeChartData data,float volume)
        {
            var palette=Resources.Load<MouseNoteAudioPalette>("MouseNoteAudioPalette");
            if(palette==null)throw new InvalidOperationException("게임 타격음 에셋이 없습니다.");
            var clips=new[]{palette.normalPerfect,palette.longStartPerfect,palette.holdTick,palette.longReleasePerfect};
            var samples=new float[clips.Length][];
            for(int i=0;i<clips.Length;i++)
            {
                if(clips[i]==null)throw new InvalidOperationException("게임 타격음이 연결되지 않았습니다: "+i);
                samples[i]=Read(clips[i]);
            }
            var pcm=Read(music);
            int rate=music.frequency,channels=music.channels;
            float gain=Mathf.Clamp01(volume);
            foreach(var cue in BuildCues(data))
            {
                int index=cue.kind;
                var clip=clips[index];
                var src=samples[index];
                long start=(long)Math.Round(cue.seconds*rate);
                int length=(int)Math.Ceiling(clip.samples*(double)rate/clip.frequency);
                // Preserve original game hold/normal relative loudness.
                float cueGain=gain*(index==2?.4f/.7f:1f);
                for(int frame=Math.Max(0,(int)Math.Min(int.MaxValue,-Math.Min(0L,start)));frame<length&&start+frame<music.samples;frame++)
                {
                    long dest=start+frame;
                    if(dest<0)continue;
                    double at=frame*(double)clip.frequency/rate;
                    int lo=Math.Min(clip.samples-1,(int)at);
                    int hi=Math.Min(clip.samples-1,lo+1);
                    float fraction=(float)(at-lo);
                    for(int ch=0;ch<channels;ch++)
                    {
                        float value=0;
                        if(channels==1&&clip.channels>1)
                        {
                            for(int c=0;c<clip.channels;c++)value+=Mathf.Lerp(src[lo*clip.channels+c],src[hi*clip.channels+c],fraction);
                            value/=clip.channels;
                        }
                        else
                        {
                            int c=Math.Min(ch,clip.channels-1);
                            value=Mathf.Lerp(src[lo*clip.channels+c],src[hi*clip.channels+c],fraction);
                        }
                        pcm[(int)(dest*channels+ch)]+=value*cueGain;
                    }
                }
            }
            // Global headroom preserves the music/hit ratio and avoids
            // hard clipping when a loud hit lands on a music transient.
            float peak=1f;
            foreach(float value in pcm)peak=Mathf.Max(peak,Mathf.Abs(value));
            if(peak>1f)for(int i=0;i<pcm.Length;i++)pcm[i]/=peak;
            var result=AudioClip.Create(music.name+" (채보 타격음 미리듣기)",music.samples,channels,rate,false);
            result.hideFlags=HideFlags.HideAndDontSave;
            if(!result.SetData(pcm,0))
            {
                UnityEngine.Object.DestroyImmediate(result);
                throw new InvalidOperationException("미리듣기 음원 생성 실패");
            }
            return result;
        }

        private static float[] Read(AudioClip clip)
        {
            if(clip.loadState!=AudioDataLoadState.Loaded)clip.LoadAudioData();
            if(clip.loadState!=AudioDataLoadState.Loaded)
                throw new InvalidOperationException(clip.name+" 음원 로딩 중입니다. 다시 재생해 주세요.");
            var data=new float[checked(clip.samples*clip.channels)];
            if(!clip.GetData(data,0))
                throw new InvalidOperationException(clip.name+"의 Audio Import Settings에서 Load Type을 Decompress On Load로 설정해 주세요.");
            return data;
        }
    }
}
#endif
