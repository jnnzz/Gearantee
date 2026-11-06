// Verify the migration and approved follow-ups. --staged checks only the commit.
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const staged = process.argv.includes('--staged');
const baseline = process.argv.slice(2).find(value => !value.startsWith('--')) ?? '6803c83d6a2aea89e684ca991fa67868fc593d59';
const git = args => execFileSync('git', args, { cwd: root, encoding: 'utf8' });
const read = path => (staged ? git(['show', `:${path}`]) : readFileSync(resolve(root, path), 'utf8')).replaceAll('\r\n', '\n');
const original = path => git(['show', `${baseline}:${path}`]).replaceAll('\r\n', '\n');
const escape = value => value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
const sorted = values => [...new Set(values.map(value => value.toLowerCase()))].sort();
const pagers = [
  ['Catalog/Index', 'Catalog pages', 'page', ['search', 'categoryId', 'from', 'to']],
  ['Reservation/Index', 'Reservation pages', 'page', ['status']],
  ['ReservationReview/Index', 'Review pages', 'page', ['status', 'search']],
  ['ReturnRecord/Index', 'Return pages', 'page', ['tab']],
  ['BorrowingHistory/Index', 'Borrowing history pages', 'page', ['search', 'borrower', 'item', 'categoryId', 'fromDate', 'toDate', 'status', 'lateReturn']],
  ['Users/Index', 'User account pages', 'page', ['search', 'status', 'role']],
  ['BorrowerProfile/Index', 'Borrower profile pages', 'page', ['search']],
  ['Categories/Index', 'Equipment item pages', 'equipmentPage', ['tab', 'equipmentSearch', 'equipmentStatus', 'equipmentCategoryId']],
  ['Categories/Index', 'Category pages', 'categoryPage', ['tab', 'categorySearch', 'categoryStatus']],
];
const views = [...new Set([
  'Account/Login', 'Account/ForgotPassword', 'Account/ForgotPasswordCheckInbox',
  'Account/ResetPassword', 'Account/Register', 'Account/AccessDenied',
  'Dashboard/Borrower', 'Catalog/Index', 'Catalog/Details',
  'Reservation/Create', 'Reservation/Index', 'Reservation/Details',
  'AvailabilityCalendar/Index', 'Shared/_Sidebar', 'Shared/_Topbar',
  'Shared/Components/_ConfirmDialog', ...pagers.map(([view]) => view),
])];
function contracts(source) {
  const attributes = [...source.matchAll(/\b(asp-(?:for|action|controller|route-[\w-]+|validation-for|validation-summary)|method|data-[\w-]+)="([^"]*)"/g)]
    .map(([, name, value]) => `${name}=${value}`);
  const names = [...source.matchAll(/<(?:input|select|textarea)\b[^>]*\bname="([^"]*)"/g)].map(([, value]) => `name=${value}`);
  return [...attributes, ...names].sort();
}
for (const view of views) {
  const path = `ASI.Basecode.WebApp/Views/${view}.cshtml`;
  let before = original(path);
  let after = read(path);
  if (view === 'Shared/_Sidebar') {
    const shortcuts = /<a\b(?=[^>]*\bclass="role-switch")[^>]*>[\s\S]*?<\/a>/g;
    assert.equal([...before.matchAll(shortcuts)].length, 3);
    assert.ok(!after.includes('role-switch'), 'The removed demo role switcher returned.');
    before = before.replace(shortcuts, '');
  }
  const specifications = pagers.filter(([target]) => target === view);
  const pattern = /<partial name="Components\/_Pagination" model="@\([\s\S]*?\)"\s*\/>/g;
  const components = [...after.matchAll(pattern)].map(([component]) => component);
  assert.equal(components.length, specifications.length, `${view}: shared pager count changed`);
  for (const [, label, parameter, filters] of specifications) {
    let oldPager;
    if (view === 'BorrowerProfile/Index') {
      const offset = before.lastIndexOf('@if (Model.TotalPages > 1)');
      assert.ok(offset > 0);
      oldPager = before.slice(offset);
      before = before.slice(0, offset);
    } else {
      const oldPattern = new RegExp(`<nav\\b[^>]*aria-label="${escape(label)}"[\\s\\S]*?<\\/nav>`, 'g');
      const matches = [...before.matchAll(oldPattern)];
      assert.equal(matches.length, 1, `${view}: original pager not found`);
      oldPager = matches[0][0];
      before = before.replace(oldPattern, '');
    }
    const oldKeys = [...oldPager.matchAll(/\b(?:asp-route-([\w-]+)|name="([^"]+)")/g)].map(match => match[1] ?? match[2]);
    assert.deepEqual(sorted(oldKeys), sorted([...filters, parameter]), `${label}: baseline filter contract changed`);
    const component = components.find(value => value.includes(`Label = "${label}"`));
    assert.ok(component, `${label}: pager label missing`);
    const newKeys = [...component.matchAll(/\["([^"]+)"\]\s*=/g)].map(([, key]) => key);
    assert.deepEqual(sorted(newKeys), sorted(filters), `${label}: pagination filters changed`);
    assert.equal(component.match(/PageParameter\s*=\s*"([^"]+)"/)?.[1] ?? 'page', parameter, `${label}: page parameter changed`);
  }
  after = after.replace(pattern, '');
  assert.deepEqual(contracts(after), contracts(before), `${view}: form, route, validation or script contract changed`);
  const hidden = source => [...source.matchAll(/<input\b[^>]*type="hidden"[^>]*>/g)]
    .map(([tag]) => tag.replace(/\sclass="[^"]*"/g, '').replace(/\s+/g, ' ').trim()).sort();
  assert.deepEqual(hidden(after), hidden(before), `${view}: hidden input changed`);
}
// Service changes are limited to page-size declarations and required imports.
const names = ['BorrowerProfile', 'BorrowingHistory', 'Catalog', 'Category', 'EquipmentItem', 'Reservation', 'ReturnRecord', 'UserAdministration'];
const allowed = names.map(name => `ASI.Basecode.Services/Services/${name}Service.cs`);
for (const path of allowed) {
  const before = original(path);
  assert.equal([...before.matchAll(/(?:public|private) const int PageSize = \d+;/g)].length, 1);
  let expected = before.replace(/((?:public|private) const int PageSize = )\d+;/, '$1PaginationDefaults.PageSize;');
  if (!expected.includes('using ASI.Basecode.Services.Utilities;')) expected = 'using ASI.Basecode.Services.Utilities;\n' + expected;
  assert.equal(read(path), expected, `${path}: change extends beyond the approved page limit`);
}
const defaults = 'ASI.Basecode.Services/Utilities/PaginationDefaults.cs';
assert.equal(read(defaults).replace(/\s/g, ''), 'namespaceASI.Basecode.Services.Utilities{publicstaticclassPaginationDefaults{publicconstintPageSize=10;}}');
const protectedPaths = [
  'ASI.Basecode.Data', 'ASI.Basecode.Services', 'ASI.Basecode.WebApp/Controllers',
  'ASI.Basecode.WebApp/Startup.cs', 'ASI.Basecode.WebApp/Startup.Auth.cs',
  'ASI.Basecode.WebApp/Startup.DI.cs', 'ASI.Basecode.WebApp/Program.cs',
  'ASI.Basecode.WebApp/wwwroot/js/confirm-dialog.js',
];
const changes = git(['diff', ...(staged ? ['--cached'] : []), baseline, '--name-only', '--', ...protectedPaths]).trim().split('\n').filter(Boolean);
assert.deepEqual(changes.filter(path => ![...allowed, defaults].includes(path)), [], 'A protected workflow, authentication, scheduling or persistence implementation changed.');
const calendar = 'ASI.Basecode.WebApp/wwwroot/js/availability-calendar.js';
const appearance = /        \/\/ UI appearance only:[\s\S]*?        eventInnerClass: \(\) => "availability-event-inner",\n/;
assert.equal(read(calendar).replace(appearance, ''), original(calendar), 'Calendar changes extend beyond the approved appearance callbacks.');
const css = read('ASI.Basecode.WebApp/wwwroot/css/app.css');
for (const selector of ['.flex{', '.hidden{', '.size-4{', 'lg\\:flex', '.input{', '.surface{', '.pagination{']) {
  assert.ok(css.includes(selector), `Generated CSS is missing ${selector}; rebuild the stylesheet.`);
}
console.log(`PASS: ${views.length} view contracts, ${pagers.length} pager filter contracts, 10-record limits and protected workflows (${staged ? 'staged' : 'working tree'}).`);
