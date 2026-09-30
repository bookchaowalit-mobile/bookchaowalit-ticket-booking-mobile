# Ticket Booking — Mobile

.NET MAUI app for **Ticket Booking** — pick seats for a show and book them.

Part of [Chaowalit Greepoke](https://bookchaowalit.com)'s 101 Portfolio Projects.

## Status

- `src/TicketBooking.Core` — plain `net8.0` class library with the app's domain
  logic, covered by xUnit tests in `tests/TicketBooking.Core.Tests` (20 tests).
- `TicketBooking.csproj` + XAML at the repo root — the .NET MAUI 8 app in the standard
  `dotnet new maui` layout (`App.xaml`, `AppShell.xaml`, `MauiProgram.cs`,
  `Platforms/Android`, `Platforms/iOS`, `Resources/`). One page, **Seats**: tappable 8×10 seat map coloured by tier/state, live quote with promo code, hold (10 min) and confirm via `BookingService`.
  State is in memory for now (see [docs/UPGRADE-PLAN.md](docs/UPGRADE-PLAN.md)).
- Verified locally without the MAUI workload: the shared XAML/C# compiles
  against `Microsoft.Maui.Controls` 8 with XamlC (`-p:MauiSharedCheck=true`,
  which builds a plain `net8.0` target and skips `Platforms/`). The Android/iOS
  heads are built by CI (`android` job) only; they have not been built locally.

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
- **App:** .NET MAUI 8 — Android, iOS

## Getting Started

```bash
dotnet test tests/TicketBooking.Core.Tests                                    # core + unit tests
dotnet build TicketBooking.csproj -c Release -p:MauiSharedCheck=true          # app XAML/C# check, no workload needed
dotnet workload install maui-android && dotnet build TicketBooking.csproj -f net8.0-android   # Android app (needs Android SDK + JDK 17)
```

On Linux only the Android target is enabled; macOS/Windows also get `net8.0-ios`.

## Related

- **Frontend:** [bookchaowalit-website/ticket-booking-frontend](https://github.com/bookchaowalit-website/bookchaowalit-ticket-booking-frontend)
- **Portfolio:** [bookchaowalit.com](https://bookchaowalit.com)

## License

MIT
