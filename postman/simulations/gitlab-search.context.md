# GitLab Search simulation

## Purpose

Verify the real C# CLI against HTTP fixtures. The mock is based on the upstream
GitLab.re call shapes and https://docs.gitlab.com/api/search/; fixture values are
synthetic. An owner-workspace mock search was skipped; no Postman cloud resources
or real GitLab credentials are used.

## Execution

Run from the repository root:

```sh
postman simulation run postman/simulations/gitlab-search.sim.yaml --port 4900 --output ndjson
dotnet build src/GitLabSearch -c Release
python3 tests/smoke.py
```

The first command stays running in another terminal. Stop it with Ctrl+C.
`DOTNET` can select the SDK executable; `GITLAB_SIM_URL` selects the member URL.

## Assumptions

- Observed 2026-10-27: loopback sockets require sandbox escalation in this environment.
- Reasoned 2026-10-27: fixtures model only consumed endpoints. Actual GitLab search
  availability, TLS, rate limits and deployment-specific behavior remain unverified.

## Observations

<!-- begin:observations -->
2026-10-27: smoke suite passed; the only member, gitlab, served group discovery,
project enumeration, paginated search and 401/403 paths. No members stayed silent.
The log showed encoded subgroup paths and search filters and a single search per
unique project despite duplicate project listings.
<!-- end:observations -->

## What didn't work

- 2026-10-27: initial simulator startup failed with `listen EPERM`; rerunning with
  loopback permission resolved it. The CLI also needed network permission to connect.
