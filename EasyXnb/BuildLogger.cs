using Microsoft.Xna.Framework.Content.Pipeline;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace xnbcompiler.EasyXnb
{
    public sealed class BuildLogger : ContentBuildLogger
    {
        public override void LogMessage(string message, params object[] messageArgs) 
        {
            Console.WriteLine("");
            Console.WriteLine("Log: " + message, messageArgs);
        }

        public override void LogImportantMessage(string message, params object[] messageArgs) 
        {
            Console.WriteLine("");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Log Important: " + message, messageArgs);
        }

        public override void LogWarning(string helpLink, ContentIdentity contentIdentity, string message, params object[] messageArgs) 
        {
            Console.WriteLine("");
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("Warning: " + message + ", at: ", messageArgs);
            Console.WriteLine("Warning type: " + helpLink);
            Console.WriteLine("Source file name: " + contentIdentity.SourceFilename);
            Console.WriteLine("Source tool: " + contentIdentity.SourceTool);
            Console.WriteLine("Fragment identifier: " + contentIdentity.FragmentIdentifier);
        }
    }
}
