/// greatly inspired by this blog post by Daniel Colascione:
/// http://blogs.msdn.com/b/twistylittlepassagesallalike/archive/2011/04/23/everyone-quotes-arguments-the-wrong-way.aspx

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eldo.Escaping
{
    static class Win
    {

        // this is a translation of Daniel Colascione's C function to C# - only here for assertion and only for a while
        public static string ArgvQuote(string Argument, bool Force = false)
        {
            StringBuilder CommandLine = new StringBuilder(Argument.Length * 2);
            if (!Force && !String.IsNullOrEmpty(Argument) && Argument.IndexOfAny(new char[] { ' ', '\t', '\n', '\v', '"' }) == -1)
            {
                CommandLine.Append(Argument);
            }
            else
            {
                CommandLine.Append('"');

                for (int i = 0; ; i++)
                {
                    int NumberBackslashes = 0;

                    while (i < Argument.Length && Argument[i] == '\\')
                    {
                        i++;
                        NumberBackslashes++;
                    }

                    if (i == Argument.Length)
                    {
                        // Escape all backslashes, but let the terminating
                        // double quotation mark we add below be interpreted
                        // as a metacharacter.
                        CommandLine.Append(new String('\\', NumberBackslashes * 2));
                        break;
                    }
                    else if (Argument[i] == '"')
                    {
                        // Escape all backslashes and the following
                        // double quotation mark.
                        CommandLine.Append(new String('\\', NumberBackslashes * 2 + 1));
                        CommandLine.Append(Argument[i]);
                    }
                    else
                    {
                        // Backslashes aren't special here.
                        CommandLine.Append(new String('\\', NumberBackslashes));
                        CommandLine.Append(Argument[i]);
                    }
                }

                CommandLine.Append('"');
            }
            return CommandLine.ToString();
        }
        
        
        public static string EscapeShellArg(string Argument)
        {
            StringBuilder CommandLine = new StringBuilder(Argument.Length * 2);

            if (!String.IsNullOrEmpty(Argument) && Argument.IndexOfAny(new char[] { ' ', '\t', '\n', '\v', '"' }) == -1)
                return Argument;

            CommandLine.Append('"');

            for (int i = 0; i < Argument.Length; i++)
            {
                char c = Argument[i];
                if (c == '\\' && (i + 1 >= Argument.Length || Argument[i + 1] == '"'))
                    CommandLine.Append("\\"+c);
                else if (c == '"')
                    CommandLine.Append("\\"+c);
                else
                    CommandLine.Append(c);
            }

            CommandLine.Append('"');

            System.Diagnostics.Debug.Assert(CommandLine.ToString() == ArgvQuote(Argument, false), String.Format("{0} != {1}", CommandLine.ToString(), ArgvQuote(Argument, false)));

            return CommandLine.ToString();
        }

        public static string CmdQuote(string Argument)
        {
            StringBuilder CommandLine = new StringBuilder(Argument.Length * 2);

            if (!String.IsNullOrEmpty(Argument) && Argument.IndexOfAny(new char[] {' ','(', ')', '%', '!', '^', '"', '<', '>', '&', '|' }) == -1)
                return Argument;

            CommandLine.Append("^\"");

            foreach (char c in Argument)
                switch (c)
                {
                    case '(':
                    case ')':
                    case '%':
                    case '!':
                    case '^':
                    case '"':
                    case '<':
                    case '>':
                    case '&':
                    case '|':
                        CommandLine.Append("^");
                        CommandLine.Append(c);
                        break;
                    default:
                        CommandLine.Append(c);
                        break;
                }

            CommandLine.Append("^\"");
            return CommandLine.ToString();
        }
    }


}
