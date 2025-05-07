# TC-05 非法上界（啤酒數量 11 超過上限應被拒絕）

## 測試步驟

**Context**:  
- shop: shop123  
- user: andrew / 123456  
- location: zh-TW (UTC+8, TWD)  

**Given**:

| 步驟名稱      | API                | Request                   | Response                                             | 測試結果 | 測試說明                         |
|-------------|--------------------|---------------------------|------------------------------------------------------|---------|----------------------------------|
| step 1 清空購物車 | CreateCart         | {}                        | {"id":12,"lineItems":[]}                             | pass    | 新增購物車即為空                 |
| step 2 查詢商品   | GetProducts        | {}                        | 列表包含 id=1 (18天台灣生啤酒)                         | pass    | 確認啤酒（productId=1）存在       |

**When**:

| 步驟名稱               | API             | Request                          | Response                                               | 測試結果 | 測試說明                                      |
|----------------------|-----------------|----------------------------------|--------------------------------------------------------|---------|-----------------------------------------------|
| step 3 加入 11 件啤酒    | AddItemToCart   | {"id":12,"productId":1,"qty":11} | {"id":12,"lineItems":[{"productId":1,"qty":11}]}        | fail    | 系統錯誤地接受了 11 件，未拒絕                  |

**Then**:

- 系統應立即拒絕這次加入行為，顯示「商品數量超過 10」之類的錯誤訊息。  
  測試結果：未拒絕，實際無錯誤訊息。  
- 購物車保持 **空**，或至少不包含任何啤酒項目。  
  測試結果：購物車包含 11 件啤酒，不符合預期。  
- 若使用者隨後執行金額試算，應付總額為 **0**，且無折扣資訊。  
  測試結果：跳過（已在加入階段失敗），或如估價會大於 0。

## 測試結果

**測試結果**: 測試不過 (test_fail)  
原因：AddItemToCart 呼叫時並未依規則拒絕超過上限數量，反而加入了 11 件啤酒。

---

## 結構化測試報告

```json
{
  "name": "TC-05 非法上界（啤酒數量 11 超過上限應被拒絕）",
  "result": "test_fail",
  "comments": "系統錯誤地接受了 11 件啤酒未拒絕，購物車包含非法數量。",
  "context": {
    "shop": "shop123",
    "user": {
      "access_token": "16ea28519e5d4fa5ae5a30a8510a9801",
      "user": "andrew"
    },
    "location": {
      "id": "zh-TW",
      "time-zone": "UTC+8",
      "currency": "TWD"
    }
  },
  "steps": [
    {
      "api": "CreateCart",
      "request": {},
      "response": { "id": 12, "lineItems": [] },
      "test-result": "pass",
      "test-comments": "新購物車為空"
    },
    {
      "api": "GetProducts",
      "request": {},
      "response": [
        { "id": 1, "name": "18天台灣生啤酒 355ml", "price": 65 },
        { "id": 2, "name": "可口可樂® 350ml",     "price": 18 },
        { "id": 3, "name": "御茶園 特撰冰釀綠茶 550ml", "price": 25 }
      ],
      "test-result": "pass",
      "test-comments": "啤酒(productId=1)存在"
    },
    {
      "api": "AddItemToCart",
      "request": { "id": 12, "productId": 1, "qty": 11 },
      "response": { "id": 12, "lineItems": [ { "productId": 1, "qty": 11 } ] },
      "test-result": "failure",
      "test-comments": "系統未拒絕超過上限的加入，反而成功加入"
    }
  ]
}
```

---

## 測試案例程式碼

```csharp
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

public class CartTests
{
    private readonly HttpClient _client;

    public CartTests()
    {
        // 假設已設定 BaseAddress 與 OAuth token
        _client = new HttpClient();
        _client.BaseAddress = new System.Uri("https://andrewshopoauthdemo.azurewebsites.net/api/");
        _client.DefaultRequestHeaders.Add("Authorization", "Bearer 16ea28519e5d4fa5ae5a30a8510a9801");
    }

    [Fact(DisplayName = "TC-05 非法上界：啤酒數量 11 超過上限應被拒絕")]
    public async Task BeerQuantityExceedsLimit_ShouldBeRejected()
    {
        // Given: 建立空購物車
        var createResp = await _client.PostAsync("carts/create", null);
        Assert.Equal(HttpStatusCode.OK, createResp.StatusCode);
        var cart = JsonSerializer.Deserialize<Cart>(await createResp.Content.ReadAsStringAsync());

        // Ensure product exists
        var prodResp = await _client.GetAsync("products");
        Assert.Equal(HttpStatusCode.OK, prodResp.StatusCode);
        var products = JsonSerializer.Deserialize<Product[]>(await prodResp.Content.ReadAsStringAsync());
        Assert.Contains(products, p => p.Id == 1);

        // When: 嘗試加入 11 件啤酒
        var payload = new StringContent(
            JsonSerializer.Serialize(new { productId = 1, qty = 11 }),
            Encoding.UTF8, "application/json");
        var addResp = await _client.PostAsync($"carts/{cart.Id}/items", payload);

        // Then: 預期被拒絕
        Assert.True(
            addResp.StatusCode == HttpStatusCode.BadRequest ||
            addResp.StatusCode == HttpStatusCode.Conflict,
            "預期超過上限時回傳 400/409，但實際回傳 " + addResp.StatusCode);
    }

    private class Cart
    {
        public int Id { get; set; }
    }
    private class Product
    {
        public int Id { get; set; }
    }
}
```

**說明**：測試中當呼叫 AddItemToCart 並傳入 qty = 11 時，系統仍回傳 200 並加入購物車，未符合「超出上限應被拒絕」之規範，故測試不通過。