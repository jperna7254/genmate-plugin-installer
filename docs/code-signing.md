# Code signing the installer and the plugin

This is the procedure for getting GenMate's downloads signed so that Windows names a verified publisher
instead of warning about an unknown one. It covers both repos because both are signed with the same
identity: this installer (`GenMate.PluginInstaller.exe`) and the plugin DLLs in `genmate-plugin`.

Steps 1 to 4 are one-time setup that only the account owner can do, in the Azure portal and on GitHub.
Steps 5 to 7 are code changes, one ticket per repo. Step 8 is the release order, and it is the part
that goes wrong if it is skipped.

The YAML in steps 5 and 6 is a starting point for those tickets. Once a ticket lands, its workflow is
the source of truth: replace that step here with a pointer to the workflow, so the two cannot drift.

## What the warning is, and what signing does and does not fix

A user who downloads the installer from GitHub Releases in a browser gets a file marked as coming from
the internet. When they run it, Microsoft Defender SmartScreen checks two things: whether the file is
signed by a trusted publisher, and whether that publisher or file has a download reputation.

- **Unsigned (today):** "Windows protected your PC", publisher "Unknown publisher". The user has to
  click *More info* and then *Run anyway*. That is the warning this procedure removes.
- **Signed, before reputation builds:** the dialog names the publisher, but it can still appear for the
  first releases.
- **Signed, with reputation:** no SmartScreen dialog. The UAC prompt (the installer requires
  administrator rights, see `app.manifest`) shows the verified publisher in blue instead of the yellow
  "Unknown publisher" prompt.

**No certificate buys instant reputation any more.** EV certificates stopped skipping the SmartScreen
check in 2024. Reputation builds per signing identity as signed releases are downloaded and run without
problems. So **sign every release with the same identity from the first signed release on**. A signed
release that goes back to unsigned, or to a different identity, starts the reputation over.

The **plugin** is a different case. Its zip is downloaded by the installer, not a browser, so
SmartScreen never looks at it. AutoCAD loads it from `C:\ProgramData\Autodesk\ApplicationPlugins`,
which is a trusted location by default. The plugin workflow already signs its four GenMate DLLs, but
with a certificate that does not chain to a trusted root (see the comment above the disabled
`signtool verify` in the plugin's `build-release-prod.yml`), so the signature currently gives no
visible trust. Moving it to the same identity means a real publisher shows in the DLL's properties and
in AutoCAD's security checks, and it removes the `.pfx` secret from that repo.

## The choice: Azure Artifact Signing

Use **Azure Artifact Signing** (called *Trusted Signing* until January 2026).

- Microsoft holds the key in an HSM and issues a new short-lived certificate daily, so there is no
  `.pfx` to store, leak or renew. Commercial certificates are capped at 460 days from March 2026, so
  they need a renewal every year.
- It signs from a GitHub Actions Windows runner with no secret beyond Azure IDs, using OIDC.
- The installer's self-update already expects it. `Core/SelfUpdate/AcceptUnsignedInstallerVerifier.cs`
  lists the checks its replacement must make, and they are Artifact Signing's (identity EKU
  `1.3.6.1.4.1.311.97.*`, the Microsoft Identity Verification Root CA 2020 chain, a required timestamp).
  Buying a certificate from a commercial CA instead would mean rewriting that plan.
- Basic tier: 5,000 signatures a month and one certificate profile of each type, far more than GenMate
  uses. It is billed monthly, in full, from account creation. Check the current price in the Azure
  pricing calculator.
- It needs a **paid** Azure subscription (pay-as-you-go or better). Free, trial and sponsored
  subscriptions are refused.

Rejected: an OV certificate from a commercial CA (a hardware token or a cloud HSM, a yearly renewal,
and no better SmartScreen result). EV (costs more and no longer skips SmartScreen). Self-signed, which
is what the plugin uses today (trusted by nobody).

## 1. Decide whose name goes on the certificate

The publisher users see is the identity you validate. There are two kinds, and the choice is expensive
to reverse: a new identity starts a new reputation.

| | Organization | Individual developer |
|---|---|---|
| Publisher shown | The legal business name | Your legal name, city, state, country |
| Needs | A registered legal entity in a supported country, a website on its domain, a monitored email on that domain, a business ID | US or Canada residence, a government photo ID, an Azure billing account of type *Individual* whose legal name and address match the ID |
| Time | 1 to 20 business days, longer if they ask for documents | Usually minutes to hours (ID check through AU10TIX and Microsoft Authenticator) |

**Recommendation:** Organization, if GenMate is a registered business. A customer installing
admin-level software from a company expects the company's name on it. Use Individual only if there is
no entity yet, knowing that a later switch starts the reputation over.

## 2. Create the Artifact Signing account (Azure portal)

Needs a paid Azure subscription and a Microsoft Entra tenant.

1. **Subscriptions > your subscription > Resource providers**: register `Microsoft.CodeSigning`.
2. **Artifact Signing Accounts > Create**:
   - Resource group: e.g. `genmate-signing`.
   - Account name: e.g. `genmatesigning` (3 to 24 letters and digits, globally unique).
   - Region: **East US**. Its endpoint is `https://eus.codesigning.azure.net/`. Another region works,
     but CI must then use that region's endpoint.
   - Pricing tier: **Basic**.
3. On the account's **Access control (IAM)**, give yourself **Artifact Signing Identity Verifier**.
   Without it the *New identity* button is greyed out.

CLI equivalent for 1 and 2:

```bash
az provider register --namespace Microsoft.CodeSigning
az extension add --name artifact-signing
az group create --name genmate-signing --location eastus
az artifact-signing create -n genmatesigning -l eastus -g genmate-signing --sku Basic
```

## 3. Validate the identity, then create the certificate profile

Identity validation can only be done in the portal.

1. Account > **Identity validations** > choose *Organization* or *Individual* (step 1) > **New identity**
   > **Public**.
2. Fill in the form. Before you create it, check **Certificate subject preview**: that is the
   publisher text users will see, and fixing it later means a whole new validation.
3. When the status is **Action Required**, open the link and do the ID check (AU10TIX, then Microsoft
   Authenticator). An organization validation also needs this step from the person it names.
4. Wait for **Completed**. If they ask for documents, upload them in the portal. You get three tries,
   and documents must be under 12 months old.
5. Account > **Certificate profiles** > **Create** > **Public Trust** (not *Public Trust Test*, which
   nothing trusts):
   - Name: e.g. `genmate-public`.
   - Verified CN and O: the identity from step 4. Leave street address and postal code off.

## 4. Let GitHub Actions sign with no stored secret (OIDC)

One Entra app, with a federated credential for each workflow that signs. Run this with the Azure CLI
signed in to the tenant:

```bash
APP_ID=$(az ad app create --display-name genmate-github-signing --query appId -o tsv)
az ad sp create --id "$APP_ID"

for repo in genmate-plugin-installer genmate-plugin; do
  az ad app federated-credential create --id "$APP_ID" --parameters "{
    \"name\": \"$repo-main\",
    \"issuer\": \"https://token.actions.githubusercontent.com\",
    \"subject\": \"repo:jperna7254/$repo:ref:refs/heads/main\",
    \"audiences\": [\"api://AzureADTokenExchange\"]
  }"
done

ACCOUNT_ID=$(az artifact-signing show -g genmate-signing -n genmatesigning --query id -o tsv)
az role assignment create --assignee "$APP_ID" \
  --role "Artifact Signing Certificate Profile Signer" --scope "$ACCOUNT_ID"

TENANT_ID=$(az account show --query tenantId -o tsv)
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
for repo in genmate-plugin-installer genmate-plugin; do
  gh secret set AZURE_CLIENT_ID       --repo jperna7254/$repo --body "$APP_ID"
  gh secret set AZURE_TENANT_ID       --repo jperna7254/$repo --body "$TENANT_ID"
  gh secret set AZURE_SUBSCRIPTION_ID --repo jperna7254/$repo --body "$SUBSCRIPTION_ID"
done
```

The subject pins signing to pushes to `main`, which are the only runs that publish a release in either
repo. A QA build on `develop` cannot sign, which is intended: unsigned QA builds cost nothing, while
signed ones would spend the production identity's reputation on test builds. If a workflow later moves
into a GitHub *environment*, its OIDC subject becomes `repo:jperna7254/<repo>:environment:<name>` and
the federated credential must be changed to match, or login fails.

## 5. Installer: sign the published exe (this repo)

In `.github/workflows/build-release-prod.yml`:

1. Add `id-token: write` to the job's `permissions`, next to `contents: write`.
2. Between **Publish** and **Create GitHub Release**, add the steps below. Each one gets
   `if: steps.check.outputs.EXISTS == 'false'`, like the steps around it.

```yaml
    - name: Azure login
      if: steps.check.outputs.EXISTS == 'false'
      uses: azure/login@v3
      with:
        client-id: ${{ secrets.AZURE_CLIENT_ID }}
        tenant-id: ${{ secrets.AZURE_TENANT_ID }}
        subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

    - name: Sign installer
      if: steps.check.outputs.EXISTS == 'false'
      uses: azure/artifact-signing-action@v2
      with:
        endpoint: https://eus.codesigning.azure.net/
        signing-account-name: genmatesigning
        certificate-profile-name: genmate-public
        files: ${{ github.workspace }}/bin/Release/net10.0-windows/win-x64/publish/GenMate.PluginInstaller.exe
        file-digest: SHA256
        timestamp-rfc3161: http://timestamp.acs.microsoft.com
        timestamp-digest: SHA256
        description: GenMate Plugin Installer
        exclude-environment-credential: true
        exclude-workload-identity-credential: true
        exclude-managed-identity-credential: true
        exclude-shared-token-cache-credential: true
        exclude-visual-studio-credential: true
        exclude-visual-studio-code-credential: true
        exclude-azure-cli-credential: false
        exclude-azure-powershell-credential: true
        exclude-azure-developer-cli-credential: true
        exclude-interactive-browser-credential: true

    - name: Verify signature
      if: steps.check.outputs.EXISTS == 'false'
      shell: pwsh
      run: |
        $sig = Get-AuthenticodeSignature bin/Release/net10.0-windows/win-x64/publish/GenMate.PluginInstaller.exe
        if ($sig.Status -ne 'Valid') { throw "Installer signature is $($sig.Status): $($sig.StatusMessage)" }
        if (-not $sig.TimeStamperCertificate) { throw "Installer signature has no timestamp" }
        Write-Host "Signed by $($sig.SignerCertificate.Subject)"
```

Points to keep:

- **Pin both actions to a full commit SHA with a version comment**, the way this workflow already pins
  `softprops/action-gh-release`. The tags above are only there to make the example readable.
- **`files` must be absolute paths.** That is why it starts with `${{ github.workspace }}`.

- **Signing comes after `dotnet publish` and signs the single-file exe as a whole.** Nothing may touch
  the exe after signing, or the signature breaks.
- **The verify step makes a failed signing fail the release.** Once step 7 ships, an unsigned release
  would silently stop self-update for every customer, so a red build is the right result.
- **The timestamp is required.** Each certificate lives about 72 hours, and without a timestamp the
  signature is invalid within three days.
- Consider also setting `<Company>` in `GenMate.PluginInstaller.csproj`. It is not part of the
  signature, but it is what the exe's Properties > Details tab shows as the company.

## 6. Plugin: move the existing DLL signing to the same identity (`genmate-plugin`)

File this as its own ticket in `genmate-plugin`. In that repo's `.github/workflows/build-release-prod.yml`,
in both the AutoCAD and the BricsCAD jobs:

1. Add `id-token: write` to the job permissions.
2. Replace the steps that decode `SIGNING_CERTIFICATE_BASE64` into a `.pfx`, loop `signtool sign` over
   the DLLs and delete the certificate with an `azure/login@v3` step and an
   `azure/artifact-signing-action@v2` step, both pinned to a SHA like the rest of that workflow. Use the
   same `endpoint`, `signing-account-name`, `certificate-profile-name`, timestamp and `exclude-*` inputs
   as in step 5, with `files:` listing the same four DLLs the job signs today (Abstractions, Core, UI,
   and Acad24 or Brics24) and `description: GenMate Plugin`. **The paths must be absolute**
   (`${{ github.workspace }}/src/...`). The workflow's current DLL paths are relative and will fail if
   copied as they are.
   **Give the new steps the same `if:` as the signing steps they replace in that job.** The two jobs
   use different conditions (the BricsCAD job also checks that the SDK is present), so do not copy
   step 5's condition.
3. Turn the disabled `signtool verify /pa` check back on. Its comment says to do this once the
   certificate is CA-issued, and Artifact Signing's is.
4. Keep signing the last thing that touches the binaries. The plugin's `AGENTS.md` already requires
   this, and the `assert-brics-adapter` check must still run before signing.
5. Do not sign the third-party DLLs. They are not GenMate's, and the Microsoft ones are already signed.
6. After the first release signed this way, delete the `SIGNING_CERTIFICATE_BASE64` and
   `SIGNING_CERTIFICATE_PASSWORD` secrets from the repo.

## 7. Installer: turn on signature checking for self-update (this repo, a later release)

Replace `AcceptUnsignedInstallerVerifier` with the verifier its own comment describes. The one value
that comment cannot give is the subscriber-specific identity EKU. Read it from the first signed release
(step 8), on Windows:

```powershell
$sig = Get-AuthenticodeSignature .\GenMate.PluginInstaller.exe
$sig.SignerCertificate.EnhancedKeyUsageList
```

The list has **two** entries starting `1.3.6.1.4.1.311.97.`. Pin the long one, which is unique to GenMate's identity
(Microsoft's example of the shape: `1.3.6.1.4.1.311.97.990309390.766961637.194916062.941502583`).
**Never pin `1.3.6.1.4.1.311.97.1.0`.** Every Artifact Signing Public Trust certificate carries it, so
pinning it would accept anyone's signed file, which is exactly what the verifier comment forbids.

**Open question the step 7 ticket must settle before it ships: does the pinned EKU survive renewing
the identity validation?** Microsoft's docs disagree. The certificate management page says the value
is "unique to the identity validation resource". The renewal page says "EKU values are unique at the
certificate profile level", and its renewal steps delete the certificate profile and create it again.
If renewal changes the EKU, every customer's installer rejects every update signed after the renewal,
and a fix cannot reach them, because the fix would be signed with the new EKU too. The verifier has to
survive this before it ships. Possible answers: accept a list of EKUs and release the new one while
the old profile still signs (a second Public Trust profile needs the Premium tier), or confirm with
Azure support that renewal keeps the EKU. That is a design decision for that ticket, not this
procedure.

## 8. Release order

1. **Release A:** step 5 only. Bump the installer `Version`, merge to `main`, and confirm the release
   job signed it (check below). A plugin release with step 6 can go out any time after step 4.
2. **Release B:** step 7, the strict verifier, with the EKU read from release A.

**Never ship steps 5 and 7 in the same release, and never ship 7 first.** The verifier checks the
*downloaded next* installer. Customers on a pre-7 build accept anything, so release B reaches them.
From then on every release must be signed, or those customers stay on B silently. That is correct
behaviour, but it means a lapsed Azure subscription or a broken signing step stops updates for
everyone until it is fixed.

## Checking a release

On a Windows machine, download the exe from the GitHub release **with a browser** (not `gh` or `curl`,
which do not mark the file as coming from the internet), then:

- **Properties > Digital Signatures**: one SHA256 signature, the expected publisher, and a timestamp.
  *Details* says "This digital signature is OK."
- `Get-AuthenticodeSignature .\GenMate.PluginInstaller.exe | Format-List`: `Status : Valid`.
- `signtool verify /pa /v .\GenMate.PluginInstaller.exe` ends with "Successfully verified".
- Run it. The UAC prompt shows the verified publisher. The SmartScreen dialog may still appear for the
  first signed releases (see the top section), but it names the publisher instead of "Unknown
  publisher".

For a plugin release, run the same `Get-AuthenticodeSignature` on `GenMate.Plugin.Acad24.dll` from the
release zip.

## Keeping it working

- **The identity validation expires and must be renewed.** The expiry date is on the account's
  Identity validations page, and Azure emails reminders from 60 days before it. Renewal is a full
  re-review that can take 1 to 20 business days and may ask for documents again, so **start it as soon
  as the 60-day window opens**. If it lapses, certificate renewal stops, and signing stops within about
  three days. Renewal finishes by deleting the certificate profile and creating it again with the same
  name, so the workflows need no change. Read the step 7 open question before doing this once the
  strict verifier has shipped.
- **Signing certificates rotate daily inside Azure**, and there is no certificate file to renew. The
  other things that break signing: the Azure subscription lapsing or losing its payment method, the
  Entra app or its role assignment being deleted, or a workflow's OIDC subject changing (renaming a
  repo or branch, or moving a job into an environment).
- **Identity validation details cannot be edited.** A change of legal name or address means a new
  validation, which means a new identity EKU and the same problem as the step 7 open question.
- If a signing step fails with a 403, the Entra app is missing the **Artifact Signing Certificate
  Profile Signer** role on the account. If `azure/login` fails, the federated credential subject does
  not match the run.
