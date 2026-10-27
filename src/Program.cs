using System;
using System.Threading;
using System.Windows.Forms;

namespace TaskbarClocks
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            // Only ever one set of clocks on the taskbar; a second launch just exits.
            bool isFirst;
            using (var single = new Mutex(true, "Local\\IllumEdTaskbarClocks", out isFirst))
            {
                if (!isFirst) return;

                // Before any window exists, or tray coordinates come back virtualised
                // on a scaled display and the clocks land in the wrong place.
                Native.EnableDpiAwareness();

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                Application.Run(new ClockWindow(Settings.Load()));
                GC.KeepAlive(single);
            }
        }
    }
}
