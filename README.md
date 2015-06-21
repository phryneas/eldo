# eldo
_**el**evated "**do**"_

or: a simple sudo for windows

> $ eldo.exe --help
> 
> elevated "do"
> 
> Usage: eldo [OPTIONS]+ COMMAND [ARGUMENTS]+
>
> Run command elevated as current user in the same console
> Options below will be parsed even if they are after COMMAND.
> In that case call eldo [OPTIONS]+ -- COMMAND [ARGUMENTS]+
> 
> Options:
>       --shell=VALUE          use this shell instead of SHELL environment variable
>   -v                         increase debug message verbosity, can be passed  multiple times
>  -h, --help                 show this message and exit


## Details

This command allows the user to execute commands elevated - with admin rights, but as himself, provided he can gain the neccessary rights.

Using the command opens a UAC prompt and then executes the command with the given arguments either with the current or the specified shell.

Should work with `cmd`, `powershell`, `bash` and `zsh`, in a *normal windows* environment, in *cygwin* and in *msys2*.

Feedback is welcome!