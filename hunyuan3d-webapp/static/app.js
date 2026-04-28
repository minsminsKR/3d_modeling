const fileInput = document.querySelector("#images");
const fileList = document.querySelector("#file-list");
const dropZone = document.querySelector(".drop-zone");

if (fileInput && fileList && dropZone) {
  const filesBuffer = [];

  const allowedPasteImageTypes = new Set(["image/png", "image/jpeg", "image/webp"]);

  const isTextualInputFocused = (target) => {
    if (!target || !(target instanceof Element)) {
      return false;
    }
    if (target.isContentEditable) {
      return true;
    }
    if (target.tagName === "TEXTAREA") {
      return true;
    }
    if (target.tagName === "INPUT") {
      const type = (target.type || "text").toLowerCase();
      return ["text", "search", "email", "url", "tel", "password", "number"].includes(type);
    }
    return false;
  };

  const syncInputFiles = () => {
    const transfer = new DataTransfer();
    for (const file of filesBuffer) {
      transfer.items.add(file);
    }
    fileInput.files = transfer.files;
  };

  const renderFiles = () => {
    fileList.innerHTML = "";
    filesBuffer.forEach((file, index) => {
      const chip = document.createElement("div");
      chip.className = "file-chip";

      const label = document.createElement("span");
      label.className = "file-chip-label";
      label.textContent = `${file.name} (${Math.round(file.size / 1024)} KB)`;

      const removeBtn = document.createElement("button");
      removeBtn.type = "button";
      removeBtn.className = "file-chip-remove";
      removeBtn.setAttribute("aria-label", `${file.name} 제거`);
      removeBtn.textContent = "×";
      removeBtn.addEventListener("click", (event) => {
        event.preventDefault();
        event.stopPropagation();
        filesBuffer.splice(index, 1);
        syncInputFiles();
        renderFiles();
      });

      chip.appendChild(label);
      chip.appendChild(removeBtn);
      fileList.appendChild(chip);
    });
  };

  const addFiles = (files) => {
    const imageFiles = [...files].filter((file) => file.type.startsWith("image/"));
    for (const file of imageFiles) {
      const duplicate = filesBuffer.some((existing) => (
        existing.name === file.name &&
        existing.size === file.size &&
        existing.lastModified === file.lastModified
      ));
      if (!duplicate) {
        filesBuffer.push(file);
      }
    }
    syncInputFiles();
    renderFiles();
  };

  fileInput.addEventListener("change", () => addFiles(fileInput.files));

  ["dragenter", "dragover"].forEach((eventName) => {
    dropZone.addEventListener(eventName, (event) => {
      event.preventDefault();
      dropZone.classList.add("drag-over");
    });
  });

  ["dragleave", "drop"].forEach((eventName) => {
    dropZone.addEventListener(eventName, (event) => {
      event.preventDefault();
      dropZone.classList.remove("drag-over");
    });
  });

  dropZone.addEventListener("drop", (event) => {
    addFiles(event.dataTransfer.files);
  });

  document.addEventListener("paste", (event) => {
    if (isTextualInputFocused(event.target)) {
      return;
    }
    const items = event.clipboardData?.items;
    if (!items?.length) {
      return;
    }
    const pastedFiles = [];
    for (const item of items) {
      if (item.kind !== "file" || !allowedPasteImageTypes.has(item.type)) {
        continue;
      }
      const file = item.getAsFile();
      if (file) {
        pastedFiles.push(file);
      }
    }
    if (!pastedFiles.length) {
      return;
    }
    event.preventDefault();
    addFiles(pastedFiles);
  });
}

const jobId = document.body.dataset.jobId;
if (jobId) {
  const statusPill = document.querySelector("#status-pill");
  const message = document.querySelector("#job-message");
  const elapsedTime = document.querySelector("#elapsed-time");
  const progressBar = document.querySelector("#progress-bar");
  const resultPanel = document.querySelector("#result-panel");
  const mixamoPanel = document.querySelector("#mixamo-panel");
  const failedPanel = document.querySelector("#failed-panel");
  const failedMessage = document.querySelector("#failed-message");
  const warningList = document.querySelector("#warning-list");
  const downloadLink = document.querySelector("#download-link");
  const mixamoOpenLink = document.querySelector("#mixamo-open-link");
  const mixamoPackageLink = document.querySelector("#mixamo-package-link");
  const mixamoUploadForm = document.querySelector("#mixamo-upload-form");
  const modelViewer = document.querySelector("#model-viewer");
  const processedPreview = document.querySelector("#processed-preview");
  const processedImage = document.querySelector("#processed-image");
  let currentJob = {
    status: statusPill.textContent.trim(),
    elapsedSeconds: Number(elapsedTime?.dataset.elapsed || 0),
    receivedAt: Date.now(),
  };

  const formatDuration = (seconds) => {
    const safeSeconds = Math.max(0, Math.floor(seconds));
    const minutes = Math.floor(safeSeconds / 60);
    const remainingSeconds = safeSeconds % 60;
    if (minutes === 0) {
      return `${remainingSeconds}s`;
    }
    return `${minutes}m ${String(remainingSeconds).padStart(2, "0")}s`;
  };

  const renderElapsed = () => {
    if (!elapsedTime) {
      return;
    }
    const liveOffset = ["queued", "running"].includes(currentJob.status)
      ? Math.floor((Date.now() - currentJob.receivedAt) / 1000)
      : 0;
    elapsedTime.textContent = formatDuration(currentJob.elapsedSeconds + liveOffset);
  };

  const updateStatus = async () => {
    const response = await fetch(`/api/jobs/${jobId}`);
    if (!response.ok) {
      return;
    }
    const job = await response.json();
    currentJob = {
      status: job.status,
      elapsedSeconds: job.elapsed_seconds || 0,
      receivedAt: Date.now(),
    };
    statusPill.textContent = job.status;
    statusPill.className = `pill ${job.status}`;
    message.textContent = job.message || "";
    progressBar.className = `progress-bar ${job.status}`;
    if (warningList) {
      warningList.innerHTML = "";
      for (const warning of job.warnings || []) {
        const item = document.createElement("p");
        item.textContent = warning;
        warningList.appendChild(item);
      }
      warningList.classList.toggle("hidden", !(job.warnings || []).length);
    }
    renderElapsed();

    if (job.status === "completed") {
      resultPanel.classList.remove("hidden");
      mixamoPanel?.classList.remove("hidden");
      failedPanel.classList.add("hidden");
      downloadLink.href = job.download_url;
      if (mixamoOpenLink && job.mixamo_url) {
        mixamoOpenLink.href = job.mixamo_url;
      }
      if (mixamoPackageLink && job.mixamo_package_url) {
        mixamoPackageLink.href = job.mixamo_package_url;
      }
      if (mixamoUploadForm && job.mixamo_upload_url) {
        mixamoUploadForm.action = job.mixamo_upload_url;
      }
      modelViewer.src = job.model_url;
      if (processedPreview && processedImage && job.processed_input_url) {
        processedPreview.classList.remove("hidden");
        processedImage.src = job.processed_input_url;
      }
      return;
    }

    if (job.status === "failed") {
      failedPanel.classList.remove("hidden");
      resultPanel.classList.add("hidden");
      mixamoPanel?.classList.add("hidden");
      failedMessage.textContent = job.message || "Unknown error";
      return;
    }

    window.setTimeout(updateStatus, 3000);
  };

  if (!["completed", "failed"].includes(statusPill.textContent.trim())) {
    window.setTimeout(updateStatus, 1000);
  }
  window.setInterval(renderElapsed, 1000);
  renderElapsed();
}
