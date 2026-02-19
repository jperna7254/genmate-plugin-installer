using System.Diagnostics;

namespace GenMate.PluginInstaller.Services;

public class AutoCADDetectionService : IAutoCADDetectionService
{
    public bool IsAutoCADRunning()
    {
        return Process.GetProcessesByName("acad").Length > 0;
    }
}
