# 測試案例名稱  
TC-03 (上界成功: 10 → 9)

## 測試步驟

**Context**:  
- shop: shop123  
- user: andrew / 123456  
- location: zh-TW (UTC+8，TWD)  

**Given**:

|步驟名稱                | API                 | Request                                     | Response                                                                                       | 測試結果 | 測試說明                          |
|-----------------------|---------------------|---------------------------------------------|------------------------------------------------------------------------------------------------|----------|-----------------------------------|
| step G1: 清空購物車／建立新購物車 | POST /api/carts/create | –                                           | { "id":15, "lineItems": [] }                                                                    | pass     | 成功取得空購物車 (id=15)           |
| step G2: 取得所有商品           | GET /api/products     | –                                           | [ … { "id":2, "name":"可口可樂® 350ml", "price":18 }, … ]                                        | pass     | 成功取得商品清單，可找到 productId=2 |

**When**:

|步驟名稱        | API                         | Request                       | Response                                           | 測試結果 | 測試說明                       |
|---------------|-----------------------------|-------------------------------|----------------------------------------------------|----------|--------------------------------|
| step 1: 加入10 件 | POST /api/carts/15/items    | { "productId":2, "qty":10 }    | { "id":15, "lineItems":[{ "productId":2, "qty":10 }] } | pass     | 成功將可口可樂加入 10 件        |
| step 2: 移除1 件 | POST /api/carts/15/items    | { "productId":2, "qty":-1 }    | { "id":15, "lineItems":[{ "productId":2, "qty":9 }]  } | pass     | 成功移除 1 件，剩餘 9 件         |
| step 3: 檢查購物車 | GET /api/carts/15          | –                             | { "id":15, "lineItems":[{ "productId":2, "qty":9 }] }  | pass     | 購物車內容正確，qty=9           |

**Then**:

- 預期購物車內容應該是 9 件可口可樂 → **通過** (實際 qty=9)

## 測試結果

**測試結果**: 測試通過 (test_pass)  
本次測試流程正常，購物車內商品數量與預期相符。

## 結構化測試報告

```json
{
  "name": "TC-03 (上界成功: 10 → 9)",
  "result": "test_pass",
  "comments": "購物車內商品數量與預期相符，測試通過",
  "context": {
    "shop": "shop123",
    "user": { "access_token": "956116617b9844e3851736b77c1cd367", "user": "andrew" },
    "location": { "id": "zh-TW", "time-zone": "UTC+8", "currency": "TWD" }
  },
  "steps": [
    {
      "api": "CreateCart",
      "request": {},
      "response": { "id":15, "lineItems":[] },
      "test-result": "pass",
      "test-comments": "成功取得空購物車"
    },
    {
      "api": "GetProducts",
      "request": {},
      "response": [
        { "id":1, "name":"18天台灣生啤酒 355ml", "price":65 },
        { "id":2, "name":"可口可樂® 350ml", "price":18 },
        { "id":3, "name":"御茶園 特撰冰釀綠茶 550ml", "price":25 }
      ],
      "test-result": "pass",
      "test-comments": "成功取得商品清單"
    },
    {
      "api": "AddItemToCart",
      "request": { "cartId":15, "productId":2, "qty":10 },
      "response": { "id":15, "lineItems":[{ "productId":2, "qty":10 }] },
      "test-result": "pass",
      "test-comments": "成功加入10件可口可樂"
    },
    {
      "api": "AddItemToCart",
      "request": { "cartId":15, "productId":2, "qty":-1 },
      "response": { "id":15, "lineItems":[{ "productId":2, "qty":9 }] },
      "test-result": "pass",
      "test-comments": "成功移除1件，剩餘9件"
    },
    {
      "api": "GetCart",
      "request": { "id":15 },
      "response": { "id":15, "lineItems":[{ "productId":2, "qty":9 }] },
      "test-result": "pass",
      "test-comments": "購物車內容確認為9件"
    }
  ]
}
```

## 測試案例程式碼

```csharp
using System.Linq;
using System.Threading.Tasks;
using Xunit;

public class CartTests
{
    private readonly IShopApiClient _client;

    public CartTests()
    {
        // 假設 IShopApiClient 已經註冊好 OAuth2 與 shopApiKey
        _client = new ShopApiClient("shop123", "andrew", "123456", "zh-TW");
    }

    [Fact(DisplayName = "TC-03 上界成功: 10 → 9")]
    public async Task TC03_UpperBoundSuccess_10To9()
    {
        // Given: 清空或建立新購物車
        var cart = await _client.CreateCartAsync();
        Assert.NotNull(cart);
        Assert.Empty(cart.LineItems);

        // Given: 取得商品清單並選定可口可樂(productId=2)
        var products = await _client.GetProductsAsync();
        var coke = products.FirstOrDefault(p => p.Name.Contains("可口可樂"));
        Assert.NotNull(coke);

        // When: 加入10件可口可樂
        var updated1 = await _client.AddItemToCartAsync(cart.Id, coke.Id, 10);
        Assert.Single(updated1.LineItems);
        Assert.Equal(10, updated1.LineItems.Single().Qty);

        // When: 移除1件
        var updated2 = await _client.AddItemToCartAsync(cart.Id, coke.Id, -1);
        Assert.Single(updated2.LineItems);
        Assert.Equal(9, updated2.LineItems.Single().Qty);

        // When: 取得最終購物車內容
        var finalCart = await _client.GetCartAsync(cart.Id);
        Assert.Single(finalCart.LineItems);

        // Then: 應為9件
        var line = finalCart.LineItems.Single(li => li.ProductId == coke.Id);
        Assert.Equal(9, line.Qty);
    }
}
```

**說明**  
以上程式碼使用 xUnit 框架與假設的 `IShopApiClient` 封裝了呼叫流程，驗證：  
1. 建立購物車  
2. 取得商品並選定可口可樂  
3. 加入10件  
4. 移除1件  
5. 確認最終購物車中數量為9。