#pragma once

// 原生 Aero Wizard，对应仓库根目录的 AeroWizard.cs。
// 改页面或向导行为之前先看 docs/aero_wizard_develop_guide.md。
// 返回箭头和延伸进客户区的标题栏由系统绘制，不要手画，也不要调用 DwmExtendFrameIntoClientArea。

namespace AeroWizard {

// 模态显示两页向导。PropertySheetW 返回后本函数才返回。
// 创建失败时抛出 std::system_error，error code 是 Win32 错误码。
void Show();

} // namespace AeroWizard
