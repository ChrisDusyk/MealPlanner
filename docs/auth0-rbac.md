# Auth0 RBAC Setup (MealPlanner)

This guide configures Auth0 so the application can enforce `user` and `admin` roles from access token claims.

## 1) Create roles
In Auth0 Dashboard:
1. Go to **User Management → Roles**.
2. Create role `user`.
3. Create role `admin`.

## 2) Assign default `user` role to existing users and new signups

The API enforces a `RequireUserRole` policy on core endpoints (including `/api/users/sync`).
Any authenticated user **without** the `user` role will receive `403 Forbidden` on these endpoints,
which will break first-login/user provisioning flows.

### Existing users
Assign `user` to all existing app users.

Options:
- Dashboard (manual): **User Management → Users → [user] → Roles → Assign Roles**.
- Management API (bulk): iterate users and assign role id for `user`.

### New users and first login
Ensure that every newly created Auth0 user automatically receives the `user` role before or at
their first successful login. Choose one of the following strategies:

- **Manual onboarding:** As part of your onboarding process, an admin periodically reviews new
  Auth0 users and assigns the `user` role via the Dashboard before they can use the app.
- **Automated role assignment (recommended):** Configure an Auth0 flow (e.g. Action, Rule, or
  other automation) that, on login or user creation, checks whether the user has any roles and,
  if not, assigns the default `user` role. This ensures `/api/users/sync` and other core
  endpoints work on first login.

If a new user reaches the app without the `user` role, they will be able to authenticate but
will see 403 responses from protected API calls (including `/api/users/sync`) until the role
is assigned.
## 3) Assign first admin
Assign your account the `admin` role in addition to `user`.

## 4) Enable API RBAC for your MealPlanner API
In Auth0 Dashboard:
1. Go to **Applications → APIs → [MealPlanner API]**.
2. Enable **RBAC**.
3. Enable **Add Permissions in the Access Token** (optional for this app, but recommended).

## 5) Add post-login Action to inject namespaced roles claim
Create an Auth0 Action in **Actions → Library** (Trigger: **Login / Post Login**) and deploy it.

```js
exports.onExecutePostLogin = async (event, api) => {
  const namespace = 'https://mealplanner';
  const roles = event.authorization?.roles || [];

  api.accessToken.setCustomClaim(`${namespace}/roles`, roles);
};
```

Then add this Action to the **Post Login** flow.

## 6) Confirm application settings remain aligned
Current frontend env vars should remain set:
- `AUTH_AUTH0_ID`
- `AUTH_AUTH0_SECRET`
- `AUTH_AUTH0_ISSUER`
- `AUTH_API_AUDIENCE`

API JWT settings should match the same issuer/audience:
- `Authentication:Authority`
- `Authentication:Audience`

## 7) Verify end-to-end behavior
1. Sign in as a user with only `user` role.
2. Decode access token and confirm `https://mealplanner/roles` contains `user`.
3. Access normal app routes (`/app`) and API endpoints (`/api/recipes`) → allowed.
4. Access admin route (`/app/admin`) or admin endpoint (`/api/admin/ping`) → forbidden.
5. Sign in as an admin account (`admin` role present) → admin route and endpoint allowed.
