import {readFileSync} from 'node:fs';
import vm from 'node:vm';
import assert from 'node:assert/strict';
import test from 'node:test';
const source = name => readFileSync(new URL('../wwwroot/js/' + name, import.meta.url), 'utf8');

function theme(saved, dark = false, blocked = false) {
    const listeners = {};
    const root = {dataset: {}, style: {}};
    const media = {matches: dark, addEventListener: (_, callback) => { listeners.system = callback; }};
    const picker = {value: '', addEventListener: (_, callback) => { listeners.pick = callback; }};
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

test('saved theme applies before DOM ready; OS changes only affect System', () => {
    const ui = theme('light', true);
    assert.equal(ui.root.dataset.theme, 'light');
    ui.listeners.ready();
    assert.equal(ui.picker.value, 'light');
    ui.media.matches = false;
    ui.listeners.system();
    assert.equal(ui.root.dataset.theme, 'light');
    ui.picker.value = 'dark';
    ui.listeners.pick();
    assert.equal(ui.root.dataset.theme, 'dark');
    assert.deepEqual(ui.values, [['gearantee-theme', 'dark']]);
    ui.picker.value = 'system';
    ui.listeners.pick();
    assert.equal(ui.root.dataset.theme, 'light');
    ui.media.matches = true;
    ui.listeners.system();
    assert.equal(ui.root.dataset.theme, 'dark');
    assert.equal(ui.root.style.colorScheme, 'dark');
});

test('invalid or inaccessible storage falls back to System without breaking the picker', () => {
    for (const ui of [theme('invalid', true), theme(null, true, true)]) {
        assert.equal(ui.root.dataset.theme, 'dark');
        ui.listeners.ready();
        ui.picker.value = 'light';
        ui.listeners.pick();
        assert.equal(ui.root.dataset.theme, 'light');
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
