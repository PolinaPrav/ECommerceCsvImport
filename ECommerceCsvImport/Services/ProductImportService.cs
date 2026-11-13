using System.Globalization;
using ECommerceCsvImport.Models;

namespace ECommerceCsvImport.Services;

public class ProductImportService : IProductImportService
{
    private readonly IProductRepository _repository;

    private static readonly string[] RequiredHeaders = new[]
    {
        "sku", "name", "description", "price", "currency", "category_name",
        "stock", "image_url", "is_active", "brand", "warranty_months", "model_number"
    };

    public ProductImportService(IProductRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public ImportResult ImportFromCsv(string filePath)
    {
        var result = new ImportResult();

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            result.Errors.Add(new ImportError { RowNumber = 0, Message = "Файл не найден" });
            return result;
        }

        try
        {
            var lines = File.ReadAllLines(filePath);
            return ImportFromLines(lines);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            result.Errors.Add(new ImportError { RowNumber = 0, Message = $"Ошибка чтения файла: {ex.Message}" });
            return result;
        }
    }

    public ImportResult ImportFromLines(IEnumerable<string> lines)
    {
        var result = new ImportResult();
        var lineList = lines.ToList();

        if (lineList.Count == 0 || lineList.All(string.IsNullOrWhiteSpace))
        {
            result.Errors.Add(new ImportError { RowNumber = 0, Message = "Данные для импорта отсутствуют" });
            return result;
        }

        int headerIndex = lineList.FindIndex(l => !string.IsNullOrWhiteSpace(l));
        string headerLine = lineList[headerIndex];
        int physicalRowNumber = headerIndex + 1;

        var headers = CsvLineParser.Parse(headerLine).Select(h => h.Trim().ToLowerInvariant()).ToList();

        if (RequiredHeaders.Any(req => !headers.Contains(req)))
        {
            result.Errors.Add(new ImportError { RowNumber = physicalRowNumber, Message = "Неверные заголовки CSV" });
            return result;
        }

        var columnMap = new Dictionary<string, int>();
        for (int i = 0; i < headers.Count; i++)
        {
            if (!columnMap.ContainsKey(headers[i]))
                columnMap[headers[i]] = i;
        }

        var importedSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = headerIndex + 1; i < lineList.Count; i++)
        {
            string line = lineList[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            physicalRowNumber = i + 1;
            result.TotalRows++;

            var values = CsvLineParser.Parse(line);

            if (values.Count != headers.Count)
            {
                result.Errors.Add(new ImportError { RowNumber = physicalRowNumber, Message = "Неверное количество полей в строке" });
                continue;
            }

            string GetValue(string col) => columnMap.TryGetValue(col, out int idx) && idx < values.Count ? values[idx] : string.Empty;

            var rowErrors = new List<ImportError>();

            string sku = GetValue("sku").Trim();
            string name = GetValue("name").Trim();
            string brand = GetValue("brand").Trim();

            if (string.IsNullOrWhiteSpace(sku))
                rowErrors.Add(new ImportError { RowNumber = physicalRowNumber, Message = "Поле sku не может быть пустым" });

            if (string.IsNullOrWhiteSpace(name))
                rowErrors.Add(new ImportError { RowNumber = physicalRowNumber, Message = "Поле name не может быть пустым" });

            if (!decimal.TryParse(GetValue("price"), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal price) || price <= 0)
                rowErrors.Add(new ImportError { RowNumber = physicalRowNumber, Message = "Поле price должно быть числом больше 0" });

            if (!int.TryParse(GetValue("stock"), out int stock) || stock < 0)
                rowErrors.Add(new ImportError { RowNumber = physicalRowNumber, Message = "Остаток на складе должен быть целым числом не меньше 0" });

            if (!TryParseBool(GetValue("is_active"), out bool isActive))
                rowErrors.Add(new ImportError { RowNumber = physicalRowNumber, Message = "Поле is_active должно иметь значение true, false, 1 или 0" });

            if (string.IsNullOrWhiteSpace(brand))
                rowErrors.Add(new ImportError { RowNumber = physicalRowNumber, Message = "Поле brand обязательно для варианта «Электроника»" });

            if (!int.TryParse(GetValue("warranty_months"), out int warranty) || warranty < 0 || warranty > 60)
                rowErrors.Add(new ImportError { RowNumber = physicalRowNumber, Message = "Гарантия должна быть целым числом от 0 до 60 месяцев" });

            if (rowErrors.Count > 0)
            {
                result.Errors.AddRange(rowErrors);
                continue;
            }

            if (_repository.GetBySku(sku) != null || importedSkus.Contains(sku))
            {
                result.Errors.Add(new ImportError { RowNumber = physicalRowNumber, Message = "Артикул товара уже существует" });
                continue;
            }

            var product = new Product
            {
                Sku = sku,
                Name = name,
                Description = GetValue("description").Trim(),
                Price = price,
                Currency = NormalizeCurrency(GetValue("currency")),
                CategoryName = NormalizeCategory(GetValue("category_name")),
                StockQuantity = stock,
                ImageUrl = GetValue("image_url").Trim(),
                IsActive = isActive,
                Brand = brand,
                WarrantyMonths = warranty,
                ModelNumber = GetValue("model_number").Trim()
            };

            _repository.Add(product);
            importedSkus.Add(sku);
            result.ImportedCount++;
        }

        if (result.TotalRows == 0 && result.Errors.Count == 0)
            result.Errors.Add(new ImportError { RowNumber = 0, Message = "Данные для импорта отсутствуют" });

        return result;
    }

    private static bool TryParseBool(string value, out bool result)
    {
        var v = value.Trim().ToLowerInvariant();
        if (v == "true" || v == "1") { result = true; return true; }
        if (v == "false" || v == "0") { result = false; return true; }
        result = false; return false;
    }

    private static string NormalizeCurrency(string value)
        => string.IsNullOrWhiteSpace(value) ? "RUB" : value.Trim().ToUpperInvariant();

    private static string NormalizeCategory(string value)
        => string.IsNullOrWhiteSpace(value) ? "Без категории" : value.Trim();
}