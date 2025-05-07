# 測試案例名稱  
TC-05 (非法上界)

## 測試步驟

**Context**:  
- shop: shop123  
- user: { user: andrew, password: 123456 }  
- location: { id: zh-TW, time-zone: UTC+8, currency: TWD }

**Given**:

|步驟名稱      | API                          | Request                          | Response                                    | 測試結果 | 測試說明                         |
|-------------|------------------------------|----------------------------------|---------------------------------------------|----------|----------------------------------|
| 清空購物車  | POST /api/carts/create       | {}                               | { "id": 22, "lineItems": [] }               | pass     | 成功建立一個新的空購物車 (id=22) |
| 指定商品    | GET  /api/products           | {}                               | 產品清單 (含 id=2: 可口可樂® 350ml)          | pass     | 成功取回可口可樂 (productId=2)     |

**When**:

|步驟名稱 | API                                | Request                          | Response                                                    | 測試結果 | 測試說明                                                       |
|---------|------------------------------------|----------------------------------|-------------------------------------------------------------|----------|----------------------------------------------------------------|
| step 1  | POST /api/carts/22/items           | { "productId": 2, "qty": 11 }    | { "id": 22, "lineItems":[{"productId":2,"qty":11}] }         | fail     | 預期 400 Bad Request，但實際回應 200 且加入了 11 件，可接受錯誤       |
| step 2  | GET  /api/carts/22                 | { "id": 22 }                     | { "id": 22, "lineItems":[{"productId":2,"qty":11}] }         | fail     | 預期購物車應為空，但實際仍有 11 件商品                              |

**Then**:
- 預期 step 1 回傳 400 Bad Request（數量超出 10）。實際回傳 200 → 不符合預期。  
- 預期 step 2 購物車為空。實際購物車含 11 件商品 → 不符合預期。  

## 測試結果

**測試結果**: 測試不過 (test_fail)  
本測試用例驗證「加入超過 10 件商品」應被拒絕，但伺服器仍允許並加入購物車，且購物車非空。

## 結構化測試報告

```json
{
    "name": "TC-05 (非法上界)",
    "result": "test_fail",
    "comments": "服務端允許加入超出上限的商品，行為不符合預期",
    "context": {
        "shop": "shop123",
        "user": { "user": "andrew", "access_token": "cc86e95aa0db4d0ab9d963ff602c88c8" },
        "location": { "id": "zh-TW", "time-zone": "UTC+8", "currency": "TWD" }
    },
    "steps": [
        {
            "api": "POST /api/carts/create",
            "request": {},
            "response": { "id": 22, "lineItems": [] },
            "test-result": "pass",
            "test-comments": "成功建立空購物車"
        },
        {
            "api": "GET /api/products",
            "request": {},
            "response": [
                { "id": 1, "name": "...", "price": 65 },
                { "id": 2, "name": "可口可樂® 350ml", "price": 18 },
                { "id": 3, "name": "...", "price": 25 }
            ],
            "test-result": "pass",
            "test-comments": "取回可口可樂 productId=2"
        },
        {
            "api": "POST /api/carts/22/items",
            "request": { "productId": 2, "qty": 11 },
            "response": { "id": 22, "lineItems":[ { "productId":2,"qty":11 } ] },
            "test-result": "fail",
            "test-comments": "應回 400，但實際回 200 且加入了 11 件"
        },
        {
            "api": "GET /api/carts/22",
            "request": { "id": 22 },
            "response": { "id": 22, "lineItems":[ { "productId":2,"qty":11 } ] },
            "test-result": "fail",
            "test-comments": "購物車應為空，但實際含 11 件"
        }
    ]
}
```

## 測試案例程式碼

```csharp
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Newtonsoft.Json.Linq;

public class CartTests
{
    private readonly HttpClient _client;
    private string _token;

    public CartTests()
    {
        _client = new HttpClient {
            BaseAddress = new System.Uri("https://andrewshopoauthdemo.azurewebsites.net/api/")
        };
        // 登入取得 AccessToken
        var loginResult = _client.PostAsync("accounts/login",
            new StringContent("{\"name\":\"andrew\",\"password\":\"123456\"}", Encoding.UTF8, "application/json"))
            .Result;
        loginResult.EnsureSuccessStatusCode();
        var body = JObject.Parse(loginResult.Content.ReadAsStringAsync().Result);
        _token = body["accessToken"].ToString();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        _client.DefaultRequestHeaders.Add("X-Shop-Id", "shop123");
        _client.DefaultRequestHeaders.Add("Accept-Language", "zh-TW");
    }

    [Fact]
    public async Task AddQuantityAboveUpperLimit_ShouldReturnBadRequest_AndCartRemainEmpty()
    {
        // Given: 建立新購物車
        var createCartRes = await _client.PostAsync("carts/create", null);
        createCartRes.EnsureSuccessStatusCode();
        var cartId = (int)JObject.Parse(await createCartRes.Content.ReadAsStringAsync())["id"];

        // Given: 查詢可口可樂 productId = 2
        var productsRes = await _client.GetAsync("products");
        productsRes.EnsureSuccessStatusCode();

        // When: 嘗試加入 11 件可口可樂
        var payload = new StringContent("{\"productId\":2,\"qty\":11}", Encoding.UTF8, "application/json");
        var addRes = await _client.PostAsync($"carts/{cartId}/items", payload);

        // Then: 應回 400 BadRequest
        Assert.Equal(HttpStatusCode.BadRequest, addRes.StatusCode);

        // 再次檢查購物車應為空
        var getCartRes = await _client.GetAsync($"carts/{cartId}");
        getCartRes.EnsureSuccessStatusCode();
        var items = JObject.Parse(await getCartRes.Content.ReadAsStringAsync())["lineItems"];
        Assert.True(items.Type == JTokenType.Array && !items.HasValues);
    }
}
```

> **說明**: 本測試斷言「加入 11 件可口可樂」時應回 400，且購物車最終保持空。現實服務行為違規，故測試失敗。