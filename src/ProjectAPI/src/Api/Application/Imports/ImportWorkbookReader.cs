using System.Globalization;
using System.Text.Json;
using ClosedXML.Excel;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using ProjectAPI.Api.Application.Units.Pricing;

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
        public string? Description { get; set; }
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
        public double? SaleableValue { get; set; }
        public double? SaleableValue1 { get; set; }
        public decimal? PriceSaleableValue { get; set; }
        public decimal? PriceSaleableValue1 { get; set; }
        public decimal? LatestPrice { get; set; }
        public string? View { get; set; }
        public string? Orientation { get; set; }
        /// <summary>Must match a TypeBien already linked to the target project (case-insensitive) — validated in ValidateImportBatchHandler. Blank falls back to the bedroom-count match every other unit-creation path already uses.</summary>
        public string? TypeBien { get; set; }
    }

    public class ParsedWorkbook
    {
        public List<BuildingRow> Buildings { get; set; } = new();
        public List<UnitRow> Units { get; set; } = new();
        public string? StructuralError { get; set; }
    }

    public static ParsedWorkbook Parse(byte[] fileContent)
    {
        // The real commercial source supplied by the business is a legacy
        // .xls workbook. ClosedXML deliberately supports only .xlsx, so parse
        // that format directly instead of asking an administrator to retype it.
        if (IsLegacyXls(fileContent))
        {
            return ParseLegacyPricingWorkbook(fileContent);
        }

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

    private static bool IsLegacyXls(byte[] bytes) =>
        bytes.Length >= 8 && bytes[0] == 0xD0 && bytes[1] == 0xCF && bytes[2] == 0x11 && bytes[3] == 0xE0;

    /// <summary>Reads one legacy pricing sheet per building by matching its visible headers, not fixed column positions.</summary>
    private static ParsedWorkbook ParseLegacyPricingWorkbook(byte[] fileContent)
    {
        var result = new ParsedWorkbook();
        using var stream = new MemoryStream(fileContent);
        IWorkbook workbook = new HSSFWorkbook(stream);
        var totalRows = 0;

        for (var s = 0; s < workbook.NumberOfSheets; s++)
        {
            var sheet = workbook.GetSheetAt(s);
            var headers = FindPricingHeaders(sheet);
            if (headers is null)
            {
                // Synthèse is calculated from all buildings and is deliberately
                // not imported as stock. Non-building notes are skipped too.
                continue;
            }

            var buildingName = sheet.SheetName.Trim();
            result.Buildings.Add(new BuildingRow
            {
                RowNumber = headers.Value.rowIndex + 1,
                Name = buildingName,
                IsValid = !string.IsNullOrWhiteSpace(buildingName),
                RawJson = JsonSerializer.Serialize(new { name = buildingName })
            });

            for (var r = headers.Value.rowIndex + 1; r <= sheet.LastRowNum; r++)
            {
                var row = sheet.GetRow(r);
                if (row is null || IsBlank(row)) continue;
                var unit = ParseLegacyUnitRow(row, buildingName, headers.Value.columns);
                if (string.IsNullOrWhiteSpace(unit.UnitNumber)) continue; // totals / notes below the grid
                result.Units.Add(unit);
                totalRows++;
                if (totalRows > MaxRows)
                {
                    result.StructuralError = $"Le fichier dépasse la limite de {MaxRows} lignes.";
                    return result;
                }
            }
        }

        if (result.Buildings.Count == 0)
        {
            result.StructuralError = "Aucun onglet d'immeuble avec les colonnes ETG et N° APP n'a été trouvé.";
        }
        return result;
    }

    private static (int rowIndex, Dictionary<string, int> columns)? FindPricingHeaders(ISheet sheet)
    {
        for (var r = sheet.FirstRowNum; r <= Math.Min(sheet.LastRowNum, 20); r++)
        {
            var row = sheet.GetRow(r);
            if (row is null) continue;
            var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var c = row.FirstCellNum; c >= 0 && c < row.LastCellNum; c++)
            {
                var header = NormalizeHeader(CellText(row.GetCell(c)));
                if (!string.IsNullOrWhiteSpace(header)) columns[header] = c;
            }
            if (FindColumn(columns, "ETG", "ETAGE") >= 0 && FindColumn(columns, "N APP", "NO APP", "N° APP") >= 0)
                return (r, columns);
        }
        return null;
    }

    private static UnitRow ParseLegacyUnitRow(IRow row, string buildingName, Dictionary<string, int> columns)
    {
        string Read(params string[] aliases)
        {
            var index = FindColumn(columns, aliases);
            return index < 0 ? string.Empty : CellText(row.GetCell(index)).Trim();
        }

        var apartment = TryParseDoubleOrNull(Read("SUP APP"));
        var balcony = TryParseDoubleOrNull(Read("SUP BALCON"));
        var terrace = TryParseDoubleOrNull(Read("SUP TERRASSE"));
        var garden = TryParseDoubleOrNull(Read("SUP JARDIN PRIVE", "SUP JARDIN PRIVÉ"));
        var priceSv = TryParseDecimalOrNull(Read("PRIX SV"));
        var priceSv1 = TryParseDecimalOrNull(Read("PRIX SV1"));
        // The business workbook uses several dated names for the final price.
        var latestPrice = TryParseDecimalOrNull(Read("PRIX TOTAL 161124", "PRIX 161124", "PRIX 06 11", "PRIX"));
        var calculated = UnitPricingCalculator.Calculate(new UnitPricingInput(apartment, balcony, terrace, garden, priceSv, priceSv1, latestPrice));

        var floor = Read("ETG", "ETAGE");
        var unitNumber = Read("N APP", "NO APP", "N° APP");
        var result = new UnitRow
        {
            RowNumber = row.RowNum + 1,
            BuildingName = buildingName,
            Floor = floor,
            UnitNumber = unitNumber,
            NumberOfBedrooms = TryParseIntOrNull(Read("NBR CHB")),
            NumberOfBathrooms = TryParseIntOrNull(Read("NBR SDB")),
            ApartmentSurface = apartment,
            BalconySurface = balcony,
            TerraceSurface = terrace,
            GardenSurface = garden,
            TotalSurface = calculated.TotalSurface,
            SaleableValue = calculated.SaleableValue,
            SaleableValue1 = calculated.SaleableValue1,
            PriceSaleableValue = calculated.PriceSaleableValue,
            PriceSaleableValue1 = calculated.PriceSaleableValue1,
            LatestPrice = calculated.LatestPrice,
            View = EmptyToNull(Read("DONNE COTE", "DONNE CÔTÉ")),
            Orientation = EmptyToNull(Read("ORIENTATION"))
        };
        result.RawJson = JsonSerializer.Serialize(result);
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(floor)) errors.Add("L'étage est obligatoire.");
        if (string.IsNullOrWhiteSpace(unitNumber)) errors.Add("Le numéro du bien est obligatoire.");
        result.IsValid = errors.Count == 0;
        result.Error = errors.Count == 0 ? null : string.Join(" ", errors);
        return result;
    }

    private static int FindColumn(Dictionary<string, int> columns, params string[] aliases) =>
        aliases.Select(NormalizeHeader).Where(columns.ContainsKey).Select(alias => columns[alias]).DefaultIfEmpty(-1).First();

    private static string NormalizeHeader(string? value) => new string((value ?? string.Empty).Trim().ToUpperInvariant()
        .Normalize(System.Text.NormalizationForm.FormD).Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
        .Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray()).Replace("  ", " ").Trim();

    private static string CellText(ICell? cell)
    {
        if (cell is null) return string.Empty;
        return cell.CellType switch
        {
            CellType.Numeric => cell.NumericCellValue.ToString(CultureInfo.InvariantCulture),
            CellType.Boolean => cell.BooleanCellValue.ToString(),
            CellType.Formula => cell.CachedFormulaResultType == CellType.Numeric
                ? cell.NumericCellValue.ToString(CultureInfo.InvariantCulture)
                : cell.StringCellValue,
            _ => cell.ToString()
        };
    }

    private static bool IsBlank(IRow row) => row.Cells.All(c => string.IsNullOrWhiteSpace(CellText(c)));
    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

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
        var description = row.Cell(2).GetString().Trim();

        r.RawJson = JsonSerializer.Serialize(new { name, location, description });

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add("Le nom de l'onglet (= nom du bâtiment) est vide.");
        }

        r.Location = string.IsNullOrWhiteSpace(location) ? null : location;
        r.Description = string.IsNullOrWhiteSpace(description) ? null : description;

        r.IsValid = errors.Count == 0;
        r.Error = errors.Count == 0 ? null : string.Join(" ", errors);
        return r;
    }

    private static UnitRow ParseUnitRow(IXLRow row, string buildingName)
    {
        var r = new UnitRow { RowNumber = row.RowNumber(), BuildingName = buildingName };

        var floor = row.Cell(1).GetString().Trim();
        var unitNumber = row.Cell(2).GetString().Trim();
        var typeBien = row.Cell(3).GetString().Trim();
        var bedroomsRaw = row.Cell(4).GetString().Trim();
        var bathroomsRaw = row.Cell(5).GetString().Trim();
        var apartmentSurfaceRaw = row.Cell(6).GetString().Trim();
        var totalSurfaceRaw = row.Cell(7).GetString().Trim();
        var view = row.Cell(8).GetString().Trim();
        var orientation = row.Cell(9).GetString().Trim();

        r.RawJson = JsonSerializer.Serialize(new
        {
            buildingName, floor, unitNumber, typeBien, bedroomsRaw, bathroomsRaw,
            apartmentSurfaceRaw, totalSurfaceRaw, view, orientation
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
        r.TotalSurface = TryParseDoubleOrNull(totalSurfaceRaw);
        r.View = string.IsNullOrWhiteSpace(view) ? null : view;
        r.Orientation = string.IsNullOrWhiteSpace(orientation) ? null : orientation;
        r.TypeBien = string.IsNullOrWhiteSpace(typeBien) ? null : typeBien;

        r.IsValid = errors.Count == 0;
        r.Error = errors.Count == 0 ? null : string.Join(" ", errors);
        return r;
    }

    private static int? TryParseIntOrNull(string raw) =>
        int.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static double? TryParseDoubleOrNull(string raw) =>
        double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)
            || double.TryParse(raw, NumberStyles.Any, CultureInfo.GetCultureInfo("fr-FR"), out v) ? v : null;

    private static decimal? TryParseDecimalOrNull(string raw) =>
        decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)
            || decimal.TryParse(raw, NumberStyles.Any, CultureInfo.GetCultureInfo("fr-FR"), out v) ? v : null;
}
