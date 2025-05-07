# 測試案例名稱  
TC-01：啤酒數量 4（折扣兩次）  

## 測試步驟  

**Context**:  
- shop: shop8  
- user:  
  - account: andrew  
  - access_token: 5465c5d13370483286b0ff018901b985  
- location:  
  - id: zh‑TW  
  - time‑zone: UTC+8  
  - currency: TWD  

**Given**:

|步驟名稱|API               |Request                        |Response                                                           |測試結果|測試說明             |
|--------|------------------|-------------------------------|-------------------------------------------------------------------|--------|--------------------|
|step 1  |LoginMember       |{ name: "andrew", password:"123456" }|200 OK, { access_token: "5465c5d13370483286b0ff018901b985" }|pass    |使用者成功登入        |
|step 2  |CreateCart        |{}                              |201 Created, { id:11, lineItems:[] }                               |pass    |新購物車建立且為空    |
|step 3  |GetProducts       |{}                              |200 OK, 回傳商品清單 (包含啤酒 id=1, price=65)                       |pass    |已取得啤酒商品資訊    |

**When**:

|步驟名稱|API               |Request                        |Response                                                           |測試結果|測試說明              |
|--------|------------------|-------------------------------|-------------------------------------------------------------------|--------|---------------------|
|step 1  |AddItemToCart     |{ id:11, productId:1, qty:4 }  |200 OK, { id:11, lineItems:[{ productId:1, qty:4 }] }              |pass    |成功加入 4 件啤酒     |
|step 2  |EstimatePrice     |{ id:11 }                      |200 OK, { totalPrice:208.0, discounts:[{…,-26.0},{…,-26.0}] }        |pass    |試算出正確總價與折扣   |

**Then**:

- 預期結果1: 購物車僅含啤酒，數量顯示為 4。  
  測試結果：已從 AddItemToCart 回應確認 lineItems[0].qty = 4。  
- 預期結果2: 折扣應在第 2、4 件各套用一次「六折」。  
  測試結果：EstimatePrice 回傳 discounts 陣列有 2 筆，每筆折抵 0.4 × 65 = 26。  
- 預期結果3: 應付總額 = 4 × 65 − 2 × (0.4 × 65) = 208.0。  
  測試結果：EstimatePrice.totalPrice = 208.0，計算正確。  

## 測試結果  

**測試結果**: 測試通過 (test_pass)  

本案例所有步驟皆符合預期，通過測試。  

## 結構化測試報告  

```json
{
  "name": "TC-01：啤酒數量 4（折扣兩次）",
  "result": "test_pass",
  "comments": "所有步驟符合預期，折扣與總價計算皆正確。",
  "context": {
    "shop": "shop8",
    "user": {
      "user": "andrew",
      "access_token": "5465c5d13370483286b0ff018901b985"
    },
    "location": {
      "id": "zh‑TW",
      "time‑zone": "UTC+8",
      "currency": "TWD"
    }
  },
  "steps": [
    {
      "api": "LoginMember",
      "request": { "name": "andrew", "password": "123456" },
      "response": { "access_token": "5465c5d13370483286b0ff018901b985" },
      "test-result": "pass",
      "test-comments": "使用者成功登入"
    },
    {
      "api": "CreateCart",
      "request": {},
      "response": { "id": 11, "lineItems": [] },
      "test-result": "pass",
      "test-comments": "新購物車建立且為空"
    },
    {
      "api": "GetProducts",
      "request": {},
      "response": [
        { "id": 1, "name": "18天台灣生啤酒 355ml", "price": 65 },
        { "id": 2, "name": "可口可樂® 350ml", "price": 18 },
        { "id": 3, "name": "御茶園 特撰冰釀綠茶 550ml", "price": 25 }
      ],
      "test-result": "pass",
      "test-comments": "已取得商品清單，包含啤酒"
    },
    {
      "api": "AddItemToCart",
      "request": { "id": 11, "productId": 1, "qty": 4 },
      "response": { "id": 11, "lineItems": [ { "productId": 1, "qty": 4 } ] },
      "test-result": "pass",
      "test-comments": "成功加入 4 件啤酒"
    },
    {
      "api": "EstimatePrice",
      "request": { "id": 11 },
      "response": { "totalPrice": 208.0, "discounts": [ { "discountAmount": -26.0 }, { "discountAmount": -26.0 } ] },
      "test-result": "pass",
      "test-comments": "試算出正確總價與折扣"
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
    private readonly HttpClient _client;

    public BeerDiscountTests()
    {
        _client = new HttpClient();
        _client.BaseAddress = new System.Uri("https://andrewshopoauthdemo.azurewebsites.net/api/");
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private async Task<string> LoginAsync(string name, string password)
    {
        var payload = JsonSerializer.Serialize(new { name, password });
        var resp = await _client.PostAsync("members/login",
            new StringContent(payload, Encoding.UTF8, "application/json"));
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("access_token").GetString();
    }

    [Fact]
    public async Task TC01_BeerQuantity4_TwoDiscounts()
    {
        // 1. 使用者登入
        var token = await LoginAsync("andrew", "123456");
        _client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", token);

        // 2. 建立新購物車
        var cartResp = await _client.PostAsync("carts/create", null);
        cartResp.EnsureSuccessStatusCode();
        var cartJson = await cartResp.Content.ReadAsStringAsync();
        var cart = JsonDocument.Parse(cartJson).RootElement;
        int cartId = cart.GetProperty("id").GetInt32();

        // 3. 加入 4 件啤酒 (productId = 1)
        var addPayload = JsonSerializer.Serialize(new { productId = 1, qty = 4 });
        var addResp = await _client.PostAsync($"carts/{cartId}/items",
            new StringContent(addPayload, Encoding.UTF8, "application/json"));
        addResp.EnsureSuccessStatusCode();

        // 4. 試算價格
        var estimateResp = await _client.PostAsync($"carts/{cartId}/estimate", null);
        estimateResp.EnsureSuccessStatusCode();
        var estimateJson = await estimateResp.Content.ReadAsStringAsync();
        var estimate = JsonDocument.Parse(estimateJson).RootElement;

        // 驗證折扣次數與金額
        var discounts = estimate.GetProperty("discounts");
        Assert.Equal(2, discounts.GetArrayLength());
        foreach (var d in discounts.EnumerateArray())
        {
            Assert.Equal(-26.0, d.GetProperty("discountAmount").GetDouble());
        }

        // 驗證總價
        double total = estimate.GetProperty("totalPrice").GetDouble();
        Assert.Equal(208.0, total);
    }
}
```