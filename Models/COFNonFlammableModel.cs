using System;

namespace RBI_Malaysia.Models
{
    public class COFNonFlammableModel
    {
        public decimal NonFlameID { get; set; }
        public decimal? ProcID { get; set; }
        public decimal? EquID { get; set; }
        public decimal? CompID { get; set; }

        // ---------------- UI Inputs ----------------
        public string? Fluid { get; set; } // Fluid Stored (Liquid / Gas)
        public string? Repfluid { get; set; } // steam / water / Acid
        public string? Phase { get; set; } // Final Phase — not persisted in old system either
        public string? FluidType { get; set; } // TYPE 0, etc.
        public decimal? Pressure { get; set; }
        public decimal? Temperature { get; set; }
        public decimal? P1 { get; set; }
        public decimal? Inventory { get; set; } // Mass, lbs
        public string? Detection { get; set; }
        public string? Isolation { get; set; }
        public string? MitigationValue { get; set; }

        // ---------------- Final Output ----------------
        public decimal CAInjTotal { get; set; } // Personnel Injury Area (final)
        public string? CAInjCategory { get; set; } // Injury Category (A-E)

        // ---------------- Intermediate Calculation Values ----------------
        public double W1 { get; set; }
        public double W2 { get; set; }
        public double W3 { get; set; }
        public double W4 { get; set; }

        public string Time1 { get; set; } = "";
        public string Time2 { get; set; } = "";
        public string Time3 { get; set; } = "";
        public string Time4 { get; set; } = "";

        public double T1 { get; set; }
        public double T2 { get; set; }
        public double T3 { get; set; }
        public double T4 { get; set; }

        public double Rate1 { get; set; }
        public double Rate2 { get; set; }
        public double Rate3 { get; set; }
        public double Rate4 { get; set; }

        public string ID1 { get; set; } = "";
        public string ID2 { get; set; } = "";
        public string ID3 { get; set; } = "";
        public string ID4 { get; set; } = "";

        public double Mass1 { get; set; }
        public double Mass2 { get; set; }
        public double Mass3 { get; set; }
        public double Mass4 { get; set; }

        public double Efficiency { get; set; } // eneff

        // Continuous-release injury area per hole size ("steam" path: C9*rate; else path: 0.2*g*rate^h)
        public double CAInj1 { get; set; }
        public double CAInj2 { get; set; }
        public double CAInj3 { get; set; }
        public double CAInj4 { get; set; }

        // Instantaneous-release injury area per hole size ("steam" path only: C10*mass^0.6384)
        public double CAInsInj1 { get; set; }
        public double CAInsInj2 { get; set; }
        public double CAInsInj3 { get; set; }
        public double CAInsInj4 { get; set; }

        // Continuous/Instantaneous blending factor (TYPE 0 & mass >= 10,000 lbs, "steam" path only)
        public double Factic1 { get; set; }
        public double Factic2 { get; set; }
        public double Factic3 { get; set; }
        public double Factic4 { get; set; }

        // Selected/blended injury area per hole size (== CAInj or CAInsInj, or blend of both)
        public double CAbleInj1 { get; set; }
        public double CAbleInj2 { get; set; }
        public double CAbleInj3 { get; set; }
        public double CAbleInj4 { get; set; }

        // GFF-weighted final injury area per hole size
        public double CAInjFinal1 { get; set; }
        public double CAInjFinal2 { get; set; }
        public double CAInjFinal3 { get; set; }
        public double CAInjFinal4 { get; set; }

        public double Ptrans { get; set; } // sonic/subsonic transition pressure (gas only)

        // g/h coefficients — used only in the non-"steam" path (pressure-based correlation)
        public double G { get; set; }
        public double H { get; set; }

        // ---------------- Audit Fields ----------------
        public string? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }
}
