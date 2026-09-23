using System.Globalization;
using System.Text.Json;
using ClosedXML.Excel;

namespace ProjectAPI.Api.Application.Imports;

/// <summary>
/// §5.11/§23 — parses the Buildings + Units tabs into plain row DTOs and
/// validates each one independently, so both ValidateImportBatchHandler
/// (dry-run) and CommitImportBatchHandler (the actual write) read the file
/// identically — no risk of the two tabs disagreeing about what is valid.
/// </summary>
public static class ImportWorkbookReader
{
    public const int MaxRows = 10_000;

    public class BuildingRow
    {
        public int RowNumber { get; set; }
        public string RawJson { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public string? Error { get; set; }

        public string Name { get; set; } = string.Empty;
        public string? Location { get; set; }
        public string? Type { get; set; }
        public string? ResidencyType { get; set; }
        public decimal MinPrice { get; set; }
        public decimal MaxPrice { get; set; }
        public string? Description { get; set; }
    }

    public class UnitRow
    {
        public int RowNumber { get; set; }
        public string RawJson { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public string? Error { get; set; }

        public string BuildingName { get; set; } = string.Empty;
        public string Floor { get; set; } = string.Empty;
        public string UnitNumber { get; set; } = string.Empty;
        public int? NumberOfBedrooms { get; set; }
        public int? NumberOfBathrooms { get; set; }
        public double? ApartmentSurface { get; set; }
        public double? BalconySurface { get; set; }
        public double? TerraceSurface { get; set; }
        public double? GardenSurface { get; set; }
        public double? TotalSurface { get; set; }
        public string? View { get; set; }
        public string? Orientation { get; set; }
        public decimal? LatestPrice { get; set; }
    }

    public class ParsedWorkbook
    {
        public List<BuildingRow> Buildings { get; set; } = new();
        public List<UnitRow> Units { get; set; } = new();
        public string? StructuralError { get; set; }
    }

    public static ParsedWorkbook Parse(byte[] fileContent)
    {
        var result = new ParsedWorkbook();

        using var stream = new MemoryStream(fileContent);
        using var workbook = new XLWorkbook(stream);

        var buildingsSheet = workbook.Worksheets.FirstOrDefault(w =>
            string.Equals(w.Name, "Buildings", StringComparison.OrdinalIgnoreCase));
        var unitsSheet = workbook.Worksheets.FirstOrDefault(w =>
            string.Equals(w.Name, "Units", StringComparison.OrdinalIgnoreCase));

        if (buildingsSheet is null || unitsSheet is null)
        {
            result.StructuralError = "Le fichier doit contenir les onglets 'Buildings' et 'Units'.";
            return result;
        }

        var buildingRows = buildingsSheet.RowsUsed().Skip(1).ToList();
        var unitRows = unitsSheet.RowsUsed().Skip(1).ToList();

        if (buildingRows.Count + unitRows.Count > MaxRows)
        {
            result.StructuralError = $"Le fichier dépasse la limite de {MaxRows} lignes.";
            return result;
        }

        foreach (var row in buildingRows)
        {
            result.Buildings.Add(ParseBuildingRow(row));
        }

        foreach (var row in unitRows)
        {
            result.Units.Add(ParseUnitRow(row));
        }

        return result;
    }

    private static BuildingRow ParseBuildingRow(IXLRow row)
    {
        var r = new BuildingRow { RowNumber = row.RowNumber() };

        var name = row.Cell(1).GetString().Trim();
        var location = row.Cell(2).GetString().Trim();
        var type = row.Cell(3).GetString().Trim();
        var residencyType = row.Cell(4).GetString().Trim();
        var minPriceRaw = row.Cell(5).GetString().Trim();
        var maxPriceRaw = row.Cell(6).GetString().Trim();
        var description = row.Cell(7).GetString().Trim();

        r.RawJson = JsonSerializer.Serialize(new
        {
            name, location, type, residencyType, minPriceRaw, maxPriceRaw, description
        });

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add("Le nom du bâtiment est obligatoire.");
        }

        if (!TryParseDecimal(minPriceRaw, out var minPrice))
        {
            errors.Add("Prix minimum invalide.");
        }

        if (!TryParseDecimal(maxPriceRaw, out var maxPrice))
        {
            errors.Add("Prix maximum invalide.");
        }
        else if (minPrice > maxPrice && errors.Count == 0)
        {
            errors.Add("Le prix minimum doit être inférieur ou égal au prix maximum.");
        }

        r.Name = name;
        r.Location = string.IsNullOrWhiteSpace(location) ? null : location;
        r.Type = string.IsNullOrWhiteSpace(type) ? null : type;
        r.ResidencyType = string.IsNullOrWhiteSpace(residencyType) ? null : residencyType;
        r.MinPrice = minPrice;
        r.MaxPrice = maxPrice;
        r.Description = string.IsNullOrWhiteSpace(description) ? null : description;

        r.IsValid = errors.Count == 0;
        r.Error = errors.Count == 0 ? null : string.Join(" ", errors);
        return r;
    }

    private static UnitRow ParseUnitRow(IXLRow row)
    {
        var r = new UnitRow { RowNumber = row.RowNumber() };

        var buildingName = row.Cell(1).GetString().Trim();
        var floor = row.Cell(2).GetString().Trim();
        var unitNumber = row.Cell(3).GetString().Trim();
        var bedroomsRaw = row.Cell(4).GetString().Trim();
        var bathroomsRaw = row.Cell(5).GetString().Trim();
        var apartmentSurfaceRaw = row.Cell(6).GetString().Trim();
        var balconySurfaceRaw = row.Cell(7).GetString().Trim();
        var terraceSurfaceRaw = row.Cell(8).GetString().Trim();
        var gardenSurfaceRaw = row.Cell(9).GetString().Trim();
        var totalSurfaceRaw = row.Cell(10).GetString().Trim();
        var view = row.Cell(11).GetString().Trim();
        var orientation = row.Cell(12).GetString().Trim();
        var priceRaw = row.Cell(13).GetString().Trim();

        r.RawJson = JsonSerializer.Serialize(new
        {
            buildingName, floor, unitNumber, bedroomsRaw, bathroomsRaw, apartmentSurfaceRaw,
            balconySurfaceRaw, terraceSurfaceRaw, gardenSurfaceRaw, totalSurfaceRaw, view, orientation, priceRaw
        });

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(buildingName))
        {
            errors.Add("Le nom du bâtiment est obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(unitNumber))
        {
            errors.Add("Le numéro du bien est obligatoire.");
        }

        r.BuildingName = buildingName;
        r.Floor = floor;
        r.UnitNumber = unitNumber;
        r.NumberOfBedrooms = TryParseIntOrNull(bedroomsRaw);
        r.NumberOfBathrooms = TryParseIntOrNull(bathroomsRaw);
        r.ApartmentSurface = TryParseDoubleOrNull(apartmentSurfaceRaw);
        r.BalconySurface = TryParseDoubleOrNull(balconySurfaceRaw);
        r.TerraceSurface = TryParseDoubleOrNull(terraceSurfaceRaw);
        r.GardenSurface = TryParseDoubleOrNull(gardenSurfaceRaw);
        r.TotalSurface = TryParseDoubleOrNull(totalSurfaceRaw);
        r.View = string.IsNullOrWhiteSpace(view) ? null : view;
        r.Orientation = string.IsNullOrWhiteSpace(orientation) ? null : orientation;
        r.LatestPrice = TryParseDecimalOrNull(priceRaw);

        r.IsValid = errors.Count == 0;
        r.Error = errors.Count == 0 ? null : string.Join(" ", errors);
        return r;
    }

    private static bool TryParseDecimal(string raw, out decimal value) =>
        decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out value);

    private static decimal? TryParseDecimalOrNull(string raw) =>
        decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static int? TryParseIntOrNull(string raw) =>
        int.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static double? TryParseDoubleOrNull(string raw) =>
        double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
}
