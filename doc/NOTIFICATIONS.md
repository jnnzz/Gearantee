# Notifications, authentication and UI polish

Implementation branch: `feat/notifications-ui-polish`, based on `feat/workspace-calendar-refinement`.
Date: 11/6/2026. This is Additional work, not a new WBS milestone.

## Authentication and UI

- Sign-in accepts the canonical School ID (`AspNetUsers.UserName`) and password. Staff account IDs remain valid. Email and legacy `UserCode` aliases are not alternative login lookups.
- Forgot password and OTP verification accept School ID. The server resolves the registered email; forms and confirmation pages never contain that email. Existing OTP expiry, single use, attempt limits, resend cooldown, IP limits and lockout reset remain in place.
- Authentication pages force light before stylesheet paint and have no theme toggle. The saved workspace theme is not overwritten.
- Borrower Profiles uses a visible Action heading and shared edit/eligibility action buttons.
- Dashboard load failures retain exception logging and shared error feedback, with a retry state instead of fabricated zero metrics. The former full-width banner is removed. Local logs showed missing `Reservation.IsFullDay`; the existing full-day migration is present in the verified local database.

## Event matrix

| Event | In-app recipients | Email |
| --- | --- | --- |
| Submitted, cancelled, expired | Active custodians with reservation review; active admins with history access | None |
| Approved, rejected | Affected borrower | Affected borrower only |
| Released, return recorded | Affected borrower | None |
| Late return recorded | Affected borrower and admins with history access | None |
| Overdue active loan | Borrower and custodians with release/return access | None |
| Due within configured window | Borrower | None |

Recipients are deduplicated. Late returns create one borrower notice containing the condition/late information. No other borrower's name or purpose is copied into a notification. Admins do not have the custodian-only review route, so reviewer notices link to their dashboard; existing destination authorization is unchanged.

## Storage and delivery

`Notifications` stores the inbox and deferred email state. `RelatedEntityId` always refers to a reservation, including return events; a redundant entity-type column is unnecessary. The 2,000-character body accommodates the existing 1,000-character rejection reason plus item information. A SHA-256 event/reservation/recipient key has a unique index.

Workflow services stage notices in the same transaction as the successful transition. Cancellation and lazy expiry retain conditional updates; a losing transition creates no notice. No email/network call happens under workflow database locks.

The hosted worker scans once per minute while the application is running. Reminders use batches of 100 candidate IDs and recheck unreturned loans inside each transaction. The current due timestamp is authoritative: full-day reservations already store the following midnight; timed legacy reservations retain their exact timestamp. Overdue means strictly past that timestamp, matching existing services. Each reminder type occurs once per reservation/recipient. There is no historical event backfill; existing active loans can receive reminders on enablement.

Email batches contain at most 25 pending rows. An atomic five-minute claim lease prevents normal overlapping delivery. Each provider call times out after 30 seconds; failures retry after 2, 4, 8 and 16 minutes, with a maximum of five attempts. Failed rows remain for diagnosis; inactive or email-less recipients are not sent mail. Read/unread state does not affect delivery. Provider error bodies are not stored.

Delivery is at-least-once: a process crash or ambiguous provider response after acceptance can cause a duplicate on retry. The lease does not promise exactly-once email delivery. No SMS, SignalR, mobile push, preferences, or staff email are added.

## Setup

Apply migrations before enabling notifications on any other database:

```powershell
Update-Database -Project ASI.Basecode.Data -StartupProject ASI.Basecode.WebApp -Context AsiBasecodeDBContext
```

The additive migration is `20261106122123_AddNotifications`. It was applied to local `(localdb)\MSSQLLocalDB` / `GearanteeDev` on 11/6/2026. Existing users, passwords and borrowing records were not modified.

`Notifications:Enabled` defaults to false outside Development. Development enables it; `Notifications:DueSoonHours` defaults to 24 (bounded to 1–168). Restart the app after changing these options. Disabling pauses generation, endpoints and the worker without deleting inbox or queued rows. Re-enabling resumes pending delivery.

Reuse existing `Brevo:ApiKey`, `Brevo:SenderEmail`, `Brevo:SenderName`, and `Brevo:BaseUrl` settings. Store credentials in user secrets or environment configuration, not source control. Missing provider configuration causes bounded delivery failures, not a failed reservation transaction. Password-reset OTP delivery remains separate.

## UI/API

- Authenticated bell: unread count and recent notices, refreshed at page load/open.
- `/Notification/Index`: ten records per page, newest first, safe internal detail links.
- GET `/Notification/UnreadCount`, `/Notification/Recent`: current user's data only; no client-supplied recipient parameter.
- POST `/Notification/MarkRead`, `/Notification/MarkAllRead`: antiforgery-protected and recipient-scoped.
- No read-state changes on GET. Text is encoded in Razor, plain-text DOM updates and email HTML. Notification pages are not cacheable.
- Toastr remains immediate action feedback; persistent notifications represent workflow events, not infrastructure errors.

## Verification

Tests use fake email delivery and disposable databases. No test calls Brevo. Coverage includes recipient/channel mapping, rollback, direct-update hooks, retries/leases, reminder boundaries, paging/ownership, antiforgery, School ID-only login/recovery, OTP reuse, inactive/email-less accounts, and rate limits. Frontend tests cover forced light mode, safe bell content/links, fetch errors and keyboard dismissal.

Final standard Release run: **485 passed, 30 opt-in/interactive skipped, 0 failed** (515 total). All **9 frontend tests** and the generated asset guard pass. The SQL workflow run passed 20 checks; two tests timed out during disposable database migration, before behavior assertions. The additional overlapping-worker SQL test timed out during database creation. Rerun those when LocalDB is healthy. Browser automation is disabled in Work settings, so desktop/mobile visual acceptance remains pending. Live Brevo delivery was not exercised.

Logs (ignored local artifacts): `artifacts/notifications-final-tests.log`, `artifacts/notifications-sql-tests.log`, and `artifacts/notifications-worker-sql.log`. Run the application with the Development profile to view the bell after signing in. A restart is required if an older build is still running.
