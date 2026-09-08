using System;
using System.Collections.Generic;

namespace RBI_Malaysia.Models
{
    /// <summary>
    /// Maps 1:1 to dbo.ThinningDamage, plus a few transient (not persisted)
    /// properties used to drive the Art/Tdf calculation and the UI.
    /// Mirrors the merged-model pattern used for COFFlammableModel/Result.
    /// </summary>
    public class ThinningDamageModel
    {
        // ---------- Persisted columns (dbo.ThinningDamage) ----------
        public long? TDFId { get; set; }

        public int? ProcID { get; set; }
        public int? EquID { get; set; }
        public int? CompID { get; set; }

        /// <summary>
        /// Text of the linked "clad reference" component (cboclad.SelectedItem.Text
        /// in the original page) - only meaningful when the selected component's
        /// own Clad flag = "Yes". NOT the same as ComponentClad below.
        /// </summary>
        public string? Clad { get; set; }

        public int? Age { get; set; }
        public decimal? Art { get; set; }
        public decimal? Tdf { get; set; }

        /// <summary>Inspection effectiveness category: A / B / C / D / E</summary>
        public string? InspectCate { get; set; }

        public int? NoOfIns { get; set; }
        public DateTime? InspectDate { get; set; }

        /// <summary>"General" or "Local" (cbo_thin_type.SelectedItem.Text)</summary>
        public string? ThinType { get; set; }

        public int? CompanyID { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public int? Rowversions { get; set; }
        public bool? Deleted { get; set; }

        public decimal? YS { get; set; }
        public decimal? TS { get; set; }
        public decimal? S { get; set; }
        public decimal? E { get; set; }

        public int? NoFinsB { get; set; }
        public int? NoFinsC { get; set; }
        public int? NoFinsD { get; set; }

        public decimal? Prp1Thin { get; set; }
        public decimal? Prp2Thin { get; set; }
        public decimal? Prp3Thin { get; set; }

        public decimal? DS1 { get; set; }
        public decimal? DS2 { get; set; }
        public decimal? DS3 { get; set; }

        // ---------- Transient (UI / calc support only, never saved) ----------

        /// <summary>Component's own Clad flag from Tbl_EquipmentComponentDetails ("Clad"/"Yes"/"No").</summary>
        public string? ComponentClad { get; set; }

        /// <summary>True when ComponentClad indicates a clad component (drives cboclad.Enabled).</summary>
        public bool CladDropdownEnabled => ComponentClad == "Clad" || ComponentClad == "Yes";

        /// <summary>Populated only when CladDropdownEnabled - list of components flagged Clad='Clad'.</summary>
        public List<CladOption> CladOptions { get; set; } = new();

        /// <summary>"Save" or "Update" - mirrors btnThinningSubmit.ToolTip in the original page.</summary>
        public bool IsExistingRecord => TDFId.HasValue && TDFId.Value > 0;

        public string DamageFactorCode => (ThinType == "General") ? "Thinning" : "ThinningL";
    }

    public class CladOption
    {
        public int CompAutoID { get; set; }
        public string CompName { get; set; } = string.Empty;
    }

    /// <summary>Raw component data pulled from Tbl_EquipmentComponentDetails, needed for the Art formula.</summary>
    public class ComponentThinningInfo
    {
        public double ReadVal { get; set; }             // trd
        public double MRT { get; set; }                 // tmin
        public double CorrosionAllowance { get; set; }   // CA
        public double UCR { get; set; }                  // crcm
        public string Clad { get; set; } = string.Empty; // component's own Clad flag
    }
}
