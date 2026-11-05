using System.Windows;
using MiniEnvi.UI;

namespace MiniEnvi;

public partial class App : Application
{
    private WorkspaceController? _controller;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _controller = new WorkspaceController();
        _controller.Show();
    }
}
