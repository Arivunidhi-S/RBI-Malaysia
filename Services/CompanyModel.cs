using System.ComponentModel.DataAnnotations;

namespace Services
{
    public class CompanyModel
    {
        public int CompanyID { get; set; }

        [Required(ErrorMessage = "Company name is required.")]
        public string CompanyName { get; set; } = "";

        public string Address1 { get; set; } = "";
        public string Address2 { get; set; } = "";
        public string City { get; set; } = "";
        public string State { get; set; } = "";
        [Required(ErrorMessage = "Country is required.")]
        public string Country { get; set; } = "";
        [RegularExpression(@"^[0-9]{5,10}$",
        ErrorMessage = "Enter a valid postcode.")]
        public string Postcode { get; set; } = "";
        public string Description { get; set; } = "";
        [Required(ErrorMessage = "Contact number is required.")]
        [RegularExpression(@"^[0-9]{5,10}$",
        ErrorMessage = "Enter a valid Contact number.")]
        public string Phone { get; set; } = "";
        public string Fax { get; set; } = "";
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string Email { get; set; } = "";
        public string Website { get; set; } = "";
    }
}