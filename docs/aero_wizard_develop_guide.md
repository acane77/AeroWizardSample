# Aero Wizard 开发指南

这个程序是一个 Win32 Aero Wizard 示例。返回箭头、标题栏，以及标题栏并入客户区的外观，都由当前系统的 comctl32 按本机主题绘制。Windows 11 上是细箭头；更早的系统上是该系统自己的箭头。不要在 Windows 11 上模仿旧版玻璃标题栏。

页面内容现在只是占位：蓝色标题 `Hello World`，正文 `page 1` / `page 2`。接实际功能时，改页面模板和对话框过程，不要换掉这套向导外壳。

## 不要做的事

- 不要自己画返回箭头，也不要用 WinForms `Button` 或自绘控件代替标题栏里的 Back。
- 不要调用 `DwmExtendFrameIntoClientArea`。Aero Wizard 已经把标题栏延伸进客户区，再扩一次会盖住系统返回按钮。
- 不要去掉 `app.manifest` 里的 `Microsoft.Windows.Common-Controls` 6.0 依赖，也不要去掉 `Program.Main` 上的 `[STAThread]`。没有 comctl32 v6 或 STA，Aero Wizard 不会出现。
- 不要去掉清单里的 `dpiAwareness` `PerMonitorV2`，也不要去掉 `App.config` 里的同名设置。进程需要按每个显示器的 DPI 创建向导，而不是由系统把整窗放大。
- 不要改 `PropSheetHeader` / `PropSheetPage` 的字段顺序、类型或 `Pack`。`EnsureNativeLayout` 要求布局与 `prsht.h` 一致：64 位 header 96、page 104；32 位 header 52、page 56。

## 入口和文件

`Program.Main` 启用视觉样式后调用 `AeroWizard.Show()`。`PropertySheetW` 自己跑模态消息循环，所以这里没有 `Application.Run`。`Form1` 不参与向导。

| 文件 | 作用 |
| --- | --- |
| `AeroWizard.cs` | PInvoke、内存对话框模板、两页对话框过程 |
| `Program.cs` | STA 入口 |
| `app.manifest` | 启用 comctl32 6.0，声明 Windows 10 / 11，并把进程设为 PerMonitorV2 |
| `App.config` | WinForms 的 `DpiAwareness` 同样是 `PerMonitorV2` |
| `WindowsExtendsClientPInvoke.csproj` | `ApplicationManifest` 指向 `app.manifest` |

## 窗口怎么创建

`Show` 填一个 `PROPSHEETHEADERW`，标志是：

```text
PSH_WIZARD | PSH_AEROWIZARD | PSH_PROPSHEETPAGE
```

`pszCaption` 是标题栏里返回箭头右边的文字，现在是 `Hello World`。`ppsp` 指向一块非托管内存里的 `PROPSHEETPAGEW` 数组，`nPages` 是页数。然后调用 `comctl32!PropertySheetW`。返回值 `-1` 表示创建失败，会抛出 `Win32Exception`。用户点 Finish 或 Cancel 后函数才返回。

每一页的标志是：

```text
PSP_DLGINDIRECT | PSP_USEHEADERTITLE | PSP_USEHEADERSUBTITLE
```

`pResource` 指向内存里的 `DLGTEMPLATE`，不是资源脚本。`pszHeaderTitle` 是客户区顶部的蓝色标题。`pfnDlgProc` 是该页的对话框过程。

## 页眉和正文

蓝色标题来自 `pszHeaderTitle`，由向导绘制。

`pszHeaderSubTitle` 已经设成 `page 1` / `page 2`，但 Windows 11 的 Aero Wizard 不绘制这个副标题。用户能看见的正文是对话框模板里的 `STATIC` 控件（控件 ID 100）。接功能时：

- 蓝色大标题继续放在 `pszHeaderTitle`。
- 说明和输入控件放进 `DLGTEMPLATE`。
- 不要指望 `pszHeaderSubTitle` 会显示出来。

## 内存要活到向导关闭

`PropertySheetW` 在返回之前一直使用这些指针：

- 页数组、header、两个对话框模板
- `pszCaption`、页眉标题、副标题（`Marshal.StringToHGlobalUni`）
- 两个 `DialogProc` 委托。函数指针在 `GetFunctionPointerForDelegate` 时取走，委托本身必须用 `GC.KeepAlive` 留到 `PropertySheetW` 返回之后

这些释放都在 `Show` 的 `finally` 里。新增的非托管字符串、模板或委托也要同样处理，不能在向导还显示时释放。

## 翻页和按钮

对话框过程目前只处理 `WM_NOTIFY` / `PSN_SETACTIVE`（通知码 `-200`）。通知码在 `NMHDR.code`，偏移是 `IntPtr.Size * 2`。

向导窗口是页面的父窗口，用 `GetParent(页面hwnd)` 取得。按钮消息要 `PostMessage`，不要 `SendMessage`：

| 消息 | 值 | 作用 |
| --- | --- | --- |
| `PSM_SETWIZBUTTONS` | `WM_USER + 112` | 决定出现哪些按钮 |
| `PSM_ENABLEWIZBUTTONS` | `WM_USER + 139` | 单独启用或禁用 Aero 标题栏返回箭头 |

先发 `PSM_SETWIZBUTTONS`，再发 `PSM_ENABLEWIZBUTTONS`。

| 页 | `PSM_SETWIZBUTTONS` 的 lParam | 返回箭头 |
| --- | --- | --- |
| 第一页 | `PSWIZB_NEXT` (0x2) | `Enable(state=0, mask=PSWIZB_BACK)`，禁用 |
| 最后一页 | `PSWIZB_BACK \| PSWIZB_FINISH` (0x1 \| 0x4) | `Enable(state=PSWIZB_BACK, mask=PSWIZB_BACK)` |
| 中间页 | `PSWIZB_BACK \| PSWIZB_NEXT` | 同样启用返回箭头 |

Cancel 一直在。底部没有 Back 按钮，返回只存在于标题栏。

处理完 `PSN_SETACTIVE` 后，把 `DWLP_MSGRESULT`（0）设为 0 并返回 1，表示允许显示这一页。

## 接实际功能

### 在页面上加控件

改 `CreatePageTemplate`。模板是经典 `DLGTEMPLATE`，不是 `DLGTEMPLATEEX`。页面客户区是 317 x 143 对话框单位。现有控件从偏移 0,0 起，宽 300、高 24。新控件放在它下面，避免重叠。

每个控件是一个按 DWORD 对齐的 `DLGITEMTEMPLATE`：

1. 把模板开头的 `cdit` 改成控件个数。
2. 在字体名结束并 `AlignDword` 之后，按顺序写每个控件。
3. 每个控件写完类名、标题和 creation data（一个 `WORD` 0）后，再 `AlignDword`。
4. 控件 ID 不要复用 100，除非故意替换那个占位 `STATIC`。

预定义类用 `0xFFFF` 加原子：

| 原子 | 类 |
| --- | --- |
| `0x0080` | Button |
| `0x0081` | Edit |
| `0x0082` | Static |
| `0x0085` | ComboBox |

控件样式至少要 `WS_CHILD | WS_VISIBLE`。需要键盘切换时加上 `WS_TABSTOP`（`0x00010000`）。复选框用 `BS_AUTOCHECKBOX`（`0x3`）。坐标是对话框单位。

模板解析错时，`PropertySheetW` 会返回 `-1`，或者页面是空的。先核对 `cdit`、`0xFFFF` 类原子和 DWORD 对齐。

### 读取控件和阻止翻页

在对应页的对话框过程里处理消息。`hwnd` 是这一页，不是向导外框。

```csharp
const int WM_COMMAND = 0x0111;
IntPtr edit = GetDlgItem(hwnd, 101);
var buffer = new StringBuilder(512);
GetWindowTextW(edit, buffer, buffer.Capacity);
```

翻页通知码（都是无符号比较）：

| 通知 | 值 | 返回 0 | 返回 -1 |
| --- | --- | --- | --- |
| `PSN_WIZNEXT` | -207 | 进入下一页 | 留在当前页 |
| `PSN_WIZBACK` | -206 | 回到上一页 | 留在当前页 |
| `PSN_WIZFINISH` | -208 | 关闭向导 | 留在当前页 |
| `PSN_QUERYCANCEL` | -209 | 允许 Cancel | 拒绝关闭 |

要拒绝时，把 `DWLP_MSGRESULT` 设为 `-1`，对话框过程返回 1。现在的 `OnPageNotify` 对没处理的消息返回 0，所以 Next、Back、Finish、Cancel 都走系统默认行为。加校验时只拦截需要拦住的通知。

`PSN_KILLACTIVE`（-201）在离开页面前到达，适合把控件值写进共享状态。

### 页与页共享数据

让每一页的 `DialogProc` 闭包捕获同一个托管对象，例如 `WizardState`。这个对象要像现有委托一样，活到 `PropertySheetW` 返回。不要把托管对象地址塞进 `PROPSHEETPAGE.lParam`。

页面第一次创建时会收到 `WM_INITDIALOG`。那时的 `lParam` 指向系统复制过的 `PROPSHEETPAGE`，不是长期保存托管状态的地方。

### 增加一页

1. 再做一个模板、一个委托，并在 `finally` 里释放和 `KeepAlive`。
2. `nPages` 加 1，页数组按 `Marshal.SizeOf(typeof(PropSheetPage))` 增大。
3. 中间页在 `PSN_SETACTIVE` 里使用 `PSWIZB_BACK | PSWIZB_NEXT`，并启用返回箭头。
4. 只有最后一页使用 `PSWIZB_FINISH`。

默认从 `nStartPage = 0` 开始。页面顺序就是数组顺序。

### 改标题

- 标题栏文字：`Show` 里赋给 `pszCaption` 的字符串。
- 蓝色标题：该页的 `pszHeaderTitle`。
- 正文：模板里的控件，不是副标题字符串。

## 构建

用 Visual Studio 或 MSBuild 生成 `bin\Debug\WindowsExtendsClientPInvoke.exe` 后直接运行。向导是模态的，关掉它进程才退出。
