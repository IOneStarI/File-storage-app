// Attaches a confirmation handler to each form that declares a confirmation message.
document.querySelectorAll('form[data-confirm]').forEach(form => form.addEventListener('submit', /* Cancels submission when the user declines confirmation. */ event => { if (!confirm(form.dataset.confirm)) event.preventDefault(); }));
const input = document.getElementById('file');
if (input) {
  const zone = document.getElementById('drop-zone');
  const error = document.getElementById('upload-error');
  const preview = document.getElementById('file-preview');
  // Lists the selected files and checks count, total size, individual size, and extension limits.
  // Returns true only when the entire selection is valid.
  const validate = () => {
    const files = Array.from(input.files);
    error.textContent = '';
    preview.replaceChildren();
    if (!files.length) { preview.textContent = 'No files selected'; return false; }
    // Adds a preview row using text content so file names are displayed safely.
    files.forEach(file => {
      const row = document.createElement('div');
      row.textContent = `${file.name} · ${(file.size / 1024 / 1024).toFixed(2)} MB`;
      preview.appendChild(row);
    });
    if (files.length > Number(input.dataset.maxCount)) error.textContent = `Choose at most ${input.dataset.maxCount} files at a time.`;
    else if (files.reduce(/* Sums selected file sizes in bytes. */ (total, file) => total + file.size, 0) > Number(input.dataset.maxTotal)) error.textContent = 'The selected files exceed the total upload size limit.';
    else {
      for (const file of files) {
        const extension = '.' + file.name.split('.').pop().toLowerCase();
        if (!file.size) error.textContent = `Choose a non-empty file: ${file.name}.`;
        else if (file.size > Number(input.dataset.maxSize)) error.textContent = `${file.name} exceeds the per-file upload size limit.`;
        else if (!input.accept.split(',').includes(extension)) error.textContent = `${file.name} has an unsupported file type.`;
        if (error.textContent) break;
      }
    }
    return !error.textContent;
  };
  input.addEventListener('change', validate);
  // Registers handlers that prevent browser navigation and highlight the drop zone during a drag.
  ['dragenter', 'dragover'].forEach(name => zone.addEventListener(name, event => { event.preventDefault(); zone.classList.add('dragging'); }));
  // Registers handlers that prevent browser navigation and remove the drag highlight.
  ['dragleave', 'drop'].forEach(name => zone.addEventListener(name, event => { event.preventDefault(); zone.classList.remove('dragging'); }));
  // Copies dropped files to the file input and validates the complete selection.
  zone.addEventListener('drop', event => {
    if (!event.dataTransfer.files.length) return;
    input.files = event.dataTransfer.files;
    validate();
  });
  // Blocks invalid uploads and disables the submit button while a valid upload is in progress.
  document.getElementById('upload-form').addEventListener('submit', event => {
    if (!validate()) { event.preventDefault(); return; }
    const button = document.getElementById('upload-button');
    button.disabled = true; button.textContent = 'Uploading…';
  });
}
