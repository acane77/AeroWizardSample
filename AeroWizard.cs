using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace WindowsExtendsClientPInvoke
{
    internal static class AeroWizard
    {
        const uint PSH_PROPSHEETPAGE = 0x00000008;
        const uint PSH_WIZARD = 0x00000020;
        const uint PSH_AEROWIZARD = 0x00004000;

        const uint PSP_DLGINDIRECT = 0x00000001;
        const uint PSP_USEHEADERTITLE = 0x00001000;
        const uint PSP_USEHEADERSUBTITLE = 0x00002000;

        const int PSWIZB_BACK = 0x00000001;
        const int PSWIZB_NEXT = 0x00000002;
        const int PSWIZB_FINISH = 0x00000004;

        const uint WM_NOTIFY = 0x004E;
        const uint WM_USER = 0x0400;
        const uint PSM_SETWIZBUTTONS = WM_USER + 112;
        const uint PSM_ENABLEWIZBUTTONS = WM_USER + 139;
        const uint PSN_SETACTIVE = unchecked((uint)-200);
        const int DWLP_MSGRESULT = 0;

        const int ICC_WIN95_CLASSES = 0x000000FF;

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        delegate IntPtr DialogProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        public static void Show()
        {
            EnsureNativeLayout();
            InitCommonControls();

            DialogProc page1Proc = (hwnd, msg, wParam, lParam) => OnPageNotify(hwnd, msg, lParam, firstPage: true);
            DialogProc page2Proc = (hwnd, msg, wParam, lParam) => OnPageNotify(hwnd, msg, lParam, firstPage: false);

            IntPtr template1 = IntPtr.Zero;
            IntPtr template2 = IntPtr.Zero;
            IntPtr caption = IntPtr.Zero;
            IntPtr title = IntPtr.Zero;
            IntPtr subtitle1 = IntPtr.Zero;
            IntPtr subtitle2 = IntPtr.Zero;
            IntPtr pages = IntPtr.Zero;

            try
            {
                template1 = CreatePageTemplate("page 1");
                template2 = CreatePageTemplate("page 2");
                caption = Marshal.StringToHGlobalUni("Hello World");
                title = Marshal.StringToHGlobalUni("Hello World");
                subtitle1 = Marshal.StringToHGlobalUni("page 1");
                subtitle2 = Marshal.StringToHGlobalUni("page 2");

                int pageSize = Marshal.SizeOf(typeof(PropSheetPage));
                pages = Marshal.AllocHGlobal(pageSize * 2);
                WritePage(pages, 0, pageSize, template1, page1Proc, title, subtitle1);
                WritePage(pages, 1, pageSize, template2, page2Proc, title, subtitle2);

                var header = new PropSheetHeader
                {
                    dwSize = (uint)Marshal.SizeOf(typeof(PropSheetHeader)),
                    dwFlags = PSH_WIZARD | PSH_AEROWIZARD | PSH_PROPSHEETPAGE,
                    pszCaption = caption,
                    nPages = 2,
                    ppsp = pages
                };

                IntPtr headerPtr = Marshal.AllocHGlobal(Marshal.SizeOf(header));
                try
                {
                    Marshal.StructureToPtr(header, headerPtr, false);
                    IntPtr result = PropertySheetW(headerPtr);
                    if (result == new IntPtr(-1))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(headerPtr);
                }
            }
            finally
            {
                Free(pages);
                Free(subtitle2);
                Free(subtitle1);
                Free(title);
                Free(caption);
                Free(template2);
                Free(template1);
                GC.KeepAlive(page1Proc);
                GC.KeepAlive(page2Proc);
            }
        }

        static void WritePage(IntPtr pages, int index, int pageSize, IntPtr template, DialogProc proc, IntPtr headerTitle, IntPtr headerSubtitle)
        {
            var page = new PropSheetPage
            {
                dwSize = (uint)pageSize,
                dwFlags = PSP_DLGINDIRECT | PSP_USEHEADERTITLE | PSP_USEHEADERSUBTITLE,
                pResource = template,
                pfnDlgProc = Marshal.GetFunctionPointerForDelegate(proc),
                pszHeaderTitle = headerTitle,
                pszHeaderSubTitle = headerSubtitle
            };
            Marshal.StructureToPtr(page, IntPtr.Add(pages, index * pageSize), false);
        }

        static IntPtr OnPageNotify(IntPtr hwnd, uint msg, IntPtr lParam, bool firstPage)
        {
            if (msg != WM_NOTIFY || unchecked((uint)Marshal.ReadInt32(lParam, IntPtr.Size * 2)) != PSN_SETACTIVE)
            {
                return IntPtr.Zero;
            }

            IntPtr sheet = GetParent(hwnd);
            if (firstPage)
            {
                PostMessageW(sheet, PSM_SETWIZBUTTONS, IntPtr.Zero, new IntPtr(PSWIZB_NEXT));
                PostMessageW(sheet, PSM_ENABLEWIZBUTTONS, IntPtr.Zero, new IntPtr(PSWIZB_BACK));
            }
            else
            {
                PostMessageW(sheet, PSM_SETWIZBUTTONS, IntPtr.Zero, new IntPtr(PSWIZB_BACK | PSWIZB_FINISH));
                PostMessageW(sheet, PSM_ENABLEWIZBUTTONS, new IntPtr(PSWIZB_BACK), new IntPtr(PSWIZB_BACK));
            }

            SetDialogResult(hwnd, IntPtr.Zero);
            return new IntPtr(1);
        }

        static void SetDialogResult(IntPtr hwnd, IntPtr result)
        {
            if (IntPtr.Size == 8)
            {
                SetWindowLongPtr64(hwnd, DWLP_MSGRESULT, result);
            }
            else
            {
                SetWindowLong32(hwnd, DWLP_MSGRESULT, result.ToInt32());
            }
        }

        static IntPtr CreatePageTemplate(string body)
        {
            const int DS_SETFONT = 0x00000040;
            const int DS_FIXEDSYS = 0x00000008;
            const int WS_CHILD = 0x40000000;
            const int WS_VISIBLE = 0x10000000;
            const int WS_DISABLED = 0x08000000;
            const int WS_CAPTION = 0x00C00000;
            const ushort staticClass = 0x0082;
            const string fontName = "MS Shell Dlg";

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(DS_SETFONT | DS_FIXEDSYS | WS_CHILD | WS_DISABLED | WS_CAPTION);
                writer.Write(0);
                writer.Write((ushort)1);
                writer.Write((short)0);
                writer.Write((short)0);
                writer.Write((short)317);
                writer.Write((short)143);
                writer.Write((ushort)0);
                writer.Write((ushort)0);
                writer.Write((ushort)0);
                writer.Write((ushort)8);
                writer.Write(Encoding.Unicode.GetBytes(fontName));
                writer.Write((ushort)0);
                AlignDword(writer);

                writer.Write(WS_CHILD | WS_VISIBLE);
                writer.Write(0);
                writer.Write((short)0);
                writer.Write((short)0);
                writer.Write((short)300);
                writer.Write((short)24);
                writer.Write((ushort)100);
                writer.Write((ushort)0xFFFF);
                writer.Write(staticClass);
                writer.Write(Encoding.Unicode.GetBytes(body));
                writer.Write((ushort)0);
                writer.Write((ushort)0);
                AlignDword(writer);

                byte[] bytes = stream.ToArray();
                IntPtr template = Marshal.AllocHGlobal(bytes.Length);
                Marshal.Copy(bytes, 0, template, bytes.Length);
                return template;
            }
        }

        static void AlignDword(BinaryWriter writer)
        {
            while (writer.BaseStream.Position % 4 != 0)
            {
                writer.Write((byte)0);
            }
        }

        static void InitCommonControls()
        {
            var init = new CommonControlsInit
            {
                dwSize = Marshal.SizeOf(typeof(CommonControlsInit)),
                dwICC = ICC_WIN95_CLASSES
            };
            InitCommonControlsEx(ref init);
        }

        static void EnsureNativeLayout()
        {
            int headerSize = Marshal.SizeOf(typeof(PropSheetHeader));
            int pageSize = Marshal.SizeOf(typeof(PropSheetPage));
            int expectedHeader = IntPtr.Size == 8 ? 96 : 52;
            int expectedPage = IntPtr.Size == 8 ? 104 : 56;
            if (headerSize != expectedHeader || pageSize != expectedPage)
            {
                throw new InvalidOperationException(
                    "PROPSHEETHEADER/PROPSHEETPAGE layout does not match prsht.h. header="
                    + headerSize + " page=" + pageSize);
            }
        }

        static void Free(IntPtr pointer)
        {
            if (pointer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pointer);
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        struct PropSheetHeader
        {
            public uint dwSize;
            public uint dwFlags;
            public IntPtr hwndParent;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr pszCaption;
            public uint nPages;
            public IntPtr nStartPage;
            public IntPtr ppsp;
            public IntPtr pfnCallback;
            public IntPtr hbmWatermark;
            public IntPtr hplWatermark;
            public IntPtr hbmHeader;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        struct PropSheetPage
        {
            public uint dwSize;
            public uint dwFlags;
            public IntPtr hInstance;
            public IntPtr pResource;
            public IntPtr hIcon;
            public IntPtr pszTitle;
            public IntPtr pfnDlgProc;
            public IntPtr lParam;
            public IntPtr pfnCallback;
            public IntPtr pcRefParent;
            public IntPtr pszHeaderTitle;
            public IntPtr pszHeaderSubTitle;
            public IntPtr hActCtx;
            public IntPtr hbmHeader;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct CommonControlsInit
        {
            public int dwSize;
            public int dwICC;
        }

        [DllImport("comctl32.dll", EntryPoint = "InitCommonControlsEx", SetLastError = true)]
        static extern bool InitCommonControlsEx(ref CommonControlsInit init);

        [DllImport("comctl32.dll", EntryPoint = "PropertySheetW", ExactSpelling = true, SetLastError = true)]
        static extern IntPtr PropertySheetW(IntPtr header);

        [DllImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
        static extern bool PostMessageW(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr GetParent(IntPtr hwnd);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        static extern IntPtr SetWindowLongPtr64(IntPtr hwnd, int index, IntPtr newValue);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
        static extern int SetWindowLong32(IntPtr hwnd, int index, int newValue);
    }
}
