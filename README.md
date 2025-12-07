# 🚀 Hackathon Results Management System  
### XML → SQL Server → LINQ Analytics → JSON Export (Console UI with Colored ASCII Tables)


<p align="center">
  <img src="https://img.shields.io/badge/Tech-.NET%209-blue?style=for-the-badge">
  <img src="https://img.shields.io/badge/ORM-Entity%20Framework%20Core-green?style=for-the-badge">
  <img src="https://img.shields.io/badge/Data-XML%20%7C%20JSON-yellow?style=for-the-badge">
  <img src="https://img.shields.io/badge/UI-Console%20App-lightgrey?style=for-the-badge">
  <img src="https://img.shields.io/badge/Status-Production%20Ready-success?style=for-the-badge">
</p>

<p align="center">
  <img src="https://raw.githubusercontent.com/im-kaami/HackathonApp/main/docs/hackathon_logo.png" width="170">
</p>

---

# ✨ Overview

**Hackathon Results Management System** is a `.NET 9` console-based backend application designed to automate the complete lifecycle of hackathon project scoring:

- 📥 **Import XML submissions**  
- 🗄️ **Store records in SQL Server via Entity Framework Core**  
- 🔍 **Run 15 LINQ queries** (simple → medium → complex)  
- 📤 **Export results to JSON**  
- 🎨 **Display beautifully formatted colored ASCII tables**

This project demonstrates real-world backend skills:

- OOP design  
- Events & Delegates  
- EF Core with SQL Server  
- File parsing & validation  
- Analytics with LINQ  
- Clean console UI output  

---

# 📐 Architecture

<p align="center">
  <img src="https://raw.githubusercontent.com/im-kaami/HackathonApp/main/docs/architecture.png" width="700">
</p>

### System Flow

1. Read & validate XML  
2. Apply upsert logic (Insert or Update)  
3. Persist into SQL Server  
4. Execute categorized LINQ queries  
5. Render console tables  
6. Export JSON results  

---

# 🔧 Core Components

## 📝 XML Importer — `ImportService.cs`

Handles:

- XML loading & schema validation  
- Business rules:
  - ID > 0  
  - String length limits  
  - No future dates  
  - Score ∈ [0, 100]  
  - Members ∈ [1, 15]  
- Duplicate ID detection  
- Uses:
  - `IDENTITY_INSERT` to preserve XML IDs  
  - EF Core upsert (update if exists, insert if new)  
- Raises an event:  
    - DataImported(inserted, updated, skipped, duration)

---

## 🔍 Query Engine — `QueryService.cs`

### **Simple Queries (Q01–Q05)**
- Filter by team  
- Filter by date  
- Filter by category  
- Score > 90  
- Top 5 scores  

### **Medium Queries (Q06–Q10)**
- Projects from 2024  
- Combined filters  
- Multi-key ordering  
- Category counts  
- Top 3 by team  

### **Complex Queries (Q11–Q15)**
- Average score per category  
- Compare score to category average  
- Name contains “AI” AND score > 92  
- Top 5 per category  
- Members ≥ 5 AND score > global average  

---

## 🎨 Console UI — `ConsoleTablePrinter.cs`

A fully custom ASCII table renderer featuring:

- Colored borders, headers & rows  
- Automatic column sizing  
- Word wrapping & truncation  
- Clean alignment  
- GitHub-friendly text formatting  

Example Table:
```diff
+----+------------+-----------+-------+
| Id | Team       | Project   | Score |
+----+------------+-----------+-------+
| 19 | NeuralNova | VisionAI  | 96.1  |
|  5 | NeuralNova | DocuAI    | 95.0  |
| 11 | NeuralNova | InsightAI | 93.6  |
+----+------------+-----------+-------+
```
---

# 📈 Example Workflow

### Input XML:

```xml
<Project>
  <Id>5</Id>
  <TeamName>NeuralNova</TeamName>
  <ProjectName>DocuAI</ProjectName>
  <Category>AI-ML</Category>
  <EventDate>2024-06-02</EventDate>
  <Score>95.0</Score>
  <Members>5</Members>
  <Captain>Reka Szabo</Captain>
</Project>
```

### Console Output:

```markdown
----- Import Completed -----
Inserted: 0, Updated: 20, Skipped: 0
Duration: 0.74s
----------------------------
```

### JSON Export Example:

```json
[
  {
    "Id": 19,
    "TeamName": "NeuralNova",
    "ProjectName": "VisionAI",
    "Category": "AI-ML",
    "EventDate": "2025-10-12",
    "Score": 96.1,
    "Members": 5,
    "Captain": "Reka Szabo"
  }
]
```
# 🛠️ How to Run the Project (Local Setup Guide)
This project includes two .NET projects:

- HackathonApp/ → Console application (startup project)
- HackathonData/ → EF Core + DbContext + Migrations

To run the project correctly, follow these steps.
### 1️⃣. Clone the repo:

```bash 
git clone https://github.com/im-kaami/HackathonApp.git
cd HackathonApp
```
### 2️⃣. Create Your Local appsettings.json:
The repo does not include real connection strings (for security).
Copy the example file:

#### Git Bash (Linux/macOS/Windows Git Bash):
```bash
cp HackathonApp/appsettings.example.json HackathonApp/appsettings.json
```
#### PowerShell

``` bash
Copy-Item HackathonApp\appsettings.example.json HackathonApp\appsettings.json
```
#### Ensure your connection string looks like this:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=HackathonDB;Trusted_Connection=True"
  }
}
```
❗ Important: Ensure the connection string uses exactly \\ (two backslashes).
If you see \\\\ (four slashes), fix it manually — JSON escaping will break LocalDB.

### 3️⃣. Ensure SQL Server LocalDB Exists

```bash
sqllocaldb info
```
If LocalDB is not running:
```bash
sqllocaldb start MSSQLLocalDB
```
If LocalDB instance doesn't exist:
```
sqllocaldb create MSSQLLocalDB
sqllocaldb start MSSQLLocalDB
```

### 4️⃣. Install the Correct EF Core Tool (Mandatory)
This project uses .NET 9, so install EF Core CLI version 9.x.
```
dotnet tool uninstall --global dotnet-ef
dotnet tool install --global dotnet-ef --version 9.0.10
dotnet ef --version
```

### 5️⃣. Restore & Build the Solution
From the repo root:
```
dotnet restore
dotnet build
```
### 6️⃣. Apply EF Core Migrations
You MUST specify both the DbContext project and the startup project.
```bash
dotnet ef database update \
  --project HackathonData/HackathonData.csproj \
  --startup-project HackathonApp/HackathonApp.csproj \
  --verbose
```
This will create the database HackathonDB in your LocalDB instance.

### 7️⃣. Run the Console Application
```bash
cd HackathonApp
dotnet run
```
You will see:
```mathematica
1) Import XML -> DB
2) Run Simple LINQ queries
3) Run Medium LINQ queries
4) Run Complex LINQ queries
5) Export latest queries to JSON
0) Exit
```

### 8️⃣. JSON Output Location
After running queries or choosing option 5 Outputs are stored in:
```
HackathonApp/Output/
```
# 📦 Repository Structure

```txt
hackathon-results-system/
│
├── HackathonApp/
│   ├── Program.cs
│   ├── ConsoleTablePrinter.cs
│   ├── appsettings.example.json
│   ├── Data/HackathonResults.xml
│   └── Output/               
│
├── HackathonData/
│   ├── Models/
│   │   └── Project.cs
│   ├── Services/
│   │   ├── ImportService.cs
│   │   └── QueryService.cs
│   ├── HackathonDbContext.cs
│   └── Migrations/
│
├── docs/
│   ├── architecture.png
│   └── hackathon_logo.png
│
├── README.md
├── .gitignore
```

# 🧑‍💻 Future Enhancements

- Add xUnit test project
- Add logging via Serilog
- Add CSV export option
- Add pagination in console tables
- Add command-line flags:
```markdown
--import  
--query=Q03  
--export  
```

# License
This project is released under the MIT License.

# 💬 Contact

For feedback, improvements, or collaboration — open an issue or reach out!

