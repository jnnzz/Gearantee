import {readFileSync} from 'node:fs';
import vm from 'node:vm';
import assert from 'node:assert/strict';
import test from 'node:test';

function target(extra = {}) {
    const listeners = {};
    return {...extra, addEventListener(type, fn) { (listeners[type] ??= []).push(fn); },
        emit(type, extra = {}) {
            const event = {target: this, defaultPrevented: false, preventDefault() { this.defaultPrevented = true; }, ...extra};
            for (const fn of listeners[type] ?? []) fn(event);
            return event;
        }};
}
function setup(count = 1) {
    const rows = Array.from({length: count}, (_, i) => {
        const accept = target({disabled: false});
        const dismiss = target();
        const detail = {textContent: 'Item ' + i};
        const dialog = target({open: false, showModal() { this.open = true; }, close() { this.open = false; },
            querySelector: selector => ({'[data-confirm-accept]': accept, '[data-confirm-dismiss]': dismiss, '[data-confirm-detail]': detail})[selector]});
        const form = target({dataset: {confirmDialog: String(i)}, valid: true, jqueryValid: true, posts: 0, summary: [],
            reportValidity() { return this.valid; }, querySelectorAll() { return this.summary; },
            requestSubmit(submitter) {
                if (!this.valid) return;
                const event = this.emit('submit', {submitter});
                if (!event.defaultPrevented) { this.posts++; this.lastSubmitter = submitter; }
            }});
        return {form, dialog, accept, dismiss, detail};
    });
    const window = target({jQuery: Object.assign(form => ({valid: () => form.jqueryValid}), {validator: {}})});
    vm.runInNewContext(readFileSync(new URL('../wwwroot/js/confirm-dialog.js', import.meta.url), 'utf8'), {
        document: {querySelectorAll: () => rows.map(r => r.form), getElementById: id => rows[Number(id)].dialog}, window
    });
    return {rows, window};
}
test('cancel and backdrop never post; reopening confirms exactly the selected row and preserves submitter', () => {
    const {rows} = setup(2);
    const row = rows[1], button = {name: 'action', value: 'delete'};
    row.form.requestSubmit(button);
    assert.equal(row.dialog.open, true);
    assert.equal(row.form.posts, 0);
    row.dismiss.emit('click');
    assert.equal(row.dialog.open, false);
    row.form.requestSubmit(button);
    row.dialog.emit('click');
    assert.equal(row.form.posts, 0);
    row.form.requestSubmit(button);
    row.accept.emit('click');
    row.accept.emit('click');
    row.form.requestSubmit(button);
    assert.equal(row.form.posts, 1);
    assert.equal(row.form.lastSubmitter, button);
    assert.equal(rows[0].form.posts, 0);
});
test('native and unobtrusive validation prevent confirmation; valid retry succeeds', () => {
    const {rows: [row]} = setup();
    row.form.valid = false;
    row.form.emit('submit');
    assert.equal(row.dialog.open, false);
    row.form.valid = true;
    row.form.jqueryValid = false;
    row.form.requestSubmit();
    assert.equal(row.dialog.open, false);
    row.form.jqueryValid = true;
    row.form.requestSubmit();
    row.form.valid = false;
    row.accept.emit('click');
    assert.equal(row.form.posts, 0);
    assert.equal(row.accept.disabled, false);
    row.form.valid = true;
    row.form.requestSubmit();
    row.accept.emit('click');
    assert.equal(row.form.posts, 1);
});
test('later submit cancellation does not leave the form permanently confirmed or disabled', () => {
    const {rows: [row]} = setup();
    let block = true;
    row.form.addEventListener('submit', event => { if (block) event.preventDefault(); });
    row.form.requestSubmit();
    row.accept.emit('click');
    assert.equal(row.form.posts, 0);
    assert.equal(row.accept.disabled, false);
    block = false;
    row.form.requestSubmit();
    assert.equal(row.dialog.open, true);
    row.accept.emit('click');
    assert.equal(row.form.posts, 1);
});
test('return summary refreshes safely and restored pages can submit again', () => {
    const {rows: [row], window} = setup();
    row.form.summary = [{dataset: {confirmSummary: 'Condition'}, selectedOptions: [{textContent: '<Damaged>'}]}];
    row.form.requestSubmit();
    assert.equal(row.detail.textContent, 'Item 0\nCondition: <Damaged>');
    row.dismiss.emit('click');
    row.form.summary[0].selectedOptions[0].textContent = 'Good';
    row.form.requestSubmit();
    assert.equal(row.detail.textContent, 'Item 0\nCondition: Good');
    row.accept.emit('click');
    window.emit('pageshow');
    row.form.requestSubmit();
    assert.equal(row.dialog.open, true);
    row.accept.emit('click');
    assert.equal(row.form.posts, 2);
});
