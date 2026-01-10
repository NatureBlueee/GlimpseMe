#include "clipboard_monitor.h"
#include "storage.h"
#include "utils.h"
#include "debug_log.h"
#include "context/context_manager.h"
#include "context/adapters/browser_adapter.h"
#include "context/adapters/wechat_adapter.h"
#include "context/adapters/vscode_adapter.h"
#include "context/adapters/notion_adapter.h"
#include <shellapi.h>

// Global variables
ClipboardMonitor g_monitor;
Storage g_storage;
std::shared_ptr<ContextManager> g_contextManager;
NOTIFYICONDATAW g_nid = {};
bool g_monitoring = true;
WNDPROC g_originalWndProc = nullptr;

// Hotkey ID
#define HOTKEY_QUIT 1

// Tray icon menu IDs
#define ID_TRAY_EXIT    1001
#define ID_TRAY_PAUSE   1002
#define ID_TRAY_OPEN    1003
#define ID_TRAY_ICON    1

// Last clipboard entry
static ClipboardEntry g_lastEntry;

// Ctrl+C+C detection
static HHOOK g_keyboardHook = nullptr;
static DWORD g_lastCtrlCTime = 0;

// Inter-process communication with C# FloatingTool
static UINT WM_GLIMPSEME_SHOW_FLOATING = 0;

// Forward declarations
void CreateTrayIcon(HWND hwnd, HINSTANCE hInstance);
void RemoveTrayIcon();
void ShowTrayMenu(HWND hwnd);
void OpenHistoryFile();
LRESULT CALLBACK TrayWindowProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam);
LRESULT CALLBACK LowLevelKeyboardProc(int nCode, WPARAM wParam, LPARAM lParam);

// Keyboard hook for Ctrl+C+C detection
LRESULT CALLBACK LowLevelKeyboardProc(int nCode, WPARAM wParam, LPARAM lParam) {
    if (nCode == HC_ACTION && wParam == WM_KEYDOWN) {
        KBDLLHOOKSTRUCT* kb = (KBDLLHOOKSTRUCT*)lParam;
        
        // Only care about 'C' key with Ctrl held
        if (kb->vkCode == 'C' && (GetAsyncKeyState(VK_CONTROL) & 0x8000)) {
            // Skip if Shift or Alt is also pressed (that's a different shortcut)
            if (!(GetAsyncKeyState(VK_SHIFT) & 0x8000) && !(GetAsyncKeyState(VK_MENU) & 0x8000)) {
                DWORD now = GetTickCount();
                
                if (g_lastCtrlCTime > 0 && (now - g_lastCtrlCTime) < 500) {
                    // Second Ctrl+C within 500ms - broadcast to C# FloatingTool
                    DEBUG_LOG("Ctrl+C+C detected! Broadcasting to FloatingTool...");
                    g_lastCtrlCTime = 0;

                    // Write last entry to temp file for C# to read
                    if (!g_lastEntry.content.empty()) {
                        g_storage.WriteTempEntry(g_lastEntry);
                    }

                    // Broadcast to all windows (C# FloatingTool will receive this)
                    if (WM_GLIMPSEME_SHOW_FLOATING != 0) {
                        PostMessage(HWND_BROADCAST, WM_GLIMPSEME_SHOW_FLOATING, 0, 0);
                    }
                } else {
                    // First Ctrl+C - just record time
                    g_lastCtrlCTime = now;
                }
            }
        }
    }
    
    return CallNextHookEx(g_keyboardHook, nCode, wParam, lParam);
}

int WINAPI wWinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, LPWSTR lpCmdLine, int nCmdShow) {
    (void)hPrevInstance;
    (void)lpCmdLine;
    (void)nCmdShow;
    
    CoInitializeEx(NULL, COINIT_APARTMENTTHREADED);

    // Register message for IPC with C# FloatingTool
    WM_GLIMPSEME_SHOW_FLOATING = RegisterWindowMessageW(L"WM_GLIMPSEME_SHOW_FLOATING");

    std::wstring appDataPath = Utils::GetAppDataPath();
    DebugLog::Instance().Initialize(appDataPath);
    DEBUG_LOG("Starting GlimpseMe...");
    
    if (!g_storage.Initialize(appDataPath)) {
        MessageBoxW(NULL, L"Failed to initialize storage!", L"GlimpseMe Error", MB_ICONERROR);
        return 1;
    }
    DEBUG_LOG("Storage initialized");

    g_contextManager = std::make_shared<ContextManager>();
    if (!g_contextManager->Initialize()) {
        MessageBoxW(NULL, L"Failed to initialize context manager!", L"GlimpseMe Error", MB_ICONERROR);
        return 1;
    }
    DEBUG_LOG("ContextManager initialized");

    // Register adapters
    g_contextManager->RegisterAdapter(std::make_shared<BrowserAdapter>(5000));
    g_contextManager->RegisterAdapter(std::make_shared<WeChatAdapter>(5000, 5));
    g_contextManager->RegisterAdapter(std::make_shared<VSCodeAdapter>(5000));
    g_contextManager->RegisterAdapter(std::make_shared<NotionAdapter>(5000));
    DEBUG_LOG("Adapters registered");

    if (!g_monitor.Initialize(hInstance)) {
        MessageBoxW(NULL, L"Failed to initialize clipboard monitor!", L"GlimpseMe Error", MB_ICONERROR);
        return 1;
    }
    DEBUG_LOG("Clipboard monitor initialized");

    g_monitor.SetContextManager(g_contextManager);

    // Clipboard callback - store last entry
    g_monitor.SetCallback([](const ClipboardEntry& entry) {
        if (g_monitoring) {
            g_lastEntry = entry;
        }
    });
    
    CreateTrayIcon(g_monitor.GetWindowHandle(), hInstance);
    
    // Quit hotkey
    RegisterHotKey(g_monitor.GetWindowHandle(), HOTKEY_QUIT, MOD_CONTROL | MOD_SHIFT, 'Q');
    
    // Keyboard hook for Ctrl+C+C
    g_keyboardHook = SetWindowsHookExW(WH_KEYBOARD_LL, LowLevelKeyboardProc, hInstance, 0);
    DEBUG_LOG(g_keyboardHook ? "Keyboard hook installed" : "Hook failed!");
    
    // Startup notification
    g_nid.uFlags = NIF_INFO;
    wcscpy_s(g_nid.szInfoTitle, L"GlimpseMe");
    wcscpy_s(g_nid.szInfo, L"Ctrl+C then quickly Ctrl+C again to annotate");
    g_nid.dwInfoFlags = NIIF_INFO;
    Shell_NotifyIconW(NIM_MODIFY, &g_nid);
    
    g_monitor.Run();
    
    // Cleanup
    if (g_keyboardHook) UnhookWindowsHookEx(g_keyboardHook);
    UnregisterHotKey(g_monitor.GetWindowHandle(), HOTKEY_QUIT);
    RemoveTrayIcon();
    CoUninitialize();
    
    return 0;
}

void CreateTrayIcon(HWND hwnd, HINSTANCE hInstance) {
    (void)hInstance;
    g_nid.cbSize = sizeof(NOTIFYICONDATAW);
    g_nid.hWnd = hwnd;
    g_nid.uID = ID_TRAY_ICON;
    g_nid.uFlags = NIF_ICON | NIF_MESSAGE | NIF_TIP;
    g_nid.uCallbackMessage = WM_USER + 1;
    g_nid.hIcon = LoadIcon(NULL, IDI_APPLICATION);
    wcscpy_s(g_nid.szTip, L"GlimpseMe");
    Shell_NotifyIconW(NIM_ADD, &g_nid);
    g_originalWndProc = (WNDPROC)SetWindowLongPtrW(hwnd, GWLP_WNDPROC, (LONG_PTR)TrayWindowProc);
}

LRESULT CALLBACK TrayWindowProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam) {
    if (msg == WM_USER + 1) {
        if (lParam == WM_RBUTTONUP || lParam == WM_LBUTTONUP) {
            ShowTrayMenu(hwnd);
        }
        return 0;
    }

    if (msg == WM_HOTKEY && wParam == HOTKEY_QUIT) {
        g_monitor.Stop();
        return 0;
    }

    if (g_originalWndProc) {
        return CallWindowProcW(g_originalWndProc, hwnd, msg, wParam, lParam);
    }
    return DefWindowProcW(hwnd, msg, wParam, lParam);
}

void RemoveTrayIcon() {
    Shell_NotifyIconW(NIM_DELETE, &g_nid);
}

void ShowTrayMenu(HWND hwnd) {
    HMENU hMenu = CreatePopupMenu();
    AppendMenuW(hMenu, MF_STRING, ID_TRAY_OPEN, L"Open History");
    AppendMenuW(hMenu, MF_SEPARATOR, 0, NULL);
    AppendMenuW(hMenu, MF_STRING | (g_monitoring ? MF_CHECKED : 0), ID_TRAY_PAUSE, L"Monitoring");
    AppendMenuW(hMenu, MF_SEPARATOR, 0, NULL);
    AppendMenuW(hMenu, MF_STRING, ID_TRAY_EXIT, L"Exit");
    
    POINT pt;
    GetCursorPos(&pt);
    SetForegroundWindow(hwnd);
    
    int cmd = TrackPopupMenu(hMenu, TPM_RETURNCMD | TPM_NONOTIFY, pt.x, pt.y, 0, hwnd, NULL);
    
    if (cmd == ID_TRAY_EXIT) g_monitor.Stop();
    else if (cmd == ID_TRAY_PAUSE) {
        g_monitoring = !g_monitoring;
        wcscpy_s(g_nid.szTip, g_monitoring ? L"GlimpseMe" : L"GlimpseMe (Paused)");
        Shell_NotifyIconW(NIM_MODIFY, &g_nid);
    }
    else if (cmd == ID_TRAY_OPEN) OpenHistoryFile();
    
    DestroyMenu(hMenu);
}

void OpenHistoryFile() {
    ShellExecuteW(NULL, L"open", g_storage.GetFilePath().c_str(), NULL, NULL, SW_SHOWNORMAL);
}
