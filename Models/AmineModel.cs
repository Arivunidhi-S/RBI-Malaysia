using System;

namespace RBI_Malaysia.Models
{
    public class AmineModel
    {
        public decimal AmnID { get; set; }
        public decimal? ProcID { get; set; }
        public decimal? EquID { get; set; }
        public decimal? CompID { get; set; }

        // ---------------- UI Inputs ----------------
        public int? AmAge { get; set; }
        public string? AmInsEff { get; set; }     // A / B / C / D / E
        public int? AmnofIns { get; set; }
        public DateTime? InspectDate { get; set; }

        // Susceptibility — same Svi options as Caustic (High=5000, Medium=500, Low=50, None=1)
        public string? AmSvi { get; set; }        // "High" / "Medium" / "Low" / "None"
        public int? AmSviVal { get; set; }        // 5000 / 500 / 50 / 1

        // ---------------- Result ----------------
        public decimal? AmDf { get; set; }        // Df = Ref_SCC lookup × Age^1.1

        // ---------------- Audit Fields ----------------
        public decimal? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public decimal? UpdatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }
}
