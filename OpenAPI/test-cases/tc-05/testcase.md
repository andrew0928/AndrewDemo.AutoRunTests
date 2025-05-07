# Test Case: TC-05 (非法上界)

## Context
- shop: shop123
- user: { user: andrew, password: 123456 }
- location: { id: zh-TW, time-zone: UTC+8, currency: TWD }

## Given
- 測試前請清空購物車
- 指定測試商品: 可口可樂

## When
- step 1, 嘗試加入 11 件指定商品
- step 2, 檢查購物車內容

## Then
- 執行 step 1 時，伺服器應回傳 400 Bad Request（數量超出 10）
- step 2 查詢結果，購物車應該是空的