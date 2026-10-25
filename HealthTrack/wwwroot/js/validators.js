// Browser-side rules for the custom validation attributes in HealthTrack.Security.
// Loaded by _ValidationScriptsPartial after jQuery Validation and the unobtrusive adapter,
// so the adapters exist before the forms are parsed on page load.
(function ($) {
    'use strict';

    if (!$ || !$.validator || !$.validator.unobtrusive) {
        return;
    }

    // [MinimumAge]: the server sends the latest allowed date of birth (yyyy-MM-dd); ISO dates compare as strings.
    $.validator.addMethod('minimumage', function (value, element, latest) {
        return this.optional(element) || value <= latest;
    });
    $.validator.unobtrusive.adapters.add('minimumage', ['max'], function (options) {
        options.rules.minimumage = options.params.max;
        options.messages.minimumage = options.message;
    });

    // [NotInFuture]: compares a date (yyyy-MM-dd) or date and time (yyyy-MM-ddTHH:mm[:ss]) with the browser's local time.
    function localNow() {
        var now = new Date();
        var pad = function (n) { return (n < 10 ? '0' : '') + n; };
        return now.getFullYear() + '-' + pad(now.getMonth() + 1) + '-' + pad(now.getDate()) +
            'T' + pad(now.getHours()) + ':' + pad(now.getMinutes()) + ':' + pad(now.getSeconds());
    }
    $.validator.addMethod('notinfuture', function (value, element) {
        return this.optional(element) || value <= localNow().substring(0, value.length);
    });
    $.validator.unobtrusive.adapters.addBool('notinfuture');
})(window.jQuery);
