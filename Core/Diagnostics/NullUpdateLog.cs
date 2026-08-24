namespace GenMate.PluginInstaller.Core.Diagnostics;

public sealed class NullUpdateLog : IUpdateLog
{
    public static NullUpdateLog Instance { get; } = new();

    public void Write(string message)
    {
    }

    public void Write(string message, Exception exception)
    {
    }
}
