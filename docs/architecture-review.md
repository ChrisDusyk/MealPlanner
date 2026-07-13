# MealPlanner Architecture & Code Review

_Reviewed July 2026. Line numbers reference the commit at the time of review and will drift as files change._

This review covers the whole solution: the .NET 10 Aspire backend (`MealPlanner.AppHost`, `MealPlanner.Api`, `MealPlanner.MigrationService`, `MealPlanner.ServiceDefaults`, `MealPlanner.Api.Tests`) and the SvelteKit 2 / Svelte 5 frontend. Overall the codebase is in good shape: the vertical-slice CQRS design with `Result<T>`/`Option<T>` is applied consistently, async/cancellation usage is disciplined, packages are current, and handler-level test coverage is broad. The findings below are the places where the code has drifted, duplicated, or left gaps.

Items marked **✅ fixed** were addressed in the same branch as this document; everything else is a recommendation.

---

## Backend

### 1. ✅ fixed — SSRF gap in recipe import (security)

`RecipePageTextExtractor.ValidateAddressSafetyAsync` resolved DNS and rejected private IPs *before* fetching, but the page-fetch `HttpClient` followed redirects freely, so a public URL could 3xx-redirect to `169.254.169.254` or an internal host — and DNS rebinding between validation and fetch defeated the check entirely.

**Fix applied:** the page-fetch client now uses a `SocketsHttpHandler` (`Features/Recipes/Import/SsrfProtection.cs`) whose `ConnectCallback` re-resolves and validates the destination address at connect time for every connection, including each redirect hop (capped at 5). The address checks also gained `0.0.0.0/8`, IPv6 unique-local (`fc00::/7`), and `::`. The pre-flight check remains for friendly error messages.

### 2. ✅ fixed — `Result<T>` treated a null success value as failure

`Bind`/`Map`/`Match` in `Shared/Result.cs` guarded on `IsSuccess && Value is not null`, so a legitimately successful result carrying `null` fell into the failure branch and dereferenced the null `Error` (`Error!`) — a guaranteed `NullReferenceException`. They now branch on `IsSuccess` alone; `MealPlanner.Api.Tests/Shared/ResultTests.cs` covers the matrix.

### 3. ✅ fixed — `DATABASE_URL` credentials not URL-decoded

`Program.cs` split `uri.UserInfo` on every `:` and injected the raw (still percent-encoded) values into the connection string, breaking passwords containing `%`-escapes or `:`. The frontend's `toPostgresUrl` (`frontend/src/lib/server/auth-options.ts`) already handled this correctly — the two implementations had diverged. Parsing now lives in `Shared/DatabaseUrlParser.cs` using `NpgsqlConnectionStringBuilder`, with tests.

### 4. Duplicated, inconsistent recipient-by-email lookup

The "normalize email and find user" block is copy-pasted in four slices, and the normalization disagrees:

- `Features/MealPlans/Commands/ShareMealPlan.cs:49-54` — culture-sensitive `.ToUpper()`, **no `.Trim()`**
- `Features/GroceryLists/Commands/ShareGroceryList.cs:48-50` — same
- `Features/Users/Queries/FindUserByEmail.cs:32-34` — `.Trim().ToUpperInvariant()`
- `Features/Users/Commands/SendFriendRequestByEmail.cs:54-56` — `.Trim().ToUpperInvariant()`

A user typing `" friend@example.com "` can be found by the friends flow but not by the share flows, and culture-sensitive uppercasing misbehaves under e.g. the Turkish locale. **Suggestion:** extract a single shared helper (e.g. `UserEmailLookup` in `Features/Users`) that trims and uses `ToUpperInvariant`, and reuse it from all four call sites.

### 5. Share auto-propagation logic duplicated

`Features/GroceryLists/Commands/GenerateGroceryList.cs:202-319` embeds two private methods (`PropagateSharesFromMealPlanAsync`, `PropagateAutoSharesFromFriendPreferencesAsync`) that re-implement the "load existing shares → diff → `AddRange`" pattern that also exists in the meal-plan and friends sharing slices. **Suggestion:** extract a `ShareService` (or shared internal helpers) that centralizes friendship resolution and share diffing; removes roughly 120 lines of near-duplicates and keeps the propagation rules in one place.

### 6. Per-endpoint and per-handler boilerplate

- `GetUserId(HttpContext)` claim extraction is copied into every endpoint file (e.g. `Features/Recipes/RecipeEndpoints.cs:36-38`).
- The DataAnnotations `ValidateRequest` helper is duplicated across endpoint files.
- Nearly every handler wraps its body in an identical `try/catch → Error(ErrorCodes.DatabaseError, …)` block.

**Suggestion:** move `GetUserId`/`ValidateRequest` to shared extension methods, and introduce a decorator around `ICommandHandler`/`IQueryHandler` (easy to add centrally in `Shared/CqrsRegistrationExtensions.cs`) that provides the exception-to-`Result` mapping once.

### 7. `GoogleOAuthService` cleanups

- `BuildAuthorizationUrlAsync` (`Features/Integrations/GoogleKeep/Services/GoogleOAuthService.cs:22-65`) is `async` with no `await` (`_ = cancellationToken;`) — a pointless state machine and a CS1998 smell. Make it synchronous or return `Task.FromResult`.
- `ParseIdTokenClaims` (`:252-283`) hand-rolls base64url JWT payload decoding without validation. It's tolerable because the token arrives directly from Google over TLS, but the framework's `JsonWebToken`/`JwtSecurityTokenHandler` is less fragile.

### 8. jsonb value comparer cost

`SetJsonValueComparer` (`Data/MealPlannerDbContext.cs:229-236`) JSON-serializes for equality, hash code, and snapshotting on every EF change-detection pass. For large ingredient/day collections this is measurable on every `SaveChanges`. **Suggestion:** if profiling shows it matters, replace with structural comparers per element type, or mark the collections as owned/tracked differently.

### 9. Biggest test gap: no integration tests

All backend tests (300+) are handler-level unit tests on the EF **InMemory** provider. InMemory does not enforce the unique/filtered indexes the schema relies on (acknowledged in `Data/MealPlannerDbContext.cs:71-81`), so the following are untested end-to-end: uniqueness constraints, the `23505` seed-race handling in `MealPlanner.MigrationService/DbMigrator.cs`, jsonb round-tripping, JWT auth wiring, and RBAC policies. **Suggestion:** add a small `WebApplicationFactory` + Testcontainers-Postgres suite covering one representative flow per feature. This is the highest-value testing investment available. (Note: the current repo test rules mandate unit-only tests — carve out a separate integration test project so the rule stays intact.)

---

## Frontend / UI

### 1. Missing shared primitives (largest source of duplication)

- **Toast**: an identical ~44-line toast block plus `showToast`/`toastVisible`/`toastType` state is copy-pasted in `routes/app/meal-plans/+page.svelte:651-694` and `routes/app/grocery-lists/+page.svelte:1011-1055`, with two more ad-hoc variants in `CatalogueBrowserModal.svelte` and `CatalogueRecipeForm.svelte`. Extract a `Toast.svelte` plus a tiny store.
- **Modal**: the `trapFocus` handler and open/close focus `$effect` are byte-identical in `meal-plans/CopyModal.svelte`, `meal-plans/ShareModal.svelte`, and `meal-plans/AddItemModal.svelte`, as is the backdrop/`role="dialog"` shell. Extract a `Modal.svelte` (backdrop, focus trap, Escape, focus restore) and render content via snippets.
- **Icon**: 294 inline `<svg>` blocks across 27 files; the close-X, plus, checkmark, and share icons are re-inlined repeatedly, and the Google logo SVG is duplicated in `auth/signin` and `auth/signup`. Extract an `Icon.svelte`.
- **Button/Badge**: the primary green button is re-declared dozens of times with drifting padding (`px-3 py-2` → `px-6 py-3`) and rounding (`rounded-lg` vs `rounded-xl`). Permission badges even disagree on color for the same concept (amber/green in `ShareModal.svelte:274-281` vs blue in `grocery-lists/+page.svelte:475-478`). Extract `Button.svelte` and `PermissionBadge.svelte`.
- Also worth extracting: `ConfirmDialog`, `EmptyState` (nice bespoke empty states exist in grocery-lists, recipes, and the dashboard — same design, three implementations).

### 2. Oversized components

- `routes/app/grocery-lists/+page.svelte` — **1056 lines** mixing progress bar, actions, inline share panel, pantry staples, item rows, shared-with-me, delete dialog, and toast. Split into `GroceryItemRow`, `SharePanel`, `PantryStaples`, `ConfirmDialog`, `Toast`.
- `lib/components/RecipeForm.svelte` (808) and `CatalogueRecipeForm.svelte` (761) share near-identical ingredient add/remove/validate/URL-import logic and markup — extract a shared `IngredientEditor.svelte` and a shared `units` constant.
- `lib/components/Navbar.svelte` (450) fully duplicates its menu markup for desktop (`:98-279`) and mobile (`:316-449`).
- `routes/app/meal-plans/+page.svelte` (695) would shrink substantially once Toast/share pieces are shared.

### 3. Two different sharing UIs

Meal plans use the accessible, reusable `ShareModal.svelte`; grocery lists reimplement sharing as an inline dropdown panel (`grocery-lists/+page.svelte:390-494`) with its own state, handlers, colors, and no focus management. Consolidate on one shared component.

### 4. No error boundary, no navigation loading state

- There is no `+error.svelte` anywhere; `error(500, …)` throws from server loads (e.g. `meal-plans/+page.server.ts:37-40`) land on SvelteKit's default page. Add a styled root `src/routes/+error.svelte`.
- Week navigation triggers a full `goto` reload with no pending indicator (`meal-plans:133-135`, `grocery-lists:117-119`); a small `navigating`-store indicator would fix both pages at once.

### 5. Accessibility inconsistencies

- The grocery-list delete dialog (`grocery-lists/+page.svelte:957-1009`) has `role="dialog"` but no `aria-modal`, focus trap, Escape handling, or focus restore — while the meal-plan modals do all of this. The shared `Modal.svelte` from finding 1 solves this for free.
- The navbar user dropdown uses `aria-haspopup="true"` where a `menu` structure (`role="menu"`/`menuitem`) is intended (`Navbar.svelte:130-131,153`).
- `CatalogueRecipeCard.svelte` renders the title as a bare `<button>` (`:63-69`) and the recipe image with `alt=""` (`:31-33`), losing the heading semantics and image context its sibling `RecipeCard.svelte` provides.

### 6. Three different form-validation styles

`RecipeForm.svelte` sets the standard (per-field errors, `aria-invalid`/`aria-describedby`, focus-first-invalid, sr-only summary); the grocery/account friend forms use ad-hoc error strings; the auth pages rely on native `required`/`minlength` with a single generic failure line. Pick the `RecipeForm` approach and reuse it (a small `FieldError` component + validation helper would go far).

### 7. ✅ fixed — dead "Coming Soon" dashboard card

`routes/app/+page.svelte` showed a greyed-out "Grocery Lists — Coming Soon" card although the feature is fully shipped. Now a live link card matching the meal-plans card.

### 8. Magic values

- The 72px navbar height is hard-coded in four places (`+layout.svelte` `pt-[72px]`, `app/+layout.svelte` `min-h-[calc(100vh-72px)]`, `AppSidebar.svelte` `top-[72px]`/`top-[78px]`). Promote it to a CSS variable in `routes/layout.css`.
- `'ReadOnly'`/`'ReadWrite'` permission strings appear 24 times across four files with no shared constant. Export a `Permission` const/type from `$lib/api`.
- The theme tokens in `routes/layout.css` only cover greens; feature UIs introduce raw `blue-*`, `purple-*`, `amber-*` ad hoc (grocery share panel, shared lists, pantry staples). Tokenize the semantic colors so features stay consistent.

### 9. Mixed mutation patterns

Grocery lists mutate via SvelteKit form actions + `use:enhance`; meal plans and account use direct `fetch` against `+server.ts` endpoints. Both work, but pick one (form actions degrade more gracefully and centralize validation) and migrate incrementally.

---

## What to reuse when acting on this review

Backend: `Result<T>`/`Option<T>` + `ErrorCodes` (`MealPlanner.Api/Shared/`), reflection-based handler registration (`AddCqrsHandlers` — new handlers need no wiring), the named-HttpClient + `IOptions<T>` pattern (`AnthropicOptions`, `GoogleIntegrationsOptions`), and `IIntegrationTokenProtector` for secrets at rest.

Frontend: `WeekNavigator.svelte` (already shared by meal plans and grocery lists), the meal-plan modals as the a11y reference implementation, `$lib/api/apiHelpers.ts` (`ApiError`, `getApiBase`), `$lib/utils/date.ts` / `url.ts`, `$lib/context/appUserContext.ts`, and `$lib/auth/roles.ts`.

## Suggested order of attack

1. Shared `Modal` + `Toast` (fixes duplication *and* the a11y gaps at once)
2. `Icon`, `Button`, `PermissionBadge`, `EmptyState`; root `+error.svelte`
3. Email-lookup helper and `ShareService` extraction in the API
4. `IngredientEditor` extraction from the two recipe forms
5. Handler decorator for exception mapping; endpoint helper extensions
6. Integration test suite (WebApplicationFactory + Testcontainers-Postgres)
