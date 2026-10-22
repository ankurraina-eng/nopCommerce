/**
 * Wraps jQuery.ajax to retry HTTP 429 with exponential backoff,
 * honoring Retry-After. Max 5 attempts (1 initial + 4 retries).
 */
(function (window) {
    'use strict';

    var $ = window.jQuery;
    if (!$ || typeof $.ajax !== 'function' || $.ajax._nopRateLimitBackoff) {
        return;
    }

    var originalAjax = $.ajax;
    var MAX_ATTEMPTS = 5;
    var MAX_DELAY_SEC = 32;

    function parseRetryAfterSeconds(jqXHR) {
        if (!jqXHR || typeof jqXHR.getResponseHeader !== 'function') {
            return null;
        }
        var header = jqXHR.getResponseHeader('Retry-After');
        if (header == null || header === '') {
            return null;
        }
        var seconds = parseInt(header, 10);
        if (isNaN(seconds) || seconds < 0) {
            return null;
        }
        return seconds;
    }

    function delaySecondsForRetry(retryIndex, jqXHR) {
        // retryIndex: 1-based index of the upcoming retry (first retry = 1)
        var retryAfter = parseRetryAfterSeconds(jqXHR);
        if (retryAfter !== null) {
            return Math.min(retryAfter, MAX_DELAY_SEC);
        }
        return Math.min(MAX_DELAY_SEC, Math.pow(2, retryIndex - 1));
    }

    function wrapAjax(urlOrSettings, maybeSettings) {
        var settings;
        if (typeof urlOrSettings === 'string') {
            settings = $.extend({}, maybeSettings || {});
            settings.url = urlOrSettings;
        } else {
            settings = $.extend({}, urlOrSettings || {});
        }

        var userSuccess = settings.success;
        var userError = settings.error;
        var userComplete = settings.complete;
        delete settings.success;
        delete settings.error;
        delete settings.complete;

        var deferred = $.Deferred();
        var promise = deferred.promise();
        var activeXhr = null;
        var aborted = false;
        var retryTimer = null;
        var attempt = 0;

        function clearRetryTimer() {
            if (retryTimer !== null) {
                clearTimeout(retryTimer);
                retryTimer = null;
            }
        }

        function finalizeSuccess(data, textStatus, jqXHR) {
            if (typeof userSuccess === 'function') {
                userSuccess.call(settings.context, data, textStatus, jqXHR);
            }
            deferred.resolveWith(settings.context, [data, textStatus, jqXHR]);
            if (typeof userComplete === 'function') {
                userComplete.call(settings.context, jqXHR, textStatus);
            }
        }

        function finalizeError(jqXHR, textStatus, errorThrown) {
            if (typeof userError === 'function') {
                userError.call(settings.context, jqXHR, textStatus, errorThrown);
            }
            deferred.rejectWith(settings.context, [jqXHR, textStatus, errorThrown]);
            if (typeof userComplete === 'function') {
                userComplete.call(settings.context, jqXHR, textStatus);
            }
        }

        function scheduleRetry(jqXHR) {
            var retryIndex = attempt; // upcoming retry's 1-based index after this failed attempt
            var waitMs = delaySecondsForRetry(retryIndex, jqXHR) * 1000;
            retryTimer = setTimeout(function () {
                retryTimer = null;
                if (aborted) {
                    return;
                }
                send();
            }, waitMs);
        }

        function send() {
            if (aborted) {
                return;
            }

            attempt += 1;
            var requestSettings = $.extend({}, settings);

            activeXhr = originalAjax(requestSettings);

            activeXhr.done(function (data, textStatus, jqXHR) {
                if (aborted) {
                    return;
                }
                finalizeSuccess(data, textStatus, jqXHR);
            });

            activeXhr.fail(function (jqXHR, textStatus, errorThrown) {
                if (aborted) {
                    // Abort may already have finalized; avoid double-complete.
                    if (deferred.state() === 'pending') {
                        finalizeError(jqXHR, textStatus, errorThrown);
                    }
                    return;
                }

                var status = jqXHR && jqXHR.status;
                if (status === 429 && attempt < MAX_ATTEMPTS) {
                    // Intermediate 429: do not call user error/complete; retry.
                    scheduleRetry(jqXHR);
                    return;
                }

                finalizeError(jqXHR, textStatus, errorThrown);
            });
        }

        promise.abort = function () {
            if (aborted) {
                return;
            }
            aborted = true;
            clearRetryTimer();
            // Abort in-flight request; its fail handler finalizes.
            // If waiting between retries (prior jqXHR already finished), finalize here.
            if (activeXhr && activeXhr.readyState !== 4 && typeof activeXhr.abort === 'function') {
                activeXhr.abort();
            } else if (deferred.state() === 'pending') {
                finalizeError(
                    { status: 0, statusText: 'abort' },
                    'abort',
                    'abort'
                );
            }
        };

        function delegateToActiveXhr(methodName) {
            return function () {
                if (activeXhr && typeof activeXhr[methodName] === 'function') {
                    return activeXhr[methodName].apply(activeXhr, arguments);
                }
                return methodName === 'getAllResponseHeaders' ? '' : null;
            };
        }

        promise.getResponseHeader = delegateToActiveXhr('getResponseHeader');
        promise.getAllResponseHeaders = delegateToActiveXhr('getAllResponseHeaders');
        promise.setRequestHeader = delegateToActiveXhr('setRequestHeader');
        promise.overrideMimeType = delegateToActiveXhr('overrideMimeType');

        Object.defineProperty(promise, 'readyState', {
            enumerable: true,
            configurable: true,
            get: function () {
                return activeXhr ? activeXhr.readyState : 0;
            }
        });

        Object.defineProperty(promise, 'status', {
            enumerable: true,
            configurable: true,
            get: function () {
                return activeXhr ? activeXhr.status : 0;
            }
        });

        send();
        return promise;
    }

    wrapAjax._nopRateLimitBackoff = true;
    $.ajax = wrapAjax;
})(window);
