(() => {
    const bell = document.querySelector('[data-notification-bell]');
    if (!bell) return;
    const badge = bell.querySelector('[data-unread-count]');
    const summary = bell.querySelector('summary');
    const status = bell.querySelector('[data-notification-status]');
    const list = bell.querySelector('[data-notification-list]');
    async function get(url) {
        const response = await fetch(url, {credentials: 'same-origin', headers: {'Accept': 'application/json'}});
        if (!response.ok || !response.headers.get('content-type')?.includes('application/json')) throw Error('Unavailable');
        return response.json();
    }
    async function refreshCount() {
        try {
            const {count} = await get(bell.dataset.countUrl);
            badge.textContent = count > 99 ? '99+' : String(count);
            badge.hidden = count === 0;
            summary.setAttribute('aria-label', `Notifications, ${count} unread`);
        } catch {
            badge.hidden = true;
            summary.setAttribute('aria-label', 'Notifications, unread count unavailable');
        }
    }
    bell.addEventListener('toggle', async () => {
        if (!bell.open) return;
        refreshCount();
        status.textContent = 'Loading notifications…';
        list.replaceChildren();
        try {
            const items = await get(bell.dataset.recentUrl);
            status.textContent = items.length ? '' : 'You have no notifications yet.';
            for (const item of items) {
                const row = document.createElement('li');
                row.className = 'notification-preview' + (item.readAt ? '' : ' notification-unread');
                const title = document.createElement('strong');
                title.textContent = item.title;
                const body = document.createElement('p');
                body.className = 'notification-body';
                body.textContent = item.body;
                row.append(title, body);
                if (typeof item.linkUrl === 'string' && /^\/(?![\/\\])/.test(item.linkUrl) && !item.linkUrl.includes('\\')) {
                    const link = document.createElement('a');
                    link.textContent = 'View details';
                    link.className = 'action-edit';
                    link.href = bell.dataset.basePath + item.linkUrl.slice(1);
                    row.append(link);
                }
                list.append(row);
            }
        } catch { status.textContent = 'Notifications could not be loaded. Close and reopen to retry.'; }
    });
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape' && bell.open) { bell.open = false; summary.focus(); }
    });
    document.addEventListener('click', event => { if (bell.open && !bell.contains(event.target)) bell.open = false; });
    refreshCount();
})();
