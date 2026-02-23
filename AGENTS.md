# Copilot instructions

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

---

# Authentication (Auth0)

The application uses Auth0 as the identity provider.

## Architecture

- **Frontend** handles OIDC login/logout server-side using `@auth/sveltekit` with the Auth0 provider.
- **API** validates JWT bearer tokens with ASP.NET Core `AddJwtBearer` using `Authentication:Authority` and `Authentication:Audience` configuration.

## API Authentication

The API authentication setup in `MealPlanner.Api/Program.cs` uses:

- `AddJwtBearer` with `options.Authority = Authentication:Authority`
- `options.Audience = Authentication:Audience`
- `options.TokenValidationParameters.ValidateAudience = true`
- `options.RequireHttpsMetadata = false` in development

Protected endpoints use `.RequireAuthorization()` on the route group.

## Frontend Authentication

Auth.js (`@auth/sveltekit`) handles OIDC flows:

- **`src/auth.ts`**: Configures `SvelteKitAuth` with the Auth0 provider and JWT callbacks that persist the access token in the session
- **`src/hooks.server.ts`**: Re-exports the Auth.js handle function
- **`src/routes/+layout.server.ts`**: Loads the session and passes it to all pages
- **`src/app.d.ts`**: Extends Auth.js types with `accessToken` on the session

### Environment Variables (frontend `.env`)

- `AUTH_SECRET` — Cookie signing secret (required, change in production)
- `AUTH_AUTH0_ID` — Auth0 client ID
- `AUTH_AUTH0_SECRET` — Auth0 client secret
- `AUTH_AUTH0_ISSUER` — Auth0 issuer URL
- `AUTH_API_AUDIENCE` — API audience passed during Auth0 authorization

### Auth UI

- The `Navbar` component accepts a `session` prop and shows Login/Logout buttons accordingly
- Login triggers `signIn('auth0')`, Logout triggers `signOut()` from `@auth/sveltekit/client`

---

# Frontend Development (SvelteKit)

This section describes the patterns and conventions for the `frontend` project.

## Technology Stack

- **Framework**: Svelte 5 (Runes mode) + SvelteKit
- **Styling**: Tailwind CSS v4
- **Language**: TypeScript
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

### 4. Testing

- **Unit & Browser Tests**: Tests are configured in `vite.config.ts` using Vitest with the Playwright browser provider.
- Run `npm run test` to execute all tests.
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

You are able to use the Svelte MCP server, where you have access to comprehensive Svelte 5 and SvelteKit documentation. Here's how to use the available tools effectively:

## Available MCP Tools:

### 1. list-sections

Use this FIRST to discover all available documentation sections. Returns a structured list with titles, use_cases, and paths.
When asked about Svelte or SvelteKit topics, ALWAYS use this tool at the start of the chat to find relevant sections.

### 2. get-documentation

Retrieves full documentation content for specific sections. Accepts single or multiple sections.
After calling the list-sections tool, you MUST analyze the returned documentation sections (especially the use_cases field) and then use the get-documentation tool to fetch ALL documentation sections that are relevant for the user's task.

### 3. svelte-autofixer

Analyzes Svelte code and returns issues and suggestions.
You MUST use this tool whenever writing Svelte code before sending it to the user. Keep calling it until no issues or suggestions are returned.

### 4. playground-link

Generates a Svelte Playground link with the provided code.
After completing the code, ask the user if they want a playground link. Only call this tool after user confirmation and NEVER if code was written to files in their project.
