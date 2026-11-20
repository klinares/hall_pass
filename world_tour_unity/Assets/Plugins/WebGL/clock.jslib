// The browser's local date as "YYYY-MM-DD" (js/util.js localISO in the web game). DateTime.Now in a Unity web player
// may be UTC, which would open a new week hours early in the evening. Used by Game/Util/LocalClock.cs.
mergeInto(LibraryManager.library, {
  WT_LocalDateIso: function () {
    var d = new Date();
    var pad = function (n, w) { var s = String(n); while (s.length < w) s = '0' + s; return s; };
    var iso = pad(d.getFullYear(), 4) + '-' + pad(d.getMonth() + 1, 2) + '-' + pad(d.getDate(), 2);
    var size = lengthBytesUTF8(iso) + 1;
    var buffer = _malloc(size);
    stringToUTF8(iso, buffer, size);
    return buffer;
  }
});
