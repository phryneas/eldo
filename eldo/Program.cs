using Mono.Options;
using ReadEnvSample;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace eldo
{


    class Program
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool AttachConsole(uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        public static extern bool FreeConsole();

        public static uint ATTACH_PARENT_PROCESS = UInt32.MaxValue;

        public static string name = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
        public static string version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();

        /// <summary>
        /// program entry point
        /// </summary>
        /// <param name="args_"></param>
        static void Main(string[] args_)
        {
            // attach to parent console
            FreeConsole();
            AttachConsole(ATTACH_PARENT_PROCESS);

            // parse command line options
            CommandLineOptions opts = CommandLineOptions.instance;

            // log all command line arguments
            opts.Debug(2, (CommandLineOptions.debugOutput log) =>
            {
                foreach (string a in args_)
                    log(a);
            });

            // check for elevation, gain elevation if necessary
            gainElevation();

            // get shell
            string SHELL = getShell();


            ProcessStartInfo psi = new ProcessStartInfo(SHELL);

            // psi already has the full filename, parsing is easier without the extension
            if (SHELL.ToLower().EndsWith(".exe"))
                SHELL = SHELL.Substring(0, SHELL.Length - 4);

            // prepare arguments for different shells
            List<string> args;
            if (SHELL.ToLower().EndsWith("sh"))
            {
                args = new List<string>(opts.arguments);
                for (int i = 0; i < args.Count; i++)
                    args[i] = Escaping.Nix.EscapeShellArg(args[i]);

                psi.Arguments = String.Join(" ", args);
                psi.Arguments = String.Format("-c {0}", Escaping.Win.EscapeShellArg(psi.Arguments));
            }
            else if (SHELL.ToLower().EndsWith("cmd"))
            {
                args = new List<string>(opts.arguments);
                for (int i = 0; i < args.Count; i++)
                    args[i] = Escaping.Win.CmdQuote(args[i]);

                psi.Arguments = String.Join(" ", args);
                psi.Arguments = String.Format("/C {0}", "call " + psi.Arguments);
            }
            else if (SHELL.ToLower().EndsWith("powershell"))
            {
                args = new List<string>(opts.arguments);
                for (int i = 0; i < args.Count; i++)
                    args[i] = Escaping.Nix.EscapeShellArg(args[i]);

                psi.Arguments = String.Join(" ", args);
                psi.Arguments = String.Format("-Command {0}", Escaping.Win.EscapeShellArg("& " + psi.Arguments));
            }

            // we are using our own shell
            psi.UseShellExecute = false;
            opts.Debug(1, psi.FileName + " " + psi.Arguments);

            // and execute the command
            executeProcess(psi,
                afterExecution: (Process p) => { Environment.Exit(p.ExitCode); },
                executionError: (Exception e) =>
                {
                    if (e is Win32Exception && ((Win32Exception)e).ErrorCode == -2147467259)
                        Console.WriteLine("shell not found: '{0}'", SHELL);
                    else
                        Console.Error.WriteLine(e.Message);
                    Environment.Exit(-1);
                }
                );
        }

        /// <summary>
        /// try to get the best shell available, with this priority:
        /// 1. --shell command line argument
        /// 2. SHELL environment variable
        /// 3. next parent process that is either cmd or powershell
        /// 4. take cmd by default
        /// 
        /// this string will be normalized to windows path if we are executing from within cygwin and have a *nix path
        /// </summary>
        /// <returns>a shell</returns>
        private static string getShell()
        {
            // checks for *nix-style path, corrects it if neccessary & possible
            Func<string, string> fixPath = (string shell) =>
            {
                if (!File.Exists(shell) && shell.StartsWith("/"))
                    shell = cygpath(shell);

                return shell;
            };

            // look for --shell command line parameter
            if (!String.IsNullOrEmpty(CommandLineOptions.instance.shell))
            {
                return fixPath(CommandLineOptions.instance.shell);
            }

            // look for SHELL environment variable
            if (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("shell")))
            {
                return fixPath(Environment.GetEnvironmentVariable("shell"));
            }

            // try to get closest ancestor that is a shell
            {
                int apid = (int)getParentPid();
                try
                {
                    // continue until no more parent exists (which will result in an ArgumentException)
                    while (true)
                    {
                        Process ancestor = Process.GetProcessById(apid);
                        CommandLineOptions.instance.Debug(2, "Looking for parent shell, current pid: {0} ({1})", apid, ancestor.ProcessName);
                        switch (ancestor.ProcessName)
                        {
                            case "powershell":
                            case "cmd":
                            case "bash":
                            case "sh":
                            case "zsh":
                                var shell = ancestor.Modules[0].FileName;
                                CommandLineOptions.instance.Debug(2, "Found a valid ancestor shell: {0}", shell);
                                return shell;
                        }
                        apid = (int)getParentPid(apid);
                    }
                }
                catch (ArgumentException)
                {
                    CommandLineOptions.instance.Debug(2, "Reached top without finding a valid parent shell - parent process {0} is not alive", apid);
                    // continue below
                }
                catch (Win32Exception e)
                {
                    CommandLineOptions.instance.Debug(2, "We are a 32-bit process, parent shell is a 64-bit process. We can't read it's filename.\n Error message: {0}", e.Message);
                    //continue below
                }
            }

            // return failsafe default value
            return "cmd";
        }

        /// <summary>
        /// try to execute the cygpath binary to convert a *nix path to a windows path
        /// if cygpath is not found or anything else happens, simply returns the input path
        /// </summary>
        /// <param name="path"></param>
        /// <returns>windows-style path</returns>
        private static string cygpath(string path)
        {
            ProcessStartInfo psi = new ProcessStartInfo("cygpath");
            psi.Arguments = String.Format("-w {0}", Escaping.Win.EscapeShellArg(path));
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            CommandLineOptions.instance.Debug(1, psi.FileName + " " + psi.Arguments);

            executeProcess(psi,
                afterExecution: (Process p) =>
                {
                    if (!p.StandardOutput.EndOfStream && p.ExitCode == 0)
                        path = p.StandardOutput.ReadLine();
                }
                );


            return path;
        }

        /// <summary>
        /// check if this process has elevated permissions.
        /// * if so, check if we were elevated by running this method in a parent process
        ///   => inherit that parent processes ENVIRONMENT
        /// * if not, execute this process with all arguments using runas
        /// </summary>
        static void gainElevation()
        {

            if (!IsAdministrator())
            {
                Process parent = Process.GetProcessById((int)getParentPid());
                if (parent.ProcessName == Process.GetCurrentProcess().ProcessName)
                {
                    CommandLineOptions.instance.Error("Already tried elevation, but did not gain Administrator privileges. Giving up.");
                    Environment.Exit(1);
                }
                CommandLineOptions.instance.Debug(1, "Not elevated. Restarting with runas.");
                // Restart program and run as admin
                var exeName = Process.GetCurrentProcess().MainModule.FileName;
                ProcessStartInfo psi = new ProcessStartInfo(exeName);
                List<string> args = new List<string>(Environment.GetCommandLineArgs());
                args.RemoveAt(0);
                for (int i = 0; i < args.Count; i++)
                    args[i] = Escaping.Win.EscapeShellArg(args[i]);
                psi.Arguments = String.Join(" ", args);
                psi.Verb = "runas";
                CommandLineOptions.instance.Debug(1, psi.FileName + " " + psi.Arguments);

                executeProcess(psi, afterExecution: (Process p) => { Environment.Exit(p.ExitCode); });
            }
            else
            {
                // we are elevated, check if we were elevated using the runas above.
                int ppid = (int)getParentPid();
                Process parent = Process.GetProcessById(ppid);
                if (parent.ProcessName == Process.GetCurrentProcess().ProcessName)
                {
                    // if so, restore environment variables from parent process (these get lost at runas)
                    var parentEnv = ProcessEnvironment.ReadEnvironmentVariables(parent);
                    foreach (string key in parentEnv.Keys)
                    {
                        if (parentEnv[key] != Environment.GetEnvironmentVariable(key))
                            CommandLineOptions.instance.Debug(3, "setting {0}={1}, was {2}", key, parentEnv[key], Environment.GetEnvironmentVariable(key));
                        Environment.SetEnvironmentVariable(key, parentEnv[key]);
                    }
                }
            }
        }
        
        /// <summary>
        /// delegate, to be called after <see cref="executeProcess" completely executed/>
        /// </summary>
        /// <param name="p">finished Process</param>
        delegate void afterExecutionHandler(Process p);
        /// <summary>
        /// delegate, to be called on an Exception during <see cref="executeProcess"/>
        /// </summary>
        /// <param name="e">thrown Execution</param>
        delegate void executionErrorHandler(Exception e);
        /// <summary>
        /// Used to start a subprocess. This method handles Ctrl+C and cancels the child process before exiting.
        /// </summary>
        /// <param name="psi">information for the process to be started</param>
        /// <param name="afterExecution">delegate, in case of success</param>
        /// <param name="executionError">delegate, in case of error</param>
        private static void executeProcess(ProcessStartInfo psi, afterExecutionHandler afterExecution = null, executionErrorHandler executionError = null)
        {
            try
            {
                using (Process process = Process.Start(psi))
                {
                    ConsoleCancelEventHandler handler =
                    (object o, ConsoleCancelEventArgs a) =>
                    {
                        if (process != null && !process.HasExited)
                            try
                            {
                                CommandLineOptions.instance.Debug(1, "Exiting, but process didn't finish yet. Killing it.");
                                process.Kill();
                            }
                            catch { }
                    };
                    Console.CancelKeyPress += handler;
                    process.WaitForExit();
                    Console.CancelKeyPress -= handler;
                    if (afterExecution != null)
                        afterExecution(process);
                }
            }
            catch (Exception e)
            {
                if (executionError != null)
                    executionError(e);
            }
        }

        /// <summary>
        /// check for administrator privileges
        /// </summary>
        /// <returns>boolean</returns>
        private static bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        /// <summary>
        /// get parent pid for current process
        /// </summary>
        /// <returns>uint</returns>
        static uint getParentPid()
        {
            return getParentPid(Process.GetCurrentProcess().Id);
        }

        /// <summary>
        /// get parent pid for specified process
        /// </summary>
        /// <param name="cpid">child process</param>
        /// <returns>uint</returns>
        static uint getParentPid(int cpid)
        {
            string query = string.Format("SELECT ParentProcessId FROM Win32_Process WHERE ProcessId = {0}", cpid);
            var results = new ManagementObjectSearcher("root\\CIMV2", query).Get().GetEnumerator();
            results.MoveNext();
            ManagementBaseObject queryObj = results.Current;
            return (uint)queryObj["ParentProcessId"];
        }
    }
}
