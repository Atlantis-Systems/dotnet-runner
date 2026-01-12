# Dot

A .NET global tool for running tasks defined in `tasks.json` files, similar to VS Code's task runner.

## Quick Start

Install as a global .NET tool:
```bash
dotnet tool install --global Dot
```

Initialize a new tasks.json file:
```bash
dot init
```

Run a task:
```bash
dot run build
```

## Features

- Execute shell commands and processes defined in JSON configuration
- Task dependencies and sequential execution
- Concurrent task execution support
- VS Code task runner compatibility
- Custom working directories and environment variables

## Documentation

See [Dot/README.md](Dot/README.md) for detailed documentation and examples.

## Development

This project uses GitVersion for automatic semantic versioning and publishes to NuGet on every push to `main`.