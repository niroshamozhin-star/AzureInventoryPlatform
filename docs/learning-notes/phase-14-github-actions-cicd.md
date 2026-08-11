# Phase 14 — GitHub Actions CI/CD

## AZ-204 objectives covered
- Implement CI/CD using GitHub Actions

## 1. Business scenario

Every phase up to now has been deployed manually — Visual Studio's
Publish button for the Web app's code, and manual `docker build`/`push`
for the image in Phase 12/13 (before that workflow existed). This phase
automates both halves properly: a build pipeline that runs itself on
every push, and a deploy pipeline that ships that code to the live App
Service on demand.

**Deliberately two separate workflows, not one**, because they answer
different questions and carry different risk:

| | `docker-build-push.yml` | `deploy-webapp.yml` |
|---|---|---|
| Question it answers | Does the image still build? | Should this code go live? |
| Trigger | `push` to `master` (+ manual) | manual (`workflow_dispatch`) only |
| Affects the live site? | No — pushes to ACR only | Yes — deploys straight to production |
| This is | **CI** | **CD**, but manually gated |

## 2. `on: push` vs. `on: workflow_dispatch`

- **`on: push`** — the workflow runs itself, automatically, the moment
  matching code is pushed. No one has to remember to trigger it. Right
  fit for "does this still build" — cheap to run, low risk if it's
  wrong (worst case: a red X in Actions).
- **`on: workflow_dispatch`** — adds a manual **Run workflow** button in
  the Actions tab. Nothing happens until a person clicks it. Right fit
  for "deploy this to production" — a bad commit sitting in `master`
  doesn't automatically become a bad commit running live; a human
  decides *when* that happens, not just *what*.
- Both workflows in this repo actually use `workflow_dispatch` as a
  fallback trigger too — `docker-build-push.yml` runs on push AND can be
  run manually; `deploy-webapp.yml` is manual-only, on purpose (see
  table above).

## 3. Flow

```
CI — docker-build-push.yml (automatic)
  git push to master (Dockerfile / Web project / workflow file changed)
        -> GitHub Actions runner checks out the repo
        -> docker/login-action logs into niroinventoryacr.azurecr.io
        -> docker/build-push-action builds + pushes, tagged
           "latest" and the commit SHA
        (nothing live consumes this image yet — proves the build stays
        green, exactly like Phase 12/13)

CD — deploy-webapp.yml (manual only)
  a person clicks "Run workflow" in the GitHub Actions tab
        -> checkout
        -> dotnet publish (Release, same as VS's own Publish step)
        -> azure/webapps-deploy@v3 pushes the published output to
           niro-inventory-webapp using a publish-profile secret
        -> live App Service restarts with the new code
```

## 4. Code

- **`.github/workflows/docker-build-push.yml`** — unchanged from
  Phase 12/13, already covered there. Included here only because it's
  the "CI" half of this phase's story.
- **`.github/workflows/deploy-webapp.yml`** — new. `workflow_dispatch`
  only (no `push` trigger — see reasoning above). Three steps: checkout,
  `dotnet publish` the Web project to a local folder, then
  `azure/webapps-deploy@v3` ships that folder to the App Service using
  `AZURE_WEBAPP_PUBLISH_PROFILE` (a GitHub secret holding the same
  publish-profile XML Visual Studio's Publish dialog uses). Deploys the
  .NET code directly — does **not** touch the Docker image or switch the
  App Service to container deployment; that stays explicitly out of
  scope, same as Phase 12/13.

## 5. A real blocker: Basic Auth was disabled

Downloading the publish profile from the Portal failed with **"Basic
authentication is disabled."** This is a real Azure security-hardening
default — App Service can require the more secure OIDC/federated-
credential deployment method instead of the classic username/password
publish profile.

**Fix chosen:** App Service → Configuration → General settings →
**SCM Basic Auth Publishing Credentials** → On → Save. (OIDC is the more
secure long-term option, but re-enabling Basic Auth matches the level of
complexity the rest of this project has used and keeps the publish-
profile approach — the same one VS Publish already relies on.)

## 6. GitHub secrets used

| Secret | Used by | Holds |
|---|---|---|
| `ACR_LOGIN_SERVER`, `ACR_USERNAME`, `ACR_PASSWORD` | `docker-build-push.yml` | ACR admin credentials (Phase 12/13) |
| `AZURE_WEBAPP_PUBLISH_PROFILE` | `deploy-webapp.yml` | The App Service's publish-profile XML |

## 7. Testing

- Triggered `deploy-webapp.yml` manually from the Actions tab —
  **Run #1 succeeded in 46 seconds.**
- Confirmed in the App Service's own **Deployment Center → Logs**: one
  entry, status **Succeeded**, deployed via `OneDeploy`.
- Confirmed the live site itself still works after the automated
  deploy — logged in, opened Products, real data loaded correctly
  (same site, now shipped by a pipeline instead of VS Publish).

## 8. What this phase deliberately does not do

- No automatic deploy on push — a bad commit can reach `master` without
  it silently going live. Promoting `deploy-webapp.yml` to `on: push`
  later is a one-line change, not a redesign, if that tradeoff is ever
  revisited.
- No switch from Code deployment to Container deployment on the App
  Service — the Docker image this repo builds isn't running anywhere
  live; it only proves it builds and (per Phase 12/13's Container
  Instance test) runs correctly.

---
**Previous phase:** [Phase 12 & 13 — Docker and Container Registry](phase-12-13-docker-acr.md)

This was the last phase of the 14-phase AZ-204 roadmap for this project.
