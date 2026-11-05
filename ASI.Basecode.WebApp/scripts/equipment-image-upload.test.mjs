import {readFileSync} from 'node:fs';
import vm from 'node:vm';
import assert from 'node:assert/strict';
import test from 'node:test';

const source = readFileSync(new URL('../wwwroot/js/equipment-image-upload.js', import.meta.url), 'utf8');

function uploadUi() {
    const inputListeners = {};
    const dropListeners = {};
    let inputValue = '';
    const input = {
        files: [], attributes: {}, customValidity: '',
        get value() { return inputValue; },
        set value(value) { inputValue = value; if (value === '') input.files = []; },
        addEventListener: (name, callback) => { inputListeners[name] = callback; },
        setCustomValidity: value => { input.customValidity = value; },
        setAttribute: (name, value) => { input.attributes[name] = value; },
        removeAttribute: name => { delete input.attributes[name]; }
    };
    const dropZone = {
        classes: new Set(),
        classList: {
            add(name) { dropZone.classes.add(name); },
            remove(name) { dropZone.classes.delete(name); }
        },
        addEventListener: (name, callback) => { (dropListeners[name] ??= []).push(callback); }
    };
    const status = {textContent: ''};
    const error = {textContent: ''};
    const previewContainer = {hidden: true};
    const preview = {src: ''};
    const caption = {textContent: ''};
    const uploadedUrl = {value: ''};
    const currentImageUrl = {value: ''};
    const removeImage = {checked: false, addEventListener() {}};
    const imageActions = {hidden: true};
    const root = {
        querySelector(selector) {
            return ({
                "input[type='file']": input,
                '[data-drop-zone]': dropZone,
                '#image-upload-status': status,
                '[data-upload-error]': error,
                '[data-image-preview-container]': previewContainer,
                '[data-image-preview]': preview,
                '[data-image-preview-caption]': caption,
                "input[name='UploadedImageUrl']": uploadedUrl,
                "input[name='CurrentImageUrl']": currentImageUrl,
                "input[name='RemoveImage'][type='checkbox']": removeImage,
                '[data-image-actions]': imageActions,
                '[data-clear-image]': null
            })[selector] ?? null;
        }
    };
    class FakeDataTransfer {
        constructor() {
            this.files = [];
            this.items = {add: file => this.files.push(file)};
        }
    }
    const url = {createObjectURL: () => 'blob:equipment-test', revokeObjectURL() {}};
    const document = {querySelectorAll: () => [root]};
    vm.runInNewContext(source, {
        document,
        URL: url,
        DataTransfer: FakeDataTransfer,
        createImageBitmap: async file => {
            if (file.decodeFails) throw new Error('decode failed');
            return {width: file.width ?? 2, height: file.height ?? 2, close() {}};
        }
    });
    return {input, inputListeners, dropZone, dropListeners, status, error, previewContainer, preview, caption, imageActions};
}

function imageFile({name = 'camera.png', type = 'image/png', size = 100, bytes, decodeFails = false} = {}) {
    const signature = bytes ?? [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];
    return {
        name, type, size, decodeFails,
        slice: (start, end) => ({arrayBuffer: async () => Uint8Array.from(signature.slice(start, end)).buffer})
    };
}

async function selectByBrowse(ui, file) {
    ui.input.files = [file];
    await ui.inputListeners.change();
}

async function drop(ui, files) {
    const event = {dataTransfer: {files}, preventDefault() {}};
    for (const callback of ui.dropListeners.drop) await callback(event);
    await new Promise(resolve => setImmediate(resolve));
}

test('browse accepts a valid PNG and presents its preview', async () => {
    const ui = uploadUi();
    await selectByBrowse(ui, imageFile());
    assert.equal(ui.error.textContent, '');
    assert.equal(ui.previewContainer.hidden, false);
    assert.equal(ui.caption.textContent, 'New image preview');
    assert.equal(ui.imageActions.hidden, false);
});

test('drop accepts a valid JPEG and rejects multiple files', async () => {
    const ui = uploadUi();
    const jpeg = imageFile({name: 'camera.jpeg', type: 'image/jpeg', bytes: [0xff, 0xd8, 0xff, 0x00]});
    await drop(ui, [jpeg]);
    assert.equal(ui.error.textContent, '');
    assert.equal(ui.input.files[0], jpeg);
    assert.equal(ui.previewContainer.hidden, false);

    await drop(ui, [jpeg, jpeg]);
    assert.match(ui.error.textContent, /exactly one/);
    assert.deepEqual(ui.input.files, []);
});

test('browser validation rejects empty, oversized, forged, and undecodable images', async () => {
    for (const [file, expected] of [
        [imageFile({size: 0}), /empty/],
        [imageFile({size: 5_000_001}), /5,000,000 bytes/],
        [imageFile({name: 'renamed.jpg', type: 'image/jpeg'}), /do not match/],
        [imageFile({decodeFails: true}), /not a decodable/]
    ]) {
        const ui = uploadUi();
        await selectByBrowse(ui, file);
        assert.match(ui.error.textContent, expected);
        assert.equal(ui.previewContainer.hidden, true);
    }
});
