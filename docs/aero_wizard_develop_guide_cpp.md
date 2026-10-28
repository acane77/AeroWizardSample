# Aero Wizard C++ 开发指南

`cpp` 目录是原生 Win32 Aero Wizard，行为与仓库根目录的 C# 示例一致。返回箭头、标题栏，以及标题栏并入客户区的外观，都由当前系统的 comctl32 按本机主题绘制。Windows 11 上是细箭头；更早的系统上是该系统自己的箭头。不要在 Windows 11 上模仿旧版玻璃标题栏。

页面内容现在只是占位：蓝色标题 `Hello World`，正文 `page 1` / `page 2`。接实际功能时，改 `CreatePageTemplate` 和对话框过程，不要换掉这套向导外壳。

C# 版的对应说明在 [aero_wizard_develop_guide.md](aero_wizard_develop_guide.md)。

## 不要做的事

- 不要自己画返回箭头，也不要用自绘控件代替标题栏里的 Back。
- 不要调用 `DwmExtendFrameIntoClientArea`。Aero Wizard 已经把标题栏延伸进客户区，再扩一次会盖住系统返回按钮。
- 不要去掉 `cpp/app.manifest` 里的 `Microsoft.Windows.Common-Controls` 6.0 依赖，也不要去掉 `WinMain` 里的 `CoInitializeEx(..., COINIT_APARTMENTTHREADED)`。没有 comctl32 v6 或 STA，Aero Wizard 不会出现。
- 不要去掉清单里的 `dpiAwareness` `PerMonitorV2`。进程需要按每个显示器的 DPI 创建向导，而不是由系统把整窗放大。这里没有 WinForms 的 `App.config`。
- 不要手写一份 `PROPSHEETHEADERW` / `PROPSHEETPAGEW` 来替换 `prsht.h`。`AeroWizard.cpp` 开头的 `static_assert` 要求布局与头文件一致：64 位 header 96、page 104；32 位 header 52、page 56。断言失败就不要改断言去迁就，先核对 `_WIN32_WINNT` 和包含进来的 SDK 结构。
- 不要定义 `NONAMELESSUNION`。现在的代码直接写 `page.pResource`、`header.ppsp`，这个宏会把匿名联合改成具名联合。
- `AeroWizard.cpp` 必须先包含 `<windows.h>`，再包含 `<commctrl.h>` 和 `<prsht.h>`。颠倒顺序时，`commctrl.h` 里的 `_Return_type_success_(return >= 0)` 还没有 SAL 宏，编译器会在 `return` 上报语法错误。
- 不要把初始化函数命名为 `InitCommonControls`。`commctrl.h` 里已有同名 API，调用会有二义性。现在的函数是 `InitWizardCommonControls`。

## 入口和文件

`WinMain` 把线程设成 STA 后调用 `AeroWizard::Show()`。`PropertySheetW` 自己跑模态消息循环，所以这里没有自写的 `GetMessage` 循环。创建失败时 `Show` 抛出 `std::system_error`，`main.cpp` 用 `MessageBoxW` 显示系统错误文本。

入口是 `WinMain`，不是 `wWinMain`。CMake 的 `WIN32` 可执行文件使用 `WinMainCRTStartup`。`UNICODE` 仍然有定义，窗口 API 用 `W` 版本。

| 文件 | 作用 |
| --- | --- |
| `cpp/src/AeroWizard.cpp` | 内存对话框模板、两页对话框过程、`PropertySheetW` |
| `cpp/src/AeroWizard.h` | `AeroWizard::Show()` |
| `cpp/src/main.cpp` | STA 入口和错误对话框 |
| `cpp/app.manifest` | 启用 comctl32 6.0，声明 Windows 10 / 11，并把进程设为 PerMonitorV2 |
| `cpp/app.rc` | 把清单嵌成资源：`1 24 "app.manifest"` |
| `cpp/CMakeLists.txt` | C++17、`UNICODE`、`_WIN32_WINNT=0x0A00`，链接 `comctl32` 和 `ole32` |

`_WIN32_WINNT=0x0A00` 让 `PSH_AEROWIZARD` 可见。它从 Windows Vista 起就存在，不要把目标版本降到 `0x0600` 以下。

MSVC 上 CMake 加了 `/MANIFEST:NO`。清单只从 `app.rc` 的 `RT_MANIFEST` 进来。链接器再生成一份默认清单时，两个 `RT_MANIFEST` 会冲突，或者 v6 依赖丢失，向导退回旧外观。

## 窗口怎么创建

`Show` 填一个 `PROPSHEETHEADERW`，标志是：

```text
PSH_WIZARD | PSH_AEROWIZARD | PSH_PROPSHEETPAGE
```

`pszCaption` 是标题栏里返回箭头右边的文字，现在是 `Hello World`。`ppsp` 指向栈上的 `PROPSHEETPAGEW pages[2]`，`nPages` 是 2，`nStartPage` 是 0。然后调用 `PropertySheetW`。返回值 `-1` 表示创建失败，会抛出 `std::system_error`，错误码是 `GetLastError()`。用户点 Finish 或 Cancel 后函数才返回。

每一页由 `FillPage` 填写，标志是：

```text
PSP_DLGINDIRECT | PSP_USEHEADERTITLE | PSP_USEHEADERSUBTITLE
```

`pResource` 指向 `PageTemplate::data()`，也就是内存里的 `DLGTEMPLATE`，不是资源脚本。`pszHeaderTitle` 是客户区顶部的蓝色标题。`pfnDlgProc` 是该页的 `DLGPROC`：`Page1DlgProc` 或 `Page2DlgProc`。

`InitWizardCommonControls` 用 `ICC_WIN95_CLASSES` 调用 `InitCommonControlsEx`。视觉样式本身来自清单里的 comctl32 6.0，不来自这次初始化。

## 页眉和正文

蓝色标题来自 `pszHeaderTitle`，由向导绘制。

`pszHeaderSubTitle` 已经设成 `page 1` / `page 2`，但 Windows 11 的 Aero Wizard 不绘制这个副标题。用户能看见的正文是对话框模板里的 `STATIC` 控件（控件 ID `kStaticControlId`，值 100）。接功能时：

- 蓝色大标题继续放在 `pszHeaderTitle`。
- 说明和输入控件放进 `DLGTEMPLATE`。
- 不要指望 `pszHeaderSubTitle` 会显示出来。

## 内存要活到向导关闭

`PropertySheetW` 在返回之前一直使用这些指针：

- `pages` 数组和 `header`
- 两个 `PageTemplate`。字节先写入 `vector<uint8_t>`，再复制到 `vector<uint32_t>`，保证 `DLGTEMPLATE*` 按 DWORD 对齐
- `caption`、页眉标题、副标题这些 `std::wstring` 的 `c_str()`

它们都是 `Show` 的局部变量，析构发生在 `PropertySheetW` 返回之后。不要改成临时量，例如 `header.pszCaption = std::wstring(L"Hello World").c_str()`，调用开始前字符串已经销毁。

对话框过程是有静态生存期的 `CALLBACK` 函数，没有 C# 那种委托回收问题。新增的模板和宽字符串同样要放在会活过 `PropertySheetW` 的对象里。

## 翻页和按钮

对话框过程目前只处理 `WM_NOTIFY` / `PSN_SETACTIVE`（通知码 `-200`）。`OnPageNotify` 把 `lParam` 转成 `NMHDR*`，读 `code`。这和 C# 里从 `IntPtr.Size * 2` 读 `UINT` 是同一字段。

向导窗口是页面的父窗口，用 `GetParent(页面 hwnd)` 取得。按钮消息要 `PostMessageW`，不要 `SendMessageW`。在 `PSN_SETACTIVE` 里同步发送会重入向导。

| 消息 | 值 | 作用 |
| --- | --- | --- |
| `PSM_SETWIZBUTTONS` | `WM_USER + 112` | 决定出现哪些按钮 |
| `PSM_ENABLEWIZBUTTONS` | `WM_USER + 139` | 单独启用或禁用 Aero 标题栏返回箭头 |

先发 `PSM_SETWIZBUTTONS`，再发 `PSM_ENABLEWIZBUTTONS`。`wParam` 是要变成的状态，`lParam` 是要改动的按钮掩码。

| 页 | `PSM_SETWIZBUTTONS` 的 lParam | 返回箭头 |
| --- | --- | --- |
| 第一页 `PageKind::First` | `PSWIZB_NEXT` (0x2) | `PostMessageW(sheet, PSM_ENABLEWIZBUTTONS, 0, PSWIZB_BACK)`，禁用 |
| 最后一页 `PageKind::Last` | `PSWIZB_BACK \| PSWIZB_FINISH` (0x1 \| 0x4) | `PostMessageW(sheet, PSM_ENABLEWIZBUTTONS, PSWIZB_BACK, PSWIZB_BACK)` |
| 中间页 | `PSWIZB_BACK \| PSWIZB_NEXT` | 同样启用返回箭头 |

Cancel 一直在。底部没有 Back 按钮，返回只存在于标题栏。

处理完 `PSN_SETACTIVE` 后，`SetWindowLongPtrW(hwnd, DWLP_MSGRESULT, 0)` 并返回 `TRUE`，表示允许显示这一页。`SetWindowLongPtrW` 在 32 位上就是 `SetWindowLongW`，不要再拆一套 32 位调用。

## 接实际功能

### 在页面上加控件

改 `CreatePageTemplate`。模板是经典 `DLGTEMPLATE`，不是 `DLGTEMPLATEEX`。页面客户区是 317 x 143 对话框单位。现有控件从偏移 0,0 起，宽 300、高 24。新控件放在它下面，避免重叠。

模板头按这个顺序写：

1. `DWORD` style：`DS_SETFONT | DS_FIXEDSYS | WS_CHILD | WS_DISABLED | WS_CAPTION`
2. `DWORD` extended style：0
3. `WORD` `cdit`：控件个数，现在是 1
4. `short` x、y、cx、cy：0, 0, 317, 143
5. 菜单、窗口类、标题各一个 `WORD` 0
6. `DS_SETFONT` 要求的字号 `WORD` 8，然后是带结尾 NUL 的宽字符串 `MS Shell Dlg`
7. `AlignDword`

每个控件是一个按 DWORD 对齐的 `DLGITEMTEMPLATE`：

1. 把模板开头的 `cdit` 改成控件个数。
2. 在字体名结束并 `AlignDword` 之后，按顺序写每个控件。
3. 每个控件写完类名、标题（含 NUL）和 creation data（一个 `WORD` 0）后，再 `AlignDword`。
4. 控件 ID 不要复用 100，除非故意替换那个占位 `STATIC`。

预定义类用 `0xFFFF` 加原子：

| 原子 | 类 |
| --- | --- |
| `0x0080` | Button |
| `0x0081` | Edit |
| `0x0082` | Static |
| `0x0085` | ComboBox |

控件样式至少要 `WS_CHILD | WS_VISIBLE`。需要键盘切换时加上 `WS_TABSTOP`（`0x00010000`）。复选框用 `BS_AUTOCHECKBOX`（`0x3`）。坐标是对话框单位。

模板解析错时，`PropertySheetW` 会返回 `-1`，或者页面是空的。先核对 `cdit`、`0xFFFF` 类原子和 DWORD 对齐。`PageTemplate` 用 `uint32_t` 向量保存结果；改回未对齐的 `uint8_t` 缓冲会让 `data()` 失去 DWORD 对齐。

### 读取控件和阻止翻页

在对应页的对话框过程里处理消息。`hwnd` 是这一页，不是向导外框。

```cpp
HWND edit = GetDlgItem(hwnd, 101);
wchar_t buffer[512];
GetWindowTextW(edit, buffer, static_cast<int>(std::size(buffer)));
```

翻页通知码在 `NMHDR.code` 里，按无符号值和 `prsht.h` 的宏比较：

| 通知 | 值 | 返回 `FALSE` 且不改结果 | `DWLP_MSGRESULT = -1` 且返回 `TRUE` |
| --- | --- | --- | --- |
| `PSN_WIZNEXT` | -207 | 进入下一页 | 留在当前页 |
| `PSN_WIZBACK` | -206 | 回到上一页 | 留在当前页 |
| `PSN_WIZFINISH` | -208 | 关闭向导 | 留在当前页 |
| `PSN_QUERYCANCEL` | -209 | 允许 Cancel | 拒绝关闭 |

要拒绝时：

```cpp
if (notify->code == PSN_WIZNEXT) {
    SetWindowLongPtrW(hwnd, DWLP_MSGRESULT, -1);
    return TRUE;
}
```

现在的 `OnPageNotify` 对没处理的消息返回 `FALSE`，所以 Next、Back、Finish、Cancel 都走系统默认行为。加校验时只拦截需要拦住的通知。

`PSN_KILLACTIVE`（-201）在离开页面前到达，适合把控件值写进共享状态。允许离开时把 `DWLP_MSGRESULT` 设为 0 并返回 `TRUE`；要留下时设为 `TRUE` 并返回 `TRUE`。

### 页与页共享数据

在 `Show` 里放一个会活过 `PropertySheetW` 的对象，例如栈上的 `WizardState`。对话框过程是普通函数，不能像 C# 闭包那样直接捕获它。做法是：

1. `FillPage` 时把 `page.lParam` 设成这个对象的地址。整数页号也可以。
2. 处理 `WM_INITDIALOG`。此时对话框过程的 `lParam` 指向系统复制过的 `PROPSHEETPAGEW`，从那份副本读出 `lParam`，再 `SetWindowLongPtrW(hwnd, GWLP_USERDATA, page->lParam)`。
3. 之后的 `PSN_SETACTIVE`、`PSN_WIZNEXT` 用 `GetWindowLongPtrW(hwnd, GWLP_USERDATA)` 拿回状态。

`WM_INITDIALOG` 的 `lParam` 本身只在这条消息里有效，不要把那份 `PROPSHEETPAGEW` 的地址留到后面。状态对象仍然要活到 `PropertySheetW` 返回。

### 增加一页

1. 再调用一次 `CreatePageTemplate`，得到的 `PageTemplate` 放在 `Show` 的局部变量里。
2. 增加一个 `CALLBACK` 对话框过程。中间页不要复用 `PageKind::Last`。
3. 扩大 `pages` 数组，`header.nPages` 加 1，并给新页调用 `FillPage`。
4. 中间页在 `PSN_SETACTIVE` 里使用 `PSWIZB_BACK | PSWIZB_NEXT`，再按最后一页的方式启用返回箭头。
5. 只有最后一页使用 `PSWIZB_FINISH`。

默认从 `nStartPage = 0` 开始。页面顺序就是数组顺序。

### 改标题

- 标题栏文字：`Show` 里赋给 `header.pszCaption` 的字符串。
- 蓝色标题：该页的 `pszHeaderTitle`。
- 正文：模板里的控件，不是副标题字符串。

## 构建

在仓库根目录：

```powershell
cmake -S cpp -B cpp/build -G "Visual Studio 17 2022" -A x64
cmake --build cpp/build --config Release
```

Visual Studio 生成器的程序是 `cpp\build\Release\AeroWizard.exe`。单配置生成器（例如 Ninja）的程序直接在 `cpp\build\AeroWizard.exe`。向导是模态的，关掉它进程才退出。这是 `WIN32` 子系统程序，没有控制台；失败时看消息框。
