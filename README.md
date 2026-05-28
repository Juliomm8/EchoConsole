# 🌌 Echo Console - Cosmic Diner Telemetry Station

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white) ![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white) ![SQL Server](https://img.shields.io/badge/SQL_Server-CC292B?style=for-the-badge&logo=microsoft-sql-server&logoColor=white) ![Tailwind CSS](https://img.shields.io/badge/Tailwind_CSS-38B2AC?style=for-the-badge&logo=tailwind-css&logoColor=white)

**Echo Console** is a centralized, real-time telemetry and administration web platform engineered specifically for the indie horror game ***Cosmic Diner***. It provides a robust, high-performance backend infrastructure to monitor active game sessions, track hardware installations, manage release versions, and report systemic alerts seamlessly.

---

## 📸 System Previews

#### Guest View

<img width="2560" height="1528" alt="image" src="https://github.com/user-attachments/assets/eb3b8a71-4174-4ae8-8044-010dbf749ede" />

#### Admin View

<img width="2560" height="1528" alt="image" src="https://github.com/user-attachments/assets/881405d1-34cc-4d4b-bc9b-9cd5499c1dd2" />

#### User View

<img width="2560" height="1528" alt="image" src="https://github.com/user-attachments/assets/f600e5c2-6b00-44d5-8ae7-bcba555e2594" />


---

## ✨ Core Features & Modules

### 1. 📡 Live Monitoring & Telemetry (MVP Base)
* **Real-Time Session Tracking:** A dynamic dashboard structure built to monitor player progression.
* **SignalR Integration:** Features an active `AdminTelemetryHub` and a background `TelemetryRelayService` to process WebSockets events instantly.

### 2. 🎮 Games and Builds Management
* **Centralized Registry:** Tracks all deployed *Cosmic Diner* builds.
* **Version Control:** Monitors `VersionNumber`, `EngineVersion` (Unity), and `ReleaseNotes` with a fully searchable and paginated UI.

### 3. 🖥️ Devices & Installations Inventory
* **Hardware Tracking:** A master inventory system that tracks unique device profiles claiming ownership.
* **Specs Analytics:** Captures and displays comprehensive OS, CPU, GPU, and RAM telemetry from end-users.

### 4. 🔐 Security & Identity
* **BFF Security Model:** Separates user session management from internal API communication.
* **ASP.NET Core Identity:** Handles encrypted, HTTP-Only cookies for the web dashboard.
* **Server-to-Server Security:** Utilizes an `AdminApiKeyHandler` to protect API endpoints from unauthorized public access.

---

## 🏗️ Architecture & Tech Stack

The system is built using a **Backend-For-Frontend (BFF)** decoupled architecture, focusing on scalability and clean separation of concerns:

* **Backend API (`EchoConsole.Api`):** A RESTful API built with ASP.NET Core. It enforces business logic, payload validation, and data persistence using **Entity Framework Core 8.0** and **SQL Server**.
* **Frontend Web (`EchoConsole.Web`):** An ASP.NET Core MVC application functioning as the visual control station. It consumes the internal API securely via strongly-typed HTTP clients (e.g., `EchoConsoleProfileApiClient`, `EchoConsoleDashboardApiClient`).
* **UI/UX:** A fully responsive, "Cyberpunk/Dark" themed interface styled with **Tailwind CSS**, featuring full **i18n (English/Spanish)** localization support.

---

## 📂 Project Structure

```text
EchoConsole/
├── EchoConsole.Api/          # RESTful API Backend
│   ├── Contracts/            # DTOs and Request/Response Models
│   ├── Persistence/          # EF Core DbContext and Migrations
│   └── Controllers/          # API Endpoints (Admin & Client facing)
│
├── EchoConsole.Web/          # MVC Frontend Control Station
│   ├── Services/             # Typed HTTP API Clients
│   ├── Views/                # Razor Views (Tailwind CSS)
│   └── wwwroot/              # Static assets and i18n logic
│
└── EchoConsole.sln           # Visual Studio Solution
```

---

## 🚀 Getting Started (Local Development)

### Prerequisites
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
* SQL Server (LocalDB or standard instance)
* Visual Studio 2022 or VS Code

### 1. Clone the Repository
```bash
git clone [https://github.com/Juliomm8/EchoConsole.git](https://github.com/Juliomm8/EchoConsole.git)
cd EchoConsole
```

### 2. Database Setup
Ensure your SQL Server instance is running. The API uses Entity Framework Core to generate the schema automatically. Open your terminal in the `EchoConsole.Api` directory and execute:
```bash
dotnet ef database update
```

### 3. Configuration
Navigate to `EchoConsole.Web/appsettings.Development.json`. Ensure the `EchoConsoleApi:BaseUrl` matches the exact port your API is running on (e.g., `http://localhost:5047`) and that your `ApiKey` is properly set to match the API's expected key.

### 4. Run the Ecosystem
Configure Visual Studio to start **both** `EchoConsole.Api` and `EchoConsole.Web` projects simultaneously on startup.

---

## 💻 Developer Focus & Practices

This repository demonstrates the application of industry-standard software engineering practices, including:
* **Agile Branching:** Strict usage of `epic/` and `feature/` (or `task/`) branches for feature isolation.
* **Pull Request Workflow:** Structured PRs with clear scopes, detailed markdown descriptions, and module tracking.
* **Data Patterns:** Consistent implementation of DTO mapping, pagination patterns, and `.AsNoTracking()` for read optimization within Entity Framework Core.

---
*Developed for Cosmic Diner.*
