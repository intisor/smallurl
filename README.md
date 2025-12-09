# SmallURL - URL Shortener Service

A modern, feature-rich URL shortening service built with ASP.NET Core 8.0 and MySQL. SmallURL provides a simple and efficient way to create shortened URLs with additional functionality for appending Microsoft Learn contributor IDs to Microsoft domain links.

## Table of Contents

- [Features](#features)
- [Technology Stack](#technology-stack)
- [Architecture Overview](#architecture-overview)
- [Prerequisites](#prerequisites)
- [Installation](#installation)
- [Configuration](#configuration)
- [Running the Application](#running-the-application)
- [Usage](#usage)
- [Project Structure](#project-structure)
- [API Endpoints](#api-endpoints)
- [Database Schema](#database-schema)
- [Development](#development)
- [Security Considerations](#security-considerations)
- [License](#license)
- [Author](#author)

## Features

### Core Functionality
- **URL Shortening**: Convert long URLs into short, manageable links
- **Automatic Redirect**: Shortened URLs automatically redirect to original destinations
- **Hash-based Short Codes**: Uses Hashids library to generate unique, collision-resistant short codes (5 characters minimum)
- **Persistent Storage**: URLs stored in MySQL database with timestamps
- **URL Validation**: Validates URLs before shortening to ensure they are properly formatted

### Special Features
- **Microsoft Link Handler**: Secret page for appending Microsoft Student Ambassador contributor IDs to Microsoft domain links
- **Supported Microsoft Domains**: Automatically appends tracking IDs to links from:
  - azure.microsoft.com
  - learn.microsoft.com
  - code.visualstudio.com
  - developer.microsoft.com
  - And 13+ other Microsoft domains

### Web Interface
- Clean, responsive UI built with Bootstrap
- Simple form-based URL submission
- Instant shortened URL generation with copy functionality
- Error handling and user feedback

## Technology Stack

### Backend
- **Framework**: ASP.NET Core 8.0 (MVC pattern)
- **Language**: C# with .NET 8.0
- **Database**: MySQL 8.0.23+
- **ORM**: Entity Framework Core 8.0.8

### Key Libraries
- **Hashids.net** (v1.7.0) - Generates short, unique, non-sequential IDs from numbers
- **Pomelo.EntityFrameworkCore.MySql** (v8.0.2) - MySQL database provider for EF Core
- **Microsoft.EntityFrameworkCore.Tools** (v8.0.8) - EF Core tools for migrations and scaffolding

### Frontend
- **UI Framework**: Bootstrap (via bundled libraries)
- **JavaScript**: Vanilla JavaScript for clipboard operations
- **View Engine**: Razor Pages (.cshtml)

## Architecture Overview

SmallURL follows the classic MVC (Model-View-Controller) architecture pattern:

```
┌─────────────┐
│   Browser   │
└──────┬──────┘
       │
       ▼
┌─────────────────────────────────┐
│     HomeController              │
│  - Index (GET)                  │
│  - Shorten (POST)               │
│  - RedirectToUrl (GET)          │
│  - Secret (GET/POST)            │
└────────┬────────────────────────┘
         │
         ▼
┌─────────────────────────────────┐
│  ApplicationDbContext           │
│  (Entity Framework Core)        │
└────────┬────────────────────────┘
         │
         ▼
┌─────────────────────────────────┐
│     MySQL Database              │
│  - UrlMappings Table            │
└─────────────────────────────────┘
```

### Data Flow

1. **URL Shortening**:
   - User submits URL via web form
   - Controller validates URL format
   - URL saved to database, auto-generating an ID
   - Hashids encodes the numeric ID into a short code
   - Short code stored and returned to user

2. **URL Redirection**:
   - User visits shortened URL (e.g., /abc12)
   - Controller decodes short code using Hashids
   - Database lookup retrieves original URL
   - Permanent redirect (301) to original URL

## Prerequisites

Before you begin, ensure you have the following installed:

- **.NET SDK 8.0 or later** - [Download here](https://dotnet.microsoft.com/download)
- **MySQL Server 8.0.23 or later** - [Download here](https://dev.mysql.com/downloads/mysql/)
- **Git** - For cloning the repository
- **Visual Studio 2022** or **Visual Studio Code** (optional, but recommended)

### Verify Installation

```bash
# Check .NET version
dotnet --version
# Should output 8.0.x or later

# Check MySQL version
mysql --version
# Should output 8.0.23 or later
```

## Installation

### 1. Clone the Repository

```bash
git clone https://github.com/intisor/smallurl.git
cd smallurl
```

### 2. Restore Dependencies

```bash
dotnet restore
```

This will install all required NuGet packages:
- Hashids.net
- Microsoft.EntityFrameworkCore.Design
- Microsoft.EntityFrameworkCore.Tools
- Pomelo.EntityFrameworkCore.MySql

### 3. Set Up MySQL Database

Create a new MySQL database:

```sql
CREATE DATABASE smallurl CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
```

Optionally, create a dedicated user:

```sql
CREATE USER 'smallurl_user'@'localhost' IDENTIFIED BY 'your_secure_password';
GRANT ALL PRIVILEGES ON smallurl.* TO 'smallurl_user'@'localhost';
FLUSH PRIVILEGES;
```

## Configuration

### Database Connection String

Update the connection string in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "server=localhost;user=root;database=smallurl;port=3306;password=YOUR_PASSWORD"
  }
}
```

**Security Note**: For production deployments, use environment variables or secure configuration providers instead of storing credentials in `appsettings.json`.

### Environment-Specific Settings

For development, you can override settings in `appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

### Hashids Configuration

The application uses Hashids with a salt value defined in `Program.cs`. For production:

1. **Change the salt value** from "my salt" to a unique, secret value
2. **Consider making it configurable** via app settings
3. **Keep minimum length at 5** or adjust based on your needs

```csharp
builder.Services.AddSingleton<IHashids>(_ => new Hashids("your_unique_salt", 5));
```

### Apply Database Migrations

After configuring the connection string, apply the database migrations:

```bash
dotnet ef database update
```

This will create the `UrlMappings` table with the following structure:
- `Id` (int, primary key, auto-increment)
- `OriginalUrl` (longtext)
- `ShortCode` (longtext, nullable)
- `CreatedDate` (datetime)

## Running the Application

### Development Mode

```bash
dotnet run
```

Or with watch mode (auto-recompile on file changes):

```bash
dotnet watch run
```

The application will start on:
- HTTPS: `https://localhost:5001`
- HTTP: `http://localhost:5000`

### Production Mode

```bash
dotnet publish -c Release -o ./publish
cd publish
dotnet smallurl.dll
```

### Using Visual Studio

1. Open `smallurl.sln` in Visual Studio
2. Press F5 to run with debugging, or Ctrl+F5 without debugging
3. The application will launch in your default browser

## Usage

### Shortening a URL

1. Navigate to the home page (`/`)
2. Enter a valid URL in the text field (e.g., `https://example.com/very/long/path/to/resource`)
3. Click "Shorten"
4. Copy the generated short URL (e.g., `https://localhost:5001/aBc12`)

**Example**:
```
Input:  https://www.example.com/very/long/url/that/needs/shortening
Output: https://localhost:5001/jR89K
```

### Using Shortened URLs

Simply visit the shortened URL in any browser, and you'll be automatically redirected to the original destination.

```bash
# Example redirect
GET https://localhost:5001/jR89K
→ 301 Redirect to https://www.example.com/very/long/url/that/needs/shortening
```

### Microsoft Link Handler (Secret Feature)

1. Navigate to `/Secret` endpoint
2. Enter a Microsoft domain URL
3. The system will append the Student Ambassador contributor ID: `?wt.mc_id=studentamb_425455`
4. If the URL already contains a `wt.mc_id` parameter, it won't be duplicated
5. Copy the appended link

**Supported Domains**:
- azure.microsoft.com
- learn.microsoft.com
- code.visualstudio.com
- developer.microsoft.com
- dotnet.microsoft.com
- techcommunity.microsoft.com
- mvp.microsoft.com
- reactor.microsoft.com
- And more...

**Example**:
```
Input:  https://learn.microsoft.com/en-us/dotnet/
Output: https://learn.microsoft.com/en-us/dotnet/?wt.mc_id=studentamb_425455
```

## Project Structure

```
smallurl/
├── Controllers/
│   └── HomeController.cs          # Main controller handling all requests
├── Data/
│   └── ApplicationDbContext.cs    # EF Core database context
├── Migrations/
│   ├── 20241007131238_initiated.cs           # Initial migration
│   ├── 20241007131238_initiated.Designer.cs
│   └── ApplicationDbContextModelSnapshot.cs
├── Models/
│   ├── ErrorViewModel.cs          # Error page model
│   ├── SecretPageModel.cs         # Microsoft link handler model
│   └── UrlMapping.cs              # URL entity model
├── Properties/
│   └── launchSettings.json        # Development launch profiles
├── Views/
│   ├── Home/
│   │   ├── Index.cshtml           # URL shortening page
│   │   ├── Privacy.cshtml         # Privacy/about page
│   │   └── Secret.cshtml          # MS link handler page
│   ├── Shared/
│   │   └── ...                    # Layout and shared views
│   ├── _ViewImports.cshtml
│   └── _ViewStart.cshtml
├── wwwroot/
│   ├── css/                       # Stylesheets
│   ├── js/                        # JavaScript files
│   ├── lib/                       # Client-side libraries (Bootstrap, jQuery)
│   └── favicon.ico
├── appsettings.json               # Application configuration
├── appsettings.Development.json   # Development overrides
├── Program.cs                     # Application entry point
├── smallurl.csproj                # Project file
├── smallurl.sln                   # Solution file
├── LICENSE.txt                    # MIT License
└── README.md                      # This file
```

## API Endpoints

### Public Endpoints

| Method | Route | Description | Request | Response |
|--------|-------|-------------|---------|----------|
| GET | `/` | Home page with URL shortening form | - | HTML page |
| POST | `/Home/Shorten` | Shorten a URL | Form data: `url` (string) | HTML with short URL in ViewBag |
| GET | `/{shortCode}` | Redirect to original URL | Path param: shortCode | 301 Redirect or 404 |
| GET | `/Privacy` | Privacy/About page | - | HTML page |
| GET | `/Secret` | MS link handler form | - | HTML page |
| POST | `/Secret` | Append MS contributor ID | Form data: `MsLink` (string) | HTML with appended link |

### Request/Response Examples

#### Shorten URL
```http
POST /Home/Shorten HTTP/1.1
Content-Type: application/x-www-form-urlencoded

url=https://example.com/long/url
```

Response: HTML page with `ViewBag.ShortUrl` set to shortened URL

#### Redirect
```http
GET /jR89K HTTP/1.1
```

Response:
```http
HTTP/1.1 301 Moved Permanently
Location: https://example.com/long/url
```

## Database Schema

### UrlMappings Table

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| Id | INT | PRIMARY KEY, AUTO_INCREMENT | Unique identifier for each URL mapping |
| OriginalUrl | LONGTEXT | NOT NULL | The original long URL |
| ShortCode | LONGTEXT | NULLABLE | The generated short code (Hashids encoded) |
| CreatedDate | DATETIME(6) | NOT NULL | Timestamp when the URL was shortened |

**Notes**:
- The `Id` field is used as the source for generating the short code via Hashids
- `ShortCode` is nullable initially but populated after the record is saved
- Character set: `utf8mb4` for full Unicode support
- Collation: `utf8mb4_unicode_ci` for case-insensitive comparison

### Entity Relationship

```
UrlMapping
├── Id (PK)
├── OriginalUrl
├── ShortCode
└── CreatedDate
```

Currently, there are no foreign key relationships as this is a single-entity application.

## Development

### Database Migrations

Create a new migration after modifying models:

```bash
dotnet ef migrations add MigrationName
```

Apply migrations:

```bash
dotnet ef database update
```

Revert to a previous migration:

```bash
dotnet ef database update PreviousMigrationName
```

Remove the last migration (if not applied):

```bash
dotnet ef migrations remove
```

### Building the Project

```bash
# Debug build
dotnet build

# Release build
dotnet build -c Release
```

### Running Tests

Currently, the project does not include unit tests. To add testing:

```bash
# Create a test project
dotnet new xunit -n smallurl.Tests
dotnet add smallurl.Tests reference smallurl.csproj

# Run tests
dotnet test
```

### Code Style

This project follows standard C# coding conventions:
- PascalCase for public members and types
- camelCase for private fields (with underscore prefix: `_fieldName`)
- Async methods suffixed with `Async`
- Dependency injection via constructor

## Security Considerations

⚠️ **Important Security Notes**

### Current Issues

1. **Hardcoded Credentials**: The `appsettings.json` contains database credentials. For production:
   - Use Azure Key Vault, AWS Secrets Manager, or similar
   - Use environment variables
   - Never commit credentials to source control

2. **Hardcoded Hashids Salt**: The salt "my salt" should be changed:
   ```csharp
   // In Program.cs, change to a secure random value
   var salt = Environment.GetEnvironmentVariable("HASHIDS_SALT") ?? "default_salt";
   builder.Services.AddSingleton<IHashids>(_ => new Hashids(salt, 5));
   ```

3. **Open Redirects**: The application redirects to user-provided URLs. Consider:
   - Implementing a whitelist of allowed domains
   - Adding a warning page before external redirects
   - Rate limiting URL creation

4. **No Authentication**: Anyone can create short URLs. For production:
   - Add authentication/authorization
   - Implement rate limiting
   - Consider CAPTCHA for public endpoints

### Recommendations

1. **HTTPS Only**: Always use HTTPS in production (configured via `UseHttpsRedirection()`)
2. **Input Validation**: URLs are validated via `Uri.TryCreate()`
3. **SQL Injection Protection**: Entity Framework Core provides parameterized queries
4. **HSTS**: HTTP Strict Transport Security is enabled in production mode
5. **CORS**: Configure CORS policies if building an API

### Environment Variables Setup

```bash
# Linux/macOS
export ConnectionStrings__DefaultConnection="server=localhost;user=root;database=smallurl;port=3306;password=YOUR_PASSWORD"
export HASHIDS_SALT="your_random_secret_salt"

# Windows (PowerShell)
$env:ConnectionStrings__DefaultConnection="server=localhost;user=root;database=smallurl;port=3306;password=YOUR_PASSWORD"
$env:HASHIDS_SALT="your_random_secret_salt"
```

## Contributing

Contributions are welcome! Please follow these guidelines:

1. **Fork the repository**
2. **Create a feature branch**: `git checkout -b feature/amazing-feature`
3. **Commit your changes**: `git commit -m 'Add amazing feature'`
4. **Push to the branch**: `git push origin feature/amazing-feature`
5. **Open a Pull Request**

### Development Guidelines

- Follow existing code style and conventions
- Add XML documentation comments for public APIs
- Update README if adding new features
- Test thoroughly before submitting PR

## Troubleshooting

### Common Issues

**Issue**: "Unable to connect to MySQL server"
- **Solution**: Verify MySQL is running and credentials in `appsettings.json` are correct

**Issue**: "A network-related or instance-specific error occurred"
- **Solution**: Check that MySQL is listening on port 3306 and firewall allows connections

**Issue**: "The Entity Framework tools version X is older than that of the runtime"
- **Solution**: Update EF Core tools: `dotnet tool update --global dotnet-ef`

**Issue**: Short codes are not unique
- **Solution**: The Hashids salt may need to be changed. Ensure it's unique per deployment.

**Issue**: 404 when accessing shortened URL
- **Solution**: Verify the short code exists in database and routing is configured correctly

## License

This project is licensed under the MIT License - see the [LICENSE.txt](LICENSE.txt) file for details.

```
MIT License

Copyright (c) 2024 Abdulawwal Intisor

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
```

## Author

**Abdulawwal Intisor**

- LinkedIn: [intitech07](https://www.linkedin.com/in/intitech07/)
- GitHub: [@intisor](https://github.com/intisor)

Microsoft Learn Student Ambassador 

---

## Roadmap

Future enhancements being considered:

- [ ] Analytics dashboard (click tracking, geographic data)
- [ ] Custom short codes (user-defined aliases)
- [ ] QR code generation for shortened URLs
- [ ] Expiration dates for shortened URLs
- [ ] User accounts and URL management
- [ ] API endpoints with authentication
- [ ] Link preview before redirect
- [ ] Docker containerization
- [ ] Rate limiting and abuse prevention
- [ ] URL validation against malware databases

---

**Built with ❤️ using ASP.NET Core**
