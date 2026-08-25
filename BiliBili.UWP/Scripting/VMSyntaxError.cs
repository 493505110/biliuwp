using System;

namespace scripting
{
    public class VMSyntaxError : Exception
    {
        public string name;

        public VMSyntaxError(string param1)
            : base(param1)
        {
            this.name = "VMSyntaxError";
        }
    }
}
