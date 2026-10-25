using HealthTrack.Models.Enums;

namespace HealthTrack.Helpers
{
    /// <summary>
    /// The health formulas of spec 1.10. Calories and BMI are always calculated here, never typed in (BR-14).
    /// </summary>
    public static class HealthCalculator
    {
        /// <summary>BMI below this is Underweight (WHO).</summary>
        public const decimal NormalBmiFrom = 18.5m;

        /// <summary>BMI from this is Overweight (WHO).</summary>
        public const decimal OverweightBmiFrom = 25.0m;

        /// <summary>BMI from this is Obese (WHO).</summary>
        public const decimal ObeseBmiFrom = 30.0m;

        /// <summary>The suggested calorie target never goes below this (spec 1.10).</summary>
        public const int MinimumCalorieTarget = 1200;

        /// <summary>Daily deficit for the LoseWeight goal.</summary>
        public const int WeightLossDeficit = 500;

        /// <summary>Daily surplus for the GainMuscle goal.</summary>
        public const int MuscleGainSurplus = 300;

        /// <summary>
        /// Body mass index: weight in kg divided by the square of height in metres, rounded to 1 decimal place.
        /// </summary>
        /// <param name="weightKg">Weight in kilograms; must be positive.</param>
        /// <param name="heightCm">Height in centimetres; must be positive.</param>
        /// <returns>The BMI to 1 decimal place.</returns>
        public static decimal CalculateBmi(decimal weightKg, decimal heightCm)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(weightKg);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(heightCm);

            var heightM = heightCm / 100m;
            return Math.Round(weightKg / (heightM * heightM), 1, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// The WHO adult category for a BMI: under 18.5 Underweight, 18.5–24.9 Normal, 25.0–29.9 Overweight, 30.0 and over Obese.
        /// </summary>
        /// <param name="bmi">A BMI, normally from <see cref="CalculateBmi"/>.</param>
        /// <returns>The BMI category.</returns>
        public static BmiCategory GetBmiCategory(decimal bmi) => bmi switch
        {
            < NormalBmiFrom => BmiCategory.Underweight,
            < OverweightBmiFrom => BmiCategory.Normal,
            < ObeseBmiFrom => BmiCategory.Overweight,
            _ => BmiCategory.Obese
        };

        /// <summary>
        /// Age in whole years on <paramref name="today"/>; the birthday counts from the day itself.
        /// </summary>
        /// <param name="dateOfBirth">The date of birth (the time part is ignored).</param>
        /// <param name="today">The date to calculate the age on (the time part is ignored).</param>
        /// <returns>The age in completed years.</returns>
        public static int CalculateAge(DateTime dateOfBirth, DateTime today)
        {
            var age = today.Year - dateOfBirth.Year;
            if (dateOfBirth.Date > today.Date.AddYears(-age))
            {
                age--;
            }

            return age;
        }

        /// <summary>
        /// Basal metabolic rate by Mifflin–St Jeor: 10W + 6.25H − 5A + s, where s is +5 for men, −161 for women and −78 otherwise.
        /// </summary>
        /// <param name="weightKg">Weight in kilograms.</param>
        /// <param name="heightCm">Height in centimetres.</param>
        /// <param name="age">Age in years.</param>
        /// <param name="gender">Gender, which picks the constant s.</param>
        /// <returns>BMR in kcal per day, unrounded.</returns>
        public static decimal CalculateBmr(decimal weightKg, decimal heightCm, int age, Gender gender)
        {
            var genderConstant = gender switch
            {
                Gender.Male => 5m,
                Gender.Female => -161m,
                _ => -78m
            };

            return 10m * weightKg + 6.25m * heightCm - 5m * age + genderConstant;
        }

        /// <summary>
        /// Total daily energy expenditure: BMR × the activity factor (1.2, 1.375, 1.55, 1.725 or 1.9 from Sedentary to Extra active).
        /// </summary>
        /// <param name="bmr">The BMR from <see cref="CalculateBmr"/>.</param>
        /// <param name="activityLevel">The client's activity level.</param>
        /// <returns>TDEE in kcal per day, unrounded.</returns>
        public static decimal CalculateTdee(decimal bmr, ActivityLevel activityLevel)
        {
            var factor = activityLevel switch
            {
                ActivityLevel.Sedentary => 1.2m,
                ActivityLevel.LightlyActive => 1.375m,
                ActivityLevel.ModeratelyActive => 1.55m,
                ActivityLevel.VeryActive => 1.725m,
                ActivityLevel.ExtraActive => 1.9m,
                _ => throw new ArgumentOutOfRangeException(nameof(activityLevel), activityLevel, "Unknown activity level.")
            };

            return bmr * factor;
        }

        /// <summary>
        /// The suggested daily calorie target: TDEE − 500 to lose weight, TDEE + 300 to gain muscle, otherwise TDEE;
        /// rounded to whole kcal and never below 1,200 kcal.
        /// </summary>
        /// <param name="tdee">The TDEE from <see cref="CalculateTdee"/>.</param>
        /// <param name="goal">The client's fitness goal.</param>
        /// <returns>The suggested target in whole kcal.</returns>
        public static int SuggestCalorieTarget(decimal tdee, FitnessGoal goal)
        {
            var target = goal switch
            {
                FitnessGoal.LoseWeight => tdee - WeightLossDeficit,
                FitnessGoal.GainMuscle => tdee + MuscleGainSurplus,
                _ => tdee
            };

            return Math.Max(MinimumCalorieTarget, (int)Math.Round(target, 0, MidpointRounding.AwayFromZero));
        }

        /// <summary>
        /// Calories burned: MET × body weight in kg × minutes / 60, rounded to 2 decimal places (the stored precision).
        /// </summary>
        /// <param name="met">The exercise's MET value.</param>
        /// <param name="weightKg">The client's body weight in kilograms.</param>
        /// <param name="minutes">How long the exercise lasted.</param>
        /// <returns>kcal burned to 2 decimal places.</returns>
        public static decimal CaloriesBurned(decimal met, decimal weightKg, int minutes) =>
            Math.Round(met * weightKg * minutes / 60m, 2, MidpointRounding.AwayFromZero);
    }
}
