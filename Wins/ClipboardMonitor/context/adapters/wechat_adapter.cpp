#include "wechat_adapter.h"
#include "../../utils.h"
#include "../../debug_log.h"
#include <windows.h>
#include <chrono>
#include <sstream>
#include <algorithm>

WeChatAdapter::WeChatAdapter(int timeout, int messageCount)
    : m_timeout(timeout)
    , m_messageCount(messageCount)
{
}

bool WeChatAdapter::CanHandle(const std::wstring& processName,
                              const std::wstring& windowTitle)
{
    (void)windowTitle;  // Not used currently

    std::wstring lowerProcessName = Utils::ToLower(processName);
    return lowerProcessName == L"wechat.exe";
}

std::shared_ptr<ContextData> WeChatAdapter::GetContext(const SourceInfo& source)
{
    auto startTime = std::chrono::high_resolution_clock::now();

    // Create context object
    auto context = std::make_shared<WeChatContext>();
    context->adapterType = "wechat";
    context->success = false;

    try {
        // Initialize UI Automation
        UIAutomationHelper uiHelper;
        if (!uiHelper.Initialize()) {
            context->error = L"Failed to initialize UI Automation";
            DEBUG_LOG("WeChatAdapter: Failed to initialize UI Automation");
            return context;
        }

        // Get chat name
        std::wstring chatName = GetChatName(source.windowHandle, uiHelper);
        if (!chatName.empty()) {
            context->contactName = chatName;
            context->title = chatName;
            DEBUG_LOG("WeChatAdapter: Got chat name: " + Utils::WideToUtf8(chatName));
        }

        // Determine chat type
        if (!chatName.empty()) {
            context->chatType = DetermineChatType(chatName);
            DEBUG_LOG("WeChatAdapter: Chat type: " + Utils::WideToUtf8(context->chatType));
        }

        // Get recent messages
        std::vector<std::wstring> messages = GetRecentMessages(source.windowHandle, uiHelper, m_messageCount);
        if (!messages.empty()) {
            context->recentMessages = messages;
            context->messageCount = static_cast<int>(messages.size());
            DEBUG_LOG("WeChatAdapter: Got " + std::to_string(messages.size()) + " messages");
        }

        // Mark as successful if we got at least chat name
        if (!context->contactName.empty()) {
            context->success = true;

            // Add metadata
            context->metadata[L"message_count"] = std::to_wstring(context->messageCount);
            context->metadata[L"chat_type"] = context->chatType;
        } else {
            context->error = L"Failed to extract chat information";
            DEBUG_LOG("WeChatAdapter: Failed to get chat name");
        }

    } catch (const std::exception& ex) {
        context->success = false;
        context->error = L"Exception: " + Utils::Utf8ToWide(ex.what());
        DEBUG_LOG("WeChatAdapter: Exception: " + std::string(ex.what()));
    } catch (...) {
        context->success = false;
        context->error = L"Unknown exception";
        DEBUG_LOG("WeChatAdapter: Unknown exception");
    }

    // Calculate fetch time
    auto endTime = std::chrono::high_resolution_clock::now();
    context->fetchTimeMs = static_cast<int>(
        std::chrono::duration_cast<std::chrono::milliseconds>(endTime - startTime).count()
    );

    DEBUG_LOG("WeChatAdapter: Completed in " + std::to_string(context->fetchTimeMs) + "ms, success=" +
              (context->success ? "true" : "false"));

    return context;
}

std::wstring WeChatAdapter::GetChatName(HWND hwnd, UIAutomationHelper& uiHelper)
{
    if (hwnd == nullptr) {
        DEBUG_LOG("WeChatAdapter: Invalid HWND");
        return L"";
    }

    try {
        // Strategy 1: Try to find a Text or Static element near the top
        // WeChat usually displays the chat name in a prominent position

        // Get the root element
        IUIAutomation* automation = uiHelper.GetAutomation();
        if (!automation) {
            return L"";
        }

        IUIAutomationElement* rootElement = nullptr;
        HRESULT hr = automation->ElementFromHandle(hwnd, &rootElement);
        if (FAILED(hr) || !rootElement) {
            return L"";
        }

        // Try to find Text or Static elements
        IUIAutomationCondition* condition = nullptr;
        VARIANT var;
        var.vt = VT_I4;
        var.lVal = UIA_TextControlTypeId;  // Try Text controls

        hr = automation->CreatePropertyCondition(UIA_ControlTypePropertyId, var, &condition);
        if (SUCCEEDED(hr) && condition) {
            IUIAutomationElementArray* foundElements = nullptr;
            hr = rootElement->FindAll(TreeScope_Descendants, condition, &foundElements);

            if (SUCCEEDED(hr) && foundElements) {
                int length = 0;
                foundElements->get_Length(&length);

                // Get first few text elements and check which one looks like a chat name
                for (int i = 0; i < (length < 10 ? length : 10); i++) {
                    IUIAutomationElement* element = nullptr;
                    hr = foundElements->GetElement(i, &element);

                    if (SUCCEEDED(hr) && element) {
                        std::wstring text = uiHelper.GetElementText(element);
                        element->Release();

                        // Filter out empty text and common UI strings
                        if (!text.empty() &&
                            text.length() > 1 &&
                            text.length() < 100 &&  // Chat names are usually not too long
                            text.find(L"WeChat") == std::wstring::npos &&
                            text.find(L"微信") == std::wstring::npos) {

                            foundElements->Release();
                            condition->Release();
                            rootElement->Release();
                            return text;
                        }
                    }
                }

                foundElements->Release();
            }
            condition->Release();
        }

        // Strategy 2: Try window title as fallback
        // Sometimes the chat name appears in the window title
        rootElement->Release();

        // Window title might be like "ChatName - WeChat"
        wchar_t titleBuffer[256] = {0};
        GetWindowTextW(hwnd, titleBuffer, 256);
        std::wstring windowTitle(titleBuffer);

        if (!windowTitle.empty() && windowTitle != L"微信" && windowTitle != L"WeChat") {
            // Remove " - 微信" or " - WeChat" suffix
            size_t pos = windowTitle.find(L" - 微信");
            if (pos == std::wstring::npos) {
                pos = windowTitle.find(L" - WeChat");
            }
            if (pos != std::wstring::npos) {
                return windowTitle.substr(0, pos);
            }
            return windowTitle;
        }

    } catch (...) {
        DEBUG_LOG("WeChatAdapter: Exception in GetChatName");
    }

    return L"";
}

std::wstring WeChatAdapter::DetermineChatType(const std::wstring& chatName)
{
    // Heuristic rules for determining chat type:

    // 1. Group chat indicators
    if (chatName.find(L"群") != std::wstring::npos ||
        chatName.find(L"Group") != std::wstring::npos ||
        chatName.find(L"group") != std::wstring::npos) {
        return L"group";
    }

    // 2. Check for parentheses with numbers (common in group names like "工作群(123)")
    size_t openParen = chatName.find(L'(');
    size_t closeParen = chatName.find(L')');
    if (openParen != std::wstring::npos && closeParen != std::wstring::npos && closeParen > openParen) {
        std::wstring inParens = chatName.substr(openParen + 1, closeParen - openParen - 1);
        // If contains digits, likely a group
        if (std::any_of(inParens.begin(), inParens.end(), ::iswdigit)) {
            return L"group";
        }
    }

    // Default to private chat
    return L"private";
}

std::vector<std::wstring> WeChatAdapter::GetRecentMessages(HWND hwnd,
                                                           UIAutomationHelper& uiHelper,
                                                           int count)
{
    std::vector<std::wstring> messages;

    if (hwnd == nullptr || count <= 0) {
        return messages;
    }

    try {
        // Get the root element
        IUIAutomation* automation = uiHelper.GetAutomation();
        if (!automation) {
            return messages;
        }

        IUIAutomationElement* rootElement = nullptr;
        HRESULT hr = automation->ElementFromHandle(hwnd, &rootElement);
        if (FAILED(hr) || !rootElement) {
            return messages;
        }

        // WeChat's message list structure varies by version
        // The left side is the conversation list, the right side (larger) is the message area
        // Strategy: Find all List elements and pick the one that's likely the message area
        //   - The message area is usually the second List (index >= 1)
        //   - Or the one with more children (messages vs conversations)
        //   - Or the one positioned more to the right

        VARIANT var;
        var.vt = VT_I4;
        var.lVal = UIA_ListControlTypeId;

        IUIAutomationCondition* condition = nullptr;
        hr = automation->CreatePropertyCondition(UIA_ControlTypePropertyId, var, &condition);

        if (SUCCEEDED(hr) && condition) {
            IUIAutomationElementArray* foundElements = nullptr;
            hr = rootElement->FindAll(TreeScope_Descendants, condition, &foundElements);

            if (SUCCEEDED(hr) && foundElements) {
                int length = 0;
                foundElements->get_Length(&length);
                DEBUG_LOG("WeChatAdapter: Found " + std::to_string(length) + " List elements");

                // Find the best candidate for message list
                // Heuristic: Skip the first List (usually conversation list)
                // and find the List with the most children or largest bounding rect
                IUIAutomationElement* messageListElement = nullptr;
                int maxChildCount = 0;
                int bestListIndex = -1;

                for (int i = 0; i < length && i < 10; i++) {
                    IUIAutomationElement* listElement = nullptr;
                    hr = foundElements->GetElement(i, &listElement);

                    if (SUCCEEDED(hr) && listElement) {
                        // Get bounding rectangle to determine position
                        RECT rect = {0};
                        hr = listElement->get_CurrentBoundingRectangle(&rect);
                        int width = rect.right - rect.left;
                        int xPos = rect.left;

                        // Get child count as another heuristic
                        IUIAutomationCondition* trueCondition = nullptr;
                        automation->CreateTrueCondition(&trueCondition);
                        if (trueCondition) {
                            IUIAutomationElementArray* children = nullptr;
                            hr = listElement->FindAll(TreeScope_Children, trueCondition, &children);
                            int childCount = 0;
                            if (SUCCEEDED(hr) && children) {
                                children->get_Length(&childCount);
                                children->Release();
                            }
                            trueCondition->Release();

                            DEBUG_LOG("WeChatAdapter: List[" + std::to_string(i) + "] x=" + 
                                      std::to_string(xPos) + " width=" + std::to_string(width) +
                                      " children=" + std::to_string(childCount));

                            // The message list is typically:
                            // - Not the first list (index > 0)
                            // - Has a reasonable width (> 200 pixels)
                            // - Positioned more to the right (xPos > some threshold)
                            // 
                            // For WeChat, the conversation list is on the left (~300px wide)
                            // and the message area is on the right (much wider, x > 300)
                            
                            // Strategy: Prefer lists that are NOT the first one, and have children
                            // If first list is skipped and we find one with children, use it
                            if (i > 0 && childCount > 0 && width > 200) {
                                // This is likely the message list
                                if (childCount > maxChildCount) {
                                    maxChildCount = childCount;
                                    bestListIndex = i;
                                    if (messageListElement) {
                                        messageListElement->Release();
                                    }
                                    messageListElement = listElement;
                                    listElement = nullptr;  // Prevent release below
                                }
                            }
                        }

                        if (listElement) {
                            listElement->Release();
                        }
                    }
                }

                // If no good candidate found, try using position-based heuristic
                // The rightmost list is usually the message area
                if (!messageListElement && length > 1) {
                    int maxXPos = 0;
                    for (int i = 0; i < length && i < 10; i++) {
                        IUIAutomationElement* listElement = nullptr;
                        hr = foundElements->GetElement(i, &listElement);

                        if (SUCCEEDED(hr) && listElement) {
                            RECT rect = {0};
                            hr = listElement->get_CurrentBoundingRectangle(&rect);
                            int xPos = rect.left;
                            int width = rect.right - rect.left;

                            // Prefer rightmost list with reasonable width
                            if (xPos > maxXPos && width > 200) {
                                maxXPos = xPos;
                                bestListIndex = i;
                                if (messageListElement) {
                                    messageListElement->Release();
                                }
                                messageListElement = listElement;
                                listElement = nullptr;
                            }

                            if (listElement) {
                                listElement->Release();
                            }
                        }
                    }
                }

                DEBUG_LOG("WeChatAdapter: Selected list index " + std::to_string(bestListIndex) +
                          " with " + std::to_string(maxChildCount) + " children");

                // Extract messages from the selected list
                if (messageListElement) {
                    IUIAutomationCondition* trueCondition = nullptr;
                    automation->CreateTrueCondition(&trueCondition);

                    if (trueCondition) {
                        IUIAutomationElementArray* messageElements = nullptr;
                        hr = messageListElement->FindAll(TreeScope_Children, trueCondition, &messageElements);

                        if (SUCCEEDED(hr) && messageElements) {
                            int messageCount = 0;
                            messageElements->get_Length(&messageCount);

                            // Get last N messages (most recent)
                            int startIndex = (messageCount - count > 0 ? messageCount - count : 0);
                            for (int i = startIndex; i < messageCount; i++) {
                                IUIAutomationElement* msgElement = nullptr;
                                hr = messageElements->GetElement(i, &msgElement);

                                if (SUCCEEDED(hr) && msgElement) {
                                    std::wstring messageText = ExtractMessageText(msgElement, uiHelper);
                                    if (!messageText.empty()) {
                                        messages.push_back(messageText);
                                    }
                                    msgElement->Release();
                                }
                            }

                            messageElements->Release();
                        }
                        trueCondition->Release();
                    }

                    messageListElement->Release();
                }

                foundElements->Release();
            }
            condition->Release();
        }

        rootElement->Release();

    } catch (...) {
        DEBUG_LOG("WeChatAdapter: Exception in GetRecentMessages");
    }

    return messages;
}

std::wstring WeChatAdapter::ExtractMessageText(IUIAutomationElement* element,
                                              UIAutomationHelper& uiHelper)
{
    if (!element) {
        return L"";
    }

    try {
        // Try to get the element's name (which often contains the message text)
        std::wstring text = uiHelper.GetElementText(element);
        if (!text.empty()) {
            return text;
        }

        // If direct text is empty, try to find Text elements within
        IUIAutomation* automation = uiHelper.GetAutomation();
        if (!automation) {
            return L"";
        }

        IUIAutomationCondition* trueCondition = nullptr;
        automation->CreateTrueCondition(&trueCondition);

        if (trueCondition) {
            IUIAutomationElementArray* childElements = nullptr;
            HRESULT hr = element->FindAll(TreeScope_Descendants, trueCondition, &childElements);

            if (SUCCEEDED(hr) && childElements) {
                int length = 0;
                childElements->get_Length(&length);

                std::wstring combinedText;
                for (int i = 0; i < (length < 5 ? length : 5); i++) {  // Check first few children
                    IUIAutomationElement* child = nullptr;
                    hr = childElements->GetElement(i, &child);

                    if (SUCCEEDED(hr) && child) {
                        std::wstring childText = uiHelper.GetElementText(child);
                        if (!childText.empty()) {
                            if (!combinedText.empty()) {
                                combinedText += L" ";
                            }
                            combinedText += childText;
                        }
                        child->Release();
                    }
                }

                childElements->Release();
                trueCondition->Release();

                if (!combinedText.empty()) {
                    return combinedText;
                }
            }

            if (trueCondition) {
                trueCondition->Release();
            }
        }

    } catch (...) {
        DEBUG_LOG("WeChatAdapter: Exception in ExtractMessageText");
    }

    return L"";
}
