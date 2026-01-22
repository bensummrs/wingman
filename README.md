# Wingman - AI File Organization Agent

A Windows ARM-compatible AI agent built with Microsoft Semantic Kernel and Claude AI for intelligent file organization via PowerShell.

## Features

- 🤖 **AI-Powered**: Uses Claude 3.5 Sonnet for intelligent file categorization
- 📁 **File Organization**: Automatically organize files by type, date, or custom rules
- 💻 **PowerShell Integration**: Easy-to-use cmdlets for Windows automation
- 🔧 **ARM64 Compatible**: Built for Windows ARM devices
- 🛡️ **Safe Operations**: Preview changes before applying, with undo capability

## Prerequisites

- .NET 9.0 SDK or later
- Windows 10/11 (ARM64 or x64)
- Anthropic API key ([get one here](https://console.anthropic.com/))

## Quick Start

### 1. Clone and Build

```powershell
git clone <your-repo-url>
cd wingman
dotnet build
```

### 2. Configure API Key

**Option 1: Config File (Recommended)**

Copy `wingman.config.json.example` to `wingman.config.json` and add your API key:

```json
{
  "ApiKey": "your-anthropic-api-key-here",
  "Model": "claude-3-5-sonnet-20241022",
  "MaxTokens": 4096
}
```

Place this file in:
- The project directory, OR
- Your home directory (`C:\Users\YourName\wingman.config.json`)

**Option 2: Environment Variable**

```powershell
$env:ANTHROPIC_API_KEY = "your_actual_api_key_here"
```

(Note: Environment variables only work for the current session unless set permanently)

### 3. Import PowerShell Module

```powershell
Import-Module .\src\Wingman.PowerShell\bin\Debug\net9.0\Wingman.PowerShell.dll
```

### 4. Use Wingman

```powershell
# Organize your Downloads folder
Invoke-Wingman -Prompt "Organize my files by type" -Directory "C:\Users\YourName\Downloads"

# Start an interactive session
$session = Start-WingmanSession
Invoke-Wingman -Session $session -Prompt "What files are in my Documents folder?"
```

## Project Structure

```
wingman/
├── src/
│   ├── Wingman.Agent/          # Core agent logic with Semantic Kernel
│   ├── Wingman.PowerShell/     # PowerShell cmdlets
│   └── Wingman.Tests/          # Unit tests
├── examples/                    # Example scripts
├── .env.example                # Example configuration
└── README.md                   # This file
```

## Use Cases

- **Organize Downloads**: Sort files by type (images, documents, videos)
- **Clean Desktop**: Move files to appropriate folders
- **Date-based Organization**: Organize photos by year/month
- **Duplicate Detection**: Find and manage duplicate files
- **Custom Rules**: Create your own organization logic with AI

## Development

```powershell
# Build all projects
dotnet build

# Run tests
dotnet test

# Build PowerShell module
dotnet build src/Wingman.PowerShell/Wingman.PowerShell.csproj
```

## License

MIT License - see LICENSE file for details

## Contributing

Contributions welcome! Please open an issue or PR.
