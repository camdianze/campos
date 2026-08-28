using PharmaPOS.Application.Import;
using PharmaPOS.Application.Products;
using PharmaPOS.Application.Repositories;
using PharmaPOS.Domain.Entities;
using PharmaPOS.Domain.Enums;

// 테스트 프로젝트에도 Inventory라는 이름의 네임스페이스가 있어 엔티티가 가려진다.
using InventoryEntity = PharmaPOS.Domain.Entities.Inventory;

namespace PharmaPOS.Tests.Import;

/// <summary>
/// 초기 재고 임포트의 판정 규칙 테스트.
///
/// 여기를 테스트하는 이유: 이 기능은 약국이 딱 한 번, 수백 행짜리 파일로 쓴다.
/// 규칙이 어긋나면 상품이 두 벌 생기거나 재고가 두 배가 된 채로 영업이 시작되고,
/// 그때는 무엇이 잘못 들어갔는지 파일과 대조하는 것 말고는 되돌릴 방법이 없다.
/// </summary>
public class InitialImportServiceTests
{
    private const string FacilityId = "facility-1";
    private const string UserId = "user-1";

    // ── 테스트 대역 ──────────────────────────────────────────────────────────

    private sealed class FakeProductRepository : IProductRepository
    {
        public List<Product> Products { get; } = new();

        public Task<IReadOnlyList<Product>> SearchAsync(string searchTerm, EntityStatus? statusFilter)
            => Task.FromResult<IReadOnlyList<Product>>(Products.ToList());

        public Task<Product?> GetByIdAsync(string productId)
            => Task.FromResult(Products.FirstOrDefault(p => p.ProductId == productId));

        public Task<bool> BarcodeExistsAsync(string barcode, string? excludeProductId = null)
            => Task.FromResult(Products.Any(p => p.Barcode == barcode && p.ProductId != excludeProductId));

        public Task<bool> InternalBarcodeExistsAsync(string internalBarcode, string? excludeProductId = null)
            => Task.FromResult(false);

        public Task InsertAsync(Product product)
        {
            Products.Add(product);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Product product)
        {
            var index = Products.FindIndex(p => p.ProductId == product.ProductId);

            if (index >= 0)
            {
                Products[index] = product;
            }

            return Task.CompletedTask;
        }

        public Task DeactivateAsync(string productId) => Task.CompletedTask;

        /// <summary>사진은 상품 저장 경로와 갈라져 있다. 임포트는 사진을 건드리지 않는다.</summary>
        public Dictionary<string, ProductPhoto> Photos { get; } = new();

        public Task<ProductPhoto?> GetPhotoAsync(string productId) =>
            Task.FromResult(Photos.TryGetValue(productId, out var photo) ? photo : null);

        public Task SavePhotoAsync(string productId, byte[]? photo, long? updatedAt)
        {
            if (photo is null)
            {
                Photos.Remove(productId);
            }
            else
            {
                Photos[productId] = new ProductPhoto(photo, updatedAt ?? 0);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeBarcodeSequenceRepository : IInternalBarcodeSequenceRepository
    {
        private int _next = 1;

        public Task<string> GetNextInternalBarcodeAsync() => Task.FromResult($"INT-{_next++:D8}");
    }

    private sealed class FakeStockInRepository : IStockInRepository
    {
        public List<(StockTransaction Transaction, int BoxQuantity, int UnitQuantity)> Saved { get; } = new();

        public bool ShouldThrow { get; set; }

        public Task SaveStockInAsync(StockTransaction transaction, int boxQuantity, int unitQuantity)
        {
            if (ShouldThrow)
            {
                throw new InvalidOperationException("save failed");
            }

            Saved.Add((transaction, boxQuantity, unitQuantity));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeImportHistoryRepository : IImportHistoryRepository
    {
        public List<ImportHistoryEntry> Entries { get; } = new();

        public Task<bool> ExistsAsync(ImportType importType, string fileHash)
            => Task.FromResult(Entries.Any(e => e.ImportType == importType && e.FileHash == fileHash));

        public Task AddAsync(ImportHistoryEntry entry)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class Harness
    {
        public FakeProductRepository Products { get; } = new();
        public FakeStockInRepository StockIn { get; } = new();
        public FakeImportHistoryRepository History { get; } = new();

        public InitialImportService Build() => new(
            Products,
            new ProductService(Products, new FakeBarcodeSequenceRepository()),
            StockIn,
            History);
    }

    /// <summary>파일 한 행. 값을 비워 두면 그 칸이 빈 셀이다.</summary>
    private static ImportSourceRow Row(
        int lineNumber,
        string productName = "",
        string unit = "",
        string barcode = "",
        string costPrice = "",
        string sellingPrice = "",
        string safetyStock = "",
        string unitsPerBox = "",
        string looseUnitPrice = "",
        string batchNumber = "",
        string expiryDate = "",
        string quantity = "",
        string dosageForm = "")
    {
        return new ImportSourceRow
        {
            LineNumber = lineNumber,
            Values = new Dictionary<string, string>
            {
                [InitialImportColumns.ProductName[0]] = productName,
                [InitialImportColumns.Unit[0]] = unit,
                [InitialImportColumns.Barcode[0]] = barcode,
                [InitialImportColumns.CostPrice[0]] = costPrice,
                [InitialImportColumns.SellingPrice[0]] = sellingPrice,
                [InitialImportColumns.SafetyStock[0]] = safetyStock,
                [InitialImportColumns.UnitsPerBox[0]] = unitsPerBox,
                [InitialImportColumns.LooseUnitPrice[0]] = looseUnitPrice,
                [InitialImportColumns.BatchNumber[0]] = batchNumber,
                [InitialImportColumns.ExpiryDate[0]] = expiryDate,
                [InitialImportColumns.Quantity[0]] = quantity,
                [InitialImportColumns.DosageForm[0]] = dosageForm
            }
        };
    }

    /// <summary>상품 정보가 다 들어간 정상 행.</summary>
    private static ImportSourceRow FullRow(
        int lineNumber, string productName, string batchNumber = "B1",
        string expiryDate = "2099-12-31", string quantity = "10",
        string unitsPerBox = "", string looseUnitPrice = "")
        => Row(lineNumber, productName, unit: "Tablet", costPrice: "500", sellingPrice: "1000",
            safetyStock: "5", unitsPerBox: unitsPerBox, looseUnitPrice: looseUnitPrice,
            batchNumber: batchNumber, expiryDate: expiryDate, quantity: quantity);

    private static Product ExistingProduct(string name, int unitsPerBox = 1) => new()
    {
        ProductId = "id-" + name,
        ProductName = name,
        Unit = "Tablet",
        CostPrice = 500,
        SellingPrice = 1000,
        SafetyStockLevel = 5,
        UnitsPerBox = unitsPerBox,
        Status = EntityStatus.Active,
        CreatedAt = 0
    };

    // ── Products ────────────────────────────────────────────────────────────

    /// <summary>같은 상품에 배치가 여러 개면 행이 여러 개다. 상품은 첫 행만 읽는다.</summary>
    [Fact]
    public async Task PlanProducts_KeepsOnlyTheFirstRowOfEachProduct()
    {
        var harness = new Harness();

        var plan = await harness.Build().PlanProductsAsync(new[]
        {
            FullRow(2, "Amoxicillin", batchNumber: "B1"),
            // 2행부터는 상품 정보가 비어 있어도 된다.
            Row(3, productName: "Amoxicillin", batchNumber: "B2", expiryDate: "2099-12-31", quantity: "5"),
            FullRow(4, "Paracetamol")
        });

        Assert.Equal(3, plan.TotalRows);
        Assert.Equal(2, plan.CreateCount);
        Assert.Equal(1, plan.DuplicateRowCount);
        Assert.Empty(plan.Issues);
    }

    /// <summary>상품명 매칭은 앞뒤 공백과 대소문자를 무시한다.</summary>
    [Fact]
    public async Task PlanProducts_MatchesNamesIgnoringCaseAndSpaces()
    {
        var harness = new Harness();
        harness.Products.Products.Add(ExistingProduct("Amoxicillin"));

        var plan = await harness.Build().PlanProductsAsync(new[]
        {
            // 값이 기존과 같으므로 고칠 것이 없다 = 이미 등록된 상품으로 알아본 것이다.
            FullRow(2, "  amoxicillin  "),
            FullRow(3, "Ibuprofen")
        });

        Assert.Equal(1, plan.UnchangedCount);
        Assert.Equal("Ibuprofen", Assert.Single(plan.ProductsToCreate).Product.ProductName);
    }

    /// <summary>
    /// 이미 있는 상품은 파일에 <b>채워진 칸만</b> 고친다.
    /// 상품명만 적힌 행이 기존 단가를 지워 버리면 안 된다.
    /// </summary>
    [Fact]
    public async Task PlanProducts_UpdatesOnlyTheColumnsThatAreFilled()
    {
        var harness = new Harness();
        harness.Products.Products.Add(ExistingProduct("Amoxicillin"));

        var plan = await harness.Build().PlanProductsAsync(new[]
        {
            // 판매가만 적힌 행.
            Row(2, productName: "Amoxicillin", sellingPrice: "1500")
        });

        var updated = Assert.Single(plan.ProductsToUpdate).Product;

        Assert.Equal(1500m, updated.SellingPrice);
        Assert.Equal(500m, updated.CostPrice);      // 안 적었으니 그대로
        Assert.Equal("Tablet", updated.Unit);       // 안 적었으니 그대로
        Assert.Equal(5, updated.SafetyStockLevel);  // 안 적었으니 그대로
        Assert.Empty(plan.ProductsToCreate);
    }

    // ── 제형(dosage_form) ───────────────────────────────────────────────────
    //
    // 제형은 고정 목록이지만 파일은 현장에서 손으로 채운다. 표기를 얼마나 받아 주는지가
    // 조용히 깨지면 행이 통째로 오류로 빠지거나, 반대로 엉뚱한 제형이 들어간다.

    [Theory]
    [InlineData("Tablet", DosageForm.Tablet)]
    [InlineData("tablet", DosageForm.Tablet)]
    [InlineData("Tablets", DosageForm.Tablet)]     // 복수형
    [InlineData("tab", DosageForm.Tablet)]         // 줄임말
    [InlineData("caps", DosageForm.Capsule)]
    [InlineData("inj", DosageForm.Injection)]
    [InlineData("vial", DosageForm.Injection)]
    [InlineData("Sachet", DosageForm.Powder)]
    [InlineData("Eye drops", DosageForm.Drops)]    // 구분자
    [InlineData("Drops", DosageForm.Drops)]        // 원래 s로 끝나는 이름
    [InlineData("OINTMENT", DosageForm.Ointment)]
    public async Task PlanProducts_ReadsDosageFormLeniently(string text, DosageForm expected)
    {
        var harness = new Harness();

        var plan = await harness.Build().PlanProductsAsync(new[]
        {
            Row(2, productName: "Amoxicillin", unit: "Tablet", costPrice: "500",
                sellingPrice: "1000", safetyStock: "5", dosageForm: text)
        });

        Assert.Empty(plan.Issues);
        Assert.Equal(expected, Assert.Single(plan.ProductsToCreate).Product.DosageForm);
    }

    /// <summary>
    /// 목록에 없는 값은 그 행을 오류로 남긴다. 조용히 비워 두면 왜 안 들어갔는지 알 수 없고,
    /// 나중에 제형으로 거르는 조회가 빈 값을 "제형 없는 상품"으로 읽어 버린다.
    /// </summary>
    [Fact]
    public async Task PlanProducts_RejectsUnknownDosageForm()
    {
        var harness = new Harness();

        var plan = await harness.Build().PlanProductsAsync(new[]
        {
            Row(2, productName: "Amoxicillin", unit: "Tablet", costPrice: "500",
                sellingPrice: "1000", safetyStock: "5", dosageForm: "정제")
        });

        Assert.Equal(1, plan.ErrorRowCount);
        Assert.Empty(plan.ProductsToCreate);

        // 고칠 수 있게 허용 값을 함께 알려 준다.
        var issue = Assert.Single(plan.Issues);
        Assert.Contains("dosage_form", issue.Reason);
        Assert.Contains("Tablet", issue.Reason);
    }

    /// <summary>
    /// 상품명과 제형 두 칸만 담은 파일로 이미 등록된 상품의 제형을 채울 수 있어야 한다.
    /// 제형 컬럼을 뒤늦게 추가했으므로, 기존 상품 수백 개를 채우는 길이 사실상 이것뿐이다.
    /// </summary>
    [Fact]
    public async Task PlanProducts_FillsDosageFormOfExistingProductWithoutTouchingAnythingElse()
    {
        var harness = new Harness();
        harness.Products.Products.Add(ExistingProduct("Amoxicillin"));

        var plan = await harness.Build().PlanProductsAsync(new[]
        {
            Row(2, productName: "Amoxicillin", dosageForm: "Syrup")
        });

        var updated = Assert.Single(plan.ProductsToUpdate).Product;

        Assert.Equal(DosageForm.Syrup, updated.DosageForm);

        // 제형과 세는 단위는 다른 값이다. 제형을 채워도 Unit은 그대로여야 한다.
        Assert.Equal("Tablet", updated.Unit);
        Assert.Equal(500m, updated.CostPrice);
        Assert.Equal(1000m, updated.SellingPrice);
    }

    /// <summary>제형 칸이 비어 있으면 이미 정해 둔 제형을 지우지 않는다.</summary>
    [Fact]
    public async Task PlanProducts_KeepsDosageFormWhenColumnIsEmpty()
    {
        var harness = new Harness();

        var existing = ExistingProduct("Amoxicillin");
        existing.DosageForm = DosageForm.Ointment;
        harness.Products.Products.Add(existing);

        var plan = await harness.Build().PlanProductsAsync(new[]
        {
            Row(2, productName: "Amoxicillin", sellingPrice: "1500")
        });

        Assert.Equal(DosageForm.Ointment, Assert.Single(plan.ProductsToUpdate).Product.DosageForm);
    }

    /// <summary>소분 판매 칸이 비어 있으면 기존 설정을 끄지 않는다.</summary>
    [Fact]
    public async Task PlanProducts_KeepsLooseSaleSettingWhenColumnsAreEmpty()
    {
        var harness = new Harness();

        var existing = ExistingProduct("Amoxicillin", unitsPerBox: 30);
        existing.UnitSellingPrice = 50m;
        harness.Products.Products.Add(existing);

        var plan = await harness.Build().PlanProductsAsync(new[]
        {
            Row(2, productName: "Amoxicillin", sellingPrice: "1500")
        });

        var updated = Assert.Single(plan.ProductsToUpdate).Product;

        Assert.Equal(30, updated.UnitsPerBox);
        Assert.Equal(50m, updated.UnitSellingPrice);
    }

    /// <summary>상품명만 적힌 행(배치를 적으러 온 행)은 손대지 않는다.</summary>
    [Fact]
    public async Task PlanProducts_LeavesExistingProductAloneWhenNothingIsFilled()
    {
        var harness = new Harness();
        harness.Products.Products.Add(ExistingProduct("Amoxicillin"));

        var plan = await harness.Build().PlanProductsAsync(new[]
        {
            Row(2, productName: "Amoxicillin", batchNumber: "B1", expiryDate: "2099-12-31", quantity: "10")
        });

        Assert.Empty(plan.ProductsToCreate);
        Assert.Empty(plan.ProductsToUpdate);
        Assert.Equal(1, plan.UnchangedCount);
    }

    /// <summary>기존 형식 파일의 컬럼명(safety_stock_level)도 그대로 받는다.</summary>
    [Fact]
    public async Task PlanProducts_AcceptsExportedColumnNames()
    {
        var harness = new Harness();

        var row = new ImportSourceRow
        {
            LineNumber = 2,
            Values = new Dictionary<string, string>
            {
                ["productname"] = "Amoxicillin",
                ["unit"] = "Tablet",
                ["costprice"] = "500",
                ["sellingprice"] = "1000",
                ["safetystocklevel"] = "7",
                ["unitsperbox"] = "30",
                ["unitsellingprice"] = "50",
                ["genericname"] = "Amoxicillin",
                ["atccode"] = "J01CA04"
            }
        };

        var plan = await harness.Build().PlanProductsAsync(new[] { row });

        var product = Assert.Single(plan.ProductsToCreate).Product;

        Assert.Equal(7, product.SafetyStockLevel);
        Assert.Equal(30, product.UnitsPerBox);
        Assert.Equal(50m, product.UnitSellingPrice);
        Assert.Equal("J01CA04", product.AtcCode);
    }

    [Fact]
    public async Task PlanProducts_LeavesBarcodeEmptyWhenNotGiven()
    {
        var harness = new Harness();

        var plan = await harness.Build().PlanProductsAsync(new[] { FullRow(2, "Amoxicillin") });

        Assert.Null(Assert.Single(plan.ProductsToCreate).Product.Barcode);
    }

    /// <summary>소분 판매 두 칸이 모두 비면 소분 판매가 꺼진 상품이다.</summary>
    [Fact]
    public async Task PlanProducts_TurnsLooseSaleOffWhenBothColumnsAreEmpty()
    {
        var harness = new Harness();

        var plan = await harness.Build().PlanProductsAsync(new[] { FullRow(2, "Amoxicillin") });

        var product = Assert.Single(plan.ProductsToCreate).Product;
        Assert.Equal(1, product.UnitsPerBox);
        Assert.Null(product.UnitSellingPrice);
        Assert.False(product.IsBoxedProduct);
    }

    [Fact]
    public async Task PlanProducts_AcceptsLooseSaleWhenBothColumnsAreFilled()
    {
        var harness = new Harness();

        var plan = await harness.Build().PlanProductsAsync(new[]
        {
            FullRow(2, "Amoxicillin", unitsPerBox: "30", looseUnitPrice: "50")
        });

        var product = Assert.Single(plan.ProductsToCreate).Product;
        Assert.Equal(30, product.UnitsPerBox);
        Assert.Equal(50m, product.UnitSellingPrice);
    }

    /// <summary>한쪽만 채워져 있으면 짐작하지 않고 그 행을 건너뛴다.</summary>
    [Theory]
    [InlineData("30", "")]
    [InlineData("", "50")]
    public async Task PlanProducts_RejectsHalfFilledLooseSaleColumns(string unitsPerBox, string loosePrice)
    {
        var harness = new Harness();

        var plan = await harness.Build().PlanProductsAsync(new[]
        {
            FullRow(2, "Amoxicillin", unitsPerBox: unitsPerBox, looseUnitPrice: loosePrice)
        });

        Assert.Empty(plan.ProductsToCreate);
        Assert.Equal(2, Assert.Single(plan.Issues).LineNumber);
    }

    /// <summary>이미 등록된 상품은 새로 만들지 않고 제자리에서 고친다.</summary>
    [Fact]
    public async Task ApplyProducts_UpdatesExistingProductInPlace()
    {
        var harness = new Harness();
        harness.Products.Products.Add(ExistingProduct("Amoxicillin"));

        var service = harness.Build();
        var plan = await service.PlanProductsAsync(new[]
        {
            Row(2, productName: "Amoxicillin", sellingPrice: "1500")
        });

        var result = await service.ApplyProductsAsync(plan, "hash-1", "stock.csv", FacilityId);

        Assert.Equal(1, result.SuccessCount);

        // 상품이 하나 더 생기지 않는다.
        var product = Assert.Single(harness.Products.Products);
        Assert.Equal(1500m, product.SellingPrice);
    }

    [Fact]
    public async Task ApplyProducts_SavesProductsAndRecordsHistory()
    {
        var harness = new Harness();
        var service = harness.Build();

        var plan = await service.PlanProductsAsync(new[] { FullRow(2, "Amoxicillin"), FullRow(3, "Ibuprofen") });
        var result = await service.ApplyProductsAsync(plan, "hash-1", "stock.csv", FacilityId);

        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
        Assert.Equal(2, harness.Products.Products.Count);

        var entry = Assert.Single(harness.History.Entries);
        Assert.Equal(ImportType.Products, entry.ImportType);
        Assert.Equal("hash-1", entry.FileHash);
    }

    /// <summary>같은 파일을 같은 단계로 다시 넣는 것은 막고, 다음 단계는 막지 않는다.</summary>
    [Fact]
    public async Task WasAlreadyImported_BlocksTheSameStepButNotTheNextOne()
    {
        var harness = new Harness();
        var service = harness.Build();

        var plan = await service.PlanProductsAsync(new[] { FullRow(2, "Amoxicillin") });
        await service.ApplyProductsAsync(plan, "hash-1", "stock.csv", FacilityId);

        Assert.True(await service.WasAlreadyImportedAsync(ImportType.Products, "hash-1"));
        Assert.False(await service.WasAlreadyImportedAsync(ImportType.Inventory, "hash-1"));
    }

    // ── Inventory ───────────────────────────────────────────────────────────

    [Fact]
    public async Task PlanInventory_FailsRowsWhoseProductIsNotRegistered()
    {
        var harness = new Harness();
        harness.Products.Products.Add(ExistingProduct("Amoxicillin"));

        var plan = await harness.Build().PlanInventoryAsync(new[]
        {
            FullRow(2, "Amoxicillin"),
            FullRow(3, "Unknown Product")
        });

        Assert.Equal(1, plan.BatchCount);
        Assert.Equal(3, Assert.Single(plan.UnmatchedRows).LineNumber);
    }

    /// <summary>배치번호는 비어 있어도 된다. 배치 없이 관리하던 약국이 흔하다.</summary>
    [Fact]
    public async Task PlanInventory_AllowsEmptyBatchNumber()
    {
        var harness = new Harness();
        harness.Products.Products.Add(ExistingProduct("Amoxicillin"));

        var plan = await harness.Build().PlanInventoryAsync(new[]
        {
            FullRow(2, "Amoxicillin", batchNumber: "")
        });

        Assert.Equal(string.Empty, Assert.Single(plan.BatchesToCreate).BatchNumber);
    }

    /// <summary>expiry_date = N은 "유효기간 모름"이며 0으로 저장된다.</summary>
    [Fact]
    public async Task PlanInventory_TreatsNAsUnknownExpiry()
    {
        var harness = new Harness();
        harness.Products.Products.Add(ExistingProduct("Amoxicillin"));

        var plan = await harness.Build().PlanInventoryAsync(new[]
        {
            FullRow(2, "Amoxicillin", expiryDate: "N"),
            FullRow(3, "Amoxicillin", batchNumber: "B2", expiryDate: "n")
        });

        Assert.Equal(2, plan.NoExpiryCount);
        Assert.All(plan.BatchesToCreate, b => Assert.Equal(InventoryEntity.NoExpiryDate, b.ExpiryDate));
    }

    [Fact]
    public async Task PlanInventory_RejectsPastExpiryDate()
    {
        var harness = new Harness();
        harness.Products.Products.Add(ExistingProduct("Amoxicillin"));

        var plan = await harness.Build().PlanInventoryAsync(new[]
        {
            FullRow(2, "Amoxicillin", expiryDate: "2020-01-01")
        });

        Assert.Empty(plan.BatchesToCreate);
        Assert.Equal(2, Assert.Single(plan.Issues).LineNumber);
    }

    /// <summary>빈 칸은 "모름"으로 삼키지 않는다. 적는 걸 잊은 것과 구분할 수 없기 때문이다.</summary>
    [Fact]
    public async Task PlanInventory_RejectsEmptyExpiryDate()
    {
        var harness = new Harness();
        harness.Products.Products.Add(ExistingProduct("Amoxicillin"));

        var plan = await harness.Build().PlanInventoryAsync(new[]
        {
            FullRow(2, "Amoxicillin", expiryDate: "")
        });

        Assert.Empty(plan.BatchesToCreate);
        Assert.Single(plan.Issues);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("abc")]
    [InlineData("")]
    public async Task PlanInventory_RejectsNonPositiveQuantity(string quantity)
    {
        var harness = new Harness();
        harness.Products.Products.Add(ExistingProduct("Amoxicillin"));

        var plan = await harness.Build().PlanInventoryAsync(new[]
        {
            FullRow(2, "Amoxicillin", quantity: quantity)
        });

        Assert.Empty(plan.BatchesToCreate);
        Assert.Single(plan.Issues);
    }

    /// <summary>
    /// 파일의 수량은 낱개다. 박스 상품이면 저장할 때 박스 + 낱개로 나눠 올린다.
    /// 원장(Quantity)은 언제나 낱개 총량이어야 한다.
    /// </summary>
    [Fact]
    public async Task ApplyInventory_SplitsUnitsIntoBoxesAndLooseUnits()
    {
        var harness = new Harness();
        harness.Products.Products.Add(ExistingProduct("Amoxicillin", unitsPerBox: 30));

        var service = harness.Build();
        var plan = await service.PlanInventoryAsync(new[] { FullRow(2, "Amoxicillin", quantity: "65") });

        var result = await service.ApplyInventoryAsync(plan, "hash-2", "stock.csv", FacilityId, UserId);

        Assert.Equal(1, result.SuccessCount);

        var (transaction, boxQuantity, unitQuantity) = Assert.Single(harness.StockIn.Saved);
        Assert.Equal(65, transaction.Quantity);
        Assert.Equal(2, boxQuantity);
        Assert.Equal(5, unitQuantity);
    }

    /// <summary>초기 재고도 입고다. 별도 거래 유형을 만들지 않는다.</summary>
    [Fact]
    public async Task ApplyInventory_RecordsStockInTransaction()
    {
        var harness = new Harness();
        harness.Products.Products.Add(ExistingProduct("Amoxicillin"));

        var service = harness.Build();
        var plan = await service.PlanInventoryAsync(new[] { FullRow(2, "Amoxicillin", quantity: "10") });

        await service.ApplyInventoryAsync(plan, "hash-2", "stock.csv", FacilityId, UserId);

        var (transaction, _, _) = Assert.Single(harness.StockIn.Saved);
        Assert.Equal(TransactionType.StockIn, transaction.TransactionType);
        Assert.Equal(UserId, transaction.UserId);
        Assert.Equal(FacilityId, transaction.FacilityId);
        // 입고에는 판매가 스냅샷이 없다.
        Assert.Null(transaction.SellingPriceAtTransaction);
        Assert.Null(transaction.TotalAmount);
    }

    [Fact]
    public async Task ApplyInventory_ReportsFailuresWithLineNumbers()
    {
        var harness = new Harness();
        harness.Products.Products.Add(ExistingProduct("Amoxicillin"));
        harness.StockIn.ShouldThrow = true;

        var service = harness.Build();
        var plan = await service.PlanInventoryAsync(new[] { FullRow(7, "Amoxicillin") });

        var result = await service.ApplyInventoryAsync(plan, "hash-2", "stock.csv", FacilityId, UserId);

        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(7, Assert.Single(result.Failures).LineNumber);
    }

    // ── 파일 자체가 잘못된 경우 ──────────────────────────────────────────────

    /// <summary>
    /// 상품명 컬럼이 없으면 파일 자체가 잘못된 것이다.
    /// (나머지 컬럼은 없어도 된다 — 채워진 칸만 고치는 임포트이기 때문이다.)
    /// </summary>
    [Fact]
    public async Task PlanProducts_ReportsMissingProductNameColumn()
    {
        var harness = new Harness();

        var row = new ImportSourceRow
        {
            LineNumber = 2,
            Values = new Dictionary<string, string> { ["unit"] = "Tablet" }
        };

        var plan = await harness.Build().PlanProductsAsync(new[] { row });

        Assert.True(plan.HasFileError);
        Assert.Contains("product_name", plan.FileError);
    }

    /// <summary>신규 상품에는 단위와 단가가 있어야 한다. 그 행만 건너뛴다.</summary>
    [Fact]
    public async Task PlanProducts_RejectsNewProductWithoutRequiredValues()
    {
        var harness = new Harness();

        var plan = await harness.Build().PlanProductsAsync(new[]
        {
            Row(2, productName: "Amoxicillin", sellingPrice: "1000")
        });

        Assert.Empty(plan.ProductsToCreate);
        Assert.Equal(2, Assert.Single(plan.Issues).LineNumber);
    }

    [Fact]
    public async Task PlanInventory_ReportsEmptyFile()
    {
        var harness = new Harness();

        var plan = await harness.Build().PlanInventoryAsync([]);

        Assert.True(plan.HasFileError);
    }
}
