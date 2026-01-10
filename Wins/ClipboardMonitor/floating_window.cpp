#include "floating_window.h"
#include "utils.h"
#include "debug_log.h"
#include <windowsx.h>

const wchar_t* FloatingWindow::WINDOW_CLASS_NAME = L"GlimpseMeFloatingWindow";

// Window dimensions
static const int WINDOW_WIDTH = 280;
static const int WINDOW_HEIGHT = 140;

// Button dimensions
static const int BUTTON_WIDTH = 70;
static const int BUTTON_HEIGHT = 35;
static const int BUTTON_MARGIN = 12;

// Colors
static const COLORREF BG_COLOR = RGB(45, 45, 48);
static const COLORREF BTN_LIKE_COLOR = RGB(76, 175, 80);
static const COLORREF BTN_NEUTRAL_COLOR = RGB(158, 158, 158);
static const COLORREF BTN_DISLIKE_COLOR = RGB(244, 67, 54);
static const COLORREF TEXT_COLOR = RGB(255, 255, 255);
static const COLORREF EDIT_BG_COLOR = RGB(60, 60, 64);

// Font
static HFONT g_font = nullptr;
static HBRUSH g_bgBrush = nullptr;
static HBRUSH g_editBgBrush = nullptr;

// Edit control subclassing
static WNDPROC g_originalEditProc = nullptr;
static FloatingWindow* g_floatingWindowInstance = nullptr;

// Edit control subclass procedure to handle Enter/Esc
static LRESULT CALLBACK EditSubclassProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam) {
    if (msg == WM_KEYDOWN) {
        if (wParam == VK_RETURN && g_floatingWindowInstance) {
            g_floatingWindowInstance->OnSubmit();
            return 0;
        } else if (wParam == VK_ESCAPE && g_floatingWindowInstance) {
            g_floatingWindowInstance->OnCancel();
            return 0;
        }
    }
    return CallWindowProcW(g_originalEditProc, hwnd, msg, wParam, lParam);
}

FloatingWindow::FloatingWindow()
{
}

FloatingWindow::~FloatingWindow()
{
    if (m_hwnd) {
        DestroyWindow(m_hwnd);
    }
    if (g_font) {
        DeleteObject(g_font);
        g_font = nullptr;
    }
    if (g_bgBrush) {
        DeleteObject(g_bgBrush);
        g_bgBrush = nullptr;
    }
    if (g_editBgBrush) {
        DeleteObject(g_editBgBrush);
        g_editBgBrush = nullptr;
    }
}

bool FloatingWindow::Initialize(HINSTANCE hInstance)
{
    m_hInstance = hInstance;

    // Register window class
    WNDCLASSEXW wc = {};
    wc.cbSize = sizeof(WNDCLASSEXW);
    wc.style = CS_HREDRAW | CS_VREDRAW;
    wc.lpfnWndProc = FloatingWindow::WindowProc;
    wc.hInstance = hInstance;
    wc.hCursor = LoadCursor(NULL, IDC_ARROW);
    wc.hbrBackground = CreateSolidBrush(BG_COLOR);
    wc.lpszClassName = WINDOW_CLASS_NAME;

    if (!RegisterClassExW(&wc)) {
        DWORD err = GetLastError();
        if (err != ERROR_CLASS_ALREADY_EXISTS) {
            DEBUG_LOG("FloatingWindow: Failed to register class, error=" + std::to_string(err));
            return false;
        }
    }

    // Create brushes and font
    g_bgBrush = CreateSolidBrush(BG_COLOR);
    g_editBgBrush = CreateSolidBrush(EDIT_BG_COLOR);
    g_font = CreateFontW(16, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE,
                         DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
                         CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_DONTCARE,
                         L"Microsoft YaHei");

    // Create popup window (initially hidden)
    m_hwnd = CreateWindowExW(
        WS_EX_TOOLWINDOW | WS_EX_TOPMOST,
        WINDOW_CLASS_NAME,
        L"GlimpseMe",
        WS_POPUP | WS_BORDER,
        0, 0, WINDOW_WIDTH, WINDOW_HEIGHT,
        NULL, NULL, hInstance, this
    );

    if (!m_hwnd) {
        DEBUG_LOG("FloatingWindow: Failed to create window");
        return false;
    }

    // Create child controls
    CreateControls();

    DEBUG_LOG("FloatingWindow: Initialized successfully");
    return true;
}

void FloatingWindow::CreateControls()
{
    int y = BUTTON_MARGIN;
    int x = BUTTON_MARGIN;

    // Like button
    m_btnLike = CreateWindowExW(0, L"BUTTON", L"\U0001F44D 喜欢",
        WS_CHILD | WS_VISIBLE | BS_PUSHBUTTON | BS_CENTER,
        x, y, BUTTON_WIDTH, BUTTON_HEIGHT,
        m_hwnd, (HMENU)(INT_PTR)ID_BTN_LIKE, m_hInstance, NULL);
    x += BUTTON_WIDTH + 10;

    // Neutral button
    m_btnNeutral = CreateWindowExW(0, L"BUTTON", L"\U0001F610 一般",
        WS_CHILD | WS_VISIBLE | BS_PUSHBUTTON | BS_CENTER,
        x, y, BUTTON_WIDTH, BUTTON_HEIGHT,
        m_hwnd, (HMENU)(INT_PTR)ID_BTN_NEUTRAL, m_hInstance, NULL);
    x += BUTTON_WIDTH + 10;

    // Dislike button
    m_btnDislike = CreateWindowExW(0, L"BUTTON", L"\U0001F44E 不喜欢",
        WS_CHILD | WS_VISIBLE | BS_PUSHBUTTON | BS_CENTER,
        x, y, BUTTON_WIDTH + 10, BUTTON_HEIGHT,
        m_hwnd, (HMENU)(INT_PTR)ID_BTN_DISLIKE, m_hInstance, NULL);

    y += BUTTON_HEIGHT + BUTTON_MARGIN;
    x = BUTTON_MARGIN;

    // Note input field
    m_editNote = CreateWindowExW(WS_EX_CLIENTEDGE, L"EDIT", L"",
        WS_CHILD | WS_VISIBLE | ES_AUTOHSCROLL,
        x, y, WINDOW_WIDTH - 2 * BUTTON_MARGIN, 28,
        m_hwnd, (HMENU)(INT_PTR)ID_EDIT_NOTE, m_hInstance, NULL);
    SendMessage(m_editNote, EM_SETCUEBANNER, TRUE, (LPARAM)L"添加想法...");

    y += 28 + 8;

    // Select All checkbox
    m_chkSelectAll = CreateWindowExW(0, L"BUTTON", L"全选",
        WS_CHILD | WS_VISIBLE | BS_AUTOCHECKBOX,
        x, y, 80, 20,
        m_hwnd, (HMENU)(INT_PTR)ID_CHK_SELECTALL, m_hInstance, NULL);

    // Subclass the edit control to handle Enter/Esc keys
    g_floatingWindowInstance = this;
    g_originalEditProc = (WNDPROC)SetWindowLongPtrW(m_editNote, GWLP_WNDPROC, (LONG_PTR)EditSubclassProc);

    // Apply font to all controls
    if (g_font) {
        SendMessage(m_btnLike, WM_SETFONT, (WPARAM)g_font, TRUE);
        SendMessage(m_btnNeutral, WM_SETFONT, (WPARAM)g_font, TRUE);
        SendMessage(m_btnDislike, WM_SETFONT, (WPARAM)g_font, TRUE);
        SendMessage(m_editNote, WM_SETFONT, (WPARAM)g_font, TRUE);
        SendMessage(m_chkSelectAll, WM_SETFONT, (WPARAM)g_font, TRUE);
    }
}

void FloatingWindow::ShowAtCursor()
{
    POINT pt;
    if (GetCursorPos(&pt)) {
        ShowAt(pt.x, pt.y);
    }
}

void FloatingWindow::ShowAt(int x, int y)
{
    // Get monitor working area
    POINT pt = { x, y };
    HMONITOR hMonitor = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
    MONITORINFO mi = { sizeof(mi) };
    GetMonitorInfo(hMonitor, &mi);

    // Ensure window stays within screen bounds
    if (x + WINDOW_WIDTH > mi.rcWork.right) {
        x = mi.rcWork.right - WINDOW_WIDTH;
    }
    if (y + WINDOW_HEIGHT > mi.rcWork.bottom) {
        y = mi.rcWork.bottom - WINDOW_HEIGHT;
    }
    if (x < mi.rcWork.left) {
        x = mi.rcWork.left;
    }
    if (y < mi.rcWork.top) {
        y = mi.rcWork.top;
    }

    // Reset state
    m_selectedReaction.clear();
    SetWindowTextW(m_editNote, L"");
    SendMessage(m_chkSelectAll, BM_SETCHECK, BST_UNCHECKED, 0);

    // Position and show
    SetWindowPos(m_hwnd, HWND_TOPMOST, x, y, 0, 0, SWP_NOSIZE | SWP_SHOWWINDOW);
    SetForegroundWindow(m_hwnd);
    SetFocus(m_editNote);
    m_visible = true;

    DEBUG_LOG("FloatingWindow: Shown at " + std::to_string(x) + "," + std::to_string(y));
}

void FloatingWindow::Hide()
{
    ShowWindow(m_hwnd, SW_HIDE);
    m_visible = false;
    DEBUG_LOG("FloatingWindow: Hidden");
}

bool FloatingWindow::IsVisible() const
{
    return m_visible;
}

void FloatingWindow::SetCallback(AnnotationCallback callback)
{
    m_callback = callback;
}

void FloatingWindow::OnReactionClick(const std::string& reaction)
{
    m_selectedReaction = reaction;
    DEBUG_LOG("FloatingWindow: Reaction selected: " + reaction);
}

void FloatingWindow::OnSubmit()
{
    // Guard against double submit (e.g., Enter key + WM_ACTIVATE both triggering)
    if (!m_visible) {
        return;
    }
    
    DEBUG_LOG("FloatingWindow: Submit");

    // Check if select all is checked
    bool selectAll = (SendMessage(m_chkSelectAll, BM_GETCHECK, 0, 0) == BST_CHECKED);
    if (selectAll) {
        PerformSelectAll();
    }

    // Build annotation data
    AnnotationData data;
    data.reaction = m_selectedReaction;
    data.note = GetNoteText();
    data.selectAll = selectAll;
    data.cancelled = false;

    // Hide window first
    Hide();

    // Call callback
    if (m_callback) {
        m_callback(data);
    }
}

void FloatingWindow::OnCancel()
{
    DEBUG_LOG("FloatingWindow: Cancelled");

    AnnotationData data;
    data.cancelled = true;

    Hide();

    if (m_callback) {
        m_callback(data);
    }
}

void FloatingWindow::PerformSelectAll()
{
    DEBUG_LOG("FloatingWindow: Performing Select All (Ctrl+A)");

    // Hide our window first to not interfere
    ShowWindow(m_hwnd, SW_HIDE);
    Sleep(50);

    // Simulate Ctrl+A
    INPUT inputs[4] = {};

    // Press Ctrl
    inputs[0].type = INPUT_KEYBOARD;
    inputs[0].ki.wVk = VK_CONTROL;

    // Press A
    inputs[1].type = INPUT_KEYBOARD;
    inputs[1].ki.wVk = 'A';

    // Release A
    inputs[2].type = INPUT_KEYBOARD;
    inputs[2].ki.wVk = 'A';
    inputs[2].ki.dwFlags = KEYEVENTF_KEYUP;

    // Release Ctrl
    inputs[3].type = INPUT_KEYBOARD;
    inputs[3].ki.wVk = VK_CONTROL;
    inputs[3].ki.dwFlags = KEYEVENTF_KEYUP;

    SendInput(4, inputs, sizeof(INPUT));
    Sleep(100);

    // Now simulate Ctrl+C to copy (will be captured by ClipboardMonitor)
    inputs[1].ki.wVk = 'C';
    inputs[2].ki.wVk = 'C';
    SendInput(4, inputs, sizeof(INPUT));
    Sleep(100);

    DEBUG_LOG("FloatingWindow: Select All completed");
}

std::wstring FloatingWindow::GetNoteText()
{
    int len = GetWindowTextLengthW(m_editNote);
    if (len == 0) {
        return L"";
    }

    std::wstring text(len + 1, L'\0');
    GetWindowTextW(m_editNote, &text[0], len + 1);
    text.resize(len);
    return text;
}

LRESULT CALLBACK FloatingWindow::WindowProc(HWND hwnd, UINT uMsg, WPARAM wParam, LPARAM lParam)
{
    FloatingWindow* pThis = nullptr;

    if (uMsg == WM_NCCREATE) {
        CREATESTRUCT* cs = reinterpret_cast<CREATESTRUCT*>(lParam);
        pThis = reinterpret_cast<FloatingWindow*>(cs->lpCreateParams);
        SetWindowLongPtrW(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(pThis));
    } else {
        pThis = reinterpret_cast<FloatingWindow*>(GetWindowLongPtrW(hwnd, GWLP_USERDATA));
    }

    switch (uMsg) {
    case WM_COMMAND:
        if (pThis) {
            int id = LOWORD(wParam);
            int code = HIWORD(wParam);

            if (code == BN_CLICKED) {
                switch (id) {
                case FloatingWindow::ID_BTN_LIKE:
                    pThis->OnReactionClick("like");
                    break;
                case FloatingWindow::ID_BTN_NEUTRAL:
                    pThis->OnReactionClick("neutral");
                    break;
                case FloatingWindow::ID_BTN_DISLIKE:
                    pThis->OnReactionClick("dislike");
                    break;
                }
            }
        }
        break;

    case WM_KEYDOWN:
        if (pThis) {
            if (wParam == VK_RETURN) {
                pThis->OnSubmit();
                return 0;
            } else if (wParam == VK_ESCAPE) {
                pThis->OnCancel();
                return 0;
            }
        }
        break;

    case WM_CTLCOLOREDIT:
        if (g_editBgBrush) {
            HDC hdc = (HDC)wParam;
            SetTextColor(hdc, TEXT_COLOR);
            SetBkColor(hdc, EDIT_BG_COLOR);
            return (LRESULT)g_editBgBrush;
        }
        break;

    case WM_CTLCOLORSTATIC:
        if (g_bgBrush) {
            HDC hdc = (HDC)wParam;
            SetTextColor(hdc, TEXT_COLOR);
            SetBkColor(hdc, BG_COLOR);
            return (LRESULT)g_bgBrush;
        }
        break;

    case WM_ACTIVATE:
        if (LOWORD(wParam) == WA_INACTIVE && pThis && pThis->IsVisible()) {
            // Clicked outside - submit with current state
            pThis->OnSubmit();
        }
        break;

    case WM_DESTROY:
        return 0;
    }

    return DefWindowProcW(hwnd, uMsg, wParam, lParam);
}

bool FloatingWindow::ProcessMessage(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    (void)hwnd;
    (void)lParam;

    // Handle Enter key in edit control
    if (msg == WM_KEYDOWN && m_visible) {
        if (wParam == VK_RETURN) {
            OnSubmit();
            return true;
        } else if (wParam == VK_ESCAPE) {
            OnCancel();
            return true;
        }
    }
    return false;
}
