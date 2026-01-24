# Rot

[![CI](https://github.com/yourusername/Rot/actions/workflows/ci.yml/badge.svg)](https://github.com/yourusername/Rot/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Rot.svg)](https://www.nuget.org/packages/Rot/)

A .NET global tool for running tasks defined in `tasks.json` files, similar to VS Code's task runner.

## Installation

Install as a global .NET tool:

```bash
dotnet tool install --global Rot
```

## Usage

### Initialize a new tasks.json file
```bash
rot init                         # Creates tasks.json with default template
rot init --format yaml           # Creates tasks.yaml
rot init --template dotnet       # .NET project template
rot init --template node         # Node.js project template
rot init --template python       # Python project template
rot init --template docker       # Docker project template
```

### List available tasks
```bash
rotlist
rotlist --file custom-tasks.json
```

### Run a task
```bash
rot run build
rot run test --file custom-tasks.json

# Run with options
rot run build --dry-run          # Preview without executing
rot run build --verbose          # Show detailed output
rot run build --quiet            # Only show errors
```

### Run tasks by group, pattern, or tag
```bash
rot run --group build            # Run all tasks in "build" group
rot run --pattern "test-*"       # Run tasks matching pattern
rot run --tag ci                 # Run all tasks tagged with "ci"
```

### Output Capture
```bash
rot run build --output build.log   # Write output to file
rot run build --json               # Output results as JSON
rot run build --json -o results.json  # JSON output to file
```

### Describe a task
```bash
rot describe build               # Show detailed task information
```

### Watch mode
```bash
rot watch build --glob "src/**/*.cs"    # Re-run on file changes
rot watch test --glob "**/*.cs" --debounce 500
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
- **cwd**: Working directory for task execution
- **env**: Dictionary of environment variables to set for the task
- **shell**: Custom shell to use (default: /bin/bash on Unix, cmd.exe on Windows)
- **echo**: Show command output (default: true)
- **dependsOn**: Array of task names that must run before this task
- **allowConcurrent**: Allow dependencies to run in parallel when using --concurrent flag (default: false)
- **timeout**: Timeout in seconds before the task is killed
- **group**: Group name for batch execution (e.g., "build", "test")
- **tags**: Array of tags for filtering tasks (e.g., ["ci", "dev"])

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

### Environment Variables

You can set custom environment variables for tasks using the `env` property:

```json
{
  "deploy": {
    "label": "Deploy to production",
    "type": "shell",
    "command": "deploy.sh",
    "cwd": "./scripts",
    "env": {
      "ENVIRONMENT": "production",
      "API_KEY": "your-api-key"
    }
  }
}
```

Environment variables are set before the command executes and can be referenced in your commands:

```json
{
  "echo-env": {
    "label": "Echo environment variable",
    "type": "shell",
    "command": "echo $MY_VAR",
    "env": {
      "MY_VAR": "custom value"
    }
  }
}
```

**YAML Example:**
```yaml
deploy:
  label: Deploy to production
  command: deploy.sh
  cwd: ./scripts
  env:
    ENVIRONMENT: production
    API_KEY: your-api-key
```

### Custom Working Directory
```json
{
  "run-in-dir": {
    "label": "Run command in specific directory",
    "type": "shell",
    "command": "pwd",
    "cwd": "/tmp"
  }
}
```

### Variable Substitution

Define variables at the top level and use them in your commands with `${varName}` syntax:

```json
{
  "variables": {
    "config": "Release",
    "outputDir": "./dist"
  },
  "tasks": {
    "build": {
      "command": "dotnet build -c ${config} -o ${outputDir}"
    }
  }
}
```

**YAML Example:**
```yaml
variables:
  config: Release
  outputDir: ./dist

tasks:
  build:
    command: dotnet build -c ${config} -o ${outputDir}
```

You can also reference environment variables:

```json
{
  "tasks": {
    "deploy": {
      "command": "deploy.sh --env ${env:DEPLOY_ENV} --api-key ${env:API_KEY}"
    }
  }
}
```

### Task Groups and Tags

Organize tasks with groups and tags for batch execution:

```json
{
  "tasks": {
    "build": {
      "command": "dotnet build",
      "group": "compile",
      "tags": ["ci", "dev"]
    },
    "lint": {
      "command": "dotnet format --verify-no-changes",
      "group": "quality",
      "tags": ["ci"]
    },
    "test-unit": {
      "command": "dotnet test --filter Category=Unit",
      "group": "test",
      "tags": ["ci", "dev"]
    }
  }
}
```

Run tasks by group or tag:
```bash
rot run --group compile      # Run all tasks in "compile" group
rot run --tag ci             # Run all tasks tagged with "ci"
rot run --pattern "test-*"   # Run tasks matching pattern
```

### Task Timeout

Set a timeout (in seconds) to prevent runaway tasks:

```json
{
  "long-build": {
    "command": "npm run build",
    "timeout": 300
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