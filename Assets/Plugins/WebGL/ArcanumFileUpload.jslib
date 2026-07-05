// WebGL file-upload bridge for Arcanum data (.dat archives).
//
// The browser has no access to the host filesystem, so the player must upload their own legitimate
// Arcanum .dat files. This opens a native file picker, reads each selected file, and writes the bytes
// into the emscripten virtual filesystem under /arcanum/<name> — where Unity's System.IO (and thus
// DatVirtualFileSystem.MountFile) can read them exactly like a real file. Then it reports the written
// names back to C# via SendMessage.
//
// Memory: the bytes live once in the MEMFS heap; DatArchive streams entries from there on demand
// (it does NOT copy the whole archive into a MemoryStream), so this is about as light as WebGL allows.
mergeInto(LibraryManager.library, {
  // goName / cbOk / cbErr are UTF8 pointers (the GameObject name + the two SendMessage method names).
  ArcanumPickFiles: function (goNamePtr, cbOkPtr, cbErrPtr) {
    var goName = UTF8ToString(goNamePtr);
    var cbOk = UTF8ToString(cbOkPtr);
    var cbErr = UTF8ToString(cbErrPtr);

    var input = document.createElement('input');
    input.type = 'file';
    input.multiple = true;
    input.accept = '.dat';
    input.style.display = 'none';
    document.body.appendChild(input);

    var cleanup = function () { try { document.body.removeChild(input); } catch (e) {} };

    input.onchange = function () {
      var files = input.files;
      if (!files || files.length === 0) { cleanup(); SendMessage(goName, cbErr, 'no files selected'); return; }

      try { FS.mkdir('/arcanum'); } catch (e) { /* already exists */ }

      var remaining = files.length;
      var names = [];
      var failed = false;

      for (var i = 0; i < files.length; i++) {
        (function (file) {
          var reader = new FileReader();
          reader.onload = function () {
            try {
              FS.writeFile('/arcanum/' + file.name, new Uint8Array(reader.result));
              names.push(file.name);
            } catch (e) {
              failed = true;
              console.error('ArcanumFileUpload: FS.writeFile failed for ' + file.name + ': ' + e);
            }
            if (--remaining === 0) {
              cleanup();
              if (failed && names.length === 0) SendMessage(goName, cbErr, 'failed to write files (out of memory?)');
              else SendMessage(goName, cbOk, names.join('|'));
            }
          };
          reader.onerror = function () {
            failed = true;
            if (--remaining === 0) { cleanup(); SendMessage(goName, cbErr, 'read error'); }
          };
          reader.readAsArrayBuffer(file);
        })(files[i]);
      }
    };

    // The picker must open within the browser's user-activation window. Calling click() from the
    // DllImport invoked on a Unity pointer-up generally counts; if a browser blocks it, the console
    // logs a "user gesture" warning and the player can retry with a direct click.
    input.click();
  }
});
