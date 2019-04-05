namespace Fetcho.Commands
{
    public abstract class ControloCommand
    {
        public Controlo Controlo { get; set;  }

        public abstract string CommandName { get; }

        public virtual string ShortHelp { get => "Shorthelp not set"; }

        public abstract void Execute(string[] args);
    }
}
