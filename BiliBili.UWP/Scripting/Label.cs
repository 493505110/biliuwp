namespace scripting
{
    public class Label
    {
        public object address;
        public bool isExists;

        public Label()
        {
            this.initialize();
        }

        public object initialize()
        {
            this.address = null;
            this.isExists = false;
            return null;
        }

        public object commitAddress(object param1)
        {
            this.address = param1;
            this.isExists = true;
            return null;
        }
    }
}
