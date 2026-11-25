using System.Windows;
using Velopack;

namespace GameLibraryLauncher
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Must execute first so Velopack can process install/update/uninstall events
            VelopackApp.Build().Run();

            base.OnStartup(e);
        }
    }
}