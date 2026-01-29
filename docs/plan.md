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
| **Notifications** | In-app |
| **Language** | English only |
| **Deployment** | Local (development) |

---

## Analytics & Forecasting Algorithms

### Demand Forecast Algorithm

The **Demand Forecast** feature uses historical order data to predict future inventory needs and prevent stockouts.

**How It Works:**

1. **Data Collection (90-Day Lookback)**
   - Analyzes the last 90 days of **outgoing orders** (customer orders, not incoming stock)
   - Groups data by product to calculate consumption patterns
   - Only considers products with actual demand history

2. **Average Daily Demand Calculation**
   ```
   Average Daily Demand = Total Quantity Ordered / 90 days
   ```
   Example: If 44 units were ordered over 90 days → 0.49 units/day

3. **Days Until Stockout Prediction**
   ```
   Days Until Stockout = Current Stock / Average Daily Demand
   ```
   Example: 10 units in stock ÷ 0.49 units/day = ~20 days until stockout

4. **Recommended Order Quantity (with Safety Buffer)**
   ```
   Recommended Qty = (Avg Daily Demand × Days to Forecast × 1.2)
   ```
   - Default forecast period: 30 days
   - 20% safety buffer to account for demand spikes
   - Example: 0.49 × 30 × 1.2 = 17 units recommended

5. **Suggested Order Date**
   ```
   Order Date = Current Date + (Days Until Stockout - 7)
   ```
   - Orders 7 days before predicted stockout
   - Provides lead time for supplier fulfillment

6. **Confidence Scoring**
   - **80% confidence** → 5+ data points (orders) in the last 90 days
   - **50% confidence** → Fewer than 5 data points
   - More historical data = more reliable forecast

**Use Case:** Proactive inventory management to prevent stockouts without over-ordering.

---

### Reorder Suggestions Algorithm

The **Reorder Suggestions** feature identifies products that need immediate attention based on reorder levels.

**How It Works:**

1. **Low Stock Detection**
   ```
   Trigger: Current Stock ≤ Reorder Level
   ```
   - Automatically identifies products below their configured reorder threshold
   - Reorder levels are set per product (e.g., Monitor: 15 units)

2. **Stock Deficit Calculation**
   ```
   Deficit = Reorder Level - Current Stock
   ```
   Example: Reorder level of 15 - current stock of 10 = 5 unit deficit

3. **Recommended Order Quantity**
   ```
   Recommended Qty = MAX(Deficit × 2, Reorder Level)
   ```
   - Orders **twice the deficit** to build buffer stock
   - OR orders up to reorder level (whichever is higher)
   - Example: MAX(5 × 2, 15) = 15 units recommended

4. **Priority Classification**
   - **Critical** → Stock = 0 (out of stock, immediate action needed)
   - **High** → Stock < 50% of reorder level (running very low)
   - **Medium** → Stock ≤ reorder level but > 50% (needs attention soon)

5. **Cost Estimation**
   ```
   Estimated Cost = Recommended Qty × Current Unit Price
   ```
   Example: 15 units × $199.99 = $2,999.85

6. **Supplier Recommendation**
   - Analyzes recent order history (last 100 orders)
   - Suggests the most recently used supplier for that product
   - Helps maintain supplier relationships and pricing

**Use Case:** Reactive inventory management for immediate reordering needs.

---

### Key Differences

| Feature | Demand Forecast | Reorder Suggestions |
|---------|-----------------|---------------------|
| **Trigger** | Proactive (scheduled analysis) | Reactive (stock level threshold) |
| **Data Source** | 90 days of outgoing orders | Current stock vs reorder level |
| **Focus** | Future planning (30+ days) | Immediate needs (now) |
| **Algorithm** | Time-series based | Threshold-based |
| **Confidence** | Variable (50-80%) | Definitive (binary: low or not) |
| **Best For** | Strategic planning | Tactical replenishment |

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

### Phase 1: Foundation (Day 1) ✅ COMPLETED
- [x] ~~Create solution with Clean Architecture structure~~
- [x] ~~Set up Domain entities (Product, Supplier, Order, User)~~
- [x] ~~Configure EF Core with SQL Server~~
- [x] ~~Create database migrations~~
- [x] ~~Seed sample data~~

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

### Phase 6: B2B API ✅ COMPLETED
- [x] ~~Create API controllers for orders~~
- [x] ~~Create API controllers for products~~
- [x] ~~Configure JWT authentication~~
- [x] ~~Add Swagger documentation~~
- [x] ~~Implement webhook notifications~~
- [x] ~~API rate limiting~~
- [x] ~~API key management~~

### Phase 7: Reports & Analytics (Manager) ✅ COMPLETED
- [x] ~~Reports & exports (PDF/Excel)~~
- [x] ~~Predictive analytics (demand forecasting)~~
- [x] ~~Automatic reorder suggestions~~
- [x] ~~Low stock alerts configuration~~
- [x] ~~Stock movement history report~~
- [x] ~~Inventory valuation report~~
- [x] ~~Sales trends report~~

### Phase 8: Authentication and User Managment
- [x] User authentication system (without Identity)
- [x] User management (Create, Edit, Delete users)
- [x] Role-based access control
- [x] Audit trail with user tracking

### Phase 9: Advanced Features (optional)
- [ ] In-app notification system
- [ ] Barcode/QR code scanning for Terminal
- [ ] Batch entry (multiple products in one shipment)
- [ ] Expiration/shelf-life tracking

### Phase 10: Polish & Testing
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
