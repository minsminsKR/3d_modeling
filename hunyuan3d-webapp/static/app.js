const fileInput = document.querySelector("#images");
const fileList = document.querySelector("#file-list");
const dropZone = document.querySelector(".drop-zone");

if (fileInput && fileList && dropZone) {
  const filesBuffer = [];
  const maxFiles = Number(fileInput.dataset.maxFiles || 0);

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
    if (maxFiles > 0 && filesBuffer.length >= maxFiles) {
      const note = document.createElement("div");
      note.className = "file-note";
      note.textContent = `최대 ${maxFiles}장까지 선택되었습니다.`;
      fileList.appendChild(note);
    }
  };

  const addFiles = (files) => {
    const imageFiles = [...files].filter((file) => file.type.startsWith("image/"));
    for (const file of imageFiles) {
      if (maxFiles > 0 && filesBuffer.length >= maxFiles) {
        break;
      }
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

const updateSaveButton = (button, saved) => {
  button.dataset.saved = saved ? "true" : "false";
  button.setAttribute("aria-pressed", saved ? "true" : "false");
  button.classList.toggle("is-saved", saved);
  if (button.closest(".saved-job-card")) {
    button.textContent = saved ? "저장 해제" : "저장하기";
    return;
  }
  button.textContent = saved ? "저장됨" : "저장하기";
};

const toggleJobSaved = async (button) => {
  const jobId = button.dataset.jobId;
  if (!jobId || button.disabled) {
    return;
  }
  const currentlySaved = button.dataset.saved === "true";
  const nextSaved = !currentlySaved;
  button.disabled = true;
  try {
    const response = await fetch(`/api/jobs/${jobId}/saved`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ saved: nextSaved }),
    });
    if (!response.ok) {
      throw new Error("save failed");
    }
    const job = await response.json();
    document.querySelectorAll(`.save-job-btn[data-job-id="${jobId}"]`).forEach((item) => {
      updateSaveButton(item, Boolean(job.saved));
    });
    const badgeHost = document.querySelector(`a.job-row-main[href*="${jobId}"] .job-summary`);
    if (badgeHost) {
      let badge = badgeHost.querySelector(".saved-badge");
      if (job.saved && !badge) {
        badge = document.createElement("span");
        badge.className = "saved-badge";
        badge.textContent = "저장됨";
        badgeHost.appendChild(badge);
      } else if (!job.saved && badge) {
        badge.remove();
      }
    }
    const savedCard = button.closest(".saved-job-card");
    if (savedCard && !job.saved) {
      savedCard.remove();
      const remaining = document.querySelectorAll(".saved-job-card").length;
      if (remaining === 0) {
        window.location.reload();
      }
    }
  } catch (_error) {
    window.alert("저장 상태를 변경하지 못했습니다. 잠시 후 다시 시도해 주세요.");
  } finally {
    button.disabled = false;
  }
};

for (const button of document.querySelectorAll(".save-job-btn")) {
  updateSaveButton(button, button.dataset.saved === "true");
  button.addEventListener("click", (event) => {
    event.preventDefault();
    event.stopPropagation();
    toggleJobSaved(button);
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
      const saveButton = document.querySelector("#save-job-btn");
      if (saveButton) {
        updateSaveButton(saveButton, Boolean(job.saved));
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

const batchId = document.body.dataset.batchId;
if (batchId) {
  const batchStatusPill = document.querySelector("#batch-status-pill");
  const batchMessage = document.querySelector("#batch-message");
  const batchCompleted = document.querySelector("#batch-completed");
  const batchFailed = document.querySelector("#batch-failed");
  const batchDownloadLink = document.querySelector("#batch-download-link");

  const formatDuration = (seconds) => {
    const safeSeconds = Math.max(0, Math.floor(seconds));
    const minutes = Math.floor(safeSeconds / 60);
    const remainingSeconds = safeSeconds % 60;
    if (minutes === 0) {
      return `${remainingSeconds}s`;
    }
    return `${minutes}m ${String(remainingSeconds).padStart(2, "0")}s`;
  };

  const ensureModelPreview = (slot, modelUrl) => {
    if (!slot || !modelUrl) {
      return;
    }
    const existing = slot.querySelector("model-viewer");
    if (existing) {
      existing.src = modelUrl;
      return;
    }
    slot.innerHTML = "";
    slot.classList.remove("empty");
    const viewer = document.createElement("model-viewer");
    viewer.src = modelUrl;
    viewer.setAttribute("camera-controls", "");
    viewer.setAttribute("auto-rotate", "");
    viewer.setAttribute("exposure", "1");
    viewer.setAttribute("shadow-intensity", "0.45");
    viewer.setAttribute("interaction-prompt", "none");
    slot.appendChild(viewer);
  };

  const renderBatchJob = (job) => {
    const card = document.querySelector(`.batch-job[data-job-id="${job.id}"]`);
    if (!card) {
      return;
    }
    const status = card.querySelector(".batch-job-status");
    const message = card.querySelector(".batch-job-message");
    const elapsed = card.querySelector(".batch-job-elapsed");
    const slot = card.querySelector(".batch-model-slot");
    const download = card.querySelector(".batch-download");

    status.textContent = job.status;
    status.className = `batch-job-status pill ${job.status}`;
    message.textContent = job.message || "";
    elapsed.textContent = formatDuration(job.elapsed_seconds || 0);

    if (job.status === "completed") {
      ensureModelPreview(slot, job.model_url);
      if (download && job.download_url) {
        download.href = job.download_url;
        download.classList.remove("hidden");
      }
      let saveButton = card.querySelector(".save-job-btn");
      if (!saveButton) {
        const actions = card.querySelector(".saved-job-actions") || card;
        saveButton = document.createElement("button");
        saveButton.type = "button";
        saveButton.className = "save-job-btn";
        saveButton.dataset.jobId = job.id;
        saveButton.addEventListener("click", (event) => {
          event.preventDefault();
          event.stopPropagation();
          toggleJobSaved(saveButton);
        });
        actions.appendChild(saveButton);
      }
      updateSaveButton(saveButton, Boolean(job.saved));
      return;
    }

    if (download) {
      download.classList.add("hidden");
    }
    if (slot && !slot.querySelector("model-viewer")) {
      slot.classList.add("empty");
      slot.innerHTML = `<span class="preview-placeholder">${job.status === "failed" ? "실패" : "대기 중"}</span>`;
    }
  };

  const updateBatch = async () => {
    const response = await fetch(`/api/batches/${batchId}`);
    if (!response.ok) {
      return;
    }
    const batch = await response.json();
    const finished = batch.completed + batch.failed;

    batchCompleted.textContent = batch.completed;
    batchFailed.textContent = batch.failed;
    batchMessage.textContent = `${batch.completed} / ${batch.total} 완료${batch.failed ? `, ${batch.failed} 실패` : ""}`;

    batchStatusPill.textContent = finished === batch.total ? "done" : "running";
    batchStatusPill.className = `pill ${batch.failed ? "failed" : finished === batch.total ? "completed" : "running"}`;

    if (batchDownloadLink) {
      batchDownloadLink.href = batch.download_url;
      batchDownloadLink.classList.toggle("hidden", batch.completed === 0);
    }

    for (const job of batch.jobs) {
      renderBatchJob(job);
    }

    if (finished < batch.total) {
      window.setTimeout(updateBatch, 3000);
    }
  };

  window.setTimeout(updateBatch, 1000);
}
