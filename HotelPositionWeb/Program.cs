using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

builder.Services.AddScoped<IDbConnection>(_ =>
    new SqlConnection(builder.Configuration.GetConnectionString("PmsDb")));

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/hotel-position", async (
    string? property,
    DateOnly? from,
    int? days,
    IDbConnection db,
    IConfiguration configuration) =>
{
    var options = configuration.GetSection("HotelPosition");
    var propertyCode = string.IsNullOrWhiteSpace(property)
        ? options.GetValue<string>("DefaultProperty") ?? "RRK"
        : property.Trim().ToUpperInvariant();

    var numberOfDays = days.GetValueOrDefault(options.GetValue<int?>("DefaultDays") ?? 17);
    if (numberOfDays <= 0) numberOfDays = 17;
    if (numberOfDays > 90) numberOfDays = 90;

    var fromDate = from ?? DateOnly.FromDateTime(DateTime.Today);
    var toDate = fromDate.AddDays(numberOfDays - 1);
    var fromYmd = ToYmd(fromDate);
    var toYmd = ToYmd(toDate);

    var dates = Enumerable.Range(0, numberOfDays).Select(fromDate.AddDays).ToList();

    var roomTypes = (await db.QueryAsync<RoomTypeDto>("""
        SELECT
            CAST(A.DSPSEQ AS int) AS DspSeq,
            A.ROMTYP AS RomTyp,
            CAST(A.APPDAT AS int) AS AppDat,
            CAST(A.TOTROM AS int) AS TotRom,
            A.SHTNAM AS ShtNam
        FROM pms.PR003TBL A
        WHERE A.PRPCOD = @propertyCode
          AND A.DELFLG = 0
          AND A.ROMTYP <> 'ZZZ'
          AND A.APPDAT = (
                SELECT MAX(B.APPDAT)
                FROM pms.PR003TBL B
                WHERE A.PRPCOD = B.PRPCOD
                  AND A.ROMTYP = B.ROMTYP
                  AND B.APPDAT <= @fromYmd
          )
        ORDER BY A.DSPSEQ, A.ROMTYP, A.APPDAT
        """, new { propertyCode, fromYmd })).ToList();

    var positions = (await db.QueryAsync<PositionDto>("""
        SELECT
            CAST(RUNDAT AS int) AS RunDat,
            ROMTYP AS RomTyp,
            CAST(POSTON AS int) AS PostOn
        FROM pms.FMHCHTBL
        WHERE PRPCOD = @propertyCode
          AND RUNDAT >= @fromYmd
          AND RUNDAT <= @toYmd
          AND ROMTYP <> 'ZZZ'
        ORDER BY ROMTYP, RUNDAT
        """, new { propertyCode, fromYmd, toYmd })).ToList();

    var allocations = (await db.QueryAsync<AllocationDto>("""
        SELECT
            CAST(A.RUNDAT AS int) AS RunDat,
            A.COMCOD AS ComCod,
            A.ROMTYP AS RomTyp,
            CAST(A.INNALC AS int) AS InnAlc
        FROM pms.FMACHTBL A
        INNER JOIN pms.FMALCTBL B
            ON B.COMCOD = A.COMCOD
           AND B.PRPCOD = A.PRPCOD
           AND B.ROMTYP = A.ROMTYP
           AND A.RUNDAT >= B.APPDAT
           AND A.RUNDAT <= B.TOODAT
        WHERE A.PRPCOD = @propertyCode
          AND A.RUNDAT >= @fromYmd
          AND A.RUNDAT <= @toYmd
        ORDER BY A.RUNDAT, A.COMCOD, A.ROMTYP
        """, new { propertyCode, fromYmd, toYmd })).ToList();

    var roomRows = roomTypes.Select(room => new GridRow(
        room.RomTyp,
        $"{room.RomTyp} - {room.ShtNam}",
        BuildValues(dates, date => positions
            .Where(p => p.RomTyp == room.RomTyp && p.RunDat == ToYmd(date))
            .Select(p => (int?)p.PostOn)
            .FirstOrDefault()))).ToList();

    var positionRow = new GridRow(
        "Position",
        "Position",
        BuildValues(dates, date => positions.Where(p => p.RunDat == ToYmd(date)).Sum(p => (int?)p.PostOn) ?? 0));

    var allocationRow = new GridRow(
        "Allocation",
        "Allocation",
        BuildValues(dates, date => NullIfZero(allocations.Where(a => a.RunDat == ToYmd(date)).Sum(a => a.InnAlc))));

    var totalRoomsRow = new GridRow(
        "TotalRooms",
        "Total Rooms",
        BuildValues(dates, _ => roomTypes.Sum(r => r.TotRom)));

    return Results.Ok(new HotelPositionResponse(
        propertyCode,
        fromDate,
        toDate,
        dates.Select(d => d.ToString("yyyy-MM-dd")).ToList(),
        roomRows,
        positionRow,
        allocationRow,
        totalRoomsRow));
});

app.Run();

static int ToYmd(DateOnly date) => date.Year * 10000 + date.Month * 100 + date.Day;

static Dictionary<string, int?> BuildValues(IEnumerable<DateOnly> dates, Func<DateOnly, int?> valueFactory)
{
    return dates.ToDictionary(date => date.ToString("yyyy-MM-dd"), valueFactory);
}

static int? NullIfZero(int value) => value == 0 ? null : value;

public sealed record RoomTypeDto(int DspSeq, string RomTyp, int AppDat, int TotRom, string ShtNam);
public sealed record PositionDto(int RunDat, string RomTyp, int PostOn);
public sealed record AllocationDto(int RunDat, string ComCod, string RomTyp, int InnAlc);
public sealed record GridRow(string Key, string Name, Dictionary<string, int?> Values);
public sealed record HotelPositionResponse(
    string Property,
    DateOnly From,
    DateOnly To,
    List<string> DateKeys,
    List<GridRow> RoomRows,
    GridRow Position,
    GridRow Allocation,
    GridRow TotalRooms);
