using System;
using System.Collections.Generic;

/// <summary>
/// All interface-only copy for the song-selection screen lives here.
/// Actual song and artist titles remain unchanged. To add a new language,
/// add one entry in each string array rather than editing scene labels.
/// </summary>
public enum SongSelectLanguage { Korean = 0, English = 1, Japanese = 2 }

public static class SongSelectLocalization
{
    private static readonly Dictionary<string, string[]> Captions =
        new Dictionary<string, string[]>
        {
            { "heading", new[] { "곡 선택", "SELECT A SONG", "楽曲選択" } },
            { "subtitle", new[] { "루루 인 비트 · 음악 선택", "LULU IN BEAT · MUSIC SELECTION", "ルル・イン・ビート · 楽曲選択" } },
            { "list", new[] { "게임 카트리지", "GAME CARTRIDGES", "ゲームカートリッジ" } },
            { "hint", new[] { "카트리지를 눌러 곡 선택", "CLICK A CARTRIDGE TO SELECT", "カートリッジをクリックして選択" } },
            { "again", new[] { "한 번 더 누르면 게임 시작", "CLICK AGAIN TO START", "もう一度クリックしてスタート" } },
            { "inserting", new[] { "게임으로 들어가는 중...", "LOADING GAME...", "ゲームを起動中..." } },
            { "inserted", new[] { "선택한 카트리지", "SELECTED CARTRIDGE", "選択中のカートリッジ" } },
            { "track", new[] { "곡 정보", "TRACK INFO", "楽曲情報" } },
            { "difficulty", new[] { "난이도", "DIFFICULTY", "難易度" } },
            { "listen", new[] { "미리듣기", "PREVIEW", "試聴" } },
            { "back", new[] { "돌아가기", "BACK", "戻る" } },
            { "play", new[] { "게임 시작", "START GAME", "ゲーム開始" } },
            { "mouseHint", new[] { "클릭: 선택 · 재클릭: 시작 · 휠: 곡 변경", "CLICK: SELECT · AGAIN: PLAY · WHEEL: BROWSE", "クリック: 選択 · 再クリック: 開始 · ホイール: 曲変更" } },
            { "noJacket", new[] { "재킷 이미지 준비 중", "JACKET ART COMING SOON", "ジャケット画像は準備中" } },
            { "defaultArtist", new[] { "루루 / 리듬 라이브러리", "LULU / RHYTHM LIBRARY", "ルル / リズムライブラリー" } },
            { "sortTitle", new[] { "곡명", "TITLE", "曲名" } },
            { "sortLevel", new[] { "레벨", "LEVEL", "レベル" } },
            { "sortBpm", new[] { "BPM", "BPM", "BPM" } },
            { "sort", new[] { "정렬", "SORT", "並び替え" } },
            { "count", new[] { "곡", "TRACKS", "曲" } },
            { "rowLevel", new[] { "레벨 ", "LV ", "Lv " } },
            { "level", new[] { "레벨", "LEVEL", "レベル" } },
            { "duration", new[] { "재생 시간", "DURATION", "再生時間" } },
            { "noSongs", new[] { "곡을 찾지 못했어요", "NO SONGS FOUND", "楽曲が見つかりません" } },
            { "noChart", new[] { "채보 없음", "NO CHART", "譜面なし" } },
            { "noCartridge", new[] { "카트리지 없음", "NO CARTRIDGE", "カートリッジなし" } },
            { "cartridge", new[] { "카트리지", "CARTRIDGE", "カートリッジ" } },
            { "unavailable", new[] { "미리듣기 불가", "PREVIEW UNAVAILABLE", "試聴できません" } },
            { "on", new[] { "재생 중", "PLAYING", "再生中" } },
            { "off", new[] { "재생 꺼짐", "PREVIEW OFF", "試聴オフ" } },
            { "seconds", new[] { "초", "s", "秒" } },
            { "mouse", new[] { "마우스", "MOUSE", "マウス" } },
            { "easy", new[] { "쉬움", "EASY", "EASY" } },
            { "normal", new[] { "보통", "NORMAL", "NORMAL" } },
            { "hard", new[] { "어려움", "HARD", "HARD" } },
            { "expert", new[] { "전문가", "EXPERT", "EXPERT" } },
            { "error", new[] { "오류", "ERROR", "ERROR" } }
        };

    public static string Get(SongSelectLanguage language, string key)
    {
        string[] translations;
        if (!Captions.TryGetValue(key, out translations)) return key;
        int index = (int)language;
        return translations[index >= 0 && index < translations.Length
            ? index : 0];
    }

    public static string DifficultyName(SongSelectLanguage language,
        string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Get(language, "mouse");
        string normalized = raw.Trim().Replace("_", " ").ToUpperInvariant();
        string key;
        switch (normalized)
        {
            case "MOUSE": key = "mouse"; break;
            case "EASY": key = "easy"; break;
            case "NORMAL": key = "normal"; break;
            case "HARD": key = "hard"; break;
            case "EXPERT": key = "expert"; break;
            case "ERROR": key = "error"; break;
            default: return raw.Replace("_", " ");
        }
        return Get(language, key);
    }
}
