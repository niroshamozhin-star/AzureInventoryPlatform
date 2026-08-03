# Phase 3 — Authentication

## AZ-204 objectives covered
- Implement authentication and authorization
- Secure app configuration data (credentials) with User Secrets

## 1. Business scenario

Every page in the app (Products, Warehouses, Inventory, Import, Reports) was
open to anyone who had the URL. This phase adds a single login covering the
whole application: one username/password, one session, every page requires
it.

### Why not JWT, given that's what got built first
JWT bearer authentication was implemented and fully tested first (see the
"JWT evaluation" section below) — a token-based scheme where the client
fetches a signed token once and attaches it to every request's `Authorization`
header. That's the right tool for an API meant to be called by another
*program* (a script, a mobile client, a separate service) with no browser
session behind it. But once the requirement became "every page in this
interactive, multi-page browser app needs one login," JWT was the wrong fit —
it would have meant either a confusing double-login (a site-wide login *and*
a separate per-report token step) or reinventing session-like behavior on top
of a stateless token by hand. **Cookie authentication** is what ASP.NET Core
provides specifically for this shape of problem: log in once, the browser
carries a session cookie automatically on every subsequent request, no manual
token handling needed anywhere in the UI.

## 2. Code

- **`Program.cs`**: registered `AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(...)`,
  with `LoginPath`/`LogoutPath`/`AccessDeniedPath` all pointing at
  `/Account/Login`. `app.UseAuthentication()` still runs immediately before
  `app.UseAuthorization()` — same ordering rule as before: authentication
  decides *who* the caller is (now: do they have a valid auth cookie?),
  authorization decides *whether* they're allowed to proceed.
- **`[Authorize]` added to every page controller** — `HomeController`,
  `ProductsController`, `WarehousesController`, `InventoryController`,
  `ImportController`, `ReportsController`. No global fallback policy was
  used (deliberately) — an app-wide fallback policy would have also caught
  `/health`, which Azure's own health probes hit with no login and must keep
  working unauthenticated. Putting `[Authorize]` on each real page controller
  individually, and leaving `/health`'s minimal-API endpoint alone, is more
  explicit and avoids that trap entirely.
- **Added `Controllers/AccountController.cs`**, marked `[AllowAnonymous]`
  (the one exception to "every controller requires login"):
  - `GET /Account/Login` — shows the login form.
  - `POST /Account/Login` — checks the posted username/password against
    `Auth:Username`/`Auth:Password` in configuration (same config keys used
    by the earlier JWT version). On success, builds a `ClaimsPrincipal` with
    one `ClaimTypes.Name` claim and calls `HttpContext.SignInAsync(...)` —
    this is what actually writes the encrypted auth cookie to the response.
    Honors a `returnUrl` so logging in from a deep link lands back where you
    started, not always the dashboard.
  - `POST /Account/Logout` — calls `HttpContext.SignOutAsync(...)`, which
    clears the cookie server-side (unlike the earlier JWT "logout," which
    could only ever forget the token client-side — a real session cookie
    *can* be invalidated on demand, which was one concrete advantage of
    switching).
- **`Views/Account/Login.cshtml`**: a plain form (username, password, hidden
  `returnUrl`), posting to `AccountController.Login`.
- **`Views/Shared/_Layout.cshtml`**: the nav bar (links + a "Log out" button
  showing the current username) only renders when
  `User.Identity?.IsAuthenticated == true`; an anonymous visitor sees just
  the brand and the login form, no app navigation to click into.
- **`ReportsController` reverted to server-rendering** its two report pages
  directly (like Phase 2), instead of the client-side fetch-with-bearer-token
  pattern from the JWT version — once the whole page requires login via
  cookie, there's no reason left for the page to *also* separately
  authenticate itself to an API before showing data.

### What got removed
`Controllers/AuthController.cs`, `Controllers/ReportsApiController.cs`,
`Models/AuthModels.cs`, `wwwroot/js/report-auth.js`, the `Jwt` section in
`appsettings.json`, the `Jwt:Key` User Secret, and the
`Microsoft.AspNetCore.Authentication.JwtBearer` package reference — all fully
superseded by the cookie scheme above, so kept out of the codebase rather
than left in as dead, unused code.

## 3. JWT evaluation (what was built and verified first)

Before settling on cookie auth, a complete JWT bearer flow was built and
proven end-to-end: `POST /api/auth/token` minted a signed token (HMAC-SHA256,
1-hour expiry) after checking credentials; `GET /api/reports/low-stock` and
`GET /api/reports/inventory-value` were marked `[Authorize]` and validated
that token via `AddJwtBearer(...)`. Verified live against real Azure SQL
data: no token → 401, wrong password → 401, correct login → token issued,
token → real seed data returned as JSON. That work isn't wasted — it's the
reason the tradeoff above ("JWT suits API/service clients, not an
interactive multi-page site") is a conclusion drawn from direct experience,
not just a rule read somewhere. Good, honest interview material: *"I built
both token-based and cookie-based auth, and can explain concretely why I'd
reach for each one."*

## 4. Azure Portal configuration

No new Azure resource — the same two settings from before, now serving the
cookie login instead of JWT:

1. **App Service → Environment variables → App settings**:
   - `Auth__Username` = `admin`
   - `Auth__Password` = a password of your choice
2. **Remove** `Jwt__Key` if it was already added from an earlier deploy
   attempt — it's no longer read by anything.
3. **Save** — App Service restarts to pick up the change.

## 5. Testing

**Locally:**
```bash
cd src/AzureInventoryPlatform.Web
dotnet run
```
Visit `http://localhost:5104/` — you're redirected to `/Account/Login`. Log
in with your `Auth:Username`/`Auth:Password` (from User Secrets), confirm the
dashboard and every page (Products, Warehouses, Inventory, Import, Reports —
both InventoryByWarehouse and LowStock) load normally, then click "Log out"
in the nav and confirm you're bounced back to the login page on the next
request.

**Verified live**, end to end, against the real Azure SQL seed/imported
data: unauthenticated request → redirected to login; wrong password → error
shown, no access; correct login → full access to every page including both
reports with real data; logout → immediately locked out again on the next
request.

**Automated:** `dotnet test` — 11 tests, all passing, using a fixed
test-only `Auth:Username`/`Auth:Password` (via
`WebApplicationFactory.WithWebHostBuilder(...).ConfigureAppConfiguration`),
independent of local User Secrets. Covers: unauthenticated request redirects
to login; wrong password shows an error; correct login grants access; logout
revokes it again; every page renders its expected seed data once logged in.

---
**Previous phase:** [Phase 2 — Azure SQL](phase-02-azure-sql.md)
**Next phase:** [Phase 4 — Blob Storage](phase-04-blob-storage.md) *(not started yet)*
