# 🌌 EchoConsole - Cosmic Diner Telemetry Station

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)
![SQL Server](https://img.shields.io/badge/SQL_Server-CC292B?style=for-the-badge&logo=microsoft-sql-server&logoColor=white)
![Tailwind CSS](https://img.shields.io/badge/Tailwind_CSS-38B2AC?style=for-the-badge&logo=tailwind-css&logoColor=white)

EchoConsole is a centralized, real-time telemetry and administration web platform designed for the indie horror game **Cosmic Diner**. It provides a robust backend infrastructure to monitor active game sessions, track hardware installations, manage release versions, and report systemic alerts.

## 📸 System Previews
*(Screenshots of the UI will go here)*
* `[Insert Screenshot of Dashboard]`
* `[Insert Screenshot of Games and Builds Table]`

---

## 🏗️ Architecture & Tech Stack
The system is built using a decoupled architecture, focusing on scalability and clean separation of concerns.

* **Backend API (`EchoConsole.Api`):** RESTful APIs built with ASP.NET Core. It handles business logic, validation, and data persistence using **Entity Framework Core** and **SQL Server**.
* **Frontend Web (`EchoConsole.Web`):** An ASP.NET Core MVC application functioning as the visual control station. It consumes the internal API via strongly-typed HTTP clients (`HttpClientFactory`).
* **UI/UX:** Fully responsive, "Cyberpunk/Dark" themed interface built with **Tailwind CSS**. It includes dynamic data tables and full **i18n (English/Spanish)** localization support.

---

## 🚀 Implemented Modules

### 1. Live Monitoring (MVP Base)
* Real-time session tracking dashboard structure.
* Active SignalR configuration *(In progress)*.

### 2. Games and Builds
* Centralized registry of monitored *Cosmic Diner* builds.
* Tracks `VersionNumber`, `EngineVersion` (e.g., Unity), and `ReleaseNotes`.
* Paginated and searchable UI.

### 3. Devices & Installations
* Master hardware inventory tracking unique device profiles.
* Captures and displays OS, CPU, GPU, and RAM telemetry from end-users.

---

## 📂 Project Structure

```text
EchoConsole/
├── EchoConsole.Api/          # RESTful API Backend
│   ├── Contracts/            # DTOs and Request/Response Models
│   ├── Persistence/          # EF Core DbContext and Migrations
│   └── Controllers/          # API Endpoints (Admin & Client facing)
├── EchoConsole.Web/          # MVC Frontend Control Station
│   ├── Services/             # Typed HTTP API Clients
│   ├── Views/                # Razor Views (Tailwind CSS)
│   └── wwwroot/              # Static assets and i18n logic
└── EchoConsole.sln           # Visual Studio Solution
```

---

## ⚙️ Getting Started (Local Development)

### Prerequisites
* [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
* SQL Server (LocalDB or standard instance)
* Visual Studio 2022 or VS Code

### 1. Clone the repository
```bash
git clone https://github.com/your-username/EchoConsole.git
cd EchoConsole
```

### 2. Database Setup
Ensure your SQL Server is running. The API uses Entity Framework Core to generate the schema automatically. Open the terminal in the `EchoConsole.Api` directory:
```bash
dotnet ef database update
```

### 3. Configuration
Make sure the `appsettings.json` in `EchoConsole.Web` points correctly to the API's localhost port.
```json
"ApiSettings": {
  "BaseUrl": "https://localhost:XXXX" // Replace with your API port
}
```

### 4. Run the Solution
Configure Visual Studio to start **both** `EchoConsole.Api` and `EchoConsole.Web` projects simultaneously on startup.

---

## 💡 Developer Focus & Practices
This repository demonstrates applied software engineering practices including:
* **Agile Branching:** Usage of `epic/` and `task/` branches for feature isolation.
* **Pull Request Workflow:** Structured PRs with clear scopes and module tracking.
* **Data Patterns:** Consistent implementation of DTO mapping, pagination patterns, and `AsNoTracking()` for read optimization.
