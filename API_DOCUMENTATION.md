# StatStock B2B API Documentation

## Overview
The StatStock B2B API provides RESTful endpoints for integrating with the StatStock Inventory Management Platform. The API is designed for B2B clients to manage orders and view product inventory programmatically.

## Base URL
```
http://localhost:5142/api
```

## Authentication
All API endpoints (except `/api/auth/token`) require JWT Bearer token authentication.

### Getting a Token
**Endpoint:** `POST /api/auth/token`

**Request Body:**
```json
{
  "email": "client@company.com",
  "apiKey": "demo-api-key-12345"
}
```

**Response:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 86400,
  "tokenType": "Bearer"
}
```

**Usage:**
Include the token in the Authorization header for all subsequent requests:
```
Authorization: Bearer {your_token_here}
```

### Token Validation
**Endpoint:** `GET /api/auth/validate`

Validates the current JWT token and returns user information.

**Response:**
```json
{
  "valid": true,
  "userId": "api-client-ec87f8ee-8de2-47ee-9f85-c1b1fd9eb488",
  "email": "client@company.com",
  "role": "B2BClient"
}
```

---

## Products API

### List All Products
**Endpoint:** `GET /api/products`

**Query Parameters:**
- `category` (string, optional): Filter by product category
- `search` (string, optional): Search by SKU or product name
- `minStock` (integer, optional): Filter by minimum stock quantity
- `maxStock` (integer, optional): Filter by maximum stock quantity

**Example Request:**
```bash
curl -X GET "http://localhost:5142/api/products?category=Electronics&minStock=10" \
  -H "Authorization: Bearer {token}"
```

**Response:**
```json
{
  "success": true,
  "message": "Operation successful",
  "data": [
    {
      "id": 1,
      "sku": "LAPTOP-001",
      "name": "Dell Latitude 5520",
      "description": "15.6\" Business Laptop",
      "price": 1299.99,
      "category": "Electronics",
      "reorderLevel": 5,
      "stockQuantity": 25,
      "createdAt": "2026-01-20T10:00:00Z",
      "updatedAt": "2026-01-25T15:30:00Z"
    }
  ],
  "errors": []
}
```

### Get Product by ID
**Endpoint:** `GET /api/products/{id}`

**Example Request:**
```bash
curl -X GET "http://localhost:5142/api/products/1" \
  -H "Authorization: Bearer {token}"
```

**Response:**
```json
{
  "success": true,
  "message": "Operation successful",
  "data": {
    "id": 1,
    "sku": "LAPTOP-001",
    "name": "Dell Latitude 5520",
    "description": "15.6\" Business Laptop",
    "price": 1299.99,
    "category": "Electronics",
    "reorderLevel": 5,
    "stockQuantity": 25,
    "createdAt": "2026-01-20T10:00:00Z",
    "updatedAt": "2026-01-25T15:30:00Z"
  },
  "errors": []
}
```

### Create Product
**Endpoint:** `POST /api/products`

**Request Body:**
```json
{
  "sku": "MOUSE-001",
  "name": "Logitech MX Master 3",
  "description": "Wireless Mouse",
  "price": 99.99,
  "category": "Electronics",
  "reorderLevel": 10,
  "stockQuantity": 50
}
```

**Response:**
```json
{
  "success": true,
  "message": "Product created successfully",
  "data": {
    "id": 15,
    "sku": "MOUSE-001",
    "name": "Logitech MX Master 3",
    "description": "Wireless Mouse",
    "price": 99.99,
    "category": "Electronics",
    "reorderLevel": 10,
    "stockQuantity": 50,
    "createdAt": "2026-01-26T01:30:00Z",
    "updatedAt": "2026-01-26T01:30:00Z"
  },
  "errors": []
}
```

### Update Product
**Endpoint:** `PUT /api/products/{id}`

**Request Body** (all fields optional):
```json
{
  "name": "Logitech MX Master 3S",
  "price": 109.99,
  "stockQuantity": 45
}
```

**Response:**
```json
{
  "success": true,
  "message": "Product updated successfully",
  "data": {
    "id": 15,
    "sku": "MOUSE-001",
    "name": "Logitech MX Master 3S",
    "description": "Wireless Mouse",
    "price": 109.99,
    "category": "Electronics",
    "reorderLevel": 10,
    "stockQuantity": 45,
    "createdAt": "2026-01-26T01:30:00Z",
    "updatedAt": "2026-01-26T01:35:00Z"
  },
  "errors": []
}
```

### Delete Product
**Endpoint:** `DELETE /api/products/{id}`

**Response:**
```json
{
  "success": true,
  "message": "Product deleted successfully",
  "data": {
    "id": 15
  },
  "errors": []
}
```

### Get Product Categories
**Endpoint:** `GET /api/products/categories`

**Response:**
```json
{
  "success": true,
  "message": "Operation successful",
  "data": [
    "Electronics",
    "Office Supplies",
    "Furniture"
  ],
  "errors": []
}
```

### Get Low Stock Products
**Endpoint:** `GET /api/products/low-stock`

Returns products where stock quantity is at or below the reorder level.

**Response:**
```json
{
  "success": true,
  "message": "Operation successful",
  "data": [
    {
      "id": 3,
      "sku": "KEYBOARD-001",
      "name": "Mechanical Keyboard",
      "description": "RGB Backlit Keyboard",
      "price": 79.99,
      "category": "Electronics",
      "reorderLevel": 15,
      "stockQuantity": 8,
      "createdAt": "2026-01-20T10:00:00Z",
      "updatedAt": "2026-01-25T15:30:00Z"
    }
  ],
  "errors": []
}
```

---

## Orders API

### Order Type Enum Values
**IMPORTANT:** When creating or filtering orders, use integer enum values:
- `0` = **Incoming** (Receiving inventory from suppliers)
- `1` = **Outgoing** (Shipping inventory out)

### Order Status Enum Values
- `0` = **Pending** (Awaiting approval)
- `1` = **Approved** (Approved, ready for processing)
- `2` = **Shipped** (In transit)
- `3` = **Delivered** (Completed)
- `4` = **Cancelled** (Cancelled)

### List All Orders
**Endpoint:** `GET /api/orders`

**Query Parameters:**
- `status` (integer, optional): Filter by order status (0=Pending, 1=Approved, 2=Shipped, 3=Delivered, 4=Cancelled)
- `type` (integer, optional): Filter by order type (0=Incoming, 1=Outgoing)
- `fromDate` (datetime, optional): Filter orders created from this date (format: YYYY-MM-DD)
- `toDate` (datetime, optional): Filter orders created until this date (format: YYYY-MM-DD)

**Example Request:**
```bash
curl -X GET "http://localhost:5142/api/orders?status=0&type=0" \
  -H "Authorization: Bearer {token}"
```

**Response:**
```json
{
  "success": true,
  "message": "Operation successful",
  "data": [
    {
      "id": 1,
      "orderNumber": "ORD-20260126013000",
      "type": 0,
      "status": 0,
      "createdAt": "2026-01-26T01:30:00Z",
      "approvedAt": null,
      "notes": "Urgent order for Q1",
      "supplierId": 1,
      "supplierName": "TechWholesale Inc.",
      "userId": "api-client-abc123",
      "items": [
        {
          "id": 1,
          "productId": 1,
          "productName": "Dell Latitude 5520",
          "productSKU": "LAPTOP-001",
          "quantity": 10,
          "unitPrice": 1299.99,
          "subtotal": 12999.90
        }
      ],
      "totalAmount": 12999.90
    }
  ],
  "errors": []
}
```

### Get Order by ID
**Endpoint:** `GET /api/orders/{id}`

**Example Request:**
```bash
curl -X GET "http://localhost:5142/api/orders/1" \
  -H "Authorization: Bearer {token}"
```

**Response:** Same structure as individual order in list response.

### Create Order
**Endpoint:** `POST /api/orders`

**Important Validation Rules:**
1. **Order Type:** Must be `0` (Incoming) or `1` (Outgoing)
2. **Supplier Validation:** If `supplierId` is provided, the supplier must exist
3. **Product Validation:** All product IDs must exist in the database
4. **Stock Validation:** For **Outgoing** orders (type = 1), the system validates that sufficient stock is available for each product
5. **Quantity Validation:** All quantities must be greater than zero
6. **Price Validation:** Unit prices cannot be negative
7. **Items Required:** Order must have at least one item

**Request Body:**
```json
{
  "type": 0,
  "notes": "Urgent restocking order",
  "supplierId": 1,
  "items": [
    {
      "productId": 1,
      "quantity": 10,
      "unitPrice": 1299.99
    },
    {
      "productId": 2,
      "quantity": 5,
      "unitPrice": 899.99
    }
  ]
}
```

**Note:** 
- Use `type: 0` for **Incoming** orders (receiving inventory)
- Use `type: 1` for **Outgoing** orders (shipping out - requires sufficient stock)

**Success Response (201 Created):**
```json
{
  "success": true,
  "message": "Order created successfully",
  "data": {
    "id": 5,
    "orderNumber": "ORD-20260126013530",
    "type": 0,
    "status": 0,
    "createdAt": "2026-01-26T01:35:30Z",
    "approvedAt": null,
    "notes": "Urgent restocking order",
    "supplierId": 1,
    "supplierName": "TechWholesale Inc.",
    "userId": "api-client-abc123",
    "items": [
      {
        "id": 10,
        "productId": 1,
        "productName": "Dell Latitude 5520",
        "productSKU": "LAPTOP-001",
        "quantity": 10,
        "unitPrice": 1299.99,
        "subtotal": 12999.90
      },
      {
        "id": 11,
        "productId": 2,
        "productName": "HP Monitor 27\"",
        "productSKU": "MONITOR-001",
        "quantity": 5,
        "unitPrice": 899.99,
        "subtotal": 4499.95
      }
    ],
    "totalAmount": 17499.85
  },
  "errors": []
}
```

**Validation Error Response (400 Bad Request):**
```json
{
  "success": false,
  "message": "Stock validation failed",
  "data": null,
  "errors": [
    "Insufficient stock for product 'Business Laptop' (SKU: LAPTOP-001). Available: 5, Requested: 10"
  ]
}
```

**Common Validation Errors:**
- `"Supplier with ID {id} not found"` - Invalid supplier ID
- `"Products not found: 1, 2, 3"` - One or more product IDs don't exist
- `"Insufficient stock for product..."` - Not enough stock for outgoing order
- `"All item quantities must be greater than zero"` - Negative or zero quantities
- `"Unit prices cannot be negative"` - Invalid pricing
- `"Order must have at least one item"` - Empty items array

### Update Order Status
**Endpoint:** `PATCH /api/orders/{id}/status`

**Request Body:**
```json
{
  "status": 2
}
```

**Valid Status Values:**
- `0` = Pending
- `1` = Approved
- `2` = Shipped
- `3` = Delivered
- `4` = Cancelled

**Response:**
```json
{
  "success": true,
  "message": "Order status updated successfully",
  "data": {
    // Full order object with updated status
  },
  "errors": []
}
```

### Cancel Order
**Endpoint:** `POST /api/orders/{id}/cancel`

**Response:**
```json
{
  "success": true,
  "message": "Order cancelled successfully",
  "data": {
    // Full order object with Cancelled status
  },
  "errors": []
}
```

### Get My Orders
**Endpoint:** `GET /api/orders/my-orders`

Returns all orders created by the authenticated user.

**Response:**
```json
{
  "success": true,
  "message": "Operation successful",
  "data": [
    // Array of order objects
  ],
  "errors": []
}
```

---

## Rate Limiting
The API implements rate limiting to ensure fair usage:
- **Limit:** 100 requests per minute per client
- **Headers:** Rate limit information is included in response headers:
  - `X-RateLimit-Limit`: Maximum requests allowed
  - `X-RateLimit-Remaining`: Remaining requests in current window
  - `X-RateLimit-Reset`: Unix timestamp when the limit resets

**Example:**
```
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 95
X-RateLimit-Reset: 1706234400
```

**Rate Limit Exceeded Response:**
```json
{
  "error": "Rate limit exceeded",
  "message": "Maximum 100 requests per 1 minute(s) allowed",
  "retryAfter": 60
}
```

---

## Webhooks
The API supports webhook notifications for order-related events. Configure webhook URLs in `appsettings.json`:

```json
{
  "Webhooks": {
    "OrderCreatedUrl": "https://your-domain.com/webhooks/order-created",
    "OrderStatusChangedUrl": "https://your-domain.com/webhooks/order-status-changed"
  }
}
```

### Webhook Events

#### Order Created
Triggered when a new order is created via the API.

**Payload:**
```json
{
  "eventType": "order.created",
  "timestamp": "2026-01-26T01:35:30Z",
  "data": {
    // Full order object
  }
}
```

#### Order Status Changed
Triggered when an order status is updated.

**Payload:**
```json
{
  "eventType": "order.status_changed",
  "timestamp": "2026-01-26T01:40:00Z",
  "data": {
    "order": {
      // Full order object
    },
    "oldStatus": "Pending",
    "newStatus": "Approved"
  }
}
```

---

## Error Handling

All API responses follow a consistent format:

**Success Response:**
```json
{
  "success": true,
  "message": "Operation successful",
  "data": { /* response data */ },
  "errors": []
}
```

**Error Response:**
```json
{
  "success": false,
  "message": "Error message",
  "data": null,
  "errors": [
    "Detailed error 1",
    "Detailed error 2"
  ]
}
```

### HTTP Status Codes
- `200 OK`: Successful GET, PUT, PATCH, DELETE
- `201 Created`: Successful POST (resource created)
- `400 Bad Request`: Invalid request data
- `401 Unauthorized`: Missing or invalid authentication
- `404 Not Found`: Resource not found
- `429 Too Many Requests`: Rate limit exceeded
- `500 Internal Server Error`: Server error

---

## Configuration

### API Key Management
To use the API, you need a valid API key. Configure in `appsettings.json`:

```json
{
  "ApiKey": "your-secure-api-key-here"
}
```

**Note:** In production, use environment variables or secure key management systems instead of storing keys in configuration files.

### JWT Configuration
Configure JWT settings in `appsettings.json`:

```json
{
  "Jwt": {
    "Key": "your-secret-signing-key-must-be-long-enough",
    "Issuer": "StatStockAPI",
    "Audience": "StatStockClients",
    "ExpiryHours": "24"
  }
}
```

### Rate Limiting Configuration
Customize rate limiting in `appsettings.json`:

```json
{
  "RateLimiting": {
    "RequestLimit": "100",
    "TimeWindowMinutes": "1"
  }
}
```

---

## Swagger UI
Interactive API documentation is available at:
```
http://localhost:5142/swagger
```

The Swagger UI allows you to:
- Browse all available endpoints
- View request/response schemas
- Test API calls directly from the browser
- Generate client code

---

## Client Examples

### cURL
```bash
# Get token
TOKEN=$(curl -s -X POST http://localhost:5142/api/auth/token \
  -H "Content-Type: application/json" \
  -d '{"email":"client@company.com","apiKey":"demo-api-key-12345"}' \
  | jq -r '.token')

# List products
curl -X GET "http://localhost:5142/api/products" \
  -H "Authorization: Bearer $TOKEN"

# Create order (Incoming = 0, Outgoing = 1)
curl -X POST "http://localhost:5142/api/orders" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "type": 0,
    "notes": "Monthly restocking",
    "supplierId": 1,
    "items": [
      {"productId": 1, "quantity": 10, "unitPrice": 1299.99}
    ]
  }'
```

### Python
```python
import requests

# Get token
response = requests.post(
    'http://localhost:5142/api/auth/token',
    json={'email': 'client@company.com', 'apiKey': 'demo-api-key-12345'}
)
token = response.json()['token']

# Set up headers
headers = {'Authorization': f'Bearer {token}'}

# List products
products = requests.get('http://localhost:5142/api/products', headers=headers)
print(products.json())

# Create order
order_data = {
    'type': 0,  # 0 = Incoming, 1 = Outgoing
    'notes': 'Monthly restocking',
    'supplierId': 1,
    'items': [
        {'productId': 1, 'quantity': 10, 'unitPrice': 1299.99}
    ]
}
order = requests.post('http://localhost:5142/api/orders', json=order_data, headers=headers)
print(order.json())
```

### JavaScript (Node.js)
```javascript
const axios = require('axios');

const BASE_URL = 'http://localhost:5142/api';

async function main() {
  // Get token
  const tokenResponse = await axios.post(`${BASE_URL}/auth/token`, {
    email: 'client@company.com',
    apiKey: 'demo-api-key-12345'
  });
  const token = tokenResponse.data.token;

  // Set up headers
  const headers = { Authorization: `Bearer ${token}` };

  // List products
  const products = await axios.get(`${BASE_URL}/products`, { headers });
  console.log(products.data);

  // Create order
  const orderData = {
    type: 0,  // 0 = Incoming, 1 = Outgoing
    notes: 'Monthly restocking',
    supplierId: 1,
    items: [
      { productId: 1, quantity: 10, unitPrice: 1299.99 }
    ]
  };
  const order = await axios.post(`${BASE_URL}/orders`, orderData, { headers });
  console.log(order.data);
}

main();
```

---

## Security Best Practices

1. **Always use HTTPS in production** - The examples use HTTP for local development only
2. **Keep API keys secure** - Never commit API keys to version control
3. **Rotate tokens regularly** - Tokens expire after 24 hours by default
4. **Validate webhook signatures** - Implement signature verification for webhook payloads
5. **Monitor rate limits** - Track API usage to avoid hitting rate limits
6. **Use environment variables** - Store sensitive configuration in environment variables

---

## Support
For API support, please contact:
- Email: api-support@statstock.com
- Documentation: https://docs.statstock.com
- GitHub Issues: https://github.com/YaraSenousy/Stat-Stock/issues
