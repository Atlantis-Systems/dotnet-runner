# DotnetRunner

[![CI](https://github.com/yourusername/DotnetRunner/actions/workflows/ci.yml/badge.svg)](https://github.com/yourusername/DotnetRunner/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/DotnetRunner.svg)](https://www.nuget.org/packages/DotnetRunner/)

A .NET global tool for running tasks defined in `tasks.json` files, similar to VS Code's task runner.

## Installation

Install as a global .NET tool:

```bash
dotnet tool install --global DotnetRunner
```

## Usage

### List available tasks
```bash
dotnet-runner list
dotnet-runner list --file custom-tasks.json
```

### Run a task
```bash
dotnet-runner run build
dotnet-runner run test --file custom-tasks.json
```

## Task Configuration

Create a `tasks.json` file in your project root:

```json
{
  "version": "2.0.0",
  "tasks": {
    "build": {
      "label": "Build the project",
      "type": "shell",
      "command": "dotnet build",
      "group": "build"
    },
    "test": {
      "label": "Run tests",
      "type": "shell", 
      "command": "dotnet test",
      "dependsOn": ["build"],
      "group": "test"
    },
    "clean": {
      "label": "Clean build artifacts",
      "type": "shell",
      "command": "dotnet clean"
    }
  }
}
```

### Task Properties

- **label**: Human-readable task name
- **type**: Task type (`shell` or `process`)
- **command**: Command to execute
- **args**: Array of command arguments (for process type)
- **dependsOn**: Array of task names that must run before this task
- **group**: Task group (e.g., "build", "test")
- **options**: Execution options
  - **cwd**: Working directory
  - **env**: Environment variables
  - **shell**: Custom shell to use
- **presentation**: Output presentation options
  - **echo**: Show command output (default: true)
  - **reveal**: Reveal output panel (default: true)

## Examples

### Shell Commands
```json
{
  "build": {
    "type": "shell",
    "command": "dotnet build --configuration Release"
  }
}
```

### Process with Arguments
```json
{
  "echo": {
    "type": "shell",
    "command": "echo",
    "args": ["Hello", "World!"]
  }
}
```

### Task Dependencies
```json
{
  "build": {
    "command": "dotnet build"
  },
  "test": {
    "command": "dotnet test",
    "dependsOn": ["build"]
  }
}
```

### Custom Environment
```json
{
  "deploy": {
    "command": "deploy.sh",
    "options": {
      "cwd": "./scripts",
      "env": {
        "ENVIRONMENT": "production"
      }
    }
  }
}
```

## Development

### Automatic Versioning & Publishing

This project uses **GitVersion** for automatic semantic versioning and publishes to NuGet on every push to `main`.

- **Versioning**: GitVersion automatically calculates version numbers based on git history
- **Publishing**: Every push to `main` branch automatically publishes to NuGet
- **CI/CD**: Pull requests are built and tested but not published

### Version Strategy

- **main branch**: Patch increment (1.0.0 → 1.0.1 → 1.0.2)
- **Pull requests**: PullRequest pre-release versions (1.0.1-PullRequest0001.1)

To increment version types, use conventional commit messages:
- `fix:` → Patch version
- `feat:` → Minor version  
- `feat!:` or `BREAKING CHANGE:` → Major version

### Required Secrets

Add this secret to your GitHub repository:

- `NUGET_API_KEY`: Your NuGet.org API key for publishing packages

## License

MIT License - see LICENSE file for details.