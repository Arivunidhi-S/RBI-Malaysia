using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using RBI_Malaysia.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace RBI_Malaysia.Services
{
    public class ThinningService
    {
        private readonly string _connectionString;

        public ThinningService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("connString")
                ?? throw new InvalidOperationException("Connection string 'connString' not found.");
        }

        // =================================================================
        // 1. CASCADING DROPDOWNS (same tables used across the whole app)
        // =================================================================

        public async Task<List<ProcessAreaModel>> GetProcessAreasAsync(string companyId)
        {
            var list = new List<ProcessAreaModel>();
            string query = "SELECT [ProcessAreaID], [processarea] FROM [Tbl_ProcessArea] WHERE deleted = 0 and CompanyID=@companyid ORDER BY [processareaid]";
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.Add("@companyid", SqlDbType.Decimal).Value = companyId;
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

        public async Task<List<EquipmentModel>> GetEquipmentsByProcessAsync(decimal processAreaId)
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

        public async Task<List<ComponentModel>> GetComponentsByEquipmentAsync(string equipmentId)
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

        // =================================================================
        // 2. EXISTING RECORD LOAD
        // =================================================================

        public async Task<ThinningModel?> GetThinningRecordAsync(decimal procId, decimal equId, decimal compId)
        {
            string query = "SELECT * FROM ThinningDamage WHERE ProcID = @ProcID AND EquID = @EquID AND CompID = @CompID AND Deleted = 0";
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@ProcID", procId);
            cmd.Parameters.AddWithValue("@EquID", equId);
            cmd.Parameters.AddWithValue("@CompID", compId);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            decimal? Dec(string col) => reader[col] == DBNull.Value ? null : Convert.ToDecimal(reader[col]);
            int? Int(string col) => reader[col] == DBNull.Value ? null : Convert.ToInt32(reader[col]);
            string? S(string col) => reader[col] == DBNull.Value ? null : reader[col].ToString();
            DateTime? Dt(string col) => reader[col] == DBNull.Value ? null : Convert.ToDateTime(reader[col]);

            return new ThinningModel
            {
                TDFId = reader.GetDecimal(reader.GetOrdinal("TDFId")),
                ProcID = procId,
                EquID = equId,
                CompID = compId,
                Clad = S("Clad"),
                Age = Int("age"),
                Art = Dec("Art"),
                Tdf = Dec("Tdf"),
                InspectCate = S("InspectCate"),
                Nofins = Int("nofins"),
                InspectDate = Dt("InspectDate"),
                ThinType = S("ThinType"),
                CompanyID = Dec("CompanyID"),
                YS = Dec("YS"),
                TS = Dec("TS"),
                S = Dec("S"),
                E = Dec("E"),
                NofinsB = Int("nofinsB"),
                NofinsC = Int("nofinsC"),
                NofinsD = Int("nofinsD"),
                Prp1_thin = Dec("Prp1_thin"),
                Prp2_thin = Dec("Prp2_thin"),
                Prp3_thin = Dec("Prp3_thin"),
                DS1 = Dec("DS1"),
                DS2 = Dec("DS2"),
                DS3 = Dec("DS3"),
                FIP = Dec("FIP"),
                FDL = Dec("FDL"),
                FWD = Dec("FWD"),
                FAM = Dec("FAM"),
                FSM = Dec("FSM"),
                FOM = Dec("FOM"),
                FSThin = Dec("FS_thin"),
                SRPThin = Dec("SRP_thin"),
            };
        }

        // =================================================================
        // 3. COMPONENT MATERIAL DATA (for Art calculation)
        // =================================================================

        public async Task<(double trd, double tmin, double crcm, double ca, string clad, double shortCr, double constThick)?> GetComponentMaterialAsync(decimal procId, decimal equId, decimal compId)
        {
            string query = "SELECT [MRT],[ReadVal],[CorrosionAllownce],[Clad],[uCR],[ShortCRrate],[ConstThickness] FROM [Tbl_EquipmentComponentDetails] " +
                            "WHERE [ProcessareaID]=@ProcID AND EqupID=@EquID AND [CompAutoID]=@CompID AND deleted=0";
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@ProcID", procId);
            cmd.Parameters.AddWithValue("@EquID", equId);
            cmd.Parameters.AddWithValue("@CompID", compId);
            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            double SafeD(object v) => double.TryParse(v?.ToString(), out var r) ? r : 0;

            double trd = SafeD(reader["ReadVal"]);
            double tmin = SafeD(reader["MRT"]);
            double crcm = SafeD(reader["uCR"]);
            double ca = SafeD(reader["CorrosionAllownce"]);
            string clad = reader["Clad"]?.ToString()?.Trim() ?? "";
            double shortCr = SafeD(reader["ShortCRrate"]);
            double constThick = SafeD(reader["ConstThickness"]);
            return (trd, tmin, crcm, ca, clad, shortCr, constThick);
        }

        // =================================================================
        // 4. CALCULATE + SAVE (single button)
        // =================================================================

        public async Task<ThinningModel> CalculateThinningAsync(ThinningModel m)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // ---------- 1. Fetch material data ----------
            var material = await GetComponentMaterialAsync(conn, m.ProcID ?? 0, m.EquID ?? 0, m.CompID ?? 0);
            if (material == null)
                throw new InvalidOperationException("Component material data not found (Tbl_EquipmentComponentDetails).");

            var (trd, tmin, crcm, ca, clad, shortCr, constThick) = material.Value;
            m.Clad = clad;

            double age = Convert.ToDouble(m.Age ?? 0);

            // ---------- 2. Art (per confirmed Excel formula — NOT the old ASP.Net formula) ----------
            // Art = (Cr,bm × (age_tk − age_rc)) / trdi
            // Cr,bm = ShortCRrate, trdi = ReadVal, tbm = ConstThickness (assumption — confirm if wrong)
            double crbm = shortCr;
            double tbm = constThick;
            double trdi = trd;
            double tcm = trdi - tbm; // cladding measured thickness
            double agerc = crcm == 0 ? 0 : tcm / crcm; // age_rc = tcm / Cr,cm (0 when no cladding corrosion)
            double art = crbm * (age - agerc) / SafeDiv1(trdi);
            art = Math.Round(art, 2);
            m.Art = Convert.ToDecimal(art);

            // ---------- 3. Flow stress & Strength Ratio Parameter ----------
            double ys = Convert.ToDouble(m.YS ?? 0);
            double ts = Convert.ToDouble(m.TS ?? 0);
            double sVal = Convert.ToDouble(m.S ?? 0);
            double fsThin = ((ys + ts) / 2.0) * 1.1;
            double srpThin = (sVal / SafeDiv1(fsThin)) * (tmin / SafeDiv1(trdi));
            m.FSThin = Convert.ToDecimal(fsThin);
            m.SRPThin = Convert.ToDecimal(srpThin);

            // ---------- 4. Bayesian I1/I2/I3 (prior × CoP² per Table 4.6, CoP hardcoded from the reference table) ----------
            // Table 4.6 (confirmed from the provided Excel) — fixed, does not vary by component
            const double CoP1A = 0.9, CoP1B = 0.7, CoP1C = 0.5, CoP1D = 0.4;
            const double CoP2A = 0.09, CoP2B = 0.2, CoP2C = 0.3, CoP2D = 0.33;
            const double CoP3A = 0.01, CoP3B = 0.1, CoP3C = 0.2, CoP3D = 0.27;

            double prp1 = Convert.ToDouble(m.Prp1_thin ?? 0);
            double prp2 = Convert.ToDouble(m.Prp2_thin ?? 0);
            double prp3 = Convert.ToDouble(m.Prp3_thin ?? 0);

            double i1 = prp1 * Math.Pow(CoP1A, 2) * Math.Pow(CoP1B, 2) * Math.Pow(CoP1C, 2) * Math.Pow(CoP1D, 2);
            double i2 = prp2 * Math.Pow(CoP2A, 2) * Math.Pow(CoP2B, 2) * Math.Pow(CoP2C, 2) * Math.Pow(CoP2D, 2);
            double i3 = prp3 * Math.Pow(CoP3A, 2) * Math.Pow(CoP3B, 2) * Math.Pow(CoP3C, 2) * Math.Pow(CoP3D, 2);

            // ---------- 5. Posterior weights ----------
            double iSum = SafeDiv1(i1 + i2 + i3);
            double w1 = i1 / iSum, w2 = i2 / iSum, w3 = i3 / iSum;

            // ---------- 6. Per-scenario Z-score (DS = 1, 2, 4 — fixed damage-rate multipliers) ----------
            const double DS1v = 1, DS2v = 2, DS3v = 4;
            const double covT = 0.2, covSf = 0.2, covP = 0.05;

            (double mean, double sd, double z) ZScore(double ds)
            {
                double mean = 1 - (ds * art) - srpThin;
                double sd = Math.Sqrt(Math.Pow(ds, 2) * Math.Pow(art, 2) * Math.Pow(covT, 2)
                                      + Math.Pow(1 - ds * art, 2) * (Math.Pow(covSf, 2) + Math.Pow(srpThin, 2) * Math.Pow(covP, 2)));
                double z = sd == 0 ? 0 : mean / sd;
                return (mean, sd, z);
            }
            var (mean1, sd1, z1) = ZScore(DS1v);
            var (mean2, sd2, z2) = ZScore(DS2v);
            var (mean3, sd3, z3) = ZScore(DS3v);

            // ---------- 7. Combine (weight × 0.8413 × −Z, summed) ÷ fixed constant, × Table 4.7 factors, floor 0.1 ----------
            const double Q = 0.8413;      // Φ at Z=1.0
            const double I84Constant = 0.000156; // confirmed fixed constant (per user)

            double combined = (w1 * Q * -z1) + (w2 * Q * -z2) + (w3 * Q * -z3);
            double dfRaw = combined / I84Constant;

            double fip = Convert.ToDouble(m.FIP ?? 1);
            double fdl = Convert.ToDouble(m.FDL ?? 1);
            double fwd = Convert.ToDouble(m.FWD ?? 1);
            double fam = Convert.ToDouble(m.FAM ?? 1);
            double fsm = Convert.ToDouble(m.FSM ?? 1);
            double fom = Convert.ToDouble(m.FOM ?? 1);

            double dfThin = dfRaw * fip * fdl * fwd * fam * fsm * fom;
            double finalTdf = Math.Max(dfThin, 0.1);

            m.Tdf = Convert.ToDecimal(finalTdf);
            m.DS1 = 1; m.DS2 = 2; m.DS3 = 4; // fixed scenario labels — always saved as-is

            return m; // Calculate only — no DB write. Call SaveThinningAsync separately to persist.
        }

        // =================================================================
        // 5. SAVE (separate button — persists whatever is currently in the model)
        // =================================================================

        public async Task<ThinningModel> SaveThinningAsync(ThinningModel m)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await SaveFullThinningRecordAsync(conn, m);
            return m;
        }

        private static double SafeDiv1(double v) => v == 0 ? 1 : v; // avoid divide-by-zero (old code divided by crcm directly)

        /// <summary>
        /// Reproduces the old Calculate_Click bracket chain + Interpolition() method exactly:
        /// finds the [minArt,maxArt) bracket the art value falls in (18 brackets from 0.00 to 0.65),
        /// and linearly interpolates the Tbl_InspectionEffective lookup between the two bracket ends.
        /// For art > 0.65, does a direct (non-interpolated) lookup at art = 0.65.
        /// </summary>
        private async Task<int> GetTdfAsync(SqlConnection conn, string ins, double art, int noins)
        {
            (double lo, double hi)[] brackets = new (double, double)[]
            {
                (0.00,0.04),(0.04,0.06),(0.06,0.08),(0.08,0.10),(0.10,0.12),(0.12,0.14),
                (0.14,0.16),(0.16,0.18),(0.18,0.20),(0.20,0.25),(0.25,0.30),(0.30,0.35),
                (0.35,0.40),(0.40,0.45),(0.45,0.50),(0.50,0.55),(0.55,0.60),(0.60,0.65),
            };

            for (int i = 0; i < brackets.Length; i++)
            {
                var (lo, hi) = brackets[i];
                bool isLastBracket = i == brackets.Length - 1;
                bool inBracket = isLastBracket ? (art >= lo && art <= hi) : (art >= lo && art < hi);
                if (inBracket)
                {
                    return await InterpolateAsync(conn, ins, lo, hi, art, noins);
                }
            }

            // art > 0.65 — direct lookup, no interpolation
            double val = await LookupInspectAsync(conn, ins, 0.65, noins);
            return Convert.ToInt32(val);
        }

        private async Task<double> LookupInspectAsync(SqlConnection conn, string ins, double art, int noins)
        {
            // "ins" (A/B/C/D/E) selects which column to read — validated against a fixed allow-list to prevent SQL injection
            string col = ins switch { "A" => "A", "B" => "B", "C" => "C", "D" => "D", "E" => "E", _ => "A" };
            string sql = $"SELECT {col} AS inspect FROM Tbl_InspectionEffective WHERE art = @art AND inspection = @noins";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@art", art);
            cmd.Parameters.AddWithValue("@noins", noins);
            using var rd = await cmd.ExecuteReaderAsync();
            if (await rd.ReadAsync() && rd["inspect"] != DBNull.Value)
                return Convert.ToDouble(rd["inspect"]);
            return 0;
        }

        private async Task<int> InterpolateAsync(SqlConnection conn, string ins, double minArt, double maxArt, double art, int noins)
        {
            double min = await LookupInspectAsync(conn, ins, minArt, noins);
            double max = await LookupInspectAsync(conn, ins, maxArt, noins);
            double cal = (art - minArt) * (max - min) / (maxArt - minArt);
            return Convert.ToInt32(cal + min);
        }

        private async Task<(double trd, double tmin, double crcm, double ca, string clad, double shortCr, double constThick)?> GetComponentMaterialAsync(SqlConnection conn, decimal procId, decimal equId, decimal compId)
        {
            string query = "SELECT [MRT],[ReadVal],[CorrosionAllownce],[Clad],[uCR],[ShortCRrate],[ConstThickness] FROM [Tbl_EquipmentComponentDetails] " +
                            "WHERE [ProcessareaID]=@ProcID AND EqupID=@EquID AND [CompAutoID]=@CompID AND deleted=0";
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@ProcID", procId);
            cmd.Parameters.AddWithValue("@EquID", equId);
            cmd.Parameters.AddWithValue("@CompID", compId);
            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            double SafeD(object v) => double.TryParse(v?.ToString(), out var r) ? r : 0;

            double trd = SafeD(reader["ReadVal"]);
            double tmin = SafeD(reader["MRT"]);
            double crcm = SafeD(reader["uCR"]);
            double ca = SafeD(reader["CorrosionAllownce"]);
            string clad = reader["Clad"]?.ToString()?.Trim() ?? "";
            double shortCr = SafeD(reader["ShortCRrate"]);
            double constThick = SafeD(reader["ConstThickness"]);
            return (trd, tmin, crcm, ca, clad, shortCr, constThick);
        }

        private async Task SaveFullThinningRecordAsync(SqlConnection conn, ThinningModel m)
        {
            string checkQuery = "SELECT COUNT(1) FROM ThinningDamage WHERE ProcID=@ProcID AND EquID=@EquID AND CompID=@CompID AND Deleted=0";
            using var checkCmd = new SqlCommand(checkQuery, conn);
            checkCmd.Parameters.AddWithValue("@ProcID", m.ProcID ?? 0);
            checkCmd.Parameters.AddWithValue("@EquID", m.EquID ?? 0);
            checkCmd.Parameters.AddWithValue("@CompID", m.CompID ?? 0);
            int count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

            using var cmd = new SqlCommand { Connection = conn };

            if (count > 0)
            {
                cmd.CommandText = @"
UPDATE ThinningDamage SET
Clad=@Clad, age=@age, Art=@Art, Tdf=@Tdf, InspectCate=@InspectCate, nofins=@nofins,
InspectDate=@InspectDate, ThinType=@ThinType, CompanyID=@CompanyID,
YS=@YS, TS=@TS, S=@S, E=@E, nofinsB=@nofinsB, nofinsC=@nofinsC, nofinsD=@nofinsD,
Prp1_thin=@Prp1_thin, Prp2_thin=@Prp2_thin, Prp3_thin=@Prp3_thin,
DS1=@DS1, DS2=@DS2, DS3=@DS3,
FIP=@FIP, FDL=@FDL, FWD=@FWD, FAM=@FAM, FSM=@FSM, FOM=@FOM,
FS_thin=@FS_thin, SRP_thin=@SRP_thin,
ModifiedBy=@ModifiedBy, ModifiedDate=@ModifiedDate
WHERE ProcID=@ProcID AND EquID=@EquID AND CompID=@CompID";
            }
            else
            {
                cmd.CommandText = @"
INSERT INTO ThinningDamage
(ProcID, EquID, CompID, Clad, age, Art, Tdf, InspectCate, nofins, InspectDate, ThinType, CompanyID,
 YS, TS, S, E, nofinsB, nofinsC, nofinsD, Prp1_thin, Prp2_thin, Prp3_thin, DS1, DS2, DS3,
 FIP, FDL, FWD, FAM, FSM, FOM, FS_thin, SRP_thin,
 Deleted, CreatedBy, CreatedDate)
VALUES
(@ProcID, @EquID, @CompID, @Clad, @age, @Art, @Tdf, @InspectCate, @nofins, @InspectDate, @ThinType, @CompanyID,
 @YS, @TS, @S, @E, @nofinsB, @nofinsC, @nofinsD, @Prp1_thin, @Prp2_thin, @Prp3_thin, @DS1, @DS2, @DS3,
 @FIP, @FDL, @FWD, @FAM, @FSM, @FOM, @FS_thin, @SRP_thin,
 0, @CreatedBy, @CreatedDate)";
            }

            cmd.Parameters.AddWithValue("@ProcID", m.ProcID ?? 0);
            cmd.Parameters.AddWithValue("@EquID", m.EquID ?? 0);
            cmd.Parameters.AddWithValue("@CompID", m.CompID ?? 0);
            cmd.Parameters.AddWithValue("@Clad", (object?)m.Clad ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@age", (object?)m.Age ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Art", (object?)m.Art ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Tdf", (object?)m.Tdf ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@InspectCate", (object?)m.InspectCate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@nofins", (object?)m.Nofins ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@InspectDate", (object?)m.InspectDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ThinType", (object?)m.ThinType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CompanyID", (object?)m.CompanyID ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@YS", (object?)m.YS ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@TS", (object?)m.TS ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@S", (object?)m.S ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@E", (object?)m.E ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@nofinsB", (object?)m.NofinsB ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@nofinsC", (object?)m.NofinsC ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@nofinsD", (object?)m.NofinsD ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Prp1_thin", (object?)m.Prp1_thin ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Prp2_thin", (object?)m.Prp2_thin ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Prp3_thin", (object?)m.Prp3_thin ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DS1", (object?)m.DS1 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DS2", (object?)m.DS2 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DS3", (object?)m.DS3 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FIP", (object?)m.FIP ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FDL", (object?)m.FDL ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FWD", (object?)m.FWD ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FAM", (object?)m.FAM ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FSM", (object?)m.FSM ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FOM", (object?)m.FOM ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FS_thin", (object?)m.FSThin ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SRP_thin", (object?)m.SRPThin ?? DBNull.Value);

            if (count > 0)
            {
                cmd.Parameters.AddWithValue("@ModifiedBy", (object?)m.UpdatedBy ?? "1");
                cmd.Parameters.AddWithValue("@ModifiedDate", DateTime.Now);
            }
            else
            {
                cmd.Parameters.AddWithValue("@CreatedBy", (object?)m.CreatedBy ?? "1");
                cmd.Parameters.AddWithValue("@CreatedDate", DateTime.Now);
            }

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<bool> DeleteThinningAsync(decimal? procId, decimal? equId, decimal? compId)
        {
            string query = "DELETE FROM ThinningDamage WHERE ProcID = @ProcID AND EquID = @EquID AND CompID = @CompID";
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@ProcID", procId ?? 0);
            cmd.Parameters.AddWithValue("@EquID", equId ?? 0);
            cmd.Parameters.AddWithValue("@CompID", compId ?? 0);
            await conn.OpenAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
    }
}
