# Better Auth RBAC Setup (MealPlanner)

MealPlanner uses [Better Auth](https://www.better-auth.com/) as its identity
provider. Better Auth is embedded in the SvelteKit frontend and issues
RS256-signed JWTs via its JWT plugin. The .NET API validates those tokens
against the published JWKS document and enforces `user` and `admin` roles
through the `RequireUserRole` / `RequireAdminRole` authorization policies.

This guide describes how roles are assigned, how the JWT role claim is
shaped, and how to verify the end-to-end behaviour after the Auth0 → Better
Auth migration.

## 1) Roles overview

Better Auth's **admin** plugin adds a `role` column to the `auth.user`
table with these conventions:

- Default role for all new sign-ups: `user`
- Elevated role for admins: `admin`

Roles are set directly on the Better Auth user record (`auth.user.role`).
The JWT plugin copies the value into the access token so the API can gate
requests without talking to the database.

## 2) Promoting an admin

There is no external admin UI for Better Auth in this project. Promote a
user by updating the `auth.user.role` column in Postgres:

```sql
update auth."user"
set role = 'admin'
where email = 'you@example.com';
```

The change takes effect on the next sign-in (or once the current JWT
expires — the default TTL is 15 minutes).

All other users automatically receive the `user` role at sign-up because
Better Auth's admin plugin defaults `role` to `'user'` when not specified.

## 3) JWT role claim shape

The JWT plugin in `frontend/src/lib/server/auth-options.ts` shapes the
access token payload. Each token contains:

- `sub`: Better Auth user id (stable across sign-ins)
- `email`, `name`: current profile values
- `role`: the plain Better Auth role (e.g. `"admin"`)
- `https://mealplanner/roles`: legacy namespaced claim retained for
  backwards compatibility with any existing tooling; it mirrors the
  value of `role` wrapped in a one-element array

Example payload:

```json
{
  "sub": "6wRxe8WwIi0tJ0kL",
  "email": "you@example.com",
  "name": "Pat",
  "role": "admin",
  "https://mealplanner/roles": ["admin"],
  "iss": "http://localhost:3000",
  "aud": "mealplanner-api",
  "iat": 1735084800,
  "exp": 1735085700
}
```

The API's `RbacAuthorization.ExtractRoles` reads whichever of these
claims is present (`role`, `ClaimTypes.Role`, or the legacy namespaced
claim), so rotating away from the legacy claim in a future release is a
non-breaking change.

## 4) API configuration

The API's JWT bearer middleware is configured in `MealPlanner.Api/Program.cs`:

- `Authentication:Authority` — Better Auth's base URL (e.g.
  `http://localhost:3000` in dev). This is used as both the issuer and
  the base address for JWKS lookup (`{Authority}/api/auth/jwks`).
- `Authentication:Audience` — must match `BETTER_AUTH_JWT_AUDIENCE`
  (default `mealplanner-api`).

The custom `BetterAuthJwksConfigurationRetriever` wraps Better Auth's
JWKS endpoint (which does not ship a full OIDC discovery document) so
`AddJwtBearer` can hydrate its signing-key cache normally.

## 5) Environment variables

Frontend (see `.env.example`):

- `BETTER_AUTH_SECRET` — required; used to sign cookies/tokens
- `BETTER_AUTH_URL` — base URL of the running frontend
- `BETTER_AUTH_JWT_AUDIENCE` — API audience claim (defaults to
  `mealplanner-api`)
- `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET` — optional, enables the
  "Continue with Google" social login button on the sign-in / sign-up
  pages

API:

- `Authentication:Authority` — Better Auth base URL
- `Authentication:Audience` — same value as `BETTER_AUTH_JWT_AUDIENCE`

## 6) Verify end-to-end behaviour

1. Sign up via `/auth/signup`. Confirm the new row in `auth.user` has
   `role = 'user'`.
2. Sign in and decode the access token (e.g. via the browser devtools
   or `jwt.io`). Verify `role: "user"` is present.
3. Hit authenticated endpoints (e.g. `/api/recipes`, `/api/users/me`).
   Expect `200 OK`.
4. Visit `/app/admin` or `/api/admin/ping`. Expect `403 Forbidden`.
5. Promote the account to `admin` (see section 2), sign out, sign back
   in, and re-run step 4. Expect `200 OK`.

## 7) Migration notes (Auth0 → Better Auth)

- The `users.Auth0UserId` column was renamed to `users.AuthUserId` in
  migration `RenameAuthUserIdAndAddProfileFields`. Existing rows are
  automatically re-linked on first Better Auth sign-in because
  `UpsertUserFromAuthCommandHandler` falls back to matching the account
  by email when no row exists for the new `AuthUserId`.
- The same migration added `DisplayName`, `Timezone`, and
  `OnboardingCompletedAt` columns which are populated by the onboarding
  flow (`PATCH /api/users/me/onboarding`).
- The frontend now redirects any authenticated user under `/app` whose
  `onboardingCompletedAt` is null to `/app/onboarding`, where they can
  capture the remaining profile fields before accessing the rest of the
  app.
