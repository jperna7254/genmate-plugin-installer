namespace GenMate.PluginInstaller.Core.SelfUpdate;

public enum SelfUpdateOutcome
{
    /// <summary>Nothing newer was published, so this build carries on.</summary>
    AlreadyCurrent,

    /// <summary>Something went wrong and was absorbed; this build carries on.</summary>
    NotApplied,

    /// <summary>A newer installer is in place and running; this process must now exit.</summary>
    RelaunchStarted
}
