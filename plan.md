# Stat-Stock - Inventory Management Platform

## Project Overview

A unified web platform serving two distinct user groups for inventory management:

- **Managers:** View high-level statistical data, approve bulk orders, and manage inventory health
- **Floor Staff / B2B Clients:** Use a simplified "Ordering Terminal" interface to rapidly log shipments or place new orders via API

---

## Technology Stack (Confirmed)

| Component | Technology |
|-----------|------------|
| **Framework** | .NET 10 |
| **Architecture** | Clean Architecture (DDD layers) |
| **Frontend** | ASP.NET Core MVC with Razor Views |
| **CSS Framework** | Tailwind CSS |
| **Database** | Microsoft SQL Server |
| **ORM** | EF Core + Dapper (Hybrid) |
| **Authentication** | Cookie + JWT |
| **Authorization** | Hierarchical Roles |
| **API Style** | REST with OpenAPI/Swagger |
| **Real-time** | SignalR (dashboard) + Webhooks (B2B) |
| **Charting** | Chart.js |
| **Caching** | IMemoryCache (Redis-ready) |
| **Logging** | Serilog (file + console) |
| **Testing** | xUnit + FluentAssertions + Moq |
| **Notifications** | Email + In-app |
| **Language** | English only |
| **Deployment** | Local (development) |

---

## Core Domain Entities

```
+--------------+     +--------------+     +--------------+
|   Product    |---->|    Order     |<----|  Supplier    |
|              |     |              |     |              |
| - Id         |     | - Id         |     | - Id         |
| - SKU        |     | - OrderNo    |     | - Name       |
| - Name       |     | - Type       |     | - Contact    |
| - Description|     | - Status     |     | - Email      |
| - Price      |     | - CreatedAt  |     | - Phone      |
| - Category   |     | - ApprovedAt |     | - Address    |
| - ReorderLvl |     | - Items[]    |     +--------------+
| - StockQty   |     | - SupplierId |
+--------------+     | - UserId     |     +--------------+
                     +--------------+     |    User      |
                                          |              |
                                          | - Id         |
                                          | - Email      |
                                          | - Role       |
                                          | - Area       |
                                          +--------------+
```

---

## Features Breakdown

### Manager Dashboard
- [ ] Dashboard with charts (inventory levels, order trends, stock health)
- [ ] Real-time alerts (low stock, pending approvals, anomalies)
- [ ] Reports & exports (PDF/Excel)
- [ ] Predictive analytics (demand forecasting, reorder suggestions)
- [ ] Bulk approve/reject orders with filters
- [ ] Automated approval rules
- [ ] View stock levels with status indicators
- [ ] Manual reorder point configuration
- [ ] Automatic reorder suggestions
- [ ] Expiration/shelf-life tracking
- [ ] Stock movement history and audit trail

### Ordering Terminal (Floor Staff)
- [ ] Barcode/QR code scanning
- [ ] Quick search by SKU/product name
- [ ] Keyboard shortcuts for rapid entry
- [ ] Log incoming shipments
- [ ] Log outgoing shipments

### B2B API
- [ ] Place new orders (request inventory from suppliers)
- [ ] View order status and history
- [ ] Cancel/modify pending orders
- [ ] JWT authentication
- [ ] Swagger documentation

---

## Project Structure (Clean Architecture)

```
Stat-Stock/
|-- src/
|   |-- StatStock.Domain/           # Entities, Value Objects, Domain Events
|   |   |-- Entities/
|   |   |-- ValueObjects/
|   |   |-- Events/
|   |   +-- Interfaces/
|   |
|   |-- StatStock.Application/      # Use Cases, DTOs, Interfaces
|   |   |-- Common/
|   |   |-- Features/
|   |   |   |-- Products/
|   |   |   |-- Orders/
|   |   |   |-- Suppliers/
|   |   |   +-- Statistics/
|   |   +-- Interfaces/
|   |
|   |-- StatStock.Infrastructure/   # EF Core, Dapper, External Services
|   |   |-- Data/
|   |   |   |-- EfCore/
|   |   |   +-- Dapper/
|   |   |-- Services/
|   |   +-- Persistence/
|   |
|   +-- StatStock.Web/              # MVC Controllers, Views, API
|       |-- Controllers/
|       |-- Views/
|       |-- Areas/
|       |   |-- Manager/
|       |   +-- Terminal/
|       |-- Api/
|       |-- Hubs/
|       +-- wwwroot/
|
|-- tests/
|   |-- StatStock.Domain.Tests/
|   |-- StatStock.Application.Tests/
|   +-- StatStock.Web.Tests/
|
+-- StatStock.sln
```

---

## Implementation Approaches

### **Approach 1: Full Scaffold First (Recommended for Your Timeline)**

**Description:** Set up the complete project structure with all layers, then implement features incrementally starting with data display.

**Pros:**
- Proper foundation from day 1
- Easy to add features without restructuring
- Clean separation of concerns
- Best for learning/testing architecture

**Cons:**
- More initial setup time (~4-6 hours)
- May feel like slow progress initially

**Order of Implementation:**
1. Create solution structure with all projects
2. Set up Domain entities
3. Configure EF Core + database
4. Implement basic authentication
5. Create Manager Dashboard (data display)
6. Add statistics/charts
7. Implement Ordering Terminal
8. Add B2B API

---

### **Approach 2: Vertical Slice per Feature**

**Description:** Build one complete feature at a time, from UI to database, before moving to the next.

**Pros:**
- See working features quickly
- Each slice is independently testable
- Good for demo/validation

**Cons:**
- May need refactoring as patterns emerge
- Cross-cutting concerns added later

**Order of Implementation:**
1. Products feature (CRUD + display)
2. Orders feature (create + list)
3. Dashboard (aggregate statistics)
4. Authentication layer
5. API endpoints

---

### **Approach 3: Outside-In (UI First)**

**Description:** Start with UI mockups using fake data, then work backward to implement the backend.

**Pros:**
- See the UI immediately
- Stakeholder feedback early
- Faster visual progress

**Cons:**
- Technical debt if not careful
- May need to rewrite when connecting real data

**Order of Implementation:**
1. Create MVC views with hardcoded data
2. Add Tailwind styling + Chart.js
3. Implement controllers with fake services
4. Replace with real EF Core/Dapper
5. Add authentication last

---

### **Approach 4: API-First Development**

**Description:** Build the REST API first with Swagger, then build the MVC frontend that consumes it.

**Pros:**
- API is testable via Swagger immediately
- B2B clients can integrate early
- Clear contract between frontend/backend

**Cons:**
- Two layers of work (API + MVC)
- Slower to see visual results

**Order of Implementation:**
1. Define API contracts (OpenAPI spec)
2. Implement API controllers
3. Build MVC views consuming the API
4. Add SignalR for real-time

---

## Recommended Implementation Plan (Approach 1 + Quick Wins)

### Phase 1: Foundation (Day 1)
- [ ] Create solution with Clean Architecture structure
- [ ] Set up Domain entities (Product, Supplier, Order, User)
- [ ] Configure EF Core with SQL Server
- [ ] Create database migrations
- [ ] Seed sample data

### Phase 2: Manager Dashboard (Day 1-2) ✅ COMPLETED (except SignalR)
- [x] ~~Create Manager area with layout~~
- [x] ~~Implement Product listing page~~
- [x] ~~Add Order listing with status filters~~
- [x] ~~Create Dashboard with Chart.js statistics~~
- [ ] Implement real-time updates with SignalR

### Phase 3: Ordering Terminal (Day 2) ✅ COMPLETED
- [x] ~~Create Terminal area with simplified UI~~
- [x] ~~Implement quick product search~~
- [x] ~~Add incoming shipment form~~
- [x] ~~Add outgoing shipment form~~
- [x] ~~Keyboard shortcuts integration~~

### Phase 4: Order Management & Approvals (Manager) ✅ COMPLETED
- [x] ~~Bulk approve/reject orders (select multiple orders)~~
- [x] ~~Single order approve/reject buttons (currently only UpdateStatus exists)~~
- [x] ~~Order details page actions (Approve, Reject, Cancel buttons)~~
- [x] ~~Automated approval rules (auto-approve based on criteria)~~
- [x] ~~Order status change notifications~~
- [x] ~~Order filters by date range~~
- [x] ~~Order search by order number~~

### Phase 5: Product Management (Manager CRUD) ✅ COMPLETED
- [x] ~~Create Product form (Manager area)~~
- [x] ~~Edit Product form with validation~~
- [x] ~~Delete Product with confirmation~~
- [x] ~~Supplier CRUD (Create, Edit, Delete)~~
- [x] ~~Category management~~
- [x] ~~Bulk import/export products (CSV/Excel)~~

### Phase 6: B2B API
- [ ] Create API controllers for orders
- [ ] Create API controllers for products
- [ ] Configure JWT authentication
- [ ] Add Swagger documentation
- [ ] Implement webhook notifications
- [ ] API rate limiting
- [ ] API key management

### Phase 7: Reports & Analytics (Manager)
- [ ] Reports & exports (PDF/Excel)
- [ ] Predictive analytics (demand forecasting)
- [ ] Automatic reorder suggestions
- [ ] Low stock alerts configuration
- [ ] Stock movement history report
- [ ] Inventory valuation report
- [ ] Sales trends report

### Phase 8: Advanced Features
- [ ] User authentication system (without Identity)
- [ ] User management (Create, Edit, Delete users)
- [ ] Role-based access control
- [ ] Audit trail with user tracking
- [ ] Email notifications for approvals
- [ ] In-app notification system
- [ ] Barcode/QR code scanning for Terminal
- [ ] Batch entry (multiple products in one shipment)
- [ ] Expiration/shelf-life tracking

### Phase 9: Polish & Testing (Day 3)
- [ ] Write unit tests for core features
- [ ] Integration tests for API
- [ ] UI/UX improvements
- [ ] Performance optimization
- [ ] Error handling improvements
- [ ] Logging enhancements

---

## Sample Data to Seed

### Products (10+ items)
- Electronics: Laptop, Monitor, Keyboard, Mouse
- Office: Desk Chair, Standing Desk, Filing Cabinet
- Supplies: Paper, Ink Cartridges, Notebooks

### Suppliers (3-5)
- TechWholesale Inc.
- Office Depot Pro
- Global Parts Ltd.

### Orders (20+ with various statuses)
- Pending, Approved, Shipped, Delivered, Cancelled

### Users
- Admin (full access)
- Manager (dashboard + approvals)
- FloorStaff (terminal only)
- B2BClient (API access)

---

## Quick Start Commands

```bash
# Create solution
dotnet new sln -n StatStock

# Create projects
dotnet new classlib -n StatStock.Domain -o src/StatStock.Domain
dotnet new classlib -n StatStock.Application -o src/StatStock.Application
dotnet new classlib -n StatStock.Infrastructure -o src/StatStock.Infrastructure
dotnet new mvc -n StatStock.Web -o src/StatStock.Web

# Add projects to solution
dotnet sln add src/StatStock.Domain
dotnet sln add src/StatStock.Application
dotnet sln add src/StatStock.Infrastructure
dotnet sln add src/StatStock.Web

# Add references
dotnet add src/StatStock.Application reference src/StatStock.Domain
dotnet add src/StatStock.Infrastructure reference src/StatStock.Application
dotnet add src/StatStock.Web reference src/StatStock.Infrastructure

# Essential packages (Web project)
dotnet add src/StatStock.Web package Microsoft.EntityFrameworkCore.SqlServer
dotnet add src/StatStock.Web package Dapper
dotnet add src/StatStock.Web package Serilog.AspNetCore
dotnet add src/StatStock.Web package Swashbuckle.AspNetCore
```

---

## Notes

- **Timeline:** Testing agentic AI over a couple of days
- **Focus:** Full scaffold + data display features first
- **Deployment:** Local development only
- **Scale:** Single company with hierarchical access by area
