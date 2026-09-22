using System.Globalization;
using System.Text.Json;
using ClosedXML.Excel;

namespace ProjectAPI.Api.Application.Imports;

/// <summary>
/// §5.11/§23 — parses a workbook where EACH WORKSHEET IS ONE BUILDING: the
/// sheet's tab name is the building's name, row 2 carries the building's own
/// fields, and row 5 onward lists its units. This mirrors how an admin
/// actually fills the file: download the one-sheet "Exemple" template,
/// duplicate that tab once per real building (Excel's own "Move or Copy →
/// Create a copy"), rename each copy, then fill it in.
///
/// Every sheet is parsed defensively: a single malformed cell fails that ONE
/// row (recorded with a clear message) rather than throwing and losing the
/// admin's whole upload; a single malformed SHEET fails just that building.
/// Only a structural problem (no building sheets at all, a corrupt file)
/// aborts the parse outright.
/// </summary>
public static class ImportWorkbookReader
{
    public const int MaxRows = 10_000;

    /// <summary>The template's own dropdown-source sheet — never a building.</summary>
    public const string LookupSheetName = "Lookup";

    /// <summary>Fixed layout every building sheet follows (see GenerateImportTemplateQuery).</summary>
    public const int BuildingInfoRow = 2;
    public const int UnitHeaderRow = 4;
    public const int UnitFirstDataRow = 5;

    public class BuildingRow
    {
        public int RowNumber { get; set; }
        public string RawJson { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public string? Error { get; set; }

        /// <summary>The sheet's tab name — the building's identity. Not a cell value.</summary>
        public string Name { get; set; } = string.Empty;
        public string? Location { get; set; }
        public string? Type { get; set; }
        public string? ResidencyType { get; set; }
        public string? Description { get; set; }
        /// <summary>3D tour link — settable at creation on the normal single-building form too (never on a later edit there).</summary>
        public string? Module3DLink { get; set; }
    }

    public class UnitRow
    {
        public int RowNumber { get; set; }
        public string RawJson { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public string? Error { get; set; }

        /// <summary>The building's sheet/tab name — carried on every unit row for the same cross-checks the old flat layout used.</summary>
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
        /// <summary>Must match a TypeBien already linked to the target project (case-insensitive) — validated in ValidateImportBatchHandler. Blank falls back to the bedroom-count match every other unit-creation path already uses.</summary>
        public string? TypeBien { get; set; }
        public double? SaleableValue { get; set; }
        public double? SaleableValue1 { get; set; }
        public decimal? PriceSaleableValue { get; set; }
        public decimal? PriceSaleableValue1 { get; set; }
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

        var buildingSheets = workbook.Worksheets
            .Where(w => !string.Equals(w.Name, LookupSheetName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (buildingSheets.Count == 0)
        {
            result.StructuralError = "Le fichier ne contient aucun onglet de bâtiment (chaque onglet doit représenter un bâtiment).";
            return result;
        }

        var totalRows = 0;
        foreach (var sheet in buildingSheets)
        {
            var unitRows = sheet.RowsUsed().Where(r => r.RowNumber() >= UnitFirstDataRow).ToList();
            totalRows += 1 + unitRows.Count; // the building row itself + its units

            if (totalRows > MaxRows)
            {
                result.StructuralError = $"Le fichier dépasse la limite de {MaxRows} lignes.";
                return result;
            }

            result.Buildings.Add(SafeParseBuilding(sheet));

            foreach (var row in unitRows)
            {
                result.Units.Add(SafeParseUnit(row, sheet.Name.Trim()));
            }
        }

        return result;
    }

    /// <summary>A malformed sheet fails just that one building row rather than the whole upload.</summary>
    private static BuildingRow SafeParseBuilding(IXLWorksheet sheet)
    {
        try
        {
            return ParseBuildingRow(sheet);
        }
        catch (Exception ex)
        {
            return new BuildingRow
            {
                RowNumber = BuildingInfoRow,
                Name = sheet.Name.Trim(),
                RawJson = "{}",
                IsValid = false,
                Error = $"Onglet '{sheet.Name}' illisible : {ex.Message}"
            };
        }
    }

    /// <summary>A malformed row fails just that one unit rather than the whole sheet.</summary>
    private static UnitRow SafeParseUnit(IXLRow row, string buildingName)
    {
        try
        {
            return ParseUnitRow(row, buildingName);
        }
        catch (Exception ex)
        {
            var rowNumber = row.RowNumber();
            return new UnitRow
            {
                RowNumber = rowNumber,
                BuildingName = buildingName,
                RawJson = "{}",
                IsValid = false,
                Error = $"Ligne illisible ('{buildingName}' #{rowNumber}) : {ex.Message}"
            };
        }
    }

    private static BuildingRow ParseBuildingRow(IXLWorksheet sheet)
    {
        var name = sheet.Name.Trim();
        var row = sheet.Row(BuildingInfoRow);
        var r = new BuildingRow { RowNumber = BuildingInfoRow, Name = name };

        var location = row.Cell(1).GetString().Trim();
        var type = row.Cell(2).GetString().Trim();
        var residencyType = row.Cell(3).GetString().Trim();
        var description = row.Cell(4).GetString().Trim();
        var module3DLink = row.Cell(5).GetString().Trim();

        r.RawJson = JsonSerializer.Serialize(new { name, location, type, residencyType, description, module3DLink });

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add("Le nom de l'onglet (= nom du bâtiment) est vide.");
        }

        r.Location = string.IsNullOrWhiteSpace(location) ? null : location;
        r.Type = string.IsNullOrWhiteSpace(type) ? null : type;
        r.ResidencyType = string.IsNullOrWhiteSpace(residencyType) ? null : residencyType;
        r.Description = string.IsNullOrWhiteSpace(description) ? null : description;
        r.Module3DLink = string.IsNullOrWhiteSpace(module3DLink) ? null : module3DLink;

        r.IsValid = errors.Count == 0;
        r.Error = errors.Count == 0 ? null : string.Join(" ", errors);
        return r;
    }

    private static UnitRow ParseUnitRow(IXLRow row, string buildingName)
    {
        var r = new UnitRow { RowNumber = row.RowNumber(), BuildingName = buildingName };

        var floor = row.Cell(1).GetString().Trim();
        var unitNumber = row.Cell(2).GetString().Trim();
        var bedroomsRaw = row.Cell(3).GetString().Trim();
        var bathroomsRaw = row.Cell(4).GetString().Trim();
        var apartmentSurfaceRaw = row.Cell(5).GetString().Trim();
        var balconySurfaceRaw = row.Cell(6).GetString().Trim();
        var terraceSurfaceRaw = row.Cell(7).GetString().Trim();
        var gardenSurfaceRaw = row.Cell(8).GetString().Trim();
        var totalSurfaceRaw = row.Cell(9).GetString().Trim();
        var view = row.Cell(10).GetString().Trim();
        var orientation = row.Cell(11).GetString().Trim();
        var priceRaw = row.Cell(12).GetString().Trim();
        var typeBien = row.Cell(13).GetString().Trim();
        var saleableValueRaw = row.Cell(14).GetString().Trim();
        var saleableValue1Raw = row.Cell(15).GetString().Trim();
        var priceSaleableValueRaw = row.Cell(16).GetString().Trim();
        var priceSaleableValue1Raw = row.Cell(17).GetString().Trim();

        r.RawJson = JsonSerializer.Serialize(new
        {
            buildingName, floor, unitNumber, bedroomsRaw, bathroomsRaw, apartmentSurfaceRaw,
            balconySurfaceRaw, terraceSurfaceRaw, gardenSurfaceRaw, totalSurfaceRaw, view, orientation, priceRaw,
            typeBien, saleableValueRaw, saleableValue1Raw, priceSaleableValueRaw, priceSaleableValue1Raw
        });

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(floor))
        {
            errors.Add("L'étage est obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(unitNumber))
        {
            errors.Add("Le numéro du bien est obligatoire.");
        }

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
        r.TypeBien = string.IsNullOrWhiteSpace(typeBien) ? null : typeBien;
        r.SaleableValue = TryParseDoubleOrNull(saleableValueRaw);
        r.SaleableValue1 = TryParseDoubleOrNull(saleableValue1Raw);
        r.PriceSaleableValue = TryParseDecimalOrNull(priceSaleableValueRaw);
        r.PriceSaleableValue1 = TryParseDecimalOrNull(priceSaleableValue1Raw);

        r.IsValid = errors.Count == 0;
        r.Error = errors.Count == 0 ? null : string.Join(" ", errors);
        return r;
    }

    private static decimal? TryParseDecimalOrNull(string raw) =>
        decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static int? TryParseIntOrNull(string raw) =>
        int.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static double? TryParseDoubleOrNull(string raw) =>
        double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
}
