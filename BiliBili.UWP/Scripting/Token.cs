namespace scripting
{
    public class Token
    {
        public string type;
        public object value;

        public Token(string param1, object param2)
        {
            this.type = param1;
            this.value = param2;
        }
    }
}
