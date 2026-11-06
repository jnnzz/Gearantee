// Apply the explicit preference before the stylesheet paints.
(() => {
    const root = document.documentElement;
    const forceLight = root.dataset.forceLight === "true";
    let preference = "light";
    try {
        if (!forceLight && localStorage.getItem("gearantee-theme") === "dark") preference = "dark";
    } catch { /* Storage may be blocked; the toggle still works for this page. */ }
    function apply() {
        root.dataset.theme = preference;
        root.style.colorScheme = preference;
        document.querySelector('meta[name="theme-color"]')?.setAttribute("content", preference === "dark" ? "#111923" : "#F2F2F3");
        document.querySelectorAll("[data-theme-toggle]").forEach(button => {
            button.setAttribute("aria-label", "Switch to " + (preference === "dark" ? "light" : "dark") + " mode");
            button.setAttribute("aria-pressed", String(preference === "dark"));
        });
    }
    apply();
    document.addEventListener("DOMContentLoaded", () => {
        apply();
        if (forceLight) return;
        document.querySelectorAll("[data-theme-toggle]").forEach(button => button.addEventListener("click", () => {
            preference = preference === "light" ? "dark" : "light";
            try { localStorage.setItem("gearantee-theme", preference); } catch { /* Keep the page usable. */ }
            apply();
        }));
    });
})();
