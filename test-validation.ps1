# API Validation Testing Script
# Tests all validation rules for order creation

Write-Host "`n========================================" -ForegroundColor Magenta
Write-Host "  API VALIDATION TESTING" -ForegroundColor Magenta
Write-Host "========================================`n" -ForegroundColor Magenta

$baseUrl = "http://localhost:5142"

# Get authentication token
Write-Host "Getting authentication token..." -ForegroundColor Cyan
$tokenRequest = @{
    email = "client@company.com"
    apiKey = "demo-api-key-12345"
} | ConvertTo-Json

$tokenResponse = Invoke-RestMethod -Uri "$baseUrl/api/auth/token" `
    -Method POST `
    -ContentType "application/json" `
    -Body $tokenRequest

$token = $tokenResponse.token
$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}
Write-Host "✓ Token acquired`n" -ForegroundColor Green

# Get valid product and supplier for testing
$products = Invoke-RestMethod -Uri "$baseUrl/api/products" -Method GET -Headers $headers
$validProductId = $products.data[0].id
$validProductStock = $products.data[0].stockQuantity
Write-Host "Using Product ID: $validProductId (Stock: $validProductStock)`n" -ForegroundColor Yellow

# ============================================
# TEST 1: Invalid Supplier ID
# ============================================
Write-Host "=== TEST 1: Invalid Supplier ID ===" -ForegroundColor Cyan
try {
    $orderData = @{
        type = 0
        notes = "Test with invalid supplier"
        supplierId = 99999  # Non-existent supplier
        items = @(@{ productId = $validProductId; quantity = 5; unitPrice = 99.99 })
    } | ConvertTo-Json -Depth 5

    $response = Invoke-RestMethod -Uri "$baseUrl/api/orders" `
        -Method POST `
        -Headers $headers `
        -Body $orderData
    
    Write-Host "✗ FAIL: Invalid supplier was accepted!" -ForegroundColor Red
} catch {
    $errorResponse = $_.ErrorDetails.Message | ConvertFrom-Json
    if ($errorResponse.message -like "*Supplier*not found*") {
        Write-Host "✓ PASS: Invalid supplier correctly rejected" -ForegroundColor Green
        Write-Host "  Error: $($errorResponse.message)" -ForegroundColor White
        Write-Host "  Errors: $($errorResponse.errors -join ', ')`n" -ForegroundColor White
    } else {
        Write-Host "✗ FAIL: Unexpected error: $($errorResponse.message)" -ForegroundColor Red
    }
}

# ============================================
# TEST 2: Invalid Product ID
# ============================================
Write-Host "=== TEST 2: Invalid Product ID ===" -ForegroundColor Cyan
try {
    $orderData = @{
        type = 0
        notes = "Test with invalid product"
        supplierId = 1
        items = @(@{ productId = 99999; quantity = 5; unitPrice = 99.99 })  # Non-existent product
    } | ConvertTo-Json -Depth 5

    $response = Invoke-RestMethod -Uri "$baseUrl/api/orders" `
        -Method POST `
        -Headers $headers `
        -Body $orderData
    
    Write-Host "✗ FAIL: Invalid product was accepted!" -ForegroundColor Red
} catch {
    $errorResponse = $_.ErrorDetails.Message | ConvertFrom-Json
    if ($errorResponse.message -like "*Products not found*") {
        Write-Host "✓ PASS: Invalid product correctly rejected" -ForegroundColor Green
        Write-Host "  Error: $($errorResponse.message)" -ForegroundColor White
        Write-Host "  Errors: $($errorResponse.errors -join ', ')`n" -ForegroundColor White
    } else {
        Write-Host "✗ FAIL: Unexpected error: $($errorResponse.message)" -ForegroundColor Red
    }
}

# ============================================
# TEST 3: Outgoing Order - Insufficient Stock
# ============================================
Write-Host "=== TEST 3: Outgoing Order - Insufficient Stock ===" -ForegroundColor Cyan
try {
    $excessiveQuantity = $validProductStock + 100  # More than available
    $orderData = @{
        type = 1  # Outgoing
        notes = "Test with insufficient stock"
        supplierId = 1
        items = @(@{ productId = $validProductId; quantity = $excessiveQuantity; unitPrice = 99.99 })
    } | ConvertTo-Json -Depth 5

    Write-Host "  Requesting $excessiveQuantity units (Available: $validProductStock)" -ForegroundColor Yellow

    $response = Invoke-RestMethod -Uri "$baseUrl/api/orders" `
        -Method POST `
        -Headers $headers `
        -Body $orderData
    
    Write-Host "✗ FAIL: Insufficient stock was accepted!" -ForegroundColor Red
} catch {
    $errorResponse = $_.ErrorDetails.Message | ConvertFrom-Json
    if ($errorResponse.message -like "*Stock validation failed*" -or $errorResponse.errors[0] -like "*Insufficient stock*") {
        Write-Host "✓ PASS: Insufficient stock correctly rejected" -ForegroundColor Green
        Write-Host "  Error: $($errorResponse.message)" -ForegroundColor White
        Write-Host "  Details: $($errorResponse.errors -join ', ')`n" -ForegroundColor White
    } else {
        Write-Host "✗ FAIL: Unexpected error: $($errorResponse.message)" -ForegroundColor Red
    }
}

# ============================================
# TEST 4: Incoming Order - No Stock Validation
# ============================================
Write-Host "=== TEST 4: Incoming Order - No Stock Check (Should Pass) ===" -ForegroundColor Cyan
try {
    $largeQuantity = $validProductStock + 100  # More than current stock (but incoming)
    $orderData = @{
        type = 0  # Incoming - should NOT check stock
        notes = "Test incoming order with large quantity"
        supplierId = 1
        items = @(@{ productId = $validProductId; quantity = $largeQuantity; unitPrice = 99.99 })
    } | ConvertTo-Json -Depth 5

    Write-Host "  Ordering $largeQuantity units (Available: $validProductStock)" -ForegroundColor Yellow

    $response = Invoke-RestMethod -Uri "$baseUrl/api/orders" `
        -Method POST `
        -Headers $headers `
        -Body $orderData
    
    Write-Host "✓ PASS: Incoming order accepted regardless of stock" -ForegroundColor Green
    Write-Host "  Order Number: $($response.data.orderNumber)" -ForegroundColor White
    Write-Host "  Quantity: $($response.data.items[0].quantity) (stock check bypassed for incoming)`n" -ForegroundColor White
} catch {
    $errorResponse = $_.ErrorDetails.Message | ConvertFrom-Json
    Write-Host "✗ FAIL: Incoming order was rejected" -ForegroundColor Red
    Write-Host "  Error: $($errorResponse.message)`n" -ForegroundColor Red
}

# ============================================
# TEST 5: Zero Quantity
# ============================================
Write-Host "=== TEST 5: Zero Quantity ===" -ForegroundColor Cyan
try {
    $orderData = @{
        type = 0
        notes = "Test with zero quantity"
        supplierId = 1
        items = @(@{ productId = $validProductId; quantity = 0; unitPrice = 99.99 })
    } | ConvertTo-Json -Depth 5

    $response = Invoke-RestMethod -Uri "$baseUrl/api/orders" `
        -Method POST `
        -Headers $headers `
        -Body $orderData
    
    Write-Host "✗ FAIL: Zero quantity was accepted!" -ForegroundColor Red
} catch {
    $errorResponse = $_.ErrorDetails.Message | ConvertFrom-Json
    if ($errorResponse.message -like "*quantities must be greater than zero*") {
        Write-Host "✓ PASS: Zero quantity correctly rejected" -ForegroundColor Green
        Write-Host "  Error: $($errorResponse.message)`n" -ForegroundColor White
    } else {
        Write-Host "✗ FAIL: Unexpected error: $($errorResponse.message)" -ForegroundColor Red
    }
}

# ============================================
# TEST 6: Negative Quantity
# ============================================
Write-Host "=== TEST 6: Negative Quantity ===" -ForegroundColor Cyan
try {
    $orderData = @{
        type = 0
        notes = "Test with negative quantity"
        supplierId = 1
        items = @(@{ productId = $validProductId; quantity = -5; unitPrice = 99.99 })
    } | ConvertTo-Json -Depth 5

    $response = Invoke-RestMethod -Uri "$baseUrl/api/orders" `
        -Method POST `
        -Headers $headers `
        -Body $orderData
    
    Write-Host "✗ FAIL: Negative quantity was accepted!" -ForegroundColor Red
} catch {
    $errorResponse = $_.ErrorDetails.Message | ConvertFrom-Json
    if ($errorResponse.message -like "*quantities must be greater than zero*") {
        Write-Host "✓ PASS: Negative quantity correctly rejected" -ForegroundColor Green
        Write-Host "  Error: $($errorResponse.message)`n" -ForegroundColor White
    } else {
        Write-Host "✗ FAIL: Unexpected error: $($errorResponse.message)" -ForegroundColor Red
    }
}

# ============================================
# TEST 7: Negative Price
# ============================================
Write-Host "=== TEST 7: Negative Unit Price ===" -ForegroundColor Cyan
try {
    $orderData = @{
        type = 0
        notes = "Test with negative price"
        supplierId = 1
        items = @(@{ productId = $validProductId; quantity = 5; unitPrice = -99.99 })
    } | ConvertTo-Json -Depth 5

    $response = Invoke-RestMethod -Uri "$baseUrl/api/orders" `
        -Method POST `
        -Headers $headers `
        -Body $orderData
    
    Write-Host "✗ FAIL: Negative price was accepted!" -ForegroundColor Red
} catch {
    $errorResponse = $_.ErrorDetails.Message | ConvertFrom-Json
    if ($errorResponse.message -like "*prices cannot be negative*") {
        Write-Host "✓ PASS: Negative price correctly rejected" -ForegroundColor Green
        Write-Host "  Error: $($errorResponse.message)`n" -ForegroundColor White
    } else {
        Write-Host "✗ FAIL: Unexpected error: $($errorResponse.message)" -ForegroundColor Red
    }
}

# ============================================
# TEST 8: Empty Items Array
# ============================================
Write-Host "=== TEST 8: Empty Items Array ===" -ForegroundColor Cyan
try {
    $orderData = @{
        type = 0
        notes = "Test with no items"
        supplierId = 1
        items = @()
    } | ConvertTo-Json -Depth 5

    $response = Invoke-RestMethod -Uri "$baseUrl/api/orders" `
        -Method POST `
        -Headers $headers `
        -Body $orderData
    
    Write-Host "✗ FAIL: Empty items array was accepted!" -ForegroundColor Red
} catch {
    $errorResponse = $_.ErrorDetails.Message | ConvertFrom-Json
    if ($errorResponse.message -like "*must have at least one item*") {
        Write-Host "✓ PASS: Empty items array correctly rejected" -ForegroundColor Green
        Write-Host "  Error: $($errorResponse.message)`n" -ForegroundColor White
    } else {
        Write-Host "✗ FAIL: Unexpected error: $($errorResponse.message)" -ForegroundColor Red
    }
}

# ============================================
# TEST 9: Valid Outgoing Order (Sufficient Stock)
# ============================================
Write-Host "=== TEST 9: Valid Outgoing Order (Sufficient Stock) ===" -ForegroundColor Cyan
try {
    $safeQuantity = [Math]::Min($validProductStock - 1, 5)  # Take less than available
    $orderData = @{
        type = 1  # Outgoing
        notes = "Valid outgoing order"
        supplierId = 1
        items = @(@{ productId = $validProductId; quantity = $safeQuantity; unitPrice = 99.99 })
    } | ConvertTo-Json -Depth 5

    Write-Host "  Requesting $safeQuantity units (Available: $validProductStock)" -ForegroundColor Yellow

    $response = Invoke-RestMethod -Uri "$baseUrl/api/orders" `
        -Method POST `
        -Headers $headers `
        -Body $orderData
    
    Write-Host "✓ PASS: Valid outgoing order accepted" -ForegroundColor Green
    Write-Host "  Order Number: $($response.data.orderNumber)" -ForegroundColor White
    Write-Host "  Quantity: $($response.data.items[0].quantity)`n" -ForegroundColor White
} catch {
    $errorResponse = $_.ErrorDetails.Message | ConvertFrom-Json
    Write-Host "✗ FAIL: Valid order was rejected" -ForegroundColor Red
    Write-Host "  Error: $($errorResponse.message)" -ForegroundColor Red
    Write-Host "  Details: $($errorResponse.errors -join ', ')`n" -ForegroundColor Red
}

# ============================================
# TEST 10: Multiple Products with Mixed Stock Issues
# ============================================
Write-Host "=== TEST 10: Multiple Products - One Has Insufficient Stock ===" -ForegroundColor Cyan
try {
    # Get two products
    $product1 = $products.data[0]
    $product2 = $products.data[1]
    
    $orderData = @{
        type = 1  # Outgoing
        notes = "Multiple products, one with insufficient stock"
        supplierId = 1
        items = @(
            @{ productId = $product1.id; quantity = 2; unitPrice = 99.99 },  # Valid
            @{ productId = $product2.id; quantity = $product2.stockQuantity + 50; unitPrice = 199.99 }  # Too much
        )
    } | ConvertTo-Json -Depth 5

    Write-Host "  Product 1: $($product1.name) - Requesting 2 (Available: $($product1.stockQuantity))" -ForegroundColor Yellow
    Write-Host "  Product 2: $($product2.name) - Requesting $($product2.stockQuantity + 50) (Available: $($product2.stockQuantity))" -ForegroundColor Yellow

    $response = Invoke-RestMethod -Uri "$baseUrl/api/orders" `
        -Method POST `
        -Headers $headers `
        -Body $orderData
    
    Write-Host "✗ FAIL: Order with insufficient stock was accepted!" -ForegroundColor Red
} catch {
    $errorResponse = $_.ErrorDetails.Message | ConvertFrom-Json
    if ($errorResponse.message -like "*Stock validation failed*") {
        Write-Host "✓ PASS: Order correctly rejected" -ForegroundColor Green
        Write-Host "  Error: $($errorResponse.message)" -ForegroundColor White
        Write-Host "  Stock Issues:" -ForegroundColor White
        foreach ($error in $errorResponse.errors) {
            Write-Host "    - $error" -ForegroundColor White
        }
        Write-Host ""
    } else {
        Write-Host "✗ FAIL: Unexpected error: $($errorResponse.message)" -ForegroundColor Red
    }
}

Write-Host "`n========================================" -ForegroundColor Magenta
Write-Host "  VALIDATION TESTING COMPLETE!" -ForegroundColor Magenta
Write-Host "========================================`n" -ForegroundColor Magenta

Write-Host "All validation rules tested:" -ForegroundColor Cyan
Write-Host "  ✓ Supplier ID validation" -ForegroundColor White
Write-Host "  ✓ Product ID validation" -ForegroundColor White
Write-Host "  ✓ Stock validation for outgoing orders" -ForegroundColor White
Write-Host "  ✓ No stock check for incoming orders" -ForegroundColor White
Write-Host "  ✓ Quantity validation (positive numbers only)" -ForegroundColor White
Write-Host "  ✓ Price validation (non-negative)" -ForegroundColor White
Write-Host "  ✓ Items array validation (at least one item)" -ForegroundColor White
Write-Host "  ✓ Multiple product validation" -ForegroundColor White
Write-Host ""
