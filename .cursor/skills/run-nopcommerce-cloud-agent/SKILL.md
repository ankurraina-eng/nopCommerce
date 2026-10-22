---
name: run-nopcommerce-cloud-agent
description: Sets up, starts, and verifies this nopCommerce application on Cursor Cloud Agents with .NET 10 and PostgreSQL. Use when a teammate asks to run, preview, install, restart, or troubleshoot the app in a cloud development environment.
---

# Run nopCommerce on a Cloud Agent

Use the scripts in this skill; do not duplicate their setup logic in ad hoc commands.

## Bring up the app

1. Run the idempotent setup:

   ```bash
   bash .cursor/skills/run-nopcommerce-cloud-agent/scripts/setup.sh
   ```

2. Start the server in a persistent terminal session:

   ```bash
   bash .cursor/skills/run-nopcommerce-cloud-agent/scripts/start.sh
   ```

   The foreground process serves the app on `http://0.0.0.0:5000`.

3. Verify readiness from another terminal:

   ```bash
   bash .cursor/skills/run-nopcommerce-cloud-agent/scripts/smoke-test.sh
   ```

## First database installation

An empty database redirects the browser to `/install`. Use:

- Database: PostgreSQL
- Server: `127.0.0.1`
- Database name: `nopcommerce`
- Username: `nopcommerce`
- Password: `nopcommerce`
- Create database: off

Choose a unique development admin password. Installing sample data is recommended for UI testing.

The start script creates the development database and the required `citext` and `pgcrypto` extensions. Do not delete those extensions: migrations use `citext`.

After installation, nopCommerce stops the process intentionally. Restart `start.sh`; it will reconnect to the installed database.

## Verification

Confirm all of the following:

- `/` returns HTTP 200 and displays the storefront.
- A sample product can be added to `/cart` when sample data is installed.
- `/login` accepts the admin account created during installation.
- `/admin` displays the administration dashboard.

Use a normal browser for account and admin checks. nopCommerce identifies command-line HTTP clients such as `curl` as search-engine visitors, so an otherwise valid login cookie may appear unauthenticated.

## Troubleshooting

- `NU1301` or TLS failures: verify Cloud Agent egress allows `api.nuget.org`, `azuresearch-usnc.nuget.org`, `azuresearch-ussc.nuget.org`, `www.nuget.org`, and `dotnetcli.blob.core.windows.net`.
- `type "citext" does not exist`: stop the app, run `start.sh` to install PostgreSQL extensions, reset the partially migrated development database if safe, then install again.
- Port 5000 is occupied: reuse the existing server if healthy; otherwise stop its owning process before restarting.
- Build failures: run `dotnet restore src/NopCommerce.sln`, then `dotnet build src/Presentation/Nop.Web/Nop.Web.csproj --no-restore`.
- Do not commit `src/Presentation/Nop.Web/App_Data/appsettings.json`, database contents, data-protection keys, or development credentials.
