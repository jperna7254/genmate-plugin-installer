namespace GenMate.PluginInstaller.Core.Diagnostics;

/// <summary>
/// Where the launch-time update writes what it did. Nothing it records is ever shown to the user:
/// a background update that failed is a problem they cannot act on, so the app carries on at the
/// current version and the reason is left here for support.
/// </summary>
public interface IUpdateLog
{
    void Write(string message);

    void Write(string message, Exception exception);
}
