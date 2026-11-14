using System.Runtime.InteropServices;

namespace Tdt.App;

/// <summary>
/// Keeps a single instance per session. A second launch explains the situation
/// and exits before any window is created.
/// </summary>
internal static class SingleInstanceGate
{
    private const string MutexName = @"Local\Tdt.SingleInstance";
    private const uint InformationIcon = 0x00000040;

    private static Mutex? _mutex;

    public static bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (createdNew)
        {
            return true;
        }

        _mutex.Dispose();
        _mutex = null;
        return false;
    }

    public static void ShowAlreadyRunningMessage()
    {
        var result = MessageBox(
            IntPtr.Zero,
            "Torrent Diff Tool is already running. Close the existing window before starting a new instance.",
            "Torrent Diff Tool",
            InformationIcon);

        System.Diagnostics.Debug.WriteLineIf(result == 0, "The already-running message could not be shown.");
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
    private static extern int MessageBox(IntPtr windowHandle, string text, string caption, uint type);
}
