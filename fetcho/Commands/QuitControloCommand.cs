using System;
using System.Threading.Tasks;

namespace Fetcho.Commands
{
    public class QuitControloCommand : ControloCommand
    {
        public override string CommandName => "quit";

        public override string ShortHelp => "Exit the program hard"; 

        public override async Task Execute(string[] args)
        {
            Controlo.ReportInfo("Quitting");
            Controlo.Shutdown();
            //Environment.Exit(1);
        }
    }
}
