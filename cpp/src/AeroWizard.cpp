#include <windows.h>
#include <commctrl.h>
#include <prsht.h>

#include "AeroWizard.h"

#include <cstdint>
#include <cstring>
#include <cwchar>
#include <stdexcept>
#include <string>
#include <system_error>
#include <vector>

namespace {

// 与 prsht.h 以及 C# EnsureNativeLayout 使用的尺寸一致。
static_assert(sizeof(PROPSHEETHEADERW) == (sizeof(void*) == 8 ? 96u : 52u),
              "PROPSHEETHEADERW layout does not match prsht.h");
static_assert(sizeof(PROPSHEETPAGEW) == (sizeof(void*) == 8 ? 104u : 56u),
              "PROPSHEETPAGEW layout does not match prsht.h");

constexpr int kStaticControlId = 100;
constexpr std::uint16_t kStaticClassAtom = 0x0082;

struct PageTemplate {
    // uint32_t 保证模板指针按 DWORD 对齐。PropertySheetW 返回前必须一直活着。
    std::vector<std::uint32_t> words;

    const DLGTEMPLATE* data() const
    {
        return reinterpret_cast<const DLGTEMPLATE*>(words.data());
    }
};

void AppendBytes(std::vector<std::uint8_t>& bytes, const void* data, std::size_t size)
{
    const auto* begin = static_cast<const std::uint8_t*>(data);
    bytes.insert(bytes.end(), begin, begin + size);
}

void AppendWord(std::vector<std::uint8_t>& bytes, std::uint16_t value)
{
    AppendBytes(bytes, &value, sizeof(value));
}

void AppendDword(std::vector<std::uint8_t>& bytes, std::uint32_t value)
{
    AppendBytes(bytes, &value, sizeof(value));
}

void AppendWideString(std::vector<std::uint8_t>& bytes, const wchar_t* text)
{
    AppendBytes(bytes, text, (std::wcslen(text) + 1) * sizeof(wchar_t));
}

void AlignDword(std::vector<std::uint8_t>& bytes)
{
    while (bytes.size() % 4 != 0) {
        bytes.push_back(0);
    }
}

// 经典 DLGTEMPLATE，不是 DLGTEMPLATEEX。页面客户区 317 x 143 对话框单位。
PageTemplate CreatePageTemplate(const wchar_t* body)
{
    std::vector<std::uint8_t> bytes;

    const std::uint32_t style = DS_SETFONT | DS_FIXEDSYS | WS_CHILD | WS_DISABLED | WS_CAPTION;
    AppendDword(bytes, style);
    AppendDword(bytes, 0);
    AppendWord(bytes, 1);
    AppendWord(bytes, 0);
    AppendWord(bytes, 0);
    AppendWord(bytes, 317);
    AppendWord(bytes, 143);
    AppendWord(bytes, 0);
    AppendWord(bytes, 0);
    AppendWord(bytes, 0);
    AppendWord(bytes, 8);
    AppendWideString(bytes, L"MS Shell Dlg");
    AlignDword(bytes);

    const std::uint32_t itemStyle = WS_CHILD | WS_VISIBLE;
    AppendDword(bytes, itemStyle);
    AppendDword(bytes, 0);
    AppendWord(bytes, 0);
    AppendWord(bytes, 0);
    AppendWord(bytes, 300);
    AppendWord(bytes, 24);
    AppendWord(bytes, static_cast<std::uint16_t>(kStaticControlId));
    AppendWord(bytes, 0xFFFF);
    AppendWord(bytes, kStaticClassAtom);
    AppendWideString(bytes, body);
    AppendWord(bytes, 0);
    AlignDword(bytes);

    PageTemplate tmpl;
    tmpl.words.resize((bytes.size() + 3) / 4);
    std::memcpy(tmpl.words.data(), bytes.data(), bytes.size());
    if (reinterpret_cast<std::uintptr_t>(tmpl.data()) % 4 != 0) {
        throw std::runtime_error("dialog template is not DWORD aligned");
    }
    return tmpl;
}

void InitWizardCommonControls()
{
    INITCOMMONCONTROLSEX init{};
    init.dwSize = sizeof(init);
    init.dwICC = ICC_WIN95_CLASSES;
    if (!InitCommonControlsEx(&init)) {
        throw std::system_error(static_cast<int>(GetLastError()),
                                std::system_category(),
                                "InitCommonControlsEx");
    }
}

enum class PageKind {
    First,
    Last,
};

void ApplyWizardButtons(HWND page, PageKind kind)
{
    // 向导外框是页面的父窗口。按钮消息必须 PostMessage，避免在 PSN_SETACTIVE 里重入。
    const HWND sheet = GetParent(page);
    if (kind == PageKind::First) {
        PostMessageW(sheet, PSM_SETWIZBUTTONS, 0, PSWIZB_NEXT);
        PostMessageW(sheet, PSM_ENABLEWIZBUTTONS, 0, PSWIZB_BACK);
        return;
    }

    PostMessageW(sheet, PSM_SETWIZBUTTONS, 0, PSWIZB_BACK | PSWIZB_FINISH);
    PostMessageW(sheet, PSM_ENABLEWIZBUTTONS, PSWIZB_BACK, PSWIZB_BACK);
}

INT_PTR OnPageNotify(HWND hwnd, UINT message, LPARAM lParam, PageKind kind)
{
    if (message != WM_NOTIFY || lParam == 0) {
        return FALSE;
    }

    const auto* notify = reinterpret_cast<const NMHDR*>(lParam);
    if (notify->code != PSN_SETACTIVE) {
        return FALSE;
    }

    ApplyWizardButtons(hwnd, kind);
    SetWindowLongPtrW(hwnd, DWLP_MSGRESULT, 0);
    return TRUE;
}

INT_PTR CALLBACK Page1DlgProc(HWND hwnd, UINT message, WPARAM, LPARAM lParam)
{
    return OnPageNotify(hwnd, message, lParam, PageKind::First);
}

INT_PTR CALLBACK Page2DlgProc(HWND hwnd, UINT message, WPARAM, LPARAM lParam)
{
    return OnPageNotify(hwnd, message, lParam, PageKind::Last);
}

void FillPage(PROPSHEETPAGEW& page,
              const PageTemplate& tmpl,
              DLGPROC proc,
              const std::wstring& headerTitle,
              const std::wstring& headerSubtitle)
{
    page.dwSize = sizeof(page);
    page.dwFlags = PSP_DLGINDIRECT | PSP_USEHEADERTITLE | PSP_USEHEADERSUBTITLE;
    page.pResource = tmpl.data();
    page.pfnDlgProc = proc;
    page.pszHeaderTitle = headerTitle.c_str();
    page.pszHeaderSubTitle = headerSubtitle.c_str();
}

} // namespace

namespace AeroWizard {

void Show()
{
    InitWizardCommonControls();

    // 这些字符串和模板会一直被 PropertySheetW 使用，必须活到它返回。
    const std::wstring caption = L"Hello World";
    const std::wstring title = L"Hello World";
    const std::wstring subtitle1 = L"page 1";
    const std::wstring subtitle2 = L"page 2";
    const PageTemplate template1 = CreatePageTemplate(L"page 1");
    const PageTemplate template2 = CreatePageTemplate(L"page 2");

    PROPSHEETPAGEW pages[2]{};
    FillPage(pages[0], template1, Page1DlgProc, title, subtitle1);
    FillPage(pages[1], template2, Page2DlgProc, title, subtitle2);

    PROPSHEETHEADERW header{};
    header.dwSize = sizeof(header);
    header.dwFlags = PSH_WIZARD | PSH_AEROWIZARD | PSH_PROPSHEETPAGE;
    header.pszCaption = caption.c_str();
    header.nPages = 2;
    header.nStartPage = 0;
    header.ppsp = pages;

    const INT_PTR result = PropertySheetW(&header);
    if (result == -1) {
        throw std::system_error(static_cast<int>(GetLastError()),
                                std::system_category(),
                                "PropertySheetW");
    }
}

} // namespace AeroWizard
