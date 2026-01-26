# Phase 6 API Testing Script
# Tests all B2B API features implemented in Phase 6
#
# IMPORTANT: Order Type Enum Values
# - 0 = Incoming (receiving inventory)
# - 1 = Outgoing (shipping out inventory)
#
# Order Status Enum Values
# - 0 = Pending
# - 1 = Approved
# - 2 = Shipped
# - 3 = Delivered
# - 4 = Cancelled

Write-Host "`n========================================" -ForegroundColor Magenta
Write-Host "   PHASE 6 API TESTING - COMPREHENSIVE" -ForegroundColor Magenta
Write-Host "========================================`n" -ForegroundColor Magenta

$baseUrl = "http://localhost:5142"
$testResults = @()

# Helper function to record test results
function Record-Test {
    param($name, $success, $details)
    $script:testResults += [PSCustomObject]@{
        Test = $name
        Result = if($success) { "✓ PASS" } else { "✗ FAIL" }
        Details = $details
    }
}

# ============================================
# TEST 1: JWT Authentication - Get Token
# ============================================
Write-Host "=== TEST 1: JWT Authentication - Get Token ===" -ForegroundColor Cyan
try {
    $tokenRequest = @{
        email = "client@company.com"
        apiKey = "demo-api-key-12345"
    } | ConvertTo-Json

    $response = Invoke-RestMethod -Uri "$baseUrl/api/auth/token" `
        -Method POST `
        -ContentType "application/json" `
        -Body $tokenRequest

    $token = $response.token
    $expiresIn = $response.expiresIn
    
    Write-Host "✓ Token Generated Successfully!" -ForegroundColor Green
    Write-Host "  - Token Length: $($token.Length) characters" -ForegroundColor White
    Write-Host "  - Token Type: $($response.tokenType)" -ForegroundColor White
    Write-Host "  - Expires In: $expiresIn seconds ($([math]::Round($expiresIn/3600, 2)) hours)" -ForegroundColor White
    Write-Host "  - Token Preview: $($token.Substring(0, 50))...`n" -ForegroundColor White
    
    Record-Test "JWT Token Generation" $true "Token: $($token.Length) chars, Expires: $expiresIn sec"
    
    # Store token for subsequent tests
    $global:token = $token
    $global:headers = @{
        "Authorization" = "Bearer $token"
        "Content-Type" = "application/json"
    }
} catch {
    Write-Host "✗ FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Record-Test "JWT Token Generation" $false $_.Exception.Message
    exit 1
}

# ============================================
# TEST 2: Token Validation
# ============================================
Write-Host "=== TEST 2: Token Validation ===" -ForegroundColor Cyan
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/auth/validate" `
        -Method GET `
        -Headers $global:headers

    Write-Host "✓ Token Validated Successfully!" -ForegroundColor Green
    Write-Host "  - Valid: $($response.valid)" -ForegroundColor White
    Write-Host "  - Email: $($response.email)" -ForegroundColor White
    Write-Host "  - Role: $($response.role)`n" -ForegroundColor White
    
    Record-Test "Token Validation" $true "Valid: $($response.valid), Role: $($response.role)"
} catch {
    Write-Host "✗ FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Record-Test "Token Validation" $false $_.Exception.Message
}

# ============================================
# TEST 3: Get All Products
# ============================================
Write-Host "=== TEST 3: Get All Products ===" -ForegroundColor Cyan
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/products" `
        -Method GET `
        -Headers $global:headers

    Write-Host "✓ Products Retrieved!" -ForegroundColor Green
    Write-Host "  - Success: $($response.success)" -ForegroundColor White
    Write-Host "  - Total Products: $($response.data.Count)" -ForegroundColor White
    if ($response.data.Count -gt 0) {
        Write-Host "  - Sample Product: $($response.data[0].name) (SKU: $($response.data[0].sku), Stock: $($response.data[0].stockQuantity))" -ForegroundColor White
    }
    Write-Host ""
    
    Record-Test "Get All Products" $true "Retrieved $($response.data.Count) products"
    $global:testProductId = $response.data[0].id
} catch {
    Write-Host "✗ FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Record-Test "Get All Products" $false $_.Exception.Message
}

# ============================================
# TEST 4: Get Product by ID
# ============================================
if ($global:testProductId) {
    Write-Host "=== TEST 4: Get Product by ID ===" -ForegroundColor Cyan
    try {
        $response = Invoke-RestMethod -Uri "$baseUrl/api/products/$($global:testProductId)" `
            -Method GET `
            -Headers $global:headers

        Write-Host "✓ Product Retrieved!" -ForegroundColor Green
        Write-Host "  - ID: $($response.data.id)" -ForegroundColor White
        Write-Host "  - Name: $($response.data.name)" -ForegroundColor White
        Write-Host "  - SKU: $($response.data.sku)" -ForegroundColor White
        Write-Host "  - Price: `$$($response.data.price)" -ForegroundColor White
        Write-Host "  - Stock: $($response.data.stockQuantity)`n" -ForegroundColor White
        
        Record-Test "Get Product by ID" $true "Product: $($response.data.name)"
    } catch {
        Write-Host "✗ FAILED: $($_.Exception.Message)" -ForegroundColor Red
        Record-Test "Get Product by ID" $false $_.Exception.Message
    }
}

# ============================================
# TEST 5: Filter Products by Category
# ============================================
Write-Host "=== TEST 5: Filter Products by Category (Electronics) ===" -ForegroundColor Cyan
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/products?category=Electronics" `
        -Method GET `
        -Headers $global:headers

    Write-Host "✓ Filtered Products Retrieved!" -ForegroundColor Green
    Write-Host "  - Electronics Count: $($response.data.Count)" -ForegroundColor White
    if ($response.data.Count -gt 0) {
        Write-Host "  - Examples: $($response.data[0..2].name -join ', ')" -ForegroundColor White
    }
    Write-Host ""
    
    Record-Test "Filter by Category" $true "Found $($response.data.Count) electronics"
} catch {
    Write-Host "✗ FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Record-Test "Filter by Category" $false $_.Exception.Message
}

# ============================================
# TEST 6: Search Products
# ============================================
Write-Host "=== TEST 6: Search Products (keyword: 'laptop') ===" -ForegroundColor Cyan
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/products?search=laptop" `
        -Method GET `
        -Headers $global:headers

    Write-Host "✓ Search Results Retrieved!" -ForegroundColor Green
    Write-Host "  - Results Count: $($response.data.Count)" -ForegroundColor White
    if ($response.data.Count -gt 0) {
        Write-Host "  - Found: $($response.data[0].name)`n" -ForegroundColor White
    }
    
    Record-Test "Search Products" $true "Found $($response.data.Count) results for 'laptop'"
} catch {
    Write-Host "✗ FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Record-Test "Search Products" $false $_.Exception.Message
}

# ============================================
# TEST 7: Get Low Stock Products
# ============================================
Write-Host "=== TEST 7: Get Low Stock Products ===" -ForegroundColor Cyan
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/products/low-stock" `
        -Method GET `
        -Headers $global:headers

    Write-Host "✓ Low Stock Products Retrieved!" -ForegroundColor Green
    Write-Host "  - Low Stock Count: $($response.data.Count)" -ForegroundColor White
    if ($response.data.Count -gt 0) {
        $product = $response.data[0]
        Write-Host "  - Example: $($product.name) (Stock: $($product.stockQuantity), Reorder: $($product.reorderLevel))`n" -ForegroundColor White
    }
    
    Record-Test "Get Low Stock" $true "Found $($response.data.Count) low stock products"
} catch {
    Write-Host "✗ FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Record-Test "Get Low Stock" $false $_.Exception.Message
}

# ============================================
# TEST 8: Get Product Categories
# ============================================
Write-Host "=== TEST 8: Get Product Categories ===" -ForegroundColor Cyan
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/products/categories" `
        -Method GET `
        -Headers $global:headers

    Write-Host "✓ Categories Retrieved!" -ForegroundColor Green
    Write-Host "  - Categories: $($response.data -join ', ')`n" -ForegroundColor White
    
    Record-Test "Get Categories" $true "Found $($response.data.Count) categories"
} catch {
    Write-Host "✗ FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Record-Test "Get Categories" $false $_.Exception.Message
}

# ============================================
# TEST 9: Create New Product
# ============================================
Write-Host "=== TEST 9: Create New Product via API ===" -ForegroundColor Cyan
try {
    $newProduct = @{
        sku = "TEST-API-$(Get-Random -Maximum 9999)"
        name = "Test Product from API"
        description = "Created via B2B API for testing"
        price = 99.99
        category = "Testing"
        stockQuantity = 50
        reorderLevel = 10
    } | ConvertTo-Json

    $response = Invoke-RestMethod -Uri "$baseUrl/api/products" `
        -Method POST `
        -Headers $global:headers `
        -Body $newProduct

    Write-Host "✓ Product Created Successfully!" -ForegroundColor Green
    Write-Host "  - ID: $($response.data.id)" -ForegroundColor White
    Write-Host "  - Name: $($response.data.name)" -ForegroundColor White
    Write-Host "  - SKU: $($response.data.sku)`n" -ForegroundColor White
    
    Record-Test "Create Product" $true "Created product ID: $($response.data.id)"
    $global:createdProductId = $response.data.id
} catch {
    Write-Host "✗ FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Record-Test "Create Product" $false $_.Exception.Message
}

# ============================================
# TEST 10: Update Product
# ============================================
if ($global:createdProductId) {
    Write-Host "=== TEST 10: Update Product via API ===" -ForegroundColor Cyan
    try {
        $updateData = @{
            price = 149.99
            stockQuantity = 75
        } | ConvertTo-Json

        $response = Invoke-RestMethod -Uri "$baseUrl/api/products/$($global:createdProductId)" `
            -Method PUT `
            -Headers $global:headers `
            -Body $updateData

        Write-Host "✓ Product Updated Successfully!" -ForegroundColor Green
        Write-Host "  - New Price: `$$($response.data.price)" -ForegroundColor White
        Write-Host "  - New Stock: $($response.data.stockQuantity)`n" -ForegroundColor White
        
        Record-Test "Update Product" $true "Updated product ID: $($global:createdProductId)"
    } catch {
        Write-Host "✗ FAILED: $($_.Exception.Message)" -ForegroundColor Red
        Record-Test "Update Product" $false $_.Exception.Message
    }
}

# ============================================
# TEST 11: Get All Orders
# ============================================
Write-Host "=== TEST 11: Get All Orders ===" -ForegroundColor Cyan
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/orders" `
        -Method GET `
        -Headers $global:headers

    Write-Host "✓ Orders Retrieved!" -ForegroundColor Green
    Write-Host "  - Total Orders: $($response.data.Count)" -ForegroundColor White
    if ($response.data.Count -gt 0) {
        $order = $response.data[0]
        Write-Host "  - Sample Order: #$($order.orderNumber) - Status: $($order.status), Total: `$$($order.totalAmount)`n" -ForegroundColor White
    }
    
    Record-Test "Get All Orders" $true "Retrieved $($response.data.Count) orders"
    if ($response.data.Count -gt 0) {
        $global:testOrderId = $response.data[0].id
    }
} catch {
    Write-Host "✗ FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Record-Test "Get All Orders" $false $_.Exception.Message
}

# ============================================
# TEST 12: Filter Orders by Status
# ============================================
Write-Host "=== TEST 12: Filter Orders by Status (Pending) ===" -ForegroundColor Cyan
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/orders?status=Pending" `
        -Method GET `
        -Headers $global:headers

    Write-Host "✓ Filtered Orders Retrieved!" -ForegroundColor Green
    Write-Host "  - Pending Orders: $($response.data.Count)`n" -ForegroundColor White
    
    Record-Test "Filter Orders by Status" $true "Found $($response.data.Count) pending orders"
} catch {
    Write-Host "✗ FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Record-Test "Filter Orders by Status" $false $_.Exception.Message
}

# ============================================
# TEST 13: Create New Order
# ============================================
Write-Host "=== TEST 13: Create New Order via API ===" -ForegroundColor Cyan
try {
    if (-not $global:testProductId) {
        throw "No test product ID available"
    }
    
    $newOrder = @{
        type = "Incoming"
        notes = "Test order created via API - Phase 6 Testing"
        supplierId = 1
        items = @(
            @{
                productId = $global:testProductId
                quantity = 25
                unitPrice = 99.99
            }
        )
    } | ConvertTo-Json -Depth 5

    $response = Invoke-RestMethod -Uri "$baseUrl/api/orders" `
        -Method POST `
        -Headers $global:headers `
        -Body $newOrder

    Write-Host "✓ Order Created Successfully!" -ForegroundColor Green
    Write-Host "  - Order ID: $($response.data.id)" -ForegroundColor White
    Write-Host "  - Order Number: $($response.data.orderNumber)" -ForegroundColor White
    Write-Host "  - Status: $($response.data.status)" -ForegroundColor White
    Write-Host "  - Total: `$$($response.data.totalAmount)`n" -ForegroundColor White
    
    Record-Test "Create Order" $true "Created order #$($response.data.orderNumber)"
    $global:createdOrderId = $response.data.id
} catch {
    Write-Host "✗ FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Record-Test "Create Order" $false $_.Exception.Message
}

# ============================================
# TEST 14: Get Order by ID
# ============================================
if ($global:createdOrderId) {
    Write-Host "=== TEST 14: Get Order by ID ===" -ForegroundColor Cyan
    try {
        $response = Invoke-RestMethod -Uri "$baseUrl/api/orders/$($global:createdOrderId)" `
            -Method GET `
            -Headers $global:headers

        Write-Host "✓ Order Retrieved!" -ForegroundColor Green
        Write-Host "  - Order Number: $($response.data.orderNumber)" -ForegroundColor White
        Write-Host "  - Status: $($response.data.status)" -ForegroundColor White
        Write-Host "  - Items Count: $($response.data.items.Count)" -ForegroundColor White
        Write-Host "  - Total Amount: `$$($response.data.totalAmount)`n" -ForegroundColor White
        
        Record-Test "Get Order by ID" $true "Order #$($response.data.orderNumber)"
    } catch {
        Write-Host "✗ FAILED: $($_.Exception.Message)" -ForegroundColor Red
        Record-Test "Get Order by ID" $false $_.Exception.Message
    }
}

# ============================================
# TEST 15: Update Order Status
# ============================================
if ($global:createdOrderId) {
    Write-Host "=== TEST 15: Update Order Status ===" -ForegroundColor Cyan
    try {
        $statusUpdate = @{
            status = "Approved"
        } | ConvertTo-Json

        $response = Invoke-RestMethod -Uri "$baseUrl/api/orders/$($global:createdOrderId)/status" `
            -Method PATCH `
            -Headers $global:headers `
            -Body $statusUpdate

        Write-Host "✓ Order Status Updated!" -ForegroundColor Green
        Write-Host "  - New Status: $($response.data.status)`n" -ForegroundColor White
        
        Record-Test "Update Order Status" $true "Status changed to: $($response.data.status)"
    } catch {
        Write-Host "✗ FAILED: $($_.Exception.Message)" -ForegroundColor Red
        Record-Test "Update Order Status" $false $_.Exception.Message
    }
}

# ============================================
# TEST 16: Rate Limiting Test
# ============================================
Write-Host "=== TEST 16: Rate Limiting Test (checking headers) ===" -ForegroundColor Cyan
try {
    $response = Invoke-WebRequest -Uri "$baseUrl/api/products" `
        -Method GET `
        -Headers $global:headers

    $rateLimit = $response.Headers["X-RateLimit-Limit"]
    $rateRemaining = $response.Headers["X-RateLimit-Remaining"]
    $rateReset = $response.Headers["X-RateLimit-Reset"]
    
    Write-Host "✓ Rate Limiting Headers Present!" -ForegroundColor Green
    Write-Host "  - Limit: $rateLimit requests per window" -ForegroundColor White
    Write-Host "  - Remaining: $rateRemaining requests" -ForegroundColor White
    Write-Host "  - Reset: $rateReset`n" -ForegroundColor White
    
    Record-Test "Rate Limiting Headers" $true "Limit: $rateLimit, Remaining: $rateRemaining"
} catch {
    Write-Host "✗ FAILED: $($_.Exception.Message)" -ForegroundColor Red
    Record-Test "Rate Limiting Headers" $false $_.Exception.Message
}

# ============================================
# TEST 17: Delete Product (Cleanup)
# ============================================
if ($global:createdProductId) {
    Write-Host "=== TEST 17: Delete Product (Cleanup) ===" -ForegroundColor Cyan
    try {
        $response = Invoke-RestMethod -Uri "$baseUrl/api/products/$($global:createdProductId)" `
            -Method DELETE `
            -Headers $global:headers

        Write-Host "✓ Product Deleted Successfully!" -ForegroundColor Green
        Write-Host "  - Message: $($response.message)`n" -ForegroundColor White
        
        Record-Test "Delete Product" $true "Deleted product ID: $($global:createdProductId)"
    } catch {
        Write-Host "✗ FAILED: $($_.Exception.Message)" -ForegroundColor Red
        Record-Test "Delete Product" $false $_.Exception.Message
    }
}

# ============================================
# TEST 18: Invalid Token Test
# ============================================
Write-Host "=== TEST 18: Invalid Token Test (Security) ===" -ForegroundColor Cyan
try {
    $invalidHeaders = @{
        "Authorization" = "Bearer invalid-token-12345"
        "Content-Type" = "application/json"
    }
    
    $response = Invoke-RestMethod -Uri "$baseUrl/api/products" `
        -Method GET `
        -Headers $invalidHeaders
    
    Write-Host "✗ SECURITY ISSUE: Invalid token was accepted!" -ForegroundColor Red
    Record-Test "Invalid Token Rejection" $false "Invalid token was accepted (security issue)"
} catch {
    if ($_.Exception.Response.StatusCode.value__ -eq 401) {
        Write-Host "✓ Invalid Token Correctly Rejected (401 Unauthorized)!" -ForegroundColor Green
        Record-Test "Invalid Token Rejection" $true "401 Unauthorized for invalid token"
    } else {
        Write-Host "✗ Unexpected error: $($_.Exception.Message)" -ForegroundColor Red
        Record-Test "Invalid Token Rejection" $false $_.Exception.Message
    }
}

# ============================================
# Summary
# ============================================
Write-Host "`n========================================" -ForegroundColor Magenta
Write-Host "          TEST RESULTS SUMMARY" -ForegroundColor Magenta
Write-Host "========================================`n" -ForegroundColor Magenta

$testResults | Format-Table -AutoSize

$passCount = ($testResults | Where-Object { $_.Result -eq "✓ PASS" }).Count
$failCount = ($testResults | Where-Object { $_.Result -eq "✗ FAIL" }).Count
$totalTests = $testResults.Count

Write-Host "`nTotal Tests: $totalTests" -ForegroundColor White
Write-Host "Passed: $passCount" -ForegroundColor Green
Write-Host "Failed: $failCount" -ForegroundColor Red
Write-Host "Success Rate: $([math]::Round(($passCount/$totalTests)*100, 2))%`n" -ForegroundColor Cyan

if ($failCount -eq 0) {
    Write-Host "🎉 ALL TESTS PASSED! Phase 6 API is fully functional." -ForegroundColor Green
} else {
    Write-Host "⚠️  Some tests failed. Review the results above." -ForegroundColor Yellow
}
