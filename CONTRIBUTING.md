# Contributing to Train Seat Reservation API

Thanks for your interest in contributing. This guide covers how to set up the project locally, the workflow we follow, and our expectations for pull requests.

## Getting started

### Prerequisites

- .NET 10 SDK
- Docker Desktop
- Git
- A dev tenant on Auth0 (see [README](./README.md#-auth0-setup))

### Setup

1. Fork the repository (external contributors) or clone it directly (maintainers).
2. Create a feature branch from `main` (see naming below).
3. Follow the [Quick start](./README.md#-quick-start-local-development) in the README to get the infrastructure running.
4. Run `dotnet test` to confirm your environment is working.

## Workflow

### Branch naming

Use the pattern `<type>/<short-description>`, kebab-case:

- `feat/add-seat-booking`
- `fix/deadlock-on-confirm`
- `refactor/extract-reservation-service`
- `test/add-integration-tests-for-cancel`
- `docs/clarify-concurrency-section`
- `chore/upgrade-mediatr`

If the work is tied to an issue, include the number: `feat/42-add-seat-booking`.

### Commit messages

We follow [Conventional Commits](https://www.conventionalcommits.org/).

```
<type>(<optional scope>): <short description>

<optional body — explain why, not what>

<optional footer — e.g. "Closes #42">
```

Common types: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`, `perf`, `build`, `ci`.

Examples:

- `feat(reservations): add idempotency key support`
- `fix(seats): handle deadlock on concurrent booking`
- `refactor: extract seat locking into repository`
- `docs(readme): update Auth0 setup steps`

Rules of thumb:

- Subject under 72 characters.
- Imperative mood ("add", not "added" or "adds").
- Describe *why* in the body if it's not obvious from the diff.

### Pull Request process

1. Push your branch and open a Pull Request against `main`.
2. Fill out the PR description:
   - **What changed** — short summary.
   - **Why** — motivation, link to the issue if any.
   - **How to test** — steps a reviewer can run locally.
3. Make sure CI is green: build, format check, all tests passing.
4. Request a review from a maintainer.
5. Address feedback. Force-push to your branch is acceptable; do **not** force-push to `main`.
6. Once approved and CI is green, the PR is merged via **Squash and merge**. The squash commit message should follow Conventional Commits — usually the PR title is enough.

### Keep PRs focused

A PR should do one thing. If you find yourself writing "this PR does X and also Y" in the description — split it. Small PRs are easier to review, easier to revert, and easier to reason about in history.

## Code style

- The project uses `.editorconfig` to enforce formatting. IDEs with C# support (Rider, Visual Studio, VS Code with C# Dev Kit) pick it up automatically.
- Run `dotnet format` before pushing. CI enforces this via `dotnet format --verify-no-changes` and will fail the build if the tree is not formatted.
- Nullable reference types are enabled project-wide. Do not suppress nullable warnings without justification in a comment.
- Naming:
  - `PascalCase` for types, public members, constants.
  - `camelCase` for locals and parameters.
  - `_camelCase` for private fields.
  - `I` prefix for interfaces (`IReservationRepository`).
- Prefer expression-bodied members and pattern matching where they improve readability.
- Keep methods short. If a handler grows beyond ~30 lines, it's probably doing too much.

## Testing

Every change that touches business logic or infrastructure needs tests:

- **Unit tests** — for domain logic and handlers. Fast, no infrastructure.
- **Integration tests** — for anything that touches the database, cache, or HTTP. Uses Testcontainers to spin up real SQL Server and Redis.
- **Architecture tests** — if you add a new layer or change a convention, update the `NetArchTest` rules.

Run the full suite locally before pushing:

```bash
dotnet test
```

All tests must pass in CI for a PR to be merged.

## Questions?

Open an [issue](../../issues) with the `question` label.