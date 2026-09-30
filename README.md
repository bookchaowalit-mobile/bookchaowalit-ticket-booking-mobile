# Ticket Booking — Mobile

.NET MAUI app for **Ticket Booking** — pick seats for a show and book them.

Part of [Chaowalit Greepoke](https://bookchaowalit.com)'s 101 Portfolio Projects.

## Status

- `src/TicketBooking.Core` — plain `net8.0` class library with the app's domain
  logic, covered by xUnit tests in `tests/TicketBooking.Core.Tests`. **This is the
  verified part of the repo** and what CI builds.
- `TicketBooking.csproj` + XAML at the repo root — the MAUI app shell. It references
  `TicketBooking.Core` but **does not compile yet**: `App.xaml`, `AppShell.xaml.cs`,
  `MauiProgram.cs`, the `Platforms/` folders and the `ExplorePage` /
  `ProfilePage` referenced from `AppShell.xaml` are missing. See
  [docs/UPGRADE-PLAN.md](docs/UPGRADE-PLAN.md).

## Core features (`TicketBooking.Core`)

- Seat ids (`C7`) with strict parsing; rectangular seat map with per-row tiers (Standard / Premium / VIP)
- "No orphan seat" rule: rejects selections that strand a single empty seat
- Time-limited seat holds, confirmation, and automatic release of expired holds
- Max 8 seats per booking, duplicate/unavailable/off-map seat checks
- Quotes: tier pricing, per-ticket fees, promo codes (discount never applies to fees), `decimal` money
- Short, unambiguous booking references (no 0/O/1/I)

## Tech Stack

- **Core:** C# / .NET 8 class library (nullable enabled, warnings as errors)
- **Tests:** xUnit
- **App (planned):** .NET MAUI 8 — Android, iOS

## Getting Started

```bash
dotnet test tests/TicketBooking.Core.Tests      # build core + run unit tests
```

The MAUI app additionally needs `dotnet workload install maui` plus the
Android SDK / Xcode once the missing app files are added.

## Related

- **Frontend:** [bookchaowalit-website/ticket-booking-frontend](https://github.com/bookchaowalit-website/bookchaowalit-ticket-booking-frontend)
- **Portfolio:** [bookchaowalit.com](https://bookchaowalit.com)

## License

MIT
