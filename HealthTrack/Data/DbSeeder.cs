using HealthTrack.Models.Entities;
using HealthTrack.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Data
{
    /// <summary>
    /// Applies migrations and seeds reference and demo data at startup, so a fresh clone only needs F5.
    /// Every step seeds only into empty tables, so running it again changes nothing.
    /// </summary>
    public static class DbSeeder
    {
        /// <summary>
        /// Migrates the database, then runs the seed steps in order. Later phases add users, templates,
        /// availability and demo history after the libraries.
        /// </summary>
        /// <param name="services">The application's root service provider.</param>
        /// <param name="cancellationToken">Cancels the operation.</param>
        /// <returns>A task that completes when the database is ready.</returns>
        public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DbSeeder));

            // Migrations are relational-only; EF Core InMemory (tests) just creates the model.
            if (db.Database.IsRelational())
            {
                await db.Database.MigrateAsync(cancellationToken);
            }
            else
            {
                await db.Database.EnsureCreatedAsync(cancellationToken);
            }

            // 1. Libraries
            await SeedExercisesAsync(db, logger, cancellationToken);
            await SeedFoodItemsAsync(db, logger, cancellationToken);
        }

        private static async Task SeedExercisesAsync(ApplicationDbContext db, ILogger logger, CancellationToken cancellationToken)
        {
            if (await db.Exercises.AnyAsync(cancellationToken))
            {
                return;
            }

            // MET values from the 2011 Compendium of Physical Activities (Ainsworth et al.), using the closest listed activity.
            Exercise[] exercises =
            [
                // Cardio
                NewExercise("Brisk walking", ExerciseCategory.Cardio, MuscleGroup.Legs, null, 4.3m,
                    "Walk at about 5.5 km/h on a flat surface, swinging your arms naturally and keeping your posture tall."),
                NewExercise("Running (8 km/h)", ExerciseCategory.Cardio, MuscleGroup.Legs, null, 8.3m,
                    "Jog at a steady, conversational pace. Land softly with your feet under your hips."),
                NewExercise("Running (10 km/h)", ExerciseCategory.Cardio, MuscleGroup.Legs, null, 9.8m,
                    "Run at about 6 minutes per kilometre, breathing rhythmically and keeping your shoulders relaxed."),
                NewExercise("Stationary cycling", ExerciseCategory.Cardio, MuscleGroup.Legs, "Exercise bike", 6.8m,
                    "Pedal at a moderate effort (about 100 W) with the saddle set so your knee is slightly bent at the bottom."),
                NewExercise("Rowing machine", ExerciseCategory.Cardio, MuscleGroup.FullBody, "Rowing machine", 7.0m,
                    "Push with your legs first, then lean back slightly and pull the handle to your lower ribs. Return in reverse order."),
                NewExercise("Elliptical trainer", ExerciseCategory.Cardio, MuscleGroup.FullBody, "Elliptical trainer", 5.0m,
                    "Stride smoothly at a moderate effort, pushing and pulling the handles to involve your arms."),
                NewExercise("Swimming (freestyle)", ExerciseCategory.Cardio, MuscleGroup.FullBody, "Swimming pool", 5.8m,
                    "Swim front crawl at an easy to moderate pace, rotating your body and breathing every two or three strokes."),
                NewExercise("Stair climber", ExerciseCategory.Cardio, MuscleGroup.Legs, "Stair climber machine", 9.0m,
                    "Climb at a steady rhythm without leaning on the handrails, placing your whole foot on each step."),
                NewExercise("Jump rope", ExerciseCategory.Cardio, MuscleGroup.Legs, "Skipping rope", 11.8m,
                    "Skip at about 100–120 turns a minute on the balls of your feet, turning the rope with your wrists."),

                // Strength
                NewExercise("Barbell back squat", ExerciseCategory.Strength, MuscleGroup.Legs, "Barbell, squat rack", 5.0m,
                    "With the bar across your upper back, sit back and down until your thighs are parallel to the floor, then drive up through your heels."),
                NewExercise("Deadlift", ExerciseCategory.Strength, MuscleGroup.Back, "Barbell", 6.0m,
                    "With a flat back, hinge at the hips to grip the bar, then stand up by driving your hips forward. Lower it under control."),
                NewExercise("Bench press", ExerciseCategory.Strength, MuscleGroup.Chest, "Barbell, bench", 6.0m,
                    "Lie on the bench, lower the bar to mid-chest with your elbows at about 45 degrees, then press it back up."),
                NewExercise("Push-up", ExerciseCategory.Strength, MuscleGroup.Chest, null, 3.8m,
                    "Keep your body in a straight line from head to heels. Lower your chest towards the floor, then push back up."),
                NewExercise("Pull-up", ExerciseCategory.Strength, MuscleGroup.Back, "Pull-up bar", 3.8m,
                    "Hang with straight arms, then pull your chin over the bar without swinging. Lower yourself slowly."),
                NewExercise("Lat pulldown", ExerciseCategory.Strength, MuscleGroup.Back, "Cable machine", 3.5m,
                    "Pull the bar down to your upper chest, squeezing your shoulder blades together, then let it rise under control."),
                NewExercise("Seated cable row", ExerciseCategory.Strength, MuscleGroup.Back, "Cable machine", 3.5m,
                    "Sit tall and pull the handle to your stomach, keeping your elbows close to your body."),
                NewExercise("Dumbbell shoulder press", ExerciseCategory.Strength, MuscleGroup.Shoulders, "Dumbbells", 3.5m,
                    "Press the dumbbells from shoulder height to overhead without arching your lower back."),
                NewExercise("Lateral raise", ExerciseCategory.Strength, MuscleGroup.Shoulders, "Dumbbells", 3.5m,
                    "With a slight bend in your elbows, raise the dumbbells out to the sides to shoulder height, then lower them slowly."),
                NewExercise("Dumbbell biceps curl", ExerciseCategory.Strength, MuscleGroup.Arms, "Dumbbells", 3.5m,
                    "Keeping your elbows at your sides, curl the dumbbells up to your shoulders and lower them slowly."),
                NewExercise("Triceps dip", ExerciseCategory.Strength, MuscleGroup.Arms, "Parallel bars or a bench", 3.8m,
                    "Lower your body by bending your elbows to about 90 degrees, then straighten your arms to push back up."),
                NewExercise("Walking lunge", ExerciseCategory.Strength, MuscleGroup.Legs, null, 3.8m,
                    "Step forward and lower until both knees are bent at about 90 degrees, then step through into the next lunge."),
                NewExercise("Leg press", ExerciseCategory.Strength, MuscleGroup.Legs, "Leg press machine", 3.5m,
                    "Lower the platform until your knees reach about 90 degrees, then press it away without locking your knees."),
                NewExercise("Plank", ExerciseCategory.Strength, MuscleGroup.Core, "Exercise mat", 2.8m,
                    "Hold your body straight on your forearms and toes, bracing your stomach and squeezing your glutes."),
                NewExercise("Abdominal crunch", ExerciseCategory.Strength, MuscleGroup.Core, "Exercise mat", 2.8m,
                    "Lie on your back with your knees bent and curl your shoulders off the floor, then lower them slowly."),

                // HIIT
                NewExercise("Burpees", ExerciseCategory.HIIT, MuscleGroup.FullBody, null, 8.0m,
                    "From standing, drop to a squat, jump your feet back to a plank, return to the squat and jump up."),
                NewExercise("Jumping jacks", ExerciseCategory.HIIT, MuscleGroup.FullBody, null, 8.0m,
                    "Jump your feet out while raising your arms overhead, then jump back to the start, at a brisk pace."),
                NewExercise("Mountain climbers", ExerciseCategory.HIIT, MuscleGroup.Core, null, 8.0m,
                    "In a high plank, drive your knees towards your chest one after the other, as fast as you can with control."),
                NewExercise("Kettlebell swing", ExerciseCategory.HIIT, MuscleGroup.FullBody, "Kettlebell", 8.0m,
                    "Hinge at the hips and snap them forward to swing the kettlebell to chest height, keeping your arms loose."),
                NewExercise("Circuit training", ExerciseCategory.HIIT, MuscleGroup.FullBody, "Mixed gym equipment", 8.0m,
                    "Move between 6–10 stations with little rest, for example squats, push-ups, rows and step-ups."),

                // Flexibility
                NewExercise("Hatha yoga", ExerciseCategory.Flexibility, MuscleGroup.FullBody, "Yoga mat", 2.5m,
                    "Move slowly through standing, seated and lying poses, holding each one for several breaths."),
                NewExercise("Pilates", ExerciseCategory.Flexibility, MuscleGroup.Core, "Exercise mat", 3.0m,
                    "Controlled mat exercises that build core strength and mobility. Keep your breathing steady."),
                NewExercise("Full-body stretching", ExerciseCategory.Flexibility, MuscleGroup.FullBody, null, 2.3m,
                    "Stretch each major muscle group gently for 20–30 seconds without bouncing."),

                // Balance
                NewExercise("Tai chi", ExerciseCategory.Balance, MuscleGroup.FullBody, null, 3.0m,
                    "Flow slowly through the movements, shifting your weight smoothly from one leg to the other."),
                NewExercise("Single-leg balance", ExerciseCategory.Balance, MuscleGroup.Legs, null, 2.3m,
                    "Stand on one leg with a soft knee for 30 seconds, then switch. Close your eyes to make it harder."),
                NewExercise("Bosu ball squat", ExerciseCategory.Balance, MuscleGroup.Legs, "Bosu ball", 3.5m,
                    "Stand on the Bosu ball and squat slowly, keeping your knees in line with your toes."),

                // Sports
                NewExercise("Basketball", ExerciseCategory.Sports, MuscleGroup.FullBody, "Basketball", 6.5m,
                    "Casual play or shooting practice, with frequent sprints, jumps and changes of direction."),
                NewExercise("Football (casual)", ExerciseCategory.Sports, MuscleGroup.Legs, "Football", 7.0m,
                    "A casual game of football with a mix of jogging, sprinting and kicking."),
                NewExercise("Tennis", ExerciseCategory.Sports, MuscleGroup.FullBody, "Racket and balls", 7.3m,
                    "Rally or play a match, moving your feet to set up each shot."),
                NewExercise("Badminton", ExerciseCategory.Sports, MuscleGroup.FullBody, "Racket and shuttlecocks", 5.5m,
                    "Social singles or doubles with quick footwork around the court."),
                NewExercise("Boxing (punch bag)", ExerciseCategory.Sports, MuscleGroup.Arms, "Boxing gloves, punch bag", 5.5m,
                    "Throw combinations at the bag in timed rounds, keeping your guard up and your feet moving.")
            ];

            db.Exercises.AddRange(exercises);
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded {Count} exercises.", exercises.Length);
        }

        private static async Task SeedFoodItemsAsync(ApplicationDbContext db, ILogger logger, CancellationToken cancellationToken)
        {
            if (await db.FoodItems.AnyAsync(cancellationToken))
            {
                return;
            }

            // Typical values per serving (kcal, protein, carbs, fat in grams), from standard food composition tables.
            FoodItem[] foods =
            [
                // Grains
                NewFood("Rolled oats", FoodCategory.Grains, 40m, "g", 152m, 5.3m, 27.1m, 2.6m),
                NewFood("Wholemeal bread", FoodCategory.Grains, 1m, "slice", 78m, 3.4m, 15.1m, 0.9m),
                NewFood("White bread", FoodCategory.Grains, 1m, "slice", 77m, 2.6m, 14.3m, 1.0m),
                NewFood("Brown rice, cooked", FoodCategory.Grains, 150m, "g", 185m, 4.1m, 38.4m, 1.5m),
                NewFood("White rice, cooked", FoodCategory.Grains, 150m, "g", 195m, 4.1m, 42.3m, 0.4m),
                NewFood("Wholewheat pasta, cooked", FoodCategory.Grains, 150m, "g", 186m, 8.0m, 39.8m, 0.8m),
                NewFood("Quinoa, cooked", FoodCategory.Grains, 150m, "g", 180m, 6.6m, 32.0m, 2.9m),
                NewFood("Wholemeal tortilla wrap", FoodCategory.Grains, 1m, "piece", 180m, 5.5m, 30.0m, 4.5m),

                // Protein
                NewFood("Chicken breast, grilled", FoodCategory.Protein, 100m, "g", 165m, 31.0m, 0m, 3.6m),
                NewFood("Salmon fillet, baked", FoodCategory.Protein, 100m, "g", 206m, 22.1m, 0m, 12.4m),
                NewFood("Tuna in water, drained", FoodCategory.Protein, 100m, "g", 116m, 25.5m, 0m, 0.8m),
                NewFood("Egg, boiled", FoodCategory.Protein, 1m, "piece", 78m, 6.3m, 0.6m, 5.3m),
                NewFood("Lean beef mince, cooked", FoodCategory.Protein, 100m, "g", 170m, 26.0m, 0m, 7.0m),
                NewFood("Turkey breast, roasted", FoodCategory.Protein, 100m, "g", 147m, 30.1m, 0m, 2.1m),
                NewFood("Tofu, firm", FoodCategory.Protein, 100m, "g", 144m, 17.3m, 2.8m, 8.7m),
                NewFood("Lentils, cooked", FoodCategory.Protein, 150m, "g", 174m, 13.5m, 30.2m, 0.6m),
                NewFood("Chickpeas, cooked", FoodCategory.Protein, 100m, "g", 164m, 8.9m, 27.4m, 2.6m),
                NewFood("Whey protein powder", FoodCategory.Protein, 30m, "g", 120m, 24.0m, 3.0m, 1.5m),

                // Dairy
                NewFood("Semi-skimmed milk", FoodCategory.Dairy, 200m, "ml", 94m, 7.0m, 9.4m, 3.4m),
                NewFood("Skimmed milk", FoodCategory.Dairy, 200m, "ml", 68m, 6.8m, 10.0m, 0.2m),
                NewFood("Greek yoghurt, fat-free", FoodCategory.Dairy, 150m, "g", 89m, 15.3m, 5.4m, 0.6m),
                NewFood("Natural yoghurt, low-fat", FoodCategory.Dairy, 150m, "g", 95m, 8.0m, 10.5m, 2.4m),
                NewFood("Cheddar cheese", FoodCategory.Dairy, 30m, "g", 121m, 7.5m, 0.4m, 9.9m),
                NewFood("Cottage cheese, low-fat", FoodCategory.Dairy, 100m, "g", 84m, 11.0m, 4.3m, 2.3m),

                // Fruits
                NewFood("Banana", FoodCategory.Fruits, 1m, "piece", 105m, 1.3m, 27.0m, 0.4m),
                NewFood("Apple", FoodCategory.Fruits, 1m, "piece", 95m, 0.5m, 25.1m, 0.3m),
                NewFood("Orange", FoodCategory.Fruits, 1m, "piece", 62m, 1.2m, 15.4m, 0.2m),
                NewFood("Strawberries", FoodCategory.Fruits, 1m, "cup", 49m, 1.0m, 11.7m, 0.5m),
                NewFood("Blueberries", FoodCategory.Fruits, 1m, "cup", 84m, 1.1m, 21.4m, 0.5m),
                NewFood("Grapes", FoodCategory.Fruits, 100m, "g", 69m, 0.7m, 18.1m, 0.2m),

                // Vegetables
                NewFood("Broccoli, steamed", FoodCategory.Vegetables, 100m, "g", 35m, 2.4m, 7.2m, 0.4m),
                NewFood("Spinach, raw", FoodCategory.Vegetables, 1m, "cup", 7m, 0.9m, 1.1m, 0.1m),
                NewFood("Carrot", FoodCategory.Vegetables, 1m, "piece", 25m, 0.6m, 5.8m, 0.1m),
                NewFood("Sweet potato, baked", FoodCategory.Vegetables, 150m, "g", 135m, 3.0m, 31.1m, 0.2m),
                NewFood("Potato, boiled", FoodCategory.Vegetables, 150m, "g", 131m, 2.9m, 30.2m, 0.2m),
                NewFood("Tomato", FoodCategory.Vegetables, 1m, "piece", 22m, 1.1m, 4.8m, 0.2m),
                NewFood("Mixed salad leaves", FoodCategory.Vegetables, 100m, "g", 15m, 1.4m, 2.3m, 0.2m),
                NewFood("Garden peas, boiled", FoodCategory.Vegetables, 80m, "g", 67m, 4.3m, 12.5m, 0.2m),

                // Fats and oils
                NewFood("Olive oil", FoodCategory.FatsAndOils, 1m, "tbsp", 119m, 0m, 0m, 13.5m),
                NewFood("Butter", FoodCategory.FatsAndOils, 1m, "tbsp", 102m, 0.1m, 0m, 11.5m),
                NewFood("Peanut butter", FoodCategory.FatsAndOils, 1m, "tbsp", 94m, 4.0m, 3.2m, 8.1m),
                NewFood("Almonds", FoodCategory.FatsAndOils, 30m, "g", 174m, 6.4m, 6.5m, 15.0m),
                NewFood("Walnuts", FoodCategory.FatsAndOils, 30m, "g", 196m, 4.6m, 4.1m, 19.6m),
                NewFood("Avocado", FoodCategory.FatsAndOils, 100m, "g", 160m, 2.0m, 8.5m, 14.7m),

                // Beverages
                NewFood("Orange juice", FoodCategory.Beverages, 250m, "ml", 112m, 1.7m, 26.0m, 0.5m),
                NewFood("Black coffee", FoodCategory.Beverages, 250m, "ml", 2m, 0.3m, 0m, 0m),
                NewFood("Green tea", FoodCategory.Beverages, 250m, "ml", 2m, 0.5m, 0m, 0m),
                NewFood("Cola", FoodCategory.Beverages, 330m, "ml", 139m, 0m, 35.0m, 0m),
                NewFood("Sports drink", FoodCategory.Beverages, 500m, "ml", 130m, 0m, 32.0m, 0m),
                NewFood("Oat drink", FoodCategory.Beverages, 200m, "ml", 92m, 2.0m, 13.2m, 3.0m),

                // Snacks and sweets
                NewFood("Dark chocolate (70%)", FoodCategory.SnacksAndSweets, 25m, "g", 150m, 2.0m, 11.5m, 10.7m),
                NewFood("Milk chocolate", FoodCategory.SnacksAndSweets, 25m, "g", 134m, 1.9m, 14.9m, 7.4m),
                NewFood("Potato crisps", FoodCategory.SnacksAndSweets, 25m, "g", 134m, 1.8m, 13.3m, 8.7m),
                NewFood("Cereal bar", FoodCategory.SnacksAndSweets, 1m, "piece", 130m, 2.5m, 19.0m, 5.0m),
                NewFood("Digestive biscuit", FoodCategory.SnacksAndSweets, 1m, "piece", 71m, 1.0m, 9.6m, 3.2m),
                NewFood("Rice cake", FoodCategory.SnacksAndSweets, 1m, "piece", 35m, 0.7m, 7.3m, 0.3m),
                NewFood("Popcorn, air-popped", FoodCategory.SnacksAndSweets, 1m, "cup", 31m, 1.0m, 6.2m, 0.4m)
            ];

            db.FoodItems.AddRange(foods);
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded {Count} food items.", foods.Length);
        }

        private static Exercise NewExercise(string name, ExerciseCategory category, MuscleGroup muscleGroup,
            string? equipment, decimal metValue, string instructions) => new()
            {
                Name = name,
                Category = category,
                MuscleGroup = muscleGroup,
                Equipment = equipment,
                MetValue = metValue,
                Instructions = instructions
            };

        private static FoodItem NewFood(string name, FoodCategory category, decimal servingSize, string servingUnit,
            decimal calories, decimal proteinG, decimal carbsG, decimal fatG) => new()
            {
                Name = name,
                Category = category,
                ServingSize = servingSize,
                ServingUnit = servingUnit,
                CaloriesPerServing = calories,
                ProteinG = proteinG,
                CarbsG = carbsG,
                FatG = fatG
            };
    }
}
