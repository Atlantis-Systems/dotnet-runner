namespace Rot.Services;

public static class ShellCompletionGenerator
{
    public static string GenerateBashCompletion()
    {
        return """
        # Bash completion for rot
        # Install: rot completion bash > /etc/bash_completion.d/rot
        #    or:  rot completion bash >> ~/.bashrc

        _rot_completions()
        {
            local cur prev opts commands
            COMPREPLY=()
            cur="${COMP_WORDS[COMP_CWORD]}"
            prev="${COMP_WORDS[COMP_CWORD-1]}"

            commands="list run init describe watch graph completion"
            opts="--file -f --concurrent -c --dry-run -n --verbose -v --quiet -q --log-file --help -h --version"

            case "${prev}" in
                rot)
                    # First argument - could be command or task name
                    if [[ -f "tasks.yaml" ]] || [[ -f "tasks.json" ]]; then
                        local tasks=$(rot list 2>/dev/null | grep -E '^\s+\w' | awk '{print $1}')
                        COMPREPLY=( $(compgen -W "${commands} ${tasks}" -- ${cur}) )
                    else
                        COMPREPLY=( $(compgen -W "${commands}" -- ${cur}) )
                    fi
                    return 0
                    ;;
                run)
                    if [[ -f "tasks.yaml" ]] || [[ -f "tasks.json" ]]; then
                        local tasks=$(rot list 2>/dev/null | grep -E '^\s+\w' | awk '{print $1}')
                        COMPREPLY=( $(compgen -W "${tasks}" -- ${cur}) )
                    fi
                    return 0
                    ;;
                describe)
                    if [[ -f "tasks.yaml" ]] || [[ -f "tasks.json" ]]; then
                        local tasks=$(rot list 2>/dev/null | grep -E '^\s+\w' | awk '{print $1}')
                        COMPREPLY=( $(compgen -W "${tasks}" -- ${cur}) )
                    fi
                    return 0
                    ;;
                watch)
                    if [[ -f "tasks.yaml" ]] || [[ -f "tasks.json" ]]; then
                        local tasks=$(rot list 2>/dev/null | grep -E '^\s+\w' | awk '{print $1}')
                        COMPREPLY=( $(compgen -W "${tasks}" -- ${cur}) )
                    fi
                    return 0
                    ;;
                graph)
                    if [[ -f "tasks.yaml" ]] || [[ -f "tasks.json" ]]; then
                        local tasks=$(rot list 2>/dev/null | grep -E '^\s+\w' | awk '{print $1}')
                        COMPREPLY=( $(compgen -W "${tasks}" -- ${cur}) )
                    fi
                    return 0
                    ;;
                --file|-f)
                    COMPREPLY=( $(compgen -f -- ${cur}) )
                    return 0
                    ;;
                --format)
                    COMPREPLY=( $(compgen -W "json yaml" -- ${cur}) )
                    return 0
                    ;;
                --profile)
                    # Try to get profiles from config
                    COMPREPLY=()
                    return 0
                    ;;
                --group|-g)
                    COMPREPLY=()
                    return 0
                    ;;
                --pattern|-p)
                    COMPREPLY=()
                    return 0
                    ;;
                --tag|-t)
                    COMPREPLY=()
                    return 0
                    ;;
                completion)
                    COMPREPLY=( $(compgen -W "bash zsh fish powershell" -- ${cur}) )
                    return 0
                    ;;
            esac

            if [[ ${cur} == -* ]]; then
                COMPREPLY=( $(compgen -W "${opts}" -- ${cur}) )
                return 0
            fi
        }

        complete -F _rot_completions rot
        """;
    }

    public static string GenerateZshCompletion()
    {
        return """
        #compdef rot

        # Zsh completion for rot
        # Install: rot completion zsh > ~/.zsh/completions/_rot
        # Make sure ~/.zsh/completions is in your fpath

        _rot() {
            local -a commands
            local -a tasks

            commands=(
                'list:List all available tasks'
                'run:Run a specific task'
                'init:Initialize a new tasks file'
                'describe:Show detailed information about a task'
                'watch:Watch for file changes and re-run a task'
                'graph:Display task dependency graph'
                'completion:Generate shell completion scripts'
            )

            _arguments -C \
                '(-f --file)'{-f,--file}'[Path to the tasks file]:file:_files' \
                '(-c --concurrent)'{-c,--concurrent}'[Enable concurrent execution]' \
                '(-n --dry-run)'{-n,--dry-run}'[Preview without executing]' \
                '(-v --verbose)'{-v,--verbose}'[Show detailed output]' \
                '(-q --quiet)'{-q,--quiet}'[Only show errors]' \
                '--log-file[Write logs to file]:file:_files' \
                '--help[Show help]' \
                '1: :->command' \
                '*: :->args'

            case $state in
                command)
                    _describe -t commands 'rot commands' commands
                    # Also suggest task names if tasks file exists
                    if [[ -f "tasks.yaml" ]] || [[ -f "tasks.json" ]]; then
                        tasks=(${(f)"$(rot list 2>/dev/null | grep -E '^\s+\w' | awk '{print $1}')"})
                        _describe -t tasks 'tasks' tasks
                    fi
                    ;;
                args)
                    case ${words[2]} in
                        run|describe|watch|graph)
                            if [[ -f "tasks.yaml" ]] || [[ -f "tasks.json" ]]; then
                                tasks=(${(f)"$(rot list 2>/dev/null | grep -E '^\s+\w' | awk '{print $1}')"})
                                _describe -t tasks 'tasks' tasks
                            fi
                            ;;
                        init)
                            _arguments '--format[File format]:format:(json yaml)'
                            ;;
                        completion)
                            _describe -t shells 'shells' '(bash zsh fish powershell)'
                            ;;
                    esac
                    ;;
            esac
        }

        _rot "$@"
        """;
    }

    public static string GenerateFishCompletion()
    {
        return """
        # Fish completion for rot
        # Install: rot completion fish > ~/.config/fish/completions/rot.fish

        # Disable file completion by default
        complete -c rot -f

        # Commands
        complete -c rot -n "__fish_use_subcommand" -a "list" -d "List all available tasks"
        complete -c rot -n "__fish_use_subcommand" -a "run" -d "Run a specific task"
        complete -c rot -n "__fish_use_subcommand" -a "init" -d "Initialize a new tasks file"
        complete -c rot -n "__fish_use_subcommand" -a "describe" -d "Show detailed task information"
        complete -c rot -n "__fish_use_subcommand" -a "watch" -d "Watch for changes and re-run task"
        complete -c rot -n "__fish_use_subcommand" -a "graph" -d "Display task dependency graph"
        complete -c rot -n "__fish_use_subcommand" -a "completion" -d "Generate shell completions"

        # Global options
        complete -c rot -s f -l file -d "Path to tasks file" -r
        complete -c rot -s c -l concurrent -d "Enable concurrent execution"
        complete -c rot -s n -l dry-run -d "Preview without executing"
        complete -c rot -s v -l verbose -d "Show detailed output"
        complete -c rot -s q -l quiet -d "Only show errors"
        complete -c rot -l log-file -d "Write logs to file" -r
        complete -c rot -s h -l help -d "Show help"

        # run command options
        complete -c rot -n "__fish_seen_subcommand_from run" -s g -l group -d "Run tasks by group" -r
        complete -c rot -n "__fish_seen_subcommand_from run" -s p -l pattern -d "Run tasks by pattern" -r
        complete -c rot -n "__fish_seen_subcommand_from run" -s t -l tag -d "Run tasks by tag" -r
        complete -c rot -n "__fish_seen_subcommand_from run" -l profile -d "Apply profile" -r
        complete -c rot -n "__fish_seen_subcommand_from run" -l no-cache -d "Disable caching"

        # init command options
        complete -c rot -n "__fish_seen_subcommand_from init" -l format -d "File format" -a "json yaml"

        # watch command options
        complete -c rot -n "__fish_seen_subcommand_from watch" -l glob -d "Glob pattern for files" -r
        complete -c rot -n "__fish_seen_subcommand_from watch" -l debounce -d "Debounce time in ms" -r

        # completion command arguments
        complete -c rot -n "__fish_seen_subcommand_from completion" -a "bash zsh fish powershell"

        # Task completions for run, describe, watch, graph
        function __rot_tasks
            if test -f "tasks.yaml" -o -f "tasks.json"
                rot list 2>/dev/null | string match -r '^\s+\w+' | string trim
            end
        end

        complete -c rot -n "__fish_seen_subcommand_from run describe watch graph" -a "(__rot_tasks)"
        complete -c rot -n "__fish_use_subcommand" -a "(__rot_tasks)"
        """;
    }

    public static string GeneratePowerShellCompletion()
    {
        return """
        # PowerShell completion for rot
        # Install: rot completion powershell >> $PROFILE

        Register-ArgumentCompleter -CommandName rot -Native -ScriptBlock {
            param($wordToComplete, $commandAst, $cursorPosition)

            $commands = @('list', 'run', 'init', 'describe', 'watch', 'graph', 'completion')
            $shells = @('bash', 'zsh', 'fish', 'powershell')
            $formats = @('json', 'yaml')

            # Get the command line tokens
            $tokens = $commandAst.CommandElements

            # Determine context
            $prevToken = if ($tokens.Count -gt 1) { $tokens[-2].Extent.Text } else { '' }
            $currentCommand = if ($tokens.Count -gt 1) { $tokens[1].Extent.Text } else { '' }

            # Helper to get tasks
            function Get-RotTasks {
                $tasksFile = if (Test-Path 'tasks.yaml') { 'tasks.yaml' } elseif (Test-Path 'tasks.json') { 'tasks.json' } else { $null }
                if ($tasksFile) {
                    try {
                        $output = rot list 2>$null
                        $output | Where-Object { $_ -match '^\s+\w' } | ForEach-Object { ($_ -split '\s+')[1] }
                    } catch { }
                }
            }

            # Complete based on context
            switch -Regex ($prevToken) {
                '^rot$' {
                    $tasks = Get-RotTasks
                    ($commands + $tasks) | Where-Object { $_ -like "$wordToComplete*" } | ForEach-Object {
                        [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_)
                    }
                }
                '^(run|describe|watch|graph)$' {
                    Get-RotTasks | Where-Object { $_ -like "$wordToComplete*" } | ForEach-Object {
                        [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_)
                    }
                }
                '^completion$' {
                    $shells | Where-Object { $_ -like "$wordToComplete*" } | ForEach-Object {
                        [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_)
                    }
                }
                '^--format$' {
                    $formats | Where-Object { $_ -like "$wordToComplete*" } | ForEach-Object {
                        [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_)
                    }
                }
                '^(-f|--file)$' {
                    Get-ChildItem -Path '.' -Filter '*.json' | ForEach-Object { $_.Name }
                    Get-ChildItem -Path '.' -Filter '*.yaml' | ForEach-Object { $_.Name }
                    Get-ChildItem -Path '.' -Filter '*.yml' | ForEach-Object { $_.Name }
                }
                default {
                    if ($wordToComplete -like '-*') {
                        @('-f', '--file', '-c', '--concurrent', '-n', '--dry-run', '-v', '--verbose', '-q', '--quiet', '--log-file', '--help') |
                            Where-Object { $_ -like "$wordToComplete*" } | ForEach-Object {
                                [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterName', $_)
                            }
                    } elseif ($commands -contains $currentCommand) {
                        # We're in a command context, suggest tasks
                        if (@('run', 'describe', 'watch', 'graph') -contains $currentCommand) {
                            Get-RotTasks | Where-Object { $_ -like "$wordToComplete*" } | ForEach-Object {
                                [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_)
                            }
                        }
                    } else {
                        # Root level - suggest commands and tasks
                        $tasks = Get-RotTasks
                        ($commands + $tasks) | Where-Object { $_ -like "$wordToComplete*" } | ForEach-Object {
                            [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_)
                        }
                    }
                }
            }
        }
        """;
    }

    public static string Generate(string shell)
    {
        return shell.ToLowerInvariant() switch
        {
            "bash" => GenerateBashCompletion(),
            "zsh" => GenerateZshCompletion(),
            "fish" => GenerateFishCompletion(),
            "powershell" or "pwsh" => GeneratePowerShellCompletion(),
            _ => throw new ArgumentException($"Unknown shell: {shell}. Supported shells: bash, zsh, fish, powershell")
        };
    }
}
