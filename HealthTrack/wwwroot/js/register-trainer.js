// Trainer registration: a licence number is required unless the professional is a fitness trainer (BR-03).
// The server checks the same rule in RegisterTrainerViewModel.Validate.
(function ($) {
    'use strict';

    $(function () {
        var $type = $('#ProfessionalType');
        var $licence = $('#LicenseNumber');
        if (!$type.length || !$licence.length || !$licence.rules) {
            return;
        }

        var fitnessTrainer = String($type.data('fitness-trainer'));

        function licenceRequired() {
            var value = $type.val();
            return value !== '' && value !== fitnessTrainer;
        }

        $licence.rules('add', {
            required: licenceRequired,
            messages: { required: $licence.data('required-message') }
        });

        function update() {
            $('#licence-required').toggleClass('d-none', !licenceRequired());
        }

        $type.on('change', function () {
            update();
            if ($licence.hasClass('input-validation-error') || $licence.val() !== '') {
                $licence.valid();
            }
        });
        update();
    });
})(window.jQuery);
