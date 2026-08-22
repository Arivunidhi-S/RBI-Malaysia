using System.ComponentModel.DataAnnotations;

namespace RBI_Malaysia.Services;

public class ComponentModel
{
    public decimal CompAutoID { get; set; }

    [Range(1, double.MaxValue, ErrorMessage = "Please select Process Area.")]
    public decimal ProcessAreaID { get; set; }

    public string ProcessArea { get; set; } = "";

    [Range(1, double.MaxValue, ErrorMessage = "Please select Equipment.")]
    public decimal EqupID { get; set; }

    public string EquipmentID { get; set; } = "";

    [Required(ErrorMessage = "Component No is required.")]
    public string CompNo { get; set; } = "";

    [Required(ErrorMessage = "Component Name is required.")]
    public string CompName { get; set; } = "";

    public string Insulated { get; set; } = "No";

    public string Painting { get; set; } = "No";

    [Required(ErrorMessage = "Please select Material Type.")]
    public string Materialtype { get; set; } = "None";

    public string MaterialSpecification { get; set; } = "";

    [Required(ErrorMessage = "Normal Thickness is required.")]
    public string NormalThickness { get; set; } = "";

    public string ConstThickness { get; set; } = "";

    [Required(ErrorMessage = "MRT is required.")]
    public decimal MRT { get; set; }

    public string Designpressure { get; set; } = "";
    public string DesignTemp { get; set; } = "";
    public string OPPressure { get; set; } = "";
    public string OPTemp { get; set; } = "";

    [Required(ErrorMessage = "Corrosion Allowance is required.")]
    public string CorrosionAllownce { get; set; } = "";

    [Required(ErrorMessage = "Please select Inspection Effective.")]
    public string InspectionEffective { get; set; } = "None";

    public decimal ExpectedRate { get; set; }

    public int NoofInspection { get; set; } = 0;

    [Required(ErrorMessage = "Please select Clad.")]
    public string Clad { get; set; } = "None";

    [Required(ErrorMessage = "Default Value is required.")]
    public decimal Defaultvalue { get; set; }

    public decimal CompanyID { get; set; }
}