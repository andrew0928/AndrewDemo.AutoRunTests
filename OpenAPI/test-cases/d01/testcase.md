# Test Case: 啤酒數量 1（無折扣驗證）

## Context

- shop: shop123  
- user: { user: andrew, password: 123456 }  
- location: { id: zh-TW, time-zone: UTC+8, currency: TWD }  

## Given

- 測試前請清空購物車  
- 指定測試商品: 啤酒

## When

1. 使用者登入後，建立一個全新的購物車。  
2. 在購物車中加入 **1 件**啤酒。  
3. 立即對當前購物車執行金額試算，以取得結帳總額與折扣資訊。  

## Then 

- 購物車僅含一項啤酒，數量顯示為 **1**。  
- 試算結果中的 **應付總額** 等於啤酒單價。  
- 折扣清單（若有呈現）為 **空**，或完全不出現折扣欄位。