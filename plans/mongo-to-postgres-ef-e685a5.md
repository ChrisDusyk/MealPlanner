# Migrate from MongoDB to PostgreSQL with Entity Framework Core

Replace MongoDB persistence with PostgreSQL + EF Core across the entire API, using Aspire hosting packages, `dotnet ef` migrations applied at startup, a separate Aspire-orchestrated data migration console project, and UUID primary keys.

---

## Scope summary

| Area | Count | Notes |
|---|---|---|
| Handlers using `IMongoClient` | 46 files | Every query/command handler + 2 realtime notifiers |
| Document models (BSON) | 11 files | Will become EF entities |
| Mongo index initializers | 3 extensions | Replaced by EF migration indexes |
| Test helper | `MongoTestHelpers.cs` | Replaced with EF `DbContext` test patterns |
| AppHost Mongo resource | `AppHost.cs` | Swapped to Postgres resource |

## Target schema (relational + jsonb)

| Postgres table | PK | Key columns | jsonb columns |
|---|---|---|---|
| `users` | `Guid Id` | `auth0_user_id` (unique) | — |
| `recipes` | `Guid Id` | `user_id` | `ingredients` |
| `meal_plans` | `Guid Id` | `user_id`, `week_start` (unique pair) | `days` |
| `meal_plan_shares` | `Guid Id` | `owner_user_id`, `shared_with_user_id`, `week_start` (unique) | — |
| `grocery_lists` | `Guid Id` | `user_id`, `week_start` (unique pair) | `items`, `pantry_staple_items` |
| `grocery_list_shares` | `Guid Id` | `owner_user_id`, `shared_with_user_id`, `week_start` (unique) | — |
| `friendships` | `Guid Id` | `user_a_id`, `user_b_id` (unique pair) | — |
| `friend_requests` | `Guid Id` | `requester_user_id`, `recipient_user_id` (unique pair) | — |
| `friend_auto_share_preferences` | `Guid Id` | `user_id`, `friend_user_id` (unique pair) | — |
| `google_integration_connections` | `Guid Id` | `user_id`+`provider` (unique), `google_subject`+`provider` (unique) | `scopes` |
| `grocery_list_export_links` | `Guid Id` | `user_id`+`week_start`+`provider` (unique) | — |

## Packages

| Project | Add | Remove |
|---|---|---|
| `MealPlanner.AppHost` | `Aspire.Hosting.PostgreSQL` | `Aspire.Hosting.MongoDB`, `CommunityToolkit.Aspire.Hosting.MongoDB.Extensions` |
| `MealPlanner.Api` | `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.Design` | `Aspire.MongoDB.Driver.v3` |
| `MealPlanner.DataMigration` (new) | `Aspire.MongoDB.Driver.v3`, `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` | — |
| `Directory.Packages.props` | Add versions for new packages; remove Mongo versions after cleanup | — |

Also install `dotnet-ef` CLI tool: `dotnet tool install --global dotnet-ef`

## Git workflow

**Branch:** `feature/postgres-ef-migration`

Each phase below becomes one or more atomic commits.

---

## Phase 1 — EF Core infrastructure (commits 1–3)

### Commit 1: `feat(infra): add Postgres resource to AppHost, replace MongoDB`
- Update `MealPlanner.AppHost/AppHost.cs`: replace `AddMongoDB` → `AddPostgres(...).AddDatabase("mealplannerDb")`
- Update `MealPlanner.AppHost.csproj`: swap Mongo hosting packages for `Aspire.Hosting.PostgreSQL`
- Update `Directory.Packages.props`: add `Aspire.Hosting.PostgreSQL` version, keep Mongo versions temporarily (needed by migration project)

### Commit 2: `feat(api): add EF Core DbContext with entity configurations`
- Add `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` and `Microsoft.EntityFrameworkCore.Design` to `MealPlanner.Api.csproj`
- Add `Directory.Packages.props` entries for new EF packages
- Create `MealPlanner.Api/Data/MealPlannerDbContext.cs` with `DbSet<T>` for all 11 entities
- Create EF entity classes in `MealPlanner.Api/Data/Entities/` (one per table, replacing BSON document classes)
  - Use `[Column(TypeName = "jsonb")]` or Fluent API for jsonb columns
  - Configure unique indexes via `OnModelCreating` (matching current Mongo indexes)
- Register DbContext in `Program.cs` via `builder.AddNpgsqlDbContext<MealPlannerDbContext>("mealplannerDb")`
- Remove `builder.AddMongoDBClient("mealplannerDb")`

### Commit 3: `feat(api): create initial EF migration and apply at startup`
- Run `dotnet ef migrations add InitialCreate --project MealPlanner.Api --startup-project MealPlanner.Api`
- Add migration-apply call in `Program.cs` startup (before endpoint mapping): `db.Database.MigrateAsync()`
- Remove the three `EnsureXxxIndexesAsync()` calls and their extension classes

## Phase 2 — Refactor handlers by feature slice (commits 4–9)

Each commit converts one vertical slice from `IMongoClient` → `MealPlannerDbContext`. Pattern per handler:
- Replace `IMongoClient mongoClient` constructor param with `MealPlannerDbContext db`
- Replace Mongo collection queries with EF LINQ queries
- Replace `InsertOneAsync` / `ReplaceOneAsync` / `DeleteOneAsync` with `db.Xxx.Add()` / `db.SaveChangesAsync()`
- Update internal mappers (document → domain) to use new EF entities
- Keep domain records (`User`, `Recipe`, `MealPlan`, etc.) unchanged
- Catch `DbUpdateException` instead of `MongoWriteException`

### Commit 4: `refactor(users): migrate Users handlers to EF Core`
- `FindUserByAuth0Id`, `FindUserByEmail`, `UpsertUserFromAuth`, `UpdateCurrentUserName`
- `SendFriendRequestByEmail`, `AcceptFriendRequest`, `RejectFriendRequest`, `RemoveFriend`
- `UpdateFriendAutoSharePreferences`
- `GetFriendsForUser`, `GetIncomingFriendRequests`, `GetOutgoingFriendRequests`

### Commit 5: `refactor(recipes): migrate Recipes handlers to EF Core`
- `CreateRecipe`, `UpdateRecipe`, `GetAllRecipes`, `GetRecipeById`

### Commit 6: `refactor(meal-plans): migrate MealPlans handlers to EF Core`
- `GetMealPlan`, `UpdateDaySlot`, `CopyCategory`, `RemoveSlotItem`
- `ShareMealPlan`, `RevokeMealPlanShare`, `DismissSharedMealPlan`
- `GetSharedWithMe`, `GetSharesForMealPlan`

### Commit 7: `refactor(grocery-lists): migrate GroceryLists handlers to EF Core`
- `GetGroceryList`, `GenerateGroceryList`, `DeleteGroceryList`
- `ToggleGroceryListItem`, `AddCustomItem`, `PromotePantryStapleItem`
- `ShareGroceryList`, `RevokeGroceryListShare`, `DismissSharedGroceryList`
- `GetGroceryListsSharedWithMe`, `GetSharesForGroceryList`

### Commit 8: `refactor(integrations): migrate GoogleKeep handlers to EF Core`
- `CompleteGoogleKeepConnection`, `DisconnectGoogleKeepConnection`
- `ExportGroceryListToGoogleKeep`, `GetGoogleKeepConnectionStatus`

### Commit 9: `refactor(realtime): migrate realtime notifiers to EF Core`
- `GroceryListRealtimeNotifier` — replace Mongo share lookup with EF query
- `MealPlanRealtimeNotifier` — replace Mongo share lookup with EF query

## Phase 3 — Data migration job (commit 10)

### Commit 10: `feat(migration): add Aspire-orchestrated Mongo→Postgres data migration job`
- Create `MealPlanner.DataMigration/` console project
- Add to `MealPlanner.slnx`
- References both `Aspire.MongoDB.Driver.v3` and `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL`
- Reads all Mongo collections, maps ObjectId → new UUID, writes to Postgres via EF
- Maintains a `Dictionary<string, Guid>` mapping for cross-collection foreign key resolution (e.g. share documents referencing user IDs)
- Logs counts and validation summaries
- Register in `AppHost.cs` as `builder.AddProject<Projects.MealPlanner_DataMigration>("data-migration").WithReference(mongoDb).WithReference(mealplannerDb)`

## Phase 4 — Cleanup (commits 11–12)

### Commit 11: `chore: remove MongoDB packages and dead code`
- Delete all `*Document.cs` model files (11 files)
- Delete `MongoTestHelpers.cs`
- Delete index initialization extensions (3 files)
- Remove `MongoDB.Driver` / `Aspire.MongoDB.Driver.v3` from `MealPlanner.Api.csproj`
- Remove Mongo package versions from `Directory.Packages.props` (keep only in migration project)
- Remove any remaining `using MongoDB.*` statements

### Commit 12: `docs: update AGENTS.md and README for Postgres + EF Core`
- Update AGENTS.md persistence sections
- Document new `dotnet ef` migration workflow
- Update any references to Mongo collections/documents

## Phase 5 — Test updates (commit 13)

### Commit 13: `test: update API tests for EF Core persistence`
- Replace `MongoTestHelpers` with in-memory or mock `DbContext` patterns
- Update existing handler tests that mock `IMongoClient` → mock/stub `MealPlannerDbContext`
- Add tests for new EF entity mappings
- Run full test suite: `dotnet test --solution MealPlanner.slnx`

---

## Key decisions

| Decision | Choice | Rationale |
|---|---|---|
| ORM | EF Core (direct DbContext injection) | Matches current handler-owns-persistence pattern; less boilerplate than Dapper |
| Primary keys | `Guid` (UUID) | Idiomatic Postgres; migration job maps ObjectId→UUID |
| jsonb columns | Ingredients, days/slots, grocery items, scopes | Nested aggregates read/written as units; avoids unnecessary join tables |
| Migration strategy | Clean cutover | Simpler; migration job validates counts before Mongo removal |
| Data migration | Separate console project | Clean separation; Aspire orchestrates startup order |
| EF migrations | `dotnet ef` CLI + `MigrateAsync()` at startup | Requested by user; suitable for dev/early prod |
