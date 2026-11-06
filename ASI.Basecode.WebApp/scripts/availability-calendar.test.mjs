import {readFileSync} from 'node:fs';
import vm from 'node:vm';
import test from 'node:test';
import assert from 'node:assert/strict';

const source = readFileSync(new URL('../wwwroot/js/availability-calendar.js', import.meta.url), 'utf8');
class Element {
    children = []; dataset = {}; attributes = {}; listeners = {}; value = ''; className = ''; textContent = '';
    style = {setProperty: (key, value) => { this.style[key] = value; }};
    classList = {add: () => {}, remove: () => {}, toggle: () => {}};
    append(...children) { this.children.push(...children); }
    replaceChildren(...children) { this.children = children; }
    setAttribute(key, value) { this.attributes[key] = value; }
    addEventListener(key, callback) { this.listeners[key] = callback; }
}
async function calendar({reserve = true, view = 'overview', fail = false} = {}) {
    const ids = Object.fromEntries(['availability-calendar', 'availability-filters', 'availability-overview', 'availability-date', 'availability-view', 'availability-loading', 'availability-error', 'availability-retry', 'calendar-period-label'].map(id => [id, new Element()]));
    ids['availability-calendar'].dataset = {eventsUrl: '/AvailabilityCalendar/Events', reservationUrl: '/Reservation/Create', canReserve: reserve ? 'True' : 'False', today: '2026-11-06'};
    ids['availability-date'].value = '2026-11-09';
    ids['availability-date'].dataset = {min: '0100-01-04', max: '9999-12-26'};
    ids['availability-view'].value = view;
    const data = {items: [
        {equipmentId: 1, itemName: '<script>camera</script>', itemCode: 'CAM-001-CAN', canBeReserved: true, operationalStatus: 'Available'},
        {equipmentId: 2, itemName: 'Laptop', itemCode: 'LAP-001-ASU', canBeReserved: false, operationalStatus: 'UnderMaintenance'}
    ], events: [
        {equipmentId: 1, status: 'Reserved', start: '2026-11-10T00:00:00+08:00', end: '2026-11-12T00:00:00+08:00'},
        {equipmentId: 1, status: 'Borrowed', start: '2026-11-10T12:00:00+08:00', end: '2026-11-11T00:00:00+08:00'}
    ]};
    let requestUrl;
    const context = {document: {getElementById: id => ids[id], querySelectorAll: () => [], createElement: () => new Element()},
        location: {origin: 'http://localhost'}, URL, URLSearchParams, Intl, Date, AbortController,
        FormData: class { *[Symbol.iterator]() { yield ['Search', 'camera']; } },
        fetch: async url => { requestUrl = url; if (fail) throw Error('offline'); return {ok: true, json: async () => data}; }
    };
    vm.runInNewContext(source, context);
    await new Promise(resolve => setImmediate(resolve));
    return {ids, requestUrl};
}
test('week grid spans inclusive dates, stacks overlaps and only links open days', async () => {
    const {ids, requestUrl} = await calendar();
    assert.equal(requestUrl.searchParams.get('start'), '2026-11-09T00:00:00+08:00');
    assert.equal(requestUrl.searchParams.get('end'), '2026-11-16T00:00:00+08:00');
    const rows = ids['availability-overview'].children;
    assert.equal(rows[1].children[0].children[0].textContent, '<script>camera</script>');
    const cells = rows[1].children[1].children;
    assert.equal(new URL(cells[0].href).searchParams.get('to'), '2026-11-09');
    assert.equal(cells[1].href, undefined);
    assert.equal(cells[2].href, undefined);
    assert.ok(cells[3].href);
    assert.equal(cells[7].style.gridColumn, '2 / 4');
    assert.notEqual(cells[7].style.gridRow, cells[8].style.gridRow);
    assert.equal(rows[2].children[1].children[0].href, undefined);
    assert.match(rows[2].children[1].children.at(-1).textContent, /completion date unknown/);
});
test('Day fetches a single day and unauthorized viewers cannot start reservations', async () => {
    const {ids, requestUrl} = await calendar({reserve: false, view: 'timeGridDay'});
    assert.equal(requestUrl.searchParams.get('end'), '2026-11-10T00:00:00+08:00');
    assert.equal(ids['availability-overview'].children[1].children[1].children[0].href, undefined);
});
test('failed fetch leaves no clickable stale availability and exposes retry', async () => {
    const {ids} = await calendar({fail: true});
    assert.equal(ids['availability-overview'].children.length, 0);
    assert.match(ids['availability-error'].textContent, /offline/);
    assert.equal(typeof ids['availability-retry'].listeners.click, 'function');
});
