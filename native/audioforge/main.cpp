// ============================================================================
//  WinForge Audio Editor · 音訊編輯器  ("AudioForge")
//  ---------------------------------------------------------------------------
//  A single-file, fully-offline, Audacity-inspired WAV waveform editor.
//  Part of the WinForge suite — bilingual English + 繁體中文（粵語）.
//
//  Build (MSVC):
//    cl /nologo /EHsc /O2 /std:c++17 /DUNICODE /D_UNICODE main.cpp ^
//       /link winmm.lib gdi32.lib user32.lib comdlg32.lib comctl32.lib ^
//       shell32.lib ole32.lib /SUBSYSTEM:WINDOWS
//  Build (MinGW):
//    g++ -std=c++17 -O2 -municode -mwindows -static main.cpp -o WinForgeAudioEditor.exe ^
//       -lwinmm -lgdi32 -lcomdlg32 -lcomctl32 -lshell32 -lole32
//
//  No CDN, no network, no telemetry. Pure Win32 + waveOut. One translation unit.
// ============================================================================

#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#ifndef _WIN32_WINNT
#define _WIN32_WINNT 0x0601
#endif
#ifndef WINVER
#define WINVER 0x0601
#endif

#include <windows.h>
#include <windowsx.h>
#include <mmsystem.h>
#include <commdlg.h>
#include <commctrl.h>
#include <shellapi.h>
#include <objbase.h>

#include <cstdint>
#include <cstdio>
#include <cwchar>
#include <cmath>
#include <cstring>
#include <string>
#include <vector>
#include <deque>
#include <new>
#include <algorithm>

#ifdef _MSC_VER
#pragma comment(lib, "winmm.lib")
#pragma comment(lib, "gdi32.lib")
#pragma comment(lib, "user32.lib")
#pragma comment(lib, "comdlg32.lib")
#pragma comment(lib, "comctl32.lib")
#pragma comment(lib, "shell32.lib")
#pragma comment(lib, "ole32.lib")
#endif

#ifndef WAVE_FORMAT_IEEE_FLOAT
#define WAVE_FORMAT_IEEE_FLOAT 0x0003
#endif
#ifndef WAVE_FORMAT_EXTENSIBLE
#define WAVE_FORMAT_EXTENSIBLE 0xFFFE
#endif

// ============================== command ids ================================

enum : UINT {
    CMD_OPEN = 101, CMD_SAVE, CMD_EXIT,
    CMD_UNDO, CMD_REDO,
    CMD_CUT, CMD_COPY, CMD_PASTE, CMD_DELETE, CMD_TRIM,
    CMD_SELALL, CMD_CLEARSEL,
    CMD_SILENCE, CMD_FADEIN, CMD_FADEOUT, CMD_GAIN, CMD_NORMALIZE, CMD_REVERSE,
    CMD_PLAYPAUSE, CMD_STOP,
    CMD_ZOOMIN, CMD_ZOOMOUT, CMD_ZOOMFIT, CMD_ZOOMSEL,
    CMD_VUP, CMD_VDOWN, CMD_VRESET,
    CMD_LANG_EN, CMD_LANG_ZH, CMD_LANG_BOTH,
    CMD_ABOUT,
    CMD_CURLEFT, CMD_CURRIGHT, CMD_SELLEFT, CMD_SELRIGHT, CMD_HOME, CMD_END
};

enum : UINT_PTR { TIMER_PLAY = 1, TIMER_TOAST = 2 };

// ============================== theme =======================================

static const COLORREF COL_BG       = RGB(18, 20, 26);
static const COLORREF COL_LANE     = RGB(26, 29, 38);
static const COLORREF COL_RULER    = RGB(33, 36, 46);
static const COLORREF COL_GUTTER   = RGB(24, 26, 34);
static const COLORREF COL_GRID     = RGB(52, 56, 70);
static const COLORREF COL_CENTER   = RGB(72, 78, 96);
static const COLORREF COL_WAVE     = RGB(255, 166, 61);   // forge amber
static const COLORREF COL_SEL      = RGB(54, 72, 108);
static const COLORREF COL_SELRULER = RGB(74, 96, 146);
static const COLORREF COL_CURSOR   = RGB(242, 222, 130);
static const COLORREF COL_PLAY     = RGB(92, 220, 132);
static const COLORREF COL_TEXT     = RGB(214, 218, 228);
static const COLORREF COL_TEXTDIM  = RGB(150, 156, 170);
static const COLORREF COL_EDGE     = RGB(58, 62, 78);

static const int    RULER_H    = 26;
static const int    GUTTER_W   = 50;
static const double MIN_SPP    = 1.0 / 64.0;     // 64 px per sample = deepest zoom
static const size_t PEAK_BLOCK = 256;
static const size_t MAX_UNDO   = 15;

// ============================== localization ================================

enum class Lang { En, Zh, Both };
static Lang g_lang = Lang::Both;

static std::wstring Tr(const wchar_t* en, const wchar_t* zh)
{
    switch (g_lang) {
    case Lang::En: return en;
    case Lang::Zh: return zh;
    default:
        if (wcscmp(en, zh) == 0) return en;
        return std::wstring(en) + L" · " + zh;
    }
}

// ============================== document ====================================

struct Document {
    std::vector<std::vector<float>> ch;   // planar float32, all lanes equal length
    unsigned sampleRate = 44100;
    int  srcBits  = 16;
    bool srcFloat = false;
    std::wstring path;

    size_t Total() const { return ch.empty() ? 0 : ch[0].size(); }
    int Channels() const { return (int)ch.size(); }
};

struct Snapshot {
    std::vector<std::vector<float>> ch;
    size_t selA = 0, selB = 0, cursor = 0;
};

struct ClipData {
    std::vector<std::vector<float>> ch;
    unsigned rate = 0;
    bool Empty() const { return ch.empty() || ch[0].empty(); }
};

struct Player {
    HWAVEOUT dev = nullptr;
    enum { NBUF = 4 };
    WAVEHDR hdr[NBUF];
    std::vector<short> buf[NBUF];
    size_t regionStart = 0, regionEnd = 0, feed = 0;
    int chans = 1;
    unsigned rate = 44100;
    unsigned blockBytes = 2;
    bool active = false, paused = false;
    int inFlight = 0;
};

// ============================== globals =====================================

static HINSTANCE g_hinst   = nullptr;
static HWND  g_hwnd        = nullptr;
static HWND  g_status      = nullptr;
static HWND  g_scrollbar   = nullptr;
static HFONT g_font        = nullptr;
static HFONT g_fontSm      = nullptr;
static HFONT g_fontBig     = nullptr;
static int   g_statusH     = 24;
static int   g_scrollH     = 16;

static Document g_doc;
static ClipData g_clip;
static Player   g_play;

static std::deque<Snapshot> g_undo, g_redo;

static size_t g_selA = 0, g_selB = 0, g_cursor = 0;
static bool   g_dirty = false;
static bool   g_dragging = false;

static double g_spp   = 256.0;   // samples per pixel
static double g_scroll = 0.0;    // first visible sample (fractional)
static double g_vzoom = 1.0;     // vertical view gain

static std::wstring g_toast;

static std::vector<std::vector<std::pair<float, float>>> g_peaks; // per channel

// ============================== forward decls ===============================

static void   StopPlayback();
static void   UpdateStatus();
static void   UpdateTitle();
static void   UpdateScrollBar();
static void   BuildMenus();
static void   ClampView();
static RECT   WaveRect();
static std::wstring FmtTime(double samples);

// ============================== small helpers ===============================

static bool HasSel() { return g_selA != g_selB && g_doc.Total() > 0; }
static size_t SelStart() { return g_selA < g_selB ? g_selA : g_selB; }
static size_t SelEnd()   { return g_selA < g_selB ? g_selB : g_selA; }

static void InvalidateWave()
{
    if (g_hwnd) InvalidateRect(g_hwnd, nullptr, FALSE);
}

static void Toast(const std::wstring& s)
{
    g_toast = s;
    if (g_hwnd) {
        SetTimer(g_hwnd, TIMER_TOAST, 2600, nullptr);
        UpdateStatus();
    }
}

static size_t ClampSample(double s)
{
    if (s < 0) return 0;
    double t = (double)g_doc.Total();
    if (s > t) s = t;
    long long v = (long long)std::llround(s);
    if (v < 0) v = 0;
    return (size_t)v;
}

static std::wstring FmtTime(double samples)
{
    unsigned rate = g_doc.sampleRate ? g_doc.sampleRate : 44100;
    if (samples < 0) samples = 0;
    unsigned long long ms =
        (unsigned long long)std::llround(samples * 1000.0 / (double)rate);
    unsigned long mm  = (unsigned long)(ms / 60000ULL);
    unsigned long ss  = (unsigned long)((ms / 1000ULL) % 60ULL);
    unsigned long mmm = (unsigned long)(ms % 1000ULL);
    wchar_t b[40];
    swprintf(b, 40, L"%02lu:%02lu.%03lu", mm, ss, mmm);
    return b;
}

static std::wstring FileNameOf(const std::wstring& path)
{
    size_t p = path.find_last_of(L"\\/");
    return (p == std::wstring::npos) ? path : path.substr(p + 1);
}

// ============================== peaks cache =================================

static void RebuildPeaks()
{
    g_peaks.assign((size_t)g_doc.Channels(), {});
    for (int c = 0; c < g_doc.Channels(); c++) {
        const std::vector<float>& v = g_doc.ch[(size_t)c];
        size_t n = v.size();
        size_t blocks = (n + PEAK_BLOCK - 1) / PEAK_BLOCK;
        auto& pk = g_peaks[(size_t)c];
        pk.resize(blocks);
        for (size_t b = 0; b < blocks; b++) {
            size_t a = b * PEAK_BLOCK;
            size_t e = a + PEAK_BLOCK; if (e > n) e = n;
            float mn = 1e9f, mx = -1e9f;
            for (size_t i = a; i < e; i++) {
                float s = v[i];
                if (s < mn) mn = s;
                if (s > mx) mx = s;
            }
            if (mn > mx) { mn = 0; mx = 0; }
            pk[b] = std::make_pair(mn, mx);
        }
    }
}

// ============================== undo / redo =================================

static size_t SnapBytes(const Snapshot& s)
{
    size_t t = 0;
    for (const auto& v : s.ch) t += v.size() * sizeof(float);
    return t;
}

static void PushUndo()
{
    try {
        Snapshot s;
        s.ch = g_doc.ch;
        s.selA = g_selA; s.selB = g_selB; s.cursor = g_cursor;
        g_undo.push_back(std::move(s));
    } catch (const std::bad_alloc&) {
        g_undo.clear();
        Toast(Tr(L"Low memory — undo history cleared", L"記憶體唔夠 — 復原紀錄清咗"));
        return;
    }
    g_redo.clear();
    while (g_undo.size() > MAX_UNDO) g_undo.pop_front();
    // additional memory safety cap: ~1 GB of snapshots
    size_t total = 0;
    for (const auto& s : g_undo) total += SnapBytes(s);
    while (total > ((size_t)1 << 30) && g_undo.size() > 1) {
        total -= SnapBytes(g_undo.front());
        g_undo.pop_front();
    }
}

static void ClampSelection()
{
    size_t t = g_doc.Total();
    if (g_selA > t) g_selA = t;
    if (g_selB > t) g_selB = t;
    if (g_cursor > t) g_cursor = t;
}

static void AfterEdit()
{
    g_dirty = true;
    ClampSelection();
    RebuildPeaks();
    ClampView();
    UpdateScrollBar();
    UpdateTitle();
    UpdateStatus();
    InvalidateWave();
}

static void DoUndo()
{
    if (g_undo.empty()) { MessageBeep(MB_OK); Toast(Tr(L"Nothing to undo", L"冇嘢可以復原")); return; }
    StopPlayback();
    try {
        Snapshot cur;
        cur.ch = g_doc.ch; cur.selA = g_selA; cur.selB = g_selB; cur.cursor = g_cursor;
        g_redo.push_back(std::move(cur));
        while (g_redo.size() > MAX_UNDO) g_redo.pop_front();
    } catch (const std::bad_alloc&) { g_redo.clear(); }
    Snapshot s = std::move(g_undo.back());
    g_undo.pop_back();
    g_doc.ch = std::move(s.ch);
    g_selA = s.selA; g_selB = s.selB; g_cursor = s.cursor;
    AfterEdit();
    Toast(Tr(L"Undone", L"復原咗"));
}

static void DoRedo()
{
    if (g_redo.empty()) { MessageBeep(MB_OK); Toast(Tr(L"Nothing to redo", L"冇嘢可以重做")); return; }
    StopPlayback();
    try {
        Snapshot cur;
        cur.ch = g_doc.ch; cur.selA = g_selA; cur.selB = g_selB; cur.cursor = g_cursor;
        g_undo.push_back(std::move(cur));
        while (g_undo.size() > MAX_UNDO) g_undo.pop_front();
    } catch (const std::bad_alloc&) { g_undo.clear(); }
    Snapshot s = std::move(g_redo.back());
    g_redo.pop_back();
    g_doc.ch = std::move(s.ch);
    g_selA = s.selA; g_selB = s.selB; g_cursor = s.cursor;
    AfterEdit();
    Toast(Tr(L"Redone", L"重做咗"));
}

// ============================== WAV load / save =============================

static uint32_t RdU32(const uint8_t* p)
{
    return (uint32_t)p[0] | ((uint32_t)p[1] << 8) | ((uint32_t)p[2] << 16) | ((uint32_t)p[3] << 24);
}
static uint16_t RdU16(const uint8_t* p)
{
    return (uint16_t)((uint32_t)p[0] | ((uint32_t)p[1] << 8));
}

// returns L"" on success, otherwise a bilingual error message
static std::wstring LoadWavFile(const std::wstring& path, Document& out)
{
    HANDLE f = CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr,
                           OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (f == INVALID_HANDLE_VALUE)
        return Tr(L"Cannot open the file.", L"開唔到呢個檔案。");

    LARGE_INTEGER li; li.QuadPart = 0;
    GetFileSizeEx(f, &li);
    if (li.QuadPart < 46) {
        CloseHandle(f);
        return Tr(L"File is too small to be a WAV file.", L"個檔案太細，唔會係 WAV 檔。");
    }
    if (li.QuadPart > 1024LL * 1024LL * 1024LL) {
        CloseHandle(f);
        return Tr(L"File is larger than 1 GB — too big for this editor.",
                  L"個檔案大過 1 GB — 呢個編輯器食唔落。");
    }

    std::vector<uint8_t> b;
    try { b.resize((size_t)li.QuadPart); }
    catch (const std::bad_alloc&) { CloseHandle(f); return Tr(L"Out of memory.", L"記憶體唔夠。"); }

    DWORD rd = 0;
    BOOL ok = ReadFile(f, b.data(), (DWORD)b.size(), &rd, nullptr);
    CloseHandle(f);
    if (!ok || rd != (DWORD)b.size())
        return Tr(L"Could not read the file.", L"讀唔到個檔案。");

    if (b.size() < 12 || memcmp(b.data(), "RIFF", 4) != 0 || memcmp(b.data() + 8, "WAVE", 4) != 0)
        return Tr(L"Not a valid WAV file (missing RIFF/WAVE header).",
                  L"唔係有效嘅 WAV 檔（搵唔到 RIFF/WAVE 檔頭）。");

    uint16_t tag = 0, chn = 0, bits = 0;
    uint32_t rate = 0;
    const uint8_t* data = nullptr;
    size_t dataSize = 0;
    bool haveFmt = false, haveData = false;

    size_t off = 12;
    while (off + 8 <= b.size()) {
        uint32_t sz = RdU32(b.data() + off + 4);
        size_t body = off + 8;
        if ((size_t)sz > b.size() - body) sz = (uint32_t)(b.size() - body);
        const uint8_t* p = b.data() + body;

        if (!memcmp(b.data() + off, "fmt ", 4) && sz >= 16 && !haveFmt) {
            tag  = RdU16(p);
            chn  = RdU16(p + 2);
            rate = RdU32(p + 4);
            bits = RdU16(p + 14);
            if (tag == WAVE_FORMAT_EXTENSIBLE && sz >= 26)
                tag = RdU16(p + 24);   // first 2 bytes of SubFormat GUID
            haveFmt = true;
        } else if (!memcmp(b.data() + off, "data", 4) && !haveData) {
            data = p;
            dataSize = sz;
            haveData = true;
        }
        if (haveFmt && haveData) break;
        off = body + sz + (sz & 1);
    }

    if (!haveFmt || !haveData || !data)
        return Tr(L"Not a valid WAV file (fmt/data chunk missing).",
                  L"唔係有效嘅 WAV 檔（冇 fmt 或者 data 區塊）。");

    bool okFmt =
        (tag == WAVE_FORMAT_PCM && (bits == 8 || bits == 16 || bits == 24 || bits == 32)) ||
        (tag == WAVE_FORMAT_IEEE_FLOAT && (bits == 32 || bits == 64));
    if (!okFmt || chn < 1 || chn > 8 || rate < 1000 || rate > 768000) {
        wchar_t en[160], zh[160];
        swprintf(en, 160, L"Unsupported WAV format (tag %u, %u-bit, %u ch, %u Hz). PCM 8/16/24/32-bit or float32 expected.",
                 (unsigned)tag, (unsigned)bits, (unsigned)chn, (unsigned)rate);
        swprintf(zh, 160, L"唔支援嘅 WAV 格式（tag %u，%u 位元，%u 聲道，%u Hz）。要 PCM 8/16/24/32 位元或者 float32 先得。",
                 (unsigned)tag, (unsigned)bits, (unsigned)chn, (unsigned)rate);
        return Tr(en, zh);
    }

    size_t bytesPer = (size_t)bits / 8;
    size_t blockAlign = bytesPer * chn;
    size_t frames = blockAlign ? dataSize / blockAlign : 0;
    if (frames == 0)
        return Tr(L"The WAV file contains no audio samples.", L"呢個 WAV 檔入面冇任何音訊取樣。");

    Document nd;
    nd.sampleRate = rate;
    nd.srcBits = (int)bits;
    nd.srcFloat = (tag == WAVE_FORMAT_IEEE_FLOAT);
    try {
        nd.ch.assign(chn, std::vector<float>(frames));
        for (size_t fIdx = 0; fIdx < frames; fIdx++) {
            const uint8_t* fr = data + fIdx * blockAlign;
            for (unsigned c = 0; c < chn; c++) {
                const uint8_t* sp = fr + (size_t)c * bytesPer;
                float v = 0.0f;
                if (tag == WAVE_FORMAT_PCM) {
                    if (bits == 8) {
                        v = ((int)sp[0] - 128) / 128.0f;
                    } else if (bits == 16) {
                        int16_t s = (int16_t)RdU16(sp);
                        v = s / 32768.0f;
                    } else if (bits == 24) {
                        int32_t s = (int32_t)((uint32_t)sp[0] | ((uint32_t)sp[1] << 8) | ((uint32_t)sp[2] << 16));
                        if (s & 0x800000) s |= (int32_t)0xFF000000;
                        v = s / 8388608.0f;
                    } else { // 32
                        int32_t s = (int32_t)RdU32(sp);
                        v = (float)(s / 2147483648.0);
                    }
                } else { // IEEE float
                    if (bits == 32) {
                        uint32_t u = RdU32(sp);
                        float fv;
                        memcpy(&fv, &u, 4);
                        v = fv;
                    } else { // 64
                        uint64_t u = (uint64_t)RdU32(sp) | ((uint64_t)RdU32(sp + 4) << 32);
                        double dv;
                        memcpy(&dv, &u, 8);
                        v = (float)dv;
                    }
                }
                nd.ch[c][fIdx] = v;
            }
        }
    } catch (const std::bad_alloc&) {
        return Tr(L"Out of memory while decoding the file.", L"解碼檔案嗰陣記憶體唔夠。");
    }

    out = std::move(nd);
    return L"";
}

static std::wstring SaveWavFile(const std::wstring& path, const Document& d)
{
    size_t total = d.Total();
    int ch = d.Channels();
    if (!total || !ch)
        return Tr(L"There is no audio to save.", L"冇音訊可以儲存。");

    unsigned long long dataBytes = (unsigned long long)total * (unsigned long long)ch * 2ULL;
    if (dataBytes > 0xFFFFFFF0ULL - 44ULL)
        return Tr(L"Audio is too long for a standard WAV file (4 GB limit).",
                  L"段音訊太長，超出標準 WAV 檔 4 GB 上限。");

    std::vector<uint8_t> outBuf;
    try { outBuf.reserve((size_t)dataBytes + 44); }
    catch (const std::bad_alloc&) { return Tr(L"Out of memory.", L"記憶體唔夠。"); }

    auto p8  = [&](uint8_t v)  { outBuf.push_back(v); };
    auto p16 = [&](uint16_t v) { p8((uint8_t)(v & 0xFF)); p8((uint8_t)(v >> 8)); };
    auto p32 = [&](uint32_t v) { p16((uint16_t)(v & 0xFFFF)); p16((uint16_t)(v >> 16)); };
    auto ptag = [&](const char* t) { for (int i = 0; i < 4; i++) p8((uint8_t)t[i]); };

    unsigned rate = d.sampleRate;
    uint16_t block = (uint16_t)(ch * 2);

    ptag("RIFF");
    p32((uint32_t)(36ULL + dataBytes));
    ptag("WAVE");
    ptag("fmt ");
    p32(16);
    p16(WAVE_FORMAT_PCM);
    p16((uint16_t)ch);
    p32(rate);
    p32(rate * block);
    p16(block);
    p16(16);
    ptag("data");
    p32((uint32_t)dataBytes);

    try {
        for (size_t f = 0; f < total; f++) {
            for (int c = 0; c < ch; c++) {
                float v = d.ch[(size_t)c][f];
                if (v > 1.0f) v = 1.0f;
                if (v < -1.0f) v = -1.0f;
                int s = (int)std::lround(v * 32767.0f);
                p16((uint16_t)(int16_t)s);
            }
        }
    } catch (const std::bad_alloc&) {
        return Tr(L"Out of memory while encoding.", L"編碼嗰陣記憶體唔夠。");
    }

    HANDLE hf = CreateFileW(path.c_str(), GENERIC_WRITE, 0, nullptr,
                            CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (hf == INVALID_HANDLE_VALUE)
        return Tr(L"Cannot create the output file.", L"整唔到輸出檔案。");
    DWORD wr = 0;
    BOOL ok = WriteFile(hf, outBuf.data(), (DWORD)outBuf.size(), &wr, nullptr);
    CloseHandle(hf);
    if (!ok || wr != (DWORD)outBuf.size())
        return Tr(L"Failed to write the file to disk.", L"寫檔案落硬碟失敗。");
    return L"";
}

// ============================== view geometry ===============================

static RECT WaveRect()
{
    RECT rc; rc.left = rc.top = rc.right = rc.bottom = 0;
    if (g_hwnd) GetClientRect(g_hwnd, &rc);
    rc.bottom -= (g_statusH + g_scrollH);
    rc.top += RULER_H;
    rc.left += GUTTER_W;
    if (rc.bottom < rc.top) rc.bottom = rc.top;
    if (rc.right < rc.left) rc.right = rc.left;
    return rc;
}

static double XToSample(int x)
{
    RECT wr = WaveRect();
    return g_scroll + (double)(x - wr.left) * g_spp;
}

static int SampleToX(double s)
{
    RECT wr = WaveRect();
    double dx = (s - g_scroll) / g_spp;
    if (dx < -1e6) dx = -1e6;
    if (dx > 1e6) dx = 1e6;
    return (int)wr.left + (int)std::llround(dx);
}

static void ClampView()
{
    RECT wr = WaveRect();
    double w = (double)(wr.right - wr.left);
    if (w < 1) w = 1;
    double total = (double)g_doc.Total();
    if (total <= 0) { g_scroll = 0; g_spp = 256.0; }
    else {
        double maxSpp = total / w;
        if (maxSpp < MIN_SPP) maxSpp = MIN_SPP;
        if (g_spp < MIN_SPP) g_spp = MIN_SPP;
        if (g_spp > maxSpp) g_spp = maxSpp;
        double maxScroll = total - w * g_spp;
        if (maxScroll < 0) maxScroll = 0;
        if (g_scroll < 0) g_scroll = 0;
        if (g_scroll > maxScroll) g_scroll = maxScroll;
    }
    if (g_vzoom < 0.25) g_vzoom = 0.25;
    if (g_vzoom > 32.0) g_vzoom = 32.0;
}

static void UpdateScrollBar()
{
    if (!g_scrollbar) return;
    RECT wr = WaveRect();
    double w = (double)(wr.right - wr.left);
    if (w < 1) w = 1;
    double total = (double)g_doc.Total();
    double vis = w * g_spp;

    SCROLLINFO si;
    ZeroMemory(&si, sizeof(si));
    si.cbSize = sizeof(si);
    si.fMask = SIF_RANGE | SIF_PAGE | SIF_POS | SIF_DISABLENOSCROLL;
    const int RANGE = 100000;
    if (total <= vis || total <= 0) {
        si.nMin = 0; si.nMax = 0; si.nPage = 1; si.nPos = 0;
    } else {
        si.nMin = 0; si.nMax = RANGE;
        double frac = vis / total;
        si.nPage = (UINT)(RANGE * frac);
        if (si.nPage < 1) si.nPage = 1;
        double maxScroll = total - vis;
        int span = RANGE - (int)si.nPage + 1;
        if (span < 1) span = 1;
        si.nPos = (int)((g_scroll / maxScroll) * span);
    }
    SetScrollInfo(g_scrollbar, SB_CTL, &si, TRUE);
}

static void ZoomAt(int clientX, double factor)
{
    if (!g_doc.Total()) return;
    RECT wr = WaveRect();
    if (clientX < (int)wr.left) clientX = (int)wr.left;
    if (clientX > (int)wr.right) clientX = (int)wr.right;
    double sampleAt = g_scroll + (double)(clientX - wr.left) * g_spp;
    g_spp *= factor;
    ClampView();
    g_scroll = sampleAt - (double)(clientX - wr.left) * g_spp;
    ClampView();
    UpdateScrollBar();
    InvalidateWave();
}

static void ZoomFit()
{
    RECT wr = WaveRect();
    double w = (double)(wr.right - wr.left);
    if (w < 1) w = 1;
    double total = (double)g_doc.Total();
    if (total > 0) g_spp = total / w;
    g_scroll = 0;
    ClampView();
    UpdateScrollBar();
    InvalidateWave();
}

static void ZoomToSelection()
{
    if (!HasSel()) { Toast(Tr(L"Select a range first", L"請先揀一個範圍")); MessageBeep(MB_OK); return; }
    RECT wr = WaveRect();
    double w = (double)(wr.right - wr.left);
    if (w < 1) w = 1;
    g_spp = (double)(SelEnd() - SelStart()) / w;
    g_scroll = (double)SelStart();
    ClampView();
    UpdateScrollBar();
    InvalidateWave();
}

static void EnsureVisible(size_t s)
{
    RECT wr = WaveRect();
    double vis = (double)(wr.right - wr.left) * g_spp;
    if ((double)s < g_scroll) g_scroll = (double)s - vis * 0.1;
    else if ((double)s > g_scroll + vis) g_scroll = (double)s - vis * 0.9;
    ClampView();
    UpdateScrollBar();
}

// ============================== playback ====================================

static bool QueueBuffer(int i)
{
    size_t remain = (g_play.feed < g_play.regionEnd) ? (g_play.regionEnd - g_play.feed) : 0;
    if (!remain || !g_play.dev) return false;

    size_t chunk = (size_t)g_play.rate / 4;
    if (chunk < 2048) chunk = 2048;
    size_t frames = remain < chunk ? remain : chunk;

    std::vector<short>& v = g_play.buf[i];
    try { v.resize(frames * (size_t)g_play.chans); }
    catch (const std::bad_alloc&) { return false; }

    int docCh = g_doc.Channels();
    for (size_t f = 0; f < frames; f++) {
        for (int c = 0; c < g_play.chans; c++) {
            int sc = c < docCh ? c : docCh - 1;
            float s = g_doc.ch[(size_t)sc][g_play.feed + f];
            if (s > 1.0f) s = 1.0f;
            if (s < -1.0f) s = -1.0f;
            v[f * (size_t)g_play.chans + (size_t)c] = (short)std::lround(s * 32767.0f);
        }
    }
    g_play.feed += frames;

    WAVEHDR& h = g_play.hdr[i];
    if (h.dwFlags & WHDR_PREPARED)
        waveOutUnprepareHeader(g_play.dev, &h, sizeof(WAVEHDR));
    ZeroMemory(&h, sizeof(h));
    h.lpData = (LPSTR)v.data();
    h.dwBufferLength = (DWORD)(v.size() * sizeof(short));
    if (waveOutPrepareHeader(g_play.dev, &h, sizeof(WAVEHDR)) != MMSYSERR_NOERROR) return false;
    if (waveOutWrite(g_play.dev, &h, sizeof(WAVEHDR)) != MMSYSERR_NOERROR) {
        waveOutUnprepareHeader(g_play.dev, &h, sizeof(WAVEHDR));
        return false;
    }
    g_play.inFlight++;
    return true;
}

static void StopPlayback()
{
    if (!g_play.dev) return;
    HWAVEOUT d = g_play.dev;
    g_play.active = false;
    if (g_play.paused) waveOutRestart(d);   // some drivers dislike reset while paused
    g_play.paused = false;
    waveOutReset(d);
    for (int i = 0; i < Player::NBUF; i++) {
        if (g_play.hdr[i].dwFlags & WHDR_PREPARED)
            waveOutUnprepareHeader(d, &g_play.hdr[i], sizeof(WAVEHDR));
        ZeroMemory(&g_play.hdr[i], sizeof(WAVEHDR));
    }
    waveOutClose(d);
    g_play.dev = nullptr;
    g_play.inFlight = 0;
    if (g_hwnd) {
        KillTimer(g_hwnd, TIMER_PLAY);
        InvalidateWave();
        UpdateStatus();
    }
}

static size_t PlayPositionSamples()
{
    if (!g_play.dev) return g_play.regionStart;
    MMTIME mmt;
    ZeroMemory(&mmt, sizeof(mmt));
    mmt.wType = TIME_SAMPLES;
    if (waveOutGetPosition(g_play.dev, &mmt, sizeof(mmt)) != MMSYSERR_NOERROR)
        return g_play.regionStart;
    size_t s = 0;
    if (mmt.wType == TIME_SAMPLES) s = (size_t)mmt.u.sample;
    else if (mmt.wType == TIME_BYTES) s = (size_t)(mmt.u.cb / (g_play.blockBytes ? g_play.blockBytes : 1));
    else if (mmt.wType == TIME_MS) s = (size_t)((double)mmt.u.ms * g_play.rate / 1000.0);
    size_t p = g_play.regionStart + s;
    if (p > g_play.regionEnd) p = g_play.regionEnd;
    return p;
}

static void StartPlayback()
{
    StopPlayback();
    size_t total = g_doc.Total();
    if (!total) { Toast(Tr(L"Open a WAV file first", L"請先開一個 WAV 檔")); MessageBeep(MB_OK); return; }

    size_t a, b;
    if (HasSel()) { a = SelStart(); b = SelEnd(); }
    else { a = g_cursor < total ? g_cursor : 0; b = total; }
    if (b <= a) { MessageBeep(MB_OK); return; }

    g_play.chans = g_doc.Channels() < 2 ? 1 : 2;
    g_play.rate = g_doc.sampleRate;
    g_play.blockBytes = (unsigned)g_play.chans * 2u;

    WAVEFORMATEX fmt;
    ZeroMemory(&fmt, sizeof(fmt));
    fmt.wFormatTag = WAVE_FORMAT_PCM;
    fmt.nChannels = (WORD)g_play.chans;
    fmt.nSamplesPerSec = g_play.rate;
    fmt.wBitsPerSample = 16;
    fmt.nBlockAlign = (WORD)(g_play.chans * 2);
    fmt.nAvgBytesPerSec = fmt.nSamplesPerSec * fmt.nBlockAlign;

    if (waveOutOpen(&g_play.dev, WAVE_MAPPER, &fmt, (DWORD_PTR)g_hwnd, 0,
                    CALLBACK_WINDOW) != MMSYSERR_NOERROR) {
        g_play.dev = nullptr;
        MessageBoxW(g_hwnd,
            Tr(L"Could not open the audio output device.", L"開唔到音訊輸出裝置。").c_str(),
            Tr(L"Playback error", L"播放錯誤").c_str(), MB_OK | MB_ICONERROR);
        return;
    }

    g_play.regionStart = a;
    g_play.regionEnd = b;
    g_play.feed = a;
    g_play.active = true;
    g_play.paused = false;
    g_play.inFlight = 0;
    ZeroMemory(g_play.hdr, sizeof(g_play.hdr));

    for (int i = 0; i < Player::NBUF; i++)
        if (!QueueBuffer(i)) break;

    if (g_play.inFlight == 0) { StopPlayback(); return; }
    SetTimer(g_hwnd, TIMER_PLAY, 33, nullptr);
    UpdateStatus();
    InvalidateWave();
}

static void TogglePlay()
{
    if (g_play.active) {
        if (g_play.paused) { waveOutRestart(g_play.dev); g_play.paused = false; }
        else { waveOutPause(g_play.dev); g_play.paused = true; }
        UpdateStatus();
        InvalidateWave();
    } else {
        StartPlayback();
    }
}

// ============================== edit operations =============================

static bool RequireAudio()
{
    if (g_doc.Total()) return true;
    Toast(Tr(L"No audio loaded", L"未有音訊"));
    MessageBeep(MB_OK);
    return false;
}

static bool RequireSel()
{
    if (HasSel()) return true;
    Toast(Tr(L"Drag over the waveform to select a range first", L"請先喺波形上面拖一個範圍"));
    MessageBeep(MB_OK);
    return false;
}

static bool GetTarget(size_t& a, size_t& b) // selection, else whole file
{
    if (!g_doc.Total()) return false;
    if (HasSel()) { a = SelStart(); b = SelEnd(); }
    else { a = 0; b = g_doc.Total(); }
    return b > a;
}

static void CopyRegionToClip(size_t a, size_t b)
{
    try {
        g_clip.rate = g_doc.sampleRate;
        g_clip.ch.assign((size_t)g_doc.Channels(), {});
        for (int c = 0; c < g_doc.Channels(); c++)
            g_clip.ch[(size_t)c].assign(g_doc.ch[(size_t)c].begin() + (ptrdiff_t)a,
                                        g_doc.ch[(size_t)c].begin() + (ptrdiff_t)b);
    } catch (const std::bad_alloc&) {
        g_clip.ch.clear();
        Toast(Tr(L"Low memory — clipboard cleared", L"記憶體唔夠 — 剪貼簿清咗"));
    }
}

static void DoCopy()
{
    if (!RequireAudio() || !RequireSel()) return;
    CopyRegionToClip(SelStart(), SelEnd());
    Toast(Tr(L"Copied ", L"複製咗 ") + FmtTime((double)(SelEnd() - SelStart())));
}

static void EraseRegion(size_t a, size_t b)
{
    for (auto& v : g_doc.ch)
        v.erase(v.begin() + (ptrdiff_t)a, v.begin() + (ptrdiff_t)b);
    g_selA = g_selB = g_cursor = a;
}

static void DoCut()
{
    if (!RequireAudio() || !RequireSel()) return;
    StopPlayback();
    size_t a = SelStart(), b = SelEnd();
    CopyRegionToClip(a, b);
    PushUndo();
    EraseRegion(a, b);
    AfterEdit();
    Toast(Tr(L"Cut ", L"剪走咗 ") + FmtTime((double)(b - a)));
}

static void DoDelete()
{
    if (!RequireAudio() || !RequireSel()) return;
    StopPlayback();
    size_t a = SelStart(), b = SelEnd();
    PushUndo();
    EraseRegion(a, b);
    AfterEdit();
    Toast(Tr(L"Deleted ", L"刪咗 ") + FmtTime((double)(b - a)));
}

static std::vector<float> ResampleLinear(const std::vector<float>& src, size_t outLen, double ratio)
{
    std::vector<float> out(outLen);
    if (src.empty()) return out;
    for (size_t i = 0; i < outLen; i++) {
        double pos = (double)i / ratio;
        size_t i0 = (size_t)pos;
        if (i0 >= src.size() - 1) { out[i] = src.back(); continue; }
        double frac = pos - (double)i0;
        out[i] = (float)((1.0 - frac) * src[i0] + frac * src[i0 + 1]);
    }
    return out;
}

static void DoPaste()
{
    if (g_clip.Empty()) {
        Toast(Tr(L"Clipboard is empty", L"剪貼簿冇嘢"));
        MessageBeep(MB_OK);
        return;
    }
    StopPlayback();

    try {
        if (g_doc.Total() == 0 && g_doc.ch.empty()) {
            // paste into an empty document — adopt clipboard wholesale
            PushUndo();
            g_doc.ch = g_clip.ch;
            g_doc.sampleRate = g_clip.rate ? g_clip.rate : 44100;
            g_doc.srcBits = 32;
            g_doc.srcFloat = true;
            g_selA = 0; g_selB = g_doc.Total(); g_cursor = 0;
            ZoomFit();
            AfterEdit();
            Toast(Tr(L"Pasted into a new track", L"貼咗入新音軌"));
            return;
        }

        PushUndo();
        size_t pos = HasSel() ? SelStart() : g_cursor;
        if (HasSel()) EraseRegion(SelStart(), SelEnd());
        if (pos > g_doc.Total()) pos = g_doc.Total();

        int docCh = g_doc.Channels();
        int clipCh = (int)g_clip.ch.size();
        unsigned clipRate = g_clip.rate ? g_clip.rate : g_doc.sampleRate;
        double ratio = (double)g_doc.sampleRate / (double)clipRate;
        size_t clipLen = g_clip.ch[0].size();
        size_t outLen = (ratio == 1.0) ? clipLen
                        : (size_t)((double)clipLen * ratio + 0.5);
        if (outLen < 1) outLen = 1;

        for (int c = 0; c < docCh; c++) {
            const std::vector<float>* src = nullptr;
            std::vector<float> mix;
            if (clipCh == 1) {
                src = &g_clip.ch[0];
            } else if (docCh == 1) {
                mix.assign(clipLen, 0.0f);
                for (int k = 0; k < clipCh; k++)
                    for (size_t i = 0; i < clipLen; i++)
                        mix[i] += g_clip.ch[(size_t)k][i] / (float)clipCh;
                src = &mix;
            } else {
                src = &g_clip.ch[(size_t)(c < clipCh ? c : clipCh - 1)];
            }
            std::vector<float> ins = (ratio == 1.0) ? *src
                                     : ResampleLinear(*src, outLen, ratio);
            if (ins.size() != outLen) ins.resize(outLen, 0.0f);
            g_doc.ch[(size_t)c].insert(g_doc.ch[(size_t)c].begin() + (ptrdiff_t)pos,
                                       ins.begin(), ins.end());
        }
        g_selA = pos;
        g_selB = pos + outLen;
        g_cursor = g_selB;
        AfterEdit();
        Toast(Tr(L"Pasted ", L"貼上咗 ") + FmtTime((double)outLen));
    } catch (const std::bad_alloc&) {
        Toast(Tr(L"Low memory — paste failed", L"記憶體唔夠 — 貼上失敗"));
        MessageBeep(MB_ICONWARNING);
    }
}

static void DoTrim()
{
    if (!RequireAudio() || !RequireSel()) return;
    StopPlayback();
    size_t a = SelStart(), b = SelEnd();
    PushUndo();
    for (auto& v : g_doc.ch) {
        std::vector<float> keep(v.begin() + (ptrdiff_t)a, v.begin() + (ptrdiff_t)b);
        v = std::move(keep);
    }
    g_selA = 0; g_selB = g_doc.Total(); g_cursor = 0;
    g_scroll = 0;
    ZoomFit();
    AfterEdit();
    Toast(Tr(L"Trimmed to selection", L"淨係留返選取範圍"));
}

static void DoSilence()
{
    size_t a, b;
    if (!RequireAudio() || !GetTarget(a, b)) return;
    StopPlayback();
    PushUndo();
    for (auto& v : g_doc.ch)
        for (size_t i = a; i < b; i++) v[i] = 0.0f;
    AfterEdit();
    Toast(Tr(L"Silenced ", L"靜咗音 ") + FmtTime((double)(b - a)));
}

static void DoFade(bool fadeIn)
{
    size_t a, b;
    if (!RequireAudio() || !GetTarget(a, b)) return;
    StopPlayback();
    PushUndo();
    double len = (double)(b - a);
    for (auto& v : g_doc.ch) {
        for (size_t i = a; i < b; i++) {
            double t = len > 1 ? (double)(i - a) / (len - 1.0) : 1.0;
            double g = fadeIn ? t : (1.0 - t);
            v[i] = (float)(v[i] * g);
        }
    }
    AfterEdit();
    Toast(fadeIn ? Tr(L"Fade in applied", L"加咗淡入")
                 : Tr(L"Fade out applied", L"加咗淡出"));
}

static void ApplyGainDb(double db)
{
    size_t a, b;
    if (!GetTarget(a, b)) return;
    StopPlayback();
    PushUndo();
    float f = (float)std::pow(10.0, db / 20.0);
    for (auto& v : g_doc.ch)
        for (size_t i = a; i < b; i++) v[i] *= f;
    AfterEdit();
    wchar_t en[64], zh[64];
    swprintf(en, 64, L"Gain %+.1f dB applied", db);
    swprintf(zh, 64, L"套用咗 %+.1f dB 增益", db);
    Toast(Tr(en, zh));
}

static void DoNormalize()
{
    size_t a, b;
    if (!RequireAudio() || !GetTarget(a, b)) return;
    float peak = 0.0f;
    for (const auto& v : g_doc.ch)
        for (size_t i = a; i < b; i++) {
            float av = v[i] < 0 ? -v[i] : v[i];
            if (av > peak) peak = av;
        }
    if (peak < 1e-9f) {
        Toast(Tr(L"All silence — nothing to normalize", L"全部靜音 — 冇嘢可以正規化"));
        MessageBeep(MB_OK);
        return;
    }
    StopPlayback();
    PushUndo();
    float target = (float)std::pow(10.0, -1.0 / 20.0);  // -1 dBFS
    float f = target / peak;
    for (auto& v : g_doc.ch)
        for (size_t i = a; i < b; i++) v[i] *= f;
    AfterEdit();
    Toast(Tr(L"Normalized to -1 dBFS", L"正規化到 -1 dBFS"));
}

static void DoReverse()
{
    size_t a, b;
    if (!RequireAudio() || !GetTarget(a, b)) return;
    StopPlayback();
    PushUndo();
    for (auto& v : g_doc.ch)
        std::reverse(v.begin() + (ptrdiff_t)a, v.begin() + (ptrdiff_t)b);
    AfterEdit();
    Toast(Tr(L"Reversed", L"反轉咗"));
}

static void SelectAll()
{
    if (!g_doc.Total()) return;
    g_selA = 0;
    g_selB = g_doc.Total();
    g_cursor = 0;
    InvalidateWave();
    UpdateStatus();
}

static void ClearSelection()
{
    g_selA = g_selB = g_cursor;
    InvalidateWave();
    UpdateStatus();
}

static void MoveCursor(int dir, bool extend)
{
    if (!g_doc.Total()) return;
    double step = g_spp * 16.0;
    if (step < 1.0) step = 1.0;
    double target = (double)g_cursor + (double)dir * step;
    size_t s = ClampSample(target);
    if (extend) {
        if (g_selA == g_selB) g_selA = g_cursor;
        g_selB = s;
    } else {
        g_selA = g_selB = s;
    }
    g_cursor = s;
    EnsureVisible(s);
    InvalidateWave();
    UpdateStatus();
}

// ============================== gain dialog =================================

struct GainCtx {
    HWND track = nullptr;
    HWND val = nullptr;
    bool done = false;
    bool ok = false;
};

static const wchar_t* GAIN_CLS = L"WinForgeAudioGainDlg";

static void GainUpdateLabel(GainCtx* ctx)
{
    if (!ctx || !ctx->track || !ctx->val) return;
    int pos = (int)SendMessageW(ctx->track, TBM_GETPOS, 0, 0);
    double db = (pos - 240) / 10.0;
    wchar_t b[48];
    swprintf(b, 48, L"%+.1f dB", db);
    SetWindowTextW(ctx->val, b);
}

static LRESULT CALLBACK GainProc(HWND w, UINT m, WPARAM wp, LPARAM lp)
{
    GainCtx* ctx = (GainCtx*)GetWindowLongPtrW(w, GWLP_USERDATA);
    switch (m) {
    case WM_CREATE: {
        CREATESTRUCTW* cs = (CREATESTRUCTW*)lp;
        ctx = (GainCtx*)cs->lpCreateParams;
        SetWindowLongPtrW(w, GWLP_USERDATA, (LONG_PTR)ctx);

        std::wstring lbl = Tr(L"Gain (dB), -24 to +24:", L"增益（分貝），-24 到 +24：");
        HWND st = CreateWindowExW(0, L"STATIC", lbl.c_str(),
            WS_CHILD | WS_VISIBLE, 16, 12, 340, 20, w, nullptr, g_hinst, nullptr);
        SendMessageW(st, WM_SETFONT, (WPARAM)g_font, TRUE);

        ctx->track = CreateWindowExW(0, L"msctls_trackbar32", L"",
            WS_CHILD | WS_VISIBLE | WS_TABSTOP | TBS_HORZ | TBS_AUTOTICKS,
            14, 38, 344, 34, w, (HMENU)(INT_PTR)100, g_hinst, nullptr);
        SendMessageW(ctx->track, TBM_SETRANGE, TRUE, MAKELPARAM(0, 480));  // -24.0 .. +24.0 in 0.1 dB
        SendMessageW(ctx->track, TBM_SETPOS, TRUE, 240);
        SendMessageW(ctx->track, TBM_SETTICFREQ, 30, 0);
        SendMessageW(ctx->track, TBM_SETPAGESIZE, 0, 10);
        SendMessageW(ctx->track, TBM_SETLINESIZE, 0, 1);

        ctx->val = CreateWindowExW(0, L"STATIC", L"+0.0 dB",
            WS_CHILD | WS_VISIBLE | SS_CENTER, 16, 78, 340, 22, w, nullptr, g_hinst, nullptr);
        SendMessageW(ctx->val, WM_SETFONT, (WPARAM)g_fontBig, TRUE);

        std::wstring okT = Tr(L"OK", L"好"), caT = Tr(L"Cancel", L"取消");
        HWND ok = CreateWindowExW(0, L"BUTTON", okT.c_str(),
            WS_CHILD | WS_VISIBLE | WS_TABSTOP | BS_DEFPUSHBUTTON,
            176, 112, 86, 28, w, (HMENU)(INT_PTR)IDOK, g_hinst, nullptr);
        HWND ca = CreateWindowExW(0, L"BUTTON", caT.c_str(),
            WS_CHILD | WS_VISIBLE | WS_TABSTOP | BS_PUSHBUTTON,
            270, 112, 86, 28, w, (HMENU)(INT_PTR)IDCANCEL, g_hinst, nullptr);
        SendMessageW(ok, WM_SETFONT, (WPARAM)g_font, TRUE);
        SendMessageW(ca, WM_SETFONT, (WPARAM)g_font, TRUE);
        return 0;
    }
    case WM_HSCROLL:
        GainUpdateLabel(ctx);
        return 0;
    case WM_COMMAND:
        if (ctx) {
            if (LOWORD(wp) == IDOK)     { ctx->ok = true; ctx->done = true; }
            if (LOWORD(wp) == IDCANCEL) { ctx->done = true; }
        }
        return 0;
    case WM_CLOSE:
        if (ctx) ctx->done = true;
        return 0;
    }
    return DefWindowProcW(w, m, wp, lp);
}

static bool ShowGainDialog(double& dbOut)
{
    static bool registered = false;
    if (!registered) {
        WNDCLASSEXW wc;
        ZeroMemory(&wc, sizeof(wc));
        wc.cbSize = sizeof(wc);
        wc.lpfnWndProc = GainProc;
        wc.hInstance = g_hinst;
        wc.hCursor = LoadCursorW(nullptr, IDC_ARROW);
        wc.hbrBackground = (HBRUSH)(COLOR_BTNFACE + 1);
        wc.lpszClassName = GAIN_CLS;
        RegisterClassExW(&wc);
        registered = true;
    }

    GainCtx ctx;
    RECT pr; GetWindowRect(g_hwnd, &pr);
    int W = 390, H = 190;
    int x = pr.left + ((pr.right - pr.left) - W) / 2;
    int y = pr.top + ((pr.bottom - pr.top) - H) / 2;

    std::wstring title = Tr(L"Gain", L"增益");
    HWND dlg = CreateWindowExW(WS_EX_DLGMODALFRAME | WS_EX_CONTROLPARENT, GAIN_CLS,
        title.c_str(), WS_POPUP | WS_CAPTION | WS_SYSMENU,
        x, y, W, H, g_hwnd, nullptr, g_hinst, &ctx);
    if (!dlg) return false;

    EnableWindow(g_hwnd, FALSE);
    ShowWindow(dlg, SW_SHOW);
    SetFocus(ctx.track);
    GainUpdateLabel(&ctx);

    MSG msg{};
    bool quit = false;
    while (!ctx.done) {
        BOOL r = GetMessageW(&msg, nullptr, 0, 0);
        if (r == 0) { quit = true; break; }
        if (r == -1) break;
        if (msg.message == WM_KEYDOWN && msg.wParam == VK_RETURN) { ctx.ok = true; ctx.done = true; continue; }
        if (msg.message == WM_KEYDOWN && msg.wParam == VK_ESCAPE) { ctx.done = true; continue; }
        if (!IsDialogMessageW(dlg, &msg)) {
            TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }
    }
    int pos = ctx.track ? (int)SendMessageW(ctx.track, TBM_GETPOS, 0, 0) : 240;
    dbOut = (pos - 240) / 10.0;

    EnableWindow(g_hwnd, TRUE);
    SetActiveWindow(g_hwnd);
    SetFocus(g_hwnd);
    DestroyWindow(dlg);
    if (quit) PostQuitMessage((int)msg.wParam);
    return ctx.ok;
}

static void DoGain()
{
    if (!RequireAudio()) return;
    double db = 0.0;
    if (!ShowGainDialog(db)) return;
    if (db == 0.0) { Toast(Tr(L"Gain unchanged (0 dB)", L"增益冇變（0 dB）")); return; }
    ApplyGainDb(db);
}

// ============================== file dialogs ================================

static std::vector<wchar_t> MakeFilter()
{
    std::vector<wchar_t> f;
    auto add = [&](const std::wstring& s) {
        f.insert(f.end(), s.begin(), s.end());
        f.push_back(L'\0');
    };
    add(Tr(L"WAV audio (*.wav)", L"WAV 音訊檔 (*.wav)"));
    add(L"*.wav");
    add(Tr(L"All files (*.*)", L"所有檔案 (*.*)"));
    add(L"*.*");
    f.push_back(L'\0');
    return f;
}

static bool AskOpenPath(std::wstring& out)
{
    std::vector<wchar_t> filter = MakeFilter();
    std::wstring title = Tr(L"Open WAV file", L"開啟 WAV 檔");
    wchar_t buf[1024]; buf[0] = 0;
    OPENFILENAMEW ofn;
    ZeroMemory(&ofn, sizeof(ofn));
    ofn.lStructSize = sizeof(ofn);
    ofn.hwndOwner = g_hwnd;
    ofn.lpstrFilter = filter.data();
    ofn.lpstrFile = buf;
    ofn.nMaxFile = 1024;
    ofn.lpstrTitle = title.c_str();
    ofn.Flags = OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_HIDEREADONLY;
    if (!GetOpenFileNameW(&ofn)) return false;
    out = buf;
    return true;
}

static bool AskSavePath(std::wstring& out)
{
    std::vector<wchar_t> filter = MakeFilter();
    std::wstring title = Tr(L"Save as 16-bit WAV", L"另存做 16-bit WAV");
    wchar_t buf[1024]; buf[0] = 0;
    if (!g_doc.path.empty()) {
        std::wstring name = FileNameOf(g_doc.path);
        const size_t copied = name.copy(buf, 1000);
        buf[copied] = L'\0';
    }
    OPENFILENAMEW ofn;
    ZeroMemory(&ofn, sizeof(ofn));
    ofn.lStructSize = sizeof(ofn);
    ofn.hwndOwner = g_hwnd;
    ofn.lpstrFilter = filter.data();
    ofn.lpstrFile = buf;
    ofn.nMaxFile = 1024;
    ofn.lpstrTitle = title.c_str();
    ofn.lpstrDefExt = L"wav";
    ofn.Flags = OFN_EXPLORER | OFN_OVERWRITEPROMPT | OFN_PATHMUSTEXIST | OFN_HIDEREADONLY;
    if (!GetSaveFileNameW(&ofn)) return false;
    out = buf;
    return true;
}

static bool CmdSaveAs()
{
    if (!g_doc.Total()) {
        Toast(Tr(L"No audio to save", L"冇音訊可以儲存"));
        MessageBeep(MB_OK);
        return false;
    }
    std::wstring p;
    if (!AskSavePath(p)) return false;
    std::wstring err = SaveWavFile(p, g_doc);
    if (!err.empty()) {
        MessageBoxW(g_hwnd, err.c_str(),
                    Tr(L"Save failed", L"儲存失敗").c_str(), MB_OK | MB_ICONERROR);
        return false;
    }
    g_doc.path = p;
    g_dirty = false;
    UpdateTitle();
    Toast(Tr(L"Saved ", L"儲存咗 ") + FileNameOf(p));
    return true;
}

static bool ConfirmDiscard()
{
    if (!g_dirty) return true;
    int r = MessageBoxW(g_hwnd,
        Tr(L"You have unsaved changes. Save them before continuing?",
           L"仲有未儲存嘅變更。想唔想儲存咗先？").c_str(),
        Tr(L"Unsaved changes", L"未儲存嘅變更").c_str(),
        MB_YESNOCANCEL | MB_ICONWARNING);
    if (r == IDCANCEL) return false;
    if (r == IDYES) return CmdSaveAs();
    return true;
}

static void OpenPath(const std::wstring& p)
{
    Document nd;
    std::wstring err = LoadWavFile(p, nd);
    if (!err.empty()) {
        MessageBoxW(g_hwnd, err.c_str(),
                    Tr(L"Cannot open file", L"開唔到檔案").c_str(), MB_OK | MB_ICONERROR);
        return;
    }
    StopPlayback();
    g_doc = std::move(nd);
    g_doc.path = p;
    g_undo.clear();
    g_redo.clear();
    g_dirty = false;
    g_selA = g_selB = g_cursor = 0;
    g_scroll = 0;
    g_vzoom = 1.0;
    RebuildPeaks();
    ZoomFit();
    UpdateScrollBar();
    UpdateTitle();
    UpdateStatus();
    InvalidateWave();
    Toast(Tr(L"Loaded ", L"開咗 ") + FileNameOf(p) + L"  (" + FmtTime((double)g_doc.Total()) + L")");
}

static void CmdOpen()
{
    if (!ConfirmDiscard()) return;
    std::wstring p;
    if (!AskOpenPath(p)) return;
    OpenPath(p);
}

// ============================== status / title / menu =======================

static void SetStatusParts()
{
    if (!g_status) return;
    int parts[6];
    parts[0] = 110;
    parts[1] = parts[0] + 160;
    parts[2] = parts[1] + 190;
    parts[3] = parts[2] + 210;
    parts[4] = parts[3] + 260;
    parts[5] = -1;
    SendMessageW(g_status, SB_SETPARTS, 6, (LPARAM)parts);
}

static void SetPart(int i, const std::wstring& s)
{
    SendMessageW(g_status, SB_SETTEXTW, (WPARAM)i, (LPARAM)s.c_str());
}

static void UpdateStatus()
{
    if (!g_status) return;
    if (!g_doc.Total()) {
        SetPart(0, L"—");
        SetPart(1, L"—");
        SetPart(2, L"—");
        SetPart(3, L"—");
        SetPart(4, L"—");
        SetPart(5, !g_toast.empty() ? g_toast
                  : Tr(L"Open a WAV file (Ctrl+O) to begin", L"撳 Ctrl+O 開個 WAV 檔嚟開始"));
        return;
    }

    wchar_t b[96];
    swprintf(b, 96, L"%u Hz", g_doc.sampleRate);
    SetPart(0, b);

    {
        wchar_t en[32], zh[32];
        swprintf(en, 32, L"%d ch", g_doc.Channels());
        swprintf(zh, 32, L"%d 聲道", g_doc.Channels());
        SetPart(1, Tr(en, zh));
    }
    {
        wchar_t en[48], zh[48];
        if (g_doc.srcFloat) {
            swprintf(en, 48, L"%d-bit float", g_doc.srcBits);
            swprintf(zh, 48, L"%d 位元浮點", g_doc.srcBits);
        } else {
            swprintf(en, 48, L"%d-bit PCM", g_doc.srcBits);
            swprintf(zh, 48, L"%d 位元 PCM", g_doc.srcBits);
        }
        SetPart(2, Tr(en, zh));
    }
    SetPart(3, Tr(L"Length ", L"長度 ") + FmtTime((double)g_doc.Total()));

    if (g_play.active) {
        std::wstring lab = g_play.paused ? Tr(L"Paused ", L"暫停咗 ")
                                         : Tr(L"Playing ", L"播緊 ");
        SetPart(4, lab + FmtTime((double)PlayPositionSamples()));
    } else {
        SetPart(4, Tr(L"Cursor ", L"游標 ") + FmtTime((double)g_cursor));
    }

    if (!g_toast.empty()) {
        SetPart(5, g_toast);
    } else if (HasSel()) {
        std::wstring s = Tr(L"Sel ", L"選取 ") + FmtTime((double)SelStart()) + L" – " +
                         FmtTime((double)SelEnd()) + L"  (" +
                         FmtTime((double)(SelEnd() - SelStart())) + L")";
        SetPart(5, s);
    } else {
        SetPart(5, Tr(L"No selection — drag to select", L"未揀範圍 — 拖吓嚟揀"));
    }
}

static void UpdateTitle()
{
    if (!g_hwnd) return;
    std::wstring t;
    switch (g_lang) {
    case Lang::En: t = L"WinForge Audio Editor"; break;
    case Lang::Zh: t = L"WinForge 音訊編輯器"; break;
    default:       t = L"WinForge Audio Editor · 音訊編輯器"; break;
    }
    if (!g_doc.path.empty()) t += L" — " + FileNameOf(g_doc.path);
    else if (g_doc.Total())  t += Tr(L" — (untitled)", L" —（未命名）");
    if (g_dirty) t += L" *";
    SetWindowTextW(g_hwnd, t.c_str());
}

static void BuildMenus()
{
    if (!g_hwnd) return;
    HMENU bar = CreateMenu();
    HMENU m;

    // --- File ---
    m = CreatePopupMenu();
    AppendMenuW(m, MF_STRING, CMD_OPEN, (Tr(L"&Open WAV…", L"開啟 WAV 檔…") + L"\tCtrl+O").c_str());
    AppendMenuW(m, MF_STRING, CMD_SAVE, (Tr(L"Save &As 16-bit WAV…", L"另存做 16-bit WAV…") + L"\tCtrl+S").c_str());
    AppendMenuW(m, MF_SEPARATOR, 0, nullptr);
    AppendMenuW(m, MF_STRING, CMD_EXIT, (Tr(L"E&xit", L"離開") + L"\tAlt+F4").c_str());
    AppendMenuW(bar, MF_POPUP, (UINT_PTR)m, Tr(L"&File", L"檔案").c_str());

    // --- Edit ---
    m = CreatePopupMenu();
    AppendMenuW(m, MF_STRING, CMD_UNDO, (Tr(L"&Undo", L"復原") + L"\tCtrl+Z").c_str());
    AppendMenuW(m, MF_STRING, CMD_REDO, (Tr(L"&Redo", L"重做") + L"\tCtrl+Y").c_str());
    AppendMenuW(m, MF_SEPARATOR, 0, nullptr);
    AppendMenuW(m, MF_STRING, CMD_CUT, (Tr(L"Cu&t", L"剪下") + L"\tCtrl+X").c_str());
    AppendMenuW(m, MF_STRING, CMD_COPY, (Tr(L"&Copy", L"複製") + L"\tCtrl+C").c_str());
    AppendMenuW(m, MF_STRING, CMD_PASTE, (Tr(L"&Paste", L"貼上") + L"\tCtrl+V").c_str());
    AppendMenuW(m, MF_STRING, CMD_DELETE, (Tr(L"&Delete selection", L"刪除選取") + L"\tDel").c_str());
    AppendMenuW(m, MF_STRING, CMD_TRIM, (Tr(L"Tri&m to selection", L"淨係留返選取範圍") + L"\tCtrl+T").c_str());
    AppendMenuW(m, MF_SEPARATOR, 0, nullptr);
    AppendMenuW(m, MF_STRING, CMD_SELALL, (Tr(L"Select &All", L"全選") + L"\tCtrl+A").c_str());
    AppendMenuW(m, MF_STRING, CMD_CLEARSEL, (Tr(L"C&lear selection", L"取消選取") + L"\tEsc").c_str());
    AppendMenuW(bar, MF_POPUP, (UINT_PTR)m, Tr(L"&Edit", L"編輯").c_str());

    // --- Effects ---
    m = CreatePopupMenu();
    AppendMenuW(m, MF_STRING, CMD_SILENCE, (Tr(L"&Silence", L"靜音") + L"\tCtrl+L").c_str());
    AppendMenuW(m, MF_STRING, CMD_FADEIN, Tr(L"Fade &In", L"淡入").c_str());
    AppendMenuW(m, MF_STRING, CMD_FADEOUT, Tr(L"Fade &Out", L"淡出").c_str());
    AppendMenuW(m, MF_SEPARATOR, 0, nullptr);
    AppendMenuW(m, MF_STRING, CMD_GAIN, (Tr(L"&Gain (dB)…", L"增益（分貝）…") + L"\tCtrl+G").c_str());
    AppendMenuW(m, MF_STRING, CMD_NORMALIZE, Tr(L"&Normalize to -1 dBFS", L"正規化到 -1 dBFS").c_str());
    AppendMenuW(m, MF_STRING, CMD_REVERSE, Tr(L"&Reverse", L"反轉").c_str());
    AppendMenuW(bar, MF_POPUP, (UINT_PTR)m, Tr(L"E&ffects", L"效果").c_str());

    // --- Transport ---
    m = CreatePopupMenu();
    AppendMenuW(m, MF_STRING, CMD_PLAYPAUSE, (Tr(L"&Play / Pause", L"播放／暫停") + L"\tSpace").c_str());
    AppendMenuW(m, MF_STRING, CMD_STOP, (Tr(L"&Stop", L"停止") + L"\tCtrl+.").c_str());
    AppendMenuW(bar, MF_POPUP, (UINT_PTR)m, Tr(L"&Transport", L"播放控制").c_str());

    // --- View ---
    m = CreatePopupMenu();
    AppendMenuW(m, MF_STRING, CMD_ZOOMIN, (Tr(L"Zoom &In", L"放大") + L"\tCtrl+=").c_str());
    AppendMenuW(m, MF_STRING, CMD_ZOOMOUT, (Tr(L"Zoom &Out", L"縮小") + L"\tCtrl+-").c_str());
    AppendMenuW(m, MF_STRING, CMD_ZOOMFIT, (Tr(L"&Fit whole file", L"成個檔案啱啱好") + L"\tCtrl+0").c_str());
    AppendMenuW(m, MF_STRING, CMD_ZOOMSEL, (Tr(L"Zoom to &Selection", L"放大到選取範圍") + L"\tCtrl+E").c_str());
    AppendMenuW(m, MF_SEPARATOR, 0, nullptr);
    AppendMenuW(m, MF_STRING, CMD_VUP, (Tr(L"&Vertical zoom in", L"垂直放大") + L"\tShift+Wheel").c_str());
    AppendMenuW(m, MF_STRING, CMD_VDOWN, Tr(L"Vertical zoom o&ut", L"垂直縮小").c_str());
    AppendMenuW(m, MF_STRING, CMD_VRESET, Tr(L"&Reset vertical zoom", L"重設垂直縮放").c_str());
    AppendMenuW(bar, MF_POPUP, (UINT_PTR)m, Tr(L"&View", L"檢視").c_str());

    // --- Language ---
    m = CreatePopupMenu();
    AppendMenuW(m, MF_STRING, CMD_LANG_EN, L"English");
    AppendMenuW(m, MF_STRING, CMD_LANG_ZH, L"中文（粵語）");
    AppendMenuW(m, MF_STRING, CMD_LANG_BOTH, L"Bilingual · 雙語");
    AppendMenuW(bar, MF_POPUP, (UINT_PTR)m, Tr(L"&Language", L"語言").c_str());

    // --- Help ---
    m = CreatePopupMenu();
    AppendMenuW(m, MF_STRING, CMD_ABOUT, (Tr(L"&About WinForge Audio Editor", L"關於 WinForge 音訊編輯器") + L"\tF1").c_str());
    AppendMenuW(bar, MF_POPUP, (UINT_PTR)m, Tr(L"&Help", L"說明").c_str());

    HMENU old = GetMenu(g_hwnd);
    SetMenu(g_hwnd, bar);
    if (old) DestroyMenu(old);

    UINT cur = (g_lang == Lang::En) ? CMD_LANG_EN
             : (g_lang == Lang::Zh) ? CMD_LANG_ZH : CMD_LANG_BOTH;
    CheckMenuRadioItem(bar, CMD_LANG_EN, CMD_LANG_BOTH, cur, MF_BYCOMMAND);
    DrawMenuBar(g_hwnd);
}

static void SetLanguage(Lang l)
{
    g_lang = l;
    BuildMenus();
    UpdateTitle();
    UpdateStatus();
    InvalidateWave();
}

static void ShowAbout()
{
    std::wstring t =
        L"WinForge Audio Editor · 音訊編輯器\n"
        L"AudioForge — " + Tr(L"offline waveform editor", L"離線波形編輯器") + L"\n\n" +
        Tr(L"Loads PCM 8/16/24/32-bit & float32/64 WAV; saves 16-bit PCM WAV.",
           L"讀 PCM 8/16/24/32 位元同 float32/64 WAV；儲存做 16 位元 PCM WAV。") + L"\n" +
        Tr(L"Cut, paste, trim, silence, fades, gain, normalize, reverse — all undoable.",
           L"剪切、貼上、修剪、靜音、淡入淡出、增益、正規化、反轉 — 全部可以復原。") + L"\n\n" +
        Tr(L"Shortcuts:", L"快捷鍵：") + L"\n"
        L"  Space — " + Tr(L"play / pause", L"播放／暫停") + L"\n"
        L"  Ctrl+O / Ctrl+S — " + Tr(L"open / save", L"開檔／儲存") + L"\n"
        L"  Ctrl+Z / Ctrl+Y — " + Tr(L"undo / redo", L"復原／重做") + L"\n"
        L"  Ctrl+A / Esc — " + Tr(L"select all / clear", L"全選／取消選取") + L"\n"
        L"  Ctrl+Wheel — " + Tr(L"zoom at cursor", L"喺游標度縮放") + L"\n"
        L"  Shift+Wheel — " + Tr(L"vertical zoom", L"垂直縮放") + L"\n\n" +
        Tr(L"Part of the WinForge suite. 100% offline — no network, no telemetry.",
           L"WinForge 系列成員。完全離線 — 冇網絡、冇遙測。");
    MessageBoxW(g_hwnd, t.c_str(),
                Tr(L"About", L"關於").c_str(), MB_OK | MB_ICONINFORMATION);
}

// ============================== painting ====================================

static int AmpToY(float v, int top, int h)
{
    double a = (double)v * g_vzoom;
    if (a > 1.0) a = 1.0;
    if (a < -1.0) a = -1.0;
    double mid = (double)top + (double)h * 0.5;
    return (int)std::llround(mid - a * ((double)h * 0.5 - 2.0));
}

static void FillRectCol(HDC dc, const RECT& r, COLORREF col)
{
    HBRUSH br = CreateSolidBrush(col);
    FillRect(dc, &r, br);
    DeleteObject(br);
}

static void DrawWaveLane(HDC dc, int c, const RECT& wr, int top, int h)
{
    size_t total = g_doc.Total();
    const std::vector<float>& chan = g_doc.ch[(size_t)c];
    int w = (int)(wr.right - wr.left);

    HPEN wavePen = CreatePen(PS_SOLID, 1, COL_WAVE);
    HGDIOBJ oldPen = SelectObject(dc, wavePen);
    HBRUSH waveBrush = CreateSolidBrush(COL_WAVE);

    if (g_spp >= 1.0) {
        static const std::vector<std::pair<float, float>> kEmptyPeaks;
        const std::vector<std::pair<float, float>>& pk =
            ((size_t)c < g_peaks.size()) ? g_peaks[(size_t)c] : kEmptyPeaks;
        for (int px = 0; px < w; px++) {
            double s0 = g_scroll + (double)px * g_spp;
            double s1 = s0 + g_spp;
            if (s1 <= 0) continue;
            if (s0 >= (double)total) break;
            size_t a = (size_t)(s0 < 0 ? 0 : s0);
            double bEnd = s1 + 1.0;
            if (bEnd > (double)total) bEnd = (double)total;
            size_t b = (size_t)bEnd;
            if (b <= a) b = a + 1;
            if (b > total) b = total;
            if (b <= a) continue;

            float mn = 1e9f, mx = -1e9f;
            if (b - a >= PEAK_BLOCK * 2 && !pk.empty()) {
                size_t b0 = a / PEAK_BLOCK;
                size_t b1 = (b - 1) / PEAK_BLOCK;
                if (b1 >= pk.size()) b1 = pk.size() - 1;
                for (size_t i = b0; i <= b1; i++) {
                    if (pk[i].first < mn) mn = pk[i].first;
                    if (pk[i].second > mx) mx = pk[i].second;
                }
            } else {
                for (size_t i = a; i < b; i++) {
                    float s = chan[i];
                    if (s < mn) mn = s;
                    if (s > mx) mx = s;
                }
            }
            if (mn > mx) continue;
            int y1 = AmpToY(mx, top, h);
            int y2 = AmpToY(mn, top, h);
            if (y2 <= y1) y2 = y1 + 1;
            MoveToEx(dc, (int)wr.left + px, y1, nullptr);
            LineTo(dc, (int)wr.left + px, y2);
        }
    } else {
        // sample-level: connected polyline plus dots
        double pxPerSample = 1.0 / g_spp;
        long long sFirst = (long long)std::floor(g_scroll) - 1;
        if (sFirst < 0) sFirst = 0;
        bool first = true;
        for (long long s = sFirst; s < (long long)total; s++) {
            int x = SampleToX((double)s);
            if (x > (int)wr.right + 2) break;
            int y = AmpToY(chan[(size_t)s], top, h);
            if (first) { MoveToEx(dc, x, y, nullptr); first = false; }
            else LineTo(dc, x, y);
            if (pxPerSample >= 6.0 && x >= (int)wr.left) {
                RECT dot = { x - 1, y - 1, x + 2, y + 2 };
                FillRect(dc, &dot, waveBrush);
            }
        }
    }

    SelectObject(dc, oldPen);
    DeleteObject(wavePen);
    DeleteObject(waveBrush);
}

static void PaintContent(HDC dc, const RECT& rcAll)
{
    FillRectCol(dc, rcAll, COL_BG);
    SetBkMode(dc, TRANSPARENT);

    RECT wr = WaveRect();
    size_t total = g_doc.Total();
    int nch = g_doc.Channels();

    // ruler + gutter backgrounds
    RECT rr = { wr.left, rcAll.top, rcAll.right, rcAll.top + RULER_H };
    FillRectCol(dc, rr, COL_RULER);
    RECT gr = { rcAll.left, rcAll.top, rcAll.left + GUTTER_W, wr.bottom };
    FillRectCol(dc, gr, COL_GUTTER);

    HPEN edgePen = CreatePen(PS_SOLID, 1, COL_EDGE);
    HGDIOBJ oldPen0 = SelectObject(dc, edgePen);
    MoveToEx(dc, (int)rcAll.left, (int)rr.bottom, nullptr);
    LineTo(dc, (int)rcAll.right, (int)rr.bottom);
    MoveToEx(dc, (int)rcAll.left + GUTTER_W, (int)rcAll.top, nullptr);
    LineTo(dc, (int)rcAll.left + GUTTER_W, (int)wr.bottom);
    SelectObject(dc, oldPen0);
    DeleteObject(edgePen);

    if (!total || nch == 0 || wr.right <= wr.left || wr.bottom <= wr.top) {
        SelectObject(dc, g_fontBig);
        SetTextColor(dc, COL_TEXTDIM);
        std::wstring h1 = Tr(L"WinForge Audio Editor", L"WinForge 音訊編輯器");
        std::wstring h2 = Tr(L"Open a WAV file to begin — Ctrl+O",
                             L"撳 Ctrl+O 開個 WAV 檔嚟開始");
        if (g_lang == Lang::Both) {
            h1 = L"WinForge Audio Editor · 音訊編輯器";
            h2 = L"Open a WAV file to begin · 撳 Ctrl+O 開個 WAV 檔嚟開始";
        }
        RECT t1 = wr; t1.bottom = (wr.top + wr.bottom) / 2;
        RECT t2 = wr; t2.top = t1.bottom;
        DrawTextW(dc, h1.c_str(), -1, &t1, DT_CENTER | DT_BOTTOM | DT_SINGLELINE);
        SelectObject(dc, g_font);
        DrawTextW(dc, h2.c_str(), -1, &t2, DT_CENTER | DT_TOP | DT_SINGLELINE);
        return;
    }

    double rate = (double)g_doc.sampleRate;

    // ---- time ruler ----
    if (HasSel()) {
        int x0 = SampleToX((double)SelStart());
        int x1 = SampleToX((double)SelEnd());
        if (x0 < (int)wr.left) x0 = (int)wr.left;
        if (x1 > (int)wr.right) x1 = (int)wr.right;
        if (x1 > x0) {
            RECT sr = { x0, rr.bottom - 8, x1, rr.bottom };
            FillRectCol(dc, sr, COL_SELRULER);
        }
    }
    {
        double secPerPx = g_spp / rate;
        static const double steps[] = { 0.001, 0.002, 0.005, 0.01, 0.02, 0.05,
                                        0.1, 0.2, 0.5, 1, 2, 5, 10, 15, 30,
                                        60, 120, 300, 600, 1800, 3600 };
        double step = 3600.0;
        for (double s : steps) {
            if (s / secPerPx >= 78.0) { step = s; break; }
        }
        HPEN tickPen = CreatePen(PS_SOLID, 1, COL_GRID);
        HGDIOBJ oldPen = SelectObject(dc, tickPen);
        SelectObject(dc, g_fontSm);
        SetTextColor(dc, COL_TEXTDIM);
        double t0 = g_scroll / rate;
        double first = std::floor(t0 / step) * step;
        int guard = 0;
        for (double t = first; guard < 600; t += step, guard++) {
            int x = SampleToX(t * rate);
            if (x > (int)wr.right) break;
            for (int k = 1; k < 5; k++) {
                int mx = SampleToX((t + step * (double)k / 5.0) * rate);
                if (mx >= (int)wr.left && mx <= (int)wr.right) {
                    MoveToEx(dc, mx, (int)rr.bottom - 4, nullptr);
                    LineTo(dc, mx, (int)rr.bottom);
                }
            }
            if (x < (int)wr.left) continue;
            MoveToEx(dc, x, (int)rr.bottom - 8, nullptr);
            LineTo(dc, x, (int)rr.bottom);
            std::wstring lb = FmtTime(t * rate);
            TextOutW(dc, x + 4, (int)rr.top + 4, lb.c_str(), (int)lb.size());
        }
        SelectObject(dc, oldPen);
        DeleteObject(tickPen);
    }

    // ---- lanes ----
    int hAll = (int)(wr.bottom - wr.top);
    int laneH = hAll / nch;
    if (laneH < 8) laneH = 8;

    HPEN gridPen = CreatePen(PS_SOLID, 1, COL_GRID);
    HPEN centerPen = CreatePen(PS_SOLID, 1, COL_CENTER);

    for (int c = 0; c < nch; c++) {
        int top = (int)wr.top + c * laneH;
        int bot = (c == nch - 1) ? (int)wr.bottom : top + laneH - 2;
        if (bot <= top) continue;
        int h = bot - top;

        RECT lr = { wr.left, top, wr.right, bot };
        FillRectCol(dc, lr, COL_LANE);

        // selection highlight
        if (HasSel()) {
            int x0 = SampleToX((double)SelStart());
            int x1 = SampleToX((double)SelEnd());
            if (x0 < (int)wr.left) x0 = (int)wr.left;
            if (x1 > (int)wr.right) x1 = (int)wr.right;
            if (x1 > x0) {
                RECT sr = { x0, top, x1, bot };
                FillRectCol(dc, sr, COL_SEL);
            }
        }

        // grid at ±0.5, center at 0
        HGDIOBJ oldPen = SelectObject(dc, gridPen);
        int yp = AmpToY(0.5f / (float)g_vzoom, top, h);
        MoveToEx(dc, (int)wr.left, yp, nullptr); LineTo(dc, (int)wr.right, yp);
        yp = AmpToY(-0.5f / (float)g_vzoom, top, h);
        MoveToEx(dc, (int)wr.left, yp, nullptr); LineTo(dc, (int)wr.right, yp);
        SelectObject(dc, centerPen);
        yp = AmpToY(0.0f, top, h);
        MoveToEx(dc, (int)wr.left, yp, nullptr); LineTo(dc, (int)wr.right, yp);
        SelectObject(dc, oldPen);

        // waveform
        DrawWaveLane(dc, c, wr, top, h);

        // amplitude gutter labels for this lane
        SelectObject(dc, g_fontSm);
        SetTextColor(dc, COL_TEXTDIM);
        static const float amps[5] = { 1.0f, 0.5f, 0.0f, -0.5f, -1.0f };
        for (int i = 0; i < 5; i++) {
            float shown = amps[i] / (float)g_vzoom;
            int y = AmpToY(shown, top, h);
            wchar_t lb[24];
            if (amps[i] == 0.0f) swprintf(lb, 24, L"0");
            else swprintf(lb, 24, L"%+.2f", (double)shown);
            RECT tr2 = { 2, y - 8, GUTTER_W - 8, y + 8 };
            if (tr2.top < top) { tr2.top = top; tr2.bottom = top + 16; }
            if (tr2.bottom > bot) { tr2.bottom = bot; tr2.top = bot - 16; }
            DrawTextW(dc, lb, -1, &tr2, DT_RIGHT | DT_VCENTER | DT_SINGLELINE);
            MoveToEx(dc, GUTTER_W - 5, y, nullptr);
            LineTo(dc, GUTTER_W, y);
        }
        // lane label L / R / ch#
        {
            std::wstring nm;
            if (nch == 1) nm = Tr(L"Mono", L"單聲道");
            else if (nch == 2) nm = (c == 0) ? L"L" : L"R";
            else { wchar_t nb[16]; swprintf(nb, 16, L"%d", c + 1); nm = nb; }
            SetTextColor(dc, COL_TEXT);
            TextOutW(dc, (int)wr.left + 6, top + 4, nm.c_str(), (int)nm.size());
        }
    }
    DeleteObject(gridPen);
    DeleteObject(centerPen);

    // ---- edit cursor line ----
    {
        int cx = SampleToX((double)g_cursor);
        if (cx >= (int)wr.left && cx <= (int)wr.right) {
            HPEN curPen = CreatePen(PS_SOLID, 1, COL_CURSOR);
            HGDIOBJ oldPen = SelectObject(dc, curPen);
            MoveToEx(dc, cx, (int)wr.top, nullptr);
            LineTo(dc, cx, (int)wr.bottom);
            // small marker in ruler
            MoveToEx(dc, cx, (int)rr.bottom - 10, nullptr);
            LineTo(dc, cx, (int)rr.bottom);
            SelectObject(dc, oldPen);
            DeleteObject(curPen);
        }
    }

    // ---- playback position line ----
    if (g_play.active) {
        size_t p = PlayPositionSamples();
        int px = SampleToX((double)p);
        if (px >= (int)wr.left && px <= (int)wr.right) {
            HPEN pp = CreatePen(PS_SOLID, 2, COL_PLAY);
            HGDIOBJ oldPen = SelectObject(dc, pp);
            MoveToEx(dc, px, (int)wr.top, nullptr);
            LineTo(dc, px, (int)wr.bottom);
            SelectObject(dc, oldPen);
            DeleteObject(pp);
        }
    }
}

// ============================== window proc =================================

static void OnSize(HWND hwnd)
{
    if (g_status) {
        SendMessageW(g_status, WM_SIZE, 0, 0);
        RECT sr; GetWindowRect(g_status, &sr);
        g_statusH = (int)(sr.bottom - sr.top);
        SetStatusParts();
    }
    RECT rc; GetClientRect(hwnd, &rc);
    g_scrollH = GetSystemMetrics(SM_CYHSCROLL);
    if (g_scrollbar)
        MoveWindow(g_scrollbar, 0, (int)rc.bottom - g_statusH - g_scrollH,
                   (int)rc.right, g_scrollH, TRUE);
    ClampView();
    UpdateScrollBar();
    InvalidateRect(hwnd, nullptr, FALSE);
}

static void OnHScroll(HWND, WPARAM wParam)
{
    SCROLLINFO si;
    ZeroMemory(&si, sizeof(si));
    si.cbSize = sizeof(si);
    si.fMask = SIF_ALL;
    GetScrollInfo(g_scrollbar, SB_CTL, &si);

    double total = (double)g_doc.Total();
    RECT wr = WaveRect();
    double vis = (double)(wr.right - wr.left) * g_spp;
    double maxScroll = total - vis;
    if (maxScroll < 0) maxScroll = 0;

    switch (LOWORD(wParam)) {
    case SB_LINELEFT:  g_scroll -= g_spp * 60.0; break;
    case SB_LINERIGHT: g_scroll += g_spp * 60.0; break;
    case SB_PAGELEFT:  g_scroll -= vis * 0.9; break;
    case SB_PAGERIGHT: g_scroll += vis * 0.9; break;
    case SB_THUMBTRACK:
    case SB_THUMBPOSITION: {
        int span = si.nMax - (int)si.nPage + 1;
        if (span > 0) g_scroll = maxScroll * ((double)si.nTrackPos / (double)span);
        break;
    }
    case SB_LEFT:  g_scroll = 0; break;
    case SB_RIGHT: g_scroll = maxScroll; break;
    default: return;
    }
    ClampView();
    UpdateScrollBar();
    InvalidateWave();
}

static void OnCommand(HWND hwnd, UINT id)
{
    switch (id) {
    case CMD_OPEN:      CmdOpen(); break;
    case CMD_SAVE:      CmdSaveAs(); break;
    case CMD_EXIT:      PostMessageW(hwnd, WM_CLOSE, 0, 0); break;

    case CMD_UNDO:      DoUndo(); break;
    case CMD_REDO:      DoRedo(); break;
    case CMD_CUT:       DoCut(); break;
    case CMD_COPY:      DoCopy(); break;
    case CMD_PASTE:     DoPaste(); break;
    case CMD_DELETE:    DoDelete(); break;
    case CMD_TRIM:      DoTrim(); break;
    case CMD_SELALL:    SelectAll(); break;
    case CMD_CLEARSEL:  ClearSelection(); break;

    case CMD_SILENCE:   DoSilence(); break;
    case CMD_FADEIN:    DoFade(true); break;
    case CMD_FADEOUT:   DoFade(false); break;
    case CMD_GAIN:      DoGain(); break;
    case CMD_NORMALIZE: DoNormalize(); break;
    case CMD_REVERSE:   DoReverse(); break;

    case CMD_PLAYPAUSE: TogglePlay(); break;
    case CMD_STOP:      StopPlayback(); break;

    case CMD_ZOOMIN: {
        RECT wr = WaveRect();
        ZoomAt((int)(wr.left + wr.right) / 2, 1.0 / 1.6);
        break;
    }
    case CMD_ZOOMOUT: {
        RECT wr = WaveRect();
        ZoomAt((int)(wr.left + wr.right) / 2, 1.6);
        break;
    }
    case CMD_ZOOMFIT:   ZoomFit(); break;
    case CMD_ZOOMSEL:   ZoomToSelection(); break;
    case CMD_VUP:       g_vzoom *= 1.5; ClampView(); InvalidateWave(); break;
    case CMD_VDOWN:     g_vzoom /= 1.5; ClampView(); InvalidateWave(); break;
    case CMD_VRESET:    g_vzoom = 1.0; InvalidateWave(); break;

    case CMD_LANG_EN:   SetLanguage(Lang::En); break;
    case CMD_LANG_ZH:   SetLanguage(Lang::Zh); break;
    case CMD_LANG_BOTH: SetLanguage(Lang::Both); break;

    case CMD_ABOUT:     ShowAbout(); break;

    case CMD_CURLEFT:   MoveCursor(-1, false); break;
    case CMD_CURRIGHT:  MoveCursor(1, false); break;
    case CMD_SELLEFT:   MoveCursor(-1, true); break;
    case CMD_SELRIGHT:  MoveCursor(1, true); break;
    case CMD_HOME:
        if (g_doc.Total()) {
            g_selA = g_selB = g_cursor = 0;
            g_scroll = 0;
            ClampView(); UpdateScrollBar(); InvalidateWave(); UpdateStatus();
        }
        break;
    case CMD_END:
        if (g_doc.Total()) {
            g_selA = g_selB = g_cursor = g_doc.Total();
            EnsureVisible(g_cursor);
            InvalidateWave(); UpdateStatus();
        }
        break;
    }
}

static void OnInitMenuPopup(HWND hwnd)
{
    HMENU mm = GetMenu(hwnd);
    if (!mm) return;
    auto en = [&](UINT id, bool e) {
        EnableMenuItem(mm, id, MF_BYCOMMAND | (e ? MF_ENABLED : MF_GRAYED));
    };
    bool has = g_doc.Total() > 0;
    bool sel = HasSel();
    en(CMD_SAVE, has);
    en(CMD_UNDO, !g_undo.empty());
    en(CMD_REDO, !g_redo.empty());
    en(CMD_CUT, sel);
    en(CMD_COPY, sel);
    en(CMD_PASTE, !g_clip.Empty());
    en(CMD_DELETE, sel);
    en(CMD_TRIM, sel);
    en(CMD_SELALL, has);
    en(CMD_CLEARSEL, sel);
    en(CMD_SILENCE, has);
    en(CMD_FADEIN, has);
    en(CMD_FADEOUT, has);
    en(CMD_GAIN, has);
    en(CMD_NORMALIZE, has);
    en(CMD_REVERSE, has);
    en(CMD_PLAYPAUSE, has);
    en(CMD_STOP, g_play.active);
    en(CMD_ZOOMIN, has);
    en(CMD_ZOOMOUT, has);
    en(CMD_ZOOMFIT, has);
    en(CMD_ZOOMSEL, sel);
}

static LRESULT CALLBACK MainProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    switch (msg) {
    case WM_CREATE: {
        g_hwnd = hwnd;
        g_font = CreateFontW(-15, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE,
            DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
            CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI");
        g_fontSm = CreateFontW(-12, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE,
            DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
            CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI");
        g_fontBig = CreateFontW(-22, 0, 0, 0, FW_SEMIBOLD, FALSE, FALSE, FALSE,
            DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
            CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI");

        g_status = CreateWindowExW(0, L"msctls_statusbar32", L"",
            WS_CHILD | WS_VISIBLE | SBARS_SIZEGRIP,
            0, 0, 0, 0, hwnd, (HMENU)(INT_PTR)1, g_hinst, nullptr);
        SendMessageW(g_status, WM_SETFONT, (WPARAM)g_font, TRUE);
        RECT sr; GetWindowRect(g_status, &sr);
        g_statusH = (int)(sr.bottom - sr.top);
        SetStatusParts();

        g_scrollH = GetSystemMetrics(SM_CYHSCROLL);
        g_scrollbar = CreateWindowExW(0, L"SCROLLBAR", L"",
            WS_CHILD | WS_VISIBLE | SBS_HORZ,
            0, 0, 10, g_scrollH, hwnd, (HMENU)(INT_PTR)2, g_hinst, nullptr);

        BuildMenus();
        UpdateTitle();
        UpdateStatus();
        UpdateScrollBar();
        DragAcceptFiles(hwnd, TRUE);
        return 0;
    }

    case WM_SIZE:
        OnSize(hwnd);
        return 0;

    case WM_GETMINMAXINFO: {
        MINMAXINFO* mmi = (MINMAXINFO*)lParam;
        mmi->ptMinTrackSize.x = 720;
        mmi->ptMinTrackSize.y = 420;
        return 0;
    }

    case WM_ERASEBKGND:
        return 1;

    case WM_PAINT: {
        PAINTSTRUCT ps;
        HDC hdc = BeginPaint(hwnd, &ps);
        RECT rc; GetClientRect(hwnd, &rc);
        rc.bottom -= (g_statusH + g_scrollH);
        if (rc.bottom < rc.top) rc.bottom = rc.top;
        int w = (int)rc.right, h = (int)rc.bottom;
        if (w < 1) w = 1;
        if (h < 1) h = 1;
        HDC mem = CreateCompatibleDC(hdc);
        HBITMAP bmp = CreateCompatibleBitmap(hdc, w, h);
        HGDIOBJ oldBmp = SelectObject(mem, bmp);
        HGDIOBJ oldFont = SelectObject(mem, g_font);
        RECT full = { 0, 0, w, h };
        PaintContent(mem, full);
        BitBlt(hdc, 0, 0, w, h, mem, 0, 0, SRCCOPY);
        SelectObject(mem, oldFont);
        SelectObject(mem, oldBmp);
        DeleteObject(bmp);
        DeleteDC(mem);
        EndPaint(hwnd, &ps);
        return 0;
    }

    case WM_HSCROLL:
        if ((HWND)lParam == g_scrollbar) OnHScroll(hwnd, wParam);
        return 0;

    case WM_MOUSEWHEEL: {
        POINT pt = { GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam) };
        ScreenToClient(hwnd, &pt);
        short delta = GET_WHEEL_DELTA_WPARAM(wParam);
        WORD keys = GET_KEYSTATE_WPARAM(wParam);
        if (keys & MK_CONTROL) {
            ZoomAt(pt.x, delta > 0 ? 1.0 / 1.3 : 1.3);
        } else if (keys & MK_SHIFT) {
            g_vzoom *= (delta > 0) ? 1.25 : 1.0 / 1.25;
            ClampView();
            InvalidateWave();
        } else {
            RECT wr = WaveRect();
            double vis = (double)(wr.right - wr.left) * g_spp;
            g_scroll -= ((double)delta / 120.0) * vis * 0.12;
            ClampView();
            UpdateScrollBar();
            InvalidateWave();
        }
        return 0;
    }

    case WM_LBUTTONDOWN: {
        SetFocus(hwnd);
        if (!g_doc.Total()) return 0;
        int x = GET_X_LPARAM(lParam);
        int y = GET_Y_LPARAM(lParam);
        RECT wr = WaveRect();
        if (y >= (int)wr.bottom || x < (int)wr.left - GUTTER_W) return 0;
        if (x < (int)wr.left) x = (int)wr.left;
        if (x > (int)wr.right) x = (int)wr.right;
        size_t s = ClampSample(XToSample(x));
        bool shift = (wParam & MK_SHIFT) != 0;
        if (shift) {
            if (g_selA == g_selB) g_selA = g_cursor;
            g_selB = s;
        } else {
            g_selA = g_selB = s;
        }
        g_cursor = s;
        g_dragging = true;
        SetCapture(hwnd);
        InvalidateWave();
        UpdateStatus();
        return 0;
    }

    case WM_MOUSEMOVE: {
        if (!g_dragging) return 0;
        int x = GET_X_LPARAM(lParam);
        RECT wr = WaveRect();
        if (x > (int)wr.right) {
            g_scroll += (double)(x - (int)wr.right) * g_spp * 0.5;
            x = (int)wr.right;
        } else if (x < (int)wr.left) {
            g_scroll -= (double)((int)wr.left - x) * g_spp * 0.5;
            x = (int)wr.left;
        }
        ClampView();
        UpdateScrollBar();
        size_t s = ClampSample(XToSample(x));
        g_selB = s;
        g_cursor = s;
        InvalidateWave();
        UpdateStatus();
        return 0;
    }

    case WM_LBUTTONUP:
        if (g_dragging) {
            g_dragging = false;
            ReleaseCapture();
            InvalidateWave();
            UpdateStatus();
        }
        return 0;

    case WM_CAPTURECHANGED:
        g_dragging = false;
        return 0;

    case WM_LBUTTONDBLCLK:
        SelectAll();
        return 0;

    case WM_COMMAND:
        OnCommand(hwnd, LOWORD(wParam));
        return 0;

    case WM_INITMENUPOPUP:
        OnInitMenuPopup(hwnd);
        return 0;

    case WM_TIMER:
        if (wParam == TIMER_PLAY) {
            if (g_play.active) {
                if (!g_dragging && !g_play.paused) {
                    size_t p = PlayPositionSamples();
                    RECT wr = WaveRect();
                    double vis = (double)(wr.right - wr.left) * g_spp;
                    if ((double)p > g_scroll + vis) {
                        g_scroll = (double)p - vis * 0.1;
                        ClampView();
                        UpdateScrollBar();
                    }
                }
                InvalidateWave();
                UpdateStatus();
            } else {
                KillTimer(hwnd, TIMER_PLAY);
            }
        } else if (wParam == TIMER_TOAST) {
            KillTimer(hwnd, TIMER_TOAST);
            g_toast.clear();
            UpdateStatus();
        }
        return 0;

    case MM_WOM_DONE: {
        if (!g_play.dev || (HWAVEOUT)wParam != g_play.dev) return 0;
        g_play.inFlight--;
        WAVEHDR* ph = (WAVEHDR*)lParam;
        int idx = -1;
        for (int i = 0; i < Player::NBUF; i++)
            if (&g_play.hdr[i] == ph) { idx = i; break; }
        if (g_play.active && idx >= 0) {
            if (!QueueBuffer(idx) && g_play.inFlight <= 0)
                StopPlayback();     // natural end of region
        }
        return 0;
    }

    case WM_DROPFILES: {
        HDROP drop = (HDROP)wParam;
        wchar_t buf[1024]; buf[0] = 0;
        DragQueryFileW(drop, 0, buf, 1024);
        DragFinish(drop);
        if (buf[0] && ConfirmDiscard()) OpenPath(buf);
        return 0;
    }

    case WM_CLOSE:
        if (!ConfirmDiscard()) return 0;
        DestroyWindow(hwnd);
        return 0;

    case WM_DESTROY:
        StopPlayback();
        if (g_font)    { DeleteObject(g_font);    g_font = nullptr; }
        if (g_fontSm)  { DeleteObject(g_fontSm);  g_fontSm = nullptr; }
        if (g_fontBig) { DeleteObject(g_fontBig); g_fontBig = nullptr; }
        PostQuitMessage(0);
        return 0;
    }
    return DefWindowProcW(hwnd, msg, wParam, lParam);
}

// ============================== entry point =================================

extern "C" int WINAPI wWinMain(HINSTANCE hInst, HINSTANCE, LPWSTR lpCmd, int nShow)
{
    g_hinst = hInst;
    SetProcessDPIAware();
    CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);

    INITCOMMONCONTROLSEX icc;
    icc.dwSize = sizeof(icc);
    icc.dwICC = ICC_BAR_CLASSES | ICC_WIN95_CLASSES;
    InitCommonControlsEx(&icc);

    WNDCLASSEXW wc;
    ZeroMemory(&wc, sizeof(wc));
    wc.cbSize = sizeof(wc);
    wc.style = CS_DBLCLKS;
    wc.lpfnWndProc = MainProc;
    wc.hInstance = hInst;
    wc.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    wc.hIcon = LoadIconW(nullptr, IDI_APPLICATION);
    wc.hIconSm = LoadIconW(nullptr, IDI_APPLICATION);
    wc.lpszClassName = L"WinForgeAudioEditorMain";
    if (!RegisterClassExW(&wc)) return 1;

    HWND hwnd = CreateWindowExW(0, wc.lpszClassName,
        L"WinForge Audio Editor · 音訊編輯器",
        WS_OVERLAPPEDWINDOW | WS_CLIPCHILDREN,
        CW_USEDEFAULT, CW_USEDEFAULT, 1240, 760,
        nullptr, nullptr, hInst, nullptr);
    if (!hwnd) return 1;

    static const ACCEL accels[] = {
        { FCONTROL | FVIRTKEY, 'O',          CMD_OPEN },
        { FCONTROL | FVIRTKEY, 'S',          CMD_SAVE },
        { FCONTROL | FVIRTKEY, 'Z',          CMD_UNDO },
        { FCONTROL | FVIRTKEY, 'Y',          CMD_REDO },
        { FCONTROL | FVIRTKEY, 'X',          CMD_CUT },
        { FCONTROL | FVIRTKEY, 'C',          CMD_COPY },
        { FCONTROL | FVIRTKEY, 'V',          CMD_PASTE },
        { FVIRTKEY,            VK_DELETE,    CMD_DELETE },
        { FCONTROL | FVIRTKEY, 'T',          CMD_TRIM },
        { FCONTROL | FVIRTKEY, 'A',          CMD_SELALL },
        { FVIRTKEY,            VK_ESCAPE,    CMD_CLEARSEL },
        { FVIRTKEY,            VK_SPACE,     CMD_PLAYPAUSE },
        { FCONTROL | FVIRTKEY, VK_OEM_PERIOD, CMD_STOP },
        { FCONTROL | FVIRTKEY, 'L',          CMD_SILENCE },
        { FCONTROL | FVIRTKEY, 'G',          CMD_GAIN },
        { FCONTROL | FVIRTKEY, VK_OEM_PLUS,  CMD_ZOOMIN },
        { FCONTROL | FVIRTKEY, VK_OEM_MINUS, CMD_ZOOMOUT },
        { FCONTROL | FVIRTKEY, '0',          CMD_ZOOMFIT },
        { FCONTROL | FVIRTKEY, 'E',          CMD_ZOOMSEL },
        { FVIRTKEY,            VK_LEFT,      CMD_CURLEFT },
        { FVIRTKEY,            VK_RIGHT,     CMD_CURRIGHT },
        { FSHIFT | FVIRTKEY,   VK_LEFT,      CMD_SELLEFT },
        { FSHIFT | FVIRTKEY,   VK_RIGHT,     CMD_SELRIGHT },
        { FVIRTKEY,            VK_HOME,      CMD_HOME },
        { FVIRTKEY,            VK_END,       CMD_END },
        { FVIRTKEY,            VK_F1,        CMD_ABOUT },
    };
    HACCEL hacc = CreateAcceleratorTableW((LPACCEL)accels,
                                          (int)(sizeof(accels) / sizeof(accels[0])));

    ShowWindow(hwnd, nShow);
    UpdateWindow(hwnd);

    // open file from command line if given
    if (lpCmd && *lpCmd) {
        std::wstring p = lpCmd;
        while (!p.empty() && (p.front() == L'"' || p.front() == L' ')) p.erase(p.begin());
        while (!p.empty() && (p.back() == L'"' || p.back() == L' ')) p.pop_back();
        if (!p.empty()) OpenPath(p);
    }

    MSG msg;
    while (GetMessageW(&msg, nullptr, 0, 0) > 0) {
        if (!TranslateAcceleratorW(hwnd, hacc, &msg)) {
            TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }
    }
    if (hacc) DestroyAcceleratorTable(hacc);
    CoUninitialize();
    return (int)msg.wParam;
}
