# Railway deployment notes

The app runs on Railway as three Docker services (frontend, API, migration
service) plus a managed Postgres database. Most traffic flows browser →
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

## Verifying after deploy

1. Load the app, open the browser dev tools network tab, and filter on
   `negotiate` — the request should go to the API's public domain and return
   200 with CORS headers echoing the frontend origin.
2. The follow-up WebSocket connection to `wss://api.../hubs/...` should show
   status 101 (Switching Protocols).
3. Editing a meal plan or grocery list in a second browser session should
   reflect in the first without a refresh.
