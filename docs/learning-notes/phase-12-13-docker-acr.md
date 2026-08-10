# Phase 12 & 13 — Docker and Container Registry

## AZ-204 objectives covered
- Develop solutions that use containers (Docker, Azure Container Registry, Azure Container Instances)

## 1. Business scenario

Up to now, the Web app only runs "the App Service way" — Azure's own
platform manages the .NET runtime, the OS, and how the app gets started.
Containerizing packages the app plus everything it needs to run into one
self-contained image that runs identically anywhere - a laptop, a
different cloud, or a different Azure service entirely.

Done together on purpose, same reasoning as Key Vault + Managed Identity
back in Phase 8/9: a container image is useless without somewhere to
store it, so Docker and Container Registry are really one piece of work.

**Deliberately out of scope:** switching the live App Service over to
run from this container image. It stays on its existing Code deployment,
untouched. This phase proves the image builds and runs correctly - it
doesn't change how the live site is actually hosted.

## 2. A real blocker, and the pivot it forced

No Docker Desktop is installed on this machine, so the plan going in was
to build the image entirely in the cloud via **Azure Container Registry
Tasks** (Quick Task) - point ACR at the GitHub repo, let Azure clone and
build it server-side, no local Docker needed at all.

That hit a wall: **ACR Tasks is blocked on this subscription.**

```
(TasksOperationsNotAllowed) ACR Tasks requests for the registry
niroinventoryacr and <subscription-id> are not permitted. Please file an
Azure support request at http://aka.ms/azuresupport for assistance.
```

This is a real, documented Azure restriction — free-trial/limited
subscriptions have ACR Tasks' build compute disabled by default, mainly
to stop the feature being abused for things like crypto-mining. It
explained two symptoms at once: the Portal's Tasks blade had no working
Create option, and the identical `az acr build` command from Cloud Shell
failed with the exact same error - confirming it was a subscription-level
lock, not a UI bug.

**The pivot:** use GitHub Actions' own runners as the build machine
instead. They already have Docker installed, so a normal `docker build` +
`docker push` works fine - it never touches the blocked ACR Tasks feature
at all, since a plain registry push only needs write access, not that
specific compute feature.

## 3. Flow

```
git push (or manual trigger)
      -> GitHub Actions runner checks out the repo
      -> docker/login-action logs into niroinventoryacr.azurecr.io
         (using ACR admin credentials stored as GitHub secrets)
      -> docker/build-push-action builds the Dockerfile
         (on GitHub's own runner - not Azure, not ACR Tasks)
      -> pushes the built image straight into the registry,
         tagged "latest" and with the commit SHA

Separately, to prove the image actually runs (not just builds):
      -> deploy that same image to a disposable Azure Container Instance
      -> supply real config as environment variables (SQL connection
         string, Auth credentials, Storage connection string) - this
         container has no Key Vault or Managed Identity of its own,
         so it falls back to the same plain-config code paths that
         existed before those phases
      -> hit its public IP, confirm the site actually works
      -> delete the Container Instance once proven
```

## 4. Code

- **`Dockerfile`** (repo root) - multi-stage build. First stage uses the
  full .NET SDK image to restore and publish just the Web project (the
  Functions project is unrelated to this image, excluded via
  `.dockerignore`). Second stage copies only the published output into
  the much smaller ASP.NET runtime image - no compiler/build tools ship
  in the final image. Listens on port 8080 (`ASPNETCORE_URLS=http://+:8080`).
- **`.github/workflows/docker-build-push.yml`** - triggers on
  `workflow_dispatch` (manual) or a push touching the Dockerfile/Web
  project. Three steps: checkout, `docker/login-action` (using
  `ACR_LOGIN_SERVER`/`ACR_USERNAME`/`ACR_PASSWORD` GitHub secrets),
  `docker/build-push-action` (builds and pushes in one step, tagged both
  `latest` and the commit SHA for traceability).

## 5. Azure resources

- **Container Registry** `niroinventoryacr`, **Basic** tier (flat
  ~$5/month, unlike Container Instances which bill per-second - this one
  is meant to stay running permanently, like every other resource in the
  project), **RBAC Registry Permissions** (not the ABAC option - simpler,
  no repository-level conditions needed for one image), **Admin user**
  enabled specifically so GitHub Actions could authenticate with a
  username/password pair rather than a service principal - simplest
  option for a project this size.
- **Container Instance** `niro-inventory-web-test` - explicitly disposable
  (named `-test`, deleted immediately after proving the image works).
  South India region - Container Instances aren't billed differently by
  region here, and the earlier Cosmos DB region-availability pattern
  suggested not fighting the Portal's regional offering for a temporary
  resource.

## 6. Testing (three rounds, each finding a real gap)

**Round 1 - SQL only:** deployed with just `ConnectionStrings__InventoryDb`
as an environment variable. Login page loaded, but real credentials
failed - because `Auth:Username`/`Auth:Password` weren't configured
anywhere on this container (no Key Vault, no shared App Service settings).

**Round 2 - added Auth:** deleted and recreated with `Auth__Username` and
`Auth__Password` added. Login succeeded, Dashboard loaded with real data
(111 products, 500 inventory records, matching live). Clicking Products
crashed with a generic error - because `ProductsController`'s constructor
requires `ProductImageStorage`, which tries to build a Blob client
immediately, and this container had no `Storage:AccountName` (no Managed
Identity here) and no real `Storage:ConnectionString` (empty placeholder
from `appsettings.json`) - it throws trying to build a Blob client from
an empty string, which fails the whole controller's construction before
any actual image logic even runs.

**Round 3 - added Storage:** deleted and recreated a third time with
`Storage__ConnectionString` added too (bypassing Managed Identity for
this one disposable test, same reasoning as the SQL/Auth fallback).
Products, Warehouses, and the rest of the CRUD pages all loaded correctly
with real data.

Container Instances don't support editing an existing container's
environment variables in place via the Portal - each round required a
full delete-and-recreate, not just an edit.

**Cleanup after testing:** the Container Instance was deleted (bills
per-second while running, no reason to keep it up). The Azure Monitor
alert from Phase 11 was also disabled around this time (via its action
group) since it had been firing repeatedly from real traffic during this
phase's testing.

---
**Previous phase:** [Phase 11 — Azure Monitor](phase-11-azure-monitor.md)
**Next phase:** Phase 14 — GitHub Actions CI/CD *(not started yet - this
phase's workflow only builds/pushes the image; the live App Service's
actual deployment is still 100% manual)*
