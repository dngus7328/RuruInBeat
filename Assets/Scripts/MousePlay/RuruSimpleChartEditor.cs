#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using LuluInBeat.MouseMode.ChartSystem;

namespace LuluInBeat.MouseMode.ChartSystem.EditorTools
{
    // A note-only workspace. Attack events are never mutated here.
    public sealed class RuruSimpleChartEditor : EditorWindow
    {
        private enum Tool { Select, Left, Right, Wheel }
        private static readonly string[] Tools = {"선택 / 이동", "L 좌클릭", "R 우클릭", "W 휠"};
        private static readonly string[] SnapNames={"1박","1/2박","1/4박","1/8박","1/3박","1/6박","1/12박"};
        private static readonly float[] Snaps={1f,.5f,.25f,.125f,1f/3f,1f/6f,1f/12f};
        private static readonly Color[] Colors={new Color(.75f,.57f,.93f),new Color(.39f,.77f,.87f),new Color(.94f,.75f,.39f)};
        private static readonly Color Background=new Color(.13f,.13f,.17f);
        [SerializeField] private MouseModeChartAsset chart;
        [SerializeField] private float beat, firstBeat, zoom=45f;
        [SerializeField] private int snapIndex=2;
        [SerializeField] private Tool tool=Tool.Left;
        [SerializeField] private bool advance=true, showBackground=true;
        [SerializeField] private float wheelLength=2f;
        private int selectedId=-1, dragId=-1, dragPoint=-1;
        private bool draggingStage, draggingTime, draggingEnd, drawingWheel, playing;
        private List<MouseModeChartAsset> catalog;
        private bool showDrafts;
        [SerializeField] private bool previewHitsounds = true;
        [SerializeField, Range(0f,1f)] private float hitsoundVolume = .7f;
        private AudioClip mixedPreview, activePreview;
        private AudioClip mixedSource;
        private string mixedNotes;
        private float mixedVolume;
        private float playFrom;
        private double playStarted;
        private AudioClip waveClip;
        private float[] wave;
        private Vector2 sideScroll;
        private string notice="";
        private double noticeUntil;
        private MouseModeChartData Data=>chart!=null?chart.data:null;
        private float Snap=>Snaps[Mathf.Clamp(snapIndex,0,Snaps.Length-1)];
        private MouseModeChartNote Selected=>Data?.notes.Find(n=>n!=null&&n.id==selectedId);
        private float EndBeat=>chart!=null&&chart.song!=null?
            Mathf.Max(1,(float)MouseModeChartTiming.SecondsToBeat(Data,chart.song.length)):
            Mathf.Max(64,Data!=null&&Data.notes.Count>0?Data.notes[Data.notes.Count-1].beat+16:64);

        [MenuItem("Tools/Lulu In Beat/쉬운 채보 편집기",false,0)]
        public static void Open(){OpenChart(null);}
        public static void OpenChart(MouseModeChartAsset asset)
        {
            var w=GetWindow<RuruSimpleChartEditor>("루루 채보 제작");
            w.minSize=new Vector2(900,600);
            if(asset!=null)w.SelectChart(asset);
            w.Show();
        }
        private void OnEnable()
        {
            EditorApplication.update+=Tick;
            Undo.undoRedoPerformed+=Repaint;
            EditorApplication.projectChanged+=RefreshCatalog;
            if(chart==null){
                string guid=EditorPrefs.GetString("Ruru.SimpleChart.Last","");
                if(!string.IsNullOrEmpty(guid))chart=AssetDatabase.LoadAssetAtPath<MouseModeChartAsset>(AssetDatabase.GUIDToAssetPath(guid));
            }
        }
        private void OnDisable(){Stop();ReleaseMix();EditorApplication.update-=Tick;Undo.undoRedoPerformed-=Repaint;EditorApplication.projectChanged-=RefreshCatalog;}
        private void RefreshCatalog(){catalog=null;Repaint();}
        private void OnLostFocus(){draggingEnd=draggingTime=draggingStage=drawingWheel=false;}
        private void SelectChart(MouseModeChartAsset value)
        {
            Stop();ReleaseMix();chart=value;selectedId=-1;beat=firstBeat=0;waveClip=null;wave=null;
            if(chart!=null){
                if(chart.data==null)chart.data=new MouseModeChartData();
                chart.data.EnsureLists();
                EditorPrefs.SetString("Ruru.SimpleChart.Last",AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(chart)));
            }
        }
        private void Notify(string message){notice=message;noticeUntil=EditorApplication.timeSinceStartup+3;Repaint();}
        private float Quantize(float b)=>Mathf.Clamp(Mathf.Round(b/Snap)*Snap,0,EndBeat);
        private void SetBeat(float b,bool snap=true)
        {
            bool resume=playing;Stop();beat=snap?Quantize(b):Mathf.Clamp(b,0,EndBeat);
            if(resume)Play();
        }
        private void Play()
        {
            if(chart==null||chart.song==null){Notify("먼저 음악을 연결하세요.");return;}
            playFrom=Mathf.Clamp((float)MouseModeChartTiming.BeatToSeconds(Data,beat),0,Mathf.Max(0,chart.song.length-.02f));
            activePreview=chart.song;
            if(previewHitsounds&&hitsoundVolume>0f)
            {
                try { activePreview=GetMixedPreview(); }
                catch(Exception ex){ReleaseMix();Notify("타격음 미리듣기 실패: "+ex.Message);return;}
            }
            if(!MouseModeEditorAudioPreview.Play(activePreview,playFrom)){Notify("음악 미리듣기를 시작할 수 없어요.");return;}
            playStarted=EditorApplication.timeSinceStartup;playing=true;
        }
        private void Stop(){if(playing)MouseModeEditorAudioPreview.Stop();playing=false;activePreview=null;}
        private void ReleaseMix(){if(mixedPreview!=null)DestroyImmediate(mixedPreview);mixedPreview=null;mixedSource=null;mixedNotes=null;}
        private AudioClip GetMixedPreview()
        {
            string signature=JsonUtility.ToJson(Data);
            if(mixedPreview!=null&&mixedSource==chart.song&&mixedNotes==signature&&Mathf.Approximately(mixedVolume,hitsoundVolume))return mixedPreview;
            ReleaseMix();
            mixedPreview=RuruChartPreviewMix.Create(chart.song,Data,hitsoundVolume);
            mixedSource=chart.song;mixedNotes=signature;mixedVolume=hitsoundVolume;
            return mixedPreview;
        }
        private void Tick()
        {
            if(!playing||chart==null||chart.song==null)return;
            float t=MouseModeEditorAudioPreview.CurrentSeconds(activePreview!=null?activePreview:chart.song);
            float fallback=playFrom+(float)(EditorApplication.timeSinceStartup-playStarted);
            if(t<playFrom-.1f||Mathf.Abs(t-fallback)>1f)t=fallback;
            beat=Mathf.Clamp((float)MouseModeChartTiming.SecondsToBeat(Data,t),0,EndBeat);
            float visible=Mathf.Max(1,(position.width-80)/zoom);
            if(beat<firstBeat||beat>firstBeat+visible*.87f)firstBeat=Mathf.Max(0,beat-visible*.15f);
            if(t>=chart.song.length-.02f)Stop();
            Repaint();
        }
        private void OnGUI()
        {
            DrawTop();
            if(chart==null||Data==null){
                GUILayout.FlexibleSpace();GUILayout.Label("위에서 곡을 선택하거나 새 채보를 만들어 주세요.",EditorStyles.centeredGreyMiniLabel);GUILayout.FlexibleSpace();return;
            }
            if(Data.notes==null)Data.notes=new List<MouseModeChartNote>();
            using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                Shortcuts(Event.current);
                DrawTransport();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                tool=(Tool)GUILayout.Toolbar((int)tool,Tools,GUILayout.Height(32));
                Rect board=GUILayoutUtility.GetRect(200,Mathf.Max(240,position.height-322),GUILayout.ExpandWidth(true));
                DrawBoard(board);
                EditorGUILayout.EndVertical();
                EditorGUILayout.BeginVertical(GUILayout.Width(232));
                sideScroll=EditorGUILayout.BeginScrollView(sideScroll);
                DrawDetails();
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
                DrawTimeline(GUILayoutUtility.GetRect(200,150,GUILayout.ExpandWidth(true)));
            }
            EditorGUILayout.LabelField(EditorApplication.timeSinceStartup<noticeUntil?notice:
                "Space 재생/정지  |  1~4 도구  |  ← → 박자 이동  |  Delete 삭제  |  Ctrl+S 저장",EditorStyles.miniLabel);
        }
        private void DrawTop()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if(catalog==null){
                catalog=new List<MouseModeChartAsset>();
                foreach(string guid in AssetDatabase.FindAssets("t:MouseModeChartAsset")){
                    var a=AssetDatabase.LoadAssetAtPath<MouseModeChartAsset>(AssetDatabase.GUIDToAssetPath(guid));
                    if(a!=null)catalog.Add(a);
                }
            }
            showDrafts=GUILayout.Toggle(showDrafts,"초안 포함",EditorStyles.toolbarButton,GUILayout.Width(72));
            var assets=catalog.FindAll(a=>a!=null&&(showDrafts||(!a.hideFromSongSelect&&a.song!=null)));
            if(chart!=null&&!assets.Contains(chart))assets.Add(chart);
            assets.Sort((a,b)=>string.Compare(a.displayTitle,b.displayTitle,StringComparison.CurrentCulture));
            var labels=new List<string>{"곡 선택..."};
            foreach(var a in assets)labels.Add((a.hideFromSongSelect?"[초안] ":"")+(string.IsNullOrEmpty(a.displayTitle)?a.name:a.displayTitle));
            int current=chart==null?0:assets.IndexOf(chart)+1;
            int next=EditorGUILayout.Popup(current,labels.ToArray(),GUILayout.MinWidth(130));
            if(next!=current)SelectChart(next==0?null:assets[next-1]);
            if(GUILayout.Button("새 채보",EditorStyles.toolbarButton,GUILayout.Width(65)))NewChart();
            using(new EditorGUI.DisabledScope(chart==null||EditorApplication.isPlayingOrWillChangePlaymode)){
                if(GUILayout.Button("저장",EditorStyles.toolbarButton,GUILayout.Width(65)))Save();
                if(GUILayout.Button("게임 테스트",EditorStyles.toolbarButton,GUILayout.Width(90)))TestGame();
            }
            EditorGUILayout.EndHorizontal();
        }
        private void DrawTransport()
        {
            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button(playing?"■ 정지":"▶ 음악 재생",GUILayout.Width(100),GUILayout.Height(28))){if(playing)Stop();else Play();}
            if(GUILayout.Button("◀",GUILayout.Width(32)))SetBeat(beat-Snap);
            if(GUILayout.Button("▶",GUILayout.Width(32)))SetBeat(beat+Snap);
            float b=EditorGUILayout.DelayedFloatField(beat,GUILayout.Width(65));
            if(!Mathf.Approximately(b,beat))SetBeat(b,false);
            GUILayout.Label("박",GUILayout.Width(19));
            GUILayout.Label(Mathf.Max(0,(float)MouseModeChartTiming.BeatToSeconds(Data,beat)).ToString("0.00")+"초",GUILayout.Width(70));
            GUILayout.FlexibleSpace();
            GUILayout.Label("눈금",GUILayout.Width(32));snapIndex=EditorGUILayout.Popup(snapIndex,SnapNames,GUILayout.Width(74));
            advance=GUILayout.Toggle(advance,"자동 다음 박자",GUILayout.Width(115));
            EditorGUILayout.EndHorizontal();
        }
        private void DrawDetails()
        {
            GUILayout.Label("곡 설정",EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var song=(AudioClip)EditorGUILayout.ObjectField("음악",chart.song,typeof(AudioClip),false);
            string title=EditorGUILayout.TextField("곡명",chart.displayTitle);
            float bpm=EditorGUILayout.DelayedFloatField("BPM",Data.bpm);
            float offset=EditorGUILayout.DelayedFloatField("시작 오프셋(초)",Data.beatZeroSeconds);
            int level=EditorGUILayout.IntSlider("레벨",Data.level,1,15);
            if(EditorGUI.EndChangeCheck()){
                Undo.RecordObject(chart,"Song settings");Stop();chart.song=song;chart.displayTitle=title;Data.bpm=Mathf.Max(1,bpm);Data.beatZeroSeconds=offset;Data.level=level;Dirty();
            }
            if(Data.tempoChanges!=null&&Data.tempoChanges.Count>0)EditorGUILayout.HelpBox("기존 BPM 변화 "+Data.tempoChanges.Count+"개 유지 중",MessageType.None);
            if(chart.hideFromSongSelect)EditorGUILayout.HelpBox("초안입니다. 게임 테스트는 가능하며 곡 목록에는 표시하지 않습니다.",MessageType.None);
            showBackground=EditorGUILayout.Toggle("배경 보기",showBackground);
            GUILayout.Space(8);
            GUILayout.Label("미리듣기 소리",EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            bool nextHits=EditorGUILayout.Toggle("타격음 재생",previewHitsounds);
            float nextVolume=EditorGUILayout.Slider("타격음 음량",hitsoundVolume,0,1);
            if(EditorGUI.EndChangeCheck())
            {
                bool resume=playing;Stop();
                previewHitsounds=nextHits;hitsoundVolume=nextVolume;
                if(resume)Play();
            }
            GUILayout.Label("판정 시각에 PERFECT 소리 · 휠은 반 박마다 유지음",EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(10);
            GUILayout.Label("노트 배치",EditorStyles.boldLabel);
            GUILayout.Label("화면을 클릭하면 현재 박자에 배치됩니다.",EditorStyles.wordWrappedMiniLabel);
            wheelLength=Mathf.Max(Snap,EditorGUILayout.FloatField("기본 휠 길이(박)",wheelLength));
            var n=Selected;
            if(n!=null){
                GUILayout.Space(8);GUILayout.Label("선택한 노트 #"+n.id,EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                var kind=(MouseModeNoteKind)EditorGUILayout.EnumPopup("종류",n.kind);
                float at=EditorGUILayout.DelayedFloatField("시작 박자",n.beat);
                float oldEnd=n.endBeat;
                float end=kind==MouseModeNoteKind.Wheel?EditorGUILayout.DelayedFloatField("끝 박자",oldEnd):oldEnd;
                if(EditorGUI.EndChangeCheck()){
                    Undo.RecordObject(chart,"Edit note");float delta=Mathf.Max(0,at)-n.beat;n.beat=Mathf.Max(0,at);
                    n.kind=kind;
                    if(kind==MouseModeNoteKind.Wheel){
                        n.endBeat=Mathf.Max(n.beat+Snap,Mathf.Approximately(end,oldEnd)?end+delta:end);
                        EnsurePath(n);
                    }else{n.endBeat=n.beat;if(n.path!=null)n.path.Clear();}
                    Dirty();
                }
                if(n.kind==MouseModeNoteKind.Wheel){
                    GUILayout.Label("Shift+클릭: 경로점 추가\n경로점 드래그: 위치 수정",EditorStyles.wordWrappedMiniLabel);
                    if(GUILayout.Button("마지막 경로점 삭제")&&n.path.Count>2){Undo.RecordObject(chart,"Remove path point");n.path.RemoveAt(n.path.Count-1);Dirty();}
                }
                if(GUILayout.Button("선택 노트 복제 (다음 눈금)"))Duplicate();
                if(GUILayout.Button("선택 노트 삭제"))Delete();
            }
            GUILayout.Space(12);GUILayout.Label("노트 "+Data.notes.Count+"개",EditorStyles.boldLabel);
            GUILayout.Label("기존 탄막 "+(Data.attacks?.Count??0)+"개 · 편집하지 않음",EditorStyles.wordWrappedMiniLabel);
            GUILayout.Label("탄막 데이터는 저장해도 변경되지 않아요.",EditorStyles.wordWrappedMiniLabel);
        }
        private void DrawBoard(Rect box)
        {
            EditorGUI.DrawRect(box,Background);
            float w=Mathf.Min(box.width-16,(box.height-12)*1.88f);
            Rect area=new Rect(box.center.x-w*.5f,box.center.y-w/1.88f*.5f,w,w/1.88f);
            EditorGUI.DrawRect(area,new Color(.23f,.21f,.28f));
            if(showBackground&&chart.backgroundImage!=null){
                var sprite=chart.backgroundImage;var t=sprite.texture;var sr=sprite.textureRect;
                GUI.DrawTextureWithTexCoords(area,t,new Rect(sr.x/t.width,sr.y/t.height,sr.width/t.width,sr.height/t.height));
                EditorGUI.DrawRect(area,new Color(.08f,.07f,.12f,.42f));
            }
            float diameter=area.height*(133.4f/977.26f);
            if(Event.current.type==EventType.Repaint){
                Handles.BeginGUI();Handles.color=new Color(1,1,1,.12f);
                for(int i=1;i<4;i++){float x=area.x+area.width*i/4f;float y=area.y+area.height*i/4f;Handles.DrawLine(new Vector3(x,area.y),new Vector3(x,area.yMax));Handles.DrawLine(new Vector3(area.x,y),new Vector3(area.xMax,y));}
                float now=(float)MouseModeChartTiming.BeatToSeconds(Data,beat);
                foreach(var n in Data.notes){
                    if(n==null)continue;
                    float t=(float)MouseModeChartTiming.BeatToSeconds(Data,n.beat);
                    float finish=(float)MouseModeChartTiming.BeatToSeconds(Data,n.kind==MouseModeNoteKind.Wheel?n.endBeat:n.beat);
                    bool active=n.id==selectedId;
                    if(!active&&(t-now>2.2f||now-finish>.13f))continue;
                    Color c=Colors[(int)n.kind];
                    float alpha=active||playing?1f:Mathf.Lerp(.35f,1f,1-Mathf.Clamp01(Mathf.Abs(t-now)/2.2f));
                    c.a=alpha;
                    Vector2 pos=Point(area,n.position);
                    if(n.kind==MouseModeNoteKind.Wheel&&n.path!=null&&n.path.Count>=2){
                        Handles.color=c;
                        for(int k=1;k<n.path.Count;k++)Handles.DrawAAPolyLine(Mathf.Max(3,diameter*.22f),Point(area,n.path[k-1]),Point(area,n.path[k]));
                        if(active&&!playing)foreach(var p in n.path)Handles.DrawSolidDisc(Point(area,p),Vector3.forward,4f);
                        pos=Point(area,playing?MouseModeChartTiming.GetPathPosition(n,Mathf.Clamp01((now-t)/Mathf.Max(.001f,finish-t))):n.path[0]);
                    }
                    Handles.color=new Color(.09f,.08f,.13f,alpha);Handles.DrawSolidDisc(pos,Vector3.forward,diameter*.55f);
                    Handles.color=c;Handles.DrawSolidDisc(pos,Vector3.forward,diameter*.48f);
                    Handles.color=new Color(.12f,.1f,.17f,alpha);Handles.DrawSolidDisc(pos,Vector3.forward,diameter*.32f);
                    if(t>=now){Handles.color=c;Handles.DrawWireDisc(pos,Vector3.forward,diameter*.48f*Mathf.Lerp(1,3.3f,Mathf.Clamp01((t-now)/2.2f)));}
                    if(active){Handles.color=Color.white;Handles.DrawWireDisc(pos,Vector3.forward,diameter*.62f);}
                    var style=new GUIStyle(EditorStyles.boldLabel){alignment=TextAnchor.MiddleCenter,fontSize=Mathf.Max(9,Mathf.RoundToInt(diameter*.33f))};style.normal.textColor=Color.white;
                    GUI.Label(new Rect(pos.x-diameter/2,pos.y-diameter/2,diameter,diameter),new[]{"L","R","W"}[(int)n.kind],style);
                }
                Handles.EndGUI();
            }
            HandleBoard(area,diameter);
        }
        private static Vector2 Point(Rect a,Vector2 p)=>new Vector2(a.x+p.x*a.width,a.y+p.y*a.height);
        private static Vector2 Normal(Rect a,Vector2 p)=>new Vector2(Mathf.Clamp01((p.x-a.x)/a.width),Mathf.Clamp01((p.y-a.y)/a.height));
        private void HandleBoard(Rect a,float diameter)
        {
            var e=Event.current;
            if(playing)return;
            if(e.type==EventType.MouseDown&&a.Contains(e.mousePosition)){
                if(e.button!=0)return;
                Vector2 pos=Normal(a,e.mousePosition);
                if(e.shift&&Selected!=null&&Selected.kind==MouseModeNoteKind.Wheel){
                    Undo.RecordObject(chart,"Add wheel point");EnsurePath(Selected);Selected.path.Add(pos);Dirty();e.Use();return;
                }
                if(tool==Tool.Select){
                    float best=Mathf.Max(12,diameter*.65f);MouseModeChartNote hit=null;int point=-1;
                    foreach(var n in Data.notes){
                        if(n==null||(n.id!=selectedId&&Mathf.Abs(n.beat-beat)>4))continue;
                        if(n.kind==MouseModeNoteKind.Wheel&&n.path!=null){
                            for(int k=0;k<n.path.Count;k++){float dist=Vector2.Distance(Point(a,n.path[k]),e.mousePosition);if(dist<best){best=dist;hit=n;point=k;}}
                        }else{float dist=Vector2.Distance(Point(a,n.position),e.mousePosition);if(dist<best){best=dist;hit=n;point=-1;}}
                    }
                    selectedId=hit?.id??-1;
                    if(hit!=null){Undo.RecordObject(chart,"Move note");dragId=hit.id;dragPoint=point;draggingStage=true;}
                }else{
                    Undo.RecordObject(chart,"Add note");
                    var n=new MouseModeChartNote{id=Data.NextId(),kind=(MouseModeNoteKind)((int)tool-1),beat=Quantize(beat),position=pos};
                    n.endBeat=n.kind==MouseModeNoteKind.Wheel?n.beat+wheelLength:n.beat;
                    if(n.kind==MouseModeNoteKind.Wheel){n.path.Add(pos);n.path.Add(new Vector2(Mathf.Clamp01(pos.x+(pos.x>.8f?-.18f:.18f)),pos.y));drawingWheel=true;dragId=n.id;}
                    Data.notes.Add(n);selectedId=n.id;Dirty();
                    if(!drawingWheel&&advance)beat=Quantize(beat+Snap);
                }
                e.Use();Repaint();
            }else if(e.type==EventType.MouseDrag&&(drawingWheel||draggingStage)){
                var n=Data.notes.Find(x=>x.id==dragId);
                if(n!=null){
                    Vector2 p=Normal(a,e.mousePosition);
                    if(drawingWheel)n.path[n.path.Count-1]=p;
                    else if(dragPoint>=0){n.path[dragPoint]=p;if(dragPoint==0)n.position=p;}
                    else n.position=p;
                    Dirty();
                }
                e.Use();Repaint();
            }else if(e.rawType==EventType.MouseUp&&(drawingWheel||draggingStage)){
                if(drawingWheel&&advance)beat=Quantize(beat+Snap);
                drawingWheel=draggingStage=false;dragId=-1;Repaint();
            }
        }
        private void EnsureWave()
        {
            if(waveClip==chart.song)return;
            waveClip=chart.song;wave=null;
            if(waveClip==null||waveClip.samples<=0||waveClip.channels<=0)return;
            const int bins=4096,block=16384;
            try{
                var peaks=new float[bins];var buffer=new float[block*waveClip.channels];
                for(int start=0;start<waveClip.samples;start+=block){
                    if(!waveClip.GetData(buffer,start))return;
                    int frames=Mathf.Min(block,waveClip.samples-start);
                    for(int i=0;i<frames;i++){int bin=(int)((long)(start+i)*bins/waveClip.samples);for(int c=0;c<waveClip.channels;c++)peaks[bin]=Mathf.Max(peaks[bin],Mathf.Abs(buffer[i*waveClip.channels+c]));}
                }
                wave=peaks;
            }catch(Exception){wave=null;}
        }
        private void DrawTimeline(Rect box)
        {
            EditorGUI.DrawRect(box,Background);
            Rect a=new Rect(box.x+32,box.y+18,box.width-40,box.height-35);
            float span=a.width/zoom;
            float X(float b)=>a.x+(b-firstBeat)*zoom;
            float B(float x)=>firstBeat+(x-a.x)/zoom;
            float[] rows={a.y+57,a.y+77,a.y+97};
            for(int i=0;i<3;i++){EditorGUI.DrawRect(new Rect(a.x,rows[i]-9,a.width,18),new Color(.23f,.22f,.27f));GUI.Label(new Rect(box.x+8,rows[i]-9,20,18),new[]{"L","R","W"}[i]);}
            EnsureWave();
            if(Event.current.type==EventType.Repaint){
                Handles.BeginGUI();
                if(wave!=null){
                    Handles.color=new Color(.63f,.65f,.74f,.75f);
                    for(float x=a.x;x<a.xMax;x+=2){float t=(float)MouseModeChartTiming.BeatToSeconds(Data,B(x));if(t<0||t>=chart.song.length)continue;float v=wave[Mathf.Clamp((int)(t/chart.song.length*wave.Length),0,wave.Length-1)]*17;Handles.DrawLine(new Vector3(x,a.y+23-v),new Vector3(x,a.y+23+v));}
                }
                for(int b=Mathf.CeilToInt(firstBeat);b<=firstBeat+span;b++){
                    float x=X(b);Handles.color=new Color(1,1,1,b%4==0?.33f:.12f);Handles.DrawLine(new Vector3(x,a.y),new Vector3(x,a.yMax));if(b%4==0)GUI.Label(new Rect(x+3,box.y,65,18),b.ToString(),EditorStyles.miniLabel);
                }
                foreach(var n in Data.notes){
                    if(n==null)continue;float x=X(n.beat),end=X(n.kind==MouseModeNoteKind.Wheel?n.endBeat:n.beat);if(end<a.x||x>a.xMax)continue;
                    var c=Colors[(int)n.kind];float y=rows[(int)n.kind];
                    if(n.kind==MouseModeNoteKind.Wheel){
                        EditorGUI.DrawRect(new Rect(Mathf.Max(a.x,x),y-3,Mathf.Max(1,Mathf.Min(a.xMax,end)-Mathf.Max(a.x,x)),6),c);
                        if(end>=a.x&&end<=a.xMax){Handles.color=n.id==selectedId?Color.white:c;Handles.DrawSolidDisc(new Vector2(end,y),Vector3.forward,4);}
                    }
                    if(x>=a.x&&x<=a.xMax){Handles.color=n.id==selectedId?Color.white:c;Handles.DrawSolidDisc(new Vector2(x,y),Vector3.forward,n.id==selectedId?6:4);}
                }
                float cursor=X(beat);if(cursor>=a.x&&cursor<=a.xMax){Handles.color=Color.white;Handles.DrawLine(new Vector3(cursor,a.y),new Vector3(cursor,a.yMax));}
                Handles.EndGUI();
            }
            float view=GUI.HorizontalScrollbar(new Rect(a.x,box.yMax-15,a.width,14),firstBeat,span,0,Mathf.Max(span,EndBeat));
            firstBeat=Mathf.Clamp(view,0,Mathf.Max(0,EndBeat-span));
            var e=Event.current;
            if(e.type==EventType.ScrollWheel&&a.Contains(e.mousePosition)){
                if(e.control||e.command){float anchor=B(e.mousePosition.x);zoom=Mathf.Clamp(zoom*(e.delta.y>0?.85f:1.18f),12,180);firstBeat=Mathf.Max(0,anchor-(e.mousePosition.x-a.x)/zoom);}
                else firstBeat=Mathf.Clamp(firstBeat+e.delta.y*2,0,Mathf.Max(0,EndBeat-span));
                e.Use();Repaint();
            }
            if(e.type==EventType.MouseDown&&e.button==0&&a.Contains(e.mousePosition)){
                MouseModeChartNote hit=null;float best=9;bool tail=false;
                foreach(var n in Data.notes){
                    if(n==null)continue;
                    float y=rows[(int)n.kind];
                    float dist=Vector2.Distance(new Vector2(X(n.beat),y),e.mousePosition);
                    if(dist<best){best=dist;hit=n;tail=false;}
                    if(n.kind==MouseModeNoteKind.Wheel){
                        dist=Vector2.Distance(new Vector2(X(n.endBeat),y),e.mousePosition);
                        if(dist<best){best=dist;hit=n;tail=true;}
                    }
                }
                if(hit!=null){Stop();selectedId=hit.id;beat=tail?hit.endBeat:hit.beat;dragId=hit.id;draggingEnd=tail;draggingTime=true;Undo.RecordObject(chart,"Move note timing");tool=Tool.Select;}
                else SetBeat(B(e.mousePosition.x));
                e.Use();Repaint();
            }else if(e.type==EventType.MouseDrag&&draggingTime){
                var n=Data.notes.Find(x=>x!=null&&x.id==dragId);
                if(n!=null){
                    float next=Quantize(B(e.mousePosition.x));
                    if(draggingEnd){n.endBeat=Mathf.Max(n.beat+Snap,next);beat=n.endBeat;}
                    else{n.endBeat+=next-n.beat;n.beat=next;beat=next;}
                    Dirty();
                }e.Use();Repaint();
            }else if(e.rawType==EventType.MouseUp&&draggingTime){draggingEnd=draggingTime=false;dragId=-1;}
        }
        private void Shortcuts(Event e)
        {
            if(e.type!=EventType.KeyDown||EditorGUIUtility.editingTextField)return;
            bool used=true;
            switch(e.keyCode){
                case KeyCode.Space:if(playing)Stop();else Play();break;
                case KeyCode.Alpha1:tool=Tool.Select;break;
                case KeyCode.Alpha2:tool=Tool.Left;break;
                case KeyCode.Alpha3:tool=Tool.Right;break;
                case KeyCode.Alpha4:tool=Tool.Wheel;break;
                case KeyCode.LeftArrow:SetBeat(beat-Snap);break;
                case KeyCode.RightArrow:SetBeat(beat+Snap);break;
                case KeyCode.Delete:case KeyCode.Backspace:Delete();break;
                case KeyCode.S:if(e.control||e.command)Save();else used=false;break;
                case KeyCode.D:if(e.control||e.command)Duplicate();else used=false;break;
                default:used=false;break;
            }
            if(used){e.Use();Repaint();}
        }
        private static void EnsurePath(MouseModeChartNote n){if(n.path==null)n.path=new List<Vector2>();if(n.path.Count==0)n.path.Add(n.position);if(n.path.Count==1)n.path.Add(new Vector2(Mathf.Clamp01(n.position.x+.12f),n.position.y));}
        private void Dirty(){EditorUtility.SetDirty(chart);}
        private void Delete(){var n=Selected;if(n==null)return;Undo.RecordObject(chart,"Delete note");Data.notes.Remove(n);selectedId=-1;Dirty();}
        private void Duplicate(){var n=Selected;if(n==null)return;Undo.RecordObject(chart,"Duplicate note");var copy=JsonUtility.FromJson<MouseModeChartNote>(JsonUtility.ToJson(n));copy.id=Data.NextId();copy.beat+=Snap;copy.endBeat+=Snap;Data.notes.Add(copy);selectedId=copy.id;beat=copy.beat;Dirty();}
        private void NewChart()
        {
            string p=EditorUtility.SaveFilePanelInProject("새 채보","NewMouseChart","asset","저장할 채보 이름","Assets/MouseMode/ChartSystem/Charts");
            if(string.IsNullOrEmpty(p))return;
            var a=CreateInstance<MouseModeChartAsset>();a.displayTitle=Path.GetFileNameWithoutExtension(p);a.data.songId=a.displayTitle;a.data.difficulty="MOUSE";a.hideFromSongSelect=true;showDrafts=true;catalog=null;
            AssetDatabase.CreateAsset(a,p);AssetDatabase.SaveAssets();SelectChart(a);Notify("새 채보 생성 · 음악을 연결하세요.");
        }
        private void Save()
        {
            if(chart==null)return;
            Data.notes.RemoveAll(n=>n==null);
            Data.notes.Sort((a,b)=>a.beat.CompareTo(b.beat));
            Dirty();AssetDatabase.SaveAssets();
            string p=AssetDatabase.GetAssetPath(chart);
            if(!string.IsNullOrEmpty(p)){string backup=Path.ChangeExtension(p,".autosave.json");File.WriteAllText(backup,JsonUtility.ToJson(Data,true));AssetDatabase.ImportAsset(backup);}
            Notify("채보 저장 완료 · 탄막 보존");
        }
        private void TestGame()
        {
            if(chart==null||chart.song==null){Notify("먼저 음악을 연결하세요.");return;}
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            Save();Stop();
            RuruSimpleChartTestSession.Start(chart);
        }
    }

    [InitializeOnLoad]
    internal static class RuruSimpleChartTestSession
    {
        const string OldScene="Ruru.SimpleChart.Test.OldStart";
        const string Active="Ruru.SimpleChart.Test.Active";
        static RuruSimpleChartTestSession(){EditorApplication.playModeStateChanged+=Changed;}
        internal static void Start(MouseModeChartAsset chart)
        {
            var scene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scene/Mouse.unity");
            if(scene==null)return;
            SessionState.SetString(OldScene,EditorSceneManager.playModeStartScene==null?"":AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetString("Ruru.SimpleChart.Test.Asset",AssetDatabase.GetAssetPath(chart));
            SessionState.SetBool(Active,true);
            EditorSceneManager.playModeStartScene=scene;
            EditorApplication.isPlaying=true;
        }
        private static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Active,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode){
                var type=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");if(type!=null)EditorWindow.GetWindow(type).Focus();
            }else if(state==PlayModeStateChange.EnteredEditMode){
                string previous=SessionState.GetString(OldScene,"");
                EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(previous)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
                SessionState.SetBool(Active,false);SessionState.EraseString("Ruru.SimpleChart.Test.Asset");
            }
        }
    }
}
#endif
