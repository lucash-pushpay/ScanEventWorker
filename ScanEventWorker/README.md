# Scan Event NoSQL

A .NET 8 worker service that processes parcel scan events from an external API and stores them in a LiteDB NoSQL database. The service handles order creation, pickup tracking, and delivery management for parcel logistics.

## Getting Started
- .NET 8.0 SDK
- External scan event API running (default: http://localhost:4444/) - if changed, make sure to update the BaseUrl in appsettings.json
- Log files are stored under `bin/Debug/net8.0/logs/` folder by default
- Db files are stored as `bin/Debug/net8.0/scanevents.db` by default