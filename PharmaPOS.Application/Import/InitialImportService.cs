using System.Globalization;
using PharmaPOS.Application.Inventory;
using PharmaPOS.Application.Products;
using PharmaPOS.Application.Repositories;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;

// 엔티티 이름(Inventory)이 Application의 네임스페이스와 같아 그냥 쓰면 네임스페이스로 읽힌다.
using InventoryEntity = PharmaPOS.Domain.Entities.Inventory;

namespace PharmaPOS.Application.Import;

/// <summary>
/// IInitialImportService의 구현체.
///
/// 저장은 전부 기존 경로를 그대로 쓴다 — 상품은 ProductService(바코드 중복·내부 바코드 규칙이
/// 여기 있다), 재고는 IStockInRepository(입고와 같은 트랜잭션)를 거친다. 임포트가 자기만의
/// 저장 경로를 파면 화면으로 넣은 재고와 파일로 넣은 재고가 다른 규칙을 타게 된다.
/// </summary>
public class InitialImportService : IInitialImportService
{
    /// <summary>
    /// 유효기간에 쓸 수 있는 날짜 형식. 뜻이 흔들리지 않는 것만 받는다 —
    /// 03/04/2027이 3월 4일인지 4월 3일인지는 파일만 봐서는 알 수 없어 아예 받지 않는다.
    /// 연-월만 적힌 경우(2027-05)는 그 달의 마지막 날로 읽는다. 약 상자에 흔한 표기이고,
    /// 그 달 말일까지는 쓸 수 있다는 뜻이기 때문이다.
    /// </summary>
    private static readonly string[] FullDateFormats = ["yyyy-MM-dd", "yyyy/MM/dd", "yyyy.MM.dd", "yyyyMMdd"];
    private static readonly string[] MonthOnlyFormats = ["yyyy-MM", "yyyy/MM", "yyyy.MM"];

    private readonly IProductRepository _productRepository;
    private readonly IProductService _productService;
    private readonly IStockInRepository _stockInRepository;
    private readonly IImportHistoryRepository _importHistoryRepository;

    public InitialImportService(
        IProductRepository productRepository,
        IProductService productService,
        IStockInRepository stockInRepository,
        IImportHistoryRepository importHistoryRepository)
    {
        _productRepository = productRepository;
        _productService = productService;
        _stockInRepository = stockInRepository;
        _importHistoryRepository = importHistoryRepository;
    }

    public async Task<bool> WasAlreadyImportedAsync(ImportType importType, string fileHash)
    {
        try
        {
            return await _importHistoryRepository.ExistsAsync(importType, fileHash);
        }
        catch (Exception)
        {
            // 이력을 못 읽었다고 임포트를 막지는 않는다. 막으면 이력 테이블 하나 때문에
            // 초기 설치가 통째로 멈춘다.
            return false;
        }
    }

    // ── Products ────────────────────────────────────────────────────────────

    public async Task<ProductImportPlan> PlanProductsAsync(IReadOnlyList<ImportSourceRow> rows)
    {
        var headerError = ValidateHeaders(rows, InitialImportColumns.RequiredForProducts);

        if (headerError is not null)
        {
            return new ProductImportPlan { FileError = headerError };
        }

        IReadOnlyList<Product> existingProducts;

        try
        {
            existingProducts = await _productRepository.SearchAsync(string.Empty, null);
        }
        catch (Exception)
        {
            return new ProductImportPlan { FileError = "Existing products could not be loaded. Please try again." };
        }

        // 이미 등록된 상품. 상태(Active/Inactive)는 보지 않는다 — 비활성 상품과 같은 이름으로
        // 하나 더 만들면 목록에 같은 이름이 둘이 되고, 어느 쪽이 파는 것인지 알 수 없게 된다.
        var existingByName = new Dictionary<string, Product>(InitialImportColumns.ProductNameComparer);

        foreach (var product in existingProducts)
        {
            existingByName.TryAdd(InitialImportColumns.NormalizeProductName(product.ProductName), product);
        }

        var seenNames = new HashSet<string>(InitialImportColumns.ProductNameComparer);
        var toCreate = new List<ProductImportLine>();
        var toUpdate = new List<ProductImportLine>();
        var unchanged = new List<string>();
        var issues = new List<ImportIssue>();

        var totalRows = 0;
        var duplicateRowCount = 0;

        foreach (var row in rows)
        {
            if (row.IsEmpty)
            {
                continue;
            }

            totalRows++;

            var productName = InitialImportColumns.NormalizeProductName(
                row.Get(InitialImportColumns.ProductName));

            if (productName.Length == 0)
            {
                issues.Add(new ImportIssue(row.LineNumber, "product_name is empty."));
                continue;
            }

            // 같은 상품에 배치가 여러 개면 2행부터는 상품 정보가 비어 있다. 첫 행만 읽는다.
            if (!seenNames.Add(productName))
            {
                duplicateRowCount++;
                continue;
            }

            if (existingByName.TryGetValue(productName, out var existing))
            {
                var merged = MergeProduct(existing, row, out var hasChanges, out var mergeError);

                if (merged is null)
                {
                    issues.Add(new ImportIssue(row.LineNumber, mergeError!));
                    continue;
                }

                if (!hasChanges)
                {
                    // 상품명만 적힌 행(배치를 적으러 온 행)은 고칠 것이 없다.
                    unchanged.Add(productName);
                    continue;
                }

                toUpdate.Add(new ProductImportLine { LineNumber = row.LineNumber, Product = merged });
                continue;
            }

            var product = BuildProduct(row, productName, out var error);

            if (product is null)
            {
                issues.Add(new ImportIssue(row.LineNumber, error!));
                continue;
            }

            toCreate.Add(new ProductImportLine { LineNumber = row.LineNumber, Product = product });
        }

        return new ProductImportPlan
        {
            TotalRows = totalRows,
            ProductsToCreate = toCreate,
            ProductsToUpdate = toUpdate,
            DuplicateRowCount = duplicateRowCount,
            UnchangedNames = unchanged,
            Issues = issues
        };
    }

    /// <summary>한 행을 새 상품으로 바꾼다. 값이 잘못됐으면 null과 사유를 돌려준다.</summary>
    private static Product? BuildProduct(ImportSourceRow row, string productName, out string? error)
    {
        // 아래 세 값은 신규 상품에만 요구한다. 기존 상품을 고치는 행에는 없어도 된다.
        var unit = row.Get(InitialImportColumns.Unit);

        if (unit.Length == 0)
        {
            error = "unit is empty. It is required for a new product.";
            return null;
        }

        if (!TryReadPrice(row, InitialImportColumns.CostPrice, out var costPrice, out error))
        {
            return null;
        }

        if (costPrice is null)
        {
            error = "cost_price is empty. It is required for a new product.";
            return null;
        }

        if (!TryReadPrice(row, InitialImportColumns.SellingPrice, out var sellingPrice, out error))
        {
            return null;
        }

        if (sellingPrice is null)
        {
            error = "selling_price is empty. It is required for a new product.";
            return null;
        }

        if (!TryReadSafetyStock(row, out var safetyStock, out error)
            || !TryReadLooseSale(row, out var looseSale, out error)
            || !TryReadStatus(row, out var status, out error)
            || !TryReadDosageForm(row, out var dosageForm, out error))
        {
            return null;
        }

        var barcode = row.Get(InitialImportColumns.Barcode);

        return new Product
        {
            // ProductId와 CreatedAt은 ProductService가 저장 시점에 다시 채운다.
            ProductId = Guid.NewGuid().ToString(),
            ProductName = productName,
            Unit = unit,
            Barcode = NullIfEmpty(barcode),
            CostPrice = costPrice.Value,
            SellingPrice = sellingPrice.Value,
            SafetyStockLevel = safetyStock ?? 0,
            UnitsPerBox = looseSale?.UnitsPerBox ?? 1,
            UnitSellingPrice = looseSale?.LooseUnitPrice,
            GenericName = NullIfEmpty(row.Get(InitialImportColumns.GenericName)),
            Strength = NullIfEmpty(row.Get(InitialImportColumns.Strength)),
            DosageForm = dosageForm,
            AtcCode = NullIfEmpty(row.Get(InitialImportColumns.AtcCode)),
            IsCombination = ParseBoolean(row.Get(InitialImportColumns.IsCombination)),
            Manufacturer = NullIfEmpty(row.Get(InitialImportColumns.Manufacturer)),
            CountryOfOrigin = NullIfEmpty(row.Get(InitialImportColumns.CountryOfOrigin)),
            Status = status ?? EntityStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }

    /// <summary>
    /// 이미 등록된 상품에 파일의 값을 얹는다. <b>채워진 칸만</b> 고치고 빈 칸은 그대로 둔다 —
    /// 상품명만 적어 둔 행(배치를 적으러 온 행)이 기존 단가를 지워 버리면 안 되기 때문이다.
    /// 그래서 이 임포트로는 값을 비울 수 없다. 값을 지우는 일은 상품 수정 화면에서 한다.
    /// </summary>
    /// <param name="hasChanges">실제로 바뀐 값이 있는지. 없으면 저장할 이유가 없다.</param>
    private static Product? MergeProduct(
        Product existing, ImportSourceRow row, out bool hasChanges, out string? error)
    {
        hasChanges = false;

        var merged = new Product
        {
            ProductId = existing.ProductId,
            ProductName = existing.ProductName,
            Barcode = existing.Barcode,
            InternalBarcode = existing.InternalBarcode,
            GenericName = existing.GenericName,
            Strength = existing.Strength,
            DosageForm = existing.DosageForm,
            Unit = existing.Unit,
            Manufacturer = existing.Manufacturer,
            CountryOfOrigin = existing.CountryOfOrigin,
            CostPrice = existing.CostPrice,
            SellingPrice = existing.SellingPrice,
            SafetyStockLevel = existing.SafetyStockLevel,
            Status = existing.Status,
            CreatedAt = existing.CreatedAt,
            AtcCode = existing.AtcCode,
            IsCombination = existing.IsCombination,
            UnitsPerBox = existing.UnitsPerBox,
            UnitSellingPrice = existing.UnitSellingPrice,
            Category = existing.Category
        };

        var changed = false;

        ApplyText(row.Get(InitialImportColumns.Unit), merged.Unit,
            value => merged.Unit = value, ref changed);
        ApplyText(row.Get(InitialImportColumns.Barcode), merged.Barcode,
            value => merged.Barcode = value, ref changed);
        ApplyText(row.Get(InitialImportColumns.GenericName), merged.GenericName,
            value => merged.GenericName = value, ref changed);
        ApplyText(row.Get(InitialImportColumns.Strength), merged.Strength,
            value => merged.Strength = value, ref changed);
        ApplyText(row.Get(InitialImportColumns.AtcCode), merged.AtcCode,
            value => merged.AtcCode = value, ref changed);
        ApplyText(row.Get(InitialImportColumns.Manufacturer), merged.Manufacturer,
            value => merged.Manufacturer = value, ref changed);
        ApplyText(row.Get(InitialImportColumns.CountryOfOrigin), merged.CountryOfOrigin,
            value => merged.CountryOfOrigin = value, ref changed);

        if (!TryReadPrice(row, InitialImportColumns.CostPrice, out var costPrice, out error))
        {
            return null;
        }

        if (costPrice is not null && costPrice.Value != merged.CostPrice)
        {
            merged.CostPrice = costPrice.Value;
            changed = true;
        }

        if (!TryReadPrice(row, InitialImportColumns.SellingPrice, out var sellingPrice, out error))
        {
            return null;
        }

        if (sellingPrice is not null && sellingPrice.Value != merged.SellingPrice)
        {
            merged.SellingPrice = sellingPrice.Value;
            changed = true;
        }

        if (!TryReadSafetyStock(row, out var safetyStock, out error))
        {
            return null;
        }

        if (safetyStock is not null && safetyStock.Value != merged.SafetyStockLevel)
        {
            merged.SafetyStockLevel = safetyStock.Value;
            changed = true;
        }

        if (!TryReadLooseSale(row, out var looseSale, out error))
        {
            return null;
        }

        if (looseSale is not null
            && (looseSale.UnitsPerBox != merged.UnitsPerBox || looseSale.LooseUnitPrice != merged.UnitSellingPrice))
        {
            merged.UnitsPerBox = looseSale.UnitsPerBox;
            merged.UnitSellingPrice = looseSale.LooseUnitPrice;
            changed = true;
        }

        if (!TryReadStatus(row, out var status, out error))
        {
            return null;
        }

        if (status is not null && status.Value != merged.Status)
        {
            merged.Status = status.Value;
            changed = true;
        }

        if (!TryReadDosageForm(row, out var dosageForm, out error))
        {
            return null;
        }

        if (dosageForm is not null && dosageForm != merged.DosageForm)
        {
            merged.DosageForm = dosageForm;
            changed = true;
        }

        var isCombinationText = row.Get(InitialImportColumns.IsCombination);

        if (isCombinationText.Length > 0)
        {
            var isCombination = ParseBoolean(isCombinationText);

            if (isCombination != merged.IsCombination)
            {
                merged.IsCombination = isCombination;
                changed = true;
            }
        }

        hasChanges = changed;
        return merged;
    }

    /// <summary>값이 적혀 있고 지금 값과 다를 때만 넣는다.</summary>
    private static void ApplyText(string value, string? current, Action<string> set, ref bool changed)
    {
        if (value.Length == 0 || string.Equals(value, current, StringComparison.Ordinal))
        {
            return;
        }

        set(value);
        changed = true;
    }

    /// <summary>빈 칸이면 null(= 적지 않음), 값이 있으면 검사해서 돌려준다.</summary>
    private static bool TryReadPrice(
        ImportSourceRow row, string[] column, out decimal? price, out string? error)
    {
        price = null;
        error = null;

        var text = row.Get(column);

        if (text.Length == 0)
        {
            return true;
        }

        if (!TryParseDecimal(text, out var parsed) || parsed <= 0)
        {
            error = $"{column[0]} must be a number greater than zero.";
            return false;
        }

        price = parsed;
        return true;
    }

    private static bool TryReadSafetyStock(ImportSourceRow row, out int? safetyStock, out string? error)
    {
        safetyStock = null;
        error = null;

        var text = row.Get(InitialImportColumns.SafetyStock);

        if (text.Length == 0)
        {
            return true;
        }

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed < 0)
        {
            error = "safety_stock must be a whole number of zero or more.";
            return false;
        }

        safetyStock = parsed;
        return true;
    }

    private static bool TryReadStatus(ImportSourceRow row, out EntityStatus? status, out string? error)
    {
        status = null;
        error = null;

        var text = row.Get(InitialImportColumns.Status);

        if (text.Length == 0)
        {
            return true;
        }

        if (string.Equals(text, "Active", StringComparison.OrdinalIgnoreCase))
        {
            status = EntityStatus.Active;
            return true;
        }

        if (string.Equals(text, "Inactive", StringComparison.OrdinalIgnoreCase))
        {
            status = EntityStatus.Inactive;
            return true;
        }

        error = "status must be Active or Inactive.";
        return false;
    }

    /// <summary>
    /// 제형. 빈 칸이면 null(= 적지 않음)이다.
    ///
    /// 고정 목록이지만 파일은 현장에서 손으로 채우므로 대소문자·구분자·흔한 줄임말은 받아 준다.
    /// 그래도 못 읽으면 그 행을 오류로 남긴다 — 조용히 비워 두면 왜 안 들어갔는지 알 수 없고,
    /// 나중에 제형으로 거르는 조회가 빈 값을 "제형 없는 상품"으로 읽어 버린다.
    /// </summary>
    private static bool TryReadDosageForm(ImportSourceRow row, out DosageForm? dosageForm, out string? error)
    {
        dosageForm = null;
        error = null;

        var text = row.Get(InitialImportColumns.DosageForm);

        if (text.Length == 0)
        {
            return true;
        }

        // "Eye drops", "eye-drops", "EYEDROPS"가 모두 같은 값이어야 한다.
        var normalized = new string(text.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

        DosageForm? resolved = normalized switch
        {
            "tab" or "tabs" => DosageForm.Tablet,
            "cap" or "caps" => DosageForm.Capsule,
            "susp" => DosageForm.Suspension,
            "sachet" or "sachets" or "granule" or "granules" => DosageForm.Powder,
            "inj" or "injectable" or "ampoule" or "vial" => DosageForm.Injection,
            "oint" => DosageForm.Ointment,
            "drop" or "eyedrop" or "eyedrops" or "eardrop" or "eardrops" => DosageForm.Drops,
            _ => ParseName(normalized)
        };

        // 서식에는 복수형으로 적는 일이 흔하다("Tablets"). 원래 s로 끝나는 이름(Drops)은
        // 아래 첫 번째 시도에서 이미 걸리므로 잘려 나가지 않는다.
        static DosageForm? ParseName(string value)
        {
            if (Enum.TryParse<DosageForm>(value, ignoreCase: true, out var parsed))
            {
                return parsed;
            }

            return value.EndsWith('s')
                   && Enum.TryParse<DosageForm>(value[..^1], ignoreCase: true, out var singular)
                ? singular
                : null;
        }

        if (resolved is null)
        {
            error = $"dosage_form must be one of: {string.Join(", ", Enum.GetNames<DosageForm>())}.";
            return false;
        }

        dosageForm = resolved;
        return true;
    }

    /// <summary>소분 판매 설정. 두 칸이 다 비면 null(= 적지 않음)이다.</summary>
    private sealed record LooseSaleSetting(int UnitsPerBox, decimal LooseUnitPrice);

    private static bool TryReadLooseSale(ImportSourceRow row, out LooseSaleSetting? looseSale, out string? error)
    {
        looseSale = null;
        error = null;

        var unitsPerBoxText = row.Get(InitialImportColumns.UnitsPerBox);
        var loosePriceText = row.Get(InitialImportColumns.LooseUnitPrice);

        var hasUnitsPerBox = unitsPerBoxText.Length > 0;
        var hasLoosePrice = loosePriceText.Length > 0;

        if (!hasUnitsPerBox && !hasLoosePrice)
        {
            return true;
        }

        // 소분 판매는 "박스당 개수"와 "낱개가"가 함께 있어야 성립한다. 하나만 있으면
        // 어느 쪽이 빠진 것인지 알 수 없으므로 짐작하지 않고 그 행을 건너뛴다.
        if (hasUnitsPerBox != hasLoosePrice)
        {
            error = hasUnitsPerBox
                ? "units_per_box is set but loose_unit_price is empty. Fill both or leave both empty."
                : "loose_unit_price is set but units_per_box is empty. Fill both or leave both empty.";
            return false;
        }

        if (!int.TryParse(unitsPerBoxText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unitsPerBox)
            || unitsPerBox <= 1)
        {
            error = "units_per_box must be a whole number greater than 1.";
            return false;
        }

        if (!TryParseDecimal(loosePriceText, out var loosePrice) || loosePrice <= 0)
        {
            error = "loose_unit_price must be a number greater than zero.";
            return false;
        }

        looseSale = new LooseSaleSetting(unitsPerBox, loosePrice);
        return true;
    }

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    private static bool ParseBoolean(string value) =>
        value.Trim().ToLowerInvariant() is "true" or "1" or "y" or "yes";

    public async Task<ImportApplyResult> ApplyProductsAsync(
        ProductImportPlan plan, string fileHash, string? fileName, string facilityId)
    {
        var failures = new List<ImportIssue>();
        var success = 0;

        // 새로 만드는 것과 고치는 것은 저장 방식만 다르고 나머지 흐름이 같다.
        var work = plan.ProductsToCreate.Select(line => (Line: line, IsNew: true))
            .Concat(plan.ProductsToUpdate.Select(line => (Line: line, IsNew: false)));

        foreach (var (line, isNew) in work)
        {
            ProductSaveResult result;

            try
            {
                // 판매가가 원가보다 낮아도 그대로 넣는다. 약국이 실제로 그 값에 팔던 상품이고,
                // 수백 건짜리 파일에서 행마다 확인을 물을 수는 없다.
                result = await _productService.SaveProductAsync(
                    line.Product, isNew, acknowledgeLowerSellingPriceWarning: true);
            }
            catch (Exception)
            {
                failures.Add(new ImportIssue(line.LineNumber, "Product could not be saved."));
                continue;
            }

            if (result.IsSuccess)
            {
                success++;
            }
            else
            {
                failures.Add(new ImportIssue(line.LineNumber, result.Message ?? "Product could not be saved."));
            }
        }

        var warning = await RecordHistoryAsync(
            ImportType.Products, fileHash, fileName, facilityId,
            plan.TotalRows, success, failures.Count);

        return new ImportApplyResult
        {
            SuccessCount = success,
            FailureCount = failures.Count,
            Failures = failures,
            HistoryWarning = warning
        };
    }

    // ── Inventory ───────────────────────────────────────────────────────────

    public async Task<InventoryImportPlan> PlanInventoryAsync(IReadOnlyList<ImportSourceRow> rows)
    {
        var headerError = ValidateHeaders(rows, InitialImportColumns.RequiredForInventory);

        if (headerError is not null)
        {
            return new InventoryImportPlan { FileError = headerError };
        }

        IReadOnlyList<Product> existingProducts;

        try
        {
            existingProducts = await _productRepository.SearchAsync(string.Empty, null);
        }
        catch (Exception)
        {
            return new InventoryImportPlan { FileError = "Existing products could not be loaded. Please try again." };
        }

        var productsByName = new Dictionary<string, Product>(InitialImportColumns.ProductNameComparer);

        foreach (var product in existingProducts)
        {
            // 이름이 겹치면 먼저 등록된 것을 쓴다. 어차피 Products 임포트가 이름 중복을 막는다.
            productsByName.TryAdd(
                InitialImportColumns.NormalizeProductName(product.ProductName), product);
        }

        var batches = new List<InventoryImportLine>();
        var unmatched = new List<ImportIssue>();
        var issues = new List<ImportIssue>();
        var totalRows = 0;

        var today = DateTime.Today;

        foreach (var row in rows)
        {
            if (row.IsEmpty)
            {
                continue;
            }

            totalRows++;

            var productName = InitialImportColumns.NormalizeProductName(
                row.Get(InitialImportColumns.ProductName));

            if (productName.Length == 0)
            {
                issues.Add(new ImportIssue(row.LineNumber, "product_name is empty."));
                continue;
            }

            if (!productsByName.TryGetValue(productName, out var product))
            {
                unmatched.Add(new ImportIssue(
                    row.LineNumber, $"'{productName}' is not registered. Import products first."));
                continue;
            }

            if (product.Status != EntityStatus.Active)
            {
                issues.Add(new ImportIssue(row.LineNumber, $"'{productName}' is inactive."));
                continue;
            }

            if (!TryParseExpiry(row.Get(InitialImportColumns.ExpiryDate), today, out var expiryDate, out var expiryError))
            {
                issues.Add(new ImportIssue(row.LineNumber, expiryError!));
                continue;
            }

            var quantityText = row.Get(InitialImportColumns.Quantity);

            if (!int.TryParse(quantityText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity)
                || quantity <= 0)
            {
                issues.Add(new ImportIssue(row.LineNumber, "quantity must be a whole number greater than zero."));
                continue;
            }

            batches.Add(new InventoryImportLine
            {
                LineNumber = row.LineNumber,
                ProductId = product.ProductId,
                ProductName = product.ProductName,
                BatchNumber = row.Get(InitialImportColumns.BatchNumber),
                ExpiryDate = expiryDate,
                QuantityInUnits = quantity,
                UnitsPerBox = product.UnitsPerBox
            });
        }

        return new InventoryImportPlan
        {
            TotalRows = totalRows,
            BatchesToCreate = batches,
            UnmatchedRows = unmatched,
            Issues = issues
        };
    }

    /// <summary>
    /// 유효기간 칸을 읽는다. "N"이면 모름(0), 날짜면 미래여야 한다.
    /// 비워 두는 것은 허용하지 않는다 — 빈 칸이 "모름"인지 "적는 걸 잊었는지" 구분할 수 없고,
    /// 잊은 쪽을 모름으로 삼키면 만료 알림에서 조용히 빠진다.
    /// </summary>
    private static bool TryParseExpiry(string text, DateTime today, out long expiryDate, out string? error)
    {
        expiryDate = InventoryEntity.NoExpiryDate;
        error = null;

        if (text.Length == 0)
        {
            error = $"expiry_date is empty. Use '{InitialImportColumns.NoExpiryMarker}' if the expiry date is unknown.";
            return false;
        }

        if (string.Equals(text, InitialImportColumns.NoExpiryMarker, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        DateTime parsed;

        if (DateTime.TryParseExact(text, FullDateFormats,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var fullDate))
        {
            parsed = fullDate.Date;
        }
        else if (DateTime.TryParseExact(text, MonthOnlyFormats,
                     CultureInfo.InvariantCulture, DateTimeStyles.None, out var monthDate))
        {
            // 연-월만 적힌 경우 그 달의 마지막 날까지 쓸 수 있는 것으로 본다.
            parsed = new DateTime(monthDate.Year, monthDate.Month,
                DateTime.DaysInMonth(monthDate.Year, monthDate.Month));
        }
        else
        {
            error = $"expiry_date '{text}' is not a valid date. Use yyyy-MM-dd, yyyy-MM, "
                  + $"or '{InitialImportColumns.NoExpiryMarker}' if unknown.";
            return false;
        }

        if (parsed <= today)
        {
            error = $"expiry_date '{text}' is not in the future.";
            return false;
        }

        // 하루 끝으로 잡아야 그 날 하루는 팔 수 있다 (입고 화면과 같은 규칙).
        expiryDate = new DateTimeOffset(
            DateTime.SpecifyKind(parsed.AddDays(1).AddSeconds(-1), DateTimeKind.Local)).ToUnixTimeMilliseconds();

        return true;
    }

    public async Task<ImportApplyResult> ApplyInventoryAsync(
        InventoryImportPlan plan, string fileHash, string? fileName, string facilityId, string userId)
    {
        var failures = new List<ImportIssue>();
        var success = 0;

        var importedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        foreach (var line in plan.BatchesToCreate)
        {
            // 파일의 수량은 낱개 총량이다. 재고에는 "안 뜯은 박스 + 헐어 놓은 낱개"로 나눠 올린다 —
            // 박스 우선으로 나누는 이유는 실사에서 60개(박스당 30)면 보통 두 통이 온전히 있는 것이고,
            // 전부 낱개로 넣으면 박스 판매가 막히기 때문이다.
            var stock = BoxUnitMath.Split(line.QuantityInUnits, line.UnitsPerBox);

            var transaction = new StockTransaction
            {
                TransactionId = Guid.NewGuid().ToString(),
                FacilityId = facilityId,
                ProductId = line.ProductId,
                UserId = userId,
                // 초기 재고도 입고다. 별도 구분값을 만들면 기존 집계가 전부 이 값을 모른다.
                TransactionType = TransactionType.StockIn,
                BatchNumber = line.BatchNumber,
                ExpiryDate = line.ExpiryDate,
                // 원장은 언제나 낱개 기준.
                Quantity = line.QuantityInUnits,
                TransactionTime = importedAt
            };

            try
            {
                await _stockInRepository.SaveStockInAsync(transaction, stock.BoxQuantity, stock.UnitQuantity);
                success++;
            }
            catch (Exception)
            {
                failures.Add(new ImportIssue(
                    line.LineNumber, $"'{line.ProductName}' could not be saved."));
            }
        }

        var warning = await RecordHistoryAsync(
            ImportType.Inventory, fileHash, fileName, facilityId,
            plan.TotalRows, success, failures.Count);

        return new ImportApplyResult
        {
            SuccessCount = success,
            FailureCount = failures.Count,
            Failures = failures,
            HistoryWarning = warning
        };
    }

    // ── 공통 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 파일에 필수 컬럼이 있는지 본다. 행이 하나도 없으면 그것도 파일 오류로 다룬다 —
    /// "0건 성공"이라고만 알려주면 왜 아무것도 안 들어갔는지 알 수 없다.
    /// </summary>
    private static string? ValidateHeaders(IReadOnlyList<ImportSourceRow> rows, IReadOnlyList<string[]> required)
    {
        if (rows.Count == 0)
        {
            return "No data rows were found in the file.";
        }

        var headers = rows[0].Values.Keys.ToList();

        return InitialImportColumns.DescribeMissingColumns(headers, required);
    }

    /// <summary>
    /// 임포트 이력을 남긴다. 실패해도 이미 저장된 데이터를 되돌리지는 않는다 —
    /// 대신 사유를 돌려주어, 같은 파일을 다시 넣어도 막히지 않는다는 사실을 알린다.
    /// </summary>
    private async Task<string?> RecordHistoryAsync(
        ImportType importType, string fileHash, string? fileName, string facilityId,
        int rowCount, int successCount, int failureCount)
    {
        try
        {
            await _importHistoryRepository.AddAsync(new ImportHistoryEntry
            {
                ImportId = Guid.NewGuid().ToString(),
                FacilityId = facilityId,
                ImportType = importType,
                FileHash = fileHash,
                FileName = fileName,
                RowCount = rowCount,
                SuccessCount = successCount,
                FailureCount = failureCount,
                ImportedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });

            return null;
        }
        catch (Exception)
        {
            return "The import was saved, but it could not be recorded in the import history. "
                 + "Importing the same file again will not be blocked.";
        }
    }

    /// <summary>
    /// 숫자 칸. 천 단위 쉼표(1,200)까지 받아 준다 — 엑셀에서 서식이 붙은 채로 저장되면 그대로 들어온다.
    /// </summary>
    private static bool TryParseDecimal(string text, out decimal value) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
}
