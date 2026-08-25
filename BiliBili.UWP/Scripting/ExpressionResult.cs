using System.Collections.Generic;

namespace scripting
{
    public class ExpressionResult
    {
        public string type;

        public object value;

        public bool isLeftHandSide = false;

        public ExpressionResult()
        {
            this.initialize();
        }

        public static ExpressionResult createLiteral(object param1)
        {
            ExpressionResult loc2 = new ExpressionResult();
            loc2.setTypeLiteral(param1);
            return loc2;
        }

        public static ExpressionResult createStack()
        {
            ExpressionResult loc1 = new ExpressionResult();
            loc1.setTypeStack();
            return loc1;
        }

        public ExpressionResult clone()
        {
            ExpressionResult loc1 = new ExpressionResult();
            loc1.setTypeAndValue(this.type, this.value);
            return loc1;
        }

        public object initialize()
        {
            this.type = "empty";
            this.value = null;
            return null;
        }

        public object setType(string param1)
        {
            this.type = param1;
            return null;
        }

        public bool isType(string param1)
        {
            return this.type == param1;
        }

        public object setValue(object param1)
        {
            this.value = param1;
            return null;
        }

        public object setTypeAndValue(string param1, object param2)
        {
            this.type = param1;
            this.value = param2;
            return null;
        }

        public bool isLiteral()
        {
            return this.isType("literal");
        }

        public bool isVariableOrMember()
        {
            return this.isVariable() || this.isMember();
        }

        public bool isVariable()
        {
            return this.isType("variable");
        }

        public bool isMember()
        {
            return this.isType("member");
        }

        public object setTypeStack()
        {
            this.setType("stack");
            return null;
        }

        public object setTypeLiteral(object param1)
        {
            this.setTypeAndValue("literal", param1);
            return null;
        }

        public object setTypeMember(ExpressionResult param1, ExpressionResult param2)
        {
            Dictionary<string, object> value = new Dictionary<string, object>();
            value["object"] = param1;
            value["member"] = param2;
            this.setTypeAndValue("member", value);
            return null;
        }

        public ExpressionResult getObjectExpression()
        {
            if (!this.isMember())
            {
                return null;
            }
            return ((Dictionary<string, object>)this.value)["object"] as ExpressionResult;
        }

        public ExpressionResult getMemberExpression()
        {
            if (!this.isMember())
            {
                return null;
            }
            return ((Dictionary<string, object>)this.value)["member"] as ExpressionResult;
        }
    }
}
