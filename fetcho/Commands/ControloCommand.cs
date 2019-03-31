namespace Fetcho.Commands
{
    public abstract class ControloCommand
    {
        public abstract string CommandName { get; }

        public virtual string ShortHelp { get => "Shorthelp not set"; }

        public abstract void Execute(Controlo controlo, string[] args);
    }
}
