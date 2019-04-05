namespace Fetcho.Commands
{
    public class HelpControloCommand : ControloCommand
    {
        public override string CommandName => "help";

        public override string ShortHelp => "List all commands";

        public override void Execute(string[] args)
        {
            foreach( var command in Controlo.Commands.Values )
            {
                Controlo.ReportInfo("{0,10}\t{1}", command.CommandName, command.ShortHelp);
            }
        }
    }
}
