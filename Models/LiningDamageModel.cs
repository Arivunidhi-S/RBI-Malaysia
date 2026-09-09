namespace RBI_Malaysia.Models
{
    public class LiningDamageModel
    {
        public decimal LnfID { get; set; }

        public decimal ProcID { get; set; }
        public decimal EquID { get; set; }
        public decimal CompID { get; set; }

        public string Lntype { get; set; } = string.Empty;

        public int SIyear { get; set; }

        public string Dfb { get; set; } = string.Empty;

        public string LnCond { get; set; } = string.Empty;
        public decimal LnCondVal { get; set; }

        public string OnMoni { get; set; } = string.Empty;
        public decimal OnMoniVal { get; set; }

        public decimal DfLine { get; set; }

        public decimal CompanyID { get; set; }

        public decimal CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }

        public decimal ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }

        public int Rowversions { get; set; }

        public bool Deleted { get; set; }
    }
}