namespace Fetcho.Commands
{
    public class HelpControloCommand : ControloCommand
    {
        public override string CommandName => "help";

        public override string ShortHelp => "List all commands";

        public override void Execute(Controlo controlo, string[] args)
        {
            foreach( var command in controlo.Commands.Values )
            {
                controlo.ReportInfo("{0,10}\t{1}", command.CommandName, command.ShortHelp);
            }
        }
    }
}
