# Test Case: 啤酒數量 10（折扣五次，上限內）

## 測試步驟

**Context**:  
- shop: shop8  
- user: andrew (access_token: d0b328ea1eb44a57aa43d89a4ec386ac)  
- location: zh-TW (time-zone: UTC+8, currency: TWD)  

**Given**:

|步驟名稱    | API            | Request                   | Response                          | 測試結果 | 測試說明               |
|-----------|----------------|---------------------------|-----------------------------------|---------|------------------------|
| step 1    | CreateCart     | {}                        | { "id": 100 }                     | pass    | 成功建立空購物車       |
| step 2    | GetCart        | { "id": 100 }             | { "id":100, "items": [] }         | pass    | 購物車確實為空         |

---

**When**:

|步驟名稱    | API               | Request                                 | Response                                                                          | 測試結果 | 測試說明                       |
|-----------|-------------------|-----------------------------------------|-----------------------------------------------------------------------------------|---------|--------------------------------|
| step 1    | CreateCart        | {}                                      | { "id": 100 }                                                                     | pass    | 啟用新的購物車                 |
| step 2    | AddItemToCart     | { "id": 100, "productId": 1, "qty": 10 }| { "id":100, "items":[{"productId":1,"qty":10}] }                                   | pass    | 成功加入 10 件啤酒             |
| step 3    | EstimatePrice     | { "id": 100 }                           | { "subTotal":650, "discountTotal":130, "total":520, "discounts":[...]}            | pass    | 成功試算出小計、折扣及總額      |

---

**Then**:

- 購物車僅含啤酒，數量顯示為 **10**。  
  測試結果：pass，AddItemToCart 回傳項目正確。  
- 總折扣金額 = 0.4 × P_B × 5 = 0.4 × 65 × 5 = **130**。  
  測試結果：pass，EstimatePrice.discountTotal = 130。  
- 應付總額 = 10 × P_B − 2 × P_B = 8 × P_B = **520**。  
  測試結果：pass，EstimatePrice.total = 520。  

---

## 測試結果

**測試結果**: 測試通過 (test_pass)  

本案例依序建立購物車、加入 10 件啤酒，並試算金額，所有預期結果皆符合。

---

## 結構化測試報告

```json
{
    "name": "啤酒數量 10（折扣五次，上限內）",
    "result": "test_pass",
    "comments": "所有步驟及預期結果皆符合。",
    "context": {
        "shop": "shop8",
        "user": { "access_token": "d0b328ea1eb44a57aa43d89a4ec386ac", "user": "andrew" },
        "location": { "id": "zh-TW", "time-zone": "UTC+8", "currency": "TWD" }
    },
    "steps": [
        { "api": "CreateCart", "request": {}, "response": { "id": 100 }, "test-result": "pass", "test-comments": "建立新的空購物車" },
        { "api": "GetCart", "request": { "id": 100 }, "response": { "id":100, "items": [] }, "test-result": "pass", "test-comments": "購物車為空" },
        { "api": "AddItemToCart", "request": { "id": 100, "productId": 1, "qty": 10 }, "response": { "id":100, "items":[{"productId":1,"qty":10}] }, "test-result": "pass", "test-comments": "成功加入 10 件啤酒" },
        { "api": "EstimatePrice", "request": { "id": 100 }, "response": { "subTotal":650, "discountTotal":130, "total":520 }, "test-result": "pass", "test-comments": "小計 650，折扣 130，總額 520" }
    ]
}
```

---

## 測試案例程式碼

```csharp
using System.Threading.Tasks;
using Xunit;

public class CartDiscountTests
{
    private readonly ShopClient client;

    public CartDiscountTests()
    {
        // 假設 ShopClient 已設定 BaseUrl, ApiKey, Token 等
        client = new ShopClient("https://andrewshopoauthdemo.azurewebsites.net/api", 
                                shopApiKey: "shop123", 
                                accessToken: "d0b328ea1eb44a57aa43d89a4ec386ac", 
                                locationId: "zh-TW");
    }

    [Fact]
    public async Task TC_BeerQuantity10_DiscountFiveTimes()
    {
        // 1. 建立購物車
        var cart = await client.CreateCartAsync();
        Assert.NotNull(cart);
        int cartId = cart.Id;

        // 2. 加入 10 件啤酒 (productId = 1, price = 65)
        var updatedCart = await client.AddItemToCartAsync(cartId, productId: 1, qty: 10);
        Assert.Single(updatedCart.Items);
        Assert.Equal(10, updatedCart.Items[0].Qty);
        Assert.Equal(1, updatedCart.Items[0].ProductId);

        // 3. 試算金額
        var estimate = await client.EstimatePriceAsync(cartId);
        Assert.Equal(65 * 10, estimate.SubTotal);         // 650
        Assert.Equal((decimal)(0.4m * 65m * 5), estimate.DiscountTotal); // 130
        Assert.Equal(520m, estimate.Total);               // 520

        // 4. 再次取購物車確認數量
        var finalCart = await client.GetCartAsync(cartId);
        Assert.Single(finalCart.Items);
        Assert.Equal(10, finalCart.Items[0].Qty);
    }
}

// 以下為模擬的 API 客戶端
public class ShopClient
{
    // ... 省略建構子與 HttpClient 初始化 ...

    public Task<CartResponse> CreateCartAsync() => /* 呼叫 CreateCart API */;
    public Task<CartResponse> AddItemToCartAsync(int cartId, int productId, int qty) => /* 呼叫 AddItemToCart API */;
    public Task<EstimateResponse> EstimatePriceAsync(int cartId) => /* 呼叫 EstimatePrice API */;
    public Task<CartResponse> GetCartAsync(int cartId) => /* 呼叫 GetCart API */;
}

public class CartResponse
{
    public int Id { get; set; }
    public CartItem[] Items { get; set; }
}

public class CartItem
{
    public int ProductId { get; set; }
    public int Qty { get; set; }
}

public class EstimateResponse
{
    public decimal SubTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal Total { get; set; }
    public object[] Discounts { get; set; }
}
```