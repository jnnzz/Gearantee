// Confirmation modal for destructive form posts.
// Usage: <form data-confirm-dialog="dialog-id"> plus <partial name="Components/_ConfirmDialog" />.
// Without JavaScript the form posts directly; the server still enforces every rule.
(() => {
    document.querySelectorAll("form[data-confirm-dialog]").forEach((form) => {
        const dialog = document.getElementById(form.dataset.confirmDialog);
        if (!dialog || typeof dialog.showModal !== "function") return;

        const accept = dialog.querySelector("[data-confirm-accept]");
        const dismiss = dialog.querySelector("[data-confirm-dismiss]");
        const detail = dialog.querySelector('[data-confirm-detail]');
        const baseDetail = detail?.textContent.trim() ?? '';
        let approved = false;
        let submitting = false;
        let submissionEvent = null;
        let submitter = null;
        const valid = () => form.reportValidity() &&
            (!window.jQuery?.validator || window.jQuery(form).valid());

        form.addEventListener("submit", (event) => {
            if (submitting || event.defaultPrevented || !valid()) {
                event.preventDefault();
                return;
            }
            if (approved) {
                submissionEvent = event;
                return;
            }
            event.preventDefault();
            submitter = event.submitter ?? null;
            if (detail) {
                const summary = [...form.querySelectorAll('[data-confirm-summary]')]
                    .map(field => `${field.dataset.confirmSummary}: ${field.selectedOptions?.[0]?.textContent ?? field.value}`);
                detail.textContent = [baseDetail, ...summary].filter(Boolean).join('\n');
            }
            dialog.showModal();
        });

        dismiss?.addEventListener("click", () => dialog.close());

        dialog.addEventListener('keydown', event => {
            if (event.key !== 'Tab') return;
            if (event.shiftKey && document.activeElement === dismiss) {
                event.preventDefault();
                accept.focus();
            } else if (!event.shiftKey && document.activeElement === accept) {
                event.preventDefault();
                dismiss.focus();
            }
        });

        // Clicking the dimmed backdrop (outside the panel) closes the dialog.
        dialog.addEventListener("click", (event) => {
            if (event.target === dialog) dialog.close();
        });

        accept?.addEventListener("click", () => {
            if (accept.disabled || submitting) return;
            approved = true;
            submissionEvent = null;
            accept.disabled = true;
            dialog.close();
            try {
                if (submitter) form.requestSubmit(submitter);
                else form.requestSubmit();
            } finally {
                approved = false;
                // A later validation listener may cancel the re-submission.
                submitting = submissionEvent !== null && !submissionEvent.defaultPrevented;
                accept.disabled = submitting;
            }
        });

        window.addEventListener('pageshow', () => {
            submitting = false;
            accept.disabled = false;
        });
    });
})();
