# Local development configuration

The API reads connection strings, JWT signing keys, and seed passwords from
configuration. Keep real values in .NET user-secrets or environment variables; do
not put them in `appsettings*.json`, `.env` files, source control, or screenshots.

## Configure the API

From `backend/HaladeHighSchool.Api`, set a local database connection string and
unique secrets. User-secrets are stored outside the repository:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.\MSSQLSERVER01;Database=HaladeHighSchoolDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
dotnet user-secrets set "Jwt:Key" "<random signing key of at least 32 characters>"
dotnet user-secrets set "SeedAdmin:Email" "<your administrator email>"
dotnet user-secrets set "SeedAdmin:Password" "<unique administrator password>"
```

If your SQL Server uses SQL authentication, store that full connection string with
`dotnet user-secrets` instead of placing its username or password in an appsettings
file. The checked-in connection string uses Windows integrated authentication.

The project uses a `UserSecretsId` so the commands work without putting credentials
in the project file. The administrator seeder does nothing until both admin email and
password are configured. `Jwt:Key` is required when the API starts.

## Optional development accounts

Demo teacher/student seeding is disabled by default. To enable it locally, configure
unique passwords first, then enable the section:

```powershell
dotnet user-secrets set "SeedDemoAccounts:Teacher:Password" "<unique teacher password>"
dotnet user-secrets set "SeedDemoAccounts:Student:Password" "<unique student password>"
dotnet user-secrets set "SeedDemoAccounts:Enabled" "true"
```

Demo accounts are only seeded when the API runs in the Development environment. Existing
accounts are not reset by the seeder. The startup summary reports the number of accounts
processed and does not print credentials.

The optional `database/seed-demo-data.ps1` script requires these environment variables in
the current PowerShell session: `SMS_ADMIN_EMAIL`, `SMS_ADMIN_PASSWORD`,
`SMS_DEMO_TEACHER_PASSWORD`, and `SMS_DEMO_STUDENT_PASSWORD`. It creates demo records
through the API; do not run it against a live school database.

## Account lifecycle

Public registration submissions remain pending until an administrator approves them.
Approval provisions the login and student record together and shows a one-time temporary
password. The password is stored as a hash and cannot be retrieved later; reset it from
the Admin Console if it is lost. Password changes and reset operations should be completed
through the portal rather than by editing database records.

For an existing database, apply the appropriate schema scripts in `database/` in order.
The project supports Nursery, KG, LKG, UKG, Grades 1-12, and Sections A-F.

## Previously exposed values

Older repository history contained development seed passwords and a JWT signing key.
Treat any values that were used as compromised: rotate them and invalidate any affected
tokens. This sanitized snapshot must be pushed without the earlier Git history so those
values are not reintroduced.
