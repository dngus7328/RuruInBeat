using System;
using System.Collections.Generic;

/// <summary>
/// All interface-only copy for the song-selection screen lives here.
/// Original chart titles and audio IDs remain unchanged. Song titles are
/// translated for display only; to add a language extend the title table.
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
            { "searchPlaceholder", new[] { "곡명으로 찾기...", "SEARCH SONG TITLE...", "曲名で検索..." } },
            { "filterAll", new[] { "전체", "ALL", "すべて" } },
            { "filterLow", new[] { "1~5", "1–5", "1～5" } },
            { "filterMid", new[] { "6~10", "6–10", "6～10" } },
            { "filterHigh", new[] { "11~15", "11–15", "11～15" } },
            { "filter", new[] { "필터", "FILTER", "絞り込み" } },
            { "filterDone", new[] { "완료", "DONE", "完了" } },
            { "filterMatches", new[] { "검색 결과", "RESULTS", "検索結果" } },
            { "filterLevelHeading", new[] { "레벨", "LEVEL", "レベル" } },
            { "filterBpmHeading", new[] { "BPM", "BPM", "BPM" } },
            { "filterBpmAll", new[] { "전체", "ALL", "すべて" } },
            { "filterBpmLow", new[] { "129 이하", "UP TO 129", "129以下" } },
            { "filterBpmMid", new[] { "130~169", "130–169", "130～169" } },
            { "filterBpmHigh", new[] { "170 이상", "170+", "170以上" } },
            { "filterReset", new[] { "초기화", "RESET", "リセット" } },
            { "filterClose", new[] { "닫기", "CLOSE", "閉じる" } },
            { "noMatches", new[] { "검색 결과가 없어요", "NO MATCHING SONGS", "該当する楽曲はありません" } },
            { "again", new[] { "한 번 더 누르면 게임 시작", "CLICK AGAIN TO START", "もう一度クリックしてスタート" } },
            { "inserting", new[] { "게임으로 들어가는 중...", "LOADING GAME...", "ゲームを起動中..." } },
            { "inserted", new[] { "선택한 카트리지", "SELECTED CARTRIDGE", "選択中のカートリッジ" } },
            { "track", new[] { "곡 정보", "TRACK INFO", "楽曲情報" } },
            { "difficulty", new[] { "난이도", "DIFFICULTY", "難易度" } },
            { "listen", new[] { "미리듣기", "PREVIEW", "試聴" } },
            { "previewMode", new[] { "미리듣기", "PREVIEW", "試聴" } },
            { "previewOn", new[] { "ON", "ON", "ON" } },
            { "previewOff", new[] { "OFF", "OFF", "OFF" } },
            { "previewOffStatus", new[] { "자동 미리듣기 꺼짐", "AUTO PREVIEW OFF", "自動試聴オフ" } },
            { "back", new[] { "돌아가기", "BACK", "戻る" } },
            { "play", new[] { "게임 시작", "START GAME", "ゲーム開始" } },
            { "mouseHint", new[] { "클릭: 선택 · 재클릭: 시작 · 휠: 곡 변경", "CLICK: SELECT · AGAIN: PLAY · WHEEL: BROWSE", "クリック: 選択 · 再クリック: 開始 · ホイール: 曲変更" } },
            { "noJacket", new[] { "재킷 이미지 준비 중", "JACKET ART COMING SOON", "ジャケット画像は準備中" } },
            { "defaultArtist", new[] { "루루 / 리듬 라이브러리", "LULU / RHYTHM LIBRARY", "ルル / リズムライブラリー" } },
            { "sortTitle", new[] { "곡명", "TITLE", "曲名" } },
            { "sortLevel", new[] { "레벨", "LEVEL", "レベル" } },
            { "sortBpm", new[] { "BPM", "BPM", "BPM" } },
            { "bpm", new[] { "BPM", "BPM", "BPM" } },
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

    // Only a visual alias for the six built-in demo songs.
    // No asset filename, song ID, clip reference or original title is changed.
    private static readonly Dictionary<string, string[]> SongTitles =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "BOOTBEAT", new[] { "시작의 박동", "BOOTBEAT", "ブートビート" } },
            { "Cotton Pulse", new[] { "솜결의 맥박", "Cotton Pulse", "コットンパルス" } },
            { "Full-Band Hit", new[] { "합주의 일격", "Full-Band Hit", "フルバンドヒット" } },
            { "Oddbeat Shuffle", new[] { "엇박자의 춤", "Oddbeat Shuffle", "オッドビート・シャッフル" } },
            { "Pop'n Dash", new[] { "톡톡 튀는 질주", "Pop'n Dash", "ポップン・ダッシュ" } },
            { "瞬奏 -SHUNSOU-", new[] { "찰나의 연주", "Shunsou", "瞬奏 -SHUNSOU-" } }
        };

    public static string DisplaySongTitle(
        SongSelectLanguage language, string originalTitle)
    {
        if (string.IsNullOrWhiteSpace(originalTitle)) return originalTitle;
        string[] translations;
        if (!SongTitles.TryGetValue(originalTitle.Trim(), out translations))
            return originalTitle;
        int index = (int)language;
        return translations[index >= 0 && index < translations.Length
            ? index : 0];
    }

    public static bool SongTitleMatches(string originalTitle, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        if (string.IsNullOrEmpty(originalTitle)) return false;
        string search = query.Trim();
        if (originalTitle.IndexOf(search,
            StringComparison.OrdinalIgnoreCase) >= 0) return true;
        string[] aliases;
        if (!SongTitles.TryGetValue(originalTitle.Trim(), out aliases))
            return false;
        foreach (string name in aliases)
            if (name.IndexOf(search,
                StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

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
