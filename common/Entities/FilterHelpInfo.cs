namespace Fetcho.Common.Entities
{
    public class FilterHelpInfo
    {
        public string TokenMatch { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ShortHelp { get; set; }
        public string LongHelp { get; set; }
        public decimal DefaultCost { get; set; }

        public FilterHelpInfo(FilterAttribute attribute)
        {
            TokenMatch = attribute.TokenMatch;
            ShortHelp = attribute.ShortHelp;
            Name = attribute.Name;
            Description = attribute.Description;
        }
    }
}
