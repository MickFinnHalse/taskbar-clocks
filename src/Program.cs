using System;
using System.Threading;
using System.Windows.Forms;

namespace TaskbarClocks
{
    internal static class Program
    {
        private const string ShowEvent = "Local\\IllumEdTaskbarClocksShow";

        [STAThread]
        private static void Main()
        {
            // Only ever one set of clocks on the taskbar. A second launch - double-clicking
            // the shortcut while they are already running - asks the running copy to come
            // forward and back on screen, then exits.
            bool isFirst;
            using (var single = new Mutex(true, "Local\\IllumEdTaskbarClocks", out isFirst))
            {
                if (!isFirst)
                {
                    try
                    {
                        using (var running = EventWaitHandle.OpenExisting(ShowEvent)) running.Set();
                    }
                    catch { /* the running copy is still starting up or shutting down */ }
                    return;
                }

                // Before any window exists, or tray coordinates come back virtualised
                // on a scaled display and the clocks land in the wrong place.
                Native.EnableDpiAwareness();

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                using (var show = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEvent))
                    Application.Run(new ClockWindow(Settings.Load(), show));
                GC.KeepAlive(single);
            }
        }
    }
}
