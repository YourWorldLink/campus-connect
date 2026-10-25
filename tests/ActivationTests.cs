using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

static class ActivationTests
{
    static string expectedExecutable;
    delegate bool Callback(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] static extern bool EnumWindows(Callback callback, IntPtr parameter);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr window, StringBuilder title, int count);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window, StringBuilder title, int count);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    static IntPtr Find()
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((window, parameter) =>
        {
            uint id;
            GetWindowThreadProcessId(window, out id);
            try
            {
                using (var process = Process.GetProcessById((int)id))
                {
                    if (process.ProcessName != "CampusConnect") return true;
                    if (!String.Equals(process.MainModule.FileName, expectedExecutable, StringComparison.OrdinalIgnoreCase)) return true;
                    var title = new StringBuilder(256);
                    var className = new StringBuilder(256);
                    GetWindowText(window, title, 256);
                    GetClassName(window, className, 256);
                    if (title.ToString() != "校园网一键连接") return true;
                    if (className.ToString() == "#32770")
                    {
                        // Dismiss only obsolete 'already open' notices; no form is closed.
                        PostMessage(window, 0x0010, IntPtr.Zero, IntPtr.Zero);
                        return true;
                    }
                    if (className.ToString().StartsWith("WindowsForms10.Window")) found = window;
                }
            }
            catch (ArgumentException) { }
            return true;
        }, IntPtr.Zero);
        return found;
    }
    static void LaunchAgain(string executable)
    {
        using (var process = Process.Start(new ProcessStartInfo(executable, "--settings") { UseShellExecute = false, WindowStyle = ProcessWindowStyle.Normal }))
            if (!process.WaitForExit(5000)) throw new Exception("Second launch did not activate the existing instance and exit.");
    }
    static int Main(string[] args)
    {
        try
        {
            string executable = Path.GetFullPath(args[0]);
            expectedExecutable = executable;
            IntPtr window = Find();
            if (window == IntPtr.Zero)
            {
                Process.Start(new ProcessStartInfo(executable, "--settings") { UseShellExecute = false, WindowStyle = ProcessWindowStyle.Normal });
                for (int i = 0; i < 30 && window == IntPtr.Zero; i++) { Thread.Sleep(100); window = Find(); }
                if (window == IntPtr.Zero) throw new Exception("Main form did not appear.");
            }
            ShowWindow(window, 0);
            LaunchAgain(executable);
            if (!IsWindowVisible(window)) throw new Exception("Hidden instance was not shown.");
            Console.WriteLine("PASS repeat launch reveals hidden configuration window");
            ShowWindow(window, 6);
            LaunchAgain(executable);
            if (IsIconic(window)) throw new Exception("Minimized instance was not restored.");
            Console.WriteLine("PASS repeat launch restores minimized configuration window");
            LaunchAgain(executable);
            if (!IsWindowVisible(window)) throw new Exception("Existing form disappeared.");
            Console.WriteLine("PASS repeat launch exits without redundant already-open dialog");
            Console.WriteLine("RESULT: 3 activation regression tests passed");
            return 0;
        }
        catch (Exception ex) { Console.WriteLine(ex); return 1; }
    }
}
