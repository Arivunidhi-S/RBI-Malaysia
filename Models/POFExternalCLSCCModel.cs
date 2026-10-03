using System.ComponentModel.DataAnnotations;

namespace RBI_Malaysia.Models
{
    public class POFExternalCLSCCModel
    {
        public decimal ExCLSID { get; set; }

        public decimal ProcID { get; set; }

        public decimal EquID { get; set; }

        public decimal CompID { get; set; }

        public decimal Agtk { get; set; }

        [Required(ErrorMessage = "Inspection Effectiveness is required.")]
        public string InsEff { get; set; } = string.Empty;

        [Required(ErrorMessage = "Component / Coating Installation Date is required.")]
        public DateTime? CmpDt { get; set; }

        [Required(ErrorMessage = "Calculation Date is required.")]
        public DateTime? CalDt { get; set; }

        [Required(ErrorMessage = "No. of Inspection is required.")]
        public int? NofIns { get; set; }

        [Required(ErrorMessage = "Inspection Date is required.")]
        public DateTime? InspectDate { get; set; }

        [Required(ErrorMessage = "Coating Quality is required.")]
        public string CoatQual { get; set; } = string.Empty;

        public decimal Age { get; set; }

        [Required(ErrorMessage = "Area is required.")]
        public string CrDriver { get; set; } = string.Empty;

        public string Svi { get; set; } = string.Empty;

        public decimal ExCLSDf { get; set; }

        public decimal CompanyID { get; set; }

        public decimal CreatedBy { get; set; }

        public DateTime? CreatedDate { get; set; }

        public decimal ModifiedBy { get; set; }

        public DateTime? ModifiedDate { get; set; }

        public int Rowversions { get; set; }

        public bool Deleted { get; set; }
    }
}

