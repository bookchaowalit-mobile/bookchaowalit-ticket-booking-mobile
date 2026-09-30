# Upgrade plan

## Current state

Score: 4/10 (was 1/10) — tested .NET core library and honest CI; the MAUI app
shell is still incomplete and does not compile.

## Backlog

- P0: Make the MAUI app compile: add `App.xaml`, `AppShell.xaml.cs`,
  `MauiProgram.cs` (`CreateMauiApp`), `Platforms/Android` + `Platforms/iOS`,
  and either create `ExplorePage`/`ProfilePage` or drop them from `AppShell.xaml`.
  Align `RootNamespace` (`com.bookchaowalit.*`) with the `TicketBooking` C# namespace.
  Then add a CI job with `dotnet workload install maui-android` + `dotnet build -f net8.0-android`.
- P0: Replace the shared placeholder `ApplicationIdGuid` with a unique value per app.
- P1: Seat-map UI (grid of seats coloured by state/tier) bound to `SeatMap`.
- P1: Backend API client for shows and holds; server must stay the source of truth for availability.
- P2: Add a solution file under a subfolder or `Directory.Build.props` to share
  analyzer settings between core and app.

## Done in this pass

- Added `src/TicketBooking.Core` with the app's domain logic and 7 xUnit tests.
- App project now references the core library and excludes `src/**`/`tests/**` from its globs.
- CI: removed the `|| true` steps that hid a non-compiling app; runs the core tests for real.
- README now states what is verified and what is not.
