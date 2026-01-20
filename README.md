# Stat-Stock - Inventory Management Platform

A unified web platform for inventory management serving two distinct user groups: Managers and Floor Staff/B2B Clients.

## 🎯 Project Overview

**Stat-Stock** is a modern inventory management platform built with .NET 10 using Clean Architecture principles. It provides:
- **Manager Dashboard**: High-level statistics, order approvals, inventory health monitoring
- **Ordering Terminal**: Simplified interface for floor staff to log shipments and place orders
- **B2B API**: REST API for business clients to integrate with their systems

## 🏗️ Architecture

This project follows **Clean Architecture** with clear separation of concerns:

```
Stat-Stock/
├── src/
│   ├── StatStock.Domain/          # Core business entities and domain logic
│   ├── StatStock.Application/     # Use cases and application services
│   ├── StatStock.Infrastructure/  # Data access, Identity, external services
│   └── StatStock.Web/             # ASP.NET Core MVC web application
└── tests/                         # Unit and integration tests
```

## 🚀 Getting Started

### Prerequisites
- .NET 10 SDK
- SQL Server LocalDB (comes with Visual Studio)
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
   - Seed sample data
   - Start on https://localhost:5001

4. **Access the application**
   - Web UI: https://localhost:5001
   - API Documentation: https://localhost:5001/swagger

### Test Credentials

| Role | Email | Password |
|------|-------|----------|
| Admin | admin@statstock.com | Admin@123 |
| Manager | manager@statstock.com | Manager@123 |
| Floor Staff | staff@statstock.com | Staff@123 |
| B2B Client | client@statstock.com | Client@123 |

## 📦 Tech Stack

| Component | Technology |
|-----------|------------|
| **Framework** | .NET 10 |
| **Architecture** | Clean Architecture (DDD) |
| **Frontend** | ASP.NET Core MVC + Razor Views |
| **CSS** | Bootstrap 5 |
| **Database** | SQL Server LocalDB |
| **ORM** | EF Core 10.0 + Dapper |
| **Authentication** | ASP.NET Core Identity |
| **API Documentation** | Swagger/OpenAPI |
| **Logging** | Serilog |

## 📊 Features

### ✅ Phase 1 (Completed)
- Clean Architecture solution structure
- Domain entities (Product, Supplier, Order, OrderItem, User)
- EF Core with SQL Server configuration
- ASP.NET Core Identity with role-based authorization
- Database migrations
- Sample data seeding (12 products, 4 suppliers, 25 orders, 4 users)
- Serilog logging
- Swagger API documentation

### 🚧 Phase 2 (Upcoming)
- Manager Dashboard with statistics
- Real-time charts with Chart.js
- Product and order listing pages
- SignalR for real-time updates

### 🚧 Phase 3 (Planned)
- Ordering Terminal interface
- Quick product search
- Barcode/QR scanning
- Keyboard shortcuts

### 🚧 Phase 4 (Planned)
- REST API endpoints for B2B clients
- JWT authentication for API
- Webhook notifications

## 🗄️ Database Schema

### Core Entities
- **Product**: SKU, Name, Description, Price, Category, ReorderLevel, StockQuantity
- **Supplier**: Name, Contact, Email, Phone, Address
- **Order**: OrderNumber, Type (Incoming/Outgoing), Status, Items
- **OrderItem**: ProductId, Quantity, UnitPrice
- **ApplicationIdentityUser**: Email, FirstName, LastName, Role, Area

### Relationships
- Suppliers → Orders (One-to-Many)
- Orders → OrderItems (One-to-Many)
- Products → OrderItems (One-to-Many)

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

### Running Tests (when available)
```bash
dotnet test
```

## 📄 License

This project is licensed under the MIT License.

## 👥 Contributors

- Initial development by AI assistance for learning purposes

## 📚 Documentation

For detailed implementation notes, see:
- [Phase 1 Summary](PHASE1_SUMMARY.md) - Complete Phase 1 implementation details
- [Plan](plan.md) - Full project plan and roadmap

---

**Status**: Phase 1 Complete ✅ | **Version**: 1.0.0 | **Date**: January 2026