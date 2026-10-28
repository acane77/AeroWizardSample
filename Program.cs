using System;
using System.Windows.Forms;

namespace WindowsExtendsClientPInvoke
{
    internal static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            AeroWizard.Show();
        }
    }
}
