# Stat-Stock - Inventory Management Platform

A unified web platform for inventory management serving two distinct user groups: Managers and Floor Staff/B2B Clients.

## 🎯 Project Overview

**Stat-Stock** is a modern inventory management platform built with .NET 10 using Clean Architecture principles. It provides:
- **Manager Dashboard**: High-level statistics, order approvals, inventory health monitoring, and comprehensive reports
- **Ordering Terminal**: Simplified interface for floor staff to rapidly log shipments and place orders
- **B2B API**: Complete REST API with JWT authentication for business clients to integrate with their systems

## 🏗️ Architecture

This project follows **Clean Architecture** with clear separation of concerns:

```
Stat-Stock/
├── src/
│   ├── StatStock.Domain/          # Core business entities and domain logic
│   ├── StatStock.Application/     # Use cases, DTOs, and application services
│   ├── StatStock.Infrastructure/  # Data access, services, and external integrations
│   └── StatStock.Web/             # ASP.NET Core MVC + API endpoints
└── tests/                         # Unit and integration tests
```

## 🚀 Getting Started

### Prerequisites
- .NET 10 SDK
- SQL Server LocalDB (comes with Visual Studio) or SQL Server
- Visual Studio 2022 or VS Code (optional)

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/YaraSenousy/Stat-Stock.git
   cd Stat-Stock
   ```

2. **Build the solution**
   ```bash
   dotnet build
   ```

3. **Run the application**
   ```bash
   cd src/StatStock.Web
   dotnet run
   ```
   
   The application will automatically:
   - Apply database migrations
   - Seed sample data (12 products, 4 suppliers, 25 orders, 4 users)
   - Start on http://localhost:5142

4. **Access the application**
   - Manager Dashboard: http://localhost:5142/Manager
   - Ordering Terminal: http://localhost:5142/Terminal
   - API Documentation: http://localhost:5142/swagger

### Test Credentials

| Role | Username | Password |
|------|----------|----------|
| Admin | admin | Admin123! |
| Manager | manager | Manager123! |
| Floor Staff | staff | Staff123! |
| B2B Client | b2bclient | B2B123! |

## 📦 Tech Stack

| Component | Technology |
|-----------|------------|
| **Framework** | .NET 10 |
| **Architecture** | Clean Architecture (DDD) |
| **Frontend** | ASP.NET Core MVC + Razor Views |
| **CSS Framework** | Tailwind CSS |
| **Database** | SQL Server |
| **ORM** | EF Core 10.0 + Dapper (Hybrid) |
| **Authentication** | Cookie-based (MVC) + JWT (API) |
| **Authorization** | Role-based with hierarchical access |
| **API** | REST with OpenAPI/Swagger |
| **Real-time** | SignalR (planned) |
| **Charting** | Chart.js |
| **Caching** | IMemoryCache |
| **Logging** | Serilog (file + console) |
| **Excel Export** | ClosedXML |
| **PDF Export** | QuestPDF |
| **Testing** | xUnit + FluentAssertions + Moq |

## 📊 Features

### ✅ Phase 1: Foundation (COMPLETED)
- Clean Architecture solution structure
- Domain entities (Product, Supplier, Order, OrderItem, User)
- EF Core with SQL Server configuration
- Database migrations with automatic application
- Sample data seeding (12 products, 4 suppliers, 25 orders, 4 users)
- Serilog structured logging
- Error handling and middleware

### ✅ Phase 2: Manager Dashboard (COMPLETED)
- Modern responsive dashboard with Tailwind CSS
- Product listing with search, filters, and pagination
- Order listing with status filters and search
- Dashboard statistics with Chart.js
  - Inventory distribution by category
  - Order trends (incoming vs outgoing)
  - Stock health indicators
- Navigation sidebar with active state highlighting
- Gradient backgrounds and modern UI design

### ✅ Phase 3: Ordering Terminal (COMPLETED)
- Simplified, terminal-style interface for floor staff
- Quick product search with autocomplete
- Incoming shipment logging
- Outgoing shipment logging
- Keyboard shortcuts for rapid data entry
  - Enter: Submit form
  - Ctrl+F: Focus search
  - Esc: Clear form
- Real-time stock updates
- Minimal UI optimized for speed

### ✅ Phase 4: Order Management & Approvals (COMPLETED)
- Bulk approve/reject orders (select multiple)
- Single order approve/reject actions
- Order details page with full information
- Automated approval rules engine
  - Auto-approve orders below threshold
  - Auto-approve from trusted suppliers
  - Auto-approve specific product categories
- Order status change notifications
- Date range filters
- Order search by order number
- Status tracking (Pending, Approved, Shipped, Delivered, Cancelled)

### ✅ Phase 5: Product Management (COMPLETED)
- Complete product CRUD operations
  - Create products with validation
  - Edit products with stock updates
  - Delete products with confirmation
- Supplier management (CRUD)
- Category management
- Bulk import/export
  - Import products from CSV/Excel
  - Export products to CSV/Excel
  - Validation and error reporting
- Product search and filtering
- Stock quantity tracking
- Reorder level management

### ✅ Phase 6: B2B API (COMPLETED)
- **REST API Controllers**
  - Products API (GET, POST, PUT, DELETE)
  - Orders API (GET, POST, PATCH)
  - Auth API (token generation and validation)
- **JWT Authentication**
  - API key validation
  - Token generation with configurable expiry
  - Role-based authorization
- **Swagger/OpenAPI Documentation**
  - Interactive API testing
  - Request/response examples
  - Authentication flow documentation
- **Webhook Notifications**
  - Order created webhooks
  - Order status changed webhooks
  - Configurable webhook URLs
- **Rate Limiting**
  - Per-user/IP rate limiting (100 req/min default)
  - Rate limit headers in responses
  - HTTP 429 handling
- **Comprehensive DTOs**
  - ApiResponse wrapper
  - Product/Order DTOs
  - Validation attributes

### ✅ Phase 7: Reports & Analytics (COMPLETED)
- **Core Reports** (4)
  1. Stock Movement Report - Track product movements over time
  2. Inventory Valuation Report - Total inventory value with status
  3. Low Stock Items Report - Products needing reorder
  4. Sales Trends Report - Order history and trends analysis
- **Analytics Features** (2)
  1. Demand Forecasting - AI-powered predictions based on 90-day history
  2. Reorder Suggestions - Automated recommendations with priority
- **Export Capabilities**
  - Excel export (ClosedXML) with formatting
  - PDF export (QuestPDF) with professional layouts
  - 8 total export endpoints (4 Excel + 4 PDF)
- **UI/UX Features**
  - Modern card-based dashboard
  - Date range filters
  - Color-coded status indicators
  - Loading states and animations
  - Empty states with helpful messages
  - Responsive design

### 🚧 Phase 8: Advanced Features (PLANNED)
- User authentication system (custom, no Identity)
- User management (Create, Edit, Delete users)
- Role-based access control
- Audit trail with user tracking
- Email notifications for approvals
- In-app notification system
- Barcode/QR code scanning for Terminal
- Batch entry (multiple products in one shipment)
- Expiration/shelf-life tracking

### 🚧 Phase 9: Polish & Testing (PLANNED)
- Unit tests for core features
- Integration tests for API
- UI/UX improvements
- Performance optimization
- Error handling improvements
- Logging enhancements

## 🗄️ Database Schema

### Core Entities
- **Product**: SKU, Name, Description, Price, Category, ReorderLevel, StockQuantity
- **Supplier**: Name, Contact, Email, Phone, Address
- **Order**: OrderNumber, Type (Incoming/Outgoing), Status, Items, Total
- **OrderItem**: ProductId, Quantity, UnitPrice
- **User**: Email, FirstName, LastName, Role, Area, PasswordHash

### Relationships
- Suppliers → Orders (One-to-Many)
- Orders → OrderItems (One-to-Many)
- Products → OrderItems (One-to-Many)
- Users → Orders (One-to-Many)

## 🔌 API Usage

### Authentication
```bash
# Get JWT token
curl -X POST http://localhost:5142/api/auth/token \
  -H "Content-Type: application/json" \
  -d '{"apiKey": "demo-api-key-12345", "userId": "b2bclient"}'
```

### Get Products
```bash
curl -X GET http://localhost:5142/api/products \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

### Create Order
```bash
curl -X POST http://localhost:5142/api/orders \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "supplierId": 1,
    "type": "Incoming",
    "items": [
      {"productId": 1, "quantity": 10, "unitPrice": 999.99}
    ]
  }'
```

See full API documentation at: http://localhost:5142/swagger

## 📝 Development

### Running Migrations

Create a new migration:
```bash
dotnet ef migrations add MigrationName --project src/StatStock.Infrastructure --startup-project src/StatStock.Web
```

Update the database:
```bash
dotnet ef database update --project src/StatStock.Infrastructure --startup-project src/StatStock.Web
```

### Building the Project
```bash
dotnet build
```

### Running Tests
```bash
dotnet test
```

### Stopping the Server
```powershell
# Find the process
Get-Process | Where-Object {$_.Name -eq "StatStock.Web"}

# Stop it (replace 12345 with actual PID)
Stop-Process -Id 12345 -Force
```

## 📄 License

This project is licensed under the MIT License.

## 👥 Contributors

- Developed with AI assistance (GitHub Copilot)
- For learning and demonstration purposes

## 📚 Documentation

For detailed implementation notes, see:
- [Phase 1 Summary](PHASE1_SUMMARY.md) - Foundation and architecture
- [Phase 2 Summary](PHASE2_SUMMARY.md) - Manager dashboard
- [Phase 3 Summary](PHASE3_SUMMARY.md) - Ordering terminal
- [Phase 4 Summary](PHASE4_SUMMARY.md) - Order management & approvals
- [Phase 5 Summary](PHASE5_SUMMARY.md) - Product management & bulk operations
- [Phase 6 Summary](PHASE6_SUMMARY.md) - B2B API implementation
- [Phase 7 Summary](PHASE7_SUMMARY.md) - Reports & analytics
- [Project Plan](plan.md) - Full project roadmap

## 🎯 Project Status

**Current Phase**: 7 of 9 Complete ✅

**Completion**: 
- Phase 1: Foundation ✅
- Phase 2: Manager Dashboard ✅
- Phase 3: Ordering Terminal ✅
- Phase 4: Order Management ✅
- Phase 5: Product Management ✅
- Phase 6: B2B API ✅
- Phase 7: Reports & Analytics ✅
- Phase 8: Advanced Features 🚧
- Phase 9: Polish & Testing 🚧

**Version**: 1.7.0 | **Last Updated**: January 2026

---

**Built with ❤️ using .NET 10 and Clean Architecture**