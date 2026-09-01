using System;

namespace RBI_Malaysia.Models
{
    public class COFFlammableModel
    {
        public decimal FlameID { get; set; }
        public decimal? ProcID { get; set; }
        public decimal? EquID { get; set; }
        public decimal? CompID { get; set; }

        // ---------------- UI Inputs ----------------
        public string? Fluid { get; set; } // Fluid Stored (Liquid / Gas)
        public string? Repfluid { get; set; }
        public string? Phase { get; set; } // Final Phase
        public string? FluidType { get; set; } // TYPE 0, TYPE 1, etc. (from Ref_COF_Rep_Fluids)
        public decimal? Pressure { get; set; } // Operating Pressure
        public decimal? Temperature { get; set; } // Operating Temp
        public decimal? P1 { get; set; }
        public decimal? Inventory { get; set; } // Mass, lbs
        public string? Detection { get; set; } // A, B, C
        public string? Isolation { get; set; } // A, B, C
        public string? MitigationValue { get; set; } // factMit value

        // ---------------- Final Outputs (shown on screen) ----------------
        public decimal CAcmdTotal { get; set; } // Component Damage Area (final)
        public decimal CAInjTotal { get; set; } // Personnel Injury Area (final)
        public string? CAcmdCategory { get; set; } // Damage Category (A-E)
        public string? CAInjCategory { get; set; } // Injury Category (A-E)

        // ---------------- Intermediate Calculation Values (merged from COFFlammableResult) ----------------
        // Release rate per hole size (0.25", 1", 4", 16")
        public double W1 { get; set; }
        public double W2 { get; set; }
        public double W3 { get; set; }
        public double W4 { get; set; }

        // Continuous / Instantaneous classification per hole size
        public string Time1 { get; set; } = "";
        public string Time2 { get; set; } = "";
        public string Time3 { get; set; } = "";
        public string Time4 { get; set; } = "";

        // Raw time-to-empty per hole size
        public double T1 { get; set; }
        public double T2 { get; set; }
        public double T3 { get; set; }
        public double T4 { get; set; }

        // Rate after detection/isolation reduction
        public double Rate1 { get; set; }
        public double Rate2 { get; set; }
        public double Rate3 { get; set; }
        public double Rate4 { get; set; }

        // Leak duration used (minutes, or "Instantaneous")
        public string ID1 { get; set; } = "";
        public string ID2 { get; set; } = "";
        public string ID3 { get; set; } = "";
        public string ID4 { get; set; } = "";

        // Mass released per hole size
        public double Mass1 { get; set; }
        public double Mass2 { get; set; }
        public double Mass3 { get; set; }
        public double Mass4 { get; set; }

        public double Efficiency { get; set; } // eneffn

        // Continuous-release damage coefficients (Ref_COF_Damage)
        public double CAA { get; set; }
        public double CAB { get; set; }

        // Continuous-release component damage area per hole size
        public double CAc1 { get; set; }
        public double CAc2 { get; set; }
        public double CAc3 { get; set; }
        public double CAc4 { get; set; }

        // Instantaneous-release damage coefficients (Ref_COF_Damage)
        public double CAInsA { get; set; }
        public double CAInsB { get; set; }

        // Instantaneous-release component damage area per hole size
        public double CAInst1 { get; set; }
        public double CAInst2 { get; set; }
        public double CAInst3 { get; set; }
        public double CAInst4 { get; set; }

        // Continuous-release injury coefficients (Ref_COF_Injury)
        public double AInj { get; set; }
        public double BInj { get; set; }

        // Continuous-release personnel injury area per hole size
        public double CAInj1 { get; set; }
        public double CAInj2 { get; set; }
        public double CAInj3 { get; set; }
        public double CAInj4 { get; set; }

        // Instantaneous-release injury coefficients (Ref_COF_Injury)
        public double AInsInj { get; set; }
        public double BInsInj { get; set; }

        // Instantaneous-release personnel injury area per hole size
        public double CAInsInj1 { get; set; }
        public double CAInsInj2 { get; set; }
        public double CAInsInj3 { get; set; }
        public double CAInsInj4 { get; set; }

        // Continuous/Instantaneous blending factor (TYPE 0 fluids, mass >= 10,000 lbs)
        public double Factic1 { get; set; }
        public double Factic2 { get; set; }
        public double Factic3 { get; set; }
        public double Factic4 { get; set; }

        // Blended component damage area per hole size
        public double CAcmd1 { get; set; }
        public double CAcmd2 { get; set; }
        public double CAcmd3 { get; set; }
        public double CAcmd4 { get; set; }

        // Blended personnel injury area per hole size
        public double CAbleInj1 { get; set; }
        public double CAbleInj2 { get; set; }
        public double CAbleInj3 { get; set; }
        public double CAbleInj4 { get; set; }

        // GFF-weighted final component damage area per hole size
        public double CAcmdFinal1 { get; set; }
        public double CAcmdFinal2 { get; set; }
        public double CAcmdFinal3 { get; set; }
        public double CAcmdFinal4 { get; set; }

        // GFF-weighted final personnel injury area per hole size
        public double CAInjFinal1 { get; set; }
        public double CAInjFinal2 { get; set; }
        public double CAInjFinal3 { get; set; }
        public double CAInjFinal4 { get; set; }

        public double MaxValue { get; set; } // max(CAcmdTotal, CAInjTotal)
        public string MaxCategory { get; set; } = ""; // category of whichever is max

        public double Ptrans { get; set; } // sonic/subsonic transition pressure (gas only)

        // ---------------- Audit Fields ----------------
        public string? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }
}
