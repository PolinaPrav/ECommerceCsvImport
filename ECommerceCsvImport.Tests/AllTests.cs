using System.Collections.Generic;
using System.IO;
using ECommerceCsvImport.Models;
using ECommerceCsvImport.Services;
using Xunit;

namespace ECommerceCsvImport.Tests;

public class AllTests
{
    // 4.1. ТЕСТЫ РЕПОЗИТОРИЯ (1-4)

    [Fact]
    public void Repo_Test01_AddsProduct_GetAllContainsIt()
    {
        var repository = new InMemoryProductRepository();
        var product = new Product { Sku = "1001", Name = "Ноутбук Lenovo IdeaPad 3", Price = 45990m, Brand = "Lenovo" };
        repository.Add(product);
        var all = repository.GetAll();
        Assert.Single(all);
        Assert.Equal("1001", all[0].Sku);
        Assert.Equal(1, repository.Count);
    }

    [Fact]
    public void Repo_Test02_GetBySku_ReturnsProduct()
    {
        var repository = new InMemoryProductRepository();
        repository.Add(new Product { Sku = "1001", Name = "Test" });
        var found = repository.GetBySku("1001");
        Assert.NotNull(found);
        Assert.Equal("1001", found!.Sku);
    }

    [Fact]
    public void Repo_Test03_GetBySku_IgnoresCaseAndSpaces()
    {
        var repository = new InMemoryProductRepository();
        repository.Add(new Product { Sku = "1001", Name = "Test" });
        Assert.NotNull(repository.GetBySku(" 1001 "));
        Assert.NotNull(repository.GetBySku("1001"));
    }

    [Fact]
    public void Repo_Test04_EmptyRepository_ReturnsEmpty()
    {
        var repository = new InMemoryProductRepository();
        Assert.Equal(0, repository.Count);
        Assert.Empty(repository.GetAll());
        Assert.Null(repository.GetBySku("1001"));
    }

    // 4.2. ТЕСТЫ ПАРСЕРА CSV (5-8)

    [Fact]
    public void Parser_Test01_SimpleLine()
    {
        var result = CsvLineParser.Parse("1001,Ноутбук Lenovo IdeaPad 3,Бюджетный ноутбук,45990,RUB,Компьютеры,5,/images/laptop.jpg,true,Lenovo,24,IdeaPad3-15");
        Assert.Equal(12, result.Count);
        Assert.Equal("1001", result[0]);
    }

    [Fact]
    public void Parser_Test02_EmptyFields()
    {
        var result = CsvLineParser.Parse("1011,Блок питания Corsair CV550, ,4990,RUB,Компьютеры,9,,true,Corsair,60,CV550");
        Assert.Equal(12, result.Count);
        Assert.Equal("", result[2]);
        Assert.Equal("", result[7]);
    }

    [Fact]
    public void Parser_Test03_QuotedComma()
    {
        var result = CsvLineParser.Parse("1012, \"Ноутбук, игровой\", Тестовая строка, 59990, RUB, Компьютеры, 1, /images/notebook.jpg, true, ASUS, 36, VivoBook");
        Assert.Equal(12, result.Count);
        Assert.Equal("Ноутбук, игровой", result[1]);
    }

    [Fact]
    public void Parser_Test04_EscapedQuotes()
    {
        var result = CsvLineParser.Parse("1013, \"Название \"\"Special\"\"\", Описание, 100, RUB, Категория, 5, /img.jpg, true, Brand, 12, Model");
        Assert.Equal(12, result.Count);
        Assert.Equal("Название \"Special\"", result[1]);
    }

    // 4.3. ТЕСТЫ СЕРВИСА ИМПОРТА (9-31)

    private const string Header = "sku,name,description,price,currency,category_name,stock,image_url,is_active,brand,warranty_months,model_number";

    [Fact]
    public void Import_Test01_SuccessOneLine()
    {
        var repository = new InMemoryProductRepository();
        var service = new ProductImportService(repository);
        var lines = new[] { Header, "1001,Ноутбук Lenovo IdeaPad 3,Бюджетный ноутбук для работы и учебы,45990,RUB,Компьютеры,5,/images/laptop.jpg,true,Lenovo,24,IdeaPad 3-15" };
        var result = service.ImportFromLines(lines);
        Assert.Equal(1, result.TotalRows);
        Assert.Equal(1, result.ImportedCount);
        Assert.Empty(result.Errors);
        var product = repository.GetBySku("1001");
        Assert.Equal("Ноутбук Lenovo IdeaPad 3", product.Name);
        Assert.Equal(45990m, product.Price);
        Assert.Equal("Lenovo", product.Brand);
        Assert.Equal(24, product.WarrantyMonths);
        Assert.Equal("IdeaPad 3-15", product.ModelNumber);
    }

    [Fact]
    public void Import_Test02_SuccessMultipleLines()
    {
        var repository = new InMemoryProductRepository();
        var service = new ProductImportService(repository);
        var lines = new[] { Header, "1001,Ноутбук,Описание,45990,RUB,Компьютеры,5,/img.jpg,true,Lenovo,24,IdeaPad", "1002,Мышь,Описание,1290,RUB,Периферия,10,/img.jpg,true,Logitech,12,M185" };
        var result = service.ImportFromLines(lines);
        Assert.Equal(2, result.TotalRows);
        Assert.Equal(2, result.ImportedCount);
        Assert.Empty(result.Errors);
        Assert.Equal(2, repository.Count);
    }

    [Fact]
    public void Import_Test03_FileNotFound()
    {
        var service = new ProductImportService(new InMemoryProductRepository());
        var path = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString() + ".csv");
        var result = service.ImportFromCsv(path);
        Assert.Equal(0, result.TotalRows);
        Assert.Single(result.Errors);
        Assert.Contains("Файл не найден", result.Errors[0].Message);
    }

    [Fact]
    public void Import_Test04_EmptyFile()
    {
        var service = new ProductImportService(new InMemoryProductRepository());
        var path = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString() + ".csv");
        File.WriteAllText(path, string.Empty);
        var result = service.ImportFromCsv(path);
        Assert.Equal(0, result.TotalRows);
        Assert.Single(result.Errors);
        Assert.Contains("Данные для импорта отсутствуют", result.Errors[0].Message);
        File.Delete(path);
    }

    [Fact]
    public void Import_Test05_OnlyHeader()
    {
        var service = new ProductImportService(new InMemoryProductRepository());
        var lines = new[] { Header };
        var result = service.ImportFromLines(lines);
        Assert.Equal(0, result.TotalRows);
        Assert.Single(result.Errors);
        Assert.Contains("Данные для импорта отсутствуют", result.Errors[0].Message);
    }

    [Fact]
    public void Import_Test06_InvalidHeader()
    {
        var service = new ProductImportService(new InMemoryProductRepository());
        var lines = new[] { "sku,name,description,price,currency,category_name,stock,image_url,is_active" };
        var result = service.ImportFromLines(lines);
        Assert.Equal(0, result.TotalRows);
        Assert.Single(result.Errors);
        Assert.Contains("Неверные заголовки CSV", result.Errors[0].Message);
    }

    [Fact]
    public void Import_Test07_WrongFieldCount()
    {
        var service = new ProductImportService(new InMemoryProductRepository());
        var lines = new[] { Header, "1001,Ноутбук Lenovo IdeaPad 3" };
        var result = service.ImportFromLines(lines);
        Assert.Equal(1, result.TotalRows);
        Assert.Equal(0, result.ImportedCount);
        Assert.Contains("Неверное количество полей в строке", result.Errors[0].Message);
    }

    [Fact]
    public void Import_Test08_EmptySku()
    {
        var service = new ProductImportService(new InMemoryProductRepository());
        var lines = new[] { Header, " ,Ноутбук Lenovo IdeaPad 3,Бюджетный ноутбук,45990,RUB,Компьютеры,5,/images/laptop.jpg,true,Lenovo,24,IdeaPad 3-15" };
        var result = service.ImportFromLines(lines);
        Assert.Equal(0, result.ImportedCount);
        Assert.Contains("Поле sku не может быть пустым", result.Errors[0].Message);
    }

    [Fact]
    public void Import_Test09_EmptyName()
    {
        var service = new ProductImportService(new InMemoryProductRepository());
        var lines = new[] { Header, "1001,,Бюджетный ноутбук,45990,RUB,Компьютеры,5,/images/laptop.jpg,true,Lenovo,24,IdeaPad 3-15" };
        var result = service.ImportFromLines(lines);
        Assert.Equal(0, result.ImportedCount);
        Assert.Contains("Поле name не может быть пустым", result.Errors[0].Message);
    }

    [Fact]
    public void Import_Test10_NegativePrice()
    {
        var service = new ProductImportService(new InMemoryProductRepository());
        var lines = new[] { Header, "1003,Клавиатура A4Tech Bloody V3M,Механическая игровая клавиатура,-500,RUB,Периферия,7,/images/keyboard.jpg,true,A4Tech,24,V3M" };
        var result = service.ImportFromLines(lines);
        Assert.Equal(0, result.ImportedCount);
        Assert.Contains("Поле price должно быть числом больше 0", result.Errors[0].Message);
    }

    [Fact]
    public void Import_Test11_NonNumericPrice()
    {
        var service = new ProductImportService(new InMemoryProductRepository());
        var lines = new[] { Header, "1003,Клавиатура A4Tech Bloody V3M,Механическая игровая клавиатура,abc,RUB,Периферия,7,/images/keyboard.jpg,true,A4Tech,24,V3M" };
        var result = service.ImportFromLines(lines);
        Assert.Equal(0, result.ImportedCount);
        Assert.Contains("Поле price должно быть числом больше 0", result.Errors[0].Message);
    }

    [Fact]
    public void Import_Test12_NegativeStock()
    {
        var service = new ProductImportService(new InMemoryProductRepository());
        var lines = new[] { Header, "1004,Монитор Dell S2722QC,Игровой монитор 27 дюймов,29990,RUB,Компьютеры,-5,/images/monitor.jpg,true,Dell,24,S2722QC" };
        var result = service.ImportFromLines(lines);
        Assert.Equal(0, result.ImportedCount);
        Assert.Contains("Остаток на складе должен быть целым числом не меньше 0", result.Errors[0].Message);
    }

    [Fact]
    public void Import_Test13_InvalidIsActive()
    {
        var service = new ProductImportService(new InMemoryProductRepository());
        var lines = new[] { Header, "1007,Webcam Logitech C920,Веб-камера Full HD,4990,RUB,Периферия,6,/images/webcam.jpg,yes,Logitech,24,C920" };
        var result = service.ImportFromLines(lines);
        Assert.Equal(0, result.ImportedCount);
        Assert.Contains("Поле is_active должно иметь значение true, false, 1 или 0", result.Errors[0].Message);
    }

    [Fact]
    public void Import_Test14_MissingBrand()
    {
        var service = new ProductImportService(new InMemoryProductRepository());
        var lines = new[] { Header, "1005,Видеокарта NVIDIA GeForce RTX 3060,,24990,RUB,Компьютеры,4,/images/gpu.jpg,true,,36,RTX3060" };
        var result = service.ImportFromLines(lines);
        Assert.Equal(0, result.ImportedCount);
        Assert.Contains("Поле brand обязательно для варианта «Электроника»", result.Errors[0].Message);
    }

    [Fact]
    public void Import_Test15_WarrantyLessThanZero()
    {
        var service = new ProductImportService(new InMemoryProductRepository());
        var lines = new[] { Header, "1006,Накопитель SSD Samsung 870 EVO 1TB,Внутренний твердотельный накопитель,5990,RUB,Компьютеры,12,/images/ssd.jpg,true,Samsung,-1,870EVO-1T" };
        var result = service.ImportFromLines(lines);
        Assert.Equal(0, result.ImportedCount);
        Assert.Contains("Гарантия должна быть целым числом от 0 до 60 месяцев", result.Errors[0].Message);
    }

    [Fact]
    public void Import_Test16_WarrantyGreaterThan60()
    {
        var service = new ProductImportService(new InMemoryProductRepository());
        var lines = new[] { Header, "1006,Накопитель SSD Samsung 870 EVO 1TB,Внутренний твердотельный накопитель,5990,RUB,Компьютеры,12,/images/ssd.jpg,true,Samsung,61,870EVO-1T" };
        var result = service.ImportFromLines(lines);
        Assert.Equal(0, result.ImportedCount);
        Assert.Contains("Гарантия должна быть целым числом от 0 до 60 месяцев", result.Errors[0].Message);
    }

    [Fact]
    public void Import_Test17_DuplicateSkuInFile()
    {
        var repository = new InMemoryProductRepository();
        var service = new ProductImportService(repository);
        var lines = new[] { Header, "1001,Ноутбук Lenovo IdeaPad 3,Бюджетный ноутбук для работы и учебы,45990,RUB,Компьютеры,5,/images/laptop.jpg,true,Lenovo,24,IdeaPad 3-15", "1001,Ноутбук ASUS VivoBook,Другой ноутбук,52990,RUB,Компьютеры,7,/images/asus.jpg,true,ASUS,24,VivoBook" };
        var result = service.ImportFromLines(lines);
        Assert.Equal(2, result.TotalRows);
        Assert.Equal(1, result.ImportedCount);
        Assert.Single(result.Errors);
        Assert.Contains("Артикул товара уже существует", result.Errors[0].Message);
        var product = repository.GetBySku("1001");
        Assert.Equal("Ноутбук Lenovo IdeaPad 3", product.Name);
    }

    [Fact]
    public void Import_Test18_DuplicateSkuInRepository()
    {
        var repository = new InMemoryProductRepository();
        repository.Add(new Product { Sku = "1001", Name = "Old" });
        var service = new ProductImportService(repository);
        var lines = new[] { Header, "1001,Ноутбук ASUS VivoBook,Другой ноутбук,52990,RUB,Компьютеры,7,/images/asus.jpg,true,ASUS,24,VivoBook" };
        var result = service.ImportFromLines(lines);
        Assert.Equal(1, repository.Count);
        Assert.Contains("Артикул товара уже существует", result.Errors[0].Message);
    }

    [Fact]
    public void Import_Test19_MultipleErrorsInRow()
    {
        var service = new ProductImportService(new InMemoryProductRepository());
        var lines = new[] { Header, " , ,Некорректная строка,-10,RUB,Компьютеры,-5,/images/x.jpg,yes,,61,Model" };
        var result = service.ImportFromLines(lines);
        Assert.Equal(0, result.ImportedCount);
        Assert.True(result.Errors.Count >= 4);
    }

    [Fact]
    public void Import_Test20_EmptyOptionalFieldsAllowed()
    {
        var repository = new InMemoryProductRepository();
        var service = new ProductImportService(repository);
        var lines = new[] { Header, "1011,Блок питания Corsair CV550, ,4990,RUB,Компьютеры,9,,true,Corsair,60,CV550" };
        var result = service.ImportFromLines(lines);
        Assert.Equal(1, result.ImportedCount);
        var product = repository.GetBySku("1011");
        Assert.Equal("", product.Description);
        Assert.Equal("", product.ImageUrl);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Import_Test21_EmptyCurrency()
    {
        var repository = new InMemoryProductRepository();
        var service = new ProductImportService(repository);
        var lines = new[] { Header, "1011,Блок питания Corsair CV550,,4990,,Компьютеры,9,,true,Corsair,60,CV550" };
        service.ImportFromLines(lines);
        var product = repository.GetBySku("1011");
        Assert.Equal("RUB", product.Currency);
    }

    [Fact]
    public void Import_Test22_EmptyCategory()
    {
        var repository = new InMemoryProductRepository();
        var service = new ProductImportService(repository);
        var lines = new[] { Header, "1011,Блок питания Corsair CV550,,4990,RUB,,9,,true,Corsair,60,CV550" };
        service.ImportFromLines(lines);
        var product = repository.GetBySku("1011");
        Assert.Equal("Без категории", product.CategoryName);
    }

    [Fact]
    public void Import_Test23_CyrillicSavedCorrectly()
    {
        var repository = new InMemoryProductRepository();
        var service = new ProductImportService(repository);
        var lines = new[] { Header, "1001,Ноутбук Lenovo IdeaPad 3,Бюджетный ноутбук для работы и учебы,45990,RUB,Компьютеры,5,/images/laptop.jpg,true,Lenovo,24,IdeaPad 3-15" };
        service.ImportFromLines(lines);
        var product = repository.GetBySku("1001");
        Assert.Equal("Ноутбук Lenovo IdeaPad 3", product.Name);
        Assert.Equal("Бюджетный ноутбук для работы и учебы", product.Description);
    }
}