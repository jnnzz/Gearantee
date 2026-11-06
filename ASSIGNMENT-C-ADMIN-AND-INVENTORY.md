# Assignment C — Admin pages and inventory management

Suggested branch: feat/admin-inventory-refinement.
Estimate: 3–5 focused developer-days, excluding persistent-storage setup and review waits.

## Working agreement for all four people

This file is complete for its assignment: detailed topic instructions are included below. Numeric topic IDs (01–10) describe work chunks, not additional people. Shared-file ownership in this working agreement overrides the original topic-level editing suggestions.

| Assignment | Responsibility |
| --- | --- |
| A | Shared UI/theme/fonts/controls, shell, toast infrastructure, login and supplied picture |
| B | Student catalog/history/reservation presentation and shared calendar/search |
| C | Admin dashboard/accounts/profiles/import entry and inventory/category/image upload |
| D | Custodian/review/return pages, staff reporting/export, final acceptance coordination |

A owns `Views/Shared/**`, `wwwroot/css/tailwind.src.css`, theme code and common message code. B/C/D own their module views, controllers/services and module scripts. Send A the exact shared-shell or CSS changes your module needs; do not edit the same shared file concurrently. A applies B's borrower navigation/date change and C's Users header-search removal together with the relevant module cutover. Page owners remove their own duplicate headings/feedback blocks and consume the shared components. Rebuild generated CSS from source; never hand-edit it.

Start A's component/message contract and D's baseline check first. B can investigate calendar layout, C can work on upload validation and D can inspect exports in parallel. Once shared patterns are available, module work proceeds in parallel. D coordinates the final combined checks; each person tests and repairs their own module. One owner may deliver multiple small PRs.

### Baseline and constraints

Prepared October 31, 2026. Status: **Implementation and automated verification complete; final live drag/drop and responsive screenshot checks remain pending.** Work is on `feature/assignment-c-admin-inventory`; the PR is not open yet. The planning baseline was `develop@2c1293b8d59303eb8e142c774b4a5ea22bdaba71`; keep the integration PR separate from the upstream staging branch unless the owner asks otherwise.

Use [Figma Page 3, complete role redesign](https://www.figma.com/design/F1NcWYPCQgjVKy0kUd5kI4/campus-equipment?node-id=578-5275). The role exports under `doc/design-refresh/role-uniform` are untracked and may not exist in a teammate's clone. Barlow and the existing blue identity remain the reference. The new login picture exists with the user but its file reference is pending.

Confirmed decisions: Student is the current Borrower role; My borrowing history includes every lifecycle status and retains details/cancellation; Catalog is the main reservation entry; remove Catalog date filters; other date filters are date-only; keep actual reservation and return time inputs. The export issue means #### displayed in Excel; add XLSX with readable widths while retaining CSV.

Preserve authorization/ownership, antiforgery, OTP/authentication, approval/release/return/eligibility rules, conflict validation, retries/locks, audit behavior, existing routes and ten-record list pagination. Only narrow supporting behavior for requested search, uploads, reporting and navigation may change. No schema migration is expected. Do not treat Figma sample data as actual application content.

Every owner supplies scoped tests and light/dark responsive screenshots, checks required markers against real validation, and preserves inline errors. Run relevant existing tests, build assets when changed and report real limits. Follow `AGENTS.md` and `doc/PROJECT_MILESTONES.md`; record this follow-up under Additional work in the same PR, without changing other members' WBS owners/statuses/baseline dates. D coordinates final regression evidence; implementation has not started merely because these files exist.

## Your scope and delivery order

1. Refine the Admin dashboard, users/roles, borrower profiles and account forms using A's patterns. Remove dashboard shortcuts and duplicate page headings.
2. Make Users search visible in its filter area on every screen size; coordinate with A to remove the old header search in the same cutover.
3. Move Import CSV into Create account, preserving its existing upload route, format, limits and atomic behavior.
4. Refine equipment/category filters, forms and modals. Remove Active create/edit checkboxes while preserving persisted category status and the explicit activation action.
5. Implement the real optional JPEG/PNG upload, with a drag/browse UI, 5 MB limit, server validation, persistent storage and failure cleanup.
6. Apply A's required-marker, theme, status, controls and toast partial in your views; send shared CSS changes to A.
7. Test account/profile/inventory flows and upload failure cases; hand D scoped evidence.

**You own** Administrator dashboard, Users, BorrowerProfile and Categories views/controllers and necessary supporting module code. **You do not own** the shared Admin/Custodian return page, calendar, staff reports, shared Sidebar/Topbar or global CSS. D handles returns/reports once for all authorized roles; B handles calendar once.

Suggested PR sequence: Admin/users/profile polish and import entry → safe category forms → equipment upload. Keep image handling out of a cosmetic-only PR so validation/storage changes are reviewable.

### Assignment acceptance

- Users has one working filter search and Import CSV is reachable from Create account.
- Dashboard shortcuts, duplicate headers and raw underlined button styling are removed from your pages.
- Removing Active checkboxes never silently changes category status.
- Real JPEG/PNG uploads enforce the limit on the server and preserve existing images on failure.
- Existing account/role/profile/item permissions and business rules pass targeted regressions in both themes.

---

## Topic 06 — Admin pages, user search and import entry

### Objective and references

Remove Admin dashboard shortcuts, remove redundant page headers, use compact Figma filters, move user search into the filter area, and place Import CSV inside Create account.

Use [Figma Admin board](https://www.figma.com/design/F1NcWYPCQgjVKy0kUd5kI4/campus-equipment?node-id=578-3312), A01/A03/A04/A11/A12/A13. Apply Barlow, status colors, dark mode, required markers and non-underlined action styles from 01. Baseline inspected: `develop@2c1293b`.

### Owned and connected files

- `ASI.Basecode.WebApp/Views/Dashboard/Administrator.cshtml`: contains a permission-based Shortcuts section with inactive Soon labels.
- `Views/Users/{Index,Create,Edit,Import}.cshtml` and `Views/BorrowerProfile/{Index,Edit}.cshtml`.
- `Views/Shared/_Topbar.cshtml`: currently renders a Users-only search form.
- `Controllers/UsersController.cs` and `BorrowerProfileController.cs`: retain existing routes, query bindings and action guards.
- `ASI.Basecode.Services/ServiceModels/UserAdministration/UserAdministrationViewModels.cs` and the user/profile services: validation remains authoritative.
- `doc/USER_CSV_IMPORT.md`: navigation wording changes; import format/limits/transaction behavior do not.

Unprefixed paths are relative to WebApp. Inventory belongs to 07, return pages to 08, and report/export UI to 09.

### Ordered work

1. Remove the Admin dashboard Shortcuts card and reflow adjacent cards without changing live totals, alerts or audit data. This does not remove legitimate navigation or other permission-gated operations.
2. Remove duplicated page titles on the owned views. Retain the topbar H1, meaningful section headings, account totals and permission matrix identity. Keep valid heading/ARIA relationships.
3. Make a single `name="search"` input visible inside the Users filter form at every viewport width. Remove the current hidden duplicate search input and the mobile-only visibility restriction. Have A remove the shared topbar search in the same coordinated cutover so desktop search is never temporarily lost.
4. Keep status/role/search in the same GET submission and pagination URLs; reset page on a new filter. Use 01's compact filter pattern with visible search and selected filter state. Do not remove filtering behavior to reduce visual size.
5. Remove Import CSV from the Users listing toolbar. Add an **Import CSV** action within the Create account page/section, linking to the existing Import route. This is a navigation relocation: retain the existing upload page, template download, field errors and PRG success.
6. Do not nest the CSV upload form inside the manual account form or require completion of manual fields before opening import. Preserve direct Import URLs and the same administrator/permission gates.
7. Apply shared controls/badges/required markers to Users, role-permission matrix and borrower profiles. Replace raw or underlined button-like controls with defined styles; preserve checkbox associations and disabled/locked states.
8. Preserve the newer account identifier behavior: School ID derives from the account identifier and is read-only where currently implemented. Do not reintroduce ignored editable School ID inputs from an old Figma screen.
9. Integrate toasts, test light/dark, and update `doc/USER_CSV_IMPORT.md` to **Users & roles → Create account → Import CSV**.

### Acceptance and verification

- [x] Admin Shortcuts card is absent; live dashboard data remains correct.
- [x] Users topbar has no search, and one working filter search exists at desktop/tablet/phone widths.
- [x] Search/status/role combine correctly, survive paging and preserve encoded special characters.
- [x] Import is discoverable inside Create account; listing toolbar no longer duplicates it.
- [x] Direct import route, template, invalid file feedback, atomic import and success count still work.
- [x] Manual creation, conditional Borrower fields, role assignment, activation safeguards and permissions still work.
- [x] Required markers track conditional requirements; read-only/derived inputs are not marked as editable requirements.
- [x] Profile edits and eligibility changes remain permission checked; no UI-only hiding substitutes for access control.
- [x] No duplicate page title; actions and badges are readable in both themes and keyboard focus remains visible.

Items 1–9 are implemented and supported by the passing Release suite plus focused MVC/HTTP checks. Item 2 was checked on the live Users page at desktop, intermediate/tablet, and narrow-phone CSS viewport widths (the narrow viewport was emulated with browser zoom); one page-level filter search appeared and no topbar search was present. The search/pagination HTTP regression and generated CSS breakpoint checks passed. Item 9 was checked live in Light and Dark themes for one H1, readable actions and status badges, and visible keyboard focus. The dark-theme Edit link and input focus ring were corrected during verification. Screenshots were inspected live but were not saved or attached as files.

Use `ASI.Basecode.Tests/UserAdministration`, `BorrowerProfiles` and `UiMigration`. Focus new HTTP assertions on the relocated search/import controls and preserved query/permission behavior; existing import/role tests cover unchanged rules. Rebuild assets. Log this follow-up under Additional work in `doc/PROJECT_MILESTONES.md` without changing other members' WBS rows.


---

## Topic 07 — Equipment image uploads and category forms

### Objective / current gap

Provide a working drag-and-drop/browse picture upload for equipment (JPEG/PNG, maximum **5 MB**), remove the Active category checkbox from create/edit forms, and apply the common inventory filters/headers/status styles.

Reference [Figma Admin board](https://www.figma.com/design/F1NcWYPCQgjVKy0kUd5kI4/campus-equipment?node-id=578-3312), A02/A07–A10. Inspected baseline: `develop@2c1293b`.

The current equipment form only accepts `ImageUrl` text; there is no equipment file-upload pipeline. A drop-zone facade alone is insufficient. `CategoryService.UpdateCategoryAsync` currently assigns `category.IsActive = model.IsActive`, so removing its input without changing that update would silently alter category status.

### Affected modules

- `ASI.Basecode.WebApp/Views/Categories/{Index,Create,Edit,CreateEquipment,EditEquipment,_EquipmentItemFields}.cshtml`.
- `ASI.Basecode.WebApp/Controllers/CategoriesController.cs`; a focused upload helper/service and presentation script if needed.
- `ASI.Basecode.Services/ServiceModels/EquipmentItem/EquipmentItemModels.cs` and `Services/EquipmentItemService.cs`.
- `ASI.Basecode.Services/ServiceModels/Category/CategoryModels.cs` and `Services/CategoryService.cs`.
- `ASI.Basecode.Data/Models/EquipmentItem.cs` has existing `ImageUrl` storage: no blob column/schema migration is expected.
- Existing `ASI.Basecode.Tests/Category` service/HTTP/authorization/concurrency suites.

### Image implementation

1. Add one accessible drop area with a real file input and Browse button, keyboard support, drag state, chosen filename/size, preview and replace/clear selection. It must work with ordinary file selection when drag/drop is unavailable.
2. Specify allowed extensions **.jpg, .jpeg, .png** and MIME **image/jpeg, image/png**. Define 5 MB consistently as **5,000,000 bytes** in this packet, client message and server tests. Reject an empty file and more than one selected file; reject files over the limit before saving. Keep images optional.
3. Add multipart form handling at the WebApp boundary; do not introduce IFormFile dependencies into Data or the domain service layer. Pass a validated persisted relative image path to the existing item service.
4. Validate on the server as well as in the browser: size, permitted extension/content type, signature and actual decodable JPEG/PNG content. Reject renamed text/SVG/non-image files and malformed images. Use bounded decoding/dimension limits to avoid unbounded processing. Choose a maintained decoder only if no existing suitable implementation is available.
5. Store with server-generated unique filenames in a configured persistent image location. Never use a supplied filename as a path. Serve only the validated image content. Reuse `ImageUrl` for the resulting local URL; do not fetch arbitrary remote URLs server-side.
6. Preserve current external/local URL images when editing without a new file. Retain URL support as a clearly secondary optional input if existing workflows need it; define file selection precedence and avoid ambiguous simultaneous updates. Keep the current image visible during invalid-form redisplay.
7. Treat storage and database save failures explicitly. Only update ImageUrl after a valid upload/save sequence; clean up newly created files if the item save fails. Never delete the old image before success, and do not delete shared/external paths. Duplicate/retry submissions must not overwrite another item image or produce duplicate inventory records.
8. Keep authorization, antiforgery, existing item locks, archive/restore rules, validation and audit semantics. Document storage permissions/persistence for the deployment environment; no secret or environment-specific absolute path belongs in source.
9. On validation failure retain ordinary fields, explain file reselection if needed, and do not claim an image uploaded until saved. Show 02's success/error feedback.

### Category checkbox removal

1. Remove the Active checkbox from create/edit modals **and their standalone fallback pages**, including stale JavaScript references to its IDs.
2. Create categories active by default. Metadata edits must preserve the current persisted status on the server, including inactive categories and stale/tampered form submissions.
3. Do not rely on a hidden IsActive value as authority. Narrowly separate metadata editing from activation: preserve persisted status in the service's existing update boundary. Keep the existing explicit SetActive/activate/deactivate action as the status-changing path.
4. Retain activation audit/authorization and existing locks/constraints. Do not auto-activate categories merely because their checkbox is absent.
5. Apply 01's compact filters, one-page-title rule, required labels and semantic badges to category/item tables, forms and modals. Keep separate category/equipment query/page state.

### Acceptance / verification

- [ ] Browse and drag/drop both save a valid JPEG/PNG and display it in catalog/details after reload. HTTP integration tests cover valid JPEG/PNG persistence and serving; Node UI tests cover browse/drop selection and validation. A live drag/drop save-and-reload pass is still needed.
- [x] Exactly 5,000,000 bytes is permitted if otherwise valid; over-limit, zero-byte, wrong-type, forged extension/signature and malformed images fail server-side as well as client-side. Verified by multipart HTTP and browser-script tests.
- [x] Existing image remains on no-file edits, failed saves and unrelated validation errors; temporary failed-upload files do not accumulate. Verified by edit/create HTTP regressions and staged-file cleanup tests.
- [x] Form values survive failure; the UI explains any required file reselection. Verified by failed-edit and missing-staged-upload HTTP regressions.
- [x] Unauthorized upload cannot create a file or change an item. Filenames/path traversal cannot escape the configured storage directory. Verified by authorization HTTP and storage path tests.
- [x] Removing Active from UI does not deactivate active categories or reactivate inactive ones, even with stale/tampered input; the explicit activation action still works. Verified by category HTTP/service tests.
- [x] Locked/borrowed equipment protections and concurrent item/reservation behavior remain unchanged. Category regressions passed; the opt-in LocalDB inventory/reservation concurrency tests passed (2/2).
- [ ] Light/dark, keyboard browse, focus-trapped modals, mobile layout and 10-record filters/pagers work. Light/dark, keyboard focus, modal focus trap, and 10-record pagination have prior/live or automated coverage; a final responsive inventory screenshot pass is still needed.

Verification on November 4, 2026: full Release solution suite with `GEARANTEE_TEST_SQLSERVER=1` passed 426, failed 0, skipped 1 (interactive preview); `npm run css:build` and the UI asset guard passed; `npm run test:ui` passed 6/6; `git diff --check` and JavaScript syntax checks passed. The final live browser retest was interrupted by Escape before the equipment editor opened, so no new upload data was submitted and no screenshots were saved in this pass. Do not treat the two unchecked live UI items as manually signed off.

Use real multipart HTTP tests plus narrow validator/storage tests and existing inventory/category regressions. Do not test uploads against production data. Update `doc/PROJECT_MILESTONES.md` Additional work and deployment/setup documentation for image storage; include actual checks and PR.
