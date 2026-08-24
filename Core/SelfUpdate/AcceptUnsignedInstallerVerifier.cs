namespace GenMate.PluginInstaller.Core.SelfUpdate;

/// <summary>
/// Accepts every downloaded installer, unconditionally.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is a deliberate ruling by the captain, not an oversight, and not a stub someone forgot.</b>
/// There is no code-signing certificate for this product and no date for one; obtaining an
/// organization certificate is an external process measured in weeks. Shipping self-update without
/// verification was chosen knowing the risk, because <b>an installer that can update itself is how
/// the verification eventually reaches the customers who already have a copy.</b> Without it, every
/// future fix - this one included - requires every customer to manually re-download.
/// </para>
/// <para>
/// What the replacement must do once a certificate exists, in place of returning true:
/// </para>
/// <list type="number">
///   <item>read the downloaded file's Authenticode signature and reject if there is none;</item>
///   <item>require the pinned durable identity EKU <c>1.3.6.1.4.1.311.97.&lt;subscriber octets&gt;</c>,
///         held as a constant in this binary. This is the gate: it is what makes the check mean
///         <i>GenMate</i> rather than <i>somebody who bought a certificate</i>;</item>
///   <item>require the Code Signing EKU <c>1.3.6.1.5.5.7.3.3</c>;</item>
///   <item>require the chain to terminate at Microsoft Identity Verification Root CA 2020;</item>
///   <item>require an RFC-3161 timestamp countersignature and validate the signature as of that
///         timestamp - Artifact Signing certificates are renewed daily and live 72 hours, so an
///         untimestamped signature is dead within three days;</item>
///   <item>never pin the thumbprint, the public key or the subject name: all three rotate or change.</item>
/// </list>
/// <para>
/// Failing closed is correct here and costs the user nothing, because a rejected self-update is
/// silent: the app carries on at the current version and the reason goes to the log.
/// </para>
/// <para>
/// Replacing this class is one implementation of <see cref="IInstallerVerifier"/> and one line at
/// the construction site. It is not a redesign, and it must not become one.
/// </para>
/// </remarks>
public sealed class AcceptUnsignedInstallerVerifier : IInstallerVerifier
{
    public bool IsTrusted(string installerPath, out string? reason)
    {
        reason = null;
        return true;
    }
}
