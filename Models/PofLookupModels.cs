namespace RBI_Malaysia.Models
{
    public class ProcessAreaOption
    {
        public int ProcessAreaID { get; set; }
        public string ProcessArea { get; set; } = string.Empty;
    }

    public class EquipmentOption
    {
        public int EquAutoID { get; set; }
        public string EqupID { get; set; } = string.Empty;
        public string EqupType { get; set; } = string.Empty;
    }

    /// <summary>
    /// Mirrors the RadComboBoxItem.Attributes cache built in
    /// OnSelectedIndexChanged_cboEquipment - carries everything the Thinning
    /// (and other POF) tabs need about the selected component without a re-query.
    /// </summary>
    public class ComponentOption
    {
        public int CompAutoID { get; set; }
        public string CompNo { get; set; } = string.Empty;
        public string CompName { get; set; } = string.Empty;
        public string OPTemp { get; set; } = string.Empty;
        public string Clad { get; set; } = string.Empty;
        public string InspectionEffective { get; set; } = string.Empty;
        public string NoofInspection { get; set; } = string.Empty;
        public string MRT { get; set; } = string.Empty;
        public string CorrosionAllownce { get; set; } = string.Empty;
    }
}
