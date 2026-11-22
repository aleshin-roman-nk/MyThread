# Repository Guidelines

## Project Structure & Module Organization

JChrono is an ASP.NET Core 10 and Angular 21 learning application. `src/JChrono.Domain` owns entities and business invariants. `src/JChrono.Application` contains use cases, DTOs, and repository contracts. `src/JChrono.Infrastructure` implements persistence using EF Core and SQLite. `src/JChrono.Web` hosts controllers, dependency injection, and compiled Angular assets.

Angular source lives in `client/src/app`; shared styles are in `client/src/styles.scss`. Integration tests live in `tests/JChrono.Tests`. Generated assets in `src/JChrono.Web/wwwroot`, build outputs, caches, and SQLite databases are ignored.

## Build, Test, and Development Commands

Run from `2026/T1000` in PowerShell:

- `build_server.cmd` (double-click): build only .NET without starting it; stop the server before building.
- `build_client.cmd` (double-click): build only Angular directly into `wwwroot` without restarting the server.
- `run.cmd` (double-click): start the compiled server in the background at `http://localhost:5080` without rebuilding; logs and PID are in `artifacts/server`. Repeated runs do not restart it.
- `stop.cmd` (double-click): stop the managed background server.
- `dotnet test tests/JChrono.Tests --no-restore`: run integration tests after restoring packages.
- `dotnet publish src/JChrono.Web --no-restore -c Release -o artifacts/publish`: publish including Angular after restoring packages (see README).
- `npm.cmd run watch` from `client`: rebuild frontend assets after changes.
- `npm.cmd start` from `client`: optional Angular development server with API proxying to port 5080.

See `README.md` for prerequisites, database configuration, and standalone test commands. Commit `client/package-lock.json` when dependencies change.

## Coding Style & Naming Conventions

Follow `.editorconfig`: four spaces for C#, two for TypeScript, templates, styles, and JSON; UTF-8 with LF endings. Use PascalCase for C# types and methods, camelCase for TypeScript members, and kebab-case Angular filenames. Components are standalone and use signals for view state.

Keep Domain independent of frameworks, Application independent of persistence and HTTP, and EF Core implementation in Infrastructure. Controllers delegate to application services. No formatter or linter is configured; .NET treats warnings as errors and Angular enables strict template checking.

## Testing Guidelines

Use xUnit and `WebApplicationFactory` with isolated temporary SQLite databases. Cover observable behavior, validation, filters, and persistence across requests. Name test methods after the behavior they verify. Run integration tests for backend changes and compile Angular for frontend changes. No coverage threshold is configured.

## Commit & Pull Request Guidelines

Parent repository history uses informal short subjects; prefer imperative, scoped subjects such as `Add subject note filtering`. Keep changes inside T1000. Describe behavior, validation performed, and relevant issues; include screenshots for UI changes. Never commit credentials, database files, or machine-specific configuration.
