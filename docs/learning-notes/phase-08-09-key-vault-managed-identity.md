# Phase 8 & 9 — Key Vault and Managed Identity

## AZ-204 objectives covered
- Implement solutions that use Key Vault
- Implement managed identities for Azure resources

## 1. Business scenario

Done together on purpose, not out of laziness — they're really one change.
Before this phase, the SQL password and the storage account key both sat in
plain text under the Web App's Application settings, visible to anyone with
Portal access via "Show value". Key Vault gives the secrets a proper home;
Managed Identity is *how* the app gets into that vault without needing a
password of its own to do it. Doing Key Vault without Managed Identity would
just move the problem (now you need a password to *unlock* the vault
instead), so the two only make sense as one piece of work.

Blob Storage got the same treatment as a second example: instead of pulling
the account key (even from Key Vault), the app now connects to Blob Storage
directly using its own identity — no key anywhere at all, vault included.

## 2. Azure resources created

**Key Vault** `niroazurekeyvault`, Standard tier, same resource group/region
as everything else, **permission model: Azure RBAC** (not the older vault
access-policy model — RBAC is what lets role assignments work the same way
as every other resource in this project). Public endpoint, no VNet — same
tradeoff as the SQL server and storage account. Soft-delete stayed on
(can't be turned off), purge protection left **off** on purpose, since this
whole project will get torn down at the end of the free trial and purge
protection would just get in the way of that.

**System-assigned managed identity** enabled on the Web App (Identity blade,
one toggle). This is the actual "identity" both Key Vault and Blob Storage
now trust — not a client ID/secret pair, just the App Service's own Azure AD
identity.

## 3. Code

- **`Program.cs`** — right after `WebApplication.CreateBuilder(args)`, if a
  `KeyVault:Uri` setting is present, `builder.Configuration.AddAzureKeyVault(
  new Uri(keyVaultUri), new DefaultAzureCredential())` adds Key Vault as a
  configuration source. Locally, `KeyVault:Uri` is just never set, so this
  is skipped entirely and config falls back to User Secrets like every
  other phase — no local Key Vault needed for day-to-day development.
- **`Data/ProductImageStorage.cs`** — now branches on whether
  `Storage:AccountName` is configured. If it is, it connects with
  `new BlobContainerClient(containerUri, new DefaultAzureCredential())` —
  no key. If not, it falls back to the old connection-string constructor,
  so local dev (Azurite, or a real connection string in User Secrets) is
  unaffected.
- **Key Vault secret naming**: the SQL connection string lives in the vault
  as a secret named `ConnectionStrings--InventoryDb` — the double dash is
  Key Vault's own convention (secret names can't contain `:`), and the
  configuration provider maps `--` back to `:` automatically, so the app's
  existing `configuration.GetConnectionString("InventoryDb")` call needed
  **no changes at all**.
- **New packages**: `Azure.Identity`, `Azure.Extensions.AspNetCore.Configuration.Secrets`.

## 4. Azure Portal configuration

1. Web App → **Identity** → System-assigned → **On**.
2. Create the Key Vault (Standard, RBAC permission model, as above).
3. Key Vault → **Access control (IAM)** → Add role assignment → **Key Vault
   Secrets User** → Managed identity → the Web App. (Read-only, app-only —
   deliberately narrow.)
4. Key Vault → **Secrets** → new secret `ConnectionStrings--InventoryDb`,
   value = the SQL connection string. No expiration set (see bugs below for
   why creating this wasn't as simple as it sounds).
5. Storage account → **Access control (IAM)** → Add role assignment →
   **Storage Blob Data Contributor** → Managed identity → the Web App.
6. Web App → **Configuration → Application settings**: added
   `KeyVault__Uri` and `Storage__AccountName`; **deleted** the old
   `InventoryDb` connection string and `Storage__ConnectionString` /
   account-key setting entirely — the actual proof they're no longer
   needed.

## 5. Build errors hit along the way (not actually caused by this phase)

Two build errors showed up while publishing, but neither one is a Key
Vault/Managed Identity issue — both came from `Microsoft.Azure.Cosmos`
already sitting in the solution for the next phase (Cosmos DB), which just
happened to block the build for everyone, including this phase's testing:

- `Azure.Core` 1.55+ (required by `Azure.Storage.Blobs` 12.29.1) bundles its
  own `Azure.Identity.DefaultAzureCredential`, which collided at compile
  time with the real `Azure.Identity` package `Microsoft.Data.SqlClient`
  pulls in transitively (`CS0433: type exists in both`). **Fix:** kept
  `Azure.Identity` referenced (the real package still needs to exist at
  runtime for SqlClient) with
  `<PackageReference Include="Azure.Identity" ... ExcludeAssets="compile" />`,
  so only Azure.Core's bundled copy is visible to the C# compiler.
- `Microsoft.Azure.Cosmos` refuses to build at all without an explicit
  `Newtonsoft.Json` reference — a build-time check the package ships with,
  even though nothing in this project calls Newtonsoft directly. **Fix:**
  added `Newtonsoft.Json` directly to both the Web and Functions projects.

## 6. Real bugs found and fixed (actually Key Vault/Managed Identity)

1. **The app crashed with a plain 500.30 after the Key Vault switch, with
   no exception visible anywhere** — Log stream only ever showed the IIS
   access log, not the actual .NET exception, because the crash happened
   before the app's own logging pipeline was even built. Had to manually
   enable `stdoutLogEnabled="true"` in `web.config` via Kudu, restart, and
   read the real exception from `%home%\LogFiles\stdout_*.log` (not a
   custom `logs` folder — that setting already pointed at the App
   Service's own log folder).
2. **The real exception:** `The ConnectionString property has not been
   initialized` — `ConnectionStrings:InventoryDb` was resolving to an empty
   string, not null, so the app's own "missing config" guard never fired.
   Root cause: `appsettings.json` ships a placeholder empty string for that
   key, and the Key Vault secret wasn't actually reaching configuration to
   override it.
3. **Why the secret wasn't reaching config: Key Vault's RBAC permission
   model doesn't grant *anyone* data-plane access by default — not even the
   subscription Owner.** Being able to create the vault and assign roles on
   it is a control-plane permission; reading/writing secrets is a separate
   data-plane permission that has to be granted explicitly, to a person, the
   same way it was granted to the Web App's managed identity. Without it,
   the Secrets blade itself just says *"The operation is not allowed by
   RBAC"* — which meant the secret had likely never actually been created
   in the first place. **Fix:** granted my own account the **Key Vault
   Secrets Officer** role on the vault (separate from the app's read-only
   **Key Vault Secrets User** role), then created the secret for real.
4. **After fixing the config, reloading the site kept showing the same
   500.30** — IIS/ANCM caches a startup failure and won't retry starting
   the app on new requests until it's explicitly restarted. Had to hit
   **Restart** in the Portal, not just refresh the browser, before the fix
   actually took effect.

## 7. Testing

**Verified live, both halves separately:**
- **SQL via Key Vault** — deleted the `InventoryDb` connection string and
  `Storage__ConnectionString` entirely from App settings, confirmed the
  Dashboard and Products pages still load real data. Confirms the app is
  reading the connection string from Key Vault, not a leftover setting.
- **Blob Storage via Managed Identity** — loading the Products list alone
  doesn't prove this (it just renders existing image URLs from SQL). Had to
  specifically create/edit a product with a **new image upload**, which is
  the only code path that actually calls `ProductImageStorage` — confirmed
  the upload succeeded with no storage key configured anywhere, and the
  existing Blob-triggered thumbnail function (Phase 5) still fired
  correctly off that upload.

---
**Previous phase:** [Phase 5 — Azure Functions](phase-05-azure-functions.md) *(built out of order, before Phases 6-7)*
**Next phase:** [Phase 6 — Service Bus](phase-06-service-bus.md)
