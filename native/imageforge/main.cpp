// ============================================================================
//  WinForge Image Editor · 影像編輯器  ("ImageForge")
//  ---------------------------------------------------------------------------
//  A single-file Win32/GDI+ raster editor shipped with WinForge.
//  Fully offline and bilingual: English + 繁體中文（粵語）.
//
//  Build (MSVC):
//    cl /nologo /EHsc /O2 /std:c++17 /DUNICODE /D_UNICODE main.cpp ^
//       /Fe:WinForgeImageEditor.exe /link gdiplus.lib gdi32.lib user32.lib ^
//       comdlg32.lib ole32.lib shell32.lib comctl32.lib /SUBSYSTEM:WINDOWS
//  Build (MinGW):
//    g++ -std=c++17 -O2 -municode -mwindows main.cpp ^
//       -o WinForgeImageEditor.exe -lgdiplus -lgdi32 -lcomdlg32 -lole32 ^
//       -lshell32 -lcomctl32
//
//  No resources, CDN, network, telemetry, or upstream executable.
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
#include <commdlg.h>
#include <commctrl.h>
#include <shellapi.h>
#include <objbase.h>
#include <gdiplus.h>

#include <algorithm>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cwchar>
#include <cwctype>
#include <deque>
#include <limits>
#include <new>
#include <string>
#include <utility>
#include <vector>

#ifdef _MSC_VER
#pragma comment(lib, "gdiplus.lib")
#pragma comment(lib, "gdi32.lib")
#pragma comment(lib, "user32.lib")
#pragma comment(lib, "comdlg32.lib")
#pragma comment(lib, "ole32.lib")
#pragma comment(lib, "shell32.lib")
#pragma comment(lib, "comctl32.lib")
#endif

using namespace Gdiplus;

// ============================== commands ====================================

enum : UINT {
    CMD_NEW = 101, CMD_OPEN, CMD_SAVE, CMD_SAVEAS, CMD_EXIT,
    CMD_UNDO, CMD_REDO, CMD_SELECTALL, CMD_CLEARSEL, CMD_CLEARPIXELS,
    CMD_TOOL_SELECT, CMD_TOOL_BRUSH, CMD_TOOL_ERASER, CMD_TOOL_FILL,
    CMD_BRUSH_COLOR, CMD_BRUSH_SIZE,
    CMD_CROP, CMD_RESIZE, CMD_ROTATE_CW, CMD_ROTATE_CCW,
    CMD_FLIP_H, CMD_FLIP_V,
    CMD_BRIGHTNESS, CMD_CONTRAST, CMD_SATURATION, CMD_GAMMA, CMD_AUTOLEVELS,
    CMD_GRAYSCALE, CMD_INVERT, CMD_SEPIA, CMD_BLUR, CMD_SHARPEN, CMD_EDGES,
    CMD_ZOOMIN, CMD_ZOOMOUT, CMD_ZOOM100, CMD_ZOOMFIT,
    CMD_LANG_EN, CMD_LANG_ZH, CMD_LANG_BOTH, CMD_ABOUT
};

enum : UINT_PTR { TIMER_TOAST = 1 };

static const int TOOLBAR_H = 50;
static const int STATUS_H = 27;
static const std::size_t MAX_PIXELS = 32000000ULL;       // 128 MiB ARGB image
static const std::size_t HISTORY_LIMIT = 384ULL * 1024ULL * 1024ULL;
static const std::size_t HISTORY_STEPS = 16;

// ============================== localization ================================

enum class Lang { En, Zh, Both };
enum class Tool { Select, Brush, Eraser, Fill };

static Lang g_lang = Lang::Both;
static Tool g_tool = Tool::Select;

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
    int width = 0;
    int height = 0;
    std::vector<std::uint32_t> pixels; // straight-alpha ARGB (memory bytes BGRA)
    std::wstring path;
    bool dirty = false;

    bool Valid() const
    {
        return width > 0 && height > 0 &&
            pixels.size() == static_cast<std::size_t>(width) * static_cast<std::size_t>(height);
    }
};

struct Snapshot {
    int width = 0;
    int height = 0;
    std::vector<std::uint32_t> pixels;
    RECT selection{ 0, 0, 0, 0 };
    bool dirty = false;
};

struct ToolbarItem {
    UINT command = 0;
    RECT rect{ 0, 0, 0, 0 };
    const wchar_t* en = L"";
    const wchar_t* zh = L"";
    int width = 80;
};

static HINSTANCE g_hinst = nullptr;
static HWND g_hwnd = nullptr;
static HFONT g_font = nullptr;
static HFONT g_fontSmall = nullptr;
static ULONG_PTR g_gdiplusToken = 0;

static Document g_doc;
static std::deque<Snapshot> g_undo;
static std::deque<Snapshot> g_redo;
static RECT g_selection{ 0, 0, 0, 0 }; // normalized, right/bottom exclusive
static std::vector<ToolbarItem> g_toolbar;

static double g_zoom = 1.0;
static int g_scrollX = 0;
static int g_scrollY = 0;
static bool g_fitMode = true;

static bool g_selecting = false;
static POINT g_selectAnchor{ 0, 0 };
static bool g_painting = false;
static POINT g_lastPaint{ 0, 0 };
static bool g_panning = false;
static POINT g_lastPan{ 0, 0 };
static POINT g_mouse{ -1, -1 };
static UINT g_hoverCommand = 0;

static COLORREF g_brushColor = RGB(240, 140, 60);
static int g_brushSize = 16;
static std::wstring g_toast;

// ============================== declarations ================================

static LRESULT CALLBACK MainProc(HWND, UINT, WPARAM, LPARAM);
static void BuildMenus();
static void UpdateMenuState();
static void UpdateTitle();
static void UpdateScrollbars();
static void ZoomFit();
static void InvalidateAll();
static bool SaveDocument(bool saveAs);
static bool OpenPath(const std::wstring& path);
static bool ConfirmDestructiveAction();
static void DispatchCommand(UINT command);

// ============================== small helpers ===============================

static int ClampByte(int v) { return v < 0 ? 0 : (v > 255 ? 255 : v); }
static int A(std::uint32_t p) { return static_cast<int>((p >> 24) & 0xff); }
static int R(std::uint32_t p) { return static_cast<int>((p >> 16) & 0xff); }
static int G(std::uint32_t p) { return static_cast<int>((p >> 8) & 0xff); }
static int B(std::uint32_t p) { return static_cast<int>(p & 0xff); }

static std::uint32_t MakeArgb(int a, int r, int g, int b)
{
    return (static_cast<std::uint32_t>(ClampByte(a)) << 24) |
           (static_cast<std::uint32_t>(ClampByte(r)) << 16) |
           (static_cast<std::uint32_t>(ClampByte(g)) << 8) |
           static_cast<std::uint32_t>(ClampByte(b));
}

static std::uint32_t BrushArgb()
{
    return MakeArgb(255, GetRValue(g_brushColor), GetGValue(g_brushColor), GetBValue(g_brushColor));
}

static RECT NormalizedRect(RECT r)
{
    if (r.left > r.right) std::swap(r.left, r.right);
    if (r.top > r.bottom) std::swap(r.top, r.bottom);
    return r;
}

static bool HasSelection()
{
    RECT r = NormalizedRect(g_selection);
    return g_doc.Valid() && r.right > r.left && r.bottom > r.top;
}

static void ClearSelection()
{
    SetRectEmpty(&g_selection);
    InvalidateAll();
}

static std::wstring FileNameOnly(const std::wstring& path)
{
    const std::size_t p = path.find_last_of(L"\\/");
    return p == std::wstring::npos ? path : path.substr(p + 1);
}

static std::wstring LowerExtension(const std::wstring& path)
{
    const std::size_t slash = path.find_last_of(L"\\/");
    const std::size_t dot = path.find_last_of(L'.');
    if (dot == std::wstring::npos || (slash != std::wstring::npos && dot < slash)) return L"";
    std::wstring ext = path.substr(dot);
    std::transform(ext.begin(), ext.end(), ext.begin(),
        [](wchar_t c) { return static_cast<wchar_t>(towlower(c)); });
    return ext;
}

static void Toast(const std::wstring& message)
{
    g_toast = message;
    if (g_hwnd) {
        KillTimer(g_hwnd, TIMER_TOAST);
        SetTimer(g_hwnd, TIMER_TOAST, 2800, nullptr);
        InvalidateRect(g_hwnd, nullptr, FALSE);
    }
}

static void ErrorBox(const std::wstring& message)
{
    MessageBoxW(g_hwnd, message.c_str(), Tr(L"ImageForge error", L"ImageForge 錯誤").c_str(),
        MB_OK | MB_ICONERROR);
}

static RECT CanvasRect()
{
    RECT r{};
    if (g_hwnd) GetClientRect(g_hwnd, &r);
    r.top += TOOLBAR_H;
    r.bottom = (std::max)(r.top, r.bottom - STATUS_H);
    return r;
}

static void ImagePlacement(int& x, int& y, int& w, int& h)
{
    const RECT c = CanvasRect();
    w = g_doc.Valid() ? (std::max)(1, static_cast<int>(std::lround(g_doc.width * g_zoom))) : 0;
    h = g_doc.Valid() ? (std::max)(1, static_cast<int>(std::lround(g_doc.height * g_zoom))) : 0;
    const int cw = (std::max)(0, static_cast<int>(c.right - c.left));
    const int ch = (std::max)(0, static_cast<int>(c.bottom - c.top));
    x = c.left + (w < cw ? (cw - w) / 2 : -g_scrollX);
    y = c.top + (h < ch ? (ch - h) / 2 : -g_scrollY);
}

static bool ScreenToImage(POINT screen, POINT& image, bool clampToImage)
{
    if (!g_doc.Valid()) return false;
    int ox, oy, dw, dh;
    ImagePlacement(ox, oy, dw, dh);
    if (!clampToImage &&
        (screen.x < ox || screen.y < oy || screen.x >= ox + dw || screen.y >= oy + dh))
        return false;

    int ix = static_cast<int>(std::floor((screen.x - ox) / g_zoom));
    int iy = static_cast<int>(std::floor((screen.y - oy) / g_zoom));
    if (clampToImage) {
        ix = (std::max)(0, (std::min)(g_doc.width - 1, ix));
        iy = (std::max)(0, (std::min)(g_doc.height - 1, iy));
    }
    if (ix < 0 || iy < 0 || ix >= g_doc.width || iy >= g_doc.height) return false;
    image = POINT{ ix, iy };
    return true;
}

static void InvalidateAll()
{
    if (g_hwnd) InvalidateRect(g_hwnd, nullptr, FALSE);
}

static std::size_t HistoryBytes(const std::deque<Snapshot>& history)
{
    std::size_t n = 0;
    for (const Snapshot& s : history) n += s.pixels.size() * sizeof(std::uint32_t);
    return n;
}

static void TrimHistory(std::deque<Snapshot>& history)
{
    while (history.size() > 1 &&
        (history.size() > HISTORY_STEPS || HistoryBytes(history) > HISTORY_LIMIT))
        history.pop_front();
}

static Snapshot CaptureSnapshot()
{
    Snapshot s;
    s.width = g_doc.width;
    s.height = g_doc.height;
    s.pixels = g_doc.pixels;
    s.selection = g_selection;
    s.dirty = g_doc.dirty;
    return s;
}

static bool PushUndo()
{
    if (!g_doc.Valid()) return false;
    try {
        g_undo.push_back(CaptureSnapshot());
        TrimHistory(g_undo);
        g_redo.clear();
        return true;
    }
    catch (const std::bad_alloc&) {
        ErrorBox(Tr(L"Not enough memory to create an undo snapshot.",
            L"記憶體不足，無法建立復原快照。"));
        return false;
    }
}

static void RestoreSnapshot(Snapshot&& s)
{
    g_doc.width = s.width;
    g_doc.height = s.height;
    g_doc.pixels = std::move(s.pixels);
    g_doc.dirty = s.dirty;
    g_selection = s.selection;
    UpdateScrollbars();
    UpdateTitle();
    UpdateMenuState();
    InvalidateAll();
}

static void Undo()
{
    if (g_undo.empty() || !g_doc.Valid()) return;
    try {
        g_redo.push_back(CaptureSnapshot());
        TrimHistory(g_redo);
        Snapshot s = std::move(g_undo.back());
        g_undo.pop_back();
        RestoreSnapshot(std::move(s));
        Toast(Tr(L"Undo", L"已復原"));
    }
    catch (const std::bad_alloc&) {
        ErrorBox(Tr(L"Not enough memory to undo.", L"記憶體不足，無法復原。"));
    }
}

static void Redo()
{
    if (g_redo.empty() || !g_doc.Valid()) return;
    try {
        g_undo.push_back(CaptureSnapshot());
        TrimHistory(g_undo);
        Snapshot s = std::move(g_redo.back());
        g_redo.pop_back();
        RestoreSnapshot(std::move(s));
        Toast(Tr(L"Redo", L"已重做"));
    }
    catch (const std::bad_alloc&) {
        ErrorBox(Tr(L"Not enough memory to redo.", L"記憶體不足，無法重做。"));
    }
}

static void MarkChanged(const wchar_t* en, const wchar_t* zh)
{
    g_doc.dirty = true;
    UpdateTitle();
    UpdateMenuState();
    UpdateScrollbars();
    InvalidateAll();
    Toast(Tr(en, zh));
}

// ============================== numeric prompt ==============================

struct PromptState {
    std::wstring title;
    std::wstring label1;
    std::wstring label2;
    int value1 = 0;
    int value2 = 0;
    int minValue = 0;
    int maxValue = 0;
    bool twoValues = false;
    bool accepted = false;
    HWND edit1 = nullptr;
    HWND edit2 = nullptr;
};

static bool ParseEditInt(HWND edit, int& value)
{
    wchar_t text[64]{};
    GetWindowTextW(edit, text, 63);
    wchar_t* end = nullptr;
    const long parsed = wcstol(text, &end, 10);
    while (end && *end && iswspace(*end)) ++end;
    if (end == text || (end && *end)) return false;
    if (parsed < std::numeric_limits<int>::min() || parsed > std::numeric_limits<int>::max()) return false;
    value = static_cast<int>(parsed);
    return true;
}

static LRESULT CALLBACK PromptProc(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp)
{
    PromptState* state = reinterpret_cast<PromptState*>(GetWindowLongPtrW(hwnd, GWLP_USERDATA));
    if (msg == WM_NCCREATE) {
        const CREATESTRUCTW* cs = reinterpret_cast<const CREATESTRUCTW*>(lp);
        state = reinterpret_cast<PromptState*>(cs->lpCreateParams);
        SetWindowLongPtrW(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(state));
    }

    switch (msg) {
    case WM_CREATE:
        if (state) {
            const int labelW = 142;
            CreateWindowW(L"STATIC", state->label1.c_str(), WS_CHILD | WS_VISIBLE,
                18, 22, labelW, 24, hwnd, nullptr, g_hinst, nullptr);
            state->edit1 = CreateWindowExW(WS_EX_CLIENTEDGE, L"EDIT", L"",
                WS_CHILD | WS_VISIBLE | WS_TABSTOP | ES_AUTOHSCROLL,
                168, 18, 120, 26, hwnd, reinterpret_cast<HMENU>(1001), g_hinst, nullptr);
            if (state->twoValues) {
                CreateWindowW(L"STATIC", state->label2.c_str(), WS_CHILD | WS_VISIBLE,
                    18, 58, labelW, 24, hwnd, nullptr, g_hinst, nullptr);
                state->edit2 = CreateWindowExW(WS_EX_CLIENTEDGE, L"EDIT", L"",
                    WS_CHILD | WS_VISIBLE | WS_TABSTOP | ES_AUTOHSCROLL,
                    168, 54, 120, 26, hwnd, reinterpret_cast<HMENU>(1002), g_hinst, nullptr);
            }
            wchar_t b[32];
            swprintf(b, 32, L"%d", state->value1);
            SetWindowTextW(state->edit1, b);
            if (state->edit2) {
                swprintf(b, 32, L"%d", state->value2);
                SetWindowTextW(state->edit2, b);
            }
            CreateWindowW(L"BUTTON", Tr(L"OK", L"確定").c_str(),
                WS_CHILD | WS_VISIBLE | WS_TABSTOP | BS_DEFPUSHBUTTON,
                118, state->twoValues ? 100 : 64, 82, 28, hwnd,
                reinterpret_cast<HMENU>(IDOK), g_hinst, nullptr);
            CreateWindowW(L"BUTTON", Tr(L"Cancel", L"取消").c_str(),
                WS_CHILD | WS_VISIBLE | WS_TABSTOP,
                208, state->twoValues ? 100 : 64, 82, 28, hwnd,
                reinterpret_cast<HMENU>(IDCANCEL), g_hinst, nullptr);
            for (HWND child = GetWindow(hwnd, GW_CHILD); child; child = GetWindow(child, GW_HWNDNEXT))
                SendMessageW(child, WM_SETFONT, reinterpret_cast<WPARAM>(g_font), TRUE);
            SetFocus(state->edit1);
            SendMessageW(state->edit1, EM_SETSEL, 0, -1);
        }
        return 0;
    case WM_COMMAND:
        if (!state) break;
        if (LOWORD(wp) == IDOK) {
            int a = 0, b = 0;
            const bool okA = ParseEditInt(state->edit1, a);
            const bool okB = !state->twoValues || ParseEditInt(state->edit2, b);
            if (!okA || !okB || a < state->minValue || a > state->maxValue ||
                (state->twoValues && (b < state->minValue || b > state->maxValue))) {
                wchar_t range[180];
                swprintf(range, 180, Tr(L"Enter a whole number from %d to %d.",
                    L"請輸入 %d 至 %d 嘅整數。").c_str(), state->minValue, state->maxValue);
                MessageBoxW(hwnd, range, Tr(L"Invalid value", L"數值無效").c_str(), MB_OK | MB_ICONWARNING);
                return 0;
            }
            state->value1 = a;
            if (state->twoValues) state->value2 = b;
            state->accepted = true;
            DestroyWindow(hwnd);
            return 0;
        }
        if (LOWORD(wp) == IDCANCEL) {
            DestroyWindow(hwnd);
            return 0;
        }
        break;
    case WM_CLOSE:
        DestroyWindow(hwnd);
        return 0;
    }
    return DefWindowProcW(hwnd, msg, wp, lp);
}

static bool EnsurePromptClass()
{
    static bool registered = false;
    if (registered) return true;
    WNDCLASSEXW wc{};
    wc.cbSize = sizeof(wc);
    wc.lpfnWndProc = PromptProc;
    wc.hInstance = g_hinst;
    wc.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    wc.hbrBackground = reinterpret_cast<HBRUSH>(COLOR_WINDOW + 1);
    wc.lpszClassName = L"WinForgeImageForgePrompt";
    registered = RegisterClassExW(&wc) != 0 || GetLastError() == ERROR_CLASS_ALREADY_EXISTS;
    return registered;
}

static bool PromptIntegers(PromptState& state)
{
    if (!EnsurePromptClass()) return false;
    RECT owner{};
    GetWindowRect(g_hwnd, &owner);
    const int w = 326;
    const int h = state.twoValues ? 178 : 142;
    HWND prompt = CreateWindowExW(WS_EX_DLGMODALFRAME | WS_EX_TOPMOST,
        L"WinForgeImageForgePrompt", state.title.c_str(),
        WS_CAPTION | WS_SYSMENU | WS_POPUP,
        owner.left + ((owner.right - owner.left) - w) / 2,
        owner.top + ((owner.bottom - owner.top) - h) / 2,
        w, h, g_hwnd, nullptr, g_hinst, &state);
    if (!prompt) return false;

    EnableWindow(g_hwnd, FALSE);
    ShowWindow(prompt, SW_SHOW);
    UpdateWindow(prompt);
    MSG msg{};
    bool sawQuit = false;
    while (IsWindow(prompt)) {
        const BOOL r = GetMessageW(&msg, nullptr, 0, 0);
        if (r <= 0) {
            sawQuit = r == 0;
            break;
        }
        if (!IsDialogMessageW(prompt, &msg)) {
            TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }
    }
    EnableWindow(g_hwnd, TRUE);
    SetForegroundWindow(g_hwnd);
    if (sawQuit) PostQuitMessage(static_cast<int>(msg.wParam));
    return state.accepted;
}

static bool PromptOne(const wchar_t* titleEn, const wchar_t* titleZh,
    const wchar_t* labelEn, const wchar_t* labelZh, int& value, int minValue, int maxValue)
{
    PromptState s;
    s.title = Tr(titleEn, titleZh);
    s.label1 = Tr(labelEn, labelZh);
    s.value1 = value;
    s.minValue = minValue;
    s.maxValue = maxValue;
    if (!PromptIntegers(s)) return false;
    value = s.value1;
    return true;
}

static bool PromptTwo(const wchar_t* titleEn, const wchar_t* titleZh,
    const wchar_t* label1En, const wchar_t* label1Zh,
    const wchar_t* label2En, const wchar_t* label2Zh,
    int& first, int& second, int minValue, int maxValue)
{
    PromptState s;
    s.title = Tr(titleEn, titleZh);
    s.label1 = Tr(label1En, label1Zh);
    s.label2 = Tr(label2En, label2Zh);
    s.value1 = first;
    s.value2 = second;
    s.minValue = minValue;
    s.maxValue = maxValue;
    s.twoValues = true;
    if (!PromptIntegers(s)) return false;
    first = s.value1;
    second = s.value2;
    return true;
}

// ============================== GDI+ image I/O ===============================

static bool PixelCountAllowed(int width, int height)
{
    if (width <= 0 || height <= 0) return false;
    return static_cast<std::size_t>(width) <= MAX_PIXELS / static_cast<std::size_t>(height);
}

static bool CopyBitmapPixels(Bitmap& bitmap, std::vector<std::uint32_t>& output)
{
    const int width = static_cast<int>(bitmap.GetWidth());
    const int height = static_cast<int>(bitmap.GetHeight());
    if (!PixelCountAllowed(width, height)) return false;

    Rect area(0, 0, width, height);
    BitmapData data{};
    if (bitmap.LockBits(&area, ImageLockModeRead, PixelFormat32bppARGB, &data) != Ok)
        return false;
    bool ok = true;
    try {
        output.resize(static_cast<std::size_t>(width) * static_cast<std::size_t>(height));
        for (int y = 0; y < height; ++y) {
            const BYTE* row = static_cast<const BYTE*>(data.Scan0) +
                static_cast<std::ptrdiff_t>(y) * data.Stride;
            memcpy(output.data() + static_cast<std::size_t>(y) * width, row,
                static_cast<std::size_t>(width) * sizeof(std::uint32_t));
        }
    }
    catch (...) {
        ok = false;
    }
    bitmap.UnlockBits(&data);
    return ok;
}

static int EncoderClsid(const wchar_t* mime, CLSID& clsid)
{
    UINT count = 0;
    UINT bytes = 0;
    if (GetImageEncodersSize(&count, &bytes) != Ok || bytes == 0) return -1;
    std::vector<BYTE> storage(bytes);
    ImageCodecInfo* codecs = reinterpret_cast<ImageCodecInfo*>(storage.data());
    if (GetImageEncoders(count, bytes, codecs) != Ok) return -1;
    for (UINT i = 0; i < count; ++i) {
        if (codecs[i].MimeType && wcscmp(codecs[i].MimeType, mime) == 0) {
            clsid = codecs[i].Clsid;
            return static_cast<int>(i);
        }
    }
    return -1;
}

static const wchar_t* MimeForExtension(const std::wstring& extension)
{
    if (extension == L".jpg" || extension == L".jpeg" || extension == L".jpe") return L"image/jpeg";
    if (extension == L".bmp" || extension == L".dib") return L"image/bmp";
    if (extension == L".gif") return L"image/gif";
    if (extension == L".tif" || extension == L".tiff") return L"image/tiff";
    return L"image/png";
}

static bool SavePixelsToFile(const std::wstring& path)
{
    if (!g_doc.Valid()) return false;
    const std::wstring extension = LowerExtension(path);
    const wchar_t* mime = MimeForExtension(extension);
    CLSID encoder{};
    if (EncoderClsid(mime, encoder) < 0) return false;

    // JPEG has no alpha: flatten against white instead of producing black transparent regions.
    std::vector<std::uint32_t> flattened;
    const std::uint32_t* source = g_doc.pixels.data();
    if (wcscmp(mime, L"image/jpeg") == 0) {
        try {
            flattened.resize(g_doc.pixels.size());
            for (std::size_t i = 0; i < g_doc.pixels.size(); ++i) {
                const std::uint32_t p = g_doc.pixels[i];
                const int alpha = A(p);
                const int red = (R(p) * alpha + 255 * (255 - alpha)) / 255;
                const int green = (G(p) * alpha + 255 * (255 - alpha)) / 255;
                const int blue = (B(p) * alpha + 255 * (255 - alpha)) / 255;
                flattened[i] = MakeArgb(255, red, green, blue);
            }
            source = flattened.data();
        }
        catch (...) {
            return false;
        }
    }

    Bitmap bitmap(g_doc.width, g_doc.height, g_doc.width * 4,
        PixelFormat32bppARGB, reinterpret_cast<BYTE*>(const_cast<std::uint32_t*>(source)));
    if (bitmap.GetLastStatus() != Ok) return false;

    if (wcscmp(mime, L"image/jpeg") == 0) {
        ULONG quality = 92;
        EncoderParameters params{};
        params.Count = 1;
        params.Parameter[0].Guid = EncoderQuality;
        params.Parameter[0].Type = EncoderParameterValueTypeLong;
        params.Parameter[0].NumberOfValues = 1;
        params.Parameter[0].Value = &quality;
        return bitmap.Save(path.c_str(), &encoder, &params) == Ok;
    }
    return bitmap.Save(path.c_str(), &encoder, nullptr) == Ok;
}

static std::wstring OpenDialogPath()
{
    wchar_t path[32768]{};
    static const wchar_t filters[] =
        L"Images · 圖片 (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff)\0"
        L"*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff\0"
        L"PNG (*.png)\0*.png\0"
        L"JPEG (*.jpg;*.jpeg)\0*.jpg;*.jpeg\0"
        L"Bitmap (*.bmp)\0*.bmp\0"
        L"GIF (*.gif)\0*.gif\0"
        L"TIFF (*.tif;*.tiff)\0*.tif;*.tiff\0"
        L"All files · 所有檔案 (*.*)\0*.*\0\0";
    OPENFILENAMEW ofn{};
    ofn.lStructSize = sizeof(ofn);
    ofn.hwndOwner = g_hwnd;
    ofn.lpstrFilter = filters;
    ofn.lpstrFile = path;
    ofn.nMaxFile = static_cast<DWORD>(sizeof(path) / sizeof(path[0]));
    ofn.Flags = OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR;
    return GetOpenFileNameW(&ofn) ? std::wstring(path) : std::wstring();
}

static std::wstring SaveDialogPath(const std::wstring& suggested)
{
    wchar_t path[32768]{};
    const std::size_t copyLength = (std::min)(
        suggested.size(), (sizeof(path) / sizeof(path[0])) - 1);
    std::copy_n(suggested.c_str(), copyLength, path);
    path[copyLength] = L'\0';
    static const wchar_t filters[] =
        L"PNG (*.png)\0*.png\0"
        L"JPEG (*.jpg)\0*.jpg\0"
        L"Bitmap (*.bmp)\0*.bmp\0"
        L"GIF (*.gif)\0*.gif\0"
        L"TIFF (*.tif)\0*.tif\0\0";
    OPENFILENAMEW ofn{};
    ofn.lStructSize = sizeof(ofn);
    ofn.hwndOwner = g_hwnd;
    ofn.lpstrFilter = filters;
    ofn.nFilterIndex = 1;
    ofn.lpstrFile = path;
    ofn.nMaxFile = static_cast<DWORD>(sizeof(path) / sizeof(path[0]));
    ofn.lpstrDefExt = L"png";
    ofn.Flags = OFN_OVERWRITEPROMPT | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR;
    if (!GetSaveFileNameW(&ofn)) return L"";
    std::wstring result(path);
    if (LowerExtension(result).empty()) {
        static const wchar_t* extensions[] = { L".png", L".jpg", L".bmp", L".gif", L".tif" };
        const DWORD idx = (std::max<DWORD>)(1, (std::min<DWORD>)(5, ofn.nFilterIndex));
        result += extensions[idx - 1];
    }
    return result;
}

static bool OpenPath(const std::wstring& path)
{
    Bitmap bitmap(path.c_str(), FALSE);
    if (bitmap.GetLastStatus() != Ok) {
        ErrorBox(Tr(L"That image could not be opened. Supported formats: PNG, JPEG, BMP, GIF and TIFF.",
            L"開唔到呢張圖片。支援格式：PNG、JPEG、BMP、GIF 同 TIFF。"));
        return false;
    }
    const int width = static_cast<int>(bitmap.GetWidth());
    const int height = static_cast<int>(bitmap.GetHeight());
    if (!PixelCountAllowed(width, height)) {
        ErrorBox(Tr(L"The image is empty or too large (maximum 32 million pixels).",
            L"圖片係空白或者太大（上限 3,200 萬像素）。"));
        return false;
    }

    std::vector<std::uint32_t> pixels;
    if (!CopyBitmapPixels(bitmap, pixels)) {
        ErrorBox(Tr(L"The image pixels could not be decoded.", L"無法解碼圖片像素。"));
        return false;
    }

    g_doc.width = width;
    g_doc.height = height;
    g_doc.pixels = std::move(pixels);
    g_doc.path = path;
    g_doc.dirty = false;
    g_undo.clear();
    g_redo.clear();
    SetRectEmpty(&g_selection);
    g_scrollX = g_scrollY = 0;
    g_fitMode = true;
    ZoomFit();
    UpdateTitle();
    UpdateMenuState();
    InvalidateAll();
    Toast(Tr(L"Image opened", L"已開啟圖片"));
    return true;
}

static void NewDocument(int width, int height, bool announce)
{
    if (!PixelCountAllowed(width, height)) {
        ErrorBox(Tr(L"Canvas dimensions are too large.", L"畫布尺寸太大。"));
        return;
    }
    try {
        g_doc.width = width;
        g_doc.height = height;
        g_doc.pixels.assign(static_cast<std::size_t>(width) * height, 0xffffffffu);
    }
    catch (const std::bad_alloc&) {
        ErrorBox(Tr(L"Not enough memory for that canvas.", L"記憶體不足，無法建立呢個畫布。"));
        return;
    }
    g_doc.path.clear();
    g_doc.dirty = false;
    g_undo.clear();
    g_redo.clear();
    SetRectEmpty(&g_selection);
    g_scrollX = g_scrollY = 0;
    g_fitMode = true;
    ZoomFit();
    UpdateTitle();
    UpdateMenuState();
    InvalidateAll();
    if (announce) Toast(Tr(L"New canvas", L"新畫布"));
}

static bool SaveDocument(bool saveAs)
{
    if (!g_doc.Valid()) return false;
    std::wstring path = g_doc.path;
    if (saveAs || path.empty()) {
        std::wstring suggested = path.empty() ? L"ImageForge.png" : FileNameOnly(path);
        path = SaveDialogPath(suggested);
        if (path.empty()) return false;
    }
    HCURSOR previous = SetCursor(LoadCursorW(nullptr, IDC_WAIT));
    const bool ok = SavePixelsToFile(path);
    SetCursor(previous ? previous : LoadCursorW(nullptr, IDC_ARROW));
    if (!ok) {
        ErrorBox(Tr(L"The image could not be saved.", L"無法儲存圖片。"));
        return false;
    }
    g_doc.path = path;
    g_doc.dirty = false;
    UpdateTitle();
    UpdateMenuState();
    InvalidateAll();
    Toast(Tr(L"Image saved", L"圖片已儲存"));
    return true;
}

static bool ConfirmDestructiveAction()
{
    if (!g_doc.dirty) return true;
    const int result = MessageBoxW(g_hwnd,
        Tr(L"Save changes before continuing?", L"繼續之前要唔要儲存變更？").c_str(),
        Tr(L"Unsaved changes", L"未儲存變更").c_str(),
        MB_YESNOCANCEL | MB_ICONQUESTION);
    if (result == IDCANCEL) return false;
    if (result == IDYES) return SaveDocument(false);
    return true;
}

// ============================== view / zoom =================================

static void UpdateScrollbars()
{
    if (!g_hwnd) return;
    const RECT c = CanvasRect();
    const int cw = (std::max)(1, static_cast<int>(c.right - c.left));
    const int ch = (std::max)(1, static_cast<int>(c.bottom - c.top));
    const int dw = g_doc.Valid() ? (std::max)(1, static_cast<int>(std::lround(g_doc.width * g_zoom))) : 0;
    const int dh = g_doc.Valid() ? (std::max)(1, static_cast<int>(std::lround(g_doc.height * g_zoom))) : 0;
    g_scrollX = (std::max)(0, (std::min)(g_scrollX, (std::max)(0, dw - cw)));
    g_scrollY = (std::max)(0, (std::min)(g_scrollY, (std::max)(0, dh - ch)));

    SCROLLINFO si{};
    si.cbSize = sizeof(si);
    si.fMask = SIF_RANGE | SIF_PAGE | SIF_POS | SIF_DISABLENOSCROLL;
    si.nMin = 0;
    si.nMax = (std::max)(0, dw - 1);
    si.nPage = static_cast<UINT>(cw);
    si.nPos = g_scrollX;
    SetScrollInfo(g_hwnd, SB_HORZ, &si, TRUE);
    si.nMax = (std::max)(0, dh - 1);
    si.nPage = static_cast<UINT>(ch);
    si.nPos = g_scrollY;
    SetScrollInfo(g_hwnd, SB_VERT, &si, TRUE);
}

static void SetZoom(double zoom, bool fitMode)
{
    if (!g_doc.Valid()) return;
    const RECT c = CanvasRect();
    const int cw = (std::max)(1, static_cast<int>(c.right - c.left));
    const int ch = (std::max)(1, static_cast<int>(c.bottom - c.top));
    const double old = g_zoom;
    const double centerX = (g_scrollX + cw * 0.5) / old;
    const double centerY = (g_scrollY + ch * 0.5) / old;
    g_zoom = (std::max)(0.02, (std::min)(32.0, zoom));
    g_fitMode = fitMode;
    g_scrollX = static_cast<int>(std::lround(centerX * g_zoom - cw * 0.5));
    g_scrollY = static_cast<int>(std::lround(centerY * g_zoom - ch * 0.5));
    UpdateScrollbars();
    UpdateMenuState();
    InvalidateAll();
}

static void ZoomFit()
{
    if (!g_doc.Valid()) return;
    const RECT c = CanvasRect();
    const int cw = (std::max)(1, static_cast<int>(c.right - c.left) - 32);
    const int ch = (std::max)(1, static_cast<int>(c.bottom - c.top) - 32);
    const double zx = static_cast<double>(cw) / g_doc.width;
    const double zy = static_cast<double>(ch) / g_doc.height;
    g_zoom = (std::max)(0.02, (std::min)(8.0, (std::min)(zx, zy)));
    g_scrollX = g_scrollY = 0;
    g_fitMode = true;
    UpdateScrollbars();
    UpdateMenuState();
    InvalidateAll();
}

static void ScrollAxis(int bar, int code, int thumb)
{
    SCROLLINFO si{};
    si.cbSize = sizeof(si);
    si.fMask = SIF_ALL;
    GetScrollInfo(g_hwnd, bar, &si);
    int pos = si.nPos;
    const int line = 48;
    switch (code) {
    case SB_LINELEFT: pos -= line; break;
    case SB_LINERIGHT: pos += line; break;
    case SB_PAGELEFT: pos -= static_cast<int>(si.nPage); break;
    case SB_PAGERIGHT: pos += static_cast<int>(si.nPage); break;
    case SB_THUMBTRACK:
    case SB_THUMBPOSITION: pos = thumb; break;
    case SB_TOP: pos = si.nMin; break;
    case SB_BOTTOM: pos = si.nMax; break;
    default: return;
    }
    pos = (std::max)(si.nMin, (std::min)(pos, si.nMax - static_cast<int>(si.nPage) + 1));
    if (bar == SB_HORZ) g_scrollX = (std::max)(0, pos);
    else g_scrollY = (std::max)(0, pos);
    UpdateScrollbars();
    InvalidateAll();
}

// ============================== painting tools ==============================

static void StampBrush(int cx, int cy, Tool tool)
{
    if (!g_doc.Valid()) return;
    const int radius = (std::max)(1, g_brushSize) / 2;
    const int left = (std::max)(0, cx - radius);
    const int right = (std::min)(g_doc.width - 1, cx + radius);
    const int top = (std::max)(0, cy - radius);
    const int bottom = (std::min)(g_doc.height - 1, cy + radius);
    const int rr = radius * radius;
    const std::uint32_t color = tool == Tool::Eraser ? 0x00ffffffu : BrushArgb();
    for (int y = top; y <= bottom; ++y) {
        for (int x = left; x <= right; ++x) {
            const int dx = x - cx;
            const int dy = y - cy;
            if (dx * dx + dy * dy <= rr)
                g_doc.pixels[static_cast<std::size_t>(y) * g_doc.width + x] = color;
        }
    }
}

static void PaintLine(POINT from, POINT to, Tool tool)
{
    const int dx = to.x - from.x;
    const int dy = to.y - from.y;
    const int steps = (std::max)(std::abs(dx), std::abs(dy));
    if (steps <= 0) {
        StampBrush(from.x, from.y, tool);
        return;
    }
    for (int i = 0; i <= steps; ++i) {
        const int x = from.x + static_cast<int>(std::lround(dx * (static_cast<double>(i) / steps)));
        const int y = from.y + static_cast<int>(std::lround(dy * (static_cast<double>(i) / steps)));
        StampBrush(x, y, tool);
    }
}

static void FloodFillAt(int sx, int sy)
{
    if (!g_doc.Valid() || sx < 0 || sy < 0 || sx >= g_doc.width || sy >= g_doc.height) return;
    const std::size_t start = static_cast<std::size_t>(sy) * g_doc.width + sx;
    const std::uint32_t target = g_doc.pixels[start];
    const std::uint32_t replacement = BrushArgb();
    if (target == replacement) return;
    if (!PushUndo()) return;

    HCURSOR previous = SetCursor(LoadCursorW(nullptr, IDC_WAIT));
    try {
        std::vector<std::size_t> stack;
        stack.reserve(4096);
        stack.push_back(start);
        g_doc.pixels[start] = replacement;
        while (!stack.empty()) {
            const std::size_t idx = stack.back();
            stack.pop_back();
            const int x = static_cast<int>(idx % g_doc.width);
            const int y = static_cast<int>(idx / g_doc.width);
            const auto add = [&](std::size_t n) {
                if (g_doc.pixels[n] == target) {
                    g_doc.pixels[n] = replacement;
                    stack.push_back(n);
                }
            };
            if (x > 0) add(idx - 1);
            if (x + 1 < g_doc.width) add(idx + 1);
            if (y > 0) add(idx - static_cast<std::size_t>(g_doc.width));
            if (y + 1 < g_doc.height) add(idx + static_cast<std::size_t>(g_doc.width));
        }
        SetCursor(previous ? previous : LoadCursorW(nullptr, IDC_ARROW));
        MarkChanged(L"Area filled", L"已填滿區域");
    }
    catch (const std::bad_alloc&) {
        SetCursor(previous ? previous : LoadCursorW(nullptr, IDC_ARROW));
        if (!g_undo.empty()) {
            Snapshot s = std::move(g_undo.back());
            g_undo.pop_back();
            RestoreSnapshot(std::move(s));
        }
        ErrorBox(Tr(L"Not enough memory to fill that area.", L"記憶體不足，無法填滿區域。"));
    }
}

static void ChooseBrushColor()
{
    static COLORREF custom[16] = {
        RGB(240,140,60), RGB(84,224,126), RGB(72,140,255), RGB(255,95,95),
        RGB(255,255,255), RGB(0,0,0), RGB(255,220,80), RGB(180,100,240)
    };
    CHOOSECOLORW cc{};
    cc.lStructSize = sizeof(cc);
    cc.hwndOwner = g_hwnd;
    cc.rgbResult = g_brushColor;
    cc.lpCustColors = custom;
    cc.Flags = CC_FULLOPEN | CC_RGBINIT;
    if (ChooseColorW(&cc)) {
        g_brushColor = cc.rgbResult;
        InvalidateAll();
    }
}

// ============================== transforms ==================================

static void CropToSelection()
{
    if (!HasSelection()) {
        Toast(Tr(L"Drag a selection first", L"請先拖曳揀選範圍"));
        return;
    }
    RECT r = NormalizedRect(g_selection);
    r.left = (std::max)(0L, (std::min)(static_cast<LONG>(g_doc.width), r.left));
    r.right = (std::max)(0L, (std::min)(static_cast<LONG>(g_doc.width), r.right));
    r.top = (std::max)(0L, (std::min)(static_cast<LONG>(g_doc.height), r.top));
    r.bottom = (std::max)(0L, (std::min)(static_cast<LONG>(g_doc.height), r.bottom));
    const int newWidth = r.right - r.left;
    const int newHeight = r.bottom - r.top;
    if (newWidth <= 0 || newHeight <= 0 || !PushUndo()) return;

    try {
        std::vector<std::uint32_t> cropped(static_cast<std::size_t>(newWidth) * newHeight);
        for (int y = 0; y < newHeight; ++y) {
            const std::uint32_t* src = g_doc.pixels.data() +
                static_cast<std::size_t>(r.top + y) * g_doc.width + r.left;
            std::uint32_t* dst = cropped.data() + static_cast<std::size_t>(y) * newWidth;
            memcpy(dst, src, static_cast<std::size_t>(newWidth) * sizeof(std::uint32_t));
        }
        g_doc.width = newWidth;
        g_doc.height = newHeight;
        g_doc.pixels = std::move(cropped);
        SetRectEmpty(&g_selection);
        g_fitMode = true;
        ZoomFit();
        MarkChanged(L"Cropped to selection", L"已裁剪到揀選範圍");
    }
    catch (const std::bad_alloc&) {
        if (!g_undo.empty()) g_undo.pop_back();
        ErrorBox(Tr(L"Not enough memory to crop the image.", L"記憶體不足，無法裁剪圖片。"));
    }
}

static void ClearSelectedPixels()
{
    if (!HasSelection()) {
        Toast(Tr(L"Drag a selection first", L"請先拖曳揀選範圍"));
        return;
    }
    if (!PushUndo()) return;
    RECT r = NormalizedRect(g_selection);
    r.left = (std::max)(0L, r.left);
    r.top = (std::max)(0L, r.top);
    r.right = (std::min)(static_cast<LONG>(g_doc.width), r.right);
    r.bottom = (std::min)(static_cast<LONG>(g_doc.height), r.bottom);
    for (int y = r.top; y < r.bottom; ++y) {
        std::fill(g_doc.pixels.begin() + static_cast<std::size_t>(y) * g_doc.width + r.left,
            g_doc.pixels.begin() + static_cast<std::size_t>(y) * g_doc.width + r.right,
            0x00ffffffu);
    }
    MarkChanged(L"Selected pixels cleared", L"已清除揀選像素");
}

static void ResizeImage()
{
    if (!g_doc.Valid()) return;
    int newWidth = g_doc.width;
    int newHeight = g_doc.height;
    if (!PromptTwo(L"Resize image", L"調整圖片大小",
        L"Width (pixels)", L"闊度（像素）", L"Height (pixels)", L"高度（像素）",
        newWidth, newHeight, 1, 16000))
        return;
    if (!PixelCountAllowed(newWidth, newHeight)) {
        ErrorBox(Tr(L"That size exceeds the 32-million-pixel safety limit.",
            L"呢個尺寸超過 3,200 萬像素安全上限。"));
        return;
    }
    if (newWidth == g_doc.width && newHeight == g_doc.height) return;
    if (!PushUndo()) return;

    HCURSOR previous = SetCursor(LoadCursorW(nullptr, IDC_WAIT));
    try {
        Bitmap source(g_doc.width, g_doc.height, g_doc.width * 4, PixelFormat32bppARGB,
            reinterpret_cast<BYTE*>(g_doc.pixels.data()));
        Bitmap destination(newWidth, newHeight, PixelFormat32bppARGB);
        if (source.GetLastStatus() != Ok || destination.GetLastStatus() != Ok)
            throw std::bad_alloc();
        {
            Graphics graphics(&destination);
            graphics.SetCompositingMode(CompositingModeSourceCopy);
            graphics.SetInterpolationMode(InterpolationModeHighQualityBicubic);
            graphics.SetPixelOffsetMode(PixelOffsetModeHighQuality);
            graphics.Clear(Color(0, 255, 255, 255));
            graphics.DrawImage(&source, Rect(0, 0, newWidth, newHeight),
                0, 0, g_doc.width, g_doc.height, UnitPixel);
        }
        std::vector<std::uint32_t> resized;
        if (!CopyBitmapPixels(destination, resized)) throw std::bad_alloc();
        g_doc.width = newWidth;
        g_doc.height = newHeight;
        g_doc.pixels = std::move(resized);
        SetRectEmpty(&g_selection);
        SetCursor(previous ? previous : LoadCursorW(nullptr, IDC_ARROW));
        g_fitMode = true;
        ZoomFit();
        MarkChanged(L"Image resized", L"已調整圖片大小");
    }
    catch (...) {
        SetCursor(previous ? previous : LoadCursorW(nullptr, IDC_ARROW));
        if (!g_undo.empty()) g_undo.pop_back();
        ErrorBox(Tr(L"The image could not be resized.", L"無法調整圖片大小。"));
    }
}

static void RotateImage(bool clockwise)
{
    if (!g_doc.Valid() || !PushUndo()) return;
    const int oldWidth = g_doc.width;
    const int oldHeight = g_doc.height;
    try {
        std::vector<std::uint32_t> rotated(static_cast<std::size_t>(oldWidth) * oldHeight);
        for (int y = 0; y < oldHeight; ++y) {
            for (int x = 0; x < oldWidth; ++x) {
                int nx, ny;
                if (clockwise) {
                    nx = oldHeight - 1 - y;
                    ny = x;
                }
                else {
                    nx = y;
                    ny = oldWidth - 1 - x;
                }
                rotated[static_cast<std::size_t>(ny) * oldHeight + nx] =
                    g_doc.pixels[static_cast<std::size_t>(y) * oldWidth + x];
            }
        }
        g_doc.width = oldHeight;
        g_doc.height = oldWidth;
        g_doc.pixels = std::move(rotated);
        SetRectEmpty(&g_selection);
        if (g_fitMode) ZoomFit(); else UpdateScrollbars();
        MarkChanged(clockwise ? L"Rotated clockwise" : L"Rotated counter-clockwise",
            clockwise ? L"已順時針旋轉" : L"已逆時針旋轉");
    }
    catch (const std::bad_alloc&) {
        if (!g_undo.empty()) g_undo.pop_back();
        ErrorBox(Tr(L"Not enough memory to rotate the image.", L"記憶體不足，無法旋轉圖片。"));
    }
}

static void FlipImage(bool horizontal)
{
    if (!g_doc.Valid() || !PushUndo()) return;
    if (horizontal) {
        for (int y = 0; y < g_doc.height; ++y) {
            auto first = g_doc.pixels.begin() + static_cast<std::size_t>(y) * g_doc.width;
            std::reverse(first, first + g_doc.width);
        }
    }
    else {
        for (int y = 0; y < g_doc.height / 2; ++y) {
            const std::size_t top = static_cast<std::size_t>(y) * g_doc.width;
            const std::size_t bottom = static_cast<std::size_t>(g_doc.height - 1 - y) * g_doc.width;
            for (int x = 0; x < g_doc.width; ++x)
                std::swap(g_doc.pixels[top + x], g_doc.pixels[bottom + x]);
        }
    }
    SetRectEmpty(&g_selection);
    MarkChanged(horizontal ? L"Flipped horizontally" : L"Flipped vertically",
        horizontal ? L"已水平翻轉" : L"已垂直翻轉");
}

// ============================== adjustments =================================

template<class Fn>
static void ApplyPixelOperation(Fn operation, const wchar_t* doneEn, const wchar_t* doneZh)
{
    if (!g_doc.Valid() || !PushUndo()) return;
    HCURSOR previous = SetCursor(LoadCursorW(nullptr, IDC_WAIT));
    for (std::uint32_t& p : g_doc.pixels) p = operation(p);
    SetCursor(previous ? previous : LoadCursorW(nullptr, IDC_ARROW));
    MarkChanged(doneEn, doneZh);
}

static void AdjustBrightness()
{
    int amount = 0;
    if (!PromptOne(L"Brightness", L"亮度", L"Amount (-100 to 100)", L"幅度（-100 至 100）",
        amount, -100, 100) || amount == 0)
        return;
    const int offset = static_cast<int>(std::lround(amount * 2.55));
    ApplyPixelOperation([=](std::uint32_t p) {
        return MakeArgb(A(p), R(p) + offset, G(p) + offset, B(p) + offset);
    }, L"Brightness adjusted", L"已調整亮度");
}

static void ApplyContrastAdjustment()
{
    int amount = 0;
    if (!PromptOne(L"Contrast", L"對比度", L"Amount (-100 to 100)", L"幅度（-100 至 100）",
        amount, -100, 100) || amount == 0)
        return;
    const double value = amount * 2.55;
    const double factor = (259.0 * (value + 255.0)) / (255.0 * (259.0 - value));
    ApplyPixelOperation([=](std::uint32_t p) {
        return MakeArgb(A(p),
            static_cast<int>(std::lround(factor * (R(p) - 128) + 128)),
            static_cast<int>(std::lround(factor * (G(p) - 128) + 128)),
            static_cast<int>(std::lround(factor * (B(p) - 128) + 128)));
    }, L"Contrast adjusted", L"已調整對比度");
}

static void AdjustSaturation()
{
    int amount = 0;
    if (!PromptOne(L"Saturation", L"飽和度", L"Amount (-100 to 200)", L"幅度（-100 至 200）",
        amount, -100, 200) || amount == 0)
        return;
    const double factor = 1.0 + amount / 100.0;
    ApplyPixelOperation([=](std::uint32_t p) {
        const double gray = 0.2126 * R(p) + 0.7152 * G(p) + 0.0722 * B(p);
        return MakeArgb(A(p),
            static_cast<int>(std::lround(gray + (R(p) - gray) * factor)),
            static_cast<int>(std::lround(gray + (G(p) - gray) * factor)),
            static_cast<int>(std::lround(gray + (B(p) - gray) * factor)));
    }, L"Saturation adjusted", L"已調整飽和度");
}

static void AdjustGamma()
{
    int percent = 100;
    if (!PromptOne(L"Gamma", L"伽瑪", L"Gamma percent (10 to 300)", L"伽瑪百分比（10 至 300）",
        percent, 10, 300) || percent == 100)
        return;
    const double inverse = 100.0 / percent;
    int table[256];
    for (int i = 0; i < 256; ++i)
        table[i] = ClampByte(static_cast<int>(std::lround(255.0 * std::pow(i / 255.0, inverse))));
    ApplyPixelOperation([&](std::uint32_t p) {
        return MakeArgb(A(p), table[R(p)], table[G(p)], table[B(p)]);
    }, L"Gamma adjusted", L"已調整伽瑪");
}

static void AutoLevels()
{
    if (!g_doc.Valid()) return;
    int minR = 255, minG = 255, minB = 255;
    int maxR = 0, maxG = 0, maxB = 0;
    bool found = false;
    for (std::uint32_t p : g_doc.pixels) {
        if (A(p) == 0) continue;
        found = true;
        minR = (std::min)(minR, R(p)); maxR = (std::max)(maxR, R(p));
        minG = (std::min)(minG, G(p)); maxG = (std::max)(maxG, G(p));
        minB = (std::min)(minB, B(p)); maxB = (std::max)(maxB, B(p));
    }
    if (!found) return;
    ApplyPixelOperation([=](std::uint32_t p) {
        const auto stretch = [](int v, int lo, int hi) {
            return hi <= lo ? v : (v - lo) * 255 / (hi - lo);
        };
        return MakeArgb(A(p), stretch(R(p), minR, maxR),
            stretch(G(p), minG, maxG), stretch(B(p), minB, maxB));
    }, L"Auto levels applied", L"已套用自動色階");
}

static void Grayscale()
{
    ApplyPixelOperation([](std::uint32_t p) {
        const int gray = ClampByte(static_cast<int>(std::lround(
            0.2126 * R(p) + 0.7152 * G(p) + 0.0722 * B(p))));
        return MakeArgb(A(p), gray, gray, gray);
    }, L"Grayscale applied", L"已套用灰階");
}

static void Invert()
{
    ApplyPixelOperation([](std::uint32_t p) {
        return MakeArgb(A(p), 255 - R(p), 255 - G(p), 255 - B(p));
    }, L"Colors inverted", L"已反轉顏色");
}

static void Sepia()
{
    ApplyPixelOperation([](std::uint32_t p) {
        const int r = R(p), g = G(p), b = B(p);
        return MakeArgb(A(p),
            static_cast<int>(0.393 * r + 0.769 * g + 0.189 * b),
            static_cast<int>(0.349 * r + 0.686 * g + 0.168 * b),
            static_cast<int>(0.272 * r + 0.534 * g + 0.131 * b));
    }, L"Sepia applied", L"已套用復古色調");
}

static void BoxBlur()
{
    int radius = 2;
    if (!PromptOne(L"Box blur", L"方框模糊", L"Radius (1 to 20)", L"半徑（1 至 20）",
        radius, 1, 20))
        return;
    if (!g_doc.Valid() || !PushUndo()) return;
    HCURSOR previous = SetCursor(LoadCursorW(nullptr, IDC_WAIT));
    try {
        const int w = g_doc.width;
        const int h = g_doc.height;
        std::vector<std::uint32_t> temp(g_doc.pixels.size());
        std::vector<std::uint32_t> output(g_doc.pixels.size());

        for (int y = 0; y < h; ++y) {
            long long sa = 0, sr = 0, sg = 0, sb = 0;
            int count = 0;
            for (int x = 0; x < (std::min)(w, radius + 1); ++x) {
                const std::uint32_t p = g_doc.pixels[static_cast<std::size_t>(y) * w + x];
                sa += A(p); sr += R(p); sg += G(p); sb += B(p); ++count;
            }
            for (int x = 0; x < w; ++x) {
                temp[static_cast<std::size_t>(y) * w + x] =
                    MakeArgb(static_cast<int>(sa / count), static_cast<int>(sr / count),
                        static_cast<int>(sg / count), static_cast<int>(sb / count));
                const int remove = x - radius;
                const int add = x + radius + 1;
                if (remove >= 0) {
                    const std::uint32_t p = g_doc.pixels[static_cast<std::size_t>(y) * w + remove];
                    sa -= A(p); sr -= R(p); sg -= G(p); sb -= B(p); --count;
                }
                if (add < w) {
                    const std::uint32_t p = g_doc.pixels[static_cast<std::size_t>(y) * w + add];
                    sa += A(p); sr += R(p); sg += G(p); sb += B(p); ++count;
                }
            }
        }

        for (int x = 0; x < w; ++x) {
            long long sa = 0, sr = 0, sg = 0, sb = 0;
            int count = 0;
            for (int y = 0; y < (std::min)(h, radius + 1); ++y) {
                const std::uint32_t p = temp[static_cast<std::size_t>(y) * w + x];
                sa += A(p); sr += R(p); sg += G(p); sb += B(p); ++count;
            }
            for (int y = 0; y < h; ++y) {
                output[static_cast<std::size_t>(y) * w + x] =
                    MakeArgb(static_cast<int>(sa / count), static_cast<int>(sr / count),
                        static_cast<int>(sg / count), static_cast<int>(sb / count));
                const int remove = y - radius;
                const int add = y + radius + 1;
                if (remove >= 0) {
                    const std::uint32_t p = temp[static_cast<std::size_t>(remove) * w + x];
                    sa -= A(p); sr -= R(p); sg -= G(p); sb -= B(p); --count;
                }
                if (add < h) {
                    const std::uint32_t p = temp[static_cast<std::size_t>(add) * w + x];
                    sa += A(p); sr += R(p); sg += G(p); sb += B(p); ++count;
                }
            }
        }
        g_doc.pixels = std::move(output);
        SetCursor(previous ? previous : LoadCursorW(nullptr, IDC_ARROW));
        MarkChanged(L"Blur applied", L"已套用模糊");
    }
    catch (const std::bad_alloc&) {
        SetCursor(previous ? previous : LoadCursorW(nullptr, IDC_ARROW));
        if (!g_undo.empty()) g_undo.pop_back();
        ErrorBox(Tr(L"Not enough memory for the blur filter.", L"記憶體不足，無法套用模糊濾鏡。"));
    }
}

static void Sharpen()
{
    if (!g_doc.Valid() || !PushUndo()) return;
    HCURSOR previous = SetCursor(LoadCursorW(nullptr, IDC_WAIT));
    try {
        const int w = g_doc.width, h = g_doc.height;
        const std::vector<std::uint32_t> source = g_doc.pixels;
        for (int y = 1; y + 1 < h; ++y) {
            for (int x = 1; x + 1 < w; ++x) {
                const std::size_t i = static_cast<std::size_t>(y) * w + x;
                const std::uint32_t c = source[i];
                const std::uint32_t l = source[i - 1], r = source[i + 1];
                const std::uint32_t u = source[i - w], d = source[i + w];
                g_doc.pixels[i] = MakeArgb(A(c),
                    5 * R(c) - R(l) - R(r) - R(u) - R(d),
                    5 * G(c) - G(l) - G(r) - G(u) - G(d),
                    5 * B(c) - B(l) - B(r) - B(u) - B(d));
            }
        }
        SetCursor(previous ? previous : LoadCursorW(nullptr, IDC_ARROW));
        MarkChanged(L"Sharpen applied", L"已套用銳化");
    }
    catch (const std::bad_alloc&) {
        SetCursor(previous ? previous : LoadCursorW(nullptr, IDC_ARROW));
        if (!g_undo.empty()) g_undo.pop_back();
        ErrorBox(Tr(L"Not enough memory for the sharpen filter.", L"記憶體不足，無法套用銳化濾鏡。"));
    }
}

static int GrayOf(std::uint32_t p)
{
    return (54 * R(p) + 183 * G(p) + 19 * B(p)) / 256;
}

static void DetectEdges()
{
    if (!g_doc.Valid() || !PushUndo()) return;
    HCURSOR previous = SetCursor(LoadCursorW(nullptr, IDC_WAIT));
    try {
        const int w = g_doc.width, h = g_doc.height;
        const std::vector<std::uint32_t> source = g_doc.pixels;
        std::fill(g_doc.pixels.begin(), g_doc.pixels.end(), 0xff000000u);
        for (int y = 1; y + 1 < h; ++y) {
            for (int x = 1; x + 1 < w; ++x) {
                const std::size_t i = static_cast<std::size_t>(y) * w + x;
                const int tl = GrayOf(source[i - w - 1]), tc = GrayOf(source[i - w]);
                const int tr = GrayOf(source[i - w + 1]), ml = GrayOf(source[i - 1]);
                const int mr = GrayOf(source[i + 1]), bl = GrayOf(source[i + w - 1]);
                const int bc = GrayOf(source[i + w]), br = GrayOf(source[i + w + 1]);
                const int gx = -tl + tr - 2 * ml + 2 * mr - bl + br;
                const int gy = -tl - 2 * tc - tr + bl + 2 * bc + br;
                const int magnitude = ClampByte(static_cast<int>(std::sqrt(
                    static_cast<double>(gx * gx + gy * gy))));
                g_doc.pixels[i] = MakeArgb(A(source[i]), magnitude, magnitude, magnitude);
            }
        }
        SetCursor(previous ? previous : LoadCursorW(nullptr, IDC_ARROW));
        MarkChanged(L"Edge detection applied", L"已套用邊緣偵測");
    }
    catch (const std::bad_alloc&) {
        SetCursor(previous ? previous : LoadCursorW(nullptr, IDC_ARROW));
        if (!g_undo.empty()) g_undo.pop_back();
        ErrorBox(Tr(L"Not enough memory for edge detection.", L"記憶體不足，無法偵測邊緣。"));
    }
}

// ============================== menus / chrome ===============================

static void AppendItem(HMENU menu, UINT id, const wchar_t* en, const wchar_t* zh)
{
    const std::wstring text = Tr(en, zh);
    AppendMenuW(menu, MF_STRING, id, text.c_str());
}

static void AppendSubmenu(HMENU menu, HMENU child, const wchar_t* en, const wchar_t* zh)
{
    const std::wstring text = Tr(en, zh);
    AppendMenuW(menu, MF_POPUP, reinterpret_cast<UINT_PTR>(child), text.c_str());
}

static void BuildMenus()
{
    if (!g_hwnd) return;
    HMENU root = CreateMenu();
    HMENU file = CreatePopupMenu();
    AppendItem(file, CMD_NEW, L"&New canvas\tCtrl+N", L"新增畫布(&N)\tCtrl+N");
    AppendItem(file, CMD_OPEN, L"&Open…\tCtrl+O", L"開啟(&O)…\tCtrl+O");
    AppendMenuW(file, MF_SEPARATOR, 0, nullptr);
    AppendItem(file, CMD_SAVE, L"&Save\tCtrl+S", L"儲存(&S)\tCtrl+S");
    AppendItem(file, CMD_SAVEAS, L"Save &as…\tCtrl+Shift+S", L"另存新檔(&A)…\tCtrl+Shift+S");
    AppendMenuW(file, MF_SEPARATOR, 0, nullptr);
    AppendItem(file, CMD_EXIT, L"E&xit", L"離開(&X)");
    AppendSubmenu(root, file, L"&File", L"檔案(&F)");

    HMENU edit = CreatePopupMenu();
    AppendItem(edit, CMD_UNDO, L"&Undo\tCtrl+Z", L"復原(&U)\tCtrl+Z");
    AppendItem(edit, CMD_REDO, L"&Redo\tCtrl+Y", L"重做(&R)\tCtrl+Y");
    AppendMenuW(edit, MF_SEPARATOR, 0, nullptr);
    AppendItem(edit, CMD_SELECTALL, L"Select &all\tCtrl+A", L"全選(&A)\tCtrl+A");
    AppendItem(edit, CMD_CLEARSEL, L"Clear selection\tEsc", L"取消揀選\tEsc");
    AppendItem(edit, CMD_CLEARPIXELS, L"Clear selected pixels\tDelete", L"清除揀選像素\tDelete");
    AppendSubmenu(root, edit, L"&Edit", L"編輯(&E)");

    HMENU tools = CreatePopupMenu();
    AppendItem(tools, CMD_TOOL_SELECT, L"&Select tool\tS", L"揀選工具(&S)\tS");
    AppendItem(tools, CMD_TOOL_BRUSH, L"&Brush tool\tB", L"畫筆工具(&B)\tB");
    AppendItem(tools, CMD_TOOL_ERASER, L"&Eraser tool\tE", L"擦膠工具(&E)\tE");
    AppendItem(tools, CMD_TOOL_FILL, L"&Fill tool\tF", L"填色工具(&F)\tF");
    AppendMenuW(tools, MF_SEPARATOR, 0, nullptr);
    AppendItem(tools, CMD_BRUSH_COLOR, L"Brush &color…", L"畫筆顏色(&C)…");
    AppendItem(tools, CMD_BRUSH_SIZE, L"Brush si&ze…", L"畫筆大小(&Z)…");
    AppendSubmenu(root, tools, L"&Tools", L"工具(&T)");

    HMENU image = CreatePopupMenu();
    AppendItem(image, CMD_CROP, L"&Crop to selection", L"裁剪到揀選範圍(&C)");
    AppendItem(image, CMD_RESIZE, L"&Resize…", L"調整大小(&R)…");
    AppendMenuW(image, MF_SEPARATOR, 0, nullptr);
    AppendItem(image, CMD_ROTATE_CW, L"Rotate 90° clockwise", L"順時針旋轉 90°");
    AppendItem(image, CMD_ROTATE_CCW, L"Rotate 90° counter-clockwise", L"逆時針旋轉 90°");
    AppendItem(image, CMD_FLIP_H, L"Flip horizontal", L"水平翻轉");
    AppendItem(image, CMD_FLIP_V, L"Flip vertical", L"垂直翻轉");
    AppendSubmenu(root, image, L"&Image", L"圖片(&I)");

    HMENU adjust = CreatePopupMenu();
    AppendItem(adjust, CMD_BRIGHTNESS, L"&Brightness…", L"亮度(&B)…");
    AppendItem(adjust, CMD_CONTRAST, L"&Contrast…", L"對比度(&C)…");
    AppendItem(adjust, CMD_SATURATION, L"&Saturation…", L"飽和度(&S)…");
    AppendItem(adjust, CMD_GAMMA, L"&Gamma…", L"伽瑪(&G)…");
    AppendItem(adjust, CMD_AUTOLEVELS, L"&Auto levels", L"自動色階(&A)");
    AppendSubmenu(root, adjust, L"&Adjust", L"調整(&A)");

    HMENU filters = CreatePopupMenu();
    AppendItem(filters, CMD_GRAYSCALE, L"&Grayscale", L"灰階(&G)");
    AppendItem(filters, CMD_INVERT, L"&Invert", L"反轉顏色(&I)");
    AppendItem(filters, CMD_SEPIA, L"&Sepia", L"復古色調(&S)");
    AppendMenuW(filters, MF_SEPARATOR, 0, nullptr);
    AppendItem(filters, CMD_BLUR, L"&Box blur…", L"方框模糊(&B)…");
    AppendItem(filters, CMD_SHARPEN, L"S&harpen", L"銳化(&H)");
    AppendItem(filters, CMD_EDGES, L"&Edge detection", L"邊緣偵測(&E)");
    AppendSubmenu(root, filters, L"F&ilters", L"濾鏡(&L)");

    HMENU view = CreatePopupMenu();
    AppendItem(view, CMD_ZOOMIN, L"Zoom &in\tCtrl++", L"放大(&I)\tCtrl++");
    AppendItem(view, CMD_ZOOMOUT, L"Zoom &out\tCtrl+-", L"縮小(&O)\tCtrl+-");
    AppendItem(view, CMD_ZOOM100, L"&100%\tCtrl+1", L"100%(&1)\tCtrl+1");
    AppendItem(view, CMD_ZOOMFIT, L"&Fit image\tCtrl+0", L"配合視窗(&F)\tCtrl+0");
    AppendSubmenu(root, view, L"&View", L"檢視(&V)");

    HMENU language = CreatePopupMenu();
    AppendItem(language, CMD_LANG_EN, L"English", L"English");
    AppendItem(language, CMD_LANG_ZH, L"繁體中文（粵語）", L"繁體中文（粵語）");
    AppendItem(language, CMD_LANG_BOTH, L"Bilingual", L"雙語");
    AppendSubmenu(root, language, L"&Language", L"語言(&L)");

    HMENU help = CreatePopupMenu();
    AppendItem(help, CMD_ABOUT, L"&About ImageForge\tF1", L"關於 ImageForge(&A)\tF1");
    AppendSubmenu(root, help, L"&Help", L"說明(&H)");

    HMENU old = GetMenu(g_hwnd);
    SetMenu(g_hwnd, root);
    if (old) DestroyMenu(old);
    UpdateMenuState();
    DrawMenuBar(g_hwnd);
}

static void SetMenuEnabled(HMENU menu, UINT command, bool enabled)
{
    EnableMenuItem(menu, command, MF_BYCOMMAND | (enabled ? MF_ENABLED : MF_GRAYED));
}

static void UpdateMenuState()
{
    if (!g_hwnd) return;
    HMENU menu = GetMenu(g_hwnd);
    if (!menu) return;
    const bool image = g_doc.Valid();
    SetMenuEnabled(menu, CMD_SAVE, image);
    SetMenuEnabled(menu, CMD_SAVEAS, image);
    SetMenuEnabled(menu, CMD_UNDO, !g_undo.empty());
    SetMenuEnabled(menu, CMD_REDO, !g_redo.empty());
    for (UINT id : { CMD_SELECTALL, CMD_CLEARSEL, CMD_TOOL_SELECT, CMD_TOOL_BRUSH,
        CMD_TOOL_ERASER, CMD_TOOL_FILL, CMD_BRUSH_COLOR, CMD_BRUSH_SIZE,
        CMD_CROP, CMD_RESIZE, CMD_ROTATE_CW, CMD_ROTATE_CCW, CMD_FLIP_H, CMD_FLIP_V,
        CMD_BRIGHTNESS, CMD_CONTRAST, CMD_SATURATION, CMD_GAMMA, CMD_AUTOLEVELS,
        CMD_GRAYSCALE, CMD_INVERT, CMD_SEPIA, CMD_BLUR, CMD_SHARPEN, CMD_EDGES,
        CMD_ZOOMIN, CMD_ZOOMOUT, CMD_ZOOM100, CMD_ZOOMFIT })
        SetMenuEnabled(menu, id, image);
    SetMenuEnabled(menu, CMD_CLEARPIXELS, HasSelection());
    SetMenuEnabled(menu, CMD_CROP, HasSelection());

    const UINT toolId = g_tool == Tool::Select ? CMD_TOOL_SELECT :
        g_tool == Tool::Brush ? CMD_TOOL_BRUSH :
        g_tool == Tool::Eraser ? CMD_TOOL_ERASER : CMD_TOOL_FILL;
    CheckMenuRadioItem(menu, CMD_TOOL_SELECT, CMD_TOOL_FILL, toolId, MF_BYCOMMAND);
    const UINT langId = g_lang == Lang::En ? CMD_LANG_EN :
        g_lang == Lang::Zh ? CMD_LANG_ZH : CMD_LANG_BOTH;
    CheckMenuRadioItem(menu, CMD_LANG_EN, CMD_LANG_BOTH, langId, MF_BYCOMMAND);
    CheckMenuItem(menu, CMD_ZOOMFIT, MF_BYCOMMAND | (g_fitMode ? MF_CHECKED : MF_UNCHECKED));
    DrawMenuBar(g_hwnd);
}

static std::wstring ToolName()
{
    switch (g_tool) {
    case Tool::Brush: return Tr(L"Brush", L"畫筆");
    case Tool::Eraser: return Tr(L"Eraser", L"擦膠");
    case Tool::Fill: return Tr(L"Fill", L"填色");
    default: return Tr(L"Select", L"揀選");
    }
}

static void UpdateTitle()
{
    if (!g_hwnd) return;
    const std::wstring name = g_doc.path.empty() ? Tr(L"Untitled", L"未命名") : FileNameOnly(g_doc.path);
    const std::wstring dirty = g_doc.dirty ? L" *" : L"";
    const std::wstring title = name + dirty + L" — WinForge Image Editor · 影像編輯器";
    SetWindowTextW(g_hwnd, title.c_str());
}

static void LayoutToolbar(int width)
{
    g_toolbar.clear();
    int x = 158;
    const int top = 8;
    const int height = 33;
    const auto add = [&](UINT command, const wchar_t* en, const wchar_t* zh, int buttonWidth) {
        ToolbarItem item;
        item.command = command;
        item.en = en;
        item.zh = zh;
        item.width = buttonWidth;
        item.rect = RECT{ x, top, x + buttonWidth, top + height };
        g_toolbar.push_back(item);
        x += buttonWidth + 5;
    };
    add(CMD_NEW, L"New", L"新增", 66);
    add(CMD_OPEN, L"Open", L"開啟", 70);
    add(CMD_SAVE, L"Save", L"儲存", 70);
    x += 5;
    add(CMD_UNDO, L"Undo", L"復原", 70);
    add(CMD_REDO, L"Redo", L"重做", 70);
    x += 5;
    add(CMD_TOOL_SELECT, L"Select", L"揀選", 76);
    add(CMD_TOOL_BRUSH, L"Brush", L"畫筆", 74);
    add(CMD_TOOL_ERASER, L"Erase", L"擦膠", 74);
    add(CMD_ZOOMFIT, L"Fit", L"配合", 62);
    add(CMD_ZOOMOUT, L"−", L"−", 38);
    add(CMD_ZOOMIN, L"+", L"+", 38);
    (void)width;
}

static bool ToolbarEnabled(UINT command)
{
    if (command == CMD_NEW || command == CMD_OPEN) return true;
    if (command == CMD_UNDO) return !g_undo.empty();
    if (command == CMD_REDO) return !g_redo.empty();
    return g_doc.Valid();
}

static bool ToolbarActive(UINT command)
{
    return (command == CMD_TOOL_SELECT && g_tool == Tool::Select) ||
        (command == CMD_TOOL_BRUSH && g_tool == Tool::Brush) ||
        (command == CMD_TOOL_ERASER && g_tool == Tool::Eraser) ||
        (command == CMD_ZOOMFIT && g_fitMode);
}

static void FillColor(HDC dc, const RECT& rect, COLORREF color)
{
    HBRUSH brush = CreateSolidBrush(color);
    FillRect(dc, &rect, brush);
    DeleteObject(brush);
}

static void DrawToolbar(HDC dc, const RECT& client)
{
    RECT bar{ client.left, client.top, client.right, client.top + TOOLBAR_H };
    FillColor(dc, bar, RGB(28, 31, 39));
    RECT accent{ bar.left, bar.bottom - 2, bar.right, bar.bottom };
    FillColor(dc, accent, RGB(240, 140, 60));

    SelectObject(dc, g_font);
    SetBkMode(dc, TRANSPARENT);
    SetTextColor(dc, RGB(242, 244, 249));
    RECT brand{ 12, 7, 151, 29 };
    DrawTextW(dc, L"WF  ImageForge", -1, &brand, DT_LEFT | DT_VCENTER | DT_SINGLELINE);
    SelectObject(dc, g_fontSmall);
    SetTextColor(dc, RGB(157, 164, 180));
    RECT subtitle{ 12, 27, 151, 45 };
    const std::wstring sub = Tr(L"Raster studio", L"點陣圖工作室");
    DrawTextW(dc, sub.c_str(), -1, &subtitle, DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS);

    LayoutToolbar(client.right - client.left);
    for (const ToolbarItem& item : g_toolbar) {
        if (item.rect.left >= client.right - 4) continue;
        RECT r = item.rect;
        r.right = (std::min)(r.right, client.right - 4);
        const bool enabled = ToolbarEnabled(item.command);
        const bool active = ToolbarActive(item.command);
        const bool hover = item.command == g_hoverCommand;
        COLORREF fill = RGB(42, 46, 57);
        if (active) fill = RGB(110, 68, 34);
        else if (hover && enabled) fill = RGB(57, 62, 76);
        FillColor(dc, r, fill);
        HPEN pen = CreatePen(PS_SOLID, 1, active ? RGB(240, 140, 60) : RGB(73, 79, 95));
        HGDIOBJ oldPen = SelectObject(dc, pen);
        HGDIOBJ oldBrush = SelectObject(dc, GetStockObject(NULL_BRUSH));
        Rectangle(dc, r.left, r.top, r.right, r.bottom);
        SelectObject(dc, oldBrush);
        SelectObject(dc, oldPen);
        DeleteObject(pen);

        SelectObject(dc, g_fontSmall);
        SetTextColor(dc, enabled ? RGB(225, 228, 236) : RGB(103, 108, 121));
        std::wstring text = Tr(item.en, item.zh);
        InflateRect(&r, -5, -2);
        DrawTextW(dc, text.c_str(), -1, &r,
            DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS);
    }
}

static std::wstring StatusText()
{
    if (!g_toast.empty()) return g_toast;
    if (!g_doc.Valid()) return Tr(L"No image", L"冇圖片");
    wchar_t dimensions[128];
    swprintf(dimensions, 128, L"%d × %d px   ·   %.0f%%   ·   ",
        g_doc.width, g_doc.height, g_zoom * 100.0);
    std::wstring result = dimensions;
    result += ToolName();
    if (g_tool == Tool::Brush || g_tool == Tool::Eraser) {
        wchar_t size[40];
        swprintf(size, 40, L" %d px", g_brushSize);
        result += size;
    }
    if (HasSelection()) {
        const RECT r = NormalizedRect(g_selection);
        wchar_t selection[96];
        swprintf(selection, 96, Tr(L"   ·   Selection %ld × %ld",
            L"   ·   揀選 %ld × %ld").c_str(), r.right - r.left, r.bottom - r.top);
        result += selection;
    }
    return result;
}

// ============================== rendering ===================================

static void DrawChecker(HDC dc, const RECT& area)
{
    const int tile = 12;
    for (int y = area.top; y < area.bottom; y += tile) {
        for (int x = area.left; x < area.right; x += tile) {
            RECT r{ x, y,
                (std::min)(x + tile, static_cast<int>(area.right)),
                (std::min)(y + tile, static_cast<int>(area.bottom)) };
            const bool dark = (((x - area.left) / tile) + ((y - area.top) / tile)) % 2 != 0;
            FillColor(dc, r, dark ? RGB(183, 187, 194) : RGB(224, 226, 230));
        }
    }
}

static void DrawCanvas(HDC dc)
{
    const RECT canvas = CanvasRect();
    FillColor(dc, canvas, RGB(18, 20, 26));
    if (!g_doc.Valid()) {
        SelectObject(dc, g_font);
        SetBkMode(dc, TRANSPARENT);
        SetTextColor(dc, RGB(226, 229, 236));
        RECT title = canvas;
        title.top += (canvas.bottom - canvas.top) / 2 - 48;
        title.bottom = title.top + 30;
        const std::wstring welcome = Tr(L"Create or open an image to begin",
            L"新增或者開啟圖片，就可以開始");
        DrawTextW(dc, welcome.c_str(), -1, &title, DT_CENTER | DT_VCENTER | DT_SINGLELINE);
        SelectObject(dc, g_fontSmall);
        SetTextColor(dc, RGB(145, 151, 165));
        RECT detail = title;
        detail.top += 34;
        detail.bottom += 65;
        const std::wstring formats = Tr(
            L"PNG · JPEG · BMP · GIF · TIFF   |   Ctrl+N new · Ctrl+O open",
            L"PNG · JPEG · BMP · GIF · TIFF   |   Ctrl+N 新增 · Ctrl+O 開啟");
        DrawTextW(dc, formats.c_str(), -1, &detail, DT_CENTER | DT_TOP | DT_WORDBREAK);
        return;
    }

    int ox, oy, dw, dh;
    ImagePlacement(ox, oy, dw, dh);
    RECT imageRect{ ox, oy, ox + dw, oy + dh };
    RECT visible{};
    if (!IntersectRect(&visible, &imageRect, &canvas)) return;

    RECT shadow{ imageRect.left + 5, imageRect.top + 6, imageRect.right + 7, imageRect.bottom + 8 };
    RECT shadowVisible{};
    if (IntersectRect(&shadowVisible, &shadow, &canvas))
        FrameRect(dc, &shadowVisible, reinterpret_cast<HBRUSH>(GetStockObject(BLACK_BRUSH)));

    const int saved = SaveDC(dc);
    IntersectClipRect(dc, canvas.left, canvas.top, canvas.right, canvas.bottom);
    DrawChecker(dc, imageRect);
    {
        Graphics graphics(dc);
        graphics.SetCompositingMode(CompositingModeSourceOver);
        graphics.SetCompositingQuality(CompositingQualityHighQuality);
        graphics.SetInterpolationMode(g_zoom >= 3.0 ?
            InterpolationModeNearestNeighbor : InterpolationModeHighQualityBicubic);
        graphics.SetPixelOffsetMode(g_zoom >= 3.0 ? PixelOffsetModeHalf : PixelOffsetModeHighQuality);
        Bitmap bitmap(g_doc.width, g_doc.height, g_doc.width * 4, PixelFormat32bppARGB,
            reinterpret_cast<BYTE*>(g_doc.pixels.data()));
        if (bitmap.GetLastStatus() == Ok)
            graphics.DrawImage(&bitmap, Rect(ox, oy, dw, dh),
                0, 0, g_doc.width, g_doc.height, UnitPixel);

        if (HasSelection()) {
            const RECT s = NormalizedRect(g_selection);
            Rect shown(
                ox + static_cast<int>(std::lround(s.left * g_zoom)),
                oy + static_cast<int>(std::lround(s.top * g_zoom)),
                (std::max)(1, static_cast<int>(std::lround((s.right - s.left) * g_zoom))),
                (std::max)(1, static_cast<int>(std::lround((s.bottom - s.top) * g_zoom))));
            SolidBrush fill(Color(52, 54, 137, 255));
            Pen outline(Color(235, 106, 175, 255), 1.5f);
            graphics.FillRectangle(&fill, shown);
            graphics.DrawRectangle(&outline, shown);
        }
    }

    HPEN edge = CreatePen(PS_SOLID, 1, RGB(92, 98, 114));
    HGDIOBJ oldPen = SelectObject(dc, edge);
    HGDIOBJ oldBrush = SelectObject(dc, GetStockObject(NULL_BRUSH));
    Rectangle(dc, imageRect.left, imageRect.top, imageRect.right, imageRect.bottom);
    SelectObject(dc, oldBrush);
    SelectObject(dc, oldPen);
    DeleteObject(edge);

    if ((g_tool == Tool::Brush || g_tool == Tool::Eraser) &&
        PtInRect(&canvas, g_mouse)) {
        POINT image{};
        if (ScreenToImage(g_mouse, image, false)) {
            const int radius = (std::max)(2, static_cast<int>(std::lround(g_brushSize * g_zoom / 2.0)));
            HPEN cursorPen = CreatePen(PS_SOLID, 1,
                g_tool == Tool::Eraser ? RGB(255, 255, 255) : g_brushColor);
            HGDIOBJ previousPen = SelectObject(dc, cursorPen);
            HGDIOBJ previousBrush = SelectObject(dc, GetStockObject(NULL_BRUSH));
            Ellipse(dc, g_mouse.x - radius, g_mouse.y - radius,
                g_mouse.x + radius + 1, g_mouse.y + radius + 1);
            SelectObject(dc, previousBrush);
            SelectObject(dc, previousPen);
            DeleteObject(cursorPen);
        }
    }
    RestoreDC(dc, saved);
}

static void PaintWindow()
{
    PAINTSTRUCT ps{};
    HDC dc = BeginPaint(g_hwnd, &ps);
    RECT client{};
    GetClientRect(g_hwnd, &client);
    const int width = (std::max)(1, static_cast<int>(client.right - client.left));
    const int height = (std::max)(1, static_cast<int>(client.bottom - client.top));
    HDC memory = CreateCompatibleDC(dc);
    HBITMAP surface = CreateCompatibleBitmap(dc, width, height);
    HGDIOBJ oldBitmap = SelectObject(memory, surface);

    FillColor(memory, client, RGB(18, 20, 26));
    DrawToolbar(memory, client);
    DrawCanvas(memory);

    RECT status{ client.left, (std::max)(client.top, client.bottom - STATUS_H), client.right, client.bottom };
    FillColor(memory, status, RGB(31, 34, 42));
    RECT statusTop{ status.left, status.top, status.right, status.top + 1 };
    FillColor(memory, statusTop, RGB(67, 72, 86));
    SelectObject(memory, g_fontSmall);
    SetBkMode(memory, TRANSPARENT);
    SetTextColor(memory, RGB(190, 196, 208));
    RECT textRect = status;
    InflateRect(&textRect, -10, 0);
    const std::wstring statusText = StatusText();
    DrawTextW(memory, statusText.c_str(), -1, &textRect,
        DT_LEFT | DT_VCENTER | DT_SINGLELINE | DT_END_ELLIPSIS);

    BitBlt(dc, 0, 0, width, height, memory, 0, 0, SRCCOPY);
    SelectObject(memory, oldBitmap);
    DeleteObject(surface);
    DeleteDC(memory);
    EndPaint(g_hwnd, &ps);
}

// ============================== command routing ==============================

static void SetTool(Tool tool)
{
    g_tool = tool;
    UpdateMenuState();
    InvalidateAll();
    Toast(ToolName());
}

static void NewCanvasCommand()
{
    if (!ConfirmDestructiveAction()) return;
    int width = g_doc.Valid() ? g_doc.width : 1280;
    int height = g_doc.Valid() ? g_doc.height : 720;
    if (!PromptTwo(L"New canvas", L"新增畫布",
        L"Width (pixels)", L"闊度（像素）", L"Height (pixels)", L"高度（像素）",
        width, height, 1, 16000))
        return;
    if (!PixelCountAllowed(width, height)) {
        ErrorBox(Tr(L"That size exceeds the 32-million-pixel safety limit.",
            L"呢個尺寸超過 3,200 萬像素安全上限。"));
        return;
    }
    NewDocument(width, height, true);
}

static void OpenCommand()
{
    if (!ConfirmDestructiveAction()) return;
    const std::wstring path = OpenDialogPath();
    if (!path.empty()) OpenPath(path);
}

static void DispatchCommand(UINT command)
{
    switch (command) {
    case CMD_NEW: NewCanvasCommand(); break;
    case CMD_OPEN: OpenCommand(); break;
    case CMD_SAVE: SaveDocument(false); break;
    case CMD_SAVEAS: SaveDocument(true); break;
    case CMD_EXIT: SendMessageW(g_hwnd, WM_CLOSE, 0, 0); break;
    case CMD_UNDO: Undo(); break;
    case CMD_REDO: Redo(); break;
    case CMD_SELECTALL:
        if (g_doc.Valid()) {
            g_selection = RECT{ 0, 0, g_doc.width, g_doc.height };
            UpdateMenuState();
            InvalidateAll();
        }
        break;
    case CMD_CLEARSEL: ClearSelection(); UpdateMenuState(); break;
    case CMD_CLEARPIXELS: ClearSelectedPixels(); break;
    case CMD_TOOL_SELECT: SetTool(Tool::Select); break;
    case CMD_TOOL_BRUSH: SetTool(Tool::Brush); break;
    case CMD_TOOL_ERASER: SetTool(Tool::Eraser); break;
    case CMD_TOOL_FILL: SetTool(Tool::Fill); break;
    case CMD_BRUSH_COLOR: ChooseBrushColor(); break;
    case CMD_BRUSH_SIZE: {
        int size = g_brushSize;
        if (PromptOne(L"Brush size", L"畫筆大小", L"Diameter (1 to 200 px)",
            L"直徑（1 至 200 像素）", size, 1, 200)) {
            g_brushSize = size;
            InvalidateAll();
        }
        break;
    }
    case CMD_CROP: CropToSelection(); break;
    case CMD_RESIZE: ResizeImage(); break;
    case CMD_ROTATE_CW: RotateImage(true); break;
    case CMD_ROTATE_CCW: RotateImage(false); break;
    case CMD_FLIP_H: FlipImage(true); break;
    case CMD_FLIP_V: FlipImage(false); break;
    case CMD_BRIGHTNESS: AdjustBrightness(); break;
    case CMD_CONTRAST: ApplyContrastAdjustment(); break;
    case CMD_SATURATION: AdjustSaturation(); break;
    case CMD_GAMMA: AdjustGamma(); break;
    case CMD_AUTOLEVELS: AutoLevels(); break;
    case CMD_GRAYSCALE: Grayscale(); break;
    case CMD_INVERT: Invert(); break;
    case CMD_SEPIA: Sepia(); break;
    case CMD_BLUR: BoxBlur(); break;
    case CMD_SHARPEN: Sharpen(); break;
    case CMD_EDGES: DetectEdges(); break;
    case CMD_ZOOMIN: SetZoom(g_zoom * 1.25, false); break;
    case CMD_ZOOMOUT: SetZoom(g_zoom / 1.25, false); break;
    case CMD_ZOOM100: SetZoom(1.0, false); break;
    case CMD_ZOOMFIT: ZoomFit(); break;
    case CMD_LANG_EN:
        g_lang = Lang::En; BuildMenus(); UpdateTitle(); InvalidateAll(); break;
    case CMD_LANG_ZH:
        g_lang = Lang::Zh; BuildMenus(); UpdateTitle(); InvalidateAll(); break;
    case CMD_LANG_BOTH:
        g_lang = Lang::Both; BuildMenus(); UpdateTitle(); InvalidateAll(); break;
    case CMD_ABOUT:
        MessageBoxW(g_hwnd,
            Tr(L"WinForge Image Editor (ImageForge)\n\n"
               L"A native, offline raster studio built with Win32 and GDI+.\n"
               L"Open/save PNG, JPEG, BMP, GIF and TIFF; paint, erase, fill, select, crop, "
               L"resize, transform, adjust and filter with undo/redo.\n\n"
               L"Mouse: left drag uses the active tool; right drag pans; Ctrl+wheel zooms.",
               L"WinForge 影像編輯器（ImageForge）\n\n"
               L"用 Win32 同 GDI+ 製作嘅原生離線點陣圖工作室。\n"
               L"支援開啟／儲存 PNG、JPEG、BMP、GIF、TIFF；畫筆、擦膠、填色、揀選、裁剪、"
               L"調整大小、變形、調色、濾鏡，同埋復原／重做。\n\n"
               L"滑鼠：左鍵拖曳使用目前工具；右鍵拖曳平移；Ctrl+滾輪縮放。").c_str(),
            L"WinForge Image Editor · 影像編輯器",
            MB_OK | MB_ICONINFORMATION);
        break;
    }
}

static UINT ToolbarHitTest(POINT point)
{
    for (const ToolbarItem& item : g_toolbar)
        if (PtInRect(&item.rect, point)) return item.command;
    return 0;
}

static int ScrollTrackPosition(int bar)
{
    SCROLLINFO si{};
    si.cbSize = sizeof(si);
    si.fMask = SIF_TRACKPOS;
    return GetScrollInfo(g_hwnd, bar, &si) ? si.nTrackPos : 0;
}

// ============================== window procedure ============================

static LRESULT CALLBACK MainProc(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp)
{
    switch (msg) {
    case WM_CREATE: {
        g_hwnd = hwnd;
        g_font = CreateFontW(-17, 0, 0, 0, FW_SEMIBOLD, FALSE, FALSE, FALSE,
            DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY,
            DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI");
        g_fontSmall = CreateFontW(-14, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE,
            DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY,
            DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI");
        DragAcceptFiles(hwnd, TRUE);
        BuildMenus();
        NewDocument(1280, 720, false);
        return 0;
    }
    case WM_SIZE:
        if (g_fitMode && g_doc.Valid()) ZoomFit();
        else UpdateScrollbars();
        InvalidateAll();
        return 0;
    case WM_PAINT:
        PaintWindow();
        return 0;
    case WM_ERASEBKGND:
        return 1;
    case WM_COMMAND:
        DispatchCommand(LOWORD(wp));
        return 0;
    case WM_HSCROLL: {
        const int code = LOWORD(wp);
        const int track = (code == SB_THUMBTRACK || code == SB_THUMBPOSITION) ?
            ScrollTrackPosition(SB_HORZ) : HIWORD(wp);
        ScrollAxis(SB_HORZ, code, track);
        return 0;
    }
    case WM_VSCROLL: {
        const int code = LOWORD(wp);
        const int track = (code == SB_THUMBTRACK || code == SB_THUMBPOSITION) ?
            ScrollTrackPosition(SB_VERT) : HIWORD(wp);
        ScrollAxis(SB_VERT, code, track);
        return 0;
    }
    case WM_MOUSEWHEEL: {
        const int delta = GET_WHEEL_DELTA_WPARAM(wp);
        const UINT keys = GET_KEYSTATE_WPARAM(wp);
        if (keys & MK_CONTROL) {
            SetZoom(delta > 0 ? g_zoom * 1.15 : g_zoom / 1.15, false);
        }
        else if (keys & MK_SHIFT) {
            g_scrollX -= delta / WHEEL_DELTA * 80;
            UpdateScrollbars();
            InvalidateAll();
        }
        else {
            g_scrollY -= delta / WHEEL_DELTA * 80;
            UpdateScrollbars();
            InvalidateAll();
        }
        return 0;
    }
    case WM_LBUTTONDOWN: {
        SetFocus(hwnd);
        const POINT point{ GET_X_LPARAM(lp), GET_Y_LPARAM(lp) };
        if (point.y < TOOLBAR_H) {
            const UINT command = ToolbarHitTest(point);
            if (command && ToolbarEnabled(command)) DispatchCommand(command);
            return 0;
        }
        POINT image{};
        if (!ScreenToImage(point, image, false)) {
            if (g_tool == Tool::Select) {
                SetRectEmpty(&g_selection);
                UpdateMenuState();
                InvalidateAll();
            }
            return 0;
        }
        if (g_tool == Tool::Select) {
            g_selecting = true;
            g_selectAnchor = image;
            g_selection = RECT{ image.x, image.y, image.x, image.y };
            SetCapture(hwnd);
        }
        else if (g_tool == Tool::Brush || g_tool == Tool::Eraser) {
            if (!PushUndo()) return 0;
            g_painting = true;
            g_lastPaint = image;
            PaintLine(image, image, g_tool);
            g_doc.dirty = true;
            UpdateTitle();
            UpdateMenuState();
            SetCapture(hwnd);
            InvalidateAll();
        }
        else if (g_tool == Tool::Fill) {
            FloodFillAt(image.x, image.y);
        }
        return 0;
    }
    case WM_MOUSEMOVE: {
        const POINT point{ GET_X_LPARAM(lp), GET_Y_LPARAM(lp) };
        g_mouse = point;
        const UINT oldHover = g_hoverCommand;
        g_hoverCommand = point.y < TOOLBAR_H ? ToolbarHitTest(point) : 0;
        if (oldHover != g_hoverCommand) InvalidateRect(hwnd, nullptr, FALSE);

        if (g_selecting) {
            POINT image{};
            if (ScreenToImage(point, image, true)) {
                g_selection.left = (std::min)(g_selectAnchor.x, image.x);
                g_selection.top = (std::min)(g_selectAnchor.y, image.y);
                g_selection.right = (std::max)(g_selectAnchor.x, image.x) + 1;
                g_selection.bottom = (std::max)(g_selectAnchor.y, image.y) + 1;
                InvalidateAll();
            }
        }
        else if (g_painting) {
            POINT image{};
            if (ScreenToImage(point, image, true)) {
                PaintLine(g_lastPaint, image, g_tool);
                g_lastPaint = image;
                InvalidateAll();
            }
        }
        else if (g_panning) {
            g_scrollX -= point.x - g_lastPan.x;
            g_scrollY -= point.y - g_lastPan.y;
            g_lastPan = point;
            UpdateScrollbars();
            InvalidateAll();
        }
        else if (g_tool == Tool::Brush || g_tool == Tool::Eraser) {
            InvalidateAll();
        }
        return 0;
    }
    case WM_LBUTTONUP:
        if (g_selecting) {
            g_selecting = false;
            ReleaseCapture();
            UpdateMenuState();
            InvalidateAll();
        }
        if (g_painting) {
            g_painting = false;
            ReleaseCapture();
            MarkChanged(g_tool == Tool::Eraser ? L"Erased pixels" : L"Brush stroke",
                g_tool == Tool::Eraser ? L"已擦除像素" : L"畫筆筆劃");
        }
        return 0;
    case WM_RBUTTONDOWN: {
        const POINT point{ GET_X_LPARAM(lp), GET_Y_LPARAM(lp) };
        const RECT canvas = CanvasRect();
        if (PtInRect(&canvas, point) && g_doc.Valid()) {
            g_panning = true;
            g_lastPan = point;
            g_fitMode = false;
            SetCapture(hwnd);
            UpdateMenuState();
            SetCursor(LoadCursorW(nullptr, IDC_SIZEALL));
        }
        return 0;
    }
    case WM_RBUTTONUP:
        if (g_panning) {
            g_panning = false;
            ReleaseCapture();
            SetCursor(LoadCursorW(nullptr, IDC_ARROW));
        }
        return 0;
    case WM_CAPTURECHANGED:
        g_selecting = false;
        g_painting = false;
        g_panning = false;
        return 0;
    case WM_SETCURSOR:
        if (LOWORD(lp) == HTCLIENT) {
            POINT p{};
            GetCursorPos(&p);
            ScreenToClient(hwnd, &p);
            const RECT canvas = CanvasRect();
            if (PtInRect(&canvas, p)) {
                SetCursor(LoadCursorW(nullptr,
                    g_panning ? IDC_SIZEALL :
                    (g_tool == Tool::Select ? IDC_CROSS : IDC_CROSS)));
                return TRUE;
            }
        }
        break;
    case WM_KEYDOWN:
        if (wp == VK_ESCAPE) {
            ClearSelection();
            UpdateMenuState();
            return 0;
        }
        if (!(GetKeyState(VK_CONTROL) & 0x8000)) {
            if (wp == 'S') SetTool(Tool::Select);
            else if (wp == 'B') SetTool(Tool::Brush);
            else if (wp == 'E') SetTool(Tool::Eraser);
            else if (wp == 'F') SetTool(Tool::Fill);
        }
        break;
    case WM_DROPFILES: {
        HDROP drop = reinterpret_cast<HDROP>(wp);
        wchar_t path[32768]{};
        if (DragQueryFileW(drop, 0, path, static_cast<UINT>(sizeof(path) / sizeof(path[0]))) &&
            ConfirmDestructiveAction())
            OpenPath(path);
        DragFinish(drop);
        return 0;
    }
    case WM_TIMER:
        if (wp == TIMER_TOAST) {
            KillTimer(hwnd, TIMER_TOAST);
            g_toast.clear();
            InvalidateAll();
            return 0;
        }
        break;
    case WM_CLOSE:
        if (ConfirmDestructiveAction()) DestroyWindow(hwnd);
        return 0;
    case WM_DESTROY:
        DragAcceptFiles(hwnd, FALSE);
        if (g_font) { DeleteObject(g_font); g_font = nullptr; }
        if (g_fontSmall) { DeleteObject(g_fontSmall); g_fontSmall = nullptr; }
        g_hwnd = nullptr;
        PostQuitMessage(0);
        return 0;
    }
    return DefWindowProcW(hwnd, msg, wp, lp);
}

// ============================== entry point =================================

extern "C" int WINAPI wWinMain(HINSTANCE hInst, HINSTANCE, LPWSTR commandLine, int show)
{
    g_hinst = hInst;
    SetProcessDPIAware();
    CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);

    GdiplusStartupInput gdiplusInput;
    if (GdiplusStartup(&g_gdiplusToken, &gdiplusInput, nullptr) != Ok) {
        MessageBoxW(nullptr, L"GDI+ could not start. · 無法啟動 GDI+。",
            L"WinForge Image Editor", MB_OK | MB_ICONERROR);
        CoUninitialize();
        return 1;
    }

    INITCOMMONCONTROLSEX controls{ sizeof(controls), ICC_STANDARD_CLASSES };
    InitCommonControlsEx(&controls);

    WNDCLASSEXW wc{};
    wc.cbSize = sizeof(wc);
    wc.style = CS_HREDRAW | CS_VREDRAW;
    wc.lpfnWndProc = MainProc;
    wc.hInstance = hInst;
    wc.hIcon = LoadIconW(nullptr, IDI_APPLICATION);
    wc.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    wc.hbrBackground = nullptr;
    wc.lpszClassName = L"WinForgeImageForgeWindow";
    wc.hIconSm = wc.hIcon;
    if (!RegisterClassExW(&wc)) {
        GdiplusShutdown(g_gdiplusToken);
        CoUninitialize();
        return 1;
    }

    HWND hwnd = CreateWindowExW(0, wc.lpszClassName,
        L"WinForge Image Editor · 影像編輯器",
        WS_OVERLAPPEDWINDOW | WS_HSCROLL | WS_VSCROLL | WS_CLIPCHILDREN,
        CW_USEDEFAULT, CW_USEDEFAULT, 1280, 820,
        nullptr, nullptr, hInst, nullptr);
    if (!hwnd) {
        GdiplusShutdown(g_gdiplusToken);
        CoUninitialize();
        return 1;
    }

    static const ACCEL acceleratorTable[] = {
        { FCONTROL | FVIRTKEY, 'N', CMD_NEW },
        { FCONTROL | FVIRTKEY, 'O', CMD_OPEN },
        { FCONTROL | FVIRTKEY, 'S', CMD_SAVE },
        { FCONTROL | FSHIFT | FVIRTKEY, 'S', CMD_SAVEAS },
        { FCONTROL | FVIRTKEY, 'Z', CMD_UNDO },
        { FCONTROL | FVIRTKEY, 'Y', CMD_REDO },
        { FCONTROL | FVIRTKEY, 'A', CMD_SELECTALL },
        { FVIRTKEY, VK_DELETE, CMD_CLEARPIXELS },
        { FCONTROL | FVIRTKEY, VK_OEM_PLUS, CMD_ZOOMIN },
        { FCONTROL | FVIRTKEY, VK_ADD, CMD_ZOOMIN },
        { FCONTROL | FVIRTKEY, VK_OEM_MINUS, CMD_ZOOMOUT },
        { FCONTROL | FVIRTKEY, VK_SUBTRACT, CMD_ZOOMOUT },
        { FCONTROL | FVIRTKEY, '0', CMD_ZOOMFIT },
        { FCONTROL | FVIRTKEY, '1', CMD_ZOOM100 },
        { FVIRTKEY, VK_F1, CMD_ABOUT }
    };
    HACCEL accelerators = CreateAcceleratorTableW(
        const_cast<LPACCEL>(acceleratorTable),
        static_cast<int>(sizeof(acceleratorTable) / sizeof(acceleratorTable[0])));

    ShowWindow(hwnd, show);
    UpdateWindow(hwnd);

    if (commandLine && *commandLine) {
        std::wstring path = commandLine;
        while (!path.empty() && (path.front() == L'"' || iswspace(path.front()))) path.erase(path.begin());
        while (!path.empty() && (path.back() == L'"' || iswspace(path.back()))) path.pop_back();
        if (!path.empty()) OpenPath(path);
    }

    MSG msg{};
    while (GetMessageW(&msg, nullptr, 0, 0) > 0) {
        if (!TranslateAcceleratorW(hwnd, accelerators, &msg)) {
            TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }
    }

    if (accelerators) DestroyAcceleratorTable(accelerators);
    GdiplusShutdown(g_gdiplusToken);
    CoUninitialize();
    return static_cast<int>(msg.wParam);
}
