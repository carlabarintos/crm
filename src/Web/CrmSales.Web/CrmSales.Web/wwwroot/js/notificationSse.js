// Insert text at the current cursor position inside a textarea, then fire an
// input event so Blazor's @bind:event="oninput" picks up the change.
window.insertAtCursor = function (id, text) {
    var el = document.getElementById(id);
    if (!el) return;
    var start = el.selectionStart;
    var end = el.selectionEnd;
    el.value = el.value.substring(0, start) + text + el.value.substring(end);
    el.selectionStart = el.selectionEnd = start + text.length;
    el.dispatchEvent(new Event('input', { bubbles: true }));
    el.focus();
};

// Scroll detection for infinite-scroll dropdowns
window.isScrolledNearBottom = function (element, threshold) {
    if (!element) return false;
    return element.scrollTop + element.clientHeight >= element.scrollHeight - threshold;
};

// CSV download helper
window.downloadCsv = function (filename, content) {
    var blob = new Blob([content], { type: 'text/csv;charset=utf-8;' });
    var url = URL.createObjectURL(blob);
    var a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
};

// Native EventSource wrapper for SSE notifications
window.notifSse = (function () {
    let _src = null;
    let _dotNetRef = null;

    return {
        start: function (url, dotNetRef) {
            if (_src) {
                _src.close();
                _src = null;
            }
            _dotNetRef = dotNetRef;
            _src = new EventSource(url);

            _src.onmessage = function (e) {
                _dotNetRef.invokeMethodAsync('OnSseMessage', e.data);
            };

            _src.onerror = function () {
                // readyState 2 = CLOSED (permanent failure, not a transient retry)
                if (_src && _src.readyState === EventSource.CLOSED) {
                    _dotNetRef.invokeMethodAsync('OnSseClosed');
                }
            };
        },

        stop: function () {
            if (_src) {
                _src.close();
                _src = null;
            }
        }
    };
}());
