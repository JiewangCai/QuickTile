using System;
using System.Threading;
using System.Windows.Forms;

namespace QuickTile
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            NativeMethods.EnablePerMonitorDpiAwareness();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool createdNew;
            using (Mutex instanceMutex = new Mutex(true, "QuickTile_SingleInstance_Mutex", out createdNew))
            {
                if (!createdNew)
                {
                    return;
                }

                try
                {
                    AppConfig config = ConfigStore.Load();
                    using (TileManager manager = new TileManager(config))
                    {
                        Application.Run(manager);
                    }
                }
                finally
                {
                    try
                    {
                        instanceMutex.ReleaseMutex();
                    }
                    catch
                    {
                    }
                }
            }
        }
    }
}
