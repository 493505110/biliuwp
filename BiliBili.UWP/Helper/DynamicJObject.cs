using Newtonsoft.Json.Linq;

namespace BiliBili.UWP
{
    public class ReturnJObject
    {
        public int code { get; set; }
        public string message { get; set; }
        public string msg { get; set; }
        public JToken json { get; set; }
    }
}
