# Test Case: TC-03 (上界成功: 10 --> 9)

## Context

- shop: shop123
- user: { user: andrew, password: 123456 }
- location: { id: zh-TW, time-zone: UTC+8, currency: TWD }


## Given

- 測試前請清空購物車
- 指定測試商品: 可口可樂


## When

- step 1, 加入 10 件指定商品
- step 2, 移除 1 件指定商品 
- step 3, 檢查購物車內容


## Then

- 預期購物車內容應該是 9 件可口可樂