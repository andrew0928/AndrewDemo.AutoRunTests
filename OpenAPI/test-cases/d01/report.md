# 測試案例名稱  
TC-啤酒數量1_無折扣驗證  

## 測試步驟

**Context**:  
- shop: shop123  
- user: andrew / 123456  
- location: zh‑TW (UTC+8, TWD)  

**Given**:

| 步驟名稱 | API            | Request | Response                                                                                | 測試結果 | 測試說明                             |
|---------|----------------|---------|-----------------------------------------------------------------------------------------|----------|--------------------------------------|
| step 1  | CreateCart     | {}      | `{ "id": 9, "lineItems": [] }`                                                          | pass     | 建立新購物車並確認為空               |
| step 2  | GetProducts    | {}      | `[{ "id":1, "name":"18天台灣生啤酒 355ml", "price":65 }, ... ]`                           | pass     | 成功取得商品清單，包含啤酒 (id=1)     |

**When**:

| 步驟名稱 | API              | Request                                   | Response                                                                                   | 測試結果 | 測試說明                           |
|---------|------------------|-------------------------------------------|--------------------------------------------------------------------------------------------|----------|------------------------------------|
| step 1  | AddItemToCart    | `{ "id":9, "productId":1, "qty":1 }`       | `{ "id":9, "lineItems":[{"productId":1,"qty":1}] }`                                        | pass     | 成功將 1 件啤酒加入購物車           |
| step 2  | EstimatePrice    | `{ "id":9 }`                              | `{ "total":65, "discounts": [] }`                                                          | pass     | 試算金額回傳應付總額 65，無折扣     |

**Then**:

- 購物車僅含一項啤酒，數量顯示為 **1**：通過，購物車回傳 `lineItems[0].qty == 1`  
- 試算結果中的 **應付總額** 等於啤酒單價：通過，`total == 65`  
- 折扣清單為空或不出現折扣欄位：通過，`discounts` 為空陣列  

## 測試結果

**測試結果**: 測試通過 (test_pass)  
所有步驟皆依預期執行並驗證通過，該案例符合「無折扣」情境下之正確行為。  

## 結構化測試報告

```json
{
  "name": "TC-啤酒數量1_無折扣驗證",
  "result": "test_pass",
  "comments": "所有步驟依預期通過，購物車含 1 件啤酒，試算總額正確，無任何折扣。",
  "context": {
    "shop": "shop123",
    "user": { "access_token": "7d6dba6eff6643bba65e947a709561ce", "user": "andrew" },
    "location": { "id": "zh-TW", "time-zone": "UTC+8", "currency": "TWD" }
  },
  "steps": [
    {
      "api": "CreateCart",
      "request": {},
      "response": { "id": 9, "lineItems": [] },
      "test-result": "pass",
      "test-comments": "新購物車已建立且內容為空"
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
      "test-comments": "正確取得商品清單，包含 id=1 啤酒"
    },
    {
      "api": "AddItemToCart",
      "request": { "id":9, "productId":1, "qty":1 },
      "response": { "id":9, "lineItems":[ { "productId":1, "qty":1 } ] },
      "test-result": "pass",
      "test-comments": "購物車成功加入 1 件啤酒"
    },
    {
      "api": "EstimatePrice",
      "request": { "id":9 },
      "response": { "total":65, "discounts": [] },
      "test-result": "pass",
      "test-comments": "試算結果應付總額 65，無折扣"
    }
  ]
}
```

## 測試案例程式碼

```csharp
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

public class CartTests
{
    private readonly HttpClient _client = new HttpClient {
        BaseAddress = new System.Uri("https://andrewshopoauthdemo.azurewebsites.net/api/")
    };
    private async Task LoginAsync()
    {
        var payload = JsonSerializer.Serialize(new { name = "andrew", password = "123456" });
        var res = await _client.PostAsync("members/login", new StringContent(payload, Encoding.UTF8, "application/json"));
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        var token = doc.RootElement.GetProperty("access_token").GetString();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    [Fact]
    public async Task TC_BeerQty1_NoDiscount()
    {
        // Arrange
        await LoginAsync();
        // 清空/建立新購物車
        var resCreate = await _client.PostAsync("carts/create", null);
        resCreate.EnsureSuccessStatusCode();
        var cart = JsonSerializer.Deserialize<Cart>(await resCreate.Content.ReadAsStringAsync());

        // Act: 加入 1 件啤酒
        var addPayload = JsonSerializer.Serialize(new { id = cart.Id, productId = 1, qty = 1 });
        var resAdd = await _client.PostAsync("carts/line-items", new StringContent(addPayload, Encoding.UTF8, "application/json"));
        resAdd.EnsureSuccessStatusCode();
        var updatedCart = JsonSerializer.Deserialize<Cart>(await resAdd.Content.ReadAsStringAsync());

        // Act: 試算價格
        var resEstimate = await _client.PostAsync($"carts/{cart.Id}/estimate", null);
        resEstimate.EnsureSuccessStatusCode();
        var estimate = JsonSerializer.Deserialize<Estimate>(await resEstimate.Content.ReadAsStringAsync());

        // Assert
        Assert.Single(updatedCart.LineItems);
        Assert.Equal(1, updatedCart.LineItems[0].Qty);
        Assert.Equal(65m, estimate.Total);
        Assert.Empty(estimate.Discounts);
    }

    public class Cart
    {
        public int Id { get; set; }
        public LineItem[] LineItems { get; set; }
    }

    public class LineItem
    {
        public int ProductId { get; set; }
        public int Qty { get; set; }
    }

    public class Estimate
    {
        public decimal Total { get; set; }
        public object[] Discounts { get; set; }
    }
}
```