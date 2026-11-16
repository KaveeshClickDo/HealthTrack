// Trainer registration: a licence number is required unless the professional is a fitness trainer (BR-03).
// The server checks the same rule in RegisterTrainerViewModel.Validate.
(function ($) {
    'use strict';

    $(function () {
        var $profession = $('[data-profession]');
        var $types = $profession.find('input[type="radio"]');
        var $licence = $('#LicenseNumber');
        if (!$types.length || !$licence.length || !$licence.rules) {
            return;
        }

        var fitnessTrainer = String($profession.data('fitness-trainer'));

        function licenceRequired() {
            var value = $types.filter(':checked').val();
            return value !== undefined && value !== fitnessTrainer;
        }

        $licence.rules('add', {
            required: licenceRequired,
            messages: { required: $licence.data('required-message') }
        });

        function update() {
            $('#licence-required').toggleClass('d-none', !licenceRequired());
        }

        $types.on('change', function () {
            update();
            if ($licence.hasClass('input-validation-error') || $licence.val() !== '') {
                $licence.valid();
            }
        });
        update();
    });
})(window.jQuery);
