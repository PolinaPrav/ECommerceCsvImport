using Microsoft.AspNetCore.Mvc;
using ECommerceCsvImport.Models;
using ECommerceCsvImport.Services;
using System.IO;

namespace ECommerceCsvImport.Controllers;

public class ProductsController : Controller
{
    private const string EmptyPathMessage = "Укажите путь к CSV-файлу";

    private readonly IProductRepository _repository;
    private readonly IProductImportService _importService;
    private readonly IWebHostEnvironment _environment;

    public ProductsController(
        IProductRepository repository,
        IProductImportService importService,
        IWebHostEnvironment environment)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _importService = importService ?? throw new ArgumentNullException(nameof(importService));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    [HttpGet]
    public IActionResult Index()
    {
        var viewModel = new ProductsIndexViewModel
        {
            Products = _repository.GetAll()
        };
        return View(viewModel);
    }

    [HttpGet]
    public IActionResult Import()
    {
        return View(new ImportViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Import(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            var emptyPathResult = new ImportResult();
            emptyPathResult.Errors.Add(new ImportError { RowNumber = 0, Message = EmptyPathMessage });

            return View("Import", new ImportViewModel { Result = emptyPathResult });
        }

        var result = _importService.ImportFromCsv(ResolveFilePath(filePath));

        return View("Import", new ImportViewModel
        {
            FilePath = filePath,
            Result = result
        });
    }

    private string ResolveFilePath(string filePath)
    {
        var trimmed = filePath.Trim();

        if (System.IO.File.Exists(trimmed))
            return trimmed;

        return Path.Combine(_environment.WebRootPath, trimmed.TrimStart('/', '\\'));
    }
}
