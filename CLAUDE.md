# MealPlanner agent instructions

This repository is set up to use Aspire. Aspire is an orchestrator for the entire application and will take care of configuring dependencies, building, and running the application. The resources that make up the application are defined in `apphost.cs` including application code and external dependencies.

## General recommendations for working with Aspire

1. Before making any changes always run the apphost using `aspire run` and inspect the state of resources to make sure you are building from a known state.
1. Changes to the _apphost.cs_ file will require a restart of the application to take effect.
1. Make changes incrementally and run the aspire application using the `aspire run` command to validate changes.
1. Use the Aspire MCP tools to check the status of resources and debug issues.

## Running the application

To run the application run the following command:

```
aspire run
```

If there is already an instance of the application running it will prompt to stop the existing instance. You only need to restart the application if code in `apphost.cs` is changed, but if you experience problems it can be useful to reset everything to the starting state.

## Checking resources

To check the status of resources defined in the app model use the _list resources_ tool. This will show you the current state of each resource and if there are any issues. If a resource is not running as expected you can use the _execute resource command_ tool to restart it or perform other actions.

## Listing integrations

IMPORTANT! When a user asks you to add a resource to the app model you should first use the _list integrations_ tool to get a list of the current versions of all the available integrations. You should try to use the version of the integration which aligns with the version of the Aspire.AppHost.Sdk. Some integration versions may have a preview suffix. Once you have identified the correct integration you should always use the _get integration docs_ tool to fetch the latest documentation for the integration and follow the links to get additional guidance.

## Debugging issues

IMPORTANT! Aspire is designed to capture rich logs and telemetry for all resources defined in the app model. Use the following diagnostic tools when debugging issues with the application before making changes to make sure you are focusing on the right things.

1. _list structured logs_; use this tool to get details about structured logs.
2. _list console logs_; use this tool to get details about console logs.
3. _list traces_; use this tool to get details about traces.
4. _list trace structured logs_; use this tool to get logs related to a trace

## Other Aspire MCP tools

1. _select apphost_; use this tool if working with multiple app hosts within a workspace.
2. _list apphosts_; use this tool to get details about active app hosts.

## Playwright MCP server

The playwright MCP server has also been configured in this repository and you should use it to perform functional investigations of the resources defined in the app model as you work on the codebase. To get endpoints that can be used for navigation using the playwright MCP server use the list resources tool.

## Updating the app host

The user may request that you update the Aspire apphost. You can do this using the `aspire update` command. This will update the apphost to the latest version and some of the Aspire specific packages in referenced projects, however you may need to manually update other packages in the solution to ensure compatibility. You can consider using the `dotnet-outdated` with the users consent. To install the `dotnet-outdated` tool use the following command:

```
dotnet tool install --global dotnet-outdated-tool
```

## Persistent containers

IMPORTANT! Consider avoiding persistent containers early during development to avoid creating state management issues when restarting the app.

## Aspire workload

IMPORTANT! The aspire workload is obsolete. You should never attempt to install or use the Aspire workload.

## Official documentation

IMPORTANT! Always prefer official documentation when available. The following sites contain the official documentation for Aspire and related components

1. https://aspire.dev
2. https://learn.microsoft.com/dotnet/aspire
3. https://nuget.org (for specific integration package details)

## NuGet package management (CPM)

This repository uses .NET Central Package Management (CPM) with a repo-level `Directory.Packages.props`.

1. Use the .NET CLI to add package references to a specific project:

```
dotnet add <path-to-project.csproj> package <PackageId>
```

Optional explicit version:

```
dotnet add <path-to-project.csproj> package <PackageId> --version <x.y.z>
```

2. Keep package versions in `Directory.Packages.props` using `<PackageVersion />` entries.
3. Do not keep `Version="..."` on `PackageReference` items in project files when the package is centrally managed.
4. After dependency changes, validate with:

```
dotnet restore MealPlanner.slnx
dotnet build MealPlanner.slnx -c Release --no-restore
dotnet test --solution MealPlanner.slnx -c Release --no-build
```

---

# API Architecture Patterns

This section describes the architectural patterns and conventions used in the MealPlanner.Api project.

## CQRS Pattern with Vertical Slices

The API uses CQRS (Command Query Responsibility Segregation) to separate read operations (queries) from write operations (commands). All domain logic is organized using vertical slices within the `Features` folder.

### Organization

- **Features folder**: Contains vertical slices organized by domain object (e.g., `Features/Meals`, `Features/Users`)
- Each feature contains its queries and commands specific to that domain object
- Each query/command has its own handler class

### Core Interfaces

All domain actions are encapsulated using these interfaces:

- **`IQuery<TResult>`**: Marker interface for read operations
- **`IQueryHandler<TQuery, TResult>`**: Handles a query and returns `Task<Result<TResult>>`
- **`ICommand<TResult>`**: Marker interface for write operations
- **`ICommandHandler<TCommand, TResult>`**: Handles a command and returns `Task<Result<TResult>>`

### Handler Requirements

1. Every handler MUST return `Result<T>` (not `Result<T, Error>` - the Error type is built into Result)
2. Handlers enable chaining operations using railway-oriented programming patterns
3. Place handlers in the appropriate domain folder under `Features/`

**Example structure:**

```
Features/
  Meals/
    Queries/
      GetMeal.cs              // Contains query record and handler
      GetAllMeals.cs
    Commands/
      CreateMeal.cs           // Contains command record and handler
      UpdateMeal.cs
      DeleteMeal.cs
```

## Functional Programming Patterns

The API follows functional programming paradigms using the following core types:

### Result<T>

Represents the outcome of an operation for railway-oriented programming:

- `Result<T>.Success(value)` - successful operation with a value
- `Result<T>.Failure(error)` - failed operation with an error
- Methods: `Bind()`, `Map()`, `Match()`, `ToUnit()`

**Usage pattern:**

```csharp
public async Task<Result<MealDto>> HandleAsync(GetMealQuery query, CancellationToken ct)
{
    var meal = await _db.Meals.FindAsync(query.Id, ct);
    if (meal is null)
        return Result<MealDto>.Failure(new Error(ErrorCodes.NotFound, "Meal not found"));

    return Result<MealDto>.Success(MapToDto(meal));
}
```

### Option<T>

Represents optional values, replacing nullable types in domain code:

- `Option<T>.Some(value)` - value is present
- `Option<T>.None()` - value is absent
- `Option<T>.From(nullableValue)` - converts from nullable
- Methods: `Map()`, `Bind()`, `Match()`, `GetValueOrDefault()`, `GetValueOrNull()`

**When to use:**

- Domain records MUST use `Option<T>` instead of nullable types
- DTOs and database entities MAY use nullable types
- Use `Option<T>` to make optionality explicit in the domain model

### Error

Represents failures with context:

- Constructor: `new Error(code, message, exception?)`
- Standard error codes defined in `ErrorCodes` class
- Common codes: `NotFound`, `ValidationFailed`, `Unauthorized`, `DatabaseError`

## Domain Modeling Rules

### Immutability

- Domain objects MUST be immutable C# records
- Use `record` keyword with init-only properties or positional parameters
- Updates create new instances rather than mutating existing ones

**Example:**

```csharp
public record Meal(
    Guid Id,
    string Name,
    string Description,
    Option<DateTime> ScheduledFor,
    List<Ingredient> Ingredients
);
```

### Null Handling

1. **Domain Records**: NEVER use nullable types (`?`). Always use `Option<T>` for optional values
2. **DTOs**: Nullable types are acceptable for API request/response objects
3. **Database Entities**: Nullable types are acceptable for EF Core entities

**Example:**

```csharp
// ❌ WRONG for domain
public record Meal(Guid Id, string Name, DateTime? ScheduledFor);

// ✅ CORRECT for domain
public record Meal(Guid Id, string Name, Option<DateTime> ScheduledFor);

// ✅ ACCEPTABLE for DTO
public class MealDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime? ScheduledFor { get; set; }
}
```

### Type Philosophy

- Domain types express business rules and invariants
- DTOs are data transfer objects for API boundaries
- Database entities match persistence requirements
- Keep clear boundaries between these layers

## Railway-Oriented Programming

Chain operations using `Result<T>` methods to handle success/failure paths:

```csharp
return await ValidateCommand(command)
    .Bind(async cmd => await SaveToDatabase(cmd))
    .Map(entity => MapToDto(entity));
```

This pattern ensures errors short-circuit the chain while success flows through transformations.

## API Unit Testing (xUnit v3)

The API test project is `MealPlanner.Api.Tests` and uses xUnit v3.

### Scope and Isolation

- API tests MUST be unit tests (no real databases, no containers, no network calls).
- Handlers and mappers should be tested with mocked/faked dependencies.
- Tests SHOULD be deterministic (fixed dates, stable inputs, no reliance on local machine state); when production code uses `DateTime.UtcNow`, use clock-tolerant assertions instead of exact timestamp equality.

### Runner and Execution

- xUnit v3 is the required framework for API tests.
- Prefer Microsoft Testing Platform (MTP) mode for xUnit v3 test execution.
- Keep `dotnet test` as the standard command surface for local runs and CI.
- If test-runner configuration is changed, keep it consistent across the whole API test project.

### Required Test Coverage

1. **All Query Handlers** must have unit tests.
2. **All Command Handlers** must have unit tests.
3. **All mappers** must have unit tests, including:
   - DTO ↔ domain mapping methods
   - helper mapper methods
   - internal static mapper methods inside handlers

### Internal Mapper Testing

- Direct mapper tests are required for internal mapper methods.
- Use `InternalsVisibleTo` so `MealPlanner.Api.Tests` can validate internal mapping logic directly.
- Do not rely only on handler-level assertions when direct mapper methods exist.

### Test Organization

- Organize tests to mirror API vertical slices in `MealPlanner.Api/Features`.
- Use a mostly mirrored structure under `MealPlanner.Api.Tests` with minor flattening allowed when it improves readability.
- Keep Commands and Queries separated in tests the same way as API source.

**Preferred pattern:**

```
MealPlanner.Api/
  Features/
    Recipes/
      Queries/
        GetRecipeById.cs
      Commands/
        CreateRecipe.cs
      Dtos/
        RecipeDtos.cs

MealPlanner.Api.Tests/
  Features/
    Recipes/
      Queries/
        GetRecipeByIdTests.cs
      Commands/
        CreateRecipeTests.cs
      Dtos/
        RecipeDtosTests.cs
```

### Minimum Test Matrix per Handler

- Happy path success result.
- Validation failure path (when applicable).
- Not found / unauthorized path (when applicable).
- Exception-to-error mapping path (for try/catch handlers).

### Minimum Test Matrix per Mapper

- Correct field mapping in both directions (when applicable).
- Null/empty/optional value handling (`Option<T>` boundaries).
- Date and normalization edge cases where mapping contains date logic.

### Naming and Style

- Name test files by source file intent, ending with `Tests`.
- Use clear scenario names (for example: `HandleAsync_ReturnsFailure_WhenEntityMissing`).
- Follow Arrange/Act/Assert structure for readability.
- Keep each test focused on one behavior.

---

# Authentication (Better Auth)

The application uses [Better Auth](https://www.better-auth.com/) as its
identity provider. Better Auth is embedded inside the SvelteKit frontend
and issues RS256-signed JWTs to the .NET API via its JWT plugin. See
[`docs/auth-rbac.md`](./docs/auth-rbac.md) for the full RBAC walkthrough
and migration notes.

## Architecture

- **Frontend** hosts Better Auth directly at `/api/auth/*` through a
  SvelteKit catch-all endpoint (`src/routes/api/auth/[...all]/+server.ts`).
  Sign-in and sign-up flows use the `authClient` helper from
  `$lib/auth/client`.
- **API** validates JWT bearer tokens with ASP.NET Core `AddJwtBearer`,
  pointing at Better Auth's JWKS document via the custom
  `BetterAuthJwksConfigurationRetriever`.

## API Authentication

The API authentication setup in `MealPlanner.Api/Program.cs` uses:

- `options.Authority = Authentication:Authority` (Better Auth base URL)
- `options.Audience = Authentication:Audience` (defaults to `mealplanner-api`)
- A custom `ConfigurationManager` that fetches `{Authority}/api/auth/jwks`
  because Better Auth does not ship a full OIDC discovery document
- `options.RequireHttpsMetadata = false` in development

Protected endpoints opt in via
`.RequireAuthorization(RbacAuthorization.RequireUserRolePolicy)` or
`RequireAdminRolePolicy`. `RbacAuthorization.ExtractRoles` reads the
native Better Auth `role` claim as well as the legacy namespaced
`https://mealplanner/roles` claim for backwards compatibility.

## Frontend Authentication

Better Auth's SvelteKit integration is wired via `src/hooks.server.ts`:

- **`src/lib/server/auth.ts`** — SvelteKit-specific Better Auth instance
  (includes the `sveltekitCookies` plugin)
- **`src/lib/server/auth-options.ts`** — framework-agnostic Better Auth
  configuration so migration scripts can reuse the same plugins
- **`src/lib/server/session.ts`** — resolves the request into the
  `AppSession` shape (user, roles, access token) consumed by pages
- **`src/routes/api/auth/[...all]/+server.ts`** — delegates all HTTP
  traffic under `/api/auth` to Better Auth
- **`src/lib/auth/client.ts`** — browser client built with
  `createAuthClient` and the admin / additional-fields plugins

### Environment Variables (frontend `.env`)

- `BETTER_AUTH_SECRET` — cookie/token signing secret (required)
- `BETTER_AUTH_URL` — base URL the frontend is served from
- `BETTER_AUTH_JWT_AUDIENCE` — API audience claim (default
  `mealplanner-api`)
- `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET` — optional, enables the
  "Continue with Google" social login button

### Auth UI

- The `Navbar` component receives an `AppSession | null` prop and shows
  Log In / Sign Up / Log Out buttons accordingly
- Log In navigates to `/auth/signin`, Sign Up navigates to `/auth/signup`,
  and Log Out calls `authClient.signOut()` from `$lib/auth/client`

---

# Frontend Development (SvelteKit)

This section describes the patterns and conventions for the `frontend` project.

## Technology Stack

- **Framework**: Svelte 5 (Runes mode) + SvelteKit
- **Styling**: Tailwind CSS v4
- **Language**: TypeScript
- **Package manager**: pnpm (do NOT use npm or yarn)
- **Testing**: Vitest (Unit & Browser Mode)

## Core Principles

### 1. Svelte 5 Runes

You MUST use Svelte 5 Runes for all component logic. Do NOT use legacy Svelte 4 APIs (no `export let` for props, no `$: ` for reactivity).

- **State**: Use `$state(initialValue)`
- **Derived State**: Use `$derived(expression)`
- **Side Effects**: Use `$effect(() => { ... })`
- **Props**: Use `let { propName = defaultValue }: { propName: Type } = $props()` or simple `let { propName } = $props()`
- **Event Handling**: Use standard HTML attributes (e.g., `onclick`, `onsubmit`) instead of `on:click`.

### 2. Styling with Tailwind CSS v4

- Use Tailwind CSS utility classes for styling.
- Do not create separate CSS files unless absolutely necessary.
- Configuration is handled in `vite.config.ts` (using `@tailwindcss/vite`).

### 3. Project Structure

- **`src/routes`**: Contains the file-based routing.
  - `+page.svelte`: The page component.
  - `+page.ts` / `+page.server.ts`: Data loading.
  - `+layout.svelte`: Layout components.
- **`src/lib`**: Contains reusable components and utility functions.
  - Use the `$lib` alias to import from this directory.

### 4. Testing and Validation

- **Unit & Browser Tests**: Tests are configured in `vite.config.ts` using Vitest with the Playwright browser provider.
- Use pnpm, matching CI (`.github/workflows/pr-build-test.yml`). From the `frontend/` directory:
  - `pnpm run check` — type-check and svelte-check
  - `pnpm run build` — production build
  - `pnpm run test` — run all tests
- Place test files (`*.test.ts` or `*.spec.ts`) alongside the source files they test.

---

# Git Workflow

This repository follows a strict Git workflow to ensure code quality and traceability.

## 1. Feature Branching

- **Always** create a new branch for each implementation plan or task.
- Branch names should be descriptive (e.g., `feature/add-login`, `bugfix/fix-header-layout`).
- Do not commit directly to `main` (or `master`) unless it's a trivial documentation fix.

## 2. Atomic Commits

- **Commit often**. Ideally, commit after each step in your implementation plan.
- Commits should represent a single logical change or a completed step.
- Use `git add .` carefully; prefer staging specific files related to the change.

## 3. Commit Messages

- Use Conventional Commits format: `<type>(<scope>): <subject>`
  - `feat`: A new feature
  - `fix`: A bug fix
  - `docs`: Documentation only changes
  - `style`: Changes that do not affect the meaning of the code (white-space, formatting, etc)
  - `refactor`: A code change that neither fixes a bug nor adds a feature
  - `perf`: A code change that improves performance
  - `test`: Adding missing tests or correcting existing tests
  - `chore`: Changes to the build process or auxiliary tools and libraries such as documentation generation
- Example: `feat(auth): add login page component`

---

# Svelte MCP server

A Svelte MCP server is configured in `.mcp.json` with access to Svelte 5 and SvelteKit documentation. When working on non-trivial Svelte code: use _list-sections_ first to discover relevant documentation, fetch it with _get-documentation_, and run generated Svelte code through _svelte-autofixer_ before finalizing it.
