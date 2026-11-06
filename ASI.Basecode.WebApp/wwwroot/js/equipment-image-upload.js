// Equipment image drag/drop + preview + client validation. Wires only when a drop zone exists on the page.
(function () {
    "use strict";

    var ALLOWED_EXTENSIONS = [".jpg", ".jpeg", ".png", ".webp"];
    var ALLOWED_MIME_TYPES = ["image/jpeg", "image/png", "image/webp"];

    function wire(root) {
        var input = root.querySelector("[data-image-input]");
        var prompt = root.querySelector("[data-image-prompt]");
        var filename = root.querySelector("[data-image-filename]");
        var previewWrap = document.querySelector("[data-image-preview-wrap]");
        var preview = document.querySelector("[data-image-preview]");
        var removeCheckbox = document.querySelector("[data-image-remove]");
        var errorDisplay = document.querySelector("[data-image-error]") || document.querySelector("[data-valmsg-for='imageFile']");
        var form = input ? input.closest("form") : null;
        var objectUrl = null;
        var hasError = false;

        if (!input) {
            return;
        }

        // Server-configured cap (Supabase:MaxFileBytes) rendered by the view; fall back to 2 MB.
        var configuredMax = parseInt(input.getAttribute("data-image-max-bytes"), 10);
        var MAX_IMAGE_BYTES = Number.isFinite(configuredMax) && configuredMax > 0
            ? configuredMax
            : 2 * 1024 * 1024;
        var MAX_IMAGE_MB = Math.max(1, Math.round(MAX_IMAGE_BYTES / (1024 * 1024)));

        function revokePreview() {
            if (objectUrl) {
                URL.revokeObjectURL(objectUrl);
                objectUrl = null;
            }
        }

        function showError(message) {
            hasError = true;
            if (errorDisplay) {
                errorDisplay.textContent = message;
                errorDisplay.removeAttribute("hidden");
                errorDisplay.style.display = "block";
            }
            root.classList.add("border-red-500", "bg-red-50");
            root.classList.remove("border-primary-strong", "bg-white");
            window.alert(message);
        }

        function clearError() {
            hasError = false;
            if (errorDisplay) {
                errorDisplay.textContent = "";
            }
            root.classList.remove("border-red-500", "bg-red-50");
        }

        function resetInput() {
            input.value = "";
            try {
                if (typeof DataTransfer !== "undefined") {
                    input.files = new DataTransfer().files;
                }
            } catch (e) {
                // Ignore if DataTransfer cannot be instantiated
            }
            if (filename) {
                filename.textContent = "";
            }
            if (prompt) {
                prompt.textContent = "Drag & drop an image here, or click to browse";
            }
            revokePreview();
            if (preview) {
                preview.setAttribute("hidden", "");
            }
            if (previewWrap) {
                previewWrap.setAttribute("hidden", "");
            }
        }

        function showPreview(file) {
            if (!previewWrap || !preview) {
                return;
            }
            revokePreview();
            objectUrl = URL.createObjectURL(file);
            preview.src = objectUrl;
            preview.removeAttribute("hidden");
            previewWrap.removeAttribute("hidden");
            if (removeCheckbox) {
                removeCheckbox.checked = false;
            }
        }

        function validateFile(file) {
            if (!file) {
                return false;
            }

            // 1. Check file size (Reject files exceeding 2 MB)
            if (file.size > MAX_IMAGE_BYTES) {
                resetInput();
                showError("Image size exceeds the maximum allowed size of " + MAX_IMAGE_MB + " MB. Please select a smaller image.");
                return false;
            }

            if (file.size <= 0) {
                resetInput();
                showError("The selected file is empty. Please select a valid image.");
                return false;
            }

            // 2. Check file extension and MIME type
            var name = file.name || "";
            var dotIndex = name.lastIndexOf(".");
            var ext = dotIndex !== -1 ? name.slice(dotIndex).toLowerCase() : "";
            var mime = (file.type || "").toLowerCase();

            var extValid = ALLOWED_EXTENSIONS.indexOf(ext) !== -1;
            var mimeValid = mime ? ALLOWED_MIME_TYPES.indexOf(mime) !== -1 : true;

            if (!extValid || !mimeValid) {
                resetInput();
                showError("Unsupported image format. Please select a JPEG, PNG, or WebP image.");
                return false;
            }

            // Valid file selected
            clearError();
            if (filename) {
                filename.textContent = file.name + " (" + Math.ceil(file.size / 1024) + " KB)";
            }
            if (prompt) {
                prompt.textContent = "Replace this image, or click to choose a different file";
            }
            showPreview(file);
            return true;
        }

        input.addEventListener("change", function () {
            var file = input.files && input.files[0];
            if (file) {
                validateFile(file);
            }
        });

        ["dragenter", "dragover"].forEach(function (name) {
            root.addEventListener(name, function (event) {
                event.preventDefault();
                root.classList.add("border-primary-strong", "bg-white");
            });
        });

        ["dragleave", "drop"].forEach(function (name) {
            root.addEventListener(name, function (event) {
                event.preventDefault();
                root.classList.remove("border-primary-strong", "bg-white");
            });
        });

        root.addEventListener("drop", function (event) {
            var file = event.dataTransfer && event.dataTransfer.files && event.dataTransfer.files[0];
            if (!file) {
                return;
            }

            // Validate dropped file before routing into the input
            if (validateFile(file)) {
                try {
                    var transfer = new DataTransfer();
                    transfer.items.add(file);
                    input.files = transfer.files;
                } catch (error) {
                    input.files = event.dataTransfer.files;
                }
            } else {
                resetInput();
            }
        });

        if (form) {
            form.addEventListener("submit", function (event) {
                if (hasError) {
                    event.preventDefault();
                    if (errorDisplay) {
                        errorDisplay.scrollIntoView({ behavior: "smooth", block: "center" });
                    }
                }
            });
        }

        if (removeCheckbox) {
            removeCheckbox.addEventListener("change", function () {
                clearError();
                if (removeCheckbox.checked) {
                    resetInput();
                } else if (preview && preview.getAttribute("src")) {
                    preview.removeAttribute("hidden");
                    if (previewWrap) {
                        previewWrap.removeAttribute("hidden");
                    }
                }
            });
        }
    }

    document.querySelectorAll("[data-image-drop-zone]").forEach(wire);
})();
