# 代理说明

修改向导窗口、页面内容或 `AeroWizard.cs` 里的 PInvoke 之前，先读 [docs/aero_wizard_develop_guide.md](docs/aero_wizard_develop_guide.md)。

返回箭头和延伸进客户区的标题栏由系统 Aero Wizard 绘制。不要手画返回按钮，也不要调用 `DwmExtendFrameIntoClientArea`。新功能加在页面模板和对话框过程里。
