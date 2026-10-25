using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models.Enums
{
    /// <summary>
    /// The food group a food library item belongs to.
    /// </summary>
    public enum FoodCategory
    {
        /// <summary>Bread, rice, pasta, oats and other grains.</summary>
        [Display(Name = "Grains")]
        Grains = 1,

        /// <summary>Meat, fish, eggs, pulses and other protein foods.</summary>
        [Display(Name = "Protein")]
        Protein = 2,

        /// <summary>Milk, yoghurt and cheese.</summary>
        [Display(Name = "Dairy")]
        Dairy = 3,

        /// <summary>Fruit.</summary>
        [Display(Name = "Fruits")]
        Fruits = 4,

        /// <summary>Vegetables.</summary>
        [Display(Name = "Vegetables")]
        Vegetables = 5,

        /// <summary>Oils, butter, nuts and other fat-rich foods.</summary>
        [Display(Name = "Fats and oils")]
        FatsAndOils = 6,

        /// <summary>Drinks.</summary>
        [Display(Name = "Beverages")]
        Beverages = 7,

        /// <summary>Snacks, sweets and treats.</summary>
        [Display(Name = "Snacks and sweets")]
        SnacksAndSweets = 8
    }
}
