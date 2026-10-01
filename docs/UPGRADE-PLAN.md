# Upgrade plan

## Current state

Score: 6/10 (was 4/10) — app now has the full MAUI layout and its shared XAML/C#
compiles (XamlC) against the core; platform heads are CI-built only, no persistence yet.

## Backlog

- P0: Confirm the new `android` CI job is green on GitHub (never run locally:
  this container has no Android SDK / MAUI workload). Fix whatever it reports.
- P1: Backend API client for shows and holds; server must stay the source of truth for availability.
- P1: Show a hold countdown and release the selection when it expires; persist confirmed bookings.
- P2: Build `net8.0-ios` on a macOS runner.
- P2: Add a solution file or `Directory.Build.props` to share analyzer settings
  between core and app.

## Done in this pass (pass 1)

- Added `src/TicketBooking.Core` with the app's domain logic and 7 xUnit tests.
- App project now references the core library and excludes `src/**`/`tests/**` from its globs.
- CI: removed the `|| true` steps that hid a non-compiling app; runs the core tests for real.
- README now states what is verified and what is not.

## Done in this pass (pass 2)

- Scaffolded the standard `dotnet new maui` (.NET 8) files wired to the core:
  `App.xaml`, `AppShell.xaml.cs`, `MauiProgram.cs`, `Platforms/Android`
  (`MainActivity`, `MainApplication`, manifest without INTERNET), `Platforms/iOS`
  (`AppDelegate`, `Program`, `Info.plist`), `Resources/` icon + splash SVGs.
- `RootNamespace` now matches the C# namespace; added `ApplicationId`, versions,
  `ImplicitUsings`/`Nullable`, and a unique `ApplicationIdGuid`.
- Replaced the missing `ExplorePage`/`ProfilePage` with a single Seats page (seat grid, quote, hold/confirm) using `TicketBooking.Core`.
- Fixed: the orphan-seat rule rejected *any* selection in a row that already had a stranded seat (e.g. after a hold expired); it now only rejects selections that create a new orphan.
- Fixed: `SeatId.Parse` accepted "A+1" / "A 1"; hold reference generator could overflow `int` and index out of range after ~271k holds (now a 32^6 bijection on `long`).
- 13 more edge-case tests (parse, map bounds, orphan rule both ways, 5000 unique references, promo/fees, single-use confirm): 20 total.
- `MauiSharedCheck=true` build switch: compiles the shared XAML/C# as `net8.0`
  without the workload (verified locally, Release/XamlC, 0 warnings).
- CI: new `app-shared` job (that check) and `android` job (`maui-android` workload build).

## Done in this pass (pass 3)

- Fixed `Price`: a seat listed twice was charged twice (HoldSeats rejected duplicates, but the quote shown before holding did not); now throws like `HoldSeats`.
- Promo codes ignore zero-width characters and a BOM (`"EARLYBIRD​"` from a paste silently got no discount; .NET `Trim()` keeps U+200B/U+FEFF).
- `Confirm` accepts the hold reference in lower case / padded, as users retype it.
- `BookingService` rejects a zero or negative hold duration (every hold expired instantly).
- 7 new test cases (26 total), verified with `dotnet test tests/TicketBooking.Core.Tests` and `dotnet build TicketBooking.csproj -c Release -p:MauiSharedCheck=true` (0 warnings).
