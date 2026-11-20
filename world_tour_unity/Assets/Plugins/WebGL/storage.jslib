/* World Tour save storage for the web build (Game/Save/SaveStore.cs). One localStorage key holds the
   whole save. Every call is wrapped: if storage is missing, blocked or full, it reports failure and the
   game carries on with its in-memory copy, as the web game's js/store.js does. */
mergeInto(LibraryManager.library, {
  /* 1 when localStorage can be written and read back, else 0 */
  WT_StorageAvailable: function () {
    try {
      var s = window.localStorage, probe = '__wt_probe';
      s.setItem(probe, '1');
      s.removeItem(probe);
      return 1;
    } catch (e) { return 0; }
  },
  /* the stored text, or null when the key is missing or storage throws */
  WT_StorageGet: function (keyPtr) {
    var v = null;
    try { v = window.localStorage.getItem(UTF8ToString(keyPtr)); } catch (e) { v = null; }
    if (v === null || v === undefined) return null;
    var size = lengthBytesUTF8(v) + 1;
    var buf = _malloc(size);
    stringToUTF8(v, buf, size);
    return buf;
  },
  /* 1 when written, 0 when storage refused (blocked, full) */
  WT_StorageSet: function (keyPtr, valuePtr) {
    try { window.localStorage.setItem(UTF8ToString(keyPtr), UTF8ToString(valuePtr)); return 1; }
    catch (e) { return 0; }
  },
  WT_StorageRemove: function (keyPtr) {
    try { window.localStorage.removeItem(UTF8ToString(keyPtr)); return 1; }
    catch (e) { return 0; }
  }
});
