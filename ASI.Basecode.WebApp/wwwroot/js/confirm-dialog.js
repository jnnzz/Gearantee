// Confirmation modal for destructive form posts.
// Usage: <form data-confirm-dialog="dialog-id"> plus <partial name="Components/_ConfirmDialog" />.
// Without JavaScript the form posts directly; the server still enforces every rule.
(() => {
    document.querySelectorAll("form[data-confirm-dialog]").forEach((form) => {
        const dialog = document.getElementById(form.dataset.confirmDialog);
        if (!dialog || typeof dialog.showModal !== "function") return;

        const accept = dialog.querySelector("[data-confirm-accept]");
        const dismiss = dialog.querySelector("[data-confirm-dismiss]");
        let confirmed = false;

        form.addEventListener("submit", (event) => {
            if (confirmed) return;
            event.preventDefault();
            dialog.showModal();
        });

        dismiss?.addEventListener("click", () => dialog.close());

        // Clicking the dimmed backdrop (outside the panel) closes the dialog.
        dialog.addEventListener("click", (event) => {
            if (event.target === dialog) dialog.close();
        });

        accept?.addEventListener("click", () => {
            confirmed = true;
            accept.disabled = true; // prevents a double post
            dialog.close();
            form.requestSubmit();
        });
    });
})();
