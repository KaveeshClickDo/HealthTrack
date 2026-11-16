// Plan editor: shows the exercise chosen in the "Add an exercise" form as a photo tile beside it (the option's
// data-photo and data-group).
(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        var picker = document.querySelector('[data-exercise-picker]');
        var preview = document.querySelector('[data-exercise-preview]');
        if (!picker || !preview) {
            return;
        }

        var photo = preview.querySelector('[data-preview-photo]');
        var kicker = preview.querySelector('[data-preview-kicker]');
        var name = preview.querySelector('[data-preview-name]');

        function update() {
            var option = picker.selectedIndex > 0 ? picker.options[picker.selectedIndex] : null;
            if (option) {
                photo.setAttribute('src', option.getAttribute('data-photo'));
                kicker.textContent = 'Selected · ' + (option.getAttribute('data-group') || '');
                name.textContent = option.textContent;
            } else {
                photo.removeAttribute('src');
                kicker.textContent = 'Choose an exercise';
                name.textContent = 'Its photo shows here';
            }
        }

        picker.addEventListener('change', update);
    });
})();
