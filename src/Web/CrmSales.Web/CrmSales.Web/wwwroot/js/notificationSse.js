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
