// Phase 4–6 presentation audit, anchored to the completed phase 0–3 handoff.
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import crypto from 'node:crypto';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const baseline = '8ee9b78f29a5dbc92bd21c38fb5ca921b0eabad7';
const git = args => execFileSync('git', args, {cwd:root, encoding:'utf8'});
const staged = process.argv.includes('--staged');
const read = path => (staged ? git(['show',`:${path}`]) : readFileSync(resolve(root,path),'utf8')).replaceAll('\r\n','\n');
const paths = git(['ls-tree','-r','--name-only',baseline,'--','ASI.Basecode.WebApp/Views']).trim().split('\n').filter(path=>path.endsWith('.cshtml'));
// Read all baseline blobs in one Git process, preserving their exact byte sizes.
const blobs = execFileSync('git',['cat-file','--batch'],{cwd:root,input:paths.map(path=>`${baseline}:${path}\n`).join(''),maxBuffer:16*1024*1024});
const before = new Map();
let offset=0;
for(const path of paths){
    const end=blobs.indexOf(10,offset);
    const header=blobs.subarray(offset,end).toString('utf8').split(' ');
    assert.equal(header[1],'blob',path);
    const size=Number(header[2]);
    before.set(path,blobs.subarray(end+1,end+1+size).toString('utf8').replaceAll('\r\n','\n'));
    offset=end+1+size+1;
}
function contracts(source){
    return [...source.matchAll(/\b(asp-(?:for|action|controller|route-[\w-]+|validation-for|validation-summary|format)|method|name|id|for|data-[\w-]+|type|value|disabled|checked|selected|maxlength|min|max|enctype|autocomplete|onsubmit|onclick)="([^"]*)"/g)]
        .map(([,attribute,value])=>`${attribute}=${value}`).sort();
}
function models(source){
    return [...source.matchAll(/\b(?:Model|loan|task|category|user|reservation|borrower|item|alert|filter|permission|matrix|role|error)\.[A-Za-z_][\w.]*/g)].map(([value])=>value).sort();
}
const addedScript='<script src="~/js/management-modals.js" asp-append-version="true"></script>';
for(const path of paths){
    const original=before.get(path);
    let current=read(path);
    if(path.endsWith('/Users/Create.cshtml')){
        // Repair the existing label mismatch without changing the select's name/id or binding.
        assert.equal(current.split('asp-for="RoleName" for="initial-role"').length-1,1);
        current=current.replace('asp-for="RoleName" for="initial-role"','asp-for="RoleName"');
    }
    if(path.endsWith('/Categories/Index.cshtml')){
        assert.equal(current.split(addedScript).length-1,1,'Inventory dialog accessibility script missing or duplicated');
        current=current.replace(addedScript,'');
    }
    assert.deepEqual(contracts(current),contracts(original),`${path}: form, field, route or event contract changed`);
    assert.deepEqual(models(current),models(original),`${path}: model content or conditional binding changed`);
    const scripts=source=>[...source.matchAll(/<script\b[\s\S]*?<\/script>/g)].map(([script])=>script);
    assert.deepEqual(scripts(current),scripts(original),`${path}: existing inline or external script changed`);
    const guards=source=>[...source.matchAll(/@(?:if|foreach|for|switch)\s*\([^\n]*/g)].map(([line])=>line.trim()).sort();
    assert.deepEqual(guards(current),guards(original),`${path}: permission, condition or loop changed`);
}
const protectedPaths=['ASI.Basecode.Data','ASI.Basecode.Services','ASI.Basecode.WebApp/Controllers','ASI.Basecode.WebApp/Models','ASI.Basecode.WebApp/Data','ASI.Basecode.WebApp/Program.cs','ASI.Basecode.WebApp/Startup.cs','ASI.Basecode.WebApp/Startup.Auth.cs','ASI.Basecode.WebApp/Startup.DI.cs','ASI.Basecode.WebApp/wwwroot/js/app-shell.js','ASI.Basecode.WebApp/wwwroot/js/availability-calendar.js','ASI.Basecode.WebApp/wwwroot/js/confirm-dialog.js'];
const changes=git(['diff',...(staged?['--cached']:[]),baseline,'--name-only','--',...protectedPaths]).trim().split('\n').filter(Boolean);
const flag=process.argv.indexOf('--local-baseline');
assert.ok(!(staged && flag>=0),'Staged validation cannot exempt local working-tree edits.');
// October 27 review fix: the earlier local lockout relaxation was explicitly reverted.
// Require the full baseline authentication file, even when an older local snapshot is supplied.
const restoredAuth='ASI.Basecode.WebApp/Startup.Auth.cs';
assert.equal(read(restoredAuth),git(['show',`${baseline}:${restoredAuth}`]).replaceAll('\r\n','\n'),'Authentication must retain the original five-attempt, fifteen-minute lockout configuration.');
let localPaths=[];
if(flag>=0){
    const manifest=JSON.parse(read(process.argv[flag+1]));
    assert.equal(manifest.base,baseline);
    const allowed=['ASI.Basecode.WebApp/Startup.Auth.cs','ASI.Basecode.WebApp/Views/Account/Login.cshtml'];
    assert.deepEqual(manifest.localEdits.map(entry=>entry.path).sort(),allowed.sort());
    for(const entry of manifest.localEdits){
        if(entry.path===restoredAuth)continue; // verified against the stricter baseline above
        assert.equal(crypto.createHash('sha256').update(readFileSync(resolve(root,entry.path))).digest('hex'),entry.hash,`${entry.path}: separate local edit changed during migration`);
    }
    localPaths=manifest.localEdits.map(entry=>entry.path).filter(path=>path!==restoredAuth);
}
// October 26 follow-up: the user explicitly authorized asynchronous CSV writer disposal.
// Compare the entire controller so this exception cannot conceal any other backend edit.
const csvController='ASI.Basecode.WebApp/Controllers/BorrowingHistoryController.cs';
const originalCsv=git(['show',`${baseline}:${csvController}`]).replaceAll('\r\n','\n');
const syncWriter='            using var writer = new StreamWriter(';
assert.equal(originalCsv.split(syncWriter).length-1,1);
assert.equal(read(csvController),originalCsv.replace(syncWriter,'            await using var writer = new StreamWriter('),'CSV controller change exceeds the authorized disposal fix.');
assert.deepEqual(changes.filter(path=>!localPaths.includes(path) && path!==csvController),[],'Production business, security, scheduling, model or persistence code changed.');
const modalScript=read('ASI.Basecode.WebApp/wwwroot/js/management-modals.js');
assert.ok(!/\bfetch\s*\(|XMLHttpRequest|\.submit\s*\(|\.value\s*=/.test(modalScript),'Dialog accessibility script may not submit forms, call APIs or change field values.');
const css=read('ASI.Basecode.WebApp/wwwroot/css/tailwind.src.css');
const baseCss=git(['show',`${baseline}:ASI.Basecode.WebApp/wwwroot/css/tailwind.src.css`]);
assert.equal(css.match(/@theme\s*{[\s\S]*?\n}/)?.[0],baseCss.match(/@theme\s*{[\s\S]*?\n}/)?.[0],'Existing theme tokens changed.');
const compiled=read('ASI.Basecode.WebApp/wwwroot/css/app.css');
for(const selector of ['.role-summary','.operations-grid','.management-tabs','.management-modal-card','.pagination'])assert.ok(compiled.includes(selector),`Missing generated ${selector} styling`);
console.log(`PASS (${staged?'staged':'working tree'}): ${paths.length} view contracts, model bindings, guards and existing scripts preserved; authorized CSV disposal fix verified exactly; other production workflows/theme unchanged; ${localPaths.length} separate local edit${localPaths.length===1?'':'s'} preserved.`);
