
namespace Fetcho.Common
{
    public interface IFilter
    {
        string Name { get;  }
        MatchAction MatchAction { get; set; }

        bool IsMatch(string fragment);
        string GetTagName();
    }
}
