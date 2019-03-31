using System;

namespace Fetcho.Commands
{
    public class QuitControloCommand : ControloCommand
    {
        public override string CommandName => "quit";

        public override string ShortHelp => "Exit the program hard"; 

        public override void Execute(Controlo controlo, string[] args)
        {
            controlo.ReportInfo("Quitting");
            Environment.Exit(1);
        }
    }
}
