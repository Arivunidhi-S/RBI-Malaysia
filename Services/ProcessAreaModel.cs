using System.ComponentModel.DataAnnotations;

namespace RBI_Malaysia.Services
{
    public class ProcessAreaModel
    {
        public decimal ProcessAreaID { get; set; }


        [Required(ErrorMessage = "Process Area is required.")]
        [StringLength(
            50,
            ErrorMessage = "Process Area cannot exceed 50 characters.")]
        public string ProcessArea { get; set; } = string.Empty;


        [Required(ErrorMessage = "Description is required.")]
        [StringLength(
            100,
            ErrorMessage = "Description cannot exceed 100 characters.")]
        public string Description { get; set; } = string.Empty;


        [StringLength(
            50,
            ErrorMessage = "Process Unit cannot exceed 50 characters.")]
        public string ProcessUnit { get; set; } = string.Empty;
    }
}