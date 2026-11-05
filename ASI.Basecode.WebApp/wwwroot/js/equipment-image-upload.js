(() => {
  const maxBytes = 5_000_000;
  const maxDimension = 6_000;
  const maxPixels = 8_000_000;
  const mimeByExtension = new Map([
    [".jpg", "image/jpeg"],
    [".jpeg", "image/jpeg"],
    [".png", "image/png"],
  ]);
  const pngSignature = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];

  const decodeImage = async (file) => {
    if (typeof createImageBitmap === "function") {
      const bitmap = await createImageBitmap(file);
      try {
        return { width: bitmap.width, height: bitmap.height };
      } finally {
        bitmap.close?.();
      }
    }

    const objectUrl = URL.createObjectURL(file);
    try {
      const image = new Image();
      if (typeof image.decode === "function") {
        image.src = objectUrl;
        await image.decode();
      } else {
        await new Promise((resolve, reject) => {
          image.onload = resolve;
          image.onerror = () => reject(new Error("Image decoding failed."));
          image.src = objectUrl;
        });
      }

      return { width: image.naturalWidth, height: image.naturalHeight };
    } finally {
      URL.revokeObjectURL(objectUrl);
    }
  };

  const validateImage = async (file) => {
    if (file.size === 0) {
      return "The selected image is empty.";
    }
    if (file.size > maxBytes) {
      return "Choose an image no larger than 5,000,000 bytes.";
    }

    const dot = file.name.lastIndexOf(".");
    const extension = dot >= 0 ? file.name.slice(dot).toLowerCase() : "";
    const expectedMime = mimeByExtension.get(extension);
    const actualMime = (file.type || "").toLowerCase();
    if (!expectedMime || actualMime !== expectedMime) {
      return "Choose a .jpg, .jpeg, or .png file with a matching image content type.";
    }

    const header = new Uint8Array(await file.slice(0, 8).arrayBuffer());
    const signatureMatches = expectedMime === "image/png"
      ? pngSignature.every((byte, index) => header[index] === byte)
      : header.length >= 3 && header[0] === 0xff && header[1] === 0xd8 && header[2] === 0xff;
    if (!signatureMatches) {
      return "The file extension and content type do not match a JPEG or PNG image.";
    }

    let dimensions;
    try {
      dimensions = await decodeImage(file);
    } catch {
      return "The selected file is not a decodable JPEG or PNG image.";
    }

    if (dimensions.width <= 0 || dimensions.height <= 0 ||
        dimensions.width > maxDimension || dimensions.height > maxDimension ||
        dimensions.width * dimensions.height > maxPixels) {
      return "Image dimensions must be at most 6,000 pixels per side and 8 megapixels total.";
    }

    return "";
  };

  document.querySelectorAll("[data-equipment-image-upload]").forEach((root) => {
    const input = root.querySelector("input[type='file']");
    const dropZone = root.querySelector("[data-drop-zone]");
    const status = root.querySelector("#image-upload-status");
    const error = root.querySelector("[data-upload-error]");
    const previewContainer = root.querySelector("[data-image-preview-container]");
    const preview = root.querySelector("[data-image-preview]");
    const caption = root.querySelector("[data-image-preview-caption]");
    const uploadedUrl = root.querySelector("input[name='UploadedImageUrl']");
    const currentImageUrl = root.querySelector("input[name='CurrentImageUrl']");
    const removeImage = root.querySelector("input[name='RemoveImage'][type='checkbox']");
    const imageActions = root.querySelector("[data-image-actions]");
    const clearImageButton = root.querySelector("[data-clear-image]");
    if (!input || !dropZone || !status || !error || !previewContainer || !preview || !caption) return;

    let localPreviewUrl = null;
    let validationVersion = 0;

    const revokeLocalPreview = () => {
      if (localPreviewUrl) {
        URL.revokeObjectURL(localPreviewUrl);
        localPreviewUrl = null;
      }
    };

    const clearError = () => {
      error.textContent = "";
      input.setCustomValidity("");
      input.removeAttribute("aria-invalid");
    };

    const syncImageActions = () => {
      if (imageActions) {
        imageActions.hidden = !(input.files?.length || uploadedUrl?.value);
      }
    };

    const restoreExistingPreview = (statusMessage) => {
      revokeLocalPreview();
      const savedPreviewUrl = uploadedUrl?.value || currentImageUrl?.value;
      if (savedPreviewUrl && !removeImage?.checked) {
        preview.src = savedPreviewUrl;
        previewContainer.hidden = false;
        caption.textContent = uploadedUrl?.value ? "Selected image" : "Current image";
      } else {
        previewContainer.hidden = true;
      }

      status.textContent = statusMessage ??
        (uploadedUrl?.value ? "Previously selected image is ready to save." : "No new image selected.");
      syncImageActions();
    };

    const rejectSelection = (message) => {
      validationVersion += 1;
      input.value = "";
      input.setCustomValidity(message);
      input.setAttribute("aria-invalid", "true");
      error.textContent = message;
      restoreExistingPreview("The new image was rejected; the existing selection is unchanged.");
    };

    const validateSelection = async (files) => {
      const currentVersion = ++validationVersion;
      const selectedFiles = Array.from(files ?? []);
      if (selectedFiles.length === 0) {
        clearError();
        restoreExistingPreview();
        return;
      }

      if (selectedFiles.length !== 1) {
        rejectSelection("Choose exactly one equipment image. Multiple files are not supported.");
        return;
      }

      const file = selectedFiles[0];
      error.textContent = "";
      input.setAttribute("aria-invalid", "true");
      input.setCustomValidity("Please wait while the image is being validated.");
      status.textContent = "Validating image type, content, and dimensions…";

      let validationError;
      try {
        validationError = await validateImage(file);
      } catch {
        validationError = "The selected image could not be read. Choose another file.";
      }

      if (currentVersion !== validationVersion) return;
      if (validationError) {
        rejectSelection(validationError);
        return;
      }

      clearError();
      revokeLocalPreview();
      localPreviewUrl = URL.createObjectURL(file);
      preview.src = localPreviewUrl;
      previewContainer.hidden = false;
      caption.textContent = "New image preview";
      status.textContent = `${file.name} · ${(file.size / 1_000_000).toFixed(2)} MB`;
      if (removeImage) removeImage.checked = false;
      syncImageActions();
    };

    const existingError = error.textContent.trim();
    if (existingError) {
      input.setCustomValidity(existingError);
      input.setAttribute("aria-invalid", "true");
    }

    input.addEventListener("change", () => validateSelection(input.files));
    input.addEventListener("cancel", () => {
      if (input.files?.length) return;
      clearError();
      restoreExistingPreview();
    });
    clearImageButton?.addEventListener("click", () => {
      validationVersion += 1;
      input.value = "";
      if (uploadedUrl) uploadedUrl.value = "";
      if (removeImage) removeImage.checked = false;
      clearError();
      restoreExistingPreview(currentImageUrl?.value
        ? "Selected image cleared. The current image will be kept."
        : "Image selection cleared.");
    });
    removeImage?.addEventListener("change", () => {
      if (removeImage.checked) {
        validationVersion += 1;
        input.value = "";
        if (uploadedUrl) uploadedUrl.value = "";
        clearError();
        revokeLocalPreview();
        previewContainer.hidden = true;
        status.textContent = "The current image will be removed when you save.";
        syncImageActions();
      } else {
        validateSelection([]);
      }
    });

    ["dragenter", "dragover"].forEach((eventName) => dropZone.addEventListener(eventName, (event) => {
      event.preventDefault();
      dropZone.classList.add("is-dragging");
    }));
    ["dragleave", "drop"].forEach((eventName) => dropZone.addEventListener(eventName, (event) => {
      event.preventDefault();
      dropZone.classList.remove("is-dragging");
    }));
    dropZone.addEventListener("drop", (event) => {
      const files = Array.from(event.dataTransfer?.files ?? []);
      if (files.length === 0) return;
      if (files.length !== 1) {
        rejectSelection("Choose exactly one equipment image. Multiple files are not supported.");
        return;
      }

      try {
        const transfer = new DataTransfer();
        transfer.items.add(files[0]);
        input.files = transfer.files;
      } catch {
        rejectSelection("This browser could not read the dropped file. Use Browse to select one image.");
        return;
      }

      validateSelection(input.files);
    });

    syncImageActions();
  });
})();
