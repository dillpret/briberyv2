# Bribery

Bribery is a party game inspired by Jackbox-style experiences where players compete to win the favour of their friends with the most irresistible "bribes". Each round flows through lobby setup, personalised prompt selection, bribe submissions (text or image/GIF links), voting, and a results scoreboard that tallies scores across all rounds. The game supports reconnecting players, configurable timers, host moderation (kicking and settings updates), and whimsical auto-generated prompts and filler bribes when players run out of time.

This repository contains a full-stack implementation built with an ASP.NET Core minimal API backend and an Angular 17 frontend. The backend encapsulates all domain rules inside a `GameService` with comprehensive unit tests, and the frontend communicates through a typed API layer while maintaining client state in a store service.

## Repository layout

```
.
├── Bribery.sln                 # .NET solution containing API, domain, and tests
├── src/Bribery.Domain          # Core game rules and models
├── src/Bribery.Api             # ASP.NET Core minimal API + background ticker
├── tests/Bribery.Domain.Tests  # xUnit test suite for the domain service
└── frontend/bribery-client     # Angular SPA client (standalone components)
```

Key backend components:

- `GameService` orchestrates lobby creation, round lifecycles, pairing logic, submissions, voting, scoring, and host moderation.
- `PromptLibrary` and `RandomBribeLibrary` provide curated prompt lists and fallback bribe content when players time out.
- `GameTickHostedService` advances timer-driven phases by polling all games.

Key frontend components:

- Standalone Angular components for each phase (`LobbyComponent`, `PromptSelectionComponent`, `SubmissionComponent`, `VotingComponent`, `ScoreboardComponent`, `FinishedComponent`).
- `GameStoreService` maintains player identity, polls game state, and exposes mutation methods that wrap API calls.
- `GameApiService` centralises HTTP calls to the backend using the environment-specific base URL.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download)
- [Node.js 18+](https://nodejs.org/) and npm
- (Frontend testing) Headless Chrome dependencies – already configured via Puppeteer in `karma.conf.cjs`

## Running the application locally

### Backend API

1. Restore dependencies:
   ```bash
   dotnet restore
   ```
2. Start the API (serves on `http://localhost:5000` by default):
   ```bash
   dotnet run --project src/Bribery.Api
   ```
   Swagger UI is available at `http://localhost:5000/swagger` in development.

### Angular frontend

1. Install dependencies:
   ```bash
   cd frontend/bribery-client
   npm install
   ```
2. Ensure the development environment points at your API. The default `src/environments/environment.development.ts` already uses `http://localhost:5000/api`.
3. Run the dev server (served from `http://localhost:4200`):
   ```bash
   npm start
   ```
4. Navigate to `http://localhost:4200` in a browser. Creating a lobby from a desktop browser will expose the shareable code for mobile players.

## Running the automated tests

All tests were written first to drive the implementation (TDD) and should be executed before committing changes.

- Backend unit tests:
  ```bash
  dotnet test
  ```
- Frontend unit tests (Chrome Headless via Puppeteer):
  ```bash
  cd frontend/bribery-client
  npm test -- --watch=false
  ```

## Additional documentation

- `docs/FUNCTIONAL_BRIEFING.md` – complete functional requirements used to scope the feature set.
- `docs/ORACLE_FREE_TIER_HOSTING.md` – step-by-step guide for deploying Bribery on an Oracle Cloud Free Tier compute instance.

## Contributing

1. Run `dotnet test` and `npm test -- --watch=false` to ensure the suite is green.
2. Follow the existing patterns in `GameServiceTests` and the Angular spec files when adding new features.
3. Keep client/server contracts in sync with the DTOs under `src/Bribery.Api/Contracts` and the models in `frontend/bribery-client/src/app/models`.
