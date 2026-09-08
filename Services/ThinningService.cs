using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using RBI_Malaysia.Models;

namespace RBI_Malaysia.Services
{
    /// <summary>
    /// Full replication of the "Thinning Cracking" module from POF.aspx.cs:
    ///   - Calculate_Click          -> CalculateAsync
    ///   - Interpolition()          -> InterpolateAsync
    ///   - btnThinningSubmit_Click  -> LoadAsync
    ///   - btn_ThinningSave_Click   -> SaveAsync / CalculateAndSaveAsync
    ///   - InspectionPlan()         -> UpsertInspectionPlanAsync
    ///   - btnThiningDelete_Click   -> DeleteAsync
    ///   - Agecal()                 -> CalculateAgeAsync
    ///   - Inspectcategory()        -> InspectCategoryLabel
    ///
    /// NOTE on assumptions (please confirm / correct against your live schema):
    ///   1. Tbl_InspectionEffective is assumed to have columns: Art (numeric),
    ///      Inspection (int), and one column per effectiveness grade: A, B, C, D, E.
    ///      The original code builds "SELECT {grade} AS inspect ..." by string
    ///      concatenation - that's a SQL injection vector (column names can't be
    ///      parameterized), so this version whitelists InspectCate to A/B/C/D/E only.
    ///   2. InspectionPlan table is assumed to have columns:
    ///      ProcID, EquID, CompID, DamageFact, InspectEffec, InspectDate,
    ///      CompanyID, CreatedBy/ModifiedBy, CreatedDate/ModifiedDate, Deleted.
    ///      Adjust UpsertInspectionPlanAsync's column list if yours differs.
    /// </summary>
    public class ThinningService
    {
        private readonly string _connectionString;
        private static readonly HashSet<string> ValidGrades = new() { "A", "B", "C", "D", "E" };

        // Art brackets exactly as in the original cascading if/else in Calculate_Click
        private static readonly (double Min, double Max)[] ArtBrackets = new (double, double)[]
        {
            (0.00, 0.04), (0.04, 0.06), (0.06, 0.08), (0.08, 0.10),
            (0.10, 0.12), (0.12, 0.14), (0.14, 0.16), (0.16, 0.18),
            (0.18, 0.20), (0.20, 0.25), (0.25, 0.30), (0.30, 0.35),
            (0.35, 0.40), (0.40, 0.45), (0.45, 0.50), (0.50, 0.55),
            (0.55, 0.60), (0.60, 0.65),
        };

        public ThinningService(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("connString")
                ?? throw new InvalidOperationException("Connection string 'connString' not configured.");
        }

        // ---------------------------------------------------------------
        // Component lookups
        // ---------------------------------------------------------------

        public async Task<ComponentThinningInfo?> GetComponentDetailsAsync(int procId, int equId, int compId)
        {
            const string sql = @"SELECT [MRT],[ReadVal],[CorrosionAllownce],[Clad],[uCR]
                                  FROM [Tbl_EquipmentComponentDetails]
                                  WHERE [ProcessareaID] = @ProcID AND [EqupID] = @EquID
                                    AND [CompAutoID] = @CompID AND [deleted] = 0";

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ProcID", procId);
            cmd.Parameters.AddWithValue("@EquID", equId);
            cmd.Parameters.AddWithValue("@CompID", compId);

            await using var rd = await cmd.ExecuteReaderAsync();
            if (!await rd.ReadAsync())
                return null;

            return new ComponentThinningInfo
            {
                MRT = ToDouble(rd["MRT"]),
                ReadVal = ToDouble(rd["ReadVal"]),
                CorrosionAllowance = ToDouble(rd["CorrosionAllownce"]),
                UCR = ToDouble(rd["uCR"]),
                Clad = rd["Clad"]?.ToString()?.Trim() ?? string.Empty
            };
        }

        /// <summary>Populates CladOptions when the component's own Clad flag is Yes/Clad.</summary>
        public async Task<List<CladOption>> GetCladComponentListAsync()
        {
            const string sql = @"SELECT [CompAutoID],[CompName]
                                  FROM [Tbl_EquipmentComponentDetails]
                                  WHERE [Clad] = 'Clad' AND [deleted] = 0";

            var list = new List<CladOption>();
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                list.Add(new CladOption
                {
                    CompAutoID = Convert.ToInt32(rd["CompAutoID"]),
                    CompName = rd["CompName"]?.ToString() ?? string.Empty
                });
            }
            return list;
        }

        /// <summary>Equivalent of Agecal(): current inspection year minus the equipment's installed year.</summary>
        public async Task<int> CalculateAgeAsync(int currentYear, int procId, int equId, int companyId)
        {
            const string sql = @"SELECT [yearinstalled]
                                  FROM [Tbl_EquipmentAsset]
                                  WHERE [deleted] = 0 AND [ProcessareaID] = @ProcID
                                    AND [EquAutoID] = @EquID AND [CompanyID] = @CompanyID";

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ProcID", procId);
            cmd.Parameters.AddWithValue("@EquID", equId);
            cmd.Parameters.AddWithValue("@CompanyID", companyId);

            var result = await cmd.ExecuteScalarAsync();
            int installedYear = result is null or DBNull ? 0 : Convert.ToInt32(result);
            return currentYear - installedYear;
        }

        // ---------------------------------------------------------------
        // Calculate (Art + Tdf) - no DB writes
        // ---------------------------------------------------------------

        public async Task CalculateAsync(ThinningDamageModel model)
        {
            if (model.ProcID is null || model.EquID is null || model.CompID is null)
                throw new InvalidOperationException("Process Area / Equipment / Component must be selected.");
            if (model.Age is null)
                throw new InvalidOperationException("Age is required.");

            var comp = await GetComponentDetailsAsync(model.ProcID.Value, model.EquID.Value, model.CompID.Value);
            if (comp is null)
                throw new InvalidOperationException("Component details not found for the selected Process Area / Equipment / Component.");

            model.ComponentClad = comp.Clad;
            model.CladOptions = model.CladDropdownEnabled
                ? await GetCladComponentListAsync()
                : new List<CladOption>();

            double art = ComputeArt(comp, model.Age.Value);
            model.Art = Math.Round((decimal)art, 2);

            if (string.IsNullOrWhiteSpace(model.InspectCate) || model.NoOfIns is null)
                throw new InvalidOperationException("Inspection Effectiveness and No. of Inspections are required.");

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            int tdf = await ComputeTdfAsync(conn, model.InspectCate, art, model.NoOfIns.Value);
            model.Tdf = tdf;
        }

        /// <summary>
        /// Replicates the Clad/Non-Clad Art formula from Calculate_Click.
        /// NOTE: this is a byte-for-byte port of the original formula, including its
        /// existing quirks (e.g. the clad branch's "t" and "agerc" usage). Flag if you
        /// want the underlying API 581 thinning-rate formula corrected rather than ported as-is.
        /// </summary>
        private static double ComputeArt(ComponentThinningInfo comp, double age)
        {
            double trd = comp.ReadVal;
            double tmin = comp.MRT;
            double crcm = comp.UCR;
            double ca = comp.CorrosionAllowance;
            double art;

            if (comp.Clad == "Clad" || comp.Clad == "Yes")
            {
                double t = 0; // preserved from the original (t is never assigned before use there either)
                double agerc = Math.Max((trd - t) / crcm, 0);
                double cal2 = 1 - (trd - (crcm * agerc) - (crcm * (age - agerc)) / (tmin + ca));
                art = Math.Max(cal2, 0);
            }
            else
            {
                double cal = 1 - (trd - (crcm * age) / (tmin + ca));
                art = Math.Max(cal, 0);
            }

            return art;
        }

        /// <summary>Replicates the cascading if/else bracket selection + Interpolition() call.</summary>
        private async Task<int> ComputeTdfAsync(SqlConnection conn, string inspectCate, double artVal, int noIns)
        {
            foreach (var (min, max) in ArtBrackets)
            {
                bool inBracket = max < 0.65
                    ? artVal >= min && artVal < max
                    : artVal >= min && artVal <= max; // last bracket (0.60-0.65) is inclusive, matching original
                if (inBracket)
                    return await InterpolateAsync(conn, inspectCate, min, max, artVal, noIns);
            }

            // artVal >= 0.65 -> use the value at Art = 0.65 directly (original's fallback branch)
            double atMax = await LookupInspectValueAsync(conn, inspectCate, 0.65, noIns);
            return Convert.ToInt32(atMax);
        }

        private async Task<int> InterpolateAsync(SqlConnection conn, string inspectCate, double minArt, double maxArt, double art, int noIns)
        {
            double min = await LookupInspectValueAsync(conn, inspectCate, minArt, noIns);
            double max = await LookupInspectValueAsync(conn, inspectCate, maxArt, noIns);
            double cal = (art - minArt) * (max - min) / (maxArt - minArt);
            return Convert.ToInt32(cal + min);
        }

        private async Task<double> LookupInspectValueAsync(SqlConnection conn, string inspectCate, double art, int noIns)
        {
            if (!ValidGrades.Contains(inspectCate))
                throw new ArgumentException($"Invalid inspection effectiveness grade '{inspectCate}'.");

            // Column name comes from a whitelist above, so this is safe despite the interpolation.
            string sql = $@"SELECT [{inspectCate}] AS inspect
                             FROM [Tbl_InspectionEffective]
                             WHERE [Art] = @Art AND [Inspection] = @NoIns";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Art", art);
            cmd.Parameters.AddWithValue("@NoIns", noIns);

            var result = await cmd.ExecuteScalarAsync();
            return result is null or DBNull ? 0 : Convert.ToDouble(result);
        }

        // ---------------------------------------------------------------
        // Load / Save / Delete
        // ---------------------------------------------------------------

        public async Task<ThinningDamageModel?> LoadAsync(int procId, int equId, int compId, int companyId)
        {
            const string sql = @"SELECT TOP 1 * FROM [ThinningDamage]
                                  WHERE [Deleted] = 0 AND [ProcID] = @ProcID AND [EquID] = @EquID
                                    AND [CompID] = @CompID AND [CompanyID] = @CompanyID";

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ProcID", procId);
            cmd.Parameters.AddWithValue("@EquID", equId);
            cmd.Parameters.AddWithValue("@CompID", compId);
            cmd.Parameters.AddWithValue("@CompanyID", companyId);

            await using var rd = await cmd.ExecuteReaderAsync();
            if (!await rd.ReadAsync())
                return null;

            var model = new ThinningDamageModel
            {
                TDFId = ToNullableLong(rd["TDFId"]),
                ProcID = ToNullableInt(rd["ProcID"]),
                EquID = ToNullableInt(rd["EquID"]),
                CompID = ToNullableInt(rd["CompID"]),
                Clad = rd["Clad"] as string,
                Age = ToNullableInt(rd["age"]),
                Art = ToNullableDecimal(rd["Art"]),
                Tdf = ToNullableDecimal(rd["Tdf"]),
                InspectCate = rd["InspectCate"] as string,
                NoOfIns = ToNullableInt(rd["nofins"]),
                InspectDate = ToNullableDateTime(rd["InspectDate"]),
                ThinType = rd["ThinType"] as string,
                CompanyID = ToNullableInt(rd["CompanyID"]),
                CreatedBy = ToNullableInt(rd["CreatedBy"]),
                CreatedDate = ToNullableDateTime(rd["CreatedDate"]),
                ModifiedBy = ToNullableInt(rd["ModifiedBy"]),
                ModifiedDate = ToNullableDateTime(rd["ModifiedDate"]),
                Rowversions = ToNullableInt(rd["Rowversions"]),
                Deleted = rd["Deleted"] as bool?,
                YS = ToNullableDecimal(rd["YS"]),
                TS = ToNullableDecimal(rd["TS"]),
                S = ToNullableDecimal(rd["S"]),
                E = ToNullableDecimal(rd["E"]),
                NoFinsB = ToNullableInt(rd["nofinsB"]),
                NoFinsC = ToNullableInt(rd["nofinsC"]),
                NoFinsD = ToNullableInt(rd["nofinsD"]),
                Prp1Thin = ToNullableDecimal(rd["Prp1_thin"]),
                Prp2Thin = ToNullableDecimal(rd["Prp2_thin"]),
                Prp3Thin = ToNullableDecimal(rd["Prp3_thin"]),
                DS1 = ToNullableDecimal(rd["DS1"]),
                DS2 = ToNullableDecimal(rd["DS2"]),
                DS3 = ToNullableDecimal(rd["DS3"]),
            };

            var comp = await GetComponentDetailsAsync(procId, equId, compId);
            if (comp is not null)
            {
                model.ComponentClad = comp.Clad;
                model.CladOptions = model.CladDropdownEnabled
                    ? await GetCladComponentListAsync()
                    : new List<CladOption>();
            }

            return model;
        }

        /// <summary>
        /// Unified Calculate + Save, mirroring the CalculateAndSaveFlammableAsync pattern
        /// used for COF: computes Art/Tdf, then INSERTs or UPDATEs ThinningDamage and
        /// upserts InspectionPlan in one call, exactly like the original's
        /// Calculate_Click followed immediately by btn_ThinningSave_Click.
        /// </summary>
        public async Task<ThinningDamageModel> CalculateAndSaveAsync(ThinningDamageModel model, int companyId, int userId)
        {
            if (model.ProcID is null || model.EquID is null || model.CompID is null)
                throw new InvalidOperationException("Process Area / Equipment / Component must be selected.");
            if (model.Age is null)
                throw new InvalidOperationException("Age is required.");
            if (string.IsNullOrWhiteSpace(model.InspectCate) || model.NoOfIns is null)
                throw new InvalidOperationException("Inspection Effectiveness and No. of Inspections are required.");
            if (model.InspectDate is null)
                throw new InvalidOperationException("Inspection Date is required.");
            if (string.IsNullOrWhiteSpace(model.ThinType))
                throw new InvalidOperationException("Thinning Type is required.");
            if (model.CladDropdownEnabled && string.IsNullOrWhiteSpace(model.Clad))
                throw new InvalidOperationException("Please select the Clad Component.");

            await CalculateAsync(model);

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();

            try
            {
                var existing = await LoadExistingIdAsync(conn, tx, model.ProcID.Value, model.EquID.Value, model.CompID.Value, companyId);

                if (existing is null)
                {
                    const string insertSql = @"
                        INSERT INTO [ThinningDamage]
                            ([ProcID],[EquID],[CompID],[Clad],[age],[Art],[Tdf],[InspectCate],
                             [nofins],[InspectDate],[ThinType],[CompanyID],[CreatedBy],[CreatedDate],[Deleted])
                        VALUES
                            (@ProcID,@EquID,@CompID,@Clad,@Age,@Art,@Tdf,@InspectCate,
                             @NoOfIns,@InspectDate,@ThinType,@CompanyID,@UserId,GETDATE(),0)";

                    await using var cmd = new SqlCommand(insertSql, conn, tx);
                    AddSaveParameters(cmd, model, companyId, userId);
                    await cmd.ExecuteNonQueryAsync();
                }
                else
                {
                    const string updateSql = @"
                        UPDATE [ThinningDamage] SET
                            [Clad] = @Clad, [age] = @Age, [Art] = @Art, [Tdf] = @Tdf,
                            [InspectCate] = @InspectCate, [nofins] = @NoOfIns, [InspectDate] = @InspectDate,
                            [ThinType] = @ThinType, [ModifiedBy] = @UserId, [ModifiedDate] = GETDATE()
                        WHERE [TDFId] = @TDFId";

                    await using var cmd = new SqlCommand(updateSql, conn, tx);
                    AddSaveParameters(cmd, model, companyId, userId);
                    cmd.Parameters.AddWithValue("@TDFId", existing.Value);
                    await cmd.ExecuteNonQueryAsync();
                    model.TDFId = existing.Value;
                }

                await UpsertInspectionPlanAsync(conn, tx, model.ProcID.Value, model.EquID.Value, model.CompID.Value,
                    model.DamageFactorCode, model.InspectCate!, model.InspectDate.Value, companyId, userId);

                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }

            return model;
        }

        private static void AddSaveParameters(SqlCommand cmd, ThinningDamageModel model, int companyId, int userId)
        {
            cmd.Parameters.AddWithValue("@ProcID", model.ProcID!.Value);
            cmd.Parameters.AddWithValue("@EquID", model.EquID!.Value);
            cmd.Parameters.AddWithValue("@CompID", model.CompID!.Value);
            cmd.Parameters.AddWithValue("@Clad", (object?)model.Clad ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Age", model.Age!.Value);
            cmd.Parameters.AddWithValue("@Art", model.Art!.Value);
            cmd.Parameters.AddWithValue("@Tdf", model.Tdf!.Value);
            cmd.Parameters.AddWithValue("@InspectCate", model.InspectCate!);
            cmd.Parameters.AddWithValue("@NoOfIns", model.NoOfIns!.Value);
            cmd.Parameters.AddWithValue("@InspectDate", model.InspectDate!.Value);
            cmd.Parameters.AddWithValue("@ThinType", model.ThinType!);
            cmd.Parameters.AddWithValue("@CompanyID", companyId);
            cmd.Parameters.AddWithValue("@UserId", userId);
        }

        private static async Task<long?> LoadExistingIdAsync(SqlConnection conn, SqlTransaction tx, int procId, int equId, int compId, int companyId)
        {
            const string sql = @"SELECT [TDFId] FROM [ThinningDamage]
                                  WHERE [Deleted] = 0 AND [ProcID] = @ProcID AND [EquID] = @EquID
                                    AND [CompID] = @CompID AND [CompanyID] = @CompanyID";
            await using var cmd = new SqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("@ProcID", procId);
            cmd.Parameters.AddWithValue("@EquID", equId);
            cmd.Parameters.AddWithValue("@CompID", compId);
            cmd.Parameters.AddWithValue("@CompanyID", companyId);

            var result = await cmd.ExecuteScalarAsync();
            return result is null or DBNull ? null : Convert.ToInt64(result);
        }

        /// <summary>Equivalent of the InspectionPlan() helper - upserts one row keyed on ProcID/EquID/CompID/DamageFact.</summary>
        private static async Task UpsertInspectionPlanAsync(SqlConnection conn, SqlTransaction tx, int procId, int equId, int compId,
            string damageFactor, string inspectCate, DateTime inspectDate, int companyId, int userId)
        {
            const string existsSql = @"SELECT COUNT(1) FROM [InspectionPlan]
                                        WHERE [ProcID] = @ProcID AND [EquID] = @EquID
                                          AND [CompID] = @CompID AND [DamageFact] = @DamageFact";
            await using (var checkCmd = new SqlCommand(existsSql, conn, tx))
            {
                checkCmd.Parameters.AddWithValue("@ProcID", procId);
                checkCmd.Parameters.AddWithValue("@EquID", equId);
                checkCmd.Parameters.AddWithValue("@CompID", compId);
                checkCmd.Parameters.AddWithValue("@DamageFact", damageFactor);

                int count = (int)await checkCmd.ExecuteScalarAsync();

                string sql = count > 0
                    ? @"UPDATE [InspectionPlan] SET [InspectEffec] = @InspectCate, [InspectDate] = @InspectDate,
                            [ModifiedBy] = @UserId, [ModifiedDate] = GETDATE()
                        WHERE [ProcID] = @ProcID AND [EquID] = @EquID AND [CompID] = @CompID AND [DamageFact] = @DamageFact"
                    : @"INSERT INTO [InspectionPlan]
                            ([ProcID],[EquID],[CompID],[DamageFact],[InspectEffec],[InspectDate],[CompanyID],[CreatedBy],[CreatedDate],[Deleted])
                        VALUES
                            (@ProcID,@EquID,@CompID,@DamageFact,@InspectCate,@InspectDate,@CompanyID,@UserId,GETDATE(),0)";

                await using var cmd = new SqlCommand(sql, conn, tx);
                cmd.Parameters.AddWithValue("@ProcID", procId);
                cmd.Parameters.AddWithValue("@EquID", equId);
                cmd.Parameters.AddWithValue("@CompID", compId);
                cmd.Parameters.AddWithValue("@DamageFact", damageFactor);
                cmd.Parameters.AddWithValue("@InspectCate", inspectCate);
                cmd.Parameters.AddWithValue("@InspectDate", inspectDate);
                cmd.Parameters.AddWithValue("@CompanyID", companyId);
                cmd.Parameters.AddWithValue("@UserId", userId);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        public async Task DeleteAsync(int procId, int equId, int compId, string thinType)
        {
            string damageFactor = (thinType == "General") ? "Thinning" : "ThinningL";

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();
            try
            {
                const string delThinning = @"DELETE FROM [ThinningDamage]
                                              WHERE [ProcID] = @ProcID AND [EquID] = @EquID
                                                AND [CompID] = @CompID AND [Deleted] = 0";
                await using (var cmd = new SqlCommand(delThinning, conn, tx))
                {
                    cmd.Parameters.AddWithValue("@ProcID", procId);
                    cmd.Parameters.AddWithValue("@EquID", equId);
                    cmd.Parameters.AddWithValue("@CompID", compId);
                    await cmd.ExecuteNonQueryAsync();
                }

                const string delPlan = @"DELETE FROM [InspectionPlan]
                                          WHERE [ProcID] = @ProcID AND [EquID] = @EquID
                                            AND [CompID] = @CompID AND [DamageFact] = @DamageFact";
                await using (var cmd = new SqlCommand(delPlan, conn, tx))
                {
                    cmd.Parameters.AddWithValue("@ProcID", procId);
                    cmd.Parameters.AddWithValue("@EquID", equId);
                    cmd.Parameters.AddWithValue("@CompID", compId);
                    cmd.Parameters.AddWithValue("@DamageFact", damageFactor);
                    await cmd.ExecuteNonQueryAsync();
                }

                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        /// <summary>Equivalent of Inspectcategory() - grade code to display label.</summary>
        public static string InspectCategoryLabel(string grade) => grade switch
        {
            "A" => "Highly Effective",
            "B" => "Usually Effective",
            "C" => "Fairly Effective",
            "D" => "Poorly Effective",
            _ => "In Effective"
        };

        // ---------------------------------------------------------------
        // Small conversion helpers
        // ---------------------------------------------------------------
        private static double ToDouble(object v) => v is null or DBNull ? 0 : Convert.ToDouble(v);
        private static int? ToNullableInt(object v) => v is null or DBNull ? null : Convert.ToInt32(v);
        private static long? ToNullableLong(object v) => v is null or DBNull ? null : Convert.ToInt64(v);
        private static decimal? ToNullableDecimal(object v) => v is null or DBNull ? null : Convert.ToDecimal(v);
        private static DateTime? ToNullableDateTime(object v) => v is null or DBNull ? null : Convert.ToDateTime(v);
    }
}
