import {readFileSync} from 'node:fs';
import vm from 'node:vm';
import assert from 'node:assert/strict';
import test from 'node:test';

const source = readFileSync(new URL('../wwwroot/js/notifications.js', import.meta.url), 'utf8');
const flush = () => new Promise(resolve => setImmediate(resolve));
function host(fail = false) {
    class Element {
        children = []; attributes = {}; hidden = false;
        append(...items) { this.children.push(...items); }
        replaceChildren() { this.children = []; }
        setAttribute(key, value) { this.attributes[key] = value; }
        focus() { this.focused = true; }
    }
    const badge = new Element(), summary = new Element(), status = new Element(), list = new Element();
    const listeners = {};
    const bell = {
        dataset: {countUrl: '/count', recentUrl: '/recent', basePath: '/campus/'}, open: false,
        querySelector: name => ({'[data-unread-count]': badge, 'summary': summary, '[data-notification-status]': status, '[data-notification-list]': list})[name],
        addEventListener: (name, fn) => { listeners[name] = fn; }, contains: () => false
    };
    const document = {querySelector: () => bell, createElement: () => new Element(),
        addEventListener: (name, fn) => { listeners[name] = fn; }};
    const fetch = async url => {
        if (fail) throw Error('offline');
        return {ok: true, headers: {get: () => 'application/json'}, json: async () => url === '/count' ? {count: 2} : [
            {title: '<script>alert(1)</script>', body: '<img onerror=alert(1)>', linkUrl: '//evil.invalid', readAt: null},
            {title: 'Approved', body: 'Your equipment', linkUrl: '/Reservation/Details/1', readAt: null}
        ]};
    };
    vm.runInNewContext(source, {document, fetch});
    return {bell, badge, summary, status, list, listeners};
}

test('bell renders untrusted content as text, rejects external links and supports Escape', async () => {
    const ui = host();
    await flush();
    assert.equal(ui.badge.textContent, '2');
    ui.bell.open = true;
    await ui.listeners.toggle();
    assert.equal(ui.list.children[0].children[0].textContent, '<script>alert(1)</script>');
    assert.equal(ui.list.children[0].children.length, 2);
    assert.equal(ui.list.children[1].children[2].href, '/campus/Reservation/Details/1');
    ui.listeners.keydown({key: 'Escape'});
    assert.equal(ui.bell.open, false);
    assert.equal(ui.summary.focused, true);
});

test('failed fetch hides stale unread count and offers retry without fake data', async () => {
    const ui = host(true);
    await flush();
    assert.equal(ui.badge.hidden, true);
    assert.match(ui.summary.attributes['aria-label'], /unavailable/);
    ui.bell.open = true;
    await ui.listeners.toggle();
    assert.match(ui.status.textContent, /reopen to retry/);
    assert.equal(ui.list.children.length, 0);
});
