#include "AeroWizard.h"

#include <objbase.h>
#include <windows.h>

#include <exception>
#include <string>
#include <system_error>

namespace {

void ReportError(const wchar_t* context, DWORD error)
{
    wchar_t* system = nullptr;
    const DWORD flags = FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS;
    FormatMessageW(flags,
                   nullptr,
                   error,
                   0,
                   reinterpret_cast<wchar_t*>(&system),
                   0,
                   nullptr);

    std::wstring text = context;
    if (system != nullptr) {
        text += L"\n";
        text += system;
        LocalFree(system);
    }

    MessageBoxW(nullptr, text.c_str(), L"Aero Wizard", MB_OK | MB_ICONERROR);
}

} // namespace

// 视觉样式来自嵌入清单里的 comctl32 6.0。DPI 是清单中的 PerMonitorV2。
// 这里保持 STA，对应 C# 入口上的 [STAThread]。
int APIENTRY WinMain(HINSTANCE, HINSTANCE, LPSTR, int)
{
    const HRESULT coInit = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
    if (FAILED(coInit)) {
        ReportError(L"无法初始化 STA。", static_cast<DWORD>(coInit));
        return 1;
    }

    int exitCode = 0;
    try {
        AeroWizard::Show();
    } catch (const std::system_error& ex) {
        ReportError(L"无法创建 Aero Wizard。", static_cast<DWORD>(ex.code().value()));
        exitCode = 1;
    } catch (const std::exception&) {
        ReportError(L"无法创建 Aero Wizard。", ERROR_GEN_FAILURE);
        exitCode = 1;
    }

    CoUninitialize();
    return exitCode;
}
