# TC-啤酒數量3（折扣一次，第三件原價）

## 測試步驟

**Context**:  
- shop: shop123  
- user: `{ name: andrew, password: 123456 }`  
- location: `{ id: zh-TW, time-zone: UTC+8, currency: TWD }`  

**Given**:

| 步驟名稱           | API                 | Request | Response                                                                                                           | 測試結果 | 測試說明                   |
|--------------------|---------------------|---------|--------------------------------------------------------------------------------------------------------------------|----------|----------------------------|
| step 1: 清空購物車 | andrew_shop-CreateCart | `{}`    | `{ "id": 200 }`                                                                                                     | pass     | 建立一個新的空購物車，ID=200 |
| step 2: 指定商品    | andrew_shop-GetProducts | `{}`    | `[{"id":1,"name":"啤酒","unitPrice":100}, ...]`                                                                     | pass     | 在商品清單中找到「啤酒」，ID=1 |

**When**:

| 步驟名稱                                | API                       | Request                              | Response                                                                                                                                                              | 測試結果 | 測試說明                       |
|-----------------------------------------|---------------------------|--------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------|----------|--------------------------------|
| step 1: 使用者登入                       | andrew_shop-LoginMember   | `{"name":"andrew","password":"123456"}` | `{"access_token":"token123"}`                                                                                                                                          | pass     | 登入成功，取得 access_token     |
| step 2: 建立新的購物車                   | andrew_shop-CreateCart    | `{}`                                 | `{"id":300}`                                                                                                                                                           | pass     | 建立新的購物車，ID=300          |
| step 3: 加入3件啤酒到購物車              | andrew_shop-AddItemToCart | `{"id":300,"productId":1,"qty":3}`   | `{"id":300,"items":[{"productId":1,"qty":3,"unitPrice":100}]}`                                                                                                           | pass     | 成功將3件啤酒加入購物車        |
| step 4: 試算購物車金額(含折扣)            | andrew_shop-EstimatePrice | `{"id":300}`                         | `{"items":[{"productId":1,"qty":3,"unitPrice":100,"subTotal":300,"discount":40,"total":260}],"discountTotal":40,"total":260}`                                            | pass     | 成功試算，回傳折扣與應付總額    |

**Then**:

- 預期結果1: 購物車僅包含啤酒，數量為 3  
  測試結果說明：試算回傳的 items[0].qty = 3，符合預期。  
- 預期結果2: 折扣僅針對第二件啤酒，折扣金額 = 0.4 × 單價 = 0.4 × 100 = 40  
  測試結果說明：`discount`（單一商品折扣）與 `discountTotal` 均為 40，符合預期。  
- 預期結果3: 應付總額 = `3 × 單價 − 0.4 × 單價 = 260`  
  測試結果說明：`total` = 260，符合預期。  

## 測試結果

**測試結果**: 測試通過 (test_pass)  

所有步驟皆成功，並且金額與折扣計算均符合規格。

---

## 結構化測試報告

```json
{
    "name": "TC-啤酒數量3（折扣一次，第三件原價）",
    "result": "test_pass",
    "comments": "所有步驟皆成功，且折扣及總價符合 2.6 × 單價 的公式。",
    "context": {
        "shop": "shop123",
        "user": { "access_token": "token123", "user": "andrew" },
        "location": { "id": "zh-TW", "time-zone": "UTC+8", "currency": "TWD" }
    },
    "steps": [
        { "api": "andrew_shop-CreateCart",      "request": {},                                           "response": { "id": 200 },                                                                                                                                                  "test-result": "pass", "test-comments": "建立空購物車" },
        { "api": "andrew_shop-GetProducts",      "request": {},                                           "response": [{ "id":1,"name":"啤酒","unitPrice":100 }],                                                                                                                      "test-result": "pass", "test-comments": "取得商品清單並找到啤酒" },
        { "api": "andrew_shop-LoginMember",      "request": {"name":"andrew","password":"123456"},       "response": { "access_token":"token123" },                                                                                                                                  "test-result": "pass", "test-comments": "使用者登入成功" },
        { "api": "andrew_shop-CreateCart",       "request": {},                                           "response": { "id":300 },                                                                                                                                                   "test-result": "pass", "test-comments": "建立新的購物車" },
        { "api": "andrew_shop-AddItemToCart",    "request": {"id":300,"productId":1,"qty":3},             "response": { "id":300,"items":[{"productId":1,"qty":3,"unitPrice":100}] },                                                                                                   "test-result": "pass", "test-comments": "將3件啤酒加入購物車" },
        { "api": "andrew_shop-EstimatePrice",    "request": {"id":300},                                   "response": { "items":[{"productId":1,"qty":3,"unitPrice":100,"subTotal":300,"discount":40,"total":260}],"discountTotal":40,"total":260 },                                      "test-result": "pass", "test-comments": "試算金額並正確計算折扣與總額" }
    ]
}
```

---

## 測試案例程式碼 (C# / xUnit)

```csharp
using System.Linq;
using System.Threading.Tasks;
using Xunit;

public class BeerDiscountTests
{
    private readonly ShopClient _client;

    public BeerDiscountTests()
    {
        // 初始化 API client，帶入 shop id 及位置設定
        _client = new ShopClient(shopApiKey: "shop123", locale: "zh-TW");
    }

    [Fact]
    public async Task TC_Beer3Quantity_DiscountOnce()
    {
        // 1. 使用者登入
        var loginRes = await _client.LoginMemberAsync("andrew", "123456");
        Assert.NotNull(loginRes);
        Assert.False(string.IsNullOrEmpty(loginRes.AccessToken));
        _client.SetAccessToken(loginRes.AccessToken);

        // 2. 確保購物車為空
        var newCart = await _client.CreateCartAsync();
        Assert.NotNull(newCart);
        int cartId = newCart.Id;

        // 3. 取得商品清單並找到啤酒
        var products = await _client.GetProductsAsync();
        var beer = products.FirstOrDefault(p => p.Name.Contains("啤酒"));
        Assert.NotNull(beer);

        // 4. 加入 3 件啤酒
        var addRes = await _client.AddItemToCartAsync(cartId, beer.Id, 3);
        Assert.NotNull(addRes);
        var item = addRes.Items.FirstOrDefault(i => i.ProductId == beer.Id);
        Assert.Equal(3, item.Qty);

        // 5. 試算金額
        var estimate = await _client.EstimatePriceAsync(cartId);
        Assert.Equal(1, estimate.Items.Count);
        var line = estimate.Items[0];

        // 驗證數量
        Assert.Equal(3, line.Qty);

        // 驗證折扣：0.4 × 單價
        var expectedDiscount = 0.4m * line.UnitPrice;
        Assert.Equal(expectedDiscount, line.Discount);
        Assert.Equal(expectedDiscount, estimate.DiscountTotal);

        // 驗證總額：3 × 單價 − 折扣
        var expectedTotal = 3m * line.UnitPrice - expectedDiscount;
        Assert.Equal(expectedTotal, line.Total);
        Assert.Equal(expectedTotal, estimate.Total);
    }
}

// 以下是假想的封裝 API 呼叫的客戶端範例
public class ShopClient
{
    //... constructor, SetAccessToken, LoginMemberAsync, CreateCartAsync, GetProductsAsync,
    //    AddItemToCartAsync, EstimatePriceAsync 等方法
}
```