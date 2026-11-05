# Shared UI contract — Assignment A

Implemented November 2, 2026 on `feat/shared-ui-login-refinement`, based on `develop@2c1293b`. This is the common foundation for assignments B/C/D; their module redesigns remain separate.

## Theme and type

The layout loads Barlow at 400/500/600/700. Inputs, selects, buttons and calendar titles use the same family. The theme script runs before CSS to apply a stored Light/Dark/System preference; System follows the OS. The picker is available in both authentication and application shells. Storage failure falls back to a usable in-page selection.

Use the shared tokens instead of new hard-coded light surfaces:

| Purpose | Token |
| --- | --- |
| Card/dialog/select surface | `--ui-surface` |
| Canvas / inset surface | `--color-gray-50` |
| Elevated muted surface | `--color-gray-100` |
| Border | `--color-gray-200` |
| Body / heading / muted text | `--color-gray-700` / `--color-ink` / `--color-gray-500` |
| Links / focus | `--ui-link` |
| Blue filled primary action | `--color-primary-strong` with white text |
| Status / message families | `--ui-{success,info,warning,danger,neutral}-{bg,text}` |

The existing brand blue stays the same. Dark mode changes surface/text tokens. Equipment illustrations retain their own colors; the logo uses a theme-specific variant.

## Brand logo

The shared `_BrandLogo` partial is used by authentication pages, registration and the sidebar. Light mode keeps the original `wwwroot/img/gearantee-logo.png`. Dark mode uses `wwwroot/img/gearantee-logo-dark.png`: light blue-gray lettering with the blue emblem/wordmark on a transparent background. The old white dark-mode logo panel is removed. CSS follows the same resolved `data-theme` as the rest of the app, including System; hidden variants use `display:none`.

Created with the built-in image-generation tool, using the original logo as the edit reference. The initial transparent drafts had visible edge artifacts and were discarded. The initial opaque output was `exec-fb019986-171d-4566-b3bb-fda98d562b02.png`. The user subsequently requested transparency; the current selected output is `exec-6fb7ddf7-31b7-4335-9779-60447282de3b.png`, copied into the project with its generated alpha channel preserved. The original light logo is unchanged; the opaque revision is retained in ignored `artifacts/gearantee-logo-dark-opaque.png`.

Initial opaque generation prompt (superseded):

> Use case: precise-object-edit. Create the dark-mode production version of this exact Gearantee logo. Re-render the logo cleanly onto an entirely solid uniform deep navy background, hex #1c2938. IMPORTANT: opaque solid navy canvas, no alpha cutout. Keep the exact horizontal lockup, original bold geometric lettering, blue gear/G/check emblem and original blue suffix. Change only the dark navy 'Gear' lettering to solid off-white #edf3fa. Tagline must read exactly 'CAMPUS EQUIPMENT BORROWING AND RESERVATION SYSTEM' in crisp simple small off-white lettering with the thin flanking rules. The full wordmark reads exactly 'Gearantee'. Logo centered, full wide composition, no cropping. Keep same proportions, relatively small even padding. Smooth clean digital lettering and symbol edges, no roughness, no distressed texture, no grunge, no specks, no glow, no outline around the text. Flat opaque navy background all the way to edges, no transparency, no extra objects, no white panel.

Final transparent generation prompt (built-in tool, transparent background enabled):

> Create a CLEAN transparent-background version of this exact Gearantee logo. Single isolated digital logo, smooth flat geometric typography, clean vector-like edges. The blue gear/G/check and blue 'antee' stay the same. Use a solid light icy blue-gray #b8cde0 for 'Gear' and the small tagline/rules, rather than pure white. Exact word: Gearantee. Exact tagline: CAMPUS EQUIPMENT BORROWING AND RESERVATION SYSTEM. Entire background is true transparent alpha, including all gaps and counters. Only the logo artwork should exist. No pale matte, no splashes or rough texture, no navy backdrop, no rectangle, no speckles. The solid filled letters must be pristine and opaque. Preserve original horizontal proportions and spelling.

November 2 verification: production CSS build/utility guard, 3 focused SharedUiTests and 3 JavaScript checks passed for the initial integration. Browser checked the real local login page: both assets loaded, exactly one logo visible per theme, dark brand wrapper has no white background, and light mode retains its original asset. Initial preview: `artifacts/dark-logo-login.png`. The local application was rebuilt/restarted on port 5186. No database/seed changes; full regression suite was not rerun for this asset/presentation change.

Transparency follow-up: verified a 2169×725 ARGB PNG with alpha 0 at all four corners and a sample in empty upper background. The login browser check passed with exactly one loaded dark logo at its intended 260px width and a transparent brand wrapper; preview: `artifacts/dark-logo-transparent-login.png`. During this check, discovered the earlier line-ending cleanup had truncated the generated CSS. Regenerated it from the intact source; production CSS build and emitted-utility guard passed, and the styled page was rechecked after the rebuild. No new application logic or test changes; full regression suite was not rerun for the image replacement.

## Controls and page structure

- Use `btn-primary`, `btn-secondary`, `btn-danger` or `btn-text` for actions and `input` for fields. Keep actual disabled/loading attributes; appearance is not authorization.
- Shared confirmation dialogs default to the primary action. Set `ConfirmDialogModel.IsDestructive=true` for cancellation/rejection; approval and release retain blue confirmation buttons.
- Button/navigation links have no underline; color, weight, hover and keyboard focus indicate interaction.
- The shared topbar supplies the page H1. B/C/D should remove their own repeated page-title H2s, retaining meaningful section headings, totals, subtitles, action bars and valid aria-labelledby associations.
- Reuse `filter-panel`, `filter-panel--compact` and module filter layouts. Search remains visible; pagination/filter behavior stays unchanged.
- The shell has one Borrower My borrowing history link on the existing Reservation route; Catalog is active during reservation creation. Staff reporting remains permission gated and is labeled Borrowing reports.
- Borrower dashboard header date is removed. Other timestamps/provider logic remain.
- Users has one visible search input in the filter form; the topbar search and hidden duplicate were removed together.

## Statuses

Render `Components/_StatusBadge` with the real status label. It handles case/space/hyphen aliases, including UnderMaintenance and Under Maintenance, without altering stored data.

| Family | Labels |
| --- | --- |
| Success | Available, Active, Eligible, Returned, Good |
| Information | Approved, Awaiting Release, Reserved, Borrowed |
| Warning | Pending, Due today, Needs inspection, Under maintenance, Under repair |
| Danger | Overdue, Rejected, Damaged, Ineligible |
| Neutral | Other labels, including cancelled, expired, archived and unavailable |

Calendar Reserved/Borrowed/Overdue appearance retains the same event data/selection logic, with Borrowed now using an informational blue. Module owners can migrate remaining inline status spans to the common partial.

## Required markers

`RequiredLabelTagHelper` appends an accessible marker to `label asp-for` from MVC validation metadata. Checkbox/boolean labels are excluded. Existing for/id associations and server validation are unchanged.

`form-presentation.js` synchronizes visible markers with current required, disabled, readonly and visibility state, including conditional account fields and newly opened inventory modals. It adds a form legend only when visible required fields exist. Explicitly associated labels without asp-for also receive a marker when the field is required. Reuse existing validation/conditional requirements rather than adding requirements for visual consistency.

## Messages and toasts

The common layout renders `_FlashMessages` once. Set existing `SuccessMessage`, `ErrorMessage`, `WarningMessage` or `InfoMessage` in TempData for a redirect, or ViewData for the current response. It consumes TempData for each key; ViewData takes precedence when both are present.

Pages must not render a second flash banner or call Toastr themselves. The existing banners were removed in a narrow shared integration across the affected views; module owners can continue using their existing controller messages.

Razor encodes the inline fallback. The adapter reads plain text and enables Toastr HTML escaping. Success/info dismiss after six seconds, with hover/focus pause. Warning/error stay until dismissed. Close buttons have accessible names; errors use alert semantics and success uses status semantics. Reduced motion disables fades/progress. Failed JavaScript/library initialization leaves the inline message visible. Field errors, validation summaries, eligibility warnings, dashboard failures and calendar load errors remain contextual.

Toastr is the same existing CDN package, now bundled under `wwwroot/lib/toastr` with its MIT license, so it no longer depends on the external CDN. Do not introduce a second toast system or simulated notification inbox.

## Login

Remember me and Create one are removed. The normal form submits RememberMe=false; Identity routes, OTP, registration endpoints, credentials and lockout behavior remain unchanged.

At the user's subsequent request, a new editable SVG replaces the unspecified photo: `wwwroot/img/campus-equipment.svg`, showing a laptop, camera, projector and reservation check mark. It appears on login only; the recovery story retains its existing illustration. It is decorative (empty alt) beside explanatory text.

## Build and checks

Run `npm.cmd run assets:build` in WebApp. The CSS build now checks that font-sans, sr-only, flex and hidden utilities were emitted. During this implementation, a sandboxed native scanner silently emitted no utilities; rebuilding with normal filesystem access restored them. A passing compiler exit code alone is no longer sufficient.

Run `npm.cmd run test:ui` for theme and toast behavior checks, and `dotnet test ASI.Basecode.sln --configuration Release` for the regression suite. SharedUiTests exercise real MVC/Identity cookies, role shell links, moved search, required markers, encoded messages and message consumption after redirects.

The migration-era contract scripts remain historical audits with their old baselines. Intentional A changes are: login control removal/false hidden value; shared scripts/partials; label metadata; sidebar labels/active state; Borrower date visibility; Users search relocation; flash-message rendering; calendar color constants and presentation CSS. No production controller, service, data, permission, schema, retry or lock implementation changed.

## Verification record

- Baseline Release: 378 passed, 26 opt-in skipped, 0 failed.
- Assignment A Release: 380 passed, 26 opt-in skipped, 0 failed; JavaScript behavior checks: 3 passed.
- Frontend CSS production build, emitted-utility guard and modified JavaScript syntax checks passed. The first sandbox build omitted utilities; the guarded rebuild used normal filesystem access. A temporary Windows file lock was resolved by stopping the isolated preview before final tests.
- Browser checks used only the real MVC/Identity SQLite acceptance fixture, with test accounts/data: Admin category creation, Borrower reservation submission and Custodian approval each produced one toast; form messages remain encoded/consumed by the HTTP tests.
- Checked desktop 1440×900, tablet 768×1024, phone 390×844 and short landscape 844×390; theme persistence across navigation, computed Barlow font on controls, contextual required markers, drawer Escape/focus restoration, cancellation dismissal, scrollable inventory modal, dark calendar and moved Users search.
- Screenshots: `artifacts/assignment-a-login-light.png`, `assignment-a-login-mobile-dark.png`, `assignment-a-borrower-dark.png` (the latter two also under artifacts). The preview was stopped and its temporary browser viewport reset.
- Limits: the 26 skipped tests include SQL Server opt-in checks and the interactive fixture. Browser interactions used SQLite, not production SQL. Live email, production load, exhaustive screen-by-screen acceptance and 200% browser zoom were not checked. Module redesign/acceptance remains with B/C/D.

### Contrast review fix — November 2

Resolved the Ponytail review finding: late-return, 403 and Error indicators now use the shared danger badge styles; Reject/Cancel actions use `btn-danger`. They no longer reuse the heading color as a solid fill. Existing confirmation/authorization behavior is unchanged.

The new real-MVC regression failed on the original late-return markup, then passed after all affected consumers were migrated. Focused UI tests: 6 passed, 1 interactive skip. Production CSS and its utility guard passed. Browser assertions on disposable fixture data checked light/dark late-return and 403 badges (5.86:1 / 7.63:1 contrast) and actual hovered Reject/Cancel buttons (9.21:1 in both themes), above the 4.5:1 check threshold. Generic Error uses the same badge class; it currently has no standalone production action to exercise by URL. The full regression/SQL suite was not rerun for this five-view presentation correction.

### Account and upload contrast follow-up — November 2

Resolved the subsequent Ponytail review finding: the read-only School ID on Users/Edit now uses the existing theme-aware `text-gray-700` token, and the shared file-selection button uses `--ui-link` instead of the fixed primary fill color. The CSV import field and all other consumers of that shared selector keep their existing input/validation behavior. Source and generated CSS are updated together; no business logic changed.

The production CSS build/utility guard passed. Existing real-MVC UI checks: 6 passed, 1 opt-in interactive skip. Browser assertions on the disposable SQLite fixture checked each control against a 4.5:1 threshold in both themes: School ID 9.11:1 light / 9.61:1 dark (previously 1.70:1 dark); file-selection text 6.15:1 light / 7.51:1 dark (previously 2.33:1 dark). Evidence: `artifacts/contrast-followup-results.json`, `school-id-contrast-fixed-dark.png` and `file-button-contrast-fixed-dark.png` under artifacts. The fixture was stopped, and the existing application preview on port 5186 was refreshed. No production records were changed; full regression, SQL Server and load checks were not rerun for these two presentation changes.
