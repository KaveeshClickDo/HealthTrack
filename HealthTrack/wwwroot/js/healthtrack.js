// Site-wide script, loaded on every page by _Layout: confirmations, auto-submitting fields, progress bars and the
// notification bell.
(function () {
    'use strict';

    // Destructive actions confirm first: a form with data-confirm="..." asks before it is sent.
    // (A Content-Security-Policy rules out onclick attributes, so this one listener serves every page.)
    document.addEventListener('submit', function (event) {
        var form = event.target;
        if (!(form instanceof HTMLFormElement)) {
            return;
        }

        var message = form.getAttribute('data-confirm');
        if (message && !window.confirm(message)) {
            event.preventDefault();
        }
    });

    // A field with data-auto-submit sends its GET form as soon as it changes, for example the reschedule page's day.
    document.addEventListener('change', function (event) {
        var field = event.target;
        if (field instanceof HTMLElement && field.hasAttribute('data-auto-submit') && field.form) {
            if (field.form.requestSubmit) {
                field.form.requestSubmit();
            } else {
                field.form.submit();
            }
        }
    });

    // Progress bars: data-progress-width="40" sets the width to 40%. Set through the style property rather than a
    // style attribute in the markup, which a Content-Security-Policy without 'unsafe-inline' styles would block.
    document.addEventListener('DOMContentLoaded', function () {
        document.querySelectorAll('[data-progress-width]').forEach(function (bar) {
            var percent = Math.max(0, Math.min(100, Number(bar.getAttribute('data-progress-width')) || 0));
            bar.style.width = percent + '%';
        });
    });

    // The notification bell (signed-in pages only, spec 1.8): polls /Notifications/Poll on load and every 60 seconds,
    // updates the unread badge and shows a toast for each notification newer than the last one shown. Clicking a toast
    // marks it read and opens its page.
    document.addEventListener('DOMContentLoaded', function () {
        var bell = document.querySelector('[data-notification-bell]');
        var container = document.getElementById('notification-toasts');
        var badge = document.getElementById('notification-count');
        if (!bell || !container || !badge || !window.fetch) {
            return;
        }

        var pollUrl = bell.getAttribute('data-poll-url');
        var markReadUrl = bell.getAttribute('data-mark-read-url');
        var tokenField = container.querySelector('input[name="__RequestVerificationToken"]');
        var storageKey = 'healthtrack.lastToastId';
        var timer = null;

        // X-Requested-With makes an expired session answer 401 instead of redirecting to the login page.
        var ajaxHeaders = { 'X-Requested-With': 'XMLHttpRequest' };

        // The last toast shown, kept for the browser tab's session so moving between pages doesn't repeat toasts.
        // Storage can be blocked (private windows, strict settings): then toasts may repeat, nothing worse.
        function lastShownId() {
            try {
                return Number(window.sessionStorage.getItem(storageKey)) || 0;
            } catch (e) {
                return 0;
            }
        }

        function rememberShown(id) {
            try {
                window.sessionStorage.setItem(storageKey, String(id));
            } catch (e) {
                // See lastShownId.
            }
        }

        // The same rule as Url.IsLocalUrl: one leading "/", not "//" or "/\".
        function isLocalUrl(url) {
            return typeof url === 'string' && /^\/(?![\/\\])/.test(url);
        }

        function updateBadge(count) {
            badge.textContent = count > 99 ? '99+' : String(count);
            badge.classList.toggle('d-none', count === 0);
            bell.setAttribute('aria-label', count === 0 ? 'Notifications' : 'Notifications, ' + count + ' unread');
        }

        function markAsRead(id) {
            var headers = { 'X-Requested-With': 'XMLHttpRequest', 'RequestVerificationToken': tokenField ? tokenField.value : '' };
            var body = new URLSearchParams();
            body.append('id', String(id));
            return fetch(markReadUrl, { method: 'POST', credentials: 'same-origin', headers: headers, body: body });
        }

        // Built with textContent only: titles and messages can contain names users typed.
        function showToast(item) {
            var toast = document.createElement('div');
            toast.className = 'toast';
            toast.setAttribute('role', 'status');
            toast.setAttribute('aria-live', 'polite');
            toast.setAttribute('aria-atomic', 'true');

            var header = document.createElement('div');
            header.className = 'toast-header';
            var title = document.createElement('strong');
            title.className = 'me-auto';
            title.textContent = item.title;
            var time = document.createElement('small');
            time.className = 'text-muted ms-2';
            time.textContent = item.createdAt;
            var close = document.createElement('button');
            close.type = 'button';
            close.className = 'btn-close';
            close.setAttribute('data-bs-dismiss', 'toast');
            close.setAttribute('aria-label', 'Close');
            header.append(title, time, close);

            // A button, so the toast can be opened from the keyboard too.
            var body = document.createElement('button');
            body.type = 'button';
            body.className = 'toast-body notification-toast-body';
            body.textContent = item.message;
            body.addEventListener('click', function () {
                markAsRead(item.id).catch(function () { return null; }).then(function () {
                    if (isLocalUrl(item.linkUrl)) {
                        window.location.href = item.linkUrl;
                    } else {
                        window.bootstrap.Toast.getOrCreateInstance(toast).hide();
                        poll();
                    }
                });
            });

            toast.append(header, body);
            toast.addEventListener('hidden.bs.toast', function () {
                toast.remove();
            });
            container.appendChild(toast);
            window.bootstrap.Toast.getOrCreateInstance(toast, { delay: 10000 }).show();
        }

        function stopPolling() {
            if (timer !== null) {
                window.clearInterval(timer);
                timer = null;
            }
        }

        function poll() {
            fetch(pollUrl, { credentials: 'same-origin', headers: ajaxHeaders })
                .then(function (response) {
                    if (response.status === 401) {
                        // Signed out (the session timed out or the account was suspended): stop asking.
                        stopPolling();
                        return null;
                    }

                    return response.ok ? response.json() : null;
                })
                .then(function (data) {
                    if (!data) {
                        return;
                    }

                    updateBadge(data.count);
                    var last = lastShownId();
                    var newest = last;
                    // The poll lists newest first; toasts go up oldest first so the newest ends up nearest the corner.
                    data.items.slice().reverse().forEach(function (item) {
                        if (item.id > last) {
                            showToast(item);
                            newest = Math.max(newest, item.id);
                        }
                    });
                    if (newest > last) {
                        rememberShown(newest);
                    }
                })
                .catch(function () {
                    // Offline for a moment: the next tick tries again.
                });
        }

        poll();
        timer = window.setInterval(poll, 60000);
    });
})();
