using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Tdt.App;

/// <summary>
/// Application entry point. It replaces the generated main so the single
/// instance check can run before any WinUI object is created.
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (!SingleInstanceGate.TryAcquire())
        {
            SingleInstanceGate.ShowAlreadyRunningMessage();
            return;
        }

        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(callbackParams =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
    }
}
