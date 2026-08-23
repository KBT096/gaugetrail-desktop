using System.Windows;

namespace GaugeTrail.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                $"GaugeTrail 遇到未处理错误：\n\n{args.Exception.Message}",
                "GaugeTrail Desktop",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        base.OnStartup(e);
    }
}
