(() => {
    const toggle = document.getElementById("sidebar-toggle");
    const sidebar = document.getElementById("app-sidebar");
    const backdrop = document.getElementById("sidebar-backdrop");

    if (!toggle || !sidebar || !backdrop) return;

    const focusableElements = () => [...sidebar.querySelectorAll('a[href], button:not([disabled]), input:not([disabled]), [tabindex="0"]')]
        .filter(element => element.getClientRects().length > 0);

    const setOpen = (open) => {
        toggle.setAttribute("aria-expanded", String(open));
        toggle.setAttribute("aria-label", open ? "Close navigation" : "Open navigation");
        sidebar.classList.toggle("-translate-x-full", !open);
        sidebar.inert = !open && window.innerWidth < 1024;
        backdrop.classList.toggle("hidden", !open);
        document.body.classList.toggle("overflow-hidden", open);
        if (open && window.innerWidth < 1024) {
            focusableElements()[0]?.focus();
        } else if (!open && window.innerWidth < 1024 && sidebar.contains(document.activeElement)) {
            toggle.focus();
        }
    };

    toggle.addEventListener("click", () => setOpen(toggle.getAttribute("aria-expanded") !== "true"));
    backdrop.addEventListener("click", () => setOpen(false));
    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape") setOpen(false);
        if (event.key !== "Tab" || window.innerWidth >= 1024 || toggle.getAttribute("aria-expanded") !== "true") return;
        const items = focusableElements();
        const first = items[0];
        const last = items[items.length - 1];
        if (event.shiftKey && document.activeElement === first) {
            event.preventDefault();
            last?.focus();
        } else if (!event.shiftKey && document.activeElement === last) {
            event.preventDefault();
            first?.focus();
        }
    });
    let lastWidth = window.innerWidth;
    window.addEventListener("resize", () => {
        const width = window.innerWidth;
        if (width >= 1024 || (lastWidth >= 1024 && width < 1024)) {
            setOpen(false);
        }
        lastWidth = width;
    });

    setOpen(false);
})();
