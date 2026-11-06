(() => {
    if (!window.toastr) return; // The rendered message remains usable without the CDN.
    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    document.querySelectorAll("[data-flash-message]").forEach(message => {
        const variant = message.dataset.flashMessage;
        if (!["success", "error", "warning", "info"].includes(variant)) return;
        const persistent = variant === "error" || variant === "warning";
        try {
            const toast = window.toastr[variant](message.textContent.trim(), null, {
                escapeHtml: true,
                positionClass: "toast-top-right",
                closeButton: true,
                progressBar: !persistent && !reducedMotion,
                timeOut: persistent ? 0 : 6000,
                extendedTimeOut: persistent ? 0 : 6000,
                tapToDismiss: false,
                showDuration: reducedMotion ? 0 : 150,
                hideDuration: reducedMotion ? 0 : 150
            });
            if (!toast?.length) return;
            toast.attr("role", variant === "error" ? "alert" : "status");
            toast.attr("aria-live", variant === "error" ? "assertive" : "polite");
            toast.find(".toast-close-button").attr({ "aria-label": "Dismiss notification", type: "button" });
            toast.on("focusin", () => toast.trigger("mouseenter"));
            toast.on("focusout", event => {
                if (!toast[0].contains(event.relatedTarget)) toast.trigger("mouseleave");
            });
            message.hidden = true;
        } catch { /* Keep the inline fallback when notification enhancement fails. */ }
    });
})();
