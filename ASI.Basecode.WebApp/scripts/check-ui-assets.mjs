import {readFileSync} from 'node:fs';
import assert from 'node:assert/strict';
const css = readFileSync(new URL('../wwwroot/css/app.css', import.meta.url), 'utf8');
// A restricted scanner can report success while emitting no utilities.
for (const utility of ['font-sans', 'sr-only', 'flex', 'hidden']) {
    assert.match(css, new RegExp('\\.' + utility + '\\s*\\{'), 'Missing ' + utility + ': verify Tailwind can read Views and wwwroot/js.');
}
console.log('UI asset check: font, accessibility, layout and visibility utilities present.');
