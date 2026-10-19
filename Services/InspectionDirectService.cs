using System.Data;
using System.Globalization;
using System.Text;
using Microsoft.Data.SqlClient;

namespace RBI_Malaysia.Services;

// =============================================================
// INSPECTION SERVICE - NO STORED PROCEDURE
//
// sp_Inspection-ல இருந்த எல்லா logic-உம் இங்கே inline
// parameterised SQL + C# ஆ இருக்கு.
//
// Models (InspectionModel, InspectionResult, InspectionProcessArea,
// InspectionEquipment, InspectionComponent) ஏற்கனவே
// InspectionService.cs-ல இருக்கு - அதையே reuse பண்றோம்.
//
// Program.cs-ல register பண்ணணும்:
//     builder.Services.AddScoped<InspectionDirectService>();
// =============================================================
public class InspectionDirectService
{
    private readonly IConfiguration _configuration;

    public InspectionDirectService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private string ConnectionString =>
        _configuration.GetConnectionString("connString")
        ?? throw new InvalidOperationException(
            "Connection string 'connString' was not found.");

    // =========================================================
    // MASTERS (Process Area / Equipment / Component)
    // =========================================================

    public async Task<List<InspectionProcessArea>> GetProcessAreasAsync(int companyId)
    {
        var list = new List<InspectionProcessArea>();

        await using var con = new SqlConnection(ConnectionString);
        await using var cmd = new SqlCommand(
            @"SELECT ProcessAreaID, ProcessArea
              FROM Tbl_ProcessArea
              WHERE Deleted = 0 AND CompanyID = @CompanyID
              ORDER BY ProcessArea", con);

        cmd.CommandTimeout = 15;
        cmd.Parameters.Add("@CompanyID", SqlDbType.Int).Value = companyId;

        await con.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();

        while (await r.ReadAsync())
        {
            list.Add(new InspectionProcessArea
            {
                ID = ToDecimal(r["ProcessAreaID"]) ?? 0,
                ProcessArea = ToText(r["ProcessArea"]) ?? ""
            });
        }

        return list;
    }

    public async Task<List<InspectionEquipment>> GetEquipmentsAsync(decimal processAreaId)
    {
        var list = new List<InspectionEquipment>();

        await using var con = new SqlConnection(ConnectionString);
        await using var cmd = new SqlCommand(
            @"SELECT EquAutoID, EquPID, EqupType
              FROM Tbl_EquipmentAsset
              WHERE Deleted = 0 AND ProcessareaID = @ProcessAreaID
              ORDER BY EqupType", con);

        cmd.CommandTimeout = 15;
        AddDecimal(cmd, "@ProcessAreaID", processAreaId);

        await con.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();

        while (await r.ReadAsync())
        {
            list.Add(new InspectionEquipment
            {
                EqupID = ToText(r["EquAutoID"]) ?? "",
                EquipmentName = ToText(r["EquPID"]) ?? "",
                EqupType = ToText(r["EqupType"]) ?? ""
            });
        }

        return list;
    }

    public async Task<List<InspectionComponent>> GetComponentsAsync(string equpId)
    {
        var list = new List<InspectionComponent>();

        await using var con = new SqlConnection(ConnectionString);
        await using var cmd = new SqlCommand(
            @"SELECT CompAutoID, CompName
              FROM VW_Componentview
              WHERE EqupID = @EqupID AND Deleted = 0
              ORDER BY CompName", con);

        cmd.CommandTimeout = 15;
        AddText(cmd, "@EqupID", 20, equpId);

        await con.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();

        while (await r.ReadAsync())
        {
            list.Add(new InspectionComponent
            {
                ComponentNo = ToText(r["CompAutoID"]) ?? "",
                ComponentName = ToText(r["CompName"]) ?? ""
            });
        }

        return list;
    }

    // =========================================================
    // GRID  (old modes: GRID / GRID_PROCESS / GRID_EQUIPMENT / GRID_COMPONENT)
    // =========================================================

    public async Task<List<InspectionModel>> GetInspectionAsync(
        decimal companyId,
        decimal processAreaId = 0,
        string? equpId = null,
        string? componentNo = null)
    {
        var sql = new StringBuilder(
            @"SELECT *,
                     CASE WHEN ISNULL(ShortCRrate,0) >= ISNULL(LongCRrate,0)
                          THEN ShortCRrate ELSE LongCRrate END AS CalculatedUCR
              FROM VW_Inspection
              WHERE deleted = 0
                AND CompanyID = @CompanyID");

        if (processAreaId > 0) sql.Append(" AND ProcessareaID = @ProcessAreaID");
        if (!string.IsNullOrWhiteSpace(equpId)) sql.Append(" AND EquAutoID = @EqupID");
        if (!string.IsNullOrWhiteSpace(componentNo)) sql.Append(" AND CompAutoID = @ComponentNo");

        sql.Append(" ORDER BY EquAutoID, InspectionPointNo, InspecDate");

        await using var con = new SqlConnection(ConnectionString);
        await using var cmd = new SqlCommand(sql.ToString(), con);

        cmd.CommandTimeout = 30;
        AddDecimal(cmd, "@CompanyID", companyId);
        if (processAreaId > 0) AddDecimal(cmd, "@ProcessAreaID", processAreaId);
        if (!string.IsNullOrWhiteSpace(equpId)) AddText(cmd, "@EqupID", 20, equpId);
        if (!string.IsNullOrWhiteSpace(componentNo)) AddText(cmd, "@ComponentNo", 20, componentNo);

        return await ReadInspectionsAsync(con, cmd);
    }

    // =========================================================
    // LAST INSPECTION  (old mode: LAST)
    // =========================================================

    public async Task<List<InspectionModel>> GetLastInspectionAsync(
        decimal companyId,
        string equpId,
        string componentNo)
    {
        await using var con = new SqlConnection(ConnectionString);
        await using var cmd = new SqlCommand(
            @"SELECT *
              FROM VW_Inspection
              WHERE deleted = 0
                AND EquAutoID = @EqupID
                AND CompAutoID = @ComponentNo
                AND LongCRrate IS NOT NULL
                AND CompanyID = @CompanyID
              ORDER BY EquAutoID, InspectionPointNo, InspecDate", con);

        cmd.CommandTimeout = 30;
        AddText(cmd, "@EqupID", 20, equpId);
        AddText(cmd, "@ComponentNo", 20, componentNo);
        AddDecimal(cmd, "@CompanyID", companyId);

        return await ReadInspectionsAsync(con, cmd);
    }

    // =========================================================
    // CALCULATION CARD
    // =========================================================

    public async Task<InspectionModel?> GetInspectionCalculationAsync(
        decimal processAreaId,
        string equpId,
        string componentNo)
    {
        await using var con = new SqlConnection(ConnectionString);
        await con.OpenAsync();

        decimal shortCR = 0, longCR = 0, uCR = 0, defaultValue = 0, remainingLife = 0;
        string? InspectionPoint=null;

        await using (var cmd = new SqlCommand(
            @"SELECT ShortCRrate, LongCRrate, uCR, Defaultvalue, Remaininglife, InspectionPointNo
              FROM VW_EquipmentComponentDetails
              WHERE ProcessAreaID = @ProcessAreaID
                AND EqupID = @EqupID
                AND CompAutoID = @ComponentNo
                AND Deleted = 0", con))
        {
            AddDecimal(cmd, "@ProcessAreaID", processAreaId);
            AddText(cmd, "@EqupID", 20, equpId);
            AddText(cmd, "@ComponentNo", 20, componentNo);

            await using var r = await cmd.ExecuteReaderAsync();

            if (await r.ReadAsync())
            {
                shortCR = ToDecimal(r["ShortCRrate"]) ?? 0;
                longCR = ToDecimal(r["LongCRrate"]) ?? 0;
                uCR = ToDecimal(r["uCR"]) ?? 0;
                defaultValue = ToDecimal(r["Defaultvalue"]) ?? 0;
                remainingLife = ToDecimal(r["Remaininglife"]) ?? 0;
                InspectionPoint = ToText(r["InspectionPointNo"] ?? null);
            }
        }

        // Current thickness: முதல்ல DefaultsCR + DefaultlCR match, இல்லன்னா uCR match
        decimal? currentThickness = null;

        await using (var cmd = new SqlCommand(
            @"SELECT MIN(ReadingValue)
              FROM Tbl_InspectionDataDetails
              WHERE EqupID = @EqupID
                AND ComponentNo = @ComponentNo
                AND DefaultsCR = @DefaultsCR
                AND DefaultlCR = @DefaultLCR
                AND Deleted = 0", con))
        {
            AddText(cmd, "@EqupID", 20, equpId);
            AddText(cmd, "@ComponentNo", 20, componentNo);
            AddDecimal(cmd, "@DefaultsCR", shortCR, 3);
            AddDecimal(cmd, "@DefaultLCR", longCR, 3);

            currentThickness = ToDecimal(await cmd.ExecuteScalarAsync());
        }

        if (!currentThickness.HasValue)
        {
            await using var cmd = new SqlCommand(
                @"SELECT MIN(ReadingValue)
                  FROM Tbl_InspectionDataDetails
                  WHERE EqupID = @EqupID
                    AND ComponentNo = @ComponentNo
                    AND uCR = @UCR
                    AND Deleted = 0", con);

            AddText(cmd, "@EqupID", 20, equpId);
            AddText(cmd, "@ComponentNo", 20, componentNo);
            AddDecimal(cmd, "@UCR", uCR, 3);

            currentThickness = ToDecimal(await cmd.ExecuteScalarAsync());
        }

        return new InspectionModel
        {
            ShortCRrate = shortCR,
            LongCRrate = longCR,
            UCR = uCR,
            DefaultsCR = shortCR,
            DefaultLCR = longCR,
            RemainingLife = remainingLife,
            CurrentThickness = currentThickness,
            Defaultvalue = defaultValue,
            InspectionPointNo = InspectionPoint ?? string.Empty
        };
    }

    // =========================================================
    // INSERT / UPDATE / DELETE
    // =========================================================

    public async Task<InspectionResult> InsertInspectionAsync(
        InspectionModel model, decimal companyId, decimal userId)
    {
        if (companyId <= 0) return Fail("Company not found. Please login again.");
        if (string.IsNullOrWhiteSpace(model.EqupID)) return Fail("Equipment is required.");
        if (string.IsNullOrWhiteSpace(model.ComponentNo)) return Fail("Component is required.");
        if (string.IsNullOrWhiteSpace(model.InspectionPointNo)) return Fail("Inspection Point No is required.");
        if (!model.InspecDate.HasValue) return Fail("Inspection Date is required.");
        if (!model.ReadingValue.HasValue) return Fail("Inspection Reading Value is required.");

        string equpId = model.EqupID!;
        string componentNo = model.ComponentNo!;
        string pointNo = model.InspectionPointNo!.Trim();

        await using var con = new SqlConnection(ConnectionString);
        await con.OpenAsync();
        await using var tx = (SqlTransaction)await con.BeginTransactionAsync();

        // Component-ல Normal Thickness / MRT / Default CR இருக்கான்னு check
        var comp = await GetComponentInfoAsync(con, tx, equpId, componentNo);
        if (comp == null)
        {
            return Fail("Please check Normal Thickness, MRT and Default CR Rate values in Component.");
        }

        await using (var cmd = new SqlCommand(
            @"INSERT INTO Tbl_InspectionDataDetails
              (EqupID, ComponentNo, InspecDate, InspectionPointNo, ReadingValue,
               ShortCRrate, LongCRrate, RemainingLife,
               CompanyID, Createdby, createddate, deleted, Rowversions)
              VALUES
              (@EqupID, @ComponentNo, @InspecDate, @InspectionPointNo, @ReadingValue,
               0, 0, 0,
               @CompanyID, @UserID, GETDATE(), 0, 0)", con, tx))
        {
            AddText(cmd, "@EqupID", 20, equpId);
            AddText(cmd, "@ComponentNo", 20, componentNo);
            AddDate(cmd, "@InspecDate", model.InspecDate);
            AddText(cmd, "@InspectionPointNo", 50, pointNo);
            AddDecimal(cmd, "@ReadingValue", model.ReadingValue, 3);
            AddDecimal(cmd, "@CompanyID", companyId);
            AddDecimal(cmd, "@UserID", userId);

            await cmd.ExecuteNonQueryAsync();
        }

        await RecalculateAsync(con, tx, equpId, componentNo, pointNo, false);
        await tx.CommitAsync();

        return Ok("Inspection inserted successfully.");
    }

    public async Task<InspectionResult> UpdateInspectionAsync(
        InspectionModel model, decimal companyId, decimal userId)
    {
        if (companyId <= 0) return Fail("Company not found. Please login again.");
        if (model.InspecAutoID <= 0) return Fail("Inspection ID is required.");
        if (!model.InspecDate.HasValue) return Fail("Inspection Date is required.");
        if (!model.ReadingValue.HasValue) return Fail("Inspection Reading Value is required.");

        await using var con = new SqlConnection(ConnectionString);
        await con.OpenAsync();
        await using var tx = (SqlTransaction)await con.BeginTransactionAsync();

        // Recalculate-க்கு DB-ல இருக்கிற Equipment / Component / Point-ஐயே பயன்படுத்தறோம்
        var key = await GetInspectionKeyAsync(con, tx, model.InspecAutoID, companyId);
        if (key == null) return Fail("Inspection record not found.");

        int rows;
        await using (var cmd = new SqlCommand(
            @"UPDATE Tbl_InspectionDataDetails
              SET InspecDate = @InspecDate,
                  ReadingValue = @ReadingValue,
                  Modifiedby = @ModifiedBy,
                  Modifydate = GETDATE()
              WHERE InspecAutoID = @InspecAutoID
                AND deleted = 0
                AND CompanyID = @CompanyID", con, tx))
        {
            AddDate(cmd, "@InspecDate", model.InspecDate);
            AddDecimal(cmd, "@ReadingValue", model.ReadingValue, 3);
            AddText(cmd, "@ModifiedBy", 20, userId.ToString("0", CultureInfo.InvariantCulture));
            AddDecimal(cmd, "@InspecAutoID", model.InspecAutoID);
            AddDecimal(cmd, "@CompanyID", companyId);

            rows = await cmd.ExecuteNonQueryAsync();
        }

        if (rows == 0) return Fail("Inspection record not found.");

        await RecalculateAsync(con, tx, key.Value.EqupId, key.Value.ComponentNo, key.Value.PointNo, false);
        await tx.CommitAsync();

        return Ok("Inspection updated successfully.");
    }

    public async Task<InspectionResult> DeleteInspectionAsync(
        decimal inspectionId, decimal companyId, decimal userId)
    {
        if (companyId <= 0) return Fail("Company not found. Please login again.");
        if (inspectionId <= 0) return Fail("Inspection ID is required.");

        await using var con = new SqlConnection(ConnectionString);
        await con.OpenAsync();
        await using var tx = (SqlTransaction)await con.BeginTransactionAsync();

        var key = await GetInspectionKeyAsync(con, tx, inspectionId, companyId);
        if (key == null) return Fail("Inspection record not found.");

        // Soft delete
        await using (var cmd = new SqlCommand(
            @"UPDATE Tbl_InspectionDataDetails
              SET deleted = 1,
                  Modifiedby = @ModifiedBy,
                  Modifydate = GETDATE()
              WHERE InspecAutoID = @InspecAutoID
                AND deleted = 0
                AND CompanyID = @CompanyID", con, tx))
        {
            AddText(cmd, "@ModifiedBy", 20, userId.ToString("0", CultureInfo.InvariantCulture));
            AddDecimal(cmd, "@InspecAutoID", inspectionId);
            AddDecimal(cmd, "@CompanyID", companyId);

            await cmd.ExecuteNonQueryAsync();
        }

        await RecalculateAsync(con, tx, key.Value.EqupId, key.Value.ComponentNo, key.Value.PointNo, true);
        await tx.CommitAsync();

        return Ok("Inspection deleted successfully.");
    }

    // =========================================================
    // RECALCULATE  (INSERT / UPDATE / DELETE-க்கு அப்புறம் common)
    //
    // பழைய ASPX page (RadGrid1_InsertCommand / UpdateCommand) logic:
    //
    //  A. Point-ன் ஒவ்வொரு reading-க்கும் (date வரிசை):
    //       Short CR  = (previous reading - this reading) / (days / 365)
    //       Long CR   = (initial reading  - this reading) / (days / 365)
    //       Default SCR / LCR = CR <= 0 ? Component Default CR : CR
    //       uCR       = max(Default SCR, Default LCR)
    //       RemainingLife (row) = Round((this reading - MRT) / uCR, 0)
    //     ShortCRrate / LongCRrate / DefaultsCR / DefaultlCR / uCR
    //     கடைசி (latest) reading-ல மட்டும்; முந்தின rows-ல NULL.
    //     SCR / LCR / DSCR / DLCR / Previous / Initial / RemainingLife எல்லா rows-லயும் இருக்கும்.
    //
    //  B. Component முழுக்க (ஒவ்வொரு point-ன் latest row மட்டும் கணக்குல வரும்):
    //       max Default SCR / LCR / uCR
    //       ReadVal (current thickness) = min reading, அந்த max CR உள்ள row-ல
    //       RemainingLife (component) = Round((ReadVal - MRT) / max uCR, 0)
    //
    //  Math.Round(double) - பழைய code மாதிரியே double + default rounding.
    // =========================================================

    private static async Task RecalculateAsync(
        SqlConnection con, SqlTransaction tx,
        string equpId, string componentNo, string pointNo, bool isDelete)
    {
        var comp = await GetComponentInfoAsync(con, tx, equpId, componentNo);

        double mrt = (double)(comp?.Mrt ?? 0);
        double defaultCr = (double)(comp?.DefaultCr ?? 0.010m);

        // ---------------------------------------------------------
        // Component-ல inspection record ஒண்ணுமே இல்லன்னா reset
        // ---------------------------------------------------------
        int total;
        await using (var cmd = new SqlCommand(
            @"SELECT COUNT(*) FROM Tbl_InspectionDataDetails
              WHERE EqupID = @EqupID AND ComponentNo = @ComponentNo AND deleted = 0", con, tx))
        {
            AddText(cmd, "@EqupID", 20, equpId);
            AddText(cmd, "@ComponentNo", 20, componentNo);
            total = (int)(await cmd.ExecuteScalarAsync() ?? 0);
        }

        if (total == 0)
        {
            await ResetComponentAsync(con, tx, equpId, componentNo, 0);
            return;
        }

        // ---------------------------------------------------------
        // A. இந்த Inspection Point-ன் எல்லா reading (date, id வரிசை)
        // ---------------------------------------------------------
        var rows = new List<ReadingRow>();

        await using (var cmd = new SqlCommand(
            @"SELECT InspecAutoID, InspecDate, ReadingValue
              FROM Tbl_InspectionDataDetails
              WHERE EqupID = @EqupID
                AND ComponentNo = @ComponentNo
                AND InspectionPointNo = @InspectionPointNo
                AND deleted = 0
              ORDER BY InspecDate, InspecAutoID", con, tx))
        {
            AddText(cmd, "@EqupID", 20, equpId);
            AddText(cmd, "@ComponentNo", 20, componentNo);
            AddText(cmd, "@InspectionPointNo", 50, pointNo);

            await using var r = await cmd.ExecuteReaderAsync();

            while (await r.ReadAsync())
            {
                var date = ToDate(r["InspecDate"]);
                if (date == null) continue;

                rows.Add(new ReadingRow(
                    ToDecimal(r["InspecAutoID"]) ?? 0,
                    date.Value,
                    ToDecimal(r["ReadingValue"])));
            }
        }

        if (rows.Count > 0)
        {
            var initial = rows[0];
            double initValue = (double)(initial.Reading ?? 0);

            await using var upd = new SqlCommand(
                @"UPDATE Tbl_InspectionDataDetails
                  SET Previousdate  = @PrevDate,
                      Previousvalue = @PrevValue,
                      Initialdate   = @InitDate,
                      Initialvalue  = @InitValue,
                      SCR = @SCR,
                      LCR = @LCR,
                      DSCR = @DSCR,
                      DLCR = @DLCR,
                      ShortCRrate = @ShortCR,
                      LongCRrate  = @LongCR,
                      DefaultsCR  = @DefaultsCR,
                      DefaultlCR  = @DefaultlCR,
                      uCR = @UCR,
                      RemainingLife = @RemainingLife
                  WHERE InspecAutoID = @ID", con, tx);

            var pPrevDate = upd.Parameters.Add("@PrevDate", SqlDbType.SmallDateTime);
            var pPrevValue = NewDecimal(upd, "@PrevValue", 3);
            var pInitDate = upd.Parameters.Add("@InitDate", SqlDbType.SmallDateTime);
            var pInitValue = NewDecimal(upd, "@InitValue", 3);
            var pScr = NewDecimal(upd, "@SCR", 3);
            var pLcr = NewDecimal(upd, "@LCR", 3);
            var pDscr = NewDecimal(upd, "@DSCR", 3);
            var pDlcr = NewDecimal(upd, "@DLCR", 3);

            // இந்த ஐந்தும் கடைசி reading-ல மட்டும் இருக்கும், மத்த rows-ல NULL
            var pShortCr = NewDecimal(upd, "@ShortCR", 3);
            var pLongCr = NewDecimal(upd, "@LongCR", 3);
            var pDefS = NewDecimal(upd, "@DefaultsCR", 3);
            var pDefL = NewDecimal(upd, "@DefaultlCR", 3);
            var pUcr = NewDecimal(upd, "@UCR", 3);
            var pRemain = NewDecimal(upd, "@RemainingLife", 0);
            var pId = NewDecimal(upd, "@ID", 0);

            ReadingRow? prev = null;

            foreach (var cur in rows)
            {
                // முதல் reading (Initial date) -> CR எல்லாமே NULL
                bool isInitial = cur.Date == initial.Date;
                bool isLast = cur.Id == rows[rows.Count - 1].Id;
                double inspect = (double)(cur.Reading ?? 0);

                double sCr = 0, dsCr = 0, lCr = 0, dlCr = 0;

                if (!isInitial && prev != null)
                {
                    // Short CR
                    double years = (cur.Date - prev.Date).Days / 365.0;
                    sCr = years == 0
                        ? 0
                        : Math.Round(((double)(prev.Reading ?? 0) - inspect) / years, 3);
                    dsCr = sCr <= 0 ? defaultCr : sCr;

                    // Long CR
                    double yearsL = (cur.Date - initial.Date).Days / 365.0;
                    lCr = yearsL == 0
                        ? 0
                        : Math.Round((initValue - inspect) / yearsL, 3);
                    dlCr = lCr <= 0 ? defaultCr : lCr;
                }

                double maxdCr = Math.Max(dsCr, dlCr);

                // Row Remaining Life = (this reading - MRT) / uCR
                // Initial row-க்கு 0 (பழைய page-ல insert-ல வந்த 0 அப்படியே இருக்கும்)
                decimal? remain = isInitial
                    ? 0m
                    : (maxdCr > 0
                        ? (decimal?)Math.Round((inspect - mrt) / maxdCr, 0)
                        : null);

                pPrevDate.Value = Db(isInitial ? null : prev?.Date);
                pPrevValue.Value = Db(isInitial ? null : prev?.Reading);
                pInitDate.Value = Db(isInitial ? null : initial.Date);
                pInitValue.Value = Db(isInitial ? null : initial.Reading);

                // SCR / LCR / DSCR / DLCR: Initial row தவிர எல்லாத்துலயும் இருக்கும்
                pScr.Value = Db(isInitial ? (decimal?)null : (decimal)sCr);
                pLcr.Value = Db(isInitial ? (decimal?)null : (decimal)lCr);
                pDscr.Value = Db(isInitial ? (decimal?)null : (decimal)dsCr);
                pDlcr.Value = Db(isInitial ? (decimal?)null : (decimal)dlCr);

                // ShortCRrate / LongCRrate / DefaultsCR / DefaultlCR / uCR:
                // கடைசியா insert பண்ண (latest date) row-ல மட்டும் - மத்ததெல்லாம் NULL
                // (பழைய code-ல "Previousdt != 01/01/0001" sql7 block இதைத்தான் பண்ணுது)
                bool keepLatest = isLast && !isInitial;

                pShortCr.Value = Db(keepLatest ? (decimal?)sCr : null);
                pLongCr.Value = Db(keepLatest ? (decimal?)lCr : null);
                pDefS.Value = Db(keepLatest ? (decimal?)dsCr : null);
                pDefL.Value = Db(keepLatest ? (decimal?)dlCr : null);
                pUcr.Value = Db(keepLatest ? (decimal?)maxdCr : null);
                pRemain.Value = Db(remain);

                pId.Value = cur.Id;

                await upd.ExecuteNonQueryAsync();

                prev = cur;
            }
        }

        // ---------------------------------------------------------
        // B. Component முழுக்க உள்ள எல்லா row-உம் (எல்லா points)
        // ---------------------------------------------------------
        var all = new List<(decimal? Reading, decimal? S, decimal? L, decimal? U)>();

        await using (var cmd = new SqlCommand(
            @"SELECT ReadingValue, DefaultsCR, DefaultlCR, uCR
              FROM Tbl_InspectionDataDetails
              WHERE EqupID = @EqupID AND ComponentNo = @ComponentNo AND deleted = 0", con, tx))
        {
            AddText(cmd, "@EqupID", 20, equpId);
            AddText(cmd, "@ComponentNo", 20, componentNo);

            await using var r = await cmd.ExecuteReaderAsync();

            while (await r.ReadAsync())
            {
                all.Add((
                    ToDecimal(r["ReadingValue"]),
                    ToDecimal(r["DefaultsCR"]),
                    ToDecimal(r["DefaultlCR"]),
                    ToDecimal(r["uCR"])));
            }
        }

        int count = all.Count;

        // CR value உள்ள row ஒண்ணுமே இல்ல (initial reading மட்டும்)
        if (!all.Any(x => x.S.HasValue))
        {
            // பழைய page: Insert/Update-ல component-ஐ தொடாது, Delete-ல reset
            if (isDelete)
                await ResetComponentAsync(con, tx, equpId, componentNo, count);

            return;
        }

        decimal maxScr = all.Where(x => x.S.HasValue).Max(x => x.S!.Value);
        decimal maxLcr = all.Where(x => x.L.HasValue).Select(x => x.L!.Value).DefaultIfEmpty(0).Max();
        decimal maxUcr = Math.Max(maxScr, maxLcr);

        // ReadVal (current thickness):
        //   1) max Default SCR + max Default LCR இரண்டும் உள்ள row-ல min reading
        //   2) இல்லன்னா uCR = max uCR உள்ள row-ல min reading
        decimal? readVal = all
            .Where(x => x.Reading.HasValue && x.S == maxScr && x.L == maxLcr)
            .Min(x => x.Reading);

        if (!readVal.HasValue)
        {
            readVal = all
                .Where(x => x.Reading.HasValue && x.U == maxUcr)
                .Min(x => x.Reading);
        }

        // Component Remaining Life = (ReadVal - MRT) / max uCR
        decimal? remainLife = (readVal.HasValue && maxUcr > 0)
            ? (decimal?)Math.Round(((double)readVal.Value - mrt) / (double)maxUcr, 0)
            : null;

        await using (var cmd = new SqlCommand(
            @"UPDATE Tbl_EquipmentComponentDetails
              SET ShortCRrate = @ShortCR,
                  LongCRrate = @LongCR,
                  uCR = @UCR,
                  RemainingLife = @RemainingLife,
                  NoofInspection = @Count,
                  ReadVal = @ReadVal
              WHERE EqupID = @EqupID AND CompAutoID = @ComponentNo AND deleted = 0", con, tx))
        {
            AddDecimal(cmd, "@ShortCR", maxScr, 3);
            AddDecimal(cmd, "@LongCR", maxLcr, 3);
            AddDecimal(cmd, "@UCR", maxUcr, 3);
            AddDecimal(cmd, "@RemainingLife", remainLife, 0);
            cmd.Parameters.Add("@Count", SqlDbType.Int).Value = count;
            AddDecimal(cmd, "@ReadVal", readVal, 3);
            AddText(cmd, "@EqupID", 20, equpId);
            AddText(cmd, "@ComponentNo", 20, componentNo);

            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task ResetComponentAsync(
        SqlConnection con, SqlTransaction tx,
        string equpId, string componentNo, int count)
    {
        await using var cmd = new SqlCommand(
            @"UPDATE Tbl_EquipmentComponentDetails
              SET ShortCRrate = NULL, LongCRrate = NULL, uCR = NULL,
                  RemainingLife = NULL, NoofInspection = @Count
              WHERE EqupID = @EqupID AND CompAutoID = @ComponentNo AND deleted = 0", con, tx);

        cmd.Parameters.Add("@Count", SqlDbType.Int).Value = count;
        AddText(cmd, "@EqupID", 20, equpId);
        AddText(cmd, "@ComponentNo", 20, componentNo);

        await cmd.ExecuteNonQueryAsync();
    }

    private sealed record ReadingRow(decimal Id, DateTime Date, decimal? Reading);

    private sealed record ComponentInfo(decimal NormalThickness, decimal Mrt, decimal DefaultCr);

    // =========================================================
    // LOOKUPS
    // =========================================================

    private static async Task<ComponentInfo?> GetComponentInfoAsync(
        SqlConnection con, SqlTransaction tx, string equpId, string componentNo)
    {
        await using var cmd = new SqlCommand(
            @"SELECT NormalThickness, MRT, DefaultValue
              FROM VW_Componentview
              WHERE EqupID = @EqupID AND CompAutoID = @ComponentNo AND Deleted = 0", con, tx);

        AddText(cmd, "@EqupID", 20, equpId);
        AddText(cmd, "@ComponentNo", 20, componentNo);

        await using var r = await cmd.ExecuteReaderAsync();

        if (!await r.ReadAsync()) return null;

        decimal nt = Math.Round(ToDecimal(r["NormalThickness"]) ?? 0, 3);
        decimal mrt = Math.Round(ToDecimal(r["MRT"]) ?? 0, 3);

        decimal? def = ToDecimal(r["DefaultValue"]);
        decimal defaultCr = (def == null || def == 0) ? 0.010m : Math.Round(def.Value, 3);

        return new ComponentInfo(nt, mrt, defaultCr);
    }

    private static async Task<(string EqupId, string ComponentNo, string PointNo)?>
        GetInspectionKeyAsync(
            SqlConnection con, SqlTransaction tx, decimal inspectionId, decimal companyId)
    {
        await using var cmd = new SqlCommand(
            @"SELECT EqupID, ComponentNo, InspectionPointNo
              FROM Tbl_InspectionDataDetails
              WHERE InspecAutoID = @InspecAutoID
                AND deleted = 0
                AND CompanyID = @CompanyID", con, tx);

        AddDecimal(cmd, "@InspecAutoID", inspectionId);
        AddDecimal(cmd, "@CompanyID", companyId);

        await using var r = await cmd.ExecuteReaderAsync();

        if (!await r.ReadAsync()) return null;

        string? e = ToText(r["EqupID"]);
        string? c = ToText(r["ComponentNo"]);
        string? p = ToText(r["InspectionPointNo"]);

        if (string.IsNullOrWhiteSpace(e) || string.IsNullOrWhiteSpace(c) || p == null)
            return null;

        return (e, c, p);
    }

    // =========================================================
    // GRID READER / MAPPER
    // =========================================================

    private static async Task<List<InspectionModel>> ReadInspectionsAsync(
        SqlConnection con, SqlCommand cmd)
    {
        var list = new List<InspectionModel>();

        await con.OpenAsync();
        await using var r = await cmd.ExecuteReaderAsync();

        var ord = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < r.FieldCount; i++) ord[r.GetName(i)] = i;

        while (await r.ReadAsync())
        {
            list.Add(MapInspection(r, ord));
        }

        return list;
    }

    private static InspectionModel MapInspection(
        SqlDataReader r, Dictionary<string, int> ord)
    {
        string? S(string name) =>
            ord.TryGetValue(name, out var i) ? ToText(r.GetValue(i)) : null;

        decimal? D(string name) =>
            ord.TryGetValue(name, out var i) ? ToDecimal(r.GetValue(i)) : null;

        DateTime? T(string name) =>
            ord.TryGetValue(name, out var i) ? ToDate(r.GetValue(i)) : null;

        return new InspectionModel
        {
            InspecAutoID = D("InspecAutoID") ?? 0,
            EqupID = S("EquAutoID"),
            ComponentNo = S("CompAutoID"),
            ProcessAreaID = D("ProcessareaID") ?? 0,

            EquipmentName = S("EqupType"),
            ComponentName = S("CompName"),
            ProcessAreaName = S("ProcessArea"),

            InspectionPointNo = S("InspectionPointNo"),
            InspecDate = T("InspecDate"),
            ReadingValue = D("ReadingValue"),

            PreviousDate = T("Previousdate"),
            PreviousValue = D("Previousvalue"),
            InitialDate = T("Initialdate"),
            InitialValue = D("Initialvalue"),

            ShortCRrate = D("ShortCRrate"),
            LongCRrate = D("LongCRrate"),
            DefaultsCR = D("DefaultsCR"),
            DefaultLCR = D("DefaultlCR"),

            SCR = D("SCR"),
            LCR = D("LCR"),
            DSCR = D("DSCR"),
            DLCR = D("DLCR"),
            UCR = D("uCR"),

            RemainingLife = D("RemainingLife"),
            CalculatedUCR = D("CalculatedUCR")
        };
    }

    // =========================================================
    // SMALL HELPERS
    // (Convert.ToDecimal இல்ல - TryParse மட்டும், nvarchar column-க்கு safe)
    // =========================================================

    private static InspectionResult Ok(string message) =>
        new() { Result = 1, Message = message };

    private static InspectionResult Fail(string message) =>
        new() { Result = 0, Message = message };

    private static object Db(object? value) => value ?? DBNull.Value;

    private static decimal? ToDecimal(object? value)
    {
        if (value == null || value is DBNull) return null;
        if (value is decimal d) return d;

        return decimal.TryParse(
            Convert.ToString(value, CultureInfo.InvariantCulture),
            NumberStyles.Number | NumberStyles.AllowExponent,
            CultureInfo.InvariantCulture,
            out var n)
            ? n
            : null;
    }

    private static DateTime? ToDate(object? value)
    {
        if (value == null || value is DBNull) return null;
        if (value is DateTime dt) return dt;

        return DateTime.TryParse(
            Convert.ToString(value, CultureInfo.InvariantCulture),
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var p)
            ? p
            : null;
    }

    private static string? ToText(object? value)
    {
        if (value == null || value is DBNull) return null;
        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    // SqlParameter-ல Decimal-க்கு Precision / Scale set பண்ணலன்னா
    // decimal பகுதி truncate ஆகும் - அதனால எப்பவும் explicit.
    private static SqlParameter NewDecimal(SqlCommand cmd, string name, byte scale)
    {
        var p = new SqlParameter(name, SqlDbType.Decimal)
        {
            Precision = 18,
            Scale = scale
        };
        cmd.Parameters.Add(p);
        return p;
    }

    private static void AddDecimal(SqlCommand cmd, string name, decimal? value, byte scale = 0)
    {
        var p = NewDecimal(cmd, name, scale);
        p.Value = value.HasValue ? (object)value.Value : DBNull.Value;
    }

    private static void AddText(SqlCommand cmd, string name, int size, string? value)
    {
        cmd.Parameters.Add(name, SqlDbType.NVarChar, size).Value =
            string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
    }

    private static void AddDate(SqlCommand cmd, string name, DateTime? value)
    {
        cmd.Parameters.Add(name, SqlDbType.SmallDateTime).Value =
            value.HasValue ? (object)value.Value : DBNull.Value;
    }
}

// =============================================================
// INSPECTION MODEL
// =============================================================
public class InspectionModel
{
    public decimal InspecAutoID { get; set; }

    public decimal ProcessAreaID { get; set; }

    public string? ProcessAreaName { get; set; }

    public string? EqupID { get; set; }

    public string? EquipmentName { get; set; }

    public string? ComponentNo { get; set; }

    public string? ComponentName { get; set; }

    public string? InspectionPointNo { get; set; }

    public DateTime? InspecDate { get; set; }

    public decimal? ReadingValue { get; set; }

    public DateTime? PreviousDate { get; set; }

    public decimal? PreviousValue { get; set; }

    public DateTime? InitialDate { get; set; }

    public decimal? InitialValue { get; set; }

    public decimal? ShortCRrate { get; set; }

    public decimal? LongCRrate { get; set; }

    public decimal? DefaultsCR { get; set; }

    public decimal? DefaultLCR { get; set; }

    public decimal? SCR { get; set; }

    public decimal? LCR { get; set; }

    public decimal? DSCR { get; set; }

    public decimal? DLCR { get; set; }

    public decimal? UCR { get; set; }

    public decimal? CalculatedUCR { get; set; }

    public decimal? RemainingLife { get; set; }

    public decimal? CompanyID { get; set; }

    public decimal? CreatedBy { get; set; }

    public decimal? CurrentThickness { get; set; }

    public decimal? Defaultvalue { get; set; }
}


// =============================================================
// RESULT
// =============================================================
public class InspectionResult
{
    public int Result { get; set; }

    public string? Message { get; set; }

    public bool Success => Result == 1;
}


// =============================================================
// FILTER MODELS
// =============================================================
public class InspectionProcessArea
{
    public decimal ID { get; set; }

    public string? ProcessArea { get; set; }
}


public class InspectionEquipment
{
    public string? EqupID { get; set; }

    public string? EquipmentName { get; set; }

    public string? EqupType { get; set; }
}


public class InspectionComponent
{
    public string? ComponentNo { get; set; }

    public string? ComponentName { get; set; }
}