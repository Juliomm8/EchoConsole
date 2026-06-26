(() => {
    "use strict";

    const dialog = document.querySelector("[data-patch-edit-dialog]");
    const editButtons = document.querySelectorAll("[data-patch-edit]");
    const closeButtons = document.querySelectorAll("[data-patch-edit-close]");
    const deleteForms = document.querySelectorAll("[data-patch-delete-form]");
    const submitForms = document.querySelectorAll("[data-patch-submit-form]");
    const editForm = document.querySelector("[data-patch-edit-form]");

    const setSubmittingState = form => {
        const button = form.querySelector("button[type='submit']");

        if (!button || button.disabled) {
            return;
        }

        button.disabled = true;
        button.setAttribute("aria-busy", "true");
        button.classList.add("cursor-wait", "opacity-60");
    };

    submitForms.forEach(form => {
        form.addEventListener("submit", () => {
            setSubmittingState(form);
        });
    });

    deleteForms.forEach(form => {
        form.addEventListener("submit", event => {
            const confirmation =
                form.dataset.confirmMessage
                ?? "Delete this patch note?";

            if (!window.confirm(confirmation)) {
                event.preventDefault();
                return;
            }

            setSubmittingState(form);
        });
    });

    if (!dialog || !editForm) {
        return;
    }

    const fields = {
        id: dialog.querySelector("[data-edit-field='id']"),
        version: dialog.querySelector("[data-edit-field='version']"),
        category: dialog.querySelector("[data-edit-field='category']"),
        tone: dialog.querySelector("[data-edit-field='tone']"),
        title: dialog.querySelector("[data-edit-field='title']"),
        description: dialog.querySelector("[data-edit-field='description']"),
        published: dialog.querySelector("[data-edit-field='published']")
    };

    const hasAllFields = Object.values(fields)
        .every(field => field instanceof HTMLElement);

    if (!hasAllFields) {
        return;
    }

    const populateDialog = button => {
        fields.id.value = button.dataset.patchId ?? "";
        fields.version.value = button.dataset.patchVersion ?? "";
        fields.category.value = button.dataset.patchCategory ?? "";
        fields.tone.value = button.dataset.patchTone ?? "green";
        fields.title.value = button.dataset.patchTitle ?? "";
        fields.description.value = button.dataset.patchDescription ?? "";
        fields.published.checked =
            button.dataset.patchPublished === "true";
    };

    editButtons.forEach(button => {
        button.addEventListener("click", () => {
            populateDialog(button);

            if (typeof dialog.showModal === "function") {
                dialog.showModal();
                fields.version.focus();
            }
        });
    });

    closeButtons.forEach(button => {
        button.addEventListener("click", () => {
            dialog.close();
        });
    });

    dialog.addEventListener("click", event => {
        if (event.target === dialog) {
            dialog.close();
        }
    });

    editForm.addEventListener("submit", () => {
        setSubmittingState(editForm);
    });
})();
