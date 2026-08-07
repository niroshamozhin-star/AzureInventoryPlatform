# Phase 5 — Azure Functions (HTTP, Timer, and Blob triggers)

## AZ-204 objectives covered
- Implement Azure Functions (HTTP trigger, Timer trigger, Blob trigger — isolated worker model)
- Implement authentication and authorization using tokens (in the context this actually fits)

## 1. Business scenario

Built out of order on purpose — Phase 4 (Blob Storage) was still pending
when this started; the HTTP-trigger half was pulled forward first because
it's the natural home for something learned earlier and then retired:
**JWT bearer authentication**.

Phase 3 built a full JWT flow, then replaced it with cookie authentication,
because the actual requirement ("every page in a browser app needs one
login") was a cookie-auth problem, not a JWT one. That didn't waste the JWT
work — it identified exactly what JWT *is* for: a caller that's another
*program*, not a browser with a session. An Azure Function callable via HTTP,
with nothing behind it but a URL, is precisely that caller. So this phase is
JWT's real home in this project.

Once Phase 4 (Blob Storage) landed, this phase grew to cover all three
trigger types the AZ-204 exam actually tests: **HTTP**, **Timer**, and
**Blob** — three small, independent functions in the same project, kept
deliberately simple rather than sharing an abstraction between them.

## 2. Azure resource / project created

A second, independent project in the same solution:
`src/AzureInventoryPlatform.Functions` — Azure Functions, **isolated worker
model**, .NET 8, scaffolded via
`dotnet new func -n AzureInventoryPlatform.Functions -F net8.0` (after
installing `Microsoft.Azure.Functions.Worker.ProjectTemplates`), then added
to the solution with `dotnet sln add`.

This project is **not** a Web API project consumed by the MVC app - it's a
standalone service with its own login, its own config, and its own
connection to the same Azure SQL database. Nothing in
`AzureInventoryPlatform.Web` references it, and vice versa.

## 3. Code

- **`TokenFunction.cs`** — `[Function("GetToken")]`, HTTP trigger,
  `POST /api/auth/token`, `AuthorizationLevel.Anonymous` (the Functions
  runtime's own function-key gate is turned off, since this endpoint does
  its own credential check instead). Reads a `TokenRequest` (Username,
  Password) from the JSON body, checks it against `Auth:Username`/
  `Auth:Password` in configuration, and - on success - builds and signs a
  JWT (`JwtSecurityToken`, HMAC-SHA256, 1-hour expiry) using `Jwt:Key`. This
  is the same mechanism Phase 3's `AuthController` used, moved here since
  this is where the corresponding validator (below) now lives too.
- **`LowStockFunction.cs`** — `[Function("GetLowStockReport")]`, HTTP
  trigger, `GET /api/reports/low-stock`, also `AuthorizationLevel.Anonymous`
  at the Functions-runtime level. The real authorization check
  (`TryValidateToken`) is written out explicitly in code: read the
  `Authorization` header, confirm it starts with `Bearer `, and call
  `JwtSecurityTokenHandler().ValidateToken(...)` with the same
  `Jwt:Key`/`Issuer`/`Audience` used to sign it. **Deliberately manual**,
  rather than wiring up `[Authorize]` + ASP.NET Core authentication
  middleware (which the isolated-worker + `Http.AspNetCore` package *can*
  support) - the explicit version is one method you can read top to bottom
  with no framework wiring to trace through separately, which matters more
  for a learning project than saving a few lines.
- If the token is valid, the function opens a `SqlConnection` directly
  (same `Microsoft.Data.SqlClient` pattern as `Data/*.cs` in the Web
  project) and runs one parameterized-free `SELECT` joining
  `Inventory`/`Products`/`Warehouses`, returning rows at or below their
  reorder level as JSON.
- **`LowStockTimerFunction.cs`** — `[Function("LowStockTimerCheck")]`,
  `[TimerTrigger("0 */5 * * * *")]` (every 5 minutes). Runs the exact same
  low-stock query as `LowStockFunction`, but nothing calls it and nothing
  waits on a response - it just logs the count. This is the "no caller at
  all" trigger type: the Functions runtime wakes it up itself on a
  schedule, full stop.
- **`ProductImageThumbnailFunction.cs`** — `[Function("GenerateProductImageThumbnail")]`,
  `[BlobTrigger("product-images/{name}", Connection = "BlobStorageConnection")]`.
  Fires automatically whenever a file lands in the `product-images`
  container (created in Phase 4) - from the Web app's Create/Edit form, the
  Import feature, or a file dropped in directly via the Portal; the trigger
  has zero awareness of which, or of the `Products` table at all. Resizes
  to a 150px-max thumbnail using `System.Drawing.Common` (`SixLabors.ImageSharp`
  was tried first and rejected - v4 requires a paid commercial license just
  to build) and writes the result into a second container,
  `product-images-thumbnails`, under the same blob name. **Honest gap:**
  nothing in the UI displays this thumbnail yet - it proves the trigger
  mechanism end-to-end, but it's a backend artifact only so far.
- **Self-contained on purpose:** this project does **not** reference
  `AzureInventoryPlatform.Web` to reuse its `Models`/`Data` classes. Pulling
  in the whole MVC project just for three small classes would drag in
  unrelated dependencies (ClosedXML, cookie auth, MVC views) for no reason -
  the query here is ~15 lines of plain ADO.NET, simple enough that
  duplicating it is the right tradeoff over an awkward cross-project
  reference, for a project this size.

## 4. Where the secrets live

Same idea as every previous phase, different mechanism for this project
type: Azure Functions doesn't use `dotnet user-secrets` - it uses
**`local.settings.json`**, gitignored by the template out of the box, never
committed. Holds a fresh, separate `Jwt:Key` (a new key, not reused from the
retired Phase 3 one), `Jwt:Issuer`/`Audience`, `Auth:Username`/`Password`,
and `SqlConnectionString` (the same Azure SQL connection string used
elsewhere). In Azure, these become Function App **Application settings** -
same double-underscore-for-colon rule as App Service
(`Jwt__Key`, `Auth__Username`, etc.).

## 5. Real bugs found and fixed

Three genuine "found it by running it" bugs came out of this phase:

1. **Missing `System.Memory.Data.dll` - host crashed on startup,
   intermittently.** The Blob Storage extension package builds its own
   nested project (`WorkerExtensions.csproj`, producing
   `bin/.../.azurefunctions/`) that resolves its NuGet graph
   **independently** of the main `.csproj` - pinning a version in the main
   project has zero effect on it. Its own `function.deps.json` correctly
   declared `Azure.Storage.Queues 12.24.0 -> System.Memory.Data 8.0.1` as
   required, but the actual `.dll` was never copied into that folder - a
   real gap in the build tooling, not a version conflict introduced here.
   It only crashed *intermittently* because the Blob trigger's internal
   poison-blob tracking only loads `Azure.Storage.Queues` under certain
   code paths. **Fix:** an MSBuild `<Target AfterTargets="Build;Publish">`
   in the `.csproj` that copies the correct file from the local NuGet cache
   into `.azurefunctions/` after every build.
2. **`TokenFunction` silently rejecting correct credentials.**
   `JsonSerializer.DeserializeAsync<TokenRequest>(req.Body)` with no
   options is **case-sensitive** by default - a lowercase
   `{"username": ..., "password": ...}` payload against a PascalCase
   `TokenRequest(string Username, string Password)` record silently
   deserialized both fields to `null`, which correctly failed the
   credential check and returned a legitimate-looking 401 that had nothing
   to do with the actual password. **Fix:**
   `new JsonSerializerOptions { PropertyNameCaseInsensitive = true }`.
3. **Visual Studio's F5 failed with `Method not found:
   ParameterBindingData..ctor`; the identical code run from a terminal
   worked.** Visual Studio keeps its own separate cache of Azure Functions
   Core Tools (`%LOCALAPPDATA%\AzureFunctionsTools\Releases\`), independent
   of whatever `func` version is on PATH globally - F5 was using a cached
   build whose bundled `Microsoft.Azure.WebJobs` assembly predated a
   constructor the extension package expected. **Fix:** updated Core Tools
   via Visual Studio's Tools -> Options -> Azure Functions.

## 6. Config issues hit while testing this live on Azure

- Web app crashed with a `500.30` error after sitting idle for a while.
  Republishing from Visual Studio fixed it.
- Web app's `Storage:ConnectionString` setting was blank, not missing, so
  the app's own check didn't catch it. Added `Storage__ConnectionString`
  and `Storage__ContainerName` as app settings.
- Blob trigger wasn't firing because the function app's `AzureWebJobsStorage`
  setting didn't exist at all. Host showed unhealthy until it was added.
- Mixed up `AzureWebJobsStorageType` (where function keys are stored) with
  `AzureWebJobsStorage` (the actual storage connection) - similar names,
  different settings.
- Function list in the Portal was empty until the app was restarted after
  adding `AzureWebJobsStorage`.
- Portal's Test/Run panel won't let you set an `Authorization` header, so
  switched to the `.http` file in Visual Studio for testing the live app.
- Pasted a JWT token into the `.http` file and it got split across a few
  lines, which broke it. Had to put it back on one line.
- The `/admin/functions/{name}` endpoint needs the master key on the live
  app - locally it just works without one.
- JWT tokens expire in an hour, so had to grab a fresh one before retesting.

## 7. Testing

**Locally**, two things need to be running side by side:
1. **Azurite** (local Azure Storage emulator) - the Functions host checks
   storage health on startup even for HTTP-only triggers; without it running,
   the host reports itself unhealthy. Installed via `npm install -g azurite`,
   started with `azurite --location <some folder>`.
2. **The Functions host itself** - either `dotnet run` from a terminal, or
   F5 in Visual Studio.

**GUI-only testing (no terminal), via a `.http` file in the project**
(`AzureInventoryPlatform.Functions.http`) - Visual Studio renders a
clickable "Send Request" link above each request block:
- **HTTP** - send the token request, copy the `token` value into the
  `@token` variable (no surrounding quotes - a real mistake hit here: an
  extra quote character becomes part of the header value and breaks
  signature validation), then send the low-stock request with
  `Authorization: Bearer {{token}}`. A request with no header at all
  should come back `401`.
- **Timer** - Azure Functions Core Tools exposes an admin endpoint that
  fires any function on demand: `POST /admin/functions/LowStockTimerCheck`.
  The response panel stays empty; the real result appears in the Output
  window within a second, e.g.
  `Scheduled low-stock check: 149 item(s) at or below reorder level.`
  Cross-checking that number against the HTTP report's own output (same
  underlying query) is the actual verification.
- **Blob** - upload any image directly to the `product-images` container
  via the Portal or Storage Explorer - no relation to any real product
  required, since the trigger doesn't know or care about the `Products`
  table. Watch for `New product image detected` -> `Thumbnail saved`, then
  confirm a same-named file exists in `product-images-thumbnails`.

**Verified live**, not mocked, for all three: HTTP - no token → 401,
correct login → token issued, token → real low-stock rows from Azure SQL.
Timer - manually fired and via its own 5-minute schedule, count matched
the HTTP report exactly. Blob - direct upload to the real Storage
container produced a correctly-sized thumbnail in the second container,
confirmed by opening the file, not just trusting the log line. One
transient `SqlException: Connection Timeout Expired` was hit on the very
first HTTP call after a cold start (connection pool warming up) - retried
successfully with no code change, not a bug.

---
**Previous phase:** [Phase 4 — Blob Storage](phase-04-blob-storage.md)
**Next phase:** Phase 6 — Service Bus *(not started yet)*
