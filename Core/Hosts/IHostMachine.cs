namespace GenMate.PluginInstaller.Core.Hosts;

/// <summary>What the installer asks of Windows beyond the file system: the registry and the process list.</summary>
public interface IHostMachine
{
    bool IsApplicationInstalled(PluginHost host);

    bool IsApplicationRunning(PluginHost host);

    void RegisterDemandLoad(DemandLoadRegistration registration, string loaderPath);

    /// <summary>Removes the entry; does nothing when it is not there.</summary>
    void UnregisterDemandLoad(DemandLoadRegistration registration);
}
