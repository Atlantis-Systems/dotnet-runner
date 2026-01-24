namespace Rot.Services;

/// <summary>
/// Provides project templates for the init command.
/// </summary>
public static class InitTemplates
{
    public static readonly string[] AvailableTemplates = { "dotnet", "node", "python", "docker", "default" };

    public static string GetTemplateJson(string template)
    {
        return template.ToLowerInvariant() switch
        {
            "dotnet" => DotNetTemplateJson,
            "node" => NodeTemplateJson,
            "python" => PythonTemplateJson,
            "docker" => DockerTemplateJson,
            _ => DefaultTemplateJson
        };
    }

    public static string GetTemplateYaml(string template)
    {
        return template.ToLowerInvariant() switch
        {
            "dotnet" => DotNetTemplateYaml,
            "node" => NodeTemplateYaml,
            "python" => PythonTemplateYaml,
            "docker" => DockerTemplateYaml,
            _ => DefaultTemplateYaml
        };
    }

    private const string DefaultTemplateJson = """
        {
          "version": "5.0.0",
          "variables": {
            "config": "Debug",
            "outputDir": "./bin"
          },
          "profiles": {
            "dev": {
              "variables": { "config": "Debug" },
              "env": { "ENVIRONMENT": "Development" }
            },
            "prod": {
              "variables": { "config": "Release" },
              "env": { "ENVIRONMENT": "Production" }
            }
          },
          "aliases": {
            "ci": ["build", "test"],
            "rebuild": ["clean", "build"]
          },
          "tasks": {
            "build": {
              "label": "Build the project",
              "type": "shell",
              "command": "echo Build task - customize for your project",
              "group": "build",
              "tags": ["ci", "dev"]
            },
            "test": {
              "label": "Run tests",
              "type": "shell",
              "command": "echo Test task - customize for your project",
              "group": "test",
              "tags": ["ci", "dev"],
              "dependsOn": ["build"]
            },
            "clean": {
              "label": "Clean build artifacts",
              "type": "shell",
              "command": "echo Clean task - customize for your project",
              "group": "build",
              "tags": ["dev"]
            }
          }
        }
        """;

    private const string DefaultTemplateYaml = """
        version: "5.0.0"
        variables:
          config: Debug
          outputDir: ./bin
        profiles:
          dev:
            variables:
              config: Debug
            env:
              ENVIRONMENT: Development
          prod:
            variables:
              config: Release
            env:
              ENVIRONMENT: Production
        aliases:
          ci:
            - build
            - test
          rebuild:
            - clean
            - build
        tasks:
          build:
            label: Build the project
            type: shell
            command: echo Build task - customize for your project
            group: build
            tags:
              - ci
              - dev
          test:
            label: Run tests
            type: shell
            command: echo Test task - customize for your project
            group: test
            tags:
              - ci
              - dev
            dependsOn:
              - build
          clean:
            label: Clean build artifacts
            type: shell
            command: echo Clean task - customize for your project
            group: build
            tags:
              - dev
        """;

    private const string DotNetTemplateJson = """
        {
          "version": "5.0.0",
          "variables": {
            "config": "Debug",
            "outputDir": "./bin",
            "framework": "net8.0"
          },
          "profiles": {
            "dev": {
              "variables": { "config": "Debug" },
              "env": { "DOTNET_ENVIRONMENT": "Development", "ASPNETCORE_ENVIRONMENT": "Development" }
            },
            "prod": {
              "variables": { "config": "Release" },
              "env": { "DOTNET_ENVIRONMENT": "Production", "ASPNETCORE_ENVIRONMENT": "Production" }
            }
          },
          "aliases": {
            "ci": ["restore", "build", "test"],
            "rebuild": ["clean", "restore", "build"],
            "publish": ["restore", "build", "pack"]
          },
          "tasks": {
            "restore": {
              "label": "Restore NuGet packages",
              "type": "shell",
              "command": "dotnet restore",
              "group": "build",
              "tags": ["ci"]
            },
            "build": {
              "label": "Build the solution",
              "type": "shell",
              "command": "dotnet build -c ${config} --no-restore",
              "group": "build",
              "tags": ["ci", "dev"],
              "dependsOn": ["restore"],
              "cache": {
                "inputs": ["**/*.cs", "**/*.csproj", "**/*.props", "**/*.targets"],
                "outputs": ["**/bin/", "**/obj/"]
              }
            },
            "test": {
              "label": "Run unit tests",
              "type": "shell",
              "command": "dotnet test -c ${config} --no-build --logger trx --results-directory TestResults",
              "group": "test",
              "tags": ["ci", "dev"],
              "dependsOn": ["build"]
            },
            "test-coverage": {
              "label": "Run tests with code coverage",
              "type": "shell",
              "command": "dotnet test -c ${config} --no-build --collect:\"XPlat Code Coverage\" --results-directory TestResults",
              "group": "test",
              "tags": ["ci"],
              "dependsOn": ["build"]
            },
            "clean": {
              "label": "Clean build artifacts",
              "type": "shell",
              "command": "dotnet clean -c ${config}",
              "group": "build",
              "tags": ["dev"]
            },
            "pack": {
              "label": "Create NuGet package",
              "type": "shell",
              "command": "dotnet pack -c Release --no-build -o ./artifacts",
              "group": "build",
              "tags": ["ci"],
              "dependsOn": ["build"],
              "condition": {
                "env": { "CI": "true" }
              }
            },
            "format": {
              "label": "Format code",
              "type": "shell",
              "command": "dotnet format",
              "group": "dev",
              "tags": ["dev"]
            },
            "watch": {
              "label": "Run with hot reload",
              "type": "shell",
              "command": "dotnet watch run",
              "group": "dev",
              "tags": ["dev"]
            }
          }
        }
        """;

    private const string DotNetTemplateYaml = """
        version: "5.0.0"
        variables:
          config: Debug
          outputDir: ./bin
          framework: net8.0
        profiles:
          dev:
            variables:
              config: Debug
            env:
              DOTNET_ENVIRONMENT: Development
              ASPNETCORE_ENVIRONMENT: Development
          prod:
            variables:
              config: Release
            env:
              DOTNET_ENVIRONMENT: Production
              ASPNETCORE_ENVIRONMENT: Production
        aliases:
          ci:
            - restore
            - build
            - test
          rebuild:
            - clean
            - restore
            - build
          publish:
            - restore
            - build
            - pack
        tasks:
          restore:
            label: Restore NuGet packages
            type: shell
            command: dotnet restore
            group: build
            tags:
              - ci
          build:
            label: Build the solution
            type: shell
            command: dotnet build -c ${config} --no-restore
            group: build
            tags:
              - ci
              - dev
            dependsOn:
              - restore
            cache:
              inputs:
                - "**/*.cs"
                - "**/*.csproj"
                - "**/*.props"
                - "**/*.targets"
              outputs:
                - "**/bin/"
                - "**/obj/"
          test:
            label: Run unit tests
            type: shell
            command: dotnet test -c ${config} --no-build --logger trx --results-directory TestResults
            group: test
            tags:
              - ci
              - dev
            dependsOn:
              - build
          test-coverage:
            label: Run tests with code coverage
            type: shell
            command: dotnet test -c ${config} --no-build --collect:"XPlat Code Coverage" --results-directory TestResults
            group: test
            tags:
              - ci
            dependsOn:
              - build
          clean:
            label: Clean build artifacts
            type: shell
            command: dotnet clean -c ${config}
            group: build
            tags:
              - dev
          pack:
            label: Create NuGet package
            type: shell
            command: dotnet pack -c Release --no-build -o ./artifacts
            group: build
            tags:
              - ci
            dependsOn:
              - build
            condition:
              env:
                CI: "true"
          format:
            label: Format code
            type: shell
            command: dotnet format
            group: dev
            tags:
              - dev
          watch:
            label: Run with hot reload
            type: shell
            command: dotnet watch run
            group: dev
            tags:
              - dev
        """;

    private const string NodeTemplateJson = """
        {
          "version": "5.0.0",
          "variables": {
            "nodeEnv": "development",
            "port": "3000"
          },
          "profiles": {
            "dev": {
              "variables": { "nodeEnv": "development" },
              "env": { "NODE_ENV": "development", "DEBUG": "*" }
            },
            "prod": {
              "variables": { "nodeEnv": "production" },
              "env": { "NODE_ENV": "production" }
            },
            "test": {
              "variables": { "nodeEnv": "test" },
              "env": { "NODE_ENV": "test" }
            }
          },
          "aliases": {
            "ci": ["install", "lint", "test", "build"],
            "dev": ["install", "start-dev"],
            "prepare": ["install", "build"]
          },
          "tasks": {
            "install": {
              "label": "Install dependencies",
              "type": "shell",
              "command": "npm ci",
              "group": "setup",
              "tags": ["ci"],
              "cache": {
                "inputs": ["package.json", "package-lock.json"],
                "outputs": ["node_modules/"]
              }
            },
            "build": {
              "label": "Build for production",
              "type": "shell",
              "command": "npm run build",
              "group": "build",
              "tags": ["ci", "prod"],
              "dependsOn": ["install"],
              "env": { "NODE_ENV": "production" }
            },
            "start": {
              "label": "Start production server",
              "type": "shell",
              "command": "npm start",
              "group": "run",
              "tags": ["prod"],
              "dependsOn": ["build"]
            },
            "start-dev": {
              "label": "Start development server",
              "type": "shell",
              "command": "npm run dev",
              "group": "run",
              "tags": ["dev"],
              "dependsOn": ["install"]
            },
            "test": {
              "label": "Run tests",
              "type": "shell",
              "command": "npm test",
              "group": "test",
              "tags": ["ci", "dev"],
              "dependsOn": ["install"]
            },
            "test-watch": {
              "label": "Run tests in watch mode",
              "type": "shell",
              "command": "npm test -- --watch",
              "group": "test",
              "tags": ["dev"],
              "dependsOn": ["install"]
            },
            "test-coverage": {
              "label": "Run tests with coverage",
              "type": "shell",
              "command": "npm test -- --coverage",
              "group": "test",
              "tags": ["ci"],
              "dependsOn": ["install"]
            },
            "lint": {
              "label": "Lint source code",
              "type": "shell",
              "command": "npm run lint",
              "group": "quality",
              "tags": ["ci", "dev"],
              "dependsOn": ["install"]
            },
            "lint-fix": {
              "label": "Fix linting issues",
              "type": "shell",
              "command": "npm run lint -- --fix",
              "group": "quality",
              "tags": ["dev"],
              "dependsOn": ["install"]
            },
            "format": {
              "label": "Format code with Prettier",
              "type": "shell",
              "command": "npm run format",
              "group": "quality",
              "tags": ["dev"],
              "dependsOn": ["install"]
            },
            "clean": {
              "label": "Clean build artifacts",
              "type": "shell",
              "command": "rm -rf dist build coverage node_modules/.cache",
              "group": "build",
              "tags": ["dev"]
            },
            "outdated": {
              "label": "Check for outdated packages",
              "type": "shell",
              "command": "npm outdated",
              "group": "setup",
              "tags": ["dev"]
            }
          }
        }
        """;

    private const string NodeTemplateYaml = """
        version: "5.0.0"
        variables:
          nodeEnv: development
          port: "3000"
        profiles:
          dev:
            variables:
              nodeEnv: development
            env:
              NODE_ENV: development
              DEBUG: "*"
          prod:
            variables:
              nodeEnv: production
            env:
              NODE_ENV: production
          test:
            variables:
              nodeEnv: test
            env:
              NODE_ENV: test
        aliases:
          ci:
            - install
            - lint
            - test
            - build
          dev:
            - install
            - start-dev
          prepare:
            - install
            - build
        tasks:
          install:
            label: Install dependencies
            type: shell
            command: npm ci
            group: setup
            tags:
              - ci
            cache:
              inputs:
                - package.json
                - package-lock.json
              outputs:
                - node_modules/
          build:
            label: Build for production
            type: shell
            command: npm run build
            group: build
            tags:
              - ci
              - prod
            dependsOn:
              - install
            env:
              NODE_ENV: production
          start:
            label: Start production server
            type: shell
            command: npm start
            group: run
            tags:
              - prod
            dependsOn:
              - build
          start-dev:
            label: Start development server
            type: shell
            command: npm run dev
            group: run
            tags:
              - dev
            dependsOn:
              - install
          test:
            label: Run tests
            type: shell
            command: npm test
            group: test
            tags:
              - ci
              - dev
            dependsOn:
              - install
          test-watch:
            label: Run tests in watch mode
            type: shell
            command: npm test -- --watch
            group: test
            tags:
              - dev
            dependsOn:
              - install
          test-coverage:
            label: Run tests with coverage
            type: shell
            command: npm test -- --coverage
            group: test
            tags:
              - ci
            dependsOn:
              - install
          lint:
            label: Lint source code
            type: shell
            command: npm run lint
            group: quality
            tags:
              - ci
              - dev
            dependsOn:
              - install
          lint-fix:
            label: Fix linting issues
            type: shell
            command: npm run lint -- --fix
            group: quality
            tags:
              - dev
            dependsOn:
              - install
          format:
            label: Format code with Prettier
            type: shell
            command: npm run format
            group: quality
            tags:
              - dev
            dependsOn:
              - install
          clean:
            label: Clean build artifacts
            type: shell
            command: rm -rf dist build coverage node_modules/.cache
            group: build
            tags:
              - dev
          outdated:
            label: Check for outdated packages
            type: shell
            command: npm outdated
            group: setup
            tags:
              - dev
        """;

    private const string PythonTemplateJson = """
        {
          "version": "5.0.0",
          "variables": {
            "pythonPath": "python3",
            "venvPath": ".venv"
          },
          "profiles": {
            "dev": {
              "env": { "PYTHONDONTWRITEBYTECODE": "1", "PYTHONUNBUFFERED": "1" }
            },
            "prod": {
              "env": { "PYTHONOPTIMIZE": "2" }
            },
            "test": {
              "env": { "PYTEST_ADDOPTS": "-v" }
            }
          },
          "aliases": {
            "ci": ["install", "lint", "test"],
            "dev": ["venv", "install", "run"],
            "setup": ["venv", "install"]
          },
          "tasks": {
            "venv": {
              "label": "Create virtual environment",
              "type": "shell",
              "command": "${pythonPath} -m venv ${venvPath}",
              "group": "setup",
              "tags": ["dev"],
              "condition": {
                "fileNotExists": [".venv"]
              }
            },
            "install": {
              "label": "Install dependencies",
              "type": "shell",
              "command": "${venvPath}/bin/pip install -r requirements.txt",
              "group": "setup",
              "tags": ["ci", "dev"],
              "condition": {
                "os": "linux"
              }
            },
            "install-windows": {
              "label": "Install dependencies (Windows)",
              "type": "shell",
              "command": "${venvPath}\\Scripts\\pip install -r requirements.txt",
              "group": "setup",
              "tags": ["ci", "dev"],
              "condition": {
                "os": "windows"
              }
            },
            "install-dev": {
              "label": "Install dev dependencies",
              "type": "shell",
              "command": "${venvPath}/bin/pip install -r requirements-dev.txt",
              "group": "setup",
              "tags": ["dev"]
            },
            "run": {
              "label": "Run application",
              "type": "shell",
              "command": "${venvPath}/bin/python -m app",
              "group": "run",
              "tags": ["dev"]
            },
            "test": {
              "label": "Run tests with pytest",
              "type": "shell",
              "command": "${venvPath}/bin/pytest tests/",
              "group": "test",
              "tags": ["ci", "dev"]
            },
            "test-coverage": {
              "label": "Run tests with coverage",
              "type": "shell",
              "command": "${venvPath}/bin/pytest tests/ --cov=app --cov-report=html --cov-report=xml",
              "group": "test",
              "tags": ["ci"]
            },
            "lint": {
              "label": "Lint with ruff",
              "type": "shell",
              "command": "${venvPath}/bin/ruff check .",
              "group": "quality",
              "tags": ["ci", "dev"]
            },
            "lint-fix": {
              "label": "Fix linting issues",
              "type": "shell",
              "command": "${venvPath}/bin/ruff check . --fix",
              "group": "quality",
              "tags": ["dev"]
            },
            "format": {
              "label": "Format with black",
              "type": "shell",
              "command": "${venvPath}/bin/black .",
              "group": "quality",
              "tags": ["dev"]
            },
            "format-check": {
              "label": "Check formatting",
              "type": "shell",
              "command": "${venvPath}/bin/black . --check",
              "group": "quality",
              "tags": ["ci"]
            },
            "typecheck": {
              "label": "Type check with mypy",
              "type": "shell",
              "command": "${venvPath}/bin/mypy app/",
              "group": "quality",
              "tags": ["ci", "dev"]
            },
            "clean": {
              "label": "Clean build artifacts",
              "type": "shell",
              "command": "rm -rf __pycache__ .pytest_cache .coverage htmlcov dist build *.egg-info",
              "group": "build",
              "tags": ["dev"]
            },
            "build": {
              "label": "Build package",
              "type": "shell",
              "command": "${venvPath}/bin/python -m build",
              "group": "build",
              "tags": ["ci"]
            }
          }
        }
        """;

    private const string PythonTemplateYaml = """
        version: "5.0.0"
        variables:
          pythonPath: python3
          venvPath: .venv
        profiles:
          dev:
            env:
              PYTHONDONTWRITEBYTECODE: "1"
              PYTHONUNBUFFERED: "1"
          prod:
            env:
              PYTHONOPTIMIZE: "2"
          test:
            env:
              PYTEST_ADDOPTS: "-v"
        aliases:
          ci:
            - install
            - lint
            - test
          dev:
            - venv
            - install
            - run
          setup:
            - venv
            - install
        tasks:
          venv:
            label: Create virtual environment
            type: shell
            command: ${pythonPath} -m venv ${venvPath}
            group: setup
            tags:
              - dev
            condition:
              fileNotExists:
                - .venv
          install:
            label: Install dependencies
            type: shell
            command: ${venvPath}/bin/pip install -r requirements.txt
            group: setup
            tags:
              - ci
              - dev
            condition:
              os: linux
          install-windows:
            label: Install dependencies (Windows)
            type: shell
            command: ${venvPath}\Scripts\pip install -r requirements.txt
            group: setup
            tags:
              - ci
              - dev
            condition:
              os: windows
          install-dev:
            label: Install dev dependencies
            type: shell
            command: ${venvPath}/bin/pip install -r requirements-dev.txt
            group: setup
            tags:
              - dev
          run:
            label: Run application
            type: shell
            command: ${venvPath}/bin/python -m app
            group: run
            tags:
              - dev
          test:
            label: Run tests with pytest
            type: shell
            command: ${venvPath}/bin/pytest tests/
            group: test
            tags:
              - ci
              - dev
          test-coverage:
            label: Run tests with coverage
            type: shell
            command: ${venvPath}/bin/pytest tests/ --cov=app --cov-report=html --cov-report=xml
            group: test
            tags:
              - ci
          lint:
            label: Lint with ruff
            type: shell
            command: ${venvPath}/bin/ruff check .
            group: quality
            tags:
              - ci
              - dev
          lint-fix:
            label: Fix linting issues
            type: shell
            command: ${venvPath}/bin/ruff check . --fix
            group: quality
            tags:
              - dev
          format:
            label: Format with black
            type: shell
            command: ${venvPath}/bin/black .
            group: quality
            tags:
              - dev
          format-check:
            label: Check formatting
            type: shell
            command: ${venvPath}/bin/black . --check
            group: quality
            tags:
              - ci
          typecheck:
            label: Type check with mypy
            type: shell
            command: ${venvPath}/bin/mypy app/
            group: quality
            tags:
              - ci
              - dev
          clean:
            label: Clean build artifacts
            type: shell
            command: rm -rf __pycache__ .pytest_cache .coverage htmlcov dist build *.egg-info
            group: build
            tags:
              - dev
          build:
            label: Build package
            type: shell
            command: ${venvPath}/bin/python -m build
            group: build
            tags:
              - ci
        """;

    private const string DockerTemplateJson = """
        {
          "version": "5.0.0",
          "variables": {
            "imageName": "myapp",
            "imageTag": "latest",
            "containerName": "myapp-container",
            "port": "8080"
          },
          "profiles": {
            "dev": {
              "variables": { "imageTag": "dev" },
              "env": { "DOCKER_BUILDKIT": "1" }
            },
            "prod": {
              "variables": { "imageTag": "latest" },
              "env": { "DOCKER_BUILDKIT": "1" }
            }
          },
          "aliases": {
            "ci": ["build", "test", "push"],
            "dev": ["build-dev", "run"],
            "deploy": ["build", "push", "deploy-remote"]
          },
          "tasks": {
            "build": {
              "label": "Build Docker image",
              "type": "shell",
              "command": "docker build -t ${imageName}:${imageTag} .",
              "group": "build",
              "tags": ["ci", "prod"],
              "env": { "DOCKER_BUILDKIT": "1" }
            },
            "build-dev": {
              "label": "Build Docker image for development",
              "type": "shell",
              "command": "docker build -t ${imageName}:dev --target development .",
              "group": "build",
              "tags": ["dev"],
              "env": { "DOCKER_BUILDKIT": "1" }
            },
            "build-no-cache": {
              "label": "Build Docker image without cache",
              "type": "shell",
              "command": "docker build --no-cache -t ${imageName}:${imageTag} .",
              "group": "build",
              "tags": ["dev"]
            },
            "run": {
              "label": "Run container",
              "type": "shell",
              "command": "docker run -d --name ${containerName} -p ${port}:${port} ${imageName}:${imageTag}",
              "group": "run",
              "tags": ["dev"],
              "preTasks": ["stop"]
            },
            "run-interactive": {
              "label": "Run container interactively",
              "type": "shell",
              "command": "docker run -it --rm -p ${port}:${port} ${imageName}:${imageTag}",
              "group": "run",
              "tags": ["dev"]
            },
            "stop": {
              "label": "Stop and remove container",
              "type": "shell",
              "command": "docker rm -f ${containerName} 2>/dev/null || true",
              "group": "run",
              "tags": ["dev"]
            },
            "logs": {
              "label": "Show container logs",
              "type": "shell",
              "command": "docker logs -f ${containerName}",
              "group": "run",
              "tags": ["dev"]
            },
            "shell": {
              "label": "Open shell in running container",
              "type": "shell",
              "command": "docker exec -it ${containerName} /bin/sh",
              "group": "run",
              "tags": ["dev"]
            },
            "test": {
              "label": "Run tests in container",
              "type": "shell",
              "command": "docker run --rm ${imageName}:${imageTag} npm test",
              "group": "test",
              "tags": ["ci"]
            },
            "push": {
              "label": "Push image to registry",
              "type": "shell",
              "command": "docker push ${imageName}:${imageTag}",
              "group": "deploy",
              "tags": ["ci"],
              "condition": {
                "env": { "CI": "true" }
              }
            },
            "pull": {
              "label": "Pull latest image",
              "type": "shell",
              "command": "docker pull ${imageName}:${imageTag}",
              "group": "deploy",
              "tags": ["prod"]
            },
            "clean": {
              "label": "Remove all project images",
              "type": "shell",
              "command": "docker rmi $(docker images ${imageName} -q) 2>/dev/null || true",
              "group": "build",
              "tags": ["dev"]
            },
            "prune": {
              "label": "Prune unused Docker resources",
              "type": "shell",
              "command": "docker system prune -f",
              "group": "build",
              "tags": ["dev"]
            },
            "compose-up": {
              "label": "Start services with docker-compose",
              "type": "shell",
              "command": "docker-compose up -d",
              "group": "run",
              "tags": ["dev"],
              "condition": {
                "fileExists": ["docker-compose.yml"]
              }
            },
            "compose-down": {
              "label": "Stop services with docker-compose",
              "type": "shell",
              "command": "docker-compose down",
              "group": "run",
              "tags": ["dev"],
              "condition": {
                "fileExists": ["docker-compose.yml"]
              }
            },
            "compose-logs": {
              "label": "Show docker-compose logs",
              "type": "shell",
              "command": "docker-compose logs -f",
              "group": "run",
              "tags": ["dev"],
              "condition": {
                "fileExists": ["docker-compose.yml"]
              }
            }
          }
        }
        """;

    private const string DockerTemplateYaml = """
        version: "5.0.0"
        variables:
          imageName: myapp
          imageTag: latest
          containerName: myapp-container
          port: "8080"
        profiles:
          dev:
            variables:
              imageTag: dev
            env:
              DOCKER_BUILDKIT: "1"
          prod:
            variables:
              imageTag: latest
            env:
              DOCKER_BUILDKIT: "1"
        aliases:
          ci:
            - build
            - test
            - push
          dev:
            - build-dev
            - run
          deploy:
            - build
            - push
            - deploy-remote
        tasks:
          build:
            label: Build Docker image
            type: shell
            command: docker build -t ${imageName}:${imageTag} .
            group: build
            tags:
              - ci
              - prod
            env:
              DOCKER_BUILDKIT: "1"
          build-dev:
            label: Build Docker image for development
            type: shell
            command: docker build -t ${imageName}:dev --target development .
            group: build
            tags:
              - dev
            env:
              DOCKER_BUILDKIT: "1"
          build-no-cache:
            label: Build Docker image without cache
            type: shell
            command: docker build --no-cache -t ${imageName}:${imageTag} .
            group: build
            tags:
              - dev
          run:
            label: Run container
            type: shell
            command: docker run -d --name ${containerName} -p ${port}:${port} ${imageName}:${imageTag}
            group: run
            tags:
              - dev
            preTasks:
              - stop
          run-interactive:
            label: Run container interactively
            type: shell
            command: docker run -it --rm -p ${port}:${port} ${imageName}:${imageTag}
            group: run
            tags:
              - dev
          stop:
            label: Stop and remove container
            type: shell
            command: docker rm -f ${containerName} 2>/dev/null || true
            group: run
            tags:
              - dev
          logs:
            label: Show container logs
            type: shell
            command: docker logs -f ${containerName}
            group: run
            tags:
              - dev
          shell:
            label: Open shell in running container
            type: shell
            command: docker exec -it ${containerName} /bin/sh
            group: run
            tags:
              - dev
          test:
            label: Run tests in container
            type: shell
            command: docker run --rm ${imageName}:${imageTag} npm test
            group: test
            tags:
              - ci
          push:
            label: Push image to registry
            type: shell
            command: docker push ${imageName}:${imageTag}
            group: deploy
            tags:
              - ci
            condition:
              env:
                CI: "true"
          pull:
            label: Pull latest image
            type: shell
            command: docker pull ${imageName}:${imageTag}
            group: deploy
            tags:
              - prod
          clean:
            label: Remove all project images
            type: shell
            command: docker rmi $(docker images ${imageName} -q) 2>/dev/null || true
            group: build
            tags:
              - dev
          prune:
            label: Prune unused Docker resources
            type: shell
            command: docker system prune -f
            group: build
            tags:
              - dev
          compose-up:
            label: Start services with docker-compose
            type: shell
            command: docker-compose up -d
            group: run
            tags:
              - dev
            condition:
              fileExists:
                - docker-compose.yml
          compose-down:
            label: Stop services with docker-compose
            type: shell
            command: docker-compose down
            group: run
            tags:
              - dev
            condition:
              fileExists:
                - docker-compose.yml
          compose-logs:
            label: Show docker-compose logs
            type: shell
            command: docker-compose logs -f
            group: run
            tags:
              - dev
            condition:
              fileExists:
                - docker-compose.yml
        """;
}
