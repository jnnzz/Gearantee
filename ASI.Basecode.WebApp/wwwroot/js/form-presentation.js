(() => {
    function sync(form) {
        let requiredCount = 0;
        form.querySelectorAll("label[for]").forEach(label => {
            const field = document.getElementById(label.htmlFor);
            if (!field || !form.contains(field)) return;
            const required = !field.disabled && !field.readOnly &&
                field.getClientRects().length > 0 &&
                (field.required || field.hasAttribute("data-val-required")) &&
                !["checkbox", "radio", "hidden"].includes(field.type);
            let marker = label.querySelector("[data-required-marker]");
            if (required && !marker) {
                marker = document.createElement("span");
                marker.className = "required-marker";
                marker.dataset.requiredMarker = "";
                const star = document.createElement("span");
                star.setAttribute("aria-hidden", "true");
                star.textContent = " *";
                const description = document.createElement("span");
                description.className = "sr-only";
                description.textContent = " (required)";
                marker.append(star, description);
                label.append(marker);
            }
            if (marker) marker.hidden = !required;
            if (required) requiredCount++;
        });
        let legend = form.querySelector(".required-legend");
        const hideLegend = form.hasAttribute("data-hide-required-legend");
        if (requiredCount && !legend && !hideLegend) {
            legend = document.createElement("p");
            legend.className = "required-legend";
            legend.textContent = "* Required fields";
            form.prepend(legend);
        }
        if (legend) legend.hidden = hideLegend || !requiredCount;
    }
    const forms = [...document.forms];
    forms.forEach(form => {
        sync(form);
        form.addEventListener("change", () => sync(form));
        // Existing role/inspection scripts change conditional required/disabled state.
        new MutationObserver(() => sync(form)).observe(form, {
            subtree: true, attributes: true, attributeFilter: ["required", "disabled", "readonly"]
        });
    });
    // Required fields inside a newly opened modal were hidden on initial load.
    document.querySelectorAll(".management-modal").forEach(modal => {
        new MutationObserver(() => modal.querySelectorAll("form").forEach(sync))
            .observe(modal, { attributes: true, attributeFilter: ["class"] });
    });
})();
