# Railway deployment notes

The app runs on Railway as four Docker services (frontend, API, migration
service, flagd) plus a managed Postgres database. Most traffic flows browser →
SvelteKit server → API over Railway's private network, but **SignalR hub
connections are made directly from the browser to the API**, so the API needs
a public domain and the pieces below must line up.

## SignalR realtime connections

The browser connects to `/hubs/meal-plans` and `/hubs/grocery-lists` on the
API's public URL. Two environment variables make this work:

### Frontend service

| Variable | Example | Purpose |
| --- | --- | --- |
| `PUBLIC_API_URL` | `https://api.simplemealplanner.ca` | Browser-reachable API base URL used to build hub URLs. Read at **runtime** via SvelteKit's `$env/dynamic/public`, so no image rebuild is needed when it changes. |

Without it the hub URL resolves to a relative path on the frontend origin,
where nothing serves `/hubs/*` (the Vite proxy that handles this in local dev
does not exist in the production adapter-node server), and connections fail
with a 404 on negotiate.

### API service

| Variable | Example | Purpose |
| --- | --- | --- |
| `Cors__AllowedOrigins` | `https://simplemealplanner.ca` | Comma/semicolon-separated list of browser origins allowed to call the API cross-origin. Must be the frontend's public origin(s), scheme included, no trailing slash. |

The SignalR JavaScript client sends credentials on its negotiate request, so
the CORS policy uses `AllowCredentials` and therefore requires explicit
origins — wildcards are not allowed. When `Cors__AllowedOrigins` is empty
(the local-dev default) no CORS middleware is registered.

### Existing related variables

- `API_INTERNAL_URL` (frontend): private-network API URL used for
  server-to-server calls from the SvelteKit backend.
- `Authentication__Authority` (API): public frontend base URL; hub
  authentication validates the same Better Auth JWTs as the REST endpoints,
  passed via `access_token` in the query string for WebSocket transports.

## Feature flags (OpenFeature + flagd)

Feature flags are evaluated **server-side only** (in the API and in the
SvelteKit server) against flagd over gRPC. flagd runs as a **private** Railway
service (no public domain) built from [`flagd/Dockerfile`](../flagd/Dockerfile),
and reads its flag definitions from the API's internal endpoint, so toggling a
flag in the admin UI takes effect without a redeploy.

Flag definitions live in the `feature_flags` Postgres table; the API serves the
flagd-format document at `GET /internal/feature-flags`, and flagd HTTP-syncs
from it and hot-reloads on its poll interval.

### flagd service

Build from `flagd/Dockerfile` (Railway root directory `flagd`). Configure the
sync source with a service variable:

| Variable | Example | Purpose |
| --- | --- | --- |
| `FLAGD_SOURCES` | `[{"uri":"http://<api-service>.railway.internal:8080/internal/feature-flags","provider":"http"}]` | JSON array of flagd sync sources. Point it at the API's **private** internal endpoint so flagd polls it for flag definitions. Use `http://`, not `https://` — Railway's private network doesn't terminate TLS, and an `https://` URI with no explicit port defaults to 443, which nothing listens on internally ("connection refused"). Confirm the port under the API service's **Settings → Networking → Private Networking** (defaults to `8080` for the .NET 10 aspnet container image). |
| `FeatureFlags__SyncToken` | *(optional)* | If set, must match the API's `FeatureFlags__SyncToken`. flagd's HTTP sync source can only authenticate via its `authHeader` field (it cannot send an arbitrary custom header), so add `"authHeader":"Bearer <token>"` to the source object in `FLAGD_SOURCES`, e.g. `[{"uri":"...","provider":"http","authHeader":"Bearer <token>"}]`. Leave unset to rely on private-network isolation only. |

flagd serves gRPC evaluation on port `8013` over the private network.

### API service (additional)

| Variable | Example | Purpose |
| --- | --- | --- |
| `FeatureFlags__Host` | `mealplanner-flagd.railway.internal` | Private host of the flagd service used for gRPC evaluation. |
| `FeatureFlags__Port` | `8013` | flagd gRPC port. |
| `FeatureFlags__SyncToken` | *(optional)* | Shared secret required on `GET /internal/feature-flags`. |

### Frontend service (additional)

| Variable | Example | Purpose |
| --- | --- | --- |
| `FLAGD_HOST` | `mealplanner-flagd.railway.internal` | Private host of the flagd service for SSR evaluation. |
| `FLAGD_PORT` | `8013` | flagd gRPC port. |

> Startup order: flagd depends on the API being up (it syncs from it). The API
> and frontend connect to flagd lazily and retry, so they do **not** need to
> wait for flagd. Editing a flag updates the Postgres row; flagd picks up the
> change on its next HTTP-sync poll.

## Verifying after deploy

1. Load the app, open the browser dev tools network tab, and filter on
   `negotiate` — the request should go to the API's public domain and return
   200 with CORS headers echoing the frontend origin.
2. The follow-up WebSocket connection to `wss://api.../hubs/...` should show
   status 101 (Switching Protocols).
3. Editing a meal plan or grocery list in a second browser session should
   reflect in the first without a refresh.
