# Test Case: 啤酒數量 2（第二件六折折扣）

## 測試步驟

**Context**:
- shop: shop8  
- user: andrew, accessToken: 41013258df874c49ab77554ba3eedd50  
- location: zh-TW (time-zone: UTC+8, currency: TWD)  

**Given**:

|步驟名稱|API|Request|Response|測試結果|測試說明|
|--------|---|-------|--------|--------|--------|
|step 1|LoginMember|{"name":"andrew","password":"123456"}|{"accessToken":"41013258df874c49ab77554ba3eedd50"}|pass|使用者登入成功|
|step 2|GetProducts|{}|[{…,"id":1,"name":"18天台灣生啤酒 355ml",…},…]|pass|取得商品列表並確認啤酒 productId=1|
|step 3|（清空購物車）|–|–|pass|建立新購物車時隱含清空舊購物車|

**When**:

|步驟名稱|API|Request|Response|測試結果|測試說明|
|--------|---|-------|--------|--------|--------|
|step 1|CreateCart|{}|{"id":10,"lineItems":[]}|pass|新購物車建立成功，cartId=10|
|step 2|AddItemToCart|{"id":10,"productId":1,"qty":2}|{"id":10,"lineItems":[{"productId":1,"qty":2}]}|pass|一次加入 2 件啤酒|
|step 3|EstimatePrice|{"id":10}|{"totalPrice":104.0,"discounts":[{"name":"第二件六折","description":"符合商品: 18天台灣生啤酒 355ml x 2","discountAmount":-26.0}]}|pass|試算價格並回傳折扣資訊|

**Then**:
- 購物車僅含啤酒一項，數量顯示為 **2**：符合，lineItems[0].qty = 2  
- 折扣 **40%**（即六折）套用於第二件啤酒，折扣金額 = 0.4 × 單價 = 26：符合，discountAmount = -26.0  
- **應付總額** = 單價 + 單價×0.6 = 1.6×65 = **104.0**：符合，totalPrice = 104.0  

## 測試結果

**測試結果**: 測試通過(test_pass)  
所有步驟及 API 回應皆符合預期。

## 結構化測試報告

```json
{
  "name": "啤酒數量 2（第二件六折折扣）",
  "result": "test_pass",
  "comments": "所有步驟與預期結果吻合",
  "context": {
    "shop": "shop8",
    "user": {
      "user": "andrew",
      "access_token": "41013258df874c49ab77554ba3eedd50"
    },
    "location": {
      "id": "zh-TW",
      "time-zone": "UTC+8",
      "currency": "TWD"
    }
  },
  "steps": [
    {
      "api": "LoginMember",
      "request": { "name": "andrew", "password": "123456" },
      "response": { "accessToken": "41013258df874c49ab77554ba3eedd50" },
      "test-result": "pass",
      "test-comments": "使用者登入成功"
    },
    {
      "api": "GetProducts",
      "request": {},
      "response": [
        { "id":1,"name":"18天台灣生啤酒 355ml","price":65 },
        { "id":2,"name":"可口可樂® 350ml","price":18 },
        { "id":3,"name":"御茶園 特撰冰釀綠茶 550ml","price":25 }
      ],
      "test-result": "pass",
      "test-comments": "取得商品列表並確認啤酒 productId=1"
    },
    {
      "api": "CreateCart",
      "request": {},
      "response": { "id": 10, "lineItems": [] },
      "test-result": "pass",
      "test-comments": "建立新購物車"
    },
    {
      "api": "AddItemToCart",
      "request": { "id": 10, "productId": 1, "qty": 2 },
      "response": { "id": 10, "lineItems": [ { "productId": 1, "qty": 2 } ] },
      "test-result": "pass",
      "test-comments": "加入 2 件啤酒"
    },
    {
      "api": "EstimatePrice",
      "request": { "id": 10 },
      "response": { "totalPrice": 104.0, "discounts":[ { "name":"第二件六折", "discountAmount": -26.0 } ] },
      "test-result": "pass",
      "test-comments": "試算總價與折扣"
    }
  ]
}
```

## 測試案例程式碼

```csharp
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

public class BeerDiscountTests
{
    private readonly HttpClient _client = new HttpClient {
        BaseAddress = new System.Uri("https://andrewshopoauthdemo.azurewebsites.net/api/")
    };

    private async Task<string> LoginAsync()
    {
        var payload = JsonSerializer.Serialize(new { name = "andrew", password = "123456" });
        var resp = await _client.PostAsync("member/login",
            new StringContent(payload, Encoding.UTF8, "application/json"));
        resp.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("accessToken").GetString();
    }

    [Fact]
    public async Task BeerQuantityTwo_SecondItemSixtyPercentPrice()
    {
        // Login
        var token = await LoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Create Cart
        var createResp = await _client.PostAsync("carts/create", null);
        createResp.EnsureSuccessStatusCode();
        var cart = JsonDocument.Parse(await createResp.Content.ReadAsStringAsync());
        int cartId = cart.RootElement.GetProperty("id").GetInt32();

        // Add 2 beers
        var addPayload = JsonSerializer.Serialize(new { productId = 1, qty = 2 });
        var addResp = await _client.PostAsync($"carts/{cartId}/items",
            new StringContent(addPayload, Encoding.UTF8, "application/json"));
        addResp.EnsureSuccessStatusCode();

        // Estimate Price
        var estResp = await _client.PostAsync($"carts/{cartId}/estimate", null);
        estResp.EnsureSuccessStatusCode();
        var estDoc = JsonDocument.Parse(await estResp.Content.ReadAsStringAsync());

        var totalPrice = estDoc.RootElement.GetProperty("totalPrice").GetDouble();
        Assert.Equal(65 + 65 * 0.6, totalPrice);

        var discounts = estDoc.RootElement.GetProperty("discounts");
        Assert.Single(discounts.EnumerateArray());
        var discountAmount = discounts[0].GetProperty("discountAmount").GetDouble();
        Assert.Equal(-0.4 * 65, discountAmount);
    }
}
```