// Presentation only: retain the existing inventory modal open/close and POST handlers.
(() => {
    const modals = [...document.querySelectorAll('.management-modal')];
    if (!modals.length) return;
    let active = null;
    let returnFocus = null;
    let wasScrollLocked = false;
    const controls = modal => [...modal.querySelectorAll('a[href], button:not([disabled]), input:not([disabled]):not([type="hidden"]), select:not([disabled]), textarea:not([disabled]), [tabindex="0"]')]
        .filter(element => element.getClientRects().length > 0);
    const sync = () => {
        const open = modals.find(modal => !modal.classList.contains('hidden')) ?? null;
        if (open === active) return;
        const previous = active;
        active = open;
        if (open) {
            if (!previous) {
                returnFocus = document.activeElement;
                wasScrollLocked = document.body.classList.contains('overflow-hidden');
            }
            document.body.classList.add('overflow-hidden');
            const firstField = open.querySelector('input:not([type="hidden"]):not([disabled]), textarea:not([disabled])');
            (firstField ?? controls(open)[0])?.focus();
        } else if (previous) {
            if (!wasScrollLocked) document.body.classList.remove('overflow-hidden');
            if (returnFocus?.isConnected) returnFocus.focus();
        }
    };
    const observer = new MutationObserver(sync);
    modals.forEach(modal => observer.observe(modal, { attributes: true, attributeFilter: ['class'] }));
    document.addEventListener('keydown', event => {
        if (event.key !== 'Tab' || !active) return;
        const items = controls(active);
        const first = items[0];
        const last = items[items.length - 1];
        if (!active.contains(document.activeElement) || (event.shiftKey && document.activeElement === first)) {
            event.preventDefault();
            (event.shiftKey ? last : first)?.focus();
        } else if (!event.shiftKey && document.activeElement === last) {
            event.preventDefault();
            first?.focus();
        }
    });
    sync();
})();
