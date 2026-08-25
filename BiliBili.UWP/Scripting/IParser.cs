using System.Collections.Generic;

namespace scripting
{
    public interface IParser
    {
        List<object> parse(object param1 = null);
    }
}
