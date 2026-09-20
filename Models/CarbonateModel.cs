using System;

namespace RBI_Malaysia.Models
{
    public class CarbonateModel
    {
        public decimal CO3ID { get; set; }
        public decimal? ProcID { get; set; }
        public decimal? EquID { get; set; }
        public decimal? CompID { get; set; }

        // ---------------- UI Inputs ----------------
        public int? CO3Age { get; set; }
        public string? CO3InsEff { get; set; }    // A / B / C / D / E
        public int? CO3nofIns { get; set; }
        public DateTime? InspectDate { get; set; }

        // Step 1 — pH + CO3 → Svi
        public string? PHwater { get; set; }      // label stored in pHwater column
        public string? PHwaterCode { get; set; }  // numeric code used in Ref_CO3 query (7.6, 8.3, 9.0)
        public string? CO3 { get; set; }          // CO3 label stored in CO3 column
        public string? CO3Code { get; set; }      // Hundredppm / Five100ppm / Thousandppm / GtThousandppm

        // Known crack override
        public bool KnownCrack { get; set; }

        // Svi — auto from lookup or overridden
        public string? CO3Svi { get; set; }       // "High" / "Medium" / "Low" / "None"
        public int? CO3SviVal { get; set; }       // 1000 / 100 / 10 / 1

        // ---------------- Result ----------------
        public decimal? CO3Df { get; set; }

        // ---------------- Audit Fields ----------------
        public decimal? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public decimal? UpdatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }
}
