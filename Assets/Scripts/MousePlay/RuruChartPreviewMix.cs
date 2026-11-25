#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using LuluInBeat.MouseMode.ChartSystem;

namespace LuluInBeat.MouseMode.ChartSystem.EditorTools
{
    // AudioUtil can preview a single clip. Mix the existing game hitsounds
    // into an imported PCM WAV at exact sample offsets. AudioUtil's native
    // preview path is designed for imported audio assets, and can be silent
    // for AudioClip.Create objects even when PlayPreviewClip returns normally.
    // The disposable WAV lives in an ignored, Editor-only directory.
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
            return ImportPreview(pcm,channels,rate);
        }

        internal const string CacheFolder="Assets/Editor/RuruPreviewCache";

        internal static AudioClip ImportPreview(float[] pcm,int channels,int rate)
        {
            Directory.CreateDirectory(CacheFolder);
            string path=CacheFolder+"/Preview_"+Guid.NewGuid().ToString("N")+".wav";
            try
            {
                // Standard little-endian 16-bit PCM, not an in-memory clip.
                var values=new short[pcm.Length];
                for(int i=0;i<pcm.Length;i++)
                    values[i]=(short)Mathf.RoundToInt(Mathf.Clamp(pcm[i],-1,1)*32767);
                byte[] bytes=new byte[checked(values.Length*2)];
                Buffer.BlockCopy(values,0,bytes,0,bytes.Length);
                if(!BitConverter.IsLittleEndian)
                    for(int i=0;i<bytes.Length;i+=2){byte v=bytes[i];bytes[i]=bytes[i+1];bytes[i+1]=v;}
                using(var writer=new BinaryWriter(File.Create(path)))
                {
                    writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+bytes.Length);
                    writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);
                    writer.Write((short)1);writer.Write((short)channels);
                    writer.Write(rate);writer.Write(rate*channels*2);
                    writer.Write((short)(channels*2));writer.Write((short)16);
                    writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(bytes.Length);writer.Write(bytes);
                }
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=AssetImporter.GetAtPath(path) as AudioImporter;
                if(importer==null)throw new InvalidOperationException("미리듣기 WAV 가져오기 실패");
                var settings=importer.defaultSampleSettings;
                settings.loadType=AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat=AudioCompressionFormat.PCM;
                settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;
                importer.defaultSampleSettings=settings;
                importer.SaveAndReimport();
                var result=AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if(result==null||result.samples!=pcm.Length/channels||result.frequency!=rate)
                    throw new InvalidOperationException("미리듣기 WAV 정보 불일치");
                result.LoadAudioData();
                return result;
            }
            catch
            {
                if(!AssetDatabase.DeleteAsset(path)&&File.Exists(path))File.Delete(path);
                throw;
            }
        }

        internal static void Release(AudioClip clip)
        {
            if(clip==null)return;
            string path=AssetDatabase.GetAssetPath(clip);
            if(path.StartsWith(CacheFolder+"/",StringComparison.Ordinal))
                AssetDatabase.DeleteAsset(path);
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
