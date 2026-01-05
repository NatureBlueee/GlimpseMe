#pragma once

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif

#include <windows.h>
#include <string>
#include <functional>

// Forward declarations
struct ClipboardEntry;

// Annotation data from user interaction
struct AnnotationData {
    std::string reaction;      // "like", "dislike", "neutral", "" (none)
    std::wstring note;         // User's note/thought
    bool selectAll = false;    // Whether user checked "select all"
    bool cancelled = false;    // Whether user cancelled (Esc)
};

// Callback when user completes annotation
using AnnotationCallback = std::function<void(const AnnotationData&)>;

/**
 * @brief Floating annotation window
 * 
 * A lightweight popup window that appears at the cursor position
 * when the user triggers the annotation hotkey.
 * 
 * Features:
 * - 3 reaction buttons: Like / Neutral / Dislike
 * - Text input field for notes
 * - "Select All" checkbox
 * - Enter to confirm, Esc to cancel
 */
class FloatingWindow {
public:
    FloatingWindow();
    ~FloatingWindow();

    /**
     * @brief Initialize the floating window
     * @param hInstance Application instance
     * @return true if successful
     */
    bool Initialize(HINSTANCE hInstance);

    /**
     * @brief Show the window at cursor position
     */
    void ShowAtCursor();

    /**
     * @brief Show the window at specific position
     * @param x X coordinate
     * @param y Y coordinate
     */
    void ShowAt(int x, int y);

    /**
     * @brief Hide the window
     */
    void Hide();

    /**
     * @brief Check if window is visible
     */
    bool IsVisible() const;

    /**
     * @brief Set callback for when annotation is complete
     * @param callback Callback function
     */
    void SetCallback(AnnotationCallback callback);

    /**
     * @brief Get window handle
     */
    HWND GetWindowHandle() const { return m_hwnd; }

    /**
     * @brief Process a message (called from TrayWindowProc)
     */
    bool ProcessMessage(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam);

private:
    // Window procedure
    static LRESULT CALLBACK WindowProc(HWND hwnd, UINT uMsg, WPARAM wParam, LPARAM lParam);

    // Create child controls
    void CreateControls();

    // Handle button clicks
    void OnReactionClick(const std::string& reaction);

    // Handle enter key
    void OnSubmit();

    // Handle escape key
    void OnCancel();

    // Perform select all (Ctrl+A + Ctrl+C)
    void PerformSelectAll();

    // Get note text from input field
    std::wstring GetNoteText();

    // Window handles
    HWND m_hwnd = nullptr;
    HINSTANCE m_hInstance = nullptr;

    // Child controls
    HWND m_btnLike = nullptr;
    HWND m_btnNeutral = nullptr;
    HWND m_btnDislike = nullptr;
    HWND m_editNote = nullptr;
    HWND m_chkSelectAll = nullptr;

    // State
    std::string m_selectedReaction;
    AnnotationCallback m_callback;
    bool m_visible = false;

    // Control IDs
    static const int ID_BTN_LIKE = 1001;
    static const int ID_BTN_NEUTRAL = 1002;
    static const int ID_BTN_DISLIKE = 1003;
    static const int ID_EDIT_NOTE = 1004;
    static const int ID_CHK_SELECTALL = 1005;

    // Window class name
    static const wchar_t* WINDOW_CLASS_NAME;
};
