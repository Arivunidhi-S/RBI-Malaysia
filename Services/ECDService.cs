using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using RBI_Malaysia.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace RBI_Malaysia.Services
{
    public class ECDService
    {
        private readonly string _connectionString;

        public ECDService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("connString")
                ?? throw new InvalidOperationException("Connection string 'connString' not found.");
        }

        // =================================================================
        // 1. CASCADING DROPDOWNS
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

        public async Task<ECDModel?> GetECDRecordAsync(decimal procId, decimal equId, decimal compId)
        {
            string query = "SELECT * FROM ECD WHERE ProcID = @ProcID AND EquID = @EquID AND CompID = @CompID AND Deleted = 0";
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

            return new ECDModel
            {
                ECDID = reader.GetDecimal(reader.GetOrdinal("ECDID")),
                ProcID = procId,
                EquID = equId,
                CompID = compId,
                Agtk = Int("Agtk"),
                InsEff = S("InsEff"),
                Nofins = Int("nofins"),
                InspectDate = Dt("InspectDate"),
                CmpInstalDate = Dt("cmpdt"),
                CalcDate = Dt("caldt"),
                CoatQualCode = S("coatqual"), // stored directly as code here (no text-label mapping in this module)
                AgeCoat = Dec("ECDagcoat"),
                Age = Dec("ECDage"),
                Cr = Dec("cr"),
                ECDart = Dec("ECDart"),
                ECDDf = Dec("ECDDf"),
                Le = Dec("Le"),
                TRde = Dec("t_rde"),
                AgeTke = Dec("age_tke"),
                Cage = Dec("Cage"),
                CoatAdj = Dec("CoatAdj"),
                YS = Dec("YS"),
                TS = Dec("TS"),
                S = Dec("S"),
                E = Dec("E"),
                Prp1_ext = Dec("Prp1_ext"),
                Prp2_ext = Dec("Prp2_ext"),
                Prp3_ext = Dec("Prp3_ext"),
                DS1 = Dec("DS1"),
                DS2 = Dec("DS2"),
                DS3 = Dec("DS3"),
                FIP = Dec("FIP"),
                FDL = Dec("FDL"),
                FWD = Dec("FWD"),
                FAM = Dec("FAM"),
                FSM = Dec("FSM"),
                FOM = Dec("FOM"),
                FSExtcorr = Dec("FS_extcorr"),
                SRPExtcorr = Dec("SRP_extcorr"),
                CompanyID = Dec("CompanyID"),
            };
        }

        // =================================================================
        // 3. COMPONENT DATA
        // =================================================================

        public async Task<(double tmin, double trd)?> GetComponentDataAsync(decimal procId, decimal equId, decimal compId)
        {
            string query = "SELECT [MRT],[ReadVal] FROM [Tbl_EquipmentComponentDetails] " +
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
            return (SafeD(reader["MRT"]), SafeD(reader["ReadVal"]));
        }

        // =================================================================
        // 4. AUTO-CALC HELPERS (STEP 5 - STEP 9)
        // =================================================================

        /// <summary>STEP 5 — t_rde = t - Le.</summary>
        public decimal ComputeTRde(double trd, decimal le) => Convert.ToDecimal(trd) - le;

        /// <summary>STEP 6 — age_coat = CalcDate - CmpInstalDate, in whole years.</summary>
        public decimal ComputeAgeCoat(DateTime cmpInstalDate, DateTime calcDate)
        {
            int days = (int)(calcDate - cmpInstalDate).TotalDays;
            return Math.Max(0, days) / 365m;
        }

        /// <summary>STEP 7 — expected coating life from quality selection.</summary>
        public decimal ComputeCage(string coatQualCode) => coatQualCode switch
        {
            "1" => 0m,   // No coating / poor coating
            "2" => 5m,   // Lower quality
            "3" => 15m,  // High quality
            _ => 0m,
        };

        /// <summary>
        /// STEP 8 — coating adjustment. If age_tke >= age_coat: Coat_adj = min(Cage, age_coat).
        /// If age_tke &lt; age_coat: 0 if coating already failed by the time age_tke was established,
        /// else min(Cage,age_coat) - min(Cage, age_coat-age_tke).
        /// </summary>
        public decimal ComputeCoatAdj(decimal ageTke, decimal ageCoat, decimal cage, bool coatingFailed)
        {
            if (ageTke >= ageCoat)
                return Math.Min(cage, ageCoat);

            if (coatingFailed)
                return 0m;

            return Math.Min(cage, ageCoat) - Math.Min(cage, ageCoat - ageTke);
        }

        /// <summary>STEP 9 — final age = age_tke - Coat_adj.</summary>
        public decimal ComputeAge(decimal ageTke, decimal coatAdj) => ageTke - coatAdj;

        // =================================================================
        // 5. CALCULATE (STEP 11 Art, then STEP 12-18 Bayesian DF — no DB write)
        // =================================================================

        public async Task<ECDModel> CalculateECDAsync(ECDModel m)
        {
            var data = await GetComponentDataAsync(m.ProcID ?? 0, m.EquID ?? 0, m.CompID ?? 0);
            if (data == null)
                throw new InvalidOperationException("Component data not found (Tbl_EquipmentComponentDetails).");

            var (tmin, trd) = data.Value;

            // STEP 5
            decimal le = m.Le ?? 0;
            decimal tRde = ComputeTRde(trd, le);
            m.TRde = tRde;

            // STEP 6-9
            if (m.CmpInstalDate != null && m.CalcDate != null)
            {
                m.AgeCoat = ComputeAgeCoat(m.CmpInstalDate.Value, m.CalcDate.Value);
            }
            if (!string.IsNullOrEmpty(m.CoatQualCode))
            {
                m.Cage = ComputeCage(m.CoatQualCode);
            }
            decimal ageTke = m.AgeTke ?? 0;
            decimal ageCoat = m.AgeCoat ?? 0;
            decimal cage = m.Cage ?? 0;
            decimal coatAdj = ComputeCoatAdj(ageTke, ageCoat, cage, m.CoatingFailed);
            m.CoatAdj = coatAdj;
            decimal age = ComputeAge(ageTke, coatAdj);
            m.Age = age;

            // STEP 11 — Art = Cr × age / t_rde   (STEP 2-4 skipped per instruction — Cr is a direct manual input)
            double cr = Convert.ToDouble(m.Cr ?? 0);
            double artRaw = tRde == 0 ? 0 : (cr * (double)age) / (double)tRde;
            double art = Math.Max(artRaw, 0);
            m.ECDart = Convert.ToDecimal(art);

            // STEP 12 — Flow stress (identical formula/constants to Thinning STEP 7)
            double ys = Convert.ToDouble(m.YS ?? 0);
            double ts = Convert.ToDouble(m.TS ?? 0);
            double sVal = Convert.ToDouble(m.S ?? 0);
            double fsExt = ((ys + ts) / 2.0) * 1.1;
            m.FSExtcorr = Convert.ToDecimal(fsExt);

            // STEP 13 — Strength Ratio Parameter (identical formula to Thinning STEP 8)
            double trdi = trd; // current thickness reading, same role as Thinning's trdi
            double srpExt = (sVal / SafeDiv1(fsExt)) * (tmin / SafeDiv1(trdi));
            m.SRPExtcorr = Convert.ToDecimal(srpExt);

            // STEP 15-18 — Bayesian I1/I2/I3, weights, Z-scores, combine (identical to Thinning STEP 8-13)
            const double CoP1A = 0.9, CoP1B = 0.7, CoP1C = 0.5, CoP1D = 0.4;
            const double CoP2A = 0.09, CoP2B = 0.2, CoP2C = 0.3, CoP2D = 0.33;
            const double CoP3A = 0.01, CoP3B = 0.1, CoP3C = 0.2, CoP3D = 0.27;

            double prp1 = Convert.ToDouble(m.Prp1_ext ?? 0);
            double prp2 = Convert.ToDouble(m.Prp2_ext ?? 0);
            double prp3 = Convert.ToDouble(m.Prp3_ext ?? 0);

            double i1 = prp1 * Math.Pow(CoP1A, 2) * Math.Pow(CoP1B, 2) * Math.Pow(CoP1C, 2) * Math.Pow(CoP1D, 2);
            double i2 = prp2 * Math.Pow(CoP2A, 2) * Math.Pow(CoP2B, 2) * Math.Pow(CoP2C, 2) * Math.Pow(CoP2D, 2);
            double i3 = prp3 * Math.Pow(CoP3A, 2) * Math.Pow(CoP3B, 2) * Math.Pow(CoP3C, 2) * Math.Pow(CoP3D, 2);

            double iSum = SafeDiv1(i1 + i2 + i3);
            double w1 = i1 / iSum, w2 = i2 / iSum, w3 = i3 / iSum;

            const double DS1v = 1, DS2v = 2, DS3v = 4;
            const double covT = 0.2, covSf = 0.2, covP = 0.05;

            (double mean, double sd, double z) ZScore(double ds)
            {
                double mean = 1 - (ds * art) - srpExt;
                double sd = Math.Sqrt(Math.Pow(ds, 2) * Math.Pow(art, 2) * Math.Pow(covT, 2)
                                      + Math.Pow(1 - ds * art, 2) * (Math.Pow(covSf, 2) + Math.Pow(srpExt, 2) * Math.Pow(covP, 2)));
                double z = sd == 0 ? 0 : mean / sd;
                return (mean, sd, z);
            }
            var (_, _, z1) = ZScore(DS1v);
            var (_, _, z2) = ZScore(DS2v);
            var (_, _, z3) = ZScore(DS3v);

            const double Q = 0.8413;
            const double I84Constant = 0.000156;

            double combined = (w1 * Q * -z1) + (w2 * Q * -z2) + (w3 * Q * -z3);
            double dfRaw = combined / I84Constant;

            double fip = Convert.ToDouble(m.FIP ?? 1);
            double fdl = Convert.ToDouble(m.FDL ?? 1);
            double fwd = Convert.ToDouble(m.FWD ?? 1);
            double fam = Convert.ToDouble(m.FAM ?? 1);
            double fsm = Convert.ToDouble(m.FSM ?? 1);
            double fom = Convert.ToDouble(m.FOM ?? 1);

            double dfExt = dfRaw * fip * fdl * fwd * fam * fsm * fom;
            double finalDf = Math.Max(dfExt, 0.1);

            m.ECDDf = Convert.ToDecimal(finalDf);
            m.DS1 = 1; m.DS2 = 2; m.DS3 = 4;

            return m;
        }

        private static double SafeDiv1(double v) => v == 0 ? 1 : v;

        // =================================================================
        // 6. SAVE (separate — persists whatever is currently in the model)
        // =================================================================

        public async Task<ECDModel> SaveECDAsync(ECDModel m)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            string checkQuery = "SELECT COUNT(1) FROM ECD WHERE ProcID=@ProcID AND EquID=@EquID AND CompID=@CompID AND Deleted=0";
            using var checkCmd = new SqlCommand(checkQuery, conn);
            checkCmd.Parameters.AddWithValue("@ProcID", m.ProcID ?? 0);
            checkCmd.Parameters.AddWithValue("@EquID", m.EquID ?? 0);
            checkCmd.Parameters.AddWithValue("@CompID", m.CompID ?? 0);
            int count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

            using var cmd = new SqlCommand { Connection = conn };

            if (count > 0)
            {
                cmd.CommandText = @"
UPDATE ECD SET
Agtk=@Agtk, InsEff=@InsEff, cmpdt=@cmpdt, caldt=@caldt, nofins=@nofins, InspectDate=@InspectDate,
coatqual=@coatqual, ECDagcoat=@ECDagcoat, ECDage=@ECDage, cr=@cr, ECDart=@ECDart, ECDDf=@ECDDf,
Le=@Le, t_rde=@t_rde, age_tke=@age_tke, Cage=@Cage, CoatAdj=@CoatAdj,
YS=@YS, TS=@TS, S=@S, E=@E,
Prp1_ext=@Prp1_ext, Prp2_ext=@Prp2_ext, Prp3_ext=@Prp3_ext, DS1=@DS1, DS2=@DS2, DS3=@DS3,
FIP=@FIP, FDL=@FDL, FWD=@FWD, FAM=@FAM, FSM=@FSM, FOM=@FOM,
FS_extcorr=@FS_extcorr, SRP_extcorr=@SRP_extcorr, CompanyID=@CompanyID,
ModifiedBy=@ModifiedBy, ModifiedDate=@ModifiedDate
WHERE ProcID=@ProcID AND EquID=@EquID AND CompID=@CompID";
            }
            else
            {
                cmd.CommandText = @"
INSERT INTO ECD
(ProcID, EquID, CompID, Agtk, InsEff, cmpdt, caldt, nofins, InspectDate, coatqual, ECDagcoat, ECDage,
 cr, ECDart, ECDDf, Le, t_rde, age_tke, Cage, CoatAdj, YS, TS, S, E,
 Prp1_ext, Prp2_ext, Prp3_ext, DS1, DS2, DS3, FIP, FDL, FWD, FAM, FSM, FOM,
 FS_extcorr, SRP_extcorr, CompanyID, Deleted, CreatedBy, CreatedDate)
VALUES
(@ProcID, @EquID, @CompID, @Agtk, @InsEff, @cmpdt, @caldt, @nofins, @InspectDate, @coatqual, @ECDagcoat, @ECDage,
 @cr, @ECDart, @ECDDf, @Le, @t_rde, @age_tke, @Cage, @CoatAdj, @YS, @TS, @S, @E,
 @Prp1_ext, @Prp2_ext, @Prp3_ext, @DS1, @DS2, @DS3, @FIP, @FDL, @FWD, @FAM, @FSM, @FOM,
 @FS_extcorr, @SRP_extcorr, @CompanyID, 0, @CreatedBy, @CreatedDate)";
            }

            cmd.Parameters.AddWithValue("@ProcID", m.ProcID ?? 0);
            cmd.Parameters.AddWithValue("@EquID", m.EquID ?? 0);
            cmd.Parameters.AddWithValue("@CompID", m.CompID ?? 0);
            cmd.Parameters.AddWithValue("@Agtk", (object?)m.Agtk ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@InsEff", (object?)m.InsEff ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@cmpdt", (object?)m.CmpInstalDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@caldt", (object?)m.CalcDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@nofins", (object?)m.Nofins ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@InspectDate", (object?)m.InspectDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@coatqual", (object?)m.CoatQualCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ECDagcoat", (object?)m.AgeCoat ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ECDage", (object?)m.Age ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@cr", (object?)m.Cr ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ECDart", (object?)m.ECDart ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ECDDf", (object?)m.ECDDf ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Le", (object?)m.Le ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@t_rde", (object?)m.TRde ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@age_tke", (object?)m.AgeTke ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Cage", (object?)m.Cage ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CoatAdj", (object?)m.CoatAdj ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@YS", (object?)m.YS ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@TS", (object?)m.TS ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@S", (object?)m.S ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@E", (object?)m.E ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Prp1_ext", (object?)m.Prp1_ext ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Prp2_ext", (object?)m.Prp2_ext ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Prp3_ext", (object?)m.Prp3_ext ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DS1", (object?)m.DS1 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DS2", (object?)m.DS2 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DS3", (object?)m.DS3 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FIP", (object?)m.FIP ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FDL", (object?)m.FDL ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FWD", (object?)m.FWD ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FAM", (object?)m.FAM ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FSM", (object?)m.FSM ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FOM", (object?)m.FOM ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FS_extcorr", (object?)m.FSExtcorr ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SRP_extcorr", (object?)m.SRPExtcorr ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CompanyID", (object?)m.CompanyID ?? DBNull.Value);

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
            return m;
        }

        public async Task<bool> DeleteECDAsync(decimal? procId, decimal? equId, decimal? compId)
        {
            string query = "DELETE FROM ECD WHERE ProcID = @ProcID AND EquID = @EquID AND CompID = @CompID";
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
