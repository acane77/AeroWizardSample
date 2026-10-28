# 代理说明

修改 C# 向导窗口、页面内容或 `AeroWizard.cs` 里的 PInvoke 之前，先读 [docs/aero_wizard_develop_guide.md](docs/aero_wizard_develop_guide.md)。

修改 `cpp` 目录里的向导窗口、页面模板、对话框过程或 CMake 之前，先读 [docs/aero_wizard_develop_guide_cpp.md](docs/aero_wizard_develop_guide_cpp.md)。C++ 版用 `prsht.h` 的 `PROPSHEETHEADERW` / `PROPSHEETPAGEW`，不要手写结构体替换它们，也不要改掉 `static_assert` 要求的布局。清单在 `cpp/app.manifest`，由 `cpp/app.rc` 嵌入；保留 comctl32 6.0、PerMonitorV2，以及 `WinMain` 里的 STA 初始化。`AeroWizard.cpp` 要先包含 `<windows.h>`，再包含 `<commctrl.h>`。

两个版本的返回箭头和延伸进客户区的标题栏都由系统 Aero Wizard 绘制。不要手画返回按钮，也不要调用 `DwmExtendFrameIntoClientArea`。新功能加在页面模板和对话框过程里。
