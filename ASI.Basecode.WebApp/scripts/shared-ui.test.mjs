import {readFileSync} from 'node:fs';
import vm from 'node:vm';
import assert from 'node:assert/strict';
import test from 'node:test';
const source = name => readFileSync(new URL('../wwwroot/js/' + name, import.meta.url), 'utf8');

function theme(saved, dark = false, blocked = false, forceLight = false) {
    const listeners = {};
    const root = {dataset: {forceLight: String(forceLight)}, style: {}};
    const media = {matches: dark, addEventListener: (_, callback) => { listeners.system = callback; }};
    const picker = {value: '', setAttribute: (key, value) => { picker[key] = value; }, addEventListener: (_, callback) => { listeners.pick = callback; }};
    const values = [];
    const document = {
        documentElement: root, querySelector: () => null, querySelectorAll: () => [picker],
        addEventListener: (_, callback) => { listeners.ready = callback; }
    };
    vm.runInNewContext(source('theme.js'), {
        document, window: {matchMedia: () => media},
        localStorage: {
            getItem: () => { if (blocked) throw Error('blocked'); return saved; },
            setItem: (key, value) => { if (blocked) throw Error('blocked'); values.push([key, value]); }
        }
    });
    return {root, media, picker, values, listeners};
}

test('explicit theme persists and toggles without following the OS', () => {
    const ui = theme('dark', false);
    assert.equal(ui.root.dataset.theme, 'dark');
    ui.listeners.ready();
    assert.equal(ui.picker['aria-pressed'], 'true');
    assert.equal(ui.picker['aria-label'], 'Switch to light mode');
    assert.equal(ui.listeners.system, undefined);
    ui.listeners.pick();
    assert.equal(ui.root.dataset.theme, 'light');
    assert.deepEqual(ui.values, [['gearantee-theme', 'light']]);
    ui.listeners.pick();
    assert.equal(ui.root.style.colorScheme, 'dark');
});

test('legacy System, invalid and blocked storage use Light and remain usable', () => {
    for (const ui of [theme('system', true), theme('invalid', true), theme(null, true, true)]) {
        assert.equal(ui.root.dataset.theme, 'light');
        ui.listeners.ready();
        ui.listeners.pick();
        assert.equal(ui.root.dataset.theme, 'dark');
    }
});

test('flash enhancement uses plain text, persistent errors, and keeps fallback on failure', () => {
    const message = {dataset: {flashMessage: 'error'}, textContent: '<img onerror=alert(1)>', hidden: false};
    let options, text;
    const attributes = {};
    const toast = {length: 1, attr: (key, value) => { attributes[key] = value; }, find: () => ({attr: () => {}}), on: () => {}};
    const document = {querySelectorAll: () => [message]};
    const window = {matchMedia: () => ({matches: true}), toastr: {error: (value, title, config) => { text = value; options = config; return toast; }}};
    vm.runInNewContext(source('flash-messages.js'), {document, window});
    assert.equal(text, '<img onerror=alert(1)>');
    assert.equal(options.escapeHtml, true);
    assert.equal(options.timeOut, 0);
    assert.equal(options.showDuration, 0);
    assert.equal(attributes.role, 'alert');
    assert.equal(message.hidden, true);
    message.hidden = false;
    window.toastr.error = () => { throw Error('asset failure'); };
    vm.runInNewContext(source('flash-messages.js'), {document, window});
    assert.equal(message.hidden, false);
    delete window.toastr;
    vm.runInNewContext(source('flash-messages.js'), {document, window});
    assert.equal(message.hidden, false);
});

test('authentication forces light without overwriting a saved workspace theme', () => {
    const ui = theme('dark', true, false, true);
    assert.equal(ui.root.dataset.theme, 'light');
    ui.listeners.ready();
    assert.equal(ui.listeners.pick, undefined);
    assert.deepEqual(ui.values, []);
});
