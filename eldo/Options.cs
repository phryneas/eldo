using Mono.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eldo
{
    /// <summary>
    /// Command Line Options for eldo
    /// </summary>
    class CommandLineOptions
    {
        /// <summary>
        /// verbosity. number of times the -v flag is passed
        /// </summary>
        public int verbosity = 0;
        /// <summary>
        /// shell, if specified
        /// </summary>
        public string shell;
        /// <summary>
        /// unparsed arguments
        /// </summary>
        public List<string> arguments = new List<string>();

        /// <summary>
        /// constructor, parses command line arguments
        /// </summary>
        /// <param name="args">command line arguments (without execution file name)</param>
        public CommandLineOptions(string[] args)
        {
            bool show_help = false;
            var p = new OptionSet() {
                    "elevated \"do\" "+Program.version,
                    "",
			        "Usage: "+Program.name+" [OPTIONS]+ COMMAND [ARGUMENTS]+",
                    "",
			        "Run command elevated as current user in the same console",
                    "Options below will be parsed even if they are after COMMAND.",
			        "In that case call "+Program.name+" [OPTIONS]+ -- COMMAND [ARGUMENTS]+",
			        "",
			        "Options:",
                    {"shell=", "use this shell instead of SHELL environment variable",
                        s => { shell = s; } },
			        { "v", "increase debug message verbosity, can be passed multiple times",
			            v => { if (v != null) ++verbosity; } },
			        { "h|help",  "show this message and exit", 
			            v => show_help = v != null },
		        };

            try
            {
                arguments = p.Parse(args);
                if (!show_help)
                {
                    if (arguments.Count < 1)
                        throw new OptionException("no command given", "command");
                }
            }
            catch (OptionException e)
            {
                Console.Write(Program.name + ": ");
                Console.WriteLine(e.Message);
                Console.WriteLine("Try `" + Program.name + " --help' for more information.");
                System.Environment.Exit(1);
            }

            if (show_help)
            {
                p.WriteOptionDescriptions(Console.Out);
                System.Environment.Exit(0);
            }
        }
        /// <summary>
        /// private singleton instance
        /// </summary>
        private static CommandLineOptions _instance;

        /// <summary>
        /// singleton instance getter
        /// </summary>
        public static CommandLineOptions instance
        {
            get
            {
                if (_instance == null)
                {
                    List<string> args = new List<string>(Environment.GetCommandLineArgs());
                    args.RemoveAt(0);
                    _instance = new CommandLineOptions(args.ToArray<string>());
                }
                return _instance;
            }
        }

        /// <summary>
        /// print message if verbosity is at least <paramref name="minlevel"/>
        /// </summary>
        /// <param name="minlevel"></param>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public void Debug(int minlevel, string format, params object[] args)
        {
            if (verbosity >= minlevel)
            {
                log(format, args);
            }
        }
        
        /// <summary>
        /// all logging output will pass through this method
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        private void log(string format, params object[] args) { 
                Console.Write("# ");
                Console.WriteLine(format, args);
        
        }

        /// <summary>
        /// parameter-callback for <see cref="debugCallback"/> - usually <see cref="CommandLineOptions.log"/>
        /// </summary>
        /// <param name="format">see String.Format</param>
        /// <param name="args">see String.FOrmat</param>
        public delegate void debugOutput(string format, params object[] args);

        /// <summary>
        /// callback for <see cref="Debug"/>
        /// </summary>
        /// <param name="log">logging function</param>
        public delegate void debugCallback(debugOutput log);

        /// <summary>
        /// will call debugCallback with a logging function if verbosity is at least <paramref name="minLevel"/>
        /// </summary>
        /// <param name="minLevel"></param>
        /// <param name="callback"></param>
        public void Debug(int minLevel, debugCallback callback){
            if (verbosity >= minLevel)
                callback(log);
        }

        /// <summary>
        /// logs an error
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        internal void Error(string format, params object[] args)
        {
            Console.Error.WriteLine(format, args);
        }
    }
}
