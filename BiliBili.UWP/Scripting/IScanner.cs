namespace scripting
{
    public interface IScanner
    {
        object rewind();

        Token getToken();

        double getLineNumber();

        string getLine();
    }
}
