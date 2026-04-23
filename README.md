# 🚂 Train Seat Reservation API

Welcome to the **Train Seat Reservation API**!

This is a learning project that demonstrates modern approaches to building backend applications on the .NET platform. The goal is to deliver a reliable, testable, and easily scalable train seat reservation service using the principles of **Clean Architecture** and **Domain-Driven Design (DDD)**.

The project is a good fit for studying design patterns, CQRS, domain events, concurrent access, and microservice infrastructure.

## 🎯 About the project and business rules (Core Domain)

The problem domain is focused on the reservation process. The system has three main Aggregates:

* **Trip** — a specific train run (route, date, departure time, list of cars).
* **Seat** — a seat in a car (number, class, status: `Available` / `Reserved` / `Sold`).
* **Reservation** — the booking entity (passenger linked to seats).

**Key business rules:**

* **Idempotency and concurrency:** A seat cannot be booked if its status is already `Reserved` or `Sold`. Implementation details are covered in the *🔒 Concurrency strategy* section.
* **Reservation TTL:** A reservation lives for exactly 15 minutes. If its status has not moved to `Confirmed`, it is cancelled automatically by a background job, and the seats' status flips back to `Available` via a Domain Event.
* **Limits:** A single passenger can book at most 4 seats on one trip.
* **Time validation:** Seats cannot be booked on a trip that has already departed.
* **Pricing:** The seat class (the `SeatClass` value object) encapsulates the business logic for final price calculation.

## 🛠 Tech stack and patterns

### Architecture
* **Clean Architecture:** Strict separation into layers. The Domain has no external dependencies.
* **CQRS via MediatR:** Commands are isolated from queries. Repetitive infrastructure plumbing (UnitOfWork, uniform handling of domain exceptions) is extracted into base classes `CommandHandler<T>` and `QueryHandler<T>`, so concrete handlers contain only pure business logic. An alternative approach using Pipeline Behaviors is discussed in the backlog.
* **Domain Events:** Events (such as `SeatReservedEvent`, `ReservationExpiredEvent`) are dispatched by overriding `SaveChangesAsync` in EF Core, with an adapter to `MediatR`.
* **Result pattern:** No exceptions for business logic. The domain uses `Result<T>`, which is mapped to HTTP codes at the API layer through extension methods.

### Infrastructure
* **Database:** Microsoft SQL Server 2022, with migrations extracted into a separate console project.
* **Caching:** Redis for frequent queries (for example, `GetTripSeatsQuery`). The cache is invalidated by domain events when seat state changes.
* **Background jobs:** An `IHostedService` that automatically checks for expired reservations every minute.
* **Authentication:** Auth0 (cloud-hosted JWT validation via JWKS).
* **Logging:** Serilog (structured logging).

## 🔒 Concurrency strategy

Seat booking is the central contention point in the system: multiple users click on the same seats of a popular trip at the same time. For that load profile, we use **pessimistic row-level locking** in SQL Server:

* **Lock hints:** `WITH (UPDLOCK, ROWLOCK, HOLDLOCK)` on the `SELECT` inside the reservation transaction.
    * `UPDLOCK` — an intent-update lock: it blocks anyone else who wants to modify the row, while still allowing plain SELECTs.
    * `ROWLOCK` — forces row-level locking instead of page/table level.
    * `HOLDLOCK` — holds the lock until the transaction ends (the equivalent of SERIALIZABLE range locks, but scoped to the selected rows).
* **Isolation level:** `ReadCommitted` (the default) together with the hints above. We do not raise the global isolation level to SERIALIZABLE, to avoid blocking anything unnecessary.
* **Deterministic lock order:** seats are locked with `ORDER BY SeatId` — this is critical to prevent deadlocks when two requests compete for overlapping sets of seats (recall the 4-seat limit per reservation).
* **Deadlock policy:** we catch SQL Server error 1205 and retry up to 3 times with exponential backoff via Polly. If the conflict persists, the client gets a 409 Conflict.
* **Lock timeout:** `SET LOCK_TIMEOUT 3000` at the command level, so a connection is not held indefinitely.

This approach gives strict correctness under the moderate contention expected at the level of individual trips. Horizontal scaling is possible through sharding by `TripId` if the need arises.

## 📁 Solution structure

The project is split into logical modules to keep coupling loose:

```text
📦 TrainBooking.sln
 ┣ 📂 TrainBooking.Domain         # Entities, Value Objects, IDomainEvent, Domain Exceptions/Results
 ┣ 📂 TrainBooking.Application    # Commands, Queries, Handlers, FluentValidators, IRepository interfaces
 ┣ 📂 TrainBooking.Infrastructure # EF Core DataContext, Redis integration, MediatR adapters, Serilog
 ┣ 📂 TrainBooking.Api            # Controllers, Global Exception Middleware, Result -> HTTP extensions
 ┣ 📂 TrainBooking.Migrations     # EF Core Migrations runner (Console App)
 ┗ 📂 TrainBooking.Tests          # Architecture Tests (NetArchTest), Unit Tests, Integration Tests (Testcontainers)
```

## 🚧 Project scope (MVP boundaries)

The scope is deliberately fixed. We are not adding payment gateways, user registration, or email notifications at this stage. All development revolves around 5 endpoints and 1 background job:

**Write (Commands):**
* `POST /reservations` — create a new reservation.
* `PUT /reservations/{id}/confirm` — confirm payment/reservation.
* `PUT /reservations/{id}/cancel` — cancel a reservation on the user's behalf.

**Read (Queries):**
* `GET /trips/{id}/seats` — get the list of available seats (cached in Redis).
* `GET /reservations/{id}` — get the status of a specific reservation.

**Background:**
* A job that checks for and expires reservations (TTL > 15 min).

*Ideas for extending the functionality are welcome, but they first go into the backlog so they do not block the MVP release.*

## 🐳 Quick start (local development)

The project is fully containerized. Infrastructure is brought up via Docker Compose.

**Requirements**
* .NET 10 SDK
* Docker Desktop
* A dev tenant on Auth0 (free up to 25k MAU)

**Steps**
1. Clone the repository.
2. Configure Auth0 (see *🔑 Auth0 setup*).
3. In the root directory, run: `docker-compose up -d` (this starts SQL Server and Redis).
4. Run the `TrainBooking.Migrations` project to apply the database schema.
5. Run `TrainBooking.Api`. The Scalar UI will be available at `https://localhost:5001/scalar`.

## 🔑 Auth0 setup

1. Create a dev tenant on Auth0.
2. Under **Applications → Applications**, create an Application of type *Regular Web Application* (or *Machine-to-Machine* for service-to-service tests).
3. Under **Applications → APIs**, create an API with an identifier (audience), for example `https://train-booking-api`. In Permissions, define the scopes: `reservations:read`, `reservations:write`.
4. In `appsettings.Development.json`, configure:
   ```json
   "Auth0": {
     "Domain": "your-tenant.eu.auth0.com",
     "Audience": "https://train-booking-api"
   }
   ```
* The JWT Bearer pipeline pulls the JWKS automatically from `https://{Domain}/.well-known/openid-configuration` — signature validation does not require manual key setup.
* Scopes from the token are mapped to policies via `[Authorize(Policy = "reservations:write")]`.

## 🧪 Testing

* **Unit tests:** pure tests of the domain and handlers, with no infrastructure. `xUnit` + `Shouldly`.
* **Architecture tests:** `NetArchTest` — verifies that Domain does not reference Infrastructure, that every handler inherits from the base class, and so on.
* **Integration tests:** `WebApplicationFactory` + `Testcontainers` (`Testcontainers.MsSql`, `Testcontainers.Redis`) — a real SQL Server and Redis are spun up in Docker on every run. No in-memory database substitutes.

*Before submitting a Pull Request, please make sure all tests (Unit, Integration, Architecture) pass.*

## 🗂 Conscious trade-offs and backlog

This section honestly documents the MVP's compromises. Every item here is a deliberate decision, not an oversight.

1.  **Domain events are dispatched inside SaveChangesAsync, without an Outbox.** If a handler fails after the commit, the event is lost (for example, Redis invalidation does not happen → stale cache until the TTL expires). For production-grade reliability, moving to a Transactional Outbox + a dedicated worker is on the backlog.
2.  **The Redis cache is eventually consistent.** The baseline defense is a short TTL (60 sec) as a fallback in case event-driven invalidation does not fire.
3.  **Idempotency for POST /reservations via an Idempotency-Key header** (+ Redis with a 24-hour TTL) — not in the MVP, but on the backlog. Without it, client retries on a network timeout will produce duplicate reservations.
4.  **Base handler classes vs. Pipeline Behaviors.** The current approach with `CommandHandler<T>` / `QueryHandler<T>` reduces boilerplate through inheritance. The alternative — a set of `IPipelineBehavior<TRequest, TResponse>` implementations (Logging / Validation / Transaction) — is the more idiomatic MediatR path and gives better composition of cross-cutting behavior. A likely refactor once the domain stabilizes.
5.  **The retry policy is limited to deadlocks (error 1205).** Other transient SQL Server failures are not retried automatically — for production this should be extended via Polly combined with the `Microsoft.Data.SqlClient` transient error detector.

This is a learning project that evolves iteratively. Its goal is to reinforce the patterns of Clean Architecture, DDD, CQRS, domain events, and concurrent access in SQL Server.
