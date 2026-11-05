// Run in the head so the saved theme is applied before the stylesheet paints.
(() => {
    const root = document.documentElement;
    const system = window.matchMedia("(prefers-color-scheme: dark)");
    const choices = ["light", "dark", "system"];
    let preference = "system";
    try {
        const saved = localStorage.getItem("gearantee-theme");
        if (choices.includes(saved)) preference = saved;
    } catch { /* A blocked preference store must not prevent rendering. */ }

    function apply() {
        const theme = preference === "system" ? (system.matches ? "dark" : "light") : preference;
        root.dataset.theme = theme;
        root.style.colorScheme = theme;
        document.querySelector('meta[name="theme-color"]')?.setAttribute("content", theme === "dark" ? "#111923" : "#F2F2F3");
        document.querySelectorAll("[data-theme-picker]").forEach(picker => { picker.value = preference; });
    }

    apply();
    system.addEventListener("change", () => { if (preference === "system") apply(); });
    document.addEventListener("DOMContentLoaded", () => {
        apply();
        document.querySelectorAll("[data-theme-picker]").forEach(picker => {
            picker.addEventListener("change", () => {
                if (!choices.includes(picker.value)) return;
                preference = picker.value;
                try { localStorage.setItem("gearantee-theme", preference); } catch { /* Keep the selection for this page. */ }
                apply();
            });
        });
    });
})();
