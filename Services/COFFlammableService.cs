using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using RBI_Malaysia.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace RBI_Malaysia.Services
{
    public class COFFlammableService
    {
        private readonly string _connectionString;

        public COFFlammableService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("connString")
                ?? throw new InvalidOperationException("Connection string 'connString' not found.");
        }

        // =================================================================
        // 1. DROPDOWN DATA FETCH METHODS
        // =================================================================

        public async Task<List<ProcessAreaModel>> GetFlamProcessAreasAsync(string companyid)
        {
            var list = new List<ProcessAreaModel>();
            string query = "SELECT [ProcessAreaID], [processarea] FROM [Tbl_ProcessArea] WHERE deleted = 0 and CompanyID=@companyid ORDER BY [processareaid]";
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.Add("@companyid", SqlDbType.Decimal).Value = companyid;
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
            while (reader.HasRows && await reader.ReadAsync())
            {
                list.Add(new ProcessAreaModel
                {
                    ProcessAreaID = reader.GetDecimal(0),
                    ProcessArea = reader.IsDBNull(1) ? "" : reader.GetString(1)
                });
            }
            return list;
        }

        public async Task<List<EquipmentModel>> GetflamEquipmentsByProcessAsync(decimal processAreaId)
        {
            var list = new List<EquipmentModel>();
            string query = "SELECT EquAutoID, EqupID, EqupType FROM Tbl_EquipmentAsset WHERE ProcessAreaID = @ProcID AND deleted = 0";
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.Add("@ProcID", SqlDbType.Decimal).Value = processAreaId;
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
            while (reader.HasRows && await reader.ReadAsync())
            {
                list.Add(new EquipmentModel
                {
                    EquAutoID = reader.GetDecimal(0),
                    EquPID = $"{reader.GetString(1)} - {reader.GetString(2)}"
                });
            }
            return list;
        }

        public async Task<List<ComponentModel>> GetFlamComponentsByEquipmentAsync(string equipmentId)
        {
            var list = new List<ComponentModel>();
            string query = "SELECT compautoid, CompNo, compname FROM Tbl_EquipmentComponentDetails WHERE EqupID = @EquID AND deleted = 0";
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.Add("@EquID", SqlDbType.NVarChar, 20).Value = equipmentId ?? "";
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection);
            while (reader.HasRows && await reader.ReadAsync())
            {
                list.Add(new ComponentModel
                {
                    CompAutoID = reader.GetDecimal(0),
                    CompNo = $"{reader.GetString(1)} - {reader.GetString(2)}"
                });
            }
            return list;
        }

        public async Task<List<string>> GetFluidsAsync(string fluidStoredType)
        {
            var fluids = new List<string>();
            string query = "SELECT Fluid FROM Ref_COF_Lvl_1";
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(query, conn);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) fluids.Add(reader.GetString(0));
            return fluids;
        }

        public async Task<string> GetFluidTypeAsync(string repFluid)
        {
            string query = "SELECT Fluid_Type FROM Ref_COF_Rep_Fluids WHERE Rep_Fluid = @RepFluid";
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@RepFluid", repFluid ?? "");
            await conn.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            return result?.ToString()?.Trim() ?? "";
        }

        // =================================================================
        // 2. EXISTING DATA RETRIEVAL (AUTO-LOAD)
        // =================================================================

        public async Task<COFFlammableModel?> GetFlammableRecordAsync(decimal procId, decimal equId, decimal compId)
        {
            string query = "SELECT * FROM COF_Flammable WHERE ProcID = @ProcID AND EquID = @EquID AND CompID = @CompID AND Deleted = 0";
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@ProcID", procId);
            cmd.Parameters.AddWithValue("@EquID", equId);
            cmd.Parameters.AddWithValue("@CompID", compId);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            double D(string col) => double.TryParse(reader[col]?.ToString(), out var v) ? v : 0;
            decimal Dec(string col) => decimal.TryParse(reader[col]?.ToString(), out var v) ? v : 0;
            string S(string col) => reader[col]?.ToString() ?? "";

            return new COFFlammableModel
            {
                FlameID = reader.GetDecimal(reader.GetOrdinal("FlameID")),
                ProcID = procId,
                EquID = equId,
                CompID = compId,
                Fluid = S("Fluid"),
                Repfluid = S("Repfluid"),
                FluidType = S("Type"),
                Pressure = Dec("OpPres"),
                Temperature = Dec("Optemp"),
                P1 = Dec("P1"),
                Inventory = Dec("Mass"),
                Detection = S("Detect"),
                Isolation = S("Iso"),
                MitigationValue = S("factMit"),
                Phase = "", // Final Phase (A/B/C) has no column in COF_Flammable in the old system either — must be re-selected each time

                W1 = D("w1"), W2 = D("w2"), W3 = D("w3"), W4 = D("w4"),
                Time1 = S("Time1"), Time2 = S("Time2"), Time3 = S("Time3"), Time4 = S("Time4"),
                T1 = D("T1"), T2 = D("T2"), T3 = D("T3"), T4 = D("T4"),
                Rate1 = D("rate1"), Rate2 = D("rate2"), Rate3 = D("rate3"), Rate4 = D("rate4"),
                ID1 = S("id1"), ID2 = S("id2"), ID3 = S("id3"), ID4 = S("id4"),
                Mass1 = D("mass1"), Mass2 = D("mass2"), Mass3 = D("mass3"), Mass4 = D("mass4"),
                Efficiency = D("eneff"),
                CAA = D("CaA"), CAB = D("CaB"),
                CAc1 = D("CAc1"), CAc2 = D("CAc2"), CAc3 = D("CAc3"), CAc4 = D("CAc4"),
                CAInsA = D("CaInsA"), CAInsB = D("CaInsB"),
                CAInst1 = D("CAIns1"), CAInst2 = D("CAIns2"), CAInst3 = D("CAIns3"), CAInst4 = D("CAIns4"),
                AInj = D("Ainj"), BInj = D("Binj"),
                CAInj1 = D("CAInj1"), CAInj2 = D("CAInj2"), CAInj3 = D("CAInj3"), CAInj4 = D("CAInj4"),
                AInsInj = D("AInsInj"), BInsInj = D("BInsInj"),
                CAInsInj1 = D("CAInsInj1"), CAInsInj2 = D("CAInsInj2"), CAInsInj3 = D("CAInsInj3"), CAInsInj4 = D("CAInsInj4"),
                Factic1 = D("factic1"), Factic2 = D("factic2"), Factic3 = D("factic3"), Factic4 = D("factic4"),
                CAcmd1 = D("CAcmd1"), CAcmd2 = D("CAcmd2"), CAcmd3 = D("CAcmd3"), CAcmd4 = D("CAcmd4"),
                CAbleInj1 = D("CAbleInj1"), CAbleInj2 = D("CAbleInj2"), CAbleInj3 = D("CAbleInj3"), CAbleInj4 = D("CAbleInj4"),
                CAcmdFinal1 = D("CAcmdFinal1"), CAcmdFinal2 = D("CAcmdFinal2"), CAcmdFinal3 = D("CAcmdFinal3"), CAcmdFinal4 = D("CAcmdFinal4"),
                CAInjFinal1 = D("CAInjFinal1"), CAInjFinal2 = D("CAInjFinal2"), CAInjFinal3 = D("CAInjFinal3"), CAInjFinal4 = D("CAInjFinal4"),
                CAcmdTotal = Dec("CAcmdTotal"),
                CAInjTotal = Dec("CAInjTotal"),
                CAcmdCategory = S("CAcmdCate"),
                CAInjCategory = S("CAinjCate"),
                MaxValue = D("maxval"),
                MaxCategory = S("maxcate"),
                Ptrans = D("ptrans"),
            };
        }

        // =================================================================
        // 3. CALCULATE + SAVE (single button — matches old ASP.Net behaviour
        //    where btn_COF_Calc_Click computed AND persisted in one click)
        // =================================================================

        public async Task<COFFlammableModel> CalculateAndSaveFlammableAsync(COFFlammableModel m)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // ---------- 0. Auto-fill FluidType if not already set ----------
            if (string.IsNullOrWhiteSpace(m.FluidType) && !string.IsNullOrWhiteSpace(m.Repfluid))
            {
                using var ftCmd = new SqlCommand("SELECT Fluid_Type FROM Ref_COF_Rep_Fluids WHERE Rep_Fluid = @RepFluid", conn);
                ftCmd.Parameters.AddWithValue("@RepFluid", m.Repfluid);
                var ftResult = await ftCmd.ExecuteScalarAsync();
                m.FluidType = ftResult?.ToString()?.Trim() ?? "";
            }

            // ---------- 1. Reference fluid properties (Ref_COF_Lvl_1) ----------
            // NOTE: MW, A, B, C, D, E are nvarchar columns in the DB and hold "N/A" for
            // fluids/rows where they don't apply (e.g. most Liquid entries). We must only
            // parse them when actually needed (Gas branch), never unconditionally.
            string refQuery = "SELECT MW, LqdDensity, NBF, Ambient, IdealGas, A, B, C, D, E, IgnTemp FROM Ref_COF_Lvl_1 WHERE Fluid = @Fluid";
            using var refCmd = new SqlCommand(refQuery, conn);
            refCmd.Parameters.AddWithValue("@Fluid", m.Repfluid ?? "");
            string mwRaw = "0", idealGas = "", aStr = "0", bStr = "0", cStr = "0", dStr = "0", eStr = "0";
            using (var reader = await refCmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    mwRaw = reader["MW"]?.ToString()?.Trim() ?? "0";
                    idealGas = reader["IdealGas"]?.ToString()?.Trim() ?? "";
                    aStr = reader["A"]?.ToString()?.Trim() ?? "0";
                    bStr = reader["B"]?.ToString()?.Trim() ?? "0";
                    cStr = reader["C"]?.ToString()?.Trim() ?? "0";
                    dStr = reader["D"]?.ToString()?.Trim() ?? "0";
                    eStr = reader["E"]?.ToString()?.Trim() ?? "0";
                }
            }

            double opTempKelvin = Convert.ToDouble(m.Temperature ?? 0) + 273;
            double opPressure = Convert.ToDouble(m.Pressure ?? 0);
            double massInput = Convert.ToDouble(m.Inventory ?? 0);
            const double A1 = 0.049, A2 = 0.785, A3 = 12.56, A4 = 200.96;
            double w1 = 0, w2 = 0, w3 = 0, w4 = 0, ptrans = 0;
            string fluidStoredType = m.Fluid ?? "";

            // ---------- 2. Release rate w1..w4 ----------
            if (fluidStoredType == "Liquid")
            {
                double cd = 0.61;
                double p1Val = Convert.ToDouble(m.P1 ?? 0);
                if (p1Val > 0)
                {
                    double diff = 2 * 32.2 * (opPressure - 14.7) / p1Val;
                    w1 = cd * (A1 / 12.0) * p1Val * Math.Sqrt(diff);
                    w2 = cd * (A2 / 12.0) * p1Val * Math.Sqrt(diff);
                    w3 = cd * (A3 / 12.0) * p1Val * Math.Sqrt(diff);
                    w4 = cd * (A4 / 12.0) * p1Val * Math.Sqrt(diff);
                }
            }
            else // Gas
            {
                double T = opTempKelvin, cp = 0;
                double SafeD(string s) => double.TryParse(s, out var v) ? v : 0; // guards against "N/A"/blank text

                if (idealGas == "Note 1")
                {
                    cp = SafeD(aStr) + (SafeD(bStr) * T) + (SafeD(cStr) * Math.Pow(T, 2)) + (SafeD(dStr) * Math.Pow(T, 3));
                }
                else if (idealGas == "Note 2")
                {
                    double cVal = SafeD(cStr), eVal = SafeD(eStr);
                    double ct = (cVal / T) / Math.Sinh(cVal / T);
                    double et = (eVal / T) / Math.Cosh(eVal / T);
                    cp = SafeD(aStr) + (SafeD(bStr) * Math.Pow(ct, 2)) + (SafeD(dStr) * Math.Pow(et, 2));
                }
                else if (idealGas == "Note 3")
                {
                    cp = SafeD(aStr) + (SafeD(bStr) * T) + (SafeD(cStr) * Math.Pow(T, 2)) + (SafeD(dStr) * Math.Pow(T, 3)) + (SafeD(eStr) * Math.Pow(T, 4));
                }

                double mw = SafeD(mwRaw);
                double R = 8.314;
                double kFactor = cp / (cp - R);
                double cd2 = 0.90;
                double tsRankine = (Convert.ToDouble(m.Temperature ?? 0) * 1.8) + 491.67;
                const double R1 = 1545, patm = 14.7;
                ptrans = patm * Math.Pow((kFactor + 1) / 2, kFactor / (kFactor - 1));

                if (opPressure > ptrans) // Sonic
                {
                    double kmw = (kFactor * mw * 32.2) / (R1 * tsRankine);
                    double power = (kFactor + 1) / (kFactor - 1);
                    double kplus = Math.Pow(2 / (kFactor + 1), power);
                    w1 = cd2 * A1 * opPressure * Math.Sqrt(kmw * kplus);
                    w2 = cd2 * A2 * opPressure * Math.Sqrt(kmw * kplus);
                    w3 = cd2 * A3 * opPressure * Math.Sqrt(kmw * kplus);
                    w4 = cd2 * A4 * opPressure * Math.Sqrt(kmw * kplus);
                }
                else // SubSonic
                {
                    double gmw = (mw * 32.2) / (R1 * tsRankine);
                    double k2 = (2 * kFactor) / (kFactor - 1);
                    double patmps = Math.Pow(patm / opPressure, 2 / kFactor);
                    double patmps1 = 1 - Math.Pow(patm / opPressure, (kFactor - 1) / kFactor);
                    w1 = cd2 * A1 * opPressure * Math.Sqrt(gmw * k2 * patmps * patmps1);
                    w2 = cd2 * A2 * opPressure * Math.Sqrt(gmw * k2 * patmps * patmps1);
                    w3 = cd2 * A3 * opPressure * Math.Sqrt(gmw * k2 * patmps * patmps1);
                    w4 = cd2 * A4 * opPressure * Math.Sqrt(gmw * k2 * patmps * patmps1);
                }
            }

            // ---------- 3. Duration classification ----------
            double c3 = massInput >= 10000.0 ? 10000 : massInput;
            double t1 = SafeDiv(c3, w1), t2 = SafeDiv(c3, w2), t3 = SafeDiv(c3, w3), t4 = SafeDiv(c3, w4);
            string Timet1 = "Continuous"; // forced, matches old code
            string Timet2 = t2 <= 180 ? "Instantaneous" : "Continuous";
            string Timet3 = t3 <= 180 ? "Instantaneous" : "Continuous";
            string Timet4 = t4 <= 180 ? "Instantaneous" : "Continuous";

            // ---------- 4. Detection/Isolation reduction + leak-duration caps ----------
            double factdi = 0;
            using (var detIsoCmd = new SqlCommand("SELECT Reduction FROM Ref_COF_Adj_Det_Iso WHERE Detection=@Det AND Isolat=@Iso", conn))
            {
                detIsoCmd.Parameters.AddWithValue("@Det", m.Detection ?? "");
                detIsoCmd.Parameters.AddWithValue("@Iso", m.Isolation ?? "");
                var r = await detIsoCmd.ExecuteScalarAsync();
                if (r != null) factdi = Convert.ToDouble(r);
            }

            double ld1 = 0, ld2 = 0, ld3 = 0;
            using (var ldCmd = new SqlCommand("SELECT Inch25, Inch1, Inch4 FROM Ref_COF_Leak_Det_Iso WHERE Detection=@Det AND Isolat=@Iso", conn))
            {
                ldCmd.Parameters.AddWithValue("@Det", m.Detection ?? "");
                ldCmd.Parameters.AddWithValue("@Iso", m.Isolation ?? "");
                using var r = await ldCmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    ld1 = Convert.ToDouble(r["Inch25"]);
                    ld2 = Convert.ToDouble(r["Inch1"]);
                    ld3 = Convert.ToDouble(r["Inch4"]);
                }
            }

            double rate1 = w1 * (1 - factdi), rate2 = w2 * (1 - factdi), rate3 = w3 * (1 - factdi), rate4 = w4 * (1 - factdi);

            double usld1 = Math.Min(SafeDiv(massInput, rate1) / 60, ld1);
            double usld2 = Math.Min(SafeDiv(massInput, rate2) / 60, ld2);
            double usld3 = Math.Min(SafeDiv(massInput, rate3) / 60, ld3);
            double usld4 = SafeDiv(massInput, rate4) / 60;

            double mass1 = Timet1 == "Continuous" ? rate1 * usld1 * 60 : massInput;
            double mass2 = Timet2 == "Continuous" ? rate2 * usld2 * 60 : massInput;
            double mass3 = Timet3 == "Continuous" ? rate3 * usld3 * 60 : massInput;
            double mass4 = Timet4 == "Continuous" ? rate4 * usld4 * 60 : massInput;

            string useld1 = Timet1 == "Continuous" ? usld1.ToString("#.##") : "Instantaneous";
            string useld2 = Timet2 == "Continuous" ? usld2.ToString("#.##") : "Instantaneous";
            string useld3 = Timet3 == "Continuous" ? usld3.ToString("#.##") : "Instantaneous";
            string useld4 = Timet4 == "Continuous" ? usld4.ToString("#.##") : "Instantaneous";

            // ---------- 5. Mitigation & efficiency correction ----------
            double factmit = string.IsNullOrEmpty(m.MitigationValue) ? 0 : Convert.ToDouble(m.MitigationValue);
            double eneffn1 = (massInput > 10000 && Timet1 == "Instantaneous") ? 4 * Math.Log10(massInput) - 15 : 1.0;
            double eneffn2 = (massInput > 10000 && Timet2 == "Instantaneous") ? 4 * Math.Log10(massInput) - 15 : 1.0;
            double eneffn3 = (massInput > 10000 && Timet3 == "Instantaneous") ? 4 * Math.Log10(massInput) - 15 : 1.0;
            double eneffn4 = (massInput > 10000 && Timet4 == "Instantaneous") ? 4 * Math.Log10(massInput) - 15 : 1.0;
            double eneffnDisplay = 1.0;
            if (Timet1 == "Instantaneous") eneffnDisplay = eneffn1;
            else if (Timet2 == "Instantaneous") eneffnDisplay = eneffn2;
            else if (Timet3 == "Instantaneous") eneffnDisplay = eneffn3;
            else if (Timet4 == "Instantaneous") eneffnDisplay = eneffn4;

            const double C7 = 10.763, C4 = 1.0, C8 = 1.0;
            bool isType0Liquid = (m.FluidType?.Trim() == "TYPE 0") && (fluidStoredType == "Liquid");

            // ---------- 6. Damage/Injury coefficients (queried once — same for all 4 holes) ----------
            var (conA, conB) = await GetCoeffAsync(conn, "Ref_COF_Damage", fluidStoredType, "Con", m.Repfluid);
            var (instA, instB) = await GetCoeffAsync(conn, "Ref_COF_Damage", fluidStoredType, "Inst", m.Repfluid);
            var (injConA, injConB) = await GetCoeffAsync(conn, "Ref_COF_Injury", fluidStoredType, "Con", m.Repfluid);
            var (injInstA, injInstB) = await GetCoeffAsync(conn, "Ref_COF_Injury", fluidStoredType, "Inst", m.Repfluid);

            (double ca, double eff) ContDamage(double rate)
            {
                if (conA == 0 || rate <= 0) return (0, rate);
                if (isType0Liquid)
                {
                    double ca1 = Math.Min(conA * Math.Pow(rate, conB), C7) * (1 - factmit);
                    double eff1 = (1 / C4) * Math.Exp(Math.Log10(ca1 / (C8 * conA)) * Math.Pow(conB, -1));
                    return (ca1, eff1);
                }
                return (conA * Math.Pow(rate, conB) * (1 - factmit), rate);
            }
            (double ca, double eff) InstDamage(double mass, double eneffn)
            {
                if (instA == 0 || mass <= 0) return (0, mass);
                if (isType0Liquid)
                {
                    double ca1 = Math.Min(instA * Math.Pow(mass, instB), C7) * (1 - factmit) / eneffn;
                    double eff1 = (1 / C4) * Math.Exp(Math.Log10(ca1 / (C8 * instA)) * Math.Pow(instB, -1));
                    return (ca1, eff1);
                }
                return (instA * Math.Pow(mass, instB) * (1 - factmit) / eneffn, mass);
            }
            double InjCont(double effrate) => (injConA == 0 || effrate <= 0) ? 0 : injConA * Math.Pow(effrate, injConB) * (1 - factmit);
            double InjInst(double effmass, double eneffn) => (injInstA == 0 || effmass <= 0) ? 0 : injInstA * Math.Pow(effmass, injInstB) * (1 - factmit) / eneffn;

            var (CAc1, effrate1) = ContDamage(rate1);
            var (CAc2, effrate2) = ContDamage(rate2);
            var (CAc3, effrate3) = ContDamage(rate3);
            var (CAc4, effrate4) = ContDamage(rate4);

            var (CAInst1, effmass1) = InstDamage(mass1, eneffn1);
            var (CAInst2, effmass2) = InstDamage(mass2, eneffn2);
            var (CAInst3, effmass3) = InstDamage(mass3, eneffn3);
            var (CAInst4, effmass4) = InstDamage(mass4, eneffn4);

            double CAinjCONT1 = InjCont(effrate1), CAinjCONT2 = InjCont(effrate2), CAinjCONT3 = InjCont(effrate3), CAinjCONT4 = InjCont(effrate4);
            double CAinjINST1 = InjInst(effmass1, eneffn1), CAinjINST2 = InjInst(effmass2, eneffn2),
                   CAinjINST3 = InjInst(effmass3, eneffn3), CAinjINST4 = InjInst(effmass4, eneffn4);

            // ---------- 7. Continuous/Instantaneous blending (TYPE 0 & mass >= 10,000 lbs only) ----------
            double factic1 = 0, factic2 = 0, factic3 = 0, factic4 = 0;
            double cacmd1 = 0, cacmd2 = 0, cacmd3 = 0, cacmd4 = 0;
            double cainj1 = 0, cainj2 = 0, cainj3 = 0, cainj4 = 0;

            if (m.FluidType?.Trim() == "TYPE 0" && massInput >= 10000.0)
            {
                const double c5 = 55.6;
                factic1 = Timet1 == "Continuous" ? rate1 / c5 : 1.0;
                factic2 = Timet2 == "Continuous" ? rate2 / c5 : 1.0;
                factic3 = Timet3 == "Continuous" ? rate3 / c5 : 1.0;
                factic4 = Timet4 == "Continuous" ? rate4 / c5 : 1.0;

                double instmax3 = Math.Max(Math.Max(CAInst1, CAInst2), Math.Max(CAInst3, CAInst4));
                double injmax3 = Math.Max(Math.Max(CAinjINST1, CAinjINST2), Math.Max(CAinjINST3, CAinjINST4));

                cacmd1 = Timet1 == "Continuous" ? CAc1 * factic1 + instmax3 * (1 - factic1)
                                                 : (CAInst1 == instmax3 ? instmax3 : CAc1 * factic1 + CAInst1 * (1 - factic1));
                cacmd2 = Timet2 == "Continuous" ? CAc2 * factic2 + instmax3 * (1 - factic2)
                                                 : (CAInst2 == instmax3 ? instmax3 : CAc2 * factic2 + CAInst2 * (1 - factic2));
                cacmd3 = Timet3 == "Continuous" ? CAc3 * factic3 + instmax3 * (1 - factic3)
                                                 : (CAInst3 == instmax3 ? instmax3 : CAc3 * factic3 + CAInst3 * (1 - factic3));
                cacmd4 = Timet4 == "Continuous" ? CAc4 * factic4 + instmax3 * (1 - factic4)
                                                 : (CAInst4 == instmax3 ? instmax3 : CAc4 * factic4 + CAInst4 * (1 - factic4));

                cainj1 = Timet1 == "Continuous" ? CAinjCONT1 * factic1 + injmax3 * (1 - factic1)
                                                 : (CAinjINST1 == injmax3 ? injmax3 : CAinjCONT1 * factic1 + CAinjINST1 * (1 - factic1));
                cainj2 = Timet2 == "Continuous" ? CAinjCONT2 * factic2 + injmax3 * (1 - factic2)
                                                 : (CAinjINST2 == injmax3 ? injmax3 : CAinjCONT2 * factic2 + CAinjINST2 * (1 - factic2));
                cainj3 = Timet3 == "Continuous" ? CAinjCONT3 * factic3 + injmax3 * (1 - factic3)
                                                 : (CAinjINST3 == injmax3 ? injmax3 : CAinjCONT3 * factic3 + CAinjINST3 * (1 - factic3));
                cainj4 = Timet4 == "Continuous" ? CAinjCONT4 * factic4 + injmax3 * (1 - factic4)
                                                 : (CAinjINST4 == injmax3 ? injmax3 : CAinjCONT4 * factic4 + CAinjINST4 * (1 - factic4));
            }

            // ---------- 8. GFF hole-size weighting -> final totals ----------
            const double gff1 = 0.261, gff2 = 0.654, gff3 = 0.065, gff4 = 0.02;
            double CACMDFinal1, CACMDFinal2, CACMDFinal3, CACMDFinal4;
            double CAnfnt1, CAnfnt2, CAnfnt3, CAnfnt4;

            if (cacmd1 != 0.0)
            {
                CACMDFinal1 = cacmd1 * gff1; CACMDFinal2 = cacmd2 * gff2; CACMDFinal3 = cacmd3 * gff3; CACMDFinal4 = cacmd4 * gff4;
                CAnfnt1 = cainj1 * gff1; CAnfnt2 = cainj2 * gff2; CAnfnt3 = cainj3 * gff3; CAnfnt4 = cainj4 * gff4;
            }
            else
            {
                CACMDFinal1 = (Timet1 == "Continuous" ? CAc1 : CAInst1) * gff1;
                CACMDFinal2 = (Timet2 == "Continuous" ? CAc2 : CAInst2) * gff2;
                CACMDFinal3 = (Timet3 == "Continuous" ? CAc3 : CAInst3) * gff3;
                CACMDFinal4 = (Timet4 == "Continuous" ? CAc4 : CAInst4) * gff4;

                CAnfnt1 = (Timet1 == "Continuous" ? CAinjCONT1 : CAinjINST1) * gff1;
                CAnfnt2 = (Timet2 == "Continuous" ? CAinjCONT2 : CAinjINST2) * gff2;
                CAnfnt3 = (Timet3 == "Continuous" ? CAinjCONT3 : CAinjINST3) * gff3;
                CAnfnt4 = (Timet4 == "Continuous" ? CAinjCONT4 : CAinjINST4) * gff4;
            }

            double CACMDFinaltot = CACMDFinal1 + CACMDFinal2 + CACMDFinal3 + CACMDFinal4;
            double CAnfnttotal = CAnfnt1 + CAnfnt2 + CAnfnt3 + CAnfnt4;

            string CmdCate = GetCategoryLetter(CACMDFinaltot);
            string CAinjCate = GetCategoryLetter(CAnfnttotal);
            double maxval = CACMDFinaltot > CAnfnttotal ? CACMDFinaltot : CAnfnttotal;
            string maxcate = CACMDFinaltot > CAnfnttotal ? CmdCate : CAinjCate;

            // ---------- 9. Populate model with every intermediate value ----------
            m.W1 = w1; m.W2 = w2; m.W3 = w3; m.W4 = w4;
            m.Time1 = Timet1; m.Time2 = Timet2; m.Time3 = Timet3; m.Time4 = Timet4;
            m.T1 = t1; m.T2 = t2; m.T3 = t3; m.T4 = t4;
            m.Rate1 = rate1; m.Rate2 = rate2; m.Rate3 = rate3; m.Rate4 = rate4;
            m.ID1 = useld1; m.ID2 = useld2; m.ID3 = useld3; m.ID4 = useld4;
            m.Mass1 = mass1; m.Mass2 = mass2; m.Mass3 = mass3; m.Mass4 = mass4;
            m.Efficiency = eneffnDisplay;
            m.CAA = conA; m.CAB = conB;
            m.CAc1 = CAc1; m.CAc2 = CAc2; m.CAc3 = CAc3; m.CAc4 = CAc4;
            m.CAInsA = instA; m.CAInsB = instB;
            m.CAInst1 = CAInst1; m.CAInst2 = CAInst2; m.CAInst3 = CAInst3; m.CAInst4 = CAInst4;
            m.AInj = injConA; m.BInj = injConB;
            m.CAInj1 = CAinjCONT1; m.CAInj2 = CAinjCONT2; m.CAInj3 = CAinjCONT3; m.CAInj4 = CAinjCONT4;
            m.AInsInj = injInstA; m.BInsInj = injInstB;
            m.CAInsInj1 = CAinjINST1; m.CAInsInj2 = CAinjINST2; m.CAInsInj3 = CAinjINST3; m.CAInsInj4 = CAinjINST4;
            m.Factic1 = factic1; m.Factic2 = factic2; m.Factic3 = factic3; m.Factic4 = factic4;
            m.CAcmd1 = cacmd1; m.CAcmd2 = cacmd2; m.CAcmd3 = cacmd3; m.CAcmd4 = cacmd4;
            m.CAbleInj1 = cainj1; m.CAbleInj2 = cainj2; m.CAbleInj3 = cainj3; m.CAbleInj4 = cainj4;
            m.CAcmdFinal1 = CACMDFinal1; m.CAcmdFinal2 = CACMDFinal2; m.CAcmdFinal3 = CACMDFinal3; m.CAcmdFinal4 = CACMDFinal4;
            m.CAInjFinal1 = CAnfnt1; m.CAInjFinal2 = CAnfnt2; m.CAInjFinal3 = CAnfnt3; m.CAInjFinal4 = CAnfnt4;
            m.CAcmdTotal = SafeDecimal(CACMDFinaltot);
            m.CAInjTotal = SafeDecimal(CAnfnttotal);
            m.CAcmdCategory = CmdCate;
            m.CAInjCategory = CAinjCate;
            m.MaxValue = maxval;
            m.MaxCategory = maxcate;
            m.Ptrans = ptrans;

            // ---------- 10. Persist — check exists then UPDATE else INSERT (all columns) ----------
            await SaveFullRecordAsync(conn, m);

            return m;
        }

        private async Task SaveFullRecordAsync(SqlConnection conn, COFFlammableModel m)
        {
            string checkQuery = "SELECT COUNT(1) FROM COF_Flammable WHERE ProcID=@ProcID AND EquID=@EquID AND CompID=@CompID AND Deleted=0";
            using var checkCmd = new SqlCommand(checkQuery, conn);
            checkCmd.Parameters.AddWithValue("@ProcID", m.ProcID ?? 0);
            checkCmd.Parameters.AddWithValue("@EquID", m.EquID ?? 0);
            checkCmd.Parameters.AddWithValue("@CompID", m.CompID ?? 0);
            int count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

            using var cmd = new SqlCommand { Connection = conn };

            if (count > 0)
            {
                cmd.CommandText = @"
UPDATE COF_Flammable SET
Fluid=@Fluid, Repfluid=@Repfluid, Type=@Type, OpPres=@OpPres, Optemp=@Optemp, P1=@P1, Mass=@Mass,
Detect=@Detect, Iso=@Iso, factMit=@factMit,
w1=@w1, w2=@w2, w3=@w3, w4=@w4,
Time1=@Time1, Time2=@Time2, Time3=@Time3, Time4=@Time4,
T1=@T1, T2=@T2, T3=@T3, T4=@T4,
rate1=@rate1, rate2=@rate2, rate3=@rate3, rate4=@rate4,
id1=@id1, id2=@id2, id3=@id3, id4=@id4,
mass1=@mass1, mass2=@mass2, mass3=@mass3, mass4=@mass4,
eneff=@eneff, CaA=@CaA, CaB=@CaB,
CAc1=@CAc1, CAc2=@CAc2, CAc3=@CAc3, CAc4=@CAc4,
CaInsA=@CaInsA, CaInsB=@CaInsB,
CAIns1=@CAIns1, CAIns2=@CAIns2, CAIns3=@CAIns3, CAIns4=@CAIns4,
Ainj=@Ainj, Binj=@Binj,
CAInj1=@CAInj1, CAInj2=@CAInj2, CAInj3=@CAInj3, CAInj4=@CAInj4,
AInsInj=@AInsInj, BInsInj=@BInsInj,
CAInsInj1=@CAInsInj1, CAInsInj2=@CAInsInj2, CAInsInj3=@CAInsInj3, CAInsInj4=@CAInsInj4,
factic1=@factic1, factic2=@factic2, factic3=@factic3, factic4=@factic4,
CAcmd1=@CAcmd1, CAcmd2=@CAcmd2, CAcmd3=@CAcmd3, CAcmd4=@CAcmd4,
CAbleInj1=@CAbleInj1, CAbleInj2=@CAbleInj2, CAbleInj3=@CAbleInj3, CAbleInj4=@CAbleInj4,
CAcmdFinal1=@CAcmdFinal1, CAcmdFinal2=@CAcmdFinal2, CAcmdFinal3=@CAcmdFinal3, CAcmdFinal4=@CAcmdFinal4,
CAInjFinal1=@CAInjFinal1, CAInjFinal2=@CAInjFinal2, CAInjFinal3=@CAInjFinal3, CAInjFinal4=@CAInjFinal4,
CAcmdTotal=@CAcmdTotal, CAInjTotal=@CAInjTotal, CAcmdCate=@CAcmdCate, CAinjCate=@CAinjCate,
maxval=@maxval, maxcate=@maxcate, ptrans=@ptrans, FluidStored=@FluidStored,
ModifiedBy=@ModifiedBy, ModifiedDate=@ModifiedDate
WHERE ProcID=@ProcID AND EquID=@EquID AND CompID=@CompID";
            }
            else
            {
                cmd.CommandText = @"
INSERT INTO COF_Flammable
(ProcID, EquID, CompID, Fluid, Repfluid, Type, OpPres, Optemp, P1, Mass, Detect, Iso, factMit,
 w1, w2, w3, w4, Time1, Time2, Time3, Time4, T1, T2, T3, T4, rate1, rate2, rate3, rate4,
 id1, id2, id3, id4, mass1, mass2, mass3, mass4, eneff, CaA, CaB,
 CAc1, CAc2, CAc3, CAc4, CaInsA, CaInsB, CAIns1, CAIns2, CAIns3, CAIns4,
 Ainj, Binj, CAInj1, CAInj2, CAInj3, CAInj4, AInsInj, BInsInj,
 CAInsInj1, CAInsInj2, CAInsInj3, CAInsInj4, factic1, factic2, factic3, factic4,
 CAcmd1, CAcmd2, CAcmd3, CAcmd4, CAbleInj1, CAbleInj2, CAbleInj3, CAbleInj4,
 CAcmdFinal1, CAcmdFinal2, CAcmdFinal3, CAcmdFinal4, CAInjFinal1, CAInjFinal2, CAInjFinal3, CAInjFinal4,
 CAcmdTotal, CAInjTotal, CAcmdCate, CAinjCate, maxval, maxcate, ptrans, FluidStored,
 Deleted, CreatedBy, CreatedDate)
VALUES
(@ProcID, @EquID, @CompID, @Fluid, @Repfluid, @Type, @OpPres, @Optemp, @P1, @Mass, @Detect, @Iso, @factMit,
 @w1, @w2, @w3, @w4, @Time1, @Time2, @Time3, @Time4, @T1, @T2, @T3, @T4, @rate1, @rate2, @rate3, @rate4,
 @id1, @id2, @id3, @id4, @mass1, @mass2, @mass3, @mass4, @eneff, @CaA, @CaB,
 @CAc1, @CAc2, @CAc3, @CAc4, @CaInsA, @CaInsB, @CAIns1, @CAIns2, @CAIns3, @CAIns4,
 @Ainj, @Binj, @CAInj1, @CAInj2, @CAInj3, @CAInj4, @AInsInj, @BInsInj,
 @CAInsInj1, @CAInsInj2, @CAInsInj3, @CAInsInj4, @factic1, @factic2, @factic3, @factic4,
 @CAcmd1, @CAcmd2, @CAcmd3, @CAcmd4, @CAbleInj1, @CAbleInj2, @CAbleInj3, @CAbleInj4,
 @CAcmdFinal1, @CAcmdFinal2, @CAcmdFinal3, @CAcmdFinal4, @CAInjFinal1, @CAInjFinal2, @CAInjFinal3, @CAInjFinal4,
 @CAcmdTotal, @CAInjTotal, @CAcmdCate, @CAinjCate, @maxval, @maxcate, @ptrans, @FluidStored,
 0, @CreatedBy, @CreatedDate)";
            }

            cmd.Parameters.AddWithValue("@ProcID", m.ProcID ?? 0);
            cmd.Parameters.AddWithValue("@EquID", m.EquID ?? 0);
            cmd.Parameters.AddWithValue("@CompID", m.CompID ?? 0);
            cmd.Parameters.AddWithValue("@Fluid", m.Fluid ?? "");
            cmd.Parameters.AddWithValue("@Repfluid", m.Repfluid ?? "");
            cmd.Parameters.AddWithValue("@Type", m.FluidType ?? "");
            cmd.Parameters.AddWithValue("@OpPres", (m.Pressure ?? 0).ToString());
            cmd.Parameters.AddWithValue("@Optemp", (m.Temperature ?? 0).ToString());
            cmd.Parameters.AddWithValue("@P1", (m.P1 ?? 0).ToString());
            cmd.Parameters.AddWithValue("@Mass", (m.Inventory ?? 0).ToString());
            cmd.Parameters.AddWithValue("@Detect", m.Detection ?? "");
            cmd.Parameters.AddWithValue("@Iso", m.Isolation ?? "");
            cmd.Parameters.AddWithValue("@factMit", m.MitigationValue ?? "0");
            cmd.Parameters.AddWithValue("@w1", m.W1.ToString("#.##"));
            cmd.Parameters.AddWithValue("@w2", m.W2.ToString("#.##"));
            cmd.Parameters.AddWithValue("@w3", m.W3.ToString("#.##"));
            cmd.Parameters.AddWithValue("@w4", m.W4.ToString("#.##"));
            cmd.Parameters.AddWithValue("@Time1", m.Time1);
            cmd.Parameters.AddWithValue("@Time2", m.Time2);
            cmd.Parameters.AddWithValue("@Time3", m.Time3);
            cmd.Parameters.AddWithValue("@Time4", m.Time4);
            cmd.Parameters.AddWithValue("@T1", m.T1.ToString("#.##"));
            cmd.Parameters.AddWithValue("@T2", m.T2.ToString("#.##"));
            cmd.Parameters.AddWithValue("@T3", m.T3.ToString("#.##"));
            cmd.Parameters.AddWithValue("@T4", m.T4.ToString("#.##"));
            cmd.Parameters.AddWithValue("@rate1", m.Rate1.ToString("#.##"));
            cmd.Parameters.AddWithValue("@rate2", m.Rate2.ToString("#.##"));
            cmd.Parameters.AddWithValue("@rate3", m.Rate3.ToString("#.##"));
            cmd.Parameters.AddWithValue("@rate4", m.Rate4.ToString("#.##"));
            cmd.Parameters.AddWithValue("@id1", m.ID1);
            cmd.Parameters.AddWithValue("@id2", m.ID2);
            cmd.Parameters.AddWithValue("@id3", m.ID3);
            cmd.Parameters.AddWithValue("@id4", m.ID4);
            cmd.Parameters.AddWithValue("@mass1", m.Mass1.ToString("#.##"));
            cmd.Parameters.AddWithValue("@mass2", m.Mass2.ToString("#.##"));
            cmd.Parameters.AddWithValue("@mass3", m.Mass3.ToString("#.##"));
            cmd.Parameters.AddWithValue("@mass4", m.Mass4.ToString("#.##"));
            cmd.Parameters.AddWithValue("@eneff", m.Efficiency.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CaA", m.CAA);
            cmd.Parameters.AddWithValue("@CaB", m.CAB);
            cmd.Parameters.AddWithValue("@CAc1", m.CAc1.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAc2", m.CAc2.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAc3", m.CAc3.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAc4", m.CAc4.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CaInsA", m.CAInsA);
            cmd.Parameters.AddWithValue("@CaInsB", m.CAInsB);
            cmd.Parameters.AddWithValue("@CAIns1", m.CAInst1.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAIns2", m.CAInst2.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAIns3", m.CAInst3.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAIns4", m.CAInst4.ToString("#.##"));
            cmd.Parameters.AddWithValue("@Ainj", m.AInj);
            cmd.Parameters.AddWithValue("@Binj", m.BInj);
            cmd.Parameters.AddWithValue("@CAInj1", m.CAInj1.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAInj2", m.CAInj2.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAInj3", m.CAInj3.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAInj4", m.CAInj4.ToString("#.##"));
            cmd.Parameters.AddWithValue("@AInsInj", m.AInsInj);
            cmd.Parameters.AddWithValue("@BInsInj", m.BInsInj);
            cmd.Parameters.AddWithValue("@CAInsInj1", m.CAInsInj1.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAInsInj2", m.CAInsInj2.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAInsInj3", m.CAInsInj3.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAInsInj4", m.CAInsInj4.ToString("#.##"));
            cmd.Parameters.AddWithValue("@factic1", m.Factic1.ToString("#.##"));
            cmd.Parameters.AddWithValue("@factic2", m.Factic2.ToString("#.##"));
            cmd.Parameters.AddWithValue("@factic3", m.Factic3.ToString("#.##"));
            cmd.Parameters.AddWithValue("@factic4", m.Factic4.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAcmd1", m.CAcmd1.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAcmd2", m.CAcmd2.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAcmd3", m.CAcmd3.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAcmd4", m.CAcmd4.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAbleInj1", m.CAbleInj1.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAbleInj2", m.CAbleInj2.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAbleInj3", m.CAbleInj3.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAbleInj4", m.CAbleInj4.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAcmdFinal1", m.CAcmdFinal1.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAcmdFinal2", m.CAcmdFinal2.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAcmdFinal3", m.CAcmdFinal3.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAcmdFinal4", m.CAcmdFinal4.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAInjFinal1", m.CAInjFinal1.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAInjFinal2", m.CAInjFinal2.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAInjFinal3", m.CAInjFinal3.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAInjFinal4", m.CAInjFinal4.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAcmdTotal", m.CAcmdTotal.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAInjTotal", m.CAInjTotal.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAcmdCate", m.CAcmdCategory ?? "");
            cmd.Parameters.AddWithValue("@CAinjCate", m.CAInjCategory ?? "");
            cmd.Parameters.AddWithValue("@maxval", m.MaxValue.ToString("#.##"));
            cmd.Parameters.AddWithValue("@maxcate", m.MaxCategory ?? "");
            cmd.Parameters.AddWithValue("@ptrans", m.Ptrans.ToString());
            cmd.Parameters.AddWithValue("@FluidStored", m.Fluid ?? "");

            if (count > 0)
            {
                cmd.Parameters.AddWithValue("@ModifiedBy", m.UpdatedBy ?? "1");
                cmd.Parameters.AddWithValue("@ModifiedDate", DateTime.Now);
            }
            else
            {
                cmd.Parameters.AddWithValue("@CreatedBy", m.CreatedBy ?? "1");
                cmd.Parameters.AddWithValue("@CreatedDate", DateTime.Now);
            }

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<bool> DeleteFlammableAsync(decimal? procId, decimal? equId, decimal? compId)
        {
            string query = "DELETE FROM COF_Flammable WHERE ProcID = @ProcID AND EquID = @EquID AND CompID = @CompID";
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@ProcID", procId ?? 0);
            cmd.Parameters.AddWithValue("@EquID", equId ?? 0);
            cmd.Parameters.AddWithValue("@CompID", compId ?? 0);
            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        // =================================================================
        // 4. NON-FLAMMABLE — record load
        // =================================================================

        public async Task<COFNonFlammableModel?> GetNonFlammableRecordAsync(decimal procId, decimal equId, decimal compId)
        {
            string query = "SELECT * FROM COF_NonFlammable WHERE ProcID = @ProcID AND EquID = @EquID AND CompID = @CompID AND Deleted = 0";
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@ProcID", procId);
            cmd.Parameters.AddWithValue("@EquID", equId);
            cmd.Parameters.AddWithValue("@CompID", compId);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            // Table columns are nvarchar — must use TryParse, never Convert.ToXxx (blank/"" throws)
            double D(string col) => double.TryParse(reader[col]?.ToString(), out var v) ? v : 0;
            decimal Dec(string col) => decimal.TryParse(reader[col]?.ToString(), out var v) ? v : 0;
            string S(string col) => reader[col]?.ToString() ?? "";

            return new COFNonFlammableModel
            {
                NonFlameID = reader.GetDecimal(reader.GetOrdinal("NonFlameID")),
                ProcID = procId,
                EquID = equId,
                CompID = compId,
                Fluid = S("Fluid"),
                Repfluid = S("Repfluid"),
                FluidType = S("Type"),
                Pressure = Dec("OpPres"),
                Temperature = Dec("Optemp"),
                P1 = Dec("P1"),
                Inventory = Dec("Mass"),
                Detection = S("Detect"),
                Isolation = S("Iso"),
                MitigationValue = S("factMit"),
                Phase = "", // Final Phase has no column in COF_NonFlammable either — must be re-selected each time

                W1 = D("w1"), W2 = D("w2"), W3 = D("w3"), W4 = D("w4"),
                Time1 = S("Time1"), Time2 = S("Time2"), Time3 = S("Time3"), Time4 = S("Time4"),
                T1 = D("T1"), T2 = D("T2"), T3 = D("T3"), T4 = D("T4"),
                Rate1 = D("rate1"), Rate2 = D("rate2"), Rate3 = D("rate3"), Rate4 = D("rate4"),
                ID1 = S("id1"), ID2 = S("id2"), ID3 = S("id3"), ID4 = S("id4"),
                Mass1 = D("mass1"), Mass2 = D("mass2"), Mass3 = D("mass3"), Mass4 = D("mass4"),
                Efficiency = D("eneff"),
                CAInj1 = D("CAInj1"), CAInj2 = D("CAInj2"), CAInj3 = D("CAInj3"), CAInj4 = D("CAInj4"),
                CAInsInj1 = D("CAInsInj1"), CAInsInj2 = D("CAInsInj2"), CAInsInj3 = D("CAInsInj3"), CAInsInj4 = D("CAInsInj4"),
                Factic1 = D("factic1"), Factic2 = D("factic2"), Factic3 = D("factic3"), Factic4 = D("factic4"),
                CAbleInj1 = D("CAbleInj1"), CAbleInj2 = D("CAbleInj2"), CAbleInj3 = D("CAbleInj3"), CAbleInj4 = D("CAbleInj4"),
                CAInjFinal1 = D("CAInjFinal1"), CAInjFinal2 = D("CAInjFinal2"), CAInjFinal3 = D("CAInjFinal3"), CAInjFinal4 = D("CAInjFinal4"),
                CAInjTotal = Dec("CAInjTotal"),
                CAInjCategory = S("CAinjCate"),
                Ptrans = D("ptrans"),
                G = D("g"),
                H = D("h"),
            };
        }

        // =================================================================
        // 5. NON-FLAMMABLE — Calculate + Save (single button)
        // =================================================================

        public async Task<COFNonFlammableModel> CalculateAndSaveNonFlammableAsync(COFNonFlammableModel m)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // ---------- 0. Auto-fill FluidType if not already set ----------
            if (string.IsNullOrWhiteSpace(m.FluidType) && !string.IsNullOrWhiteSpace(m.Repfluid))
            {
                using var ftCmd = new SqlCommand("SELECT Fluid_Type FROM Ref_COF_Rep_Fluids WHERE Rep_Fluid = @RepFluid", conn);
                ftCmd.Parameters.AddWithValue("@RepFluid", m.Repfluid);
                var ftResult = await ftCmd.ExecuteScalarAsync();
                m.FluidType = ftResult?.ToString()?.Trim() ?? "";
            }

            // ---------- 1. Reference fluid properties (Ref_COF_Lvl_1) ----------
            // MW, A, B, C, D, E are nvarchar and hold "N/A" where not applicable — parse lazily, Gas branch only.
            string refQuery = "SELECT MW, LqdDensity, NBF, Ambient, IdealGas, A, B, C, D, E, IgnTemp FROM Ref_COF_Lvl_1 WHERE Fluid = @Fluid";
            using var refCmd = new SqlCommand(refQuery, conn);
            refCmd.Parameters.AddWithValue("@Fluid", m.Repfluid ?? "");
            string mwRaw = "0", idealGas = "", aStr = "0", bStr = "0", cStr = "0", dStr = "0", eStr = "0";
            using (var reader = await refCmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    mwRaw = reader["MW"]?.ToString()?.Trim() ?? "0";
                    idealGas = reader["IdealGas"]?.ToString()?.Trim() ?? "";
                    aStr = reader["A"]?.ToString()?.Trim() ?? "0";
                    bStr = reader["B"]?.ToString()?.Trim() ?? "0";
                    cStr = reader["C"]?.ToString()?.Trim() ?? "0";
                    dStr = reader["D"]?.ToString()?.Trim() ?? "0";
                    eStr = reader["E"]?.ToString()?.Trim() ?? "0";
                }
            }

            double opTempKelvin = Convert.ToDouble(m.Temperature ?? 0) + 273;
            double opPressure = Convert.ToDouble(m.Pressure ?? 0);
            double massInput = Convert.ToDouble(m.Inventory ?? 0);
            const double A1 = 0.049, A2 = 0.785, A3 = 12.56, A4 = 200.96;
            double w1 = 0, w2 = 0, w3 = 0, w4 = 0, ptrans = 0;
            string fluidStoredType = m.Fluid ?? "";

            // ---------- 2. Release rate w1..w4 ----------
            if (fluidStoredType == "Liquid")
            {
                double cd = 0.61;
                double p1Val = Convert.ToDouble(m.P1 ?? 0);
                if (p1Val > 0)
                {
                    double diff = 2 * 32.2 * (opPressure - 14.7) / p1Val;
                    w1 = cd * (A1 / 12.0) * p1Val * Math.Sqrt(diff);
                    w2 = cd * (A2 / 12.0) * p1Val * Math.Sqrt(diff);
                    w3 = cd * (A3 / 12.0) * p1Val * Math.Sqrt(diff);
                    w4 = cd * (A4 / 12.0) * p1Val * Math.Sqrt(diff);
                }
            }
            else // Gas
            {
                double T = opTempKelvin, cp = 0;
                double SafeD(string s) => double.TryParse(s, out var v) ? v : 0;

                if (idealGas == "Note 1")
                {
                    cp = SafeD(aStr) + (SafeD(bStr) * T) + (SafeD(cStr) * Math.Pow(T, 2)) + (SafeD(dStr) * Math.Pow(T, 3));
                }
                else if (idealGas == "Note 2")
                {
                    double cVal = SafeD(cStr), eVal = SafeD(eStr);
                    double ct = (cVal / T) / Math.Sinh(cVal / T);
                    double et = (eVal / T) / Math.Cosh(eVal / T);
                    cp = SafeD(aStr) + (SafeD(bStr) * Math.Pow(ct, 2)) + (SafeD(dStr) * Math.Pow(et, 2));
                }
                else if (idealGas == "Note 3")
                {
                    cp = SafeD(aStr) + (SafeD(bStr) * T) + (SafeD(cStr) * Math.Pow(T, 2)) + (SafeD(dStr) * Math.Pow(T, 3)) + (SafeD(eStr) * Math.Pow(T, 4));
                }

                double mw = SafeD(mwRaw);
                double R = 8.314;
                double kFactor = cp / (cp - R);
                double cd2 = 0.90;
                double tsRankine = (Convert.ToDouble(m.Temperature ?? 0) * 1.8) + 491.67;
                const double R1 = 1545, patm = 14.7;
                ptrans = patm * Math.Pow((kFactor + 1) / 2, kFactor / (kFactor - 1));

                if (opPressure > ptrans) // Sonic
                {
                    double kmw = (kFactor * mw * 32.2) / (R1 * tsRankine);
                    double power = (kFactor + 1) / (kFactor - 1);
                    double kplus = Math.Pow(2 / (kFactor + 1), power);
                    w1 = cd2 * A1 * opPressure * Math.Sqrt(kmw * kplus);
                    w2 = cd2 * A2 * opPressure * Math.Sqrt(kmw * kplus);
                    w3 = cd2 * A3 * opPressure * Math.Sqrt(kmw * kplus);
                    w4 = cd2 * A4 * opPressure * Math.Sqrt(kmw * kplus);
                }
                else // SubSonic
                {
                    double gmw = (mw * 32.2) / (R1 * tsRankine);
                    double k2 = (2 * kFactor) / (kFactor - 1);
                    double patmps = Math.Pow(patm / opPressure, 2 / kFactor);
                    double patmps1 = 1 - Math.Pow(patm / opPressure, (kFactor - 1) / kFactor);
                    w1 = cd2 * A1 * opPressure * Math.Sqrt(gmw * k2 * patmps * patmps1);
                    w2 = cd2 * A2 * opPressure * Math.Sqrt(gmw * k2 * patmps * patmps1);
                    w3 = cd2 * A3 * opPressure * Math.Sqrt(gmw * k2 * patmps * patmps1);
                    w4 = cd2 * A4 * opPressure * Math.Sqrt(gmw * k2 * patmps * patmps1);
                }
            }

            // ---------- 3. Duration classification ----------
            double c3 = massInput >= 10000.0 ? 10000 : massInput;
            double t1 = SafeDiv(c3, w1), t2 = SafeDiv(c3, w2), t3 = SafeDiv(c3, w3), t4 = SafeDiv(c3, w4);
            string Timet1 = "Continuous"; // forced, matches old code
            string Timet2 = t2 <= 180 ? "Instantaneous" : "Continuous";
            string Timet3 = t3 <= 180 ? "Instantaneous" : "Continuous";
            string Timet4 = t4 <= 180 ? "Instantaneous" : "Continuous";

            // ---------- 4. Detection/Isolation reduction + leak-duration caps ----------
            double factdi = 0;
            using (var detIsoCmd = new SqlCommand("SELECT Reduction FROM Ref_COF_Adj_Det_Iso WHERE Detection=@Det AND Isolat=@Iso", conn))
            {
                detIsoCmd.Parameters.AddWithValue("@Det", m.Detection ?? "");
                detIsoCmd.Parameters.AddWithValue("@Iso", m.Isolation ?? "");
                var r = await detIsoCmd.ExecuteScalarAsync();
                if (r != null) factdi = Convert.ToDouble(r);
            }

            double ld1 = 0, ld2 = 0, ld3 = 0;
            using (var ldCmd = new SqlCommand("SELECT Inch25, Inch1, Inch4 FROM Ref_COF_Leak_Det_Iso WHERE Detection=@Det AND Isolat=@Iso", conn))
            {
                ldCmd.Parameters.AddWithValue("@Det", m.Detection ?? "");
                ldCmd.Parameters.AddWithValue("@Iso", m.Isolation ?? "");
                using var r = await ldCmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    ld1 = Convert.ToDouble(r["Inch25"]);
                    ld2 = Convert.ToDouble(r["Inch1"]);
                    ld3 = Convert.ToDouble(r["Inch4"]);
                }
            }

            double rate1 = w1 * (1 - factdi), rate2 = w2 * (1 - factdi), rate3 = w3 * (1 - factdi), rate4 = w4 * (1 - factdi);

            double usld1 = Math.Min(SafeDiv(massInput, rate1) / 60, ld1);
            double usld2 = Math.Min(SafeDiv(massInput, rate2) / 60, ld2);
            double usld3 = Math.Min(SafeDiv(massInput, rate3) / 60, ld3);
            double usld4 = SafeDiv(massInput, rate4) / 60;

            double mass1 = 0, mass2 = 0, mass3 = 0, mass4 = 0;
            string useld1 = "", useld2 = "", useld3 = "", useld4 = "";

            bool isSteam = (m.Repfluid ?? "").Trim().Equals("steam", StringComparison.OrdinalIgnoreCase);
            const double C9 = 0.6, C10 = 63.32, C5 = 55.6;
            double CAInj1 = 0, CAInj2 = 0, CAInj3 = 0, CAInj4 = 0;
            double CAInjInst1 = 0, CAInjInst2 = 0, CAInjInst3 = 0, CAInjInst4 = 0;
            double effrate1 = 0, effrate2 = 0, effrate3 = 0, effrate4 = 0;
            double factic1 = 0, factic2 = 0, factic3 = 0, factic4 = 0;
            double CAc1 = 0, CAc2 = 0, CAc3 = 0, CAc4 = 0;
            double eneffn = 1.0;
            double g = 0, h = 0;

            if (isSteam)
            {
                // ---------- STEAM: continuous rate-based OR instantaneous mass-based injury area ----------
                (double m1, string id1) HoleMass(string timet, double rate, double usld)
                {
                    if (timet == "Continuous") return (rate * usld * 60, usld.ToString("#.##"));
                    return (massInput, "Instantaneous");
                }
                (mass1, useld1) = HoleMass(Timet1, rate1, usld1);
                (mass2, useld2) = HoleMass(Timet2, rate2, usld2);
                (mass3, useld3) = HoleMass(Timet3, rate3, usld3);
                (mass4, useld4) = HoleMass(Timet4, rate4, usld4);

                double factmit = string.IsNullOrEmpty(m.MitigationValue) ? 0 : Convert.ToDouble(m.MitigationValue);
                bool allInstantaneous = Timet1 == "Instantaneous" && Timet2 == "Instantaneous" && Timet3 == "Instantaneous" && Timet4 == "Instantaneous";
                eneffn = (massInput > 10000 || allInstantaneous) && massInput > 0 ? 4 * Math.Log10(massInput) - 15 : 1.0;

                void HoleInjury(string timet, double rate, double mass, out double caInj, out double caInjInst, out double effrate)
                {
                    caInj = 0; caInjInst = 0; effrate = 0;
                    if (timet == "Continuous") { caInj = C9 * rate; effrate = rate; }
                    else { caInjInst = C10 * Math.Pow(mass, 0.6384); }
                }
                HoleInjury(Timet1, rate1, mass1, out CAInj1, out CAInjInst1, out effrate1);
                HoleInjury(Timet2, rate2, mass2, out CAInj2, out CAInjInst2, out effrate2);
                HoleInjury(Timet3, rate3, mass3, out CAInj3, out CAInjInst3, out effrate3);
                HoleInjury(Timet4, rate4, mass4, out CAInj4, out CAInjInst4, out effrate4);

                bool blending = m.FluidType?.Trim() == "TYPE 0" && massInput >= 10000.0;
                if (blending)
                {
                    factic1 = Math.Min(rate1 / C5, 1.0);
                    factic2 = Math.Min(rate2 / C5, 1.0);
                    factic3 = Math.Min(rate3 / C5, 1.0);
                    factic4 = Math.Min(rate4 / C5, 1.0);

                    CAc1 = CAInjInst1 * factic1 + CAInj1 * (1 - factic1);
                    CAc2 = CAInjInst2 * factic2 + CAInj2 * (1 - factic2);
                    CAc3 = CAInjInst3 * factic3 + CAInj3 * (1 - factic3);
                    CAc4 = CAInjInst4 * factic4 + CAInj4 * (1 - factic4);
                }
                else
                {
                    CAc1 = Math.Max(CAInj1, CAInjInst1);
                    CAc2 = Math.Max(CAInj2, CAInjInst2);
                    CAc3 = Math.Max(CAInj3, CAInjInst3);
                    CAc4 = Math.Max(CAInj4, CAInjInst4);
                }
            }
            else
            {
                // ---------- NON-STEAM (water/acid/etc.): pressure-based g/h correlation, Continuous only ----------
                const double C8 = 1, C4 = 1, C11 = 1;
                double diffP = C11 * (opPressure - 14.7);
                g = 2696.0 - 21.9 * diffP + 1.474 * Math.Pow(diffP, 2);
                h = 0.31 - 0.00032 * Math.Pow(diffP - 40, 2);

                CAc1 = (Timet1 == "Continuous" && rate1 > 0) ? 0.2 * C8 * g * Math.Pow(C4 * rate1, h) : 0.0;
                CAc2 = (Timet2 == "Continuous" && rate2 > 0) ? 0.2 * C8 * g * Math.Pow(C4 * rate2, h) : 0.0;
                CAc3 = (Timet3 == "Continuous" && rate3 > 0) ? 0.2 * C8 * g * Math.Pow(C4 * rate3, h) : 0.0;
                CAc4 = (Timet4 == "Continuous" && rate4 > 0) ? 0.2 * C8 * g * Math.Pow(C4 * rate4, h) : 0.0;

                // mass1-4/useld1-4 still computed the same way as steam-continuous case for record-keeping
                (double m1, string id1) HoleMassNS(string timet, double rate, double usld)
                {
                    if (timet == "Continuous") return (rate * usld * 60, usld.ToString("#.##"));
                    return (massInput, "Instantaneous");
                }
                (mass1, useld1) = HoleMassNS(Timet1, rate1, usld1);
                (mass2, useld2) = HoleMassNS(Timet2, rate2, usld2);
                (mass3, useld3) = HoleMassNS(Timet3, rate3, usld3);
                (mass4, useld4) = HoleMassNS(Timet4, rate4, usld4);
            }

            // ---------- GFF hole-size weighting -> final totals ----------
            const double gff1 = 0.261, gff2 = 0.654, gff3 = 0.065, gff4 = 0.02;
            double CAnfnt1 = CAc1 * gff1, CAnfnt2 = CAc2 * gff2, CAnfnt3 = CAc3 * gff3, CAnfnt4 = CAc4 * gff4;
            double CAInjTotal = CAnfnt1 + CAnfnt2 + CAnfnt3 + CAnfnt4;
            string CmdCate = GetCategoryLetter(CAInjTotal);

            // ---------- Populate model ----------
            m.W1 = w1; m.W2 = w2; m.W3 = w3; m.W4 = w4;
            m.Time1 = Timet1; m.Time2 = Timet2; m.Time3 = Timet3; m.Time4 = Timet4;
            m.T1 = t1; m.T2 = t2; m.T3 = t3; m.T4 = t4;
            m.Rate1 = rate1; m.Rate2 = rate2; m.Rate3 = rate3; m.Rate4 = rate4;
            m.ID1 = useld1; m.ID2 = useld2; m.ID3 = useld3; m.ID4 = useld4;
            m.Mass1 = mass1; m.Mass2 = mass2; m.Mass3 = mass3; m.Mass4 = mass4;
            m.Efficiency = eneffn;
            m.CAInj1 = CAInj1; m.CAInj2 = CAInj2; m.CAInj3 = CAInj3; m.CAInj4 = CAInj4;
            m.CAInsInj1 = CAInjInst1; m.CAInsInj2 = CAInjInst2; m.CAInsInj3 = CAInjInst3; m.CAInsInj4 = CAInjInst4;
            m.Factic1 = factic1; m.Factic2 = factic2; m.Factic3 = factic3; m.Factic4 = factic4;
            m.CAbleInj1 = CAc1; m.CAbleInj2 = CAc2; m.CAbleInj3 = CAc3; m.CAbleInj4 = CAc4;
            m.CAInjFinal1 = CAnfnt1; m.CAInjFinal2 = CAnfnt2; m.CAInjFinal3 = CAnfnt3; m.CAInjFinal4 = CAnfnt4;
            m.CAInjTotal = SafeDecimal(CAInjTotal);
            m.CAInjCategory = CmdCate;
            m.Ptrans = ptrans;
            m.G = g; m.H = h;

            await SaveFullNonFlammableRecordAsync(conn, m);

            return m;
        }

        private async Task SaveFullNonFlammableRecordAsync(SqlConnection conn, COFNonFlammableModel m)
        {
            string checkQuery = "SELECT COUNT(1) FROM COF_NonFlammable WHERE ProcID=@ProcID AND EquID=@EquID AND CompID=@CompID AND Deleted=0";
            using var checkCmd = new SqlCommand(checkQuery, conn);
            checkCmd.Parameters.AddWithValue("@ProcID", m.ProcID ?? 0);
            checkCmd.Parameters.AddWithValue("@EquID", m.EquID ?? 0);
            checkCmd.Parameters.AddWithValue("@CompID", m.CompID ?? 0);
            int count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

            using var cmd = new SqlCommand { Connection = conn };

            if (count > 0)
            {
                cmd.CommandText = @"
UPDATE COF_NonFlammable SET
Fluid=@Fluid, Repfluid=@Repfluid, Type=@Type, OpPres=@OpPres, Optemp=@Optemp, P1=@P1, Mass=@Mass,
Detect=@Detect, Iso=@Iso, factMit=@factMit,
w1=@w1, w2=@w2, w3=@w3, w4=@w4,
Time1=@Time1, Time2=@Time2, Time3=@Time3, Time4=@Time4,
T1=@T1, T2=@T2, T3=@T3, T4=@T4,
rate1=@rate1, rate2=@rate2, rate3=@rate3, rate4=@rate4,
id1=@id1, id2=@id2, id3=@id3, id4=@id4,
mass1=@mass1, mass2=@mass2, mass3=@mass3, mass4=@mass4,
eneff=@eneff,
CAInj1=@CAInj1, CAInj2=@CAInj2, CAInj3=@CAInj3, CAInj4=@CAInj4,
CAInsInj1=@CAInsInj1, CAInsInj2=@CAInsInj2, CAInsInj3=@CAInsInj3, CAInsInj4=@CAInsInj4,
factic1=@factic1, factic2=@factic2, factic3=@factic3, factic4=@factic4,
CAbleInj1=@CAbleInj1, CAbleInj2=@CAbleInj2, CAbleInj3=@CAbleInj3, CAbleInj4=@CAbleInj4,
CAInjFinal1=@CAInjFinal1, CAInjFinal2=@CAInjFinal2, CAInjFinal3=@CAInjFinal3, CAInjFinal4=@CAInjFinal4,
CAInjTotal=@CAInjTotal, CAinjCate=@CAinjCate, ptrans=@ptrans, FluidStored=@FluidStored,
g=@g, h=@h, ModifiedBy=@ModifiedBy, ModifiedDate=@ModifiedDate
WHERE ProcID=@ProcID AND EquID=@EquID AND CompID=@CompID";
            }
            else
            {
                cmd.CommandText = @"
INSERT INTO COF_NonFlammable
(ProcID, EquID, CompID, Fluid, Repfluid, Type, OpPres, Optemp, P1, Mass, Detect, Iso, factMit,
 w1, w2, w3, w4, Time1, Time2, Time3, Time4, T1, T2, T3, T4, rate1, rate2, rate3, rate4,
 id1, id2, id3, id4, mass1, mass2, mass3, mass4, eneff,
 CAInj1, CAInj2, CAInj3, CAInj4, CAInsInj1, CAInsInj2, CAInsInj3, CAInsInj4,
 factic1, factic2, factic3, factic4, CAbleInj1, CAbleInj2, CAbleInj3, CAbleInj4,
 CAInjFinal1, CAInjFinal2, CAInjFinal3, CAInjFinal4, CAInjTotal, CAinjCate,
 ptrans, FluidStored, g, h, Deleted, CreatedBy, CreatedDate)
VALUES
(@ProcID, @EquID, @CompID, @Fluid, @Repfluid, @Type, @OpPres, @Optemp, @P1, @Mass, @Detect, @Iso, @factMit,
 @w1, @w2, @w3, @w4, @Time1, @Time2, @Time3, @Time4, @T1, @T2, @T3, @T4, @rate1, @rate2, @rate3, @rate4,
 @id1, @id2, @id3, @id4, @mass1, @mass2, @mass3, @mass4, @eneff,
 @CAInj1, @CAInj2, @CAInj3, @CAInj4, @CAInsInj1, @CAInsInj2, @CAInsInj3, @CAInsInj4,
 @factic1, @factic2, @factic3, @factic4, @CAbleInj1, @CAbleInj2, @CAbleInj3, @CAbleInj4,
 @CAInjFinal1, @CAInjFinal2, @CAInjFinal3, @CAInjFinal4, @CAInjTotal, @CAinjCate,
 @ptrans, @FluidStored, @g, @h, 0, @CreatedBy, @CreatedDate)";
            }

            cmd.Parameters.AddWithValue("@ProcID", m.ProcID ?? 0);
            cmd.Parameters.AddWithValue("@EquID", m.EquID ?? 0);
            cmd.Parameters.AddWithValue("@CompID", m.CompID ?? 0);
            cmd.Parameters.AddWithValue("@Fluid", m.Fluid ?? "");
            cmd.Parameters.AddWithValue("@Repfluid", m.Repfluid ?? "");
            cmd.Parameters.AddWithValue("@Type", m.FluidType ?? "");
            cmd.Parameters.AddWithValue("@OpPres", (m.Pressure ?? 0).ToString());
            cmd.Parameters.AddWithValue("@Optemp", (m.Temperature ?? 0).ToString());
            cmd.Parameters.AddWithValue("@P1", (m.P1 ?? 0).ToString());
            cmd.Parameters.AddWithValue("@Mass", (m.Inventory ?? 0).ToString());
            cmd.Parameters.AddWithValue("@Detect", m.Detection ?? "");
            cmd.Parameters.AddWithValue("@Iso", m.Isolation ?? "");
            cmd.Parameters.AddWithValue("@factMit", m.MitigationValue ?? "0");
            cmd.Parameters.AddWithValue("@w1", m.W1.ToString("#.##"));
            cmd.Parameters.AddWithValue("@w2", m.W2.ToString("#.##"));
            cmd.Parameters.AddWithValue("@w3", m.W3.ToString("#.##"));
            cmd.Parameters.AddWithValue("@w4", m.W4.ToString("#.##"));
            cmd.Parameters.AddWithValue("@Time1", m.Time1);
            cmd.Parameters.AddWithValue("@Time2", m.Time2);
            cmd.Parameters.AddWithValue("@Time3", m.Time3);
            cmd.Parameters.AddWithValue("@Time4", m.Time4);
            cmd.Parameters.AddWithValue("@T1", m.T1.ToString("#.##"));
            cmd.Parameters.AddWithValue("@T2", m.T2.ToString("#.##"));
            cmd.Parameters.AddWithValue("@T3", m.T3.ToString("#.##"));
            cmd.Parameters.AddWithValue("@T4", m.T4.ToString("#.##"));
            cmd.Parameters.AddWithValue("@rate1", m.Rate1.ToString("#.##"));
            cmd.Parameters.AddWithValue("@rate2", m.Rate2.ToString("#.##"));
            cmd.Parameters.AddWithValue("@rate3", m.Rate3.ToString("#.##"));
            cmd.Parameters.AddWithValue("@rate4", m.Rate4.ToString("#.##"));
            cmd.Parameters.AddWithValue("@id1", m.ID1);
            cmd.Parameters.AddWithValue("@id2", m.ID2);
            cmd.Parameters.AddWithValue("@id3", m.ID3);
            cmd.Parameters.AddWithValue("@id4", m.ID4);
            cmd.Parameters.AddWithValue("@mass1", m.Mass1.ToString("#.##"));
            cmd.Parameters.AddWithValue("@mass2", m.Mass2.ToString("#.##"));
            cmd.Parameters.AddWithValue("@mass3", m.Mass3.ToString("#.##"));
            cmd.Parameters.AddWithValue("@mass4", m.Mass4.ToString("#.##"));
            cmd.Parameters.AddWithValue("@eneff", m.Efficiency.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAInj1", m.CAInj1.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAInj2", m.CAInj2.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAInj3", m.CAInj3.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAInj4", m.CAInj4.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAInsInj1", m.CAInsInj1.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAInsInj2", m.CAInsInj2.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAInsInj3", m.CAInsInj3.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAInsInj4", m.CAInsInj4.ToString("#.##"));
            cmd.Parameters.AddWithValue("@factic1", m.Factic1.ToString("#.##"));
            cmd.Parameters.AddWithValue("@factic2", m.Factic2.ToString("#.##"));
            cmd.Parameters.AddWithValue("@factic3", m.Factic3.ToString("#.##"));
            cmd.Parameters.AddWithValue("@factic4", m.Factic4.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAbleInj1", m.CAbleInj1.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAbleInj2", m.CAbleInj2.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAbleInj3", m.CAbleInj3.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAbleInj4", m.CAbleInj4.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAInjFinal1", m.CAInjFinal1.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAInjFinal2", m.CAInjFinal2.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAInjFinal3", m.CAInjFinal3.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAInjFinal4", m.CAInjFinal4.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAInjTotal", m.CAInjTotal.ToString("#.##"));
            cmd.Parameters.AddWithValue("@CAinjCate", m.CAInjCategory ?? "");
            cmd.Parameters.AddWithValue("@ptrans", m.Ptrans.ToString());
            cmd.Parameters.AddWithValue("@FluidStored", m.Fluid ?? "");
            cmd.Parameters.AddWithValue("@g", m.G);
            cmd.Parameters.AddWithValue("@h", m.H);

            if (count > 0)
            {
                cmd.Parameters.AddWithValue("@ModifiedBy", m.UpdatedBy ?? "1");
                cmd.Parameters.AddWithValue("@ModifiedDate", DateTime.Now);
            }
            else
            {
                cmd.Parameters.AddWithValue("@CreatedBy", m.CreatedBy ?? "1");
                cmd.Parameters.AddWithValue("@CreatedDate", DateTime.Now);
            }

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<bool> DeleteNonFlammableAsync(decimal? procId, decimal? equId, decimal? compId)
        {
            string query = "DELETE FROM COF_NonFlammable WHERE ProcID = @ProcID AND EquID = @EquID AND CompID = @CompID";
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@ProcID", procId ?? 0);
            cmd.Parameters.AddWithValue("@EquID", equId ?? 0);
            cmd.Parameters.AddWithValue("@CompID", compId ?? 0);
            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        // =================================================================
        // Helpers
        // =================================================================

        private static double SafeDiv(double numerator, double denominator) => denominator == 0 ? 0 : numerator / denominator;
        private static double ToD(object val) => val == DBNull.Value ? 0 : Convert.ToDouble(val);
        private static decimal SafeDecimal(double val) => double.IsNaN(val) || double.IsInfinity(val) ? 0 : Convert.ToDecimal(val);

        private async Task<(double a, double b)> GetCoeffAsync(SqlConnection conn, string table, string fluidStoredType, string prefix, string repFluid)
        {
            string colA = $"{prefix}{fluidStoredType}A";
            string colB = $"{prefix}{fluidStoredType}B";
            string sql = $"SELECT {colA} AS a, {colB} AS b FROM {table} WHERE Fluid = @Fluid";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Fluid", repFluid ?? "");
            using var rd = await cmd.ExecuteReaderAsync();
            if (await rd.ReadAsync())
            {
                return (ToD(rd["a"]), ToD(rd["b"]));
            }
            return (0, 0);
        }

        private string GetCategoryLetter(double val)
        {
            if (val <= 100) return "A";
            if (val <= 1000) return "B";
            if (val <= 3000) return "C";
            if (val <= 10000) return "D";
            return "E";
        }
    }
}
