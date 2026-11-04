namespace GenMate.PluginInstaller.Core.SelfUpdate;

/// <summary>
/// Decides whether a downloaded installer may replace the running one. This is the seam the
/// signature check drops into; see <see cref="AcceptUnsignedInstallerVerifier"/> for why the
/// shipping implementation currently accepts everything.
/// </summary>
public interface IInstallerVerifier
{
    /// <summary>
    /// Returns false to discard the download and carry on with the current version. Must not
    /// throw: a verifier that throws is treated as a rejection, but the failure path is the
    /// caller's, not a substitute for answering.
    /// </summary>
    bool IsTrusted(string installerPath, out string? reason);
}
