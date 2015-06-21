using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eldo.Escaping
{
    static class Nix
    {
        public static string EscapeShellArg(string str)
        {
            StringBuilder o = new StringBuilder(str.Length * 2);
            o.Append("'");
            char c;
            for (int i = 0; i < str.Length; i++)
                switch (c = str[i])
                {
                    case '\'':
                        o.Append(@"\" + c);
                        break;
                    default:
                        o.Append(c);
                        break;
                }
            o.Append("'");
            return o.ToString();
        }
    }
}
