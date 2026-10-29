# Bulk account creation (WBS 15.00)

Open **Users & Roles → Import CSV** using an active Administrator account with `user_role.manage`. Download the template from the page, fill it out locally, then select the file and click **Import accounts**.

## File format

```csv
UserCode,FirstName,LastName,Email,RoleName,Password,SchoolId,Department,ContactNumber
```

- Use all nine headers exactly once; header order and capitalization may vary. Extra headers are rejected.
- Use UTF-8 CSV (a UTF-8 BOM is supported), maximum **1 MiB** and **100 account records**. Blank lines are ignored. Errors identify the physical starting line of a record, including records with quoted line breaks.
- Required for every account: user code, first name, last name, email, initial role, and password.
- Supported initial roles: `Borrower`, `Custodian`, `Administrator`. Assign additional roles later through Edit account.
- Borrowers also require a school ID and department; imported borrower profiles start **ineligible**. The school ID is the borrower's account identifier, so the `SchoolId` value must equal that row's `UserCode`.
- Contact number is optional. School ID and department may be blank for non-borrowers.
- Each password must be 8–100 characters and satisfy the configured Identity policy (normally uppercase, lowercase, digit, and non-alphanumeric character). Password whitespace is preserved; other fields are trimmed.
- Codes and school IDs are strings. Configure spreadsheet columns as text before entering leading zeroes; the importer cannot restore zeroes removed by a spreadsheet.
- Quote fields containing commas or line breaks; double an embedded quotation mark. Do not use a simple comma-split conversion.
- Emails, user codes, and borrower school IDs must not duplicate existing accounts or another row. In-file duplicate detection is case-insensitive; Identity normalization and SQL Server constraints also apply.

## Results and retrying

The whole file succeeds or fails together. Any validation or creation failure creates **zero** accounts, memberships, profiles, or audit records from the batch. Fix the listed errors and reselect the file to try again. Successful uploads redirect to Users & Roles with the number created. Uploading the same file again reports duplicates; it never updates existing accounts.

Large multipart requests can be rejected by the web server before row parsing. Split files exceeding the stated limits. Each file is its own transaction.

## Handling passwords

The CSV contains initial passwords. Use a separate strong password for each account, distribute credentials through an approved secure channel, and remove your local CSV when it is no longer needed. Do not commit CSVs containing real credentials or attach them to issues, PRs, or chat.

Gearantee buffers permitted uploads in memory and does not retain the original file. Passwords are passed to ASP.NET Core Identity for hashing and are excluded from import result pages, parser errors, TempData, application audit records, and logs. No invitation email, forced-password-change feature, or external delivery dependency is introduced by this import. The existing password-reset flow remains available.

## Developer verification

Run the normal suite from the repository root:

```powershell
dotnet test .\ASI.Basecode.sln --configuration Release
```

The suite includes real HTTP requests to a temporary loopback MVC host for authorization, antiforgery, upload results, invalid Edit rendering, and hostile search values in generated Razor links. Its authentication handler supplies test claims; production Identity authentication is unchanged. SQLite tests cover validation, transaction rollback, audits, and state changes.

To additionally verify the actual SQL Server application lock, unique email index, migrations, rollback, and the equipment inventory/reservation concurrency contract with Windows LocalDB:

```powershell
sqllocaldb start MSSQLLocalDB
$env:GEARANTEE_TEST_SQLSERVER = "1"
dotnet test .\ASI.Basecode.Tests\ASI.Basecode.Tests.csproj --configuration Release
Remove-Item Env:GEARANTEE_TEST_SQLSERVER
```

SQL Server tests create uniquely named disposable databases (user-administration tests use `Gearantee_Pr22_Test_<guid>`; the equipment race test uses `Gearantee_EquipmentRace_<guid>`) and delete only those databases on completion. They never use or migrate `GearanteeDev`. Without the opt-in environment variable, those tests are explicitly skipped. The existing CI continues to run the portable tests; SQL Server checks require a host with LocalDB and this opt-in.

CI triggered by a PR normally checks out GitHub's synthetic merge commit. Record both its head/base pair and its actual checkout when reporting results. A successful CodeQL analysis job and the separate code-scanning alert result are different checks; inspect both before merging.
