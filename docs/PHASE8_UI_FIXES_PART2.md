# Phase 8 UI Fixes (Part 2)

## Summary
Addressed remaining UI layout issues in the Manager sidebar and modernized the Login/Access Denied pages to match the application's Tailwind CSS design system.

## Changes

### 1. Manager Sidebar Layout
- **Issue:** Navigation menu items were pushing the user profile section off-screen on smaller vertical displays.
- **Fix:** Added `overflow-y-auto` to the navigation container in `Areas/Manager/Views/Shared/_Layout.cshtml`.
- **Result:** Navigation now scrolls independently, keeping the user profile section fixed and visible at the bottom.

### 2. Authentication Pages Redesign
- **Issue:** Login and Access Denied pages were using legacy Bootstrap styling and looked inconsistent with the rest of the app.
- **Fix:** 
  - Created new `_AuthLayout.cshtml` shared view with a modern, centered card layout using Tailwind CSS.
  - Rewrote `Login.cshtml` to use the new layout, with improved form styling and "Quick Fill" demo credential buttons.
  - Rewrote `AccessDenied.cshtml` to use the new layout, with a clear 403 Forbidden design and helpful action buttons.

## Files Modified/Created
- `src/StatStock.Web/Areas/Manager/Views/Shared/_Layout.cshtml` (Modified)
- `src/StatStock.Web/Views/Shared/_AuthLayout.cshtml` (Created)
- `src/StatStock.Web/Views/Account/Login.cshtml` (Modified)
- `src/StatStock.Web/Views/Account/AccessDenied.cshtml` (Modified)

## Verification
- **Sidebar:** Resize browser window vertically; sidebar menu should scroll while user profile stays pinned.
- **Login:** Visit `/Account/Login`. Page should be clean, centered, and offer clickable demo credentials.
- **Access Denied:** Try accessing a Manager page as FloorStaff. Page should show the new "Access Denied" design.
