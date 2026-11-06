# UI migration — phases 0–6

## Assignment D follow-up — November 5, 2026

Implemented the return-search and reporting changes on local branch `feat/custodian-reporting-refinement`, based on Assignment C commit `d70f6da5c648c7b286aee3989d960ac02c034f6e`.

### Return and custodian pages

- Added a trimmed, 100-character-bounded search to the shared Admin/Custodian Return & condition page. It searches equipment name/code, borrower full name/account identifier, and exact `RSV-<id>` codes in both active-loan and return queries before counts and pagination.
- Preserved the selected tab and search through paging, details, return forms, invalid form redisplay, and a successful return. New searches begin on page 1. Search-specific empty states avoid system-wide claims such as “Nothing overdue.”
- Removed duplicate queue/return page headings, used shared action styles, added required indicators (including the conditional backdate note and rejection reason), and kept server-side return validation and transaction paths unchanged.
- Added focused service and real MVC tests for the 23-match 10/10/3 paging case, name/code/account/RSV matching, returned and late tabs, HTML-like input encoding, overlong input and empty states.

### Borrowing report

- Kept the existing streamed CSV route and added a separately authorized `ExportExcel` download using `DocumentFormat.OpenXml` 3.5.1. Both use the same normalized filters, report stream, role checks, and borrower-only scope.
- The workbook writes the existing 17 columns in CSV order, keeps identifiers and supplied text as literal text, writes Manila dates as formatted Excel date serials and `Days late` as a number, and includes explicit widths, wrapped long text, and a frozen header. It writes incrementally to a temporary file, enforces Excel's row limit before returning a response, and removes the temporary file on success, failure, and cancellation.
- Kept report search visible, moved the remaining filters into a native keyboard-accessible “More filters” disclosure, displays an active-filter count, and removed the duplicate page title. Added guidance that CSV `####` is an Excel display-width symptom and recommends AutoFit or the XLSX download.
- Open XML reader tests cover all 23 rows, column order, leading-zero identifiers, non-ASCII text, comma/newline/formula-like text, numeric/date cell types, blank dates, widths, frozen header, no formulas/external workbook links, the empty report, cancellation cleanup and package validation. HTTP tests verify the XLSX MIME type, attachment filename, filter use, and the existing CSV response behavior.

### Verification and remaining acceptance evidence

- `dotnet restore .\ASI.Basecode.sln --ignore-failed-sources` — passed.
- `dotnet build .\ASI.Basecode.sln --configuration Release --no-restore` — passed with 0 warnings/errors; the final full test run also rebuilt the final changed code.
- Focused Return, XLSX, and MVC acceptance run — 50 passed, 1 opt-in interactive preview skipped, 0 failed.
- `dotnet test .\ASI.Basecode.sln --configuration Release --no-restore` — 406 passed, 26 opt-in skipped, 0 failed (432 total; 1m 5s).
- `npm.cmd run css:build` — passed; generated CSS asset check passed.
- `git diff --check` — passed.
- SQL Server opt-in tests were not enabled; their skips are not counted as passes.
- I could see the already-running localhost Administrator dashboard, but the browser-control host was unavailable and VS Code's `dotnet watch` requested an application restart after the service-interface edit. The updated pages were therefore not visually checked at desktop/tablet/phone sizes or in both themes. Figma MCP also refused detailed design-context calls because the connected account is on the Starter plan.
- The generated workbook was structurally validated and read in tests, but desktop Excel was not available to verify its rendered `####` behavior. The affected column/sample from the user's CSV was not supplied, so the original display symptom has not been independently reproduced.
- Assignment D's final cross-owner matrix is not complete: evidence from A/B/C, SQL Server concurrency tests, responsive light/dark screenshots, and desktop Excel review remain outstanding. No WBS rows or progress counts were changed; no PR was created or merged.

## Assignment A follow-up — November 2, 2026

Review correction: resolved the dark-mode contrast finding by migrating late-return/403/Error badges and Reject/Cancel actions to the shared danger styles. Added a regression verified failing before the fix; focused UI checks now pass (6 passed, 1 interactive skip). Browser-measured light/dark badge and hovered-button contrast exceeds 4.5:1. See `doc/SHARED_UI.md` for measurements and scope.

Implemented shared UI/login refinement on `feat/shared-ui-login-refinement` from `develop@2c1293b`. See `doc/SHARED_UI.md` for the reusable component/message/theme contract and verification record.

Added Light/Dark/System with stored preference and safe fallback, consistent Barlow controls, semantic badges, required-label metadata and conditional marker synchronization, local Toastr with encoded/consumed flash messages across all roles, coordinated borrower navigation/date and Users filter-search changes, and removal of login Remember me/Create one controls. The user authorized a new editable campus-equipment SVG for login instead of the unspecified picture. Existing model/action/permission/security and transaction contracts remain; the only calendar-script change is its Borrowed appearance color.

Release regression: **380 passed, 26 opt-in skipped, 0 failed**; three JavaScript behavior tests passed. Production CSS and its new utility guard passed. Real-MVC SQLite browser checks covered all three roles, action toasts, theme/font/required controls and desktop/tablet/phone/short-landscape layouts. SQL concurrency, live email, production load and 200% browser zoom were not checked. B/C/D's module changes and final whole-system acceptance remain separate.

Continuation authorized October 25, 2026 and implemented October 26, 2026 on the same branch and checkout. Phases 4–5 and the phase 6 UI consistency checks are complete. A pre-existing CSV export failure was initially reported separately, then fixed under the user's subsequent explicit authorization on October 26. The only additional controller change is asynchronous CSV writer disposal; services, Identity, permission gates, database schema, form/script contracts, calendar engine and 10-record pagination are preserved. The separate local login-copy edit is preserved. On October 27, the user authorized reverting the separate relaxed lockout settings to the original production policy; see the follow-up below.

Date: October 24, 2026. Branch: `feat/ui-design-migration`. Baseline: `6803c83d6a2aea89e684ca991fa67868fc593d59` (staging).

The original batch implemented phases 0–3 (shared foundation, authentication and Borrower UI). The user subsequently authorized Custodian/Admin migration and final whole-system acceptance (phases 4–6); progress and verification for this continuation are recorded below.

## Design authority

Use the [complete role design on Figma Page 3](https://www.figma.com/design/F1NcWYPCQgjVKy0kUd5kI4/campus-equipment?node-id=578-5275), not the earlier 24-screen exploration. The source workspace has matching 36-screen exports under `doc/design-refresh/role-uniform` (uncommitted design work is deliberately preserved in the original checkout).

Retain the existing primary `#5980A6`, strong `#4A6F94`, tints `#D6EBFF`/`#EBF5FF`, canvas `#F2F2F3`, border `#D9D9DC`, text `#3A3C40`, muted `#6B6E73`, black and white. Use Barlow, rounded white cards, pill navigation, modular dashboard grids and responsive forms.

## Phase 0 — baseline and contract inventory

The baseline Release build/test run passed: **348 passed, 17 SQL Server opt-in tests skipped, 0 failed**, 365 total. Current implementations already include the catalog, reservations, calendar, approval/release, returns, profile management and reports. Older README status notes/design sample counts do not override the running code.

| Design / scope | Current views and behavior to retain |
| --- | --- |
| Shared shell | `_Layout`, `_Sidebar`, `_Topbar`, components; preserve role/permission predicates, demo role links, sign-out POST, mobile drawer IDs and script sections |
| S01 login | `Account/Login`; UserId, Password, RememberMe, returnUrl, POST, validation, password visibility and duplicate-submit handling |
| S02–S05 recovery | `ForgotPassword`, `ForgotPasswordCheckInbox`, `ResetPassword`; preserve six-digit OTP, resend POST, generic account-existence response, School ID/user/token, expiry and validation |
| Supporting access | `Register`, `AccessDenied`; preserve all registration fields and eligibility message; preserve denial routing and sign-out behavior |
| B01 dashboard | `Dashboard/Borrower`; model-backed four summary counts, current loans, requests, rejection reasons, eligibility and late returns |
| B02 catalog | `Catalog/Index`, `Details`; Search/CategoryId/From/To, current images/metadata, booked windows, query-preserving pagination, permission-gated reserve link |
| B03/B05/B06 reservation | `Reservation/Create`; retain one server POST with EquipmentId, ReservationStart, ReservationEnd, Purpose, local-time format and server validation. Use visual sections rather than introducing a new multi-step workflow |
| Existing reservation screens | `Reservation/Index`, `Details`; retain status tabs, counts, page query, all timeline states, conditional cancellation and the existing confirmation dialog |
| B04 calendar | `AvailabilityCalendar/Index`; preserve FullCalendar day/week, events endpoint, filters, all data attributes/DOM IDs, permission gating, error handling and selection-to-reservation URL |
| B07/B08 notifications | Superseded by the approved notification enhancement: real inbox, unread count and workflow-backed notices. See `NOTIFICATIONS.md`; no simulated counts or inert actions |

### Scope boundaries

- Preserve controllers, services, DTO contracts, business rules, Identity/cookies, audit behavior, database entities and migrations. No production database migration is part of this change. Calendar JavaScript adds only two FullCalendar 7 presentation-class callbacks; its existing fetching, filtering, conflict checks and selection code remain unchanged.
- Preserve every existing `asp-for`, form action/method, route parameter, hidden value, antiforgery token and script hook. Model-backed data remains authoritative; do not copy Figma sample people/counts into application content.
- Adapt the Figma reset-link text to the existing OTP contract. Keep the current calendar engine and scheduling model, not a new resource-timeline backend.
- Keep existing interaction restrictions; existing unavailable dashboard actions remain unavailable.
- Do not mix in PR review reports or unrelated tracker changes from the original checkout.

## Verification and handoff

The original phase 0–3 batch built production frontend assets, built/tested .NET, compared functional view contracts to baseline, and exercised the real pages at desktop/tablet/mobile sizes with a disposable local database. Its results below are historical; the October 26 continuation has its own verification record at the end of this document.

### Implementation results

Phases **0–3 were implemented in the original batch**. Custodian/Admin workflow views were outside that batch and are now covered by the authorized continuation recorded below.

- Added reusable rounded surfaces, controls, badges, summary cards, table/pager styles, authentication panels, decorative category equipment placeholders, and responsive shell styling using the existing palette. Actual equipment photos still take precedence over placeholders.
- Migrated the authentication layouts without changing their forms or scripts. Replaced the old decorative hard-coded 248/5/3 access-page statistics with the approved equipment illustration; live dashboard/model counts remain untouched.
- Migrated Borrower dashboard, catalog/details, reservation creation/list/details, cancellation dialog and the shared availability calendar presentation. Reservation equipment/schedule/purpose remain one existing server form, with visual sections instead of new server steps.
- Retained existing unavailable dashboard actions and disabled header equipment search; the existing catalog search remains the working search entry. No new notifications were introduced.
- Improved mobile drawer focus trapping and Escape focus restoration. Added a skip link and removed the duplicate header/catalog search DOM ID. Preserved navigation permission predicates and all hidden form values.
- FullCalendar 7 uses semantic roles and generated internal classes. Added supported `eventClass` / `eventInnerClass` callbacks for event appearance, styled the toolbar through semantic selectors, and retained the existing Reserved/Borrowed/Overdue labels and semantic colors.

### Checks actually completed

| Check | Result |
| --- | --- |
| Baseline Release build/test | 348 passed, 17 SQL Server opt-in tests skipped, 0 failed |
| Post-change Release build/test | 348 passed, 17 skipped, 0 failed; Razor views compiled successfully |
| Production frontend assets | `npm run assets:build` passed; CSS rebuilt after the final event-class styling |
| UI contracts | `node ASI.Basecode.WebApp/scripts/check-ui-contracts.mjs` passed for 15 views, hidden inputs and protected business/security code; calendar diff limited to the two appearance callbacks |
| JavaScript syntax | `node --check` passed for app-shell and availability-calendar scripts |
| Patch whitespace | `git diff --check` passed |
| Authentication browser checks | Sign-in/out, required-field feedback, password visibility, remember-me input, generic recovery confirmation for a nonexistent test address, invalid OTP feedback, registration required fields |
| Borrower browser checks | Model-backed populated/empty dashboards, catalog search and no-match state, equipment details, invalid-time validation retaining entered values, valid submission and stored detail view, status tabs, cancellation dismiss/confirm |
| Calendar browser checks | Real SQL-backed events, event colors, equipment filter, day/week switch, future-week navigation and dragging a free interval into the existing reservation form with correct equipment/from/to values |
| Role checks | Borrower-only account sees its permitted links; direct Users page request is denied and the return-to-dashboard link works |
| Responsive checks | 390-pixel authentication/forms and mobile submission; 768-pixel dashboard; 1440-pixel desktop dashboard/calendar. No page-level horizontal overflow observed in these checks; mobile drawer Shift+Tab wraps and Escape restores menu-button focus |

Browser checks used **only** the separate `Gearantee_UI_Migration_6803c83` LocalDB database, seeded with test records. Baseline login/dashboard visuals were inspected before replacing the preview views; after-change pages were inspected in the actual MVC application, not a static HTML mockup. The preview runs at `http://127.0.0.1:5186` from the ignored `artifacts/ui-preview` directory. Its configuration and fixture files are not part of the application patch.

### Verification limits

- The 17 opt-in SQL Server concurrency tests were not enabled; browser interactions did use a real isolated SQL Server database. Services, controllers and migrations are unchanged.
- Live email delivery and a successful emailed-OTP/password-change journey were not exercised. Existing OTP service tests passed; the UI retains the exact recovery fields, hidden values, actions and validation targets.
- The catalog sample fits on one page. Pager contracts are unchanged and covered by the contract comparison; a multi-page catalog was not manually exercised.
- At that handoff, full-system acceptance and detailed Custodian/Admin workflow migration remained phases 4–6. Existing Google Sheet/WBS feature dates and completion statuses are unchanged because this is Additional work.

### Sidebar follow-up — October 24, 2026

At the user's request, removed the development-only role-switch panel. Navigation permissions, destinations and the sign-out form are unchanged. The sidebar now has a viewport-bounded card, compact short-desktop spacing and a fixed profile/sign-out footer. Only the navigation list scrolls when needed; its scrollbar is hidden while wheel, touch and keyboard access remain available. Short landscape screens use a smaller brand area.

Verified the running Visual Studio application with the multi-role demo account: all 11 navigation links, no role switcher or visible sidebar scrollbar, no sidebar overflow at 1366×768, mobile drawer at 390×844, and navigation scrolling with visible sign-out at 844×390. Checked keyboard access to the last link, drawer focus wrapping and Escape dismissal. The 15-view contract comparisons pass with the three intentionally removed demo links excluded; the script's subsequent whole-change guard still flags the separate pre-existing `Startup.Auth.cs` edit, which this follow-up preserves. The standalone preview's LocalDB startup failure prevents checking it there, so browser verification used the running Visual Studio app.

### Pagination follow-up — October 24, 2026

At the user's explicit request, standardized all nine paginated lists on 10 records: catalog, My Reservations, review queue, return desk, borrowing history, users, borrower profiles, equipment and categories. Eight services now use the shared `PaginationDefaults.PageSize`; this is an authorized page-size change beyond the original presentation-only batch. Filters, sorting, permissions, page clamping, calendar event fetching and exports are unchanged. Both equipment/category tabs retain their separate page parameter names and active-tab filters.

All lists share `Components/_Pagination` and `PaginationViewModel`: record ranges, a 10-per-page indicator, page count, bounded numbered links with ellipses, current-page/disabled states, and Previous/Next links. Mobile uses compact Previous/page count/Next controls. Table scroll wrappers now contain positioned screen-reader labels so they do not force horizontal page overflow.

Verification: Release build and the suite passed with 367 tests passed and 17 SQL Server opt-in tests skipped. An additional HTTP/Razor integration test then passed, navigating 23 filtered accounts over 10/10/3 rows through the rendered Next links and checking the final disabled state. Existing hostile-search encoding and category tab/filter pagination tests passed. Added coverage for all four return tabs, catalog and reservation boundaries, record ranges, large-page windows and preserved route parameters. Production CSS build and `git diff --check` passed.

Browser QA at 1366×900 and 390×844 used the actual Razor HTML saved by the SQLite integration test, served as a local, read-only fixture preview at port 5191. Checked Next/Previous navigation, first/current/last page states, 10/10/3 rendered rows and no mobile page overflow after the containment fix. These visual checks are not a claim of a new live SQL Server session. Rebuild and restart the Visual Studio app to load the new compiled page-size logic and pager model. The following handoff notes describe the updated staged contract checker for these authorized follow-ups; it continues to reject unrelated authentication changes.

### Git handoff — October 25, 2026

The handoff includes the UI migration and explicitly requested sidebar/header, Custodian seed and 10-record pagination follow-ups. Separate local lockout settings, login-label copy changes, earlier design exports/review reports and their four original tracker notes are excluded from the commit without modifying those working files.

The contract checker now supports `--staged`: it verifies unchanged form/hidden-field/script contracts, all nine pager filter/page parameters, exact page-size-only changes in the eight services, the shared 10-record constant and unchanged protected authentication/controller/persistence code. This mode checks the proposed commit independently of the separate local authentication edit. Build validation can substitute a copy of the unchanged baseline `Startup.Auth.cs` through an ignored MSBuild targets file so those local lockout changes stay untouched.

Handoff validation passed: full-solution Release build/test (368 passed, 17 SQL Server opt-in tests skipped), 22 view and nine pager contracts from the staged index, JavaScript syntax, production frontend asset build and generated-asset consistency, EF pending-model check (no changes), and NuGet vulnerability audit (none found from the configured sources). The Release check compiled the baseline authentication configuration through the validation targets; the working file remained unchanged. Remote CI status is separate from these local results.

### Original checkout and header follow-up

Header follow-up: removed the disabled catalog-search placeholder from the shared topbar at the user's request. Verified the Borrower, Custodian and Administrator dashboards each render without a header search field. Working catalog filters and the Users page's account-search form remain unchanged.

Use `feat/ui-design-migration` directly in the original repository checkout. At the user's request, the UI changes were relocated there and the separate worktree was retired. Earlier uncommitted design files and PR review reports remain preserved locally, together with their separate tracker notes. A recovery ZIP is stored under the ignored `artifacts` directory. The user subsequently authorized phases 4–6; no additional checkout or worktree was created.

## Phase 4–6 continuation — October 26, 2026

Baseline: `8ee9b78f29a5dbc92bd21c38fb5ca921b0eabad7`. Implemented in the original checkout on `feat/ui-design-migration`. The user requested commit/push handoff on October 27; no PR creation was requested.

| Phase | Implemented presentation | Preserved behavior |
| --- | --- | --- |
| 4 — Custodian | Modular dashboard, review queue and status tabs, approval/rejection/release details, return desk and inspection/history details, shared reporting/calendar styling | Existing approval/rejection/release POSTs and permission gates, required rejection reason, inspection fields, backdate note, damaged-item status restrictions, late-return information and all existing script handlers |
| 5 — Admin | Dashboard/category inventory bars, equipment/category lists and forms, category dialogs, borrower profiles/eligibility, users/roles/permission matrix, CSV import and history filters | Model data, concurrency stamps, item edit locks, activation/eligibility actions, role/permission restrictions, upload format, validation, filtering and 10-record pagination |
| 6 — UI consistency | Shared palette, rounded cards, responsive forms/tables, long-content wrapping, keyboard focus, phone drawer and inventory dialog focus containment | Existing routes, fields, data hooks and inline scripts; no production controller/service/model/schema changes |

Changed 21 workflow views, source/compiled CSS, and added a presentation-only category-dialog accessibility script. Added the missing review-page titles and corrected the account form's existing label association with `initial-role`. No simulated notification feature or new workflow was introduced. Existing dashboard actions marked **Soon** remain unavailable, with operational workflows accessible through the existing navigation.

### Continuation verification

| Check | Observed result |
| --- | --- |
| Release regression suite excluding the separately reproduced CSV diagnostic | **370 passed, 18 skipped, 0 failed**. Skips: 17 opt-in SQL Server tests and one opt-in interactive preview |
| New MVC/Identity acceptance | Real production controllers/services and Razor views, disposable SQLite database, actual cookies/CSRF. All 41 role-page requests render; Borrower/Custodian requests to user administration are denied. No-match lists and invalid category/user/profile/return submissions retain validation and form context |
| CSV diagnostic before the authorized repair | **1 failed**, reproducing the pre-existing controller disposal error described below. This historical filtered regression result did not establish that export worked; see the subsequent repair verification |
| View/source contracts | `check-ui-continuation.mjs --local-baseline artifacts/ui-phase4-baseline.json` passed for all **51 views**. Fields/routes/model references/guards/original scripts preserved; the exact label-association repair and single accessibility script are the explicit presentation exceptions. Two pre-existing local authentication/login files match their pre-work hashes |
| Assets and syntax | `npm run assets:build`, JavaScript syntax and `git diff --check` passed. Rebuilt calendar vendor assets match HEAD apart from normalized Windows line endings |
| Desktop browser | 1440×1000 Custodian/Admin dashboards, long equipment name wrapping, inventory/category tabs and category create dialog |
| Tablet/phone browser | 768×1024 return inspection and calendar; 390×844 dashboard, review detail, return form and account form. Tables scroll within their containers; no page-level horizontal overflow observed |
| Keyboard and states | Category dialog focuses its first field, Tab/Shift+Tab wrap, Escape closes and restores its opener/scroll state. At 844×390 the drawer wraps keyboard focus and Escape restores the menu button. Selecting Custodian hides Borrower account fields and removes their required flags. Damaged return condition disables Available and selects Under Maintenance. Calendar Week/Day switching and seeded events render |

Browser checks used the opt-in `UiAcceptanceSite` on port 5192 with **disposable SQLite test records**, real MVC/Identity and existing services. They did not use the configured application SQL database or send email. The preview and gallery servers were stopped after checks; viewport overrides and temporary tabs were cleaned up. Screenshots are saved in ignored `artifacts/ui-custodian-desktop.png`, `artifacts/ui-admin-desktop.png` and `artifacts/ui-admin-mobile-form.png`.

The fixture is deliberately test-only: `GEARANTEE_UI_PREVIEW=1` enables its interactive test, and `artifacts/ui-acceptance-stop` stops it early. Its sample credentials are not application seed credentials. Ordinary test runs skip the interactive host. The new rendering/validation tests exercise HTTP contracts; successful production SQL transactions, live email and the opt-in SQL concurrency scenarios were not re-exercised in this continuation. Existing transaction/service tests passed in the regression run.

### Authorized follow-up — borrowing-history CSV export

**Status: fixed.** The user initially chose to report the issue separately, then explicitly requested its repair on October 26. This follow-up expands the earlier UI-only scope solely to fix streamed-response disposal.

Authenticated `GET /BorrowingHistory/Export` under default Kestrel response settings previously failed when `BorrowingHistoryController.Export` disposed its `StreamWriter` synchronously after `FlushAsync`. Kestrel threw **“Synchronous operations are disallowed”** from `StreamWriter.Dispose`, terminating the response prematurely. Changed exactly `using var writer` to `await using var writer`. The response stream stays open, and formatting, filters, authorization, encoding and filename generation remain unchanged. Synchronous server IO was not enabled.

`Csv_export_completes_under_default_Kestrel_response_rules` now passes against real Kestrel/MVC with disposable SQLite records. It verifies a completed HTTP 200 response, CSV content type, attachment filename, exact formatter header and seeded equipment data. It remains an ordinary regression test; its obsolete `KnownExistingIssue` trait was removed. The continuation contract checker verifies the entire controller equals its baseline plus this exact one-line disposal change, while continuing to reject other backend edits.

Post-repair verification: the **unfiltered Release suite passed: 371 passed, 18 opt-in tests skipped, 0 failed** (389 total). The dedicated export test, all 51 view contracts and `git diff --check` passed. Test results: `ASI.Basecode.Tests/TestResults/csv-fix-regression.trx` (local ignored artifact). No production database was modified. Rebuild/restart the application to load the repaired controller.

Acceptance disposition: Custodian/Admin UI implementation and scoped responsive/accessibility checks are complete, and the CSV export exception is resolved. The production SQL concurrency and live-email verification limits above remain explicit; this does not establish production deployment acceptance. WBS statuses/dates/owners and milestone checkboxes are unchanged; the migration and separately authorized repair are recorded under Additional work.

### Review follow-up — login lockout, October 27, 2026

At the user's request, reverted the separate local authentication relaxation: accounts lock after **5 failed attempts for 15 minutes**, replacing 20 attempts/1 minute. `Startup.Auth.cs` now matches the original Git baseline exactly. The existing login message already says 15 minutes, so it is consistent again. The UI, CSV fix, account data and other Identity settings are preserved; no database migration or account unlock was performed.

The continuation checker now requires the complete baseline authentication file, including when supplied with the older pre-work snapshot. That snapshot's former authentication exception can no longer accept the weaker settings. The remaining separate login-copy file is still verified against its original hash. The 51-view contract check and whitespace check passed.

Post-restoration Release build/regression passed: **371 passed, 18 opt-in tests skipped, 0 failed**. The original lockout settings were verified by comparing the complete production authentication source to the Git baseline; the disposable UI fixture's separate Identity configuration is not evidence of production lockout timing. Rebuild/restart the running application to load the restored policy.

### Continuation Git handoff — October 27, 2026

The scoped handoff includes the 21 migrated Custodian/Admin views, source/compiled styles, category-dialog focus handling, CSV disposal repair, disposable HTTP acceptance tests, the continuation checker and tracker/documentation updates. The separate login-label copy, four earlier Figma tracker notes, design exports and PR review reports remain local. Authentication matches the original policy and has no pending source diff.

`node ASI.Basecode.WebApp/scripts/check-ui-continuation.mjs --staged` reads all audited files directly from the proposed commit's index, including compiled styles, and permits no local-file exemptions. It still requires the full original authentication file and allows only the exact authorized asynchronous CSV disposal change in that controller. The working-tree mode retains hash verification for the separate login-copy edit. The verified comparison base for the PR draft is `origin/staging` at `6803c83d6a2aea89e684ca991fa67868fc593d59`; the full branch includes the earlier phase 0–3/follow-up commit as well as this continuation. No existing PR base could be queried because the GitHub connector is unavailable.

## Assignment B student flow and calendar

Implemented on `feat/student-calendar-refinement`, based on Assignment A commit `100b465`. The borrower dashboard links directly to the catalog and personal history. The catalog keeps keyword and category search and ten-record pagination. Old catalog list URLs with From or To redirect to a clean URL with the same search and category, starting at page one. Existing catalog detail links retain their scheduling context.

My borrowing history uses the existing reservation routes, filters, ownership checks and cancellation flow. Borrowers with an explicit reporting grant can open their own report from history. Staff still use the separate reporting link. The reservation form retains the original Manila start and end time inputs and validation.

The shared calendar defaults to equipment rows with seven Manila date columns. Bookings spanning several days appear in each occupied date cell. Expand a booking to see the interval returned for the displayed week. Each equipment row links to the existing detailed grid, where drag selection and a keyboard-accessible reservation form link provide precise scheduling. Search by equipment name or code runs before the item cap and event query. Category, item and location filters apply together with search. The calendar retains old day and week view URLs, and cancels outdated requests.

Verification used the existing MVC and disposable SQLite fixture. The targeted availability, reservation and UI migration suites passed 123 tests with 11 opt-in skips. The Tailwind build, utility guard, JavaScript syntax and whitespace checks passed. Browser checks covered light desktop and dark mobile, equipment search, an empty result, keeping search across week changes, keyboard access to event details and the reservation time fields, and the borrower history navigation. Screenshots are in ignored `artifacts/assignment-b-calendar-light.png` and `artifacts/assignment-b-calendar-mobile-dark.png`. Production SQL concurrency, the full test suite, and exhaustive visual comparisons with every Figma frame were outside this verification pass.
