/* World Tour save export and import for the web build (Game/Office/SaveFiles.cs). Everything stays in the
   page: a download is a Blob behind an <a download> link, a pick is an <input type="file"> read with
   FileReader, a copy goes to the clipboard, and a paste is taken from the browser's paste event. No network.
   Results come back by polling (WT_FilePickState / WT_FilePickTake, WT_PasteTake), so no callbacks into C#
   are needed. Every call is wrapped and reports failure instead of throwing. */
mergeInto(LibraryManager.library, {
  $WTFile: { pick: { state: 0, text: null, input: null }, paste: { armed: 0, text: null, hooked: 0, max: 0, big: 0, seq: 0, got: -1 } },

  /* 1 when the browser was asked to save the file, 0 when it couldn't */
  WT_FileDownload__deps: ['$WTFile'],
  WT_FileDownload: function (namePtr, textPtr, typePtr) {
    try {
      var name = UTF8ToString(namePtr), text = UTF8ToString(textPtr), type = UTF8ToString(typePtr) || 'application/json';
      var url = URL.createObjectURL(new Blob([text], { type: type }));
      var a = document.createElement('a');
      a.href = url; a.download = name; a.style.display = 'none'; a.rel = 'noopener';
      document.body.appendChild(a);
      a.click();
      setTimeout(function () { try { URL.revokeObjectURL(url); a.remove(); } catch (e) { } }, 4000);
      return 1;
    } catch (e) { return 0; }
  },

  /* opens the file picker; the result arrives through WT_FilePickState. 1 when the picker was asked to open */
  WT_FilePickOpen__deps: ['$WTFile'],
  WT_FilePickOpen: function (maxBytes) {
    var p = WTFile.pick;
    try {
      if (p.input) { try { p.input.remove(); } catch (e) { } }
      p.state = 1; p.text = null;
      var inp = document.createElement('input');
      inp.type = 'file'; inp.accept = '.json,.txt,application/json,text/plain'; inp.style.display = 'none';
      inp.addEventListener('change', function () {
        var f = inp.files && inp.files[0];
        if (!f) { p.state = 3; return; }
        if (f.size > maxBytes) { p.state = 4; return; }
        var r = new FileReader();
        r.onload = function () { p.text = String(r.result || ''); p.state = 2; };
        r.onerror = function () { p.state = 5; };
        r.readAsText(f);
      });
      inp.addEventListener('cancel', function () { p.state = 3; });
      document.body.appendChild(inp);
      p.input = inp;
      inp.click();
      return 1;
    } catch (e) { p.state = 5; return 0; }
  },

  /* 0 nothing asked, 1 waiting, 2 read (take it), 3 cancelled, 4 too large, 5 couldn't read */
  WT_FilePickState__deps: ['$WTFile'],
  WT_FilePickState: function () { return WTFile.pick.state; },

  /* the file's text once (state 2), then the picker goes back to state 0 */
  WT_FilePickTake__deps: ['$WTFile'],
  WT_FilePickTake: function () {
    var p = WTFile.pick, v = p.text;
    p.text = null; p.state = 0;
    if (p.input) { try { p.input.remove(); } catch (e) { } p.input = null; }
    if (v === null || v === undefined) return null;
    var size = lengthBytesUTF8(v) + 1, buf = _malloc(size);
    stringToUTF8(v, buf, size);
    return buf;
  },

  /* puts text on the clipboard. 1 when a copy was made or started, 0 when the browser refused */
  WT_ClipboardCopy__deps: ['$WTFile'],
  WT_ClipboardCopy: function (textPtr) {
    var text = UTF8ToString(textPtr);
    try {
      if (navigator.clipboard && window.isSecureContext) { navigator.clipboard.writeText(text).catch(function () { }); return 1; }
    } catch (e) { }
    try {
      var ta = document.createElement('textarea'), back = document.activeElement;
      ta.value = text; ta.setAttribute('readonly', ''); ta.style.position = 'fixed'; ta.style.left = '-9999px';
      document.body.appendChild(ta); ta.select();
      var ok = document.execCommand('copy');
      ta.remove();
      if (back && back.focus) back.focus();
      return ok ? 1 : 0;
    } catch (e) { return 0; }
  },

  /* 1 while the office's paste box has focus: the next paste (ctrl+v) is kept for WT_PasteTake, unless it is
     longer than maxChars (then WT_PasteState says 2) */
  WT_PasteArm__deps: ['$WTFile'],
  WT_PasteArm: function (on, maxChars) {
    var q = WTFile.paste;
    try {
      if (!q.hooked) {
        q.hooked = 1;
        /* Unity's player cancels key events it handles, which would also cancel the browser's own paste. While the
           paste box is armed, ctrl+v (cmd+v) is kept away from the player so the browser pastes and fires 'paste'. */
        var keep = function (t) {
          t = String(t || '');
          if (q.max > 0 && t.length > q.max) { q.big = 1; q.text = null; } else { q.big = 0; q.text = t; }
        };
        window.addEventListener('keydown', function (e) {
          if (!q.armed) return;
          if (!((e.ctrlKey || e.metaKey) && !e.altKey && (e.key === 'v' || e.key === 'V'))) return;
          e.stopImmediatePropagation();
          /* If something registered earlier still cancelled the key, no 'paste' event comes: then read the clipboard
             directly (the key press is the user gesture; the browser may ask once for permission). */
          var asked = ++q.seq;
          setTimeout(function () {
            if (q.seq !== asked || q.got === asked || !q.armed) return;
            try {
              if (navigator.clipboard && navigator.clipboard.readText)
                navigator.clipboard.readText().then(function (t) { if (q.armed && q.got !== asked) { q.got = asked; keep(t); } }, function () { });
            } catch (err) { }
          }, 150);
        }, true);
        document.addEventListener('paste', function (e) {
          if (!q.armed) return;
          try {
            q.got = q.seq;
            keep(e.clipboardData ? e.clipboardData.getData('text') : '');
            e.preventDefault();
          } catch (err) { }
        }, true);
      }
      q.armed = on ? 1 : 0;
      q.max = maxChars | 0;
      if (!on) { q.text = null; q.big = 0; }
      return 1;
    } catch (e) { return 0; }
  },

  /* 0 nothing pasted, 1 a paste is waiting (take it), 2 the paste was over the limit (cleared by this call) */
  WT_PasteState__deps: ['$WTFile'],
  WT_PasteState: function () {
    var q = WTFile.paste;
    if (q.big) { q.big = 0; return 2; }
    return q.text !== null && q.text !== undefined ? 1 : 0;
  },

  /* the last pasted text once, or null */
  WT_PasteTake__deps: ['$WTFile'],
  WT_PasteTake: function () {
    var q = WTFile.paste, v = q.text;
    q.text = null;
    if (v === null || v === undefined) return null;
    var size = lengthBytesUTF8(v) + 1, buf = _malloc(size);
    stringToUTF8(v, buf, size);
    return buf;
  }
});
