using System;
using System.Collections.Generic;

namespace scripting
{
    public class CodeGenerator
    {

        private static readonly Dictionary<string, System.Reflection.MethodInfo> _opCache = new Dictionary<string, System.Reflection.MethodInfo>();

        /// <summary>Returns a bound method delegate for an opcode (optimized mode), or the op name string (normal mode).</summary>
        public static object VmOp(VirtualMachine vm, string name)
        {
            if (!_opCache.TryGetValue(name, out var mi))
            {
                mi = typeof(VirtualMachine).GetMethod(name, new[] { typeof(List<object>), typeof(double) });
                _opCache[name] = mi;
            }
            return mi.CreateDelegate(typeof(Func<List<object>, double, object>), vm);
        }

        private List<object> code;

        private List<object> stackList;

        private List<object> localVariableList;

        public VirtualMachine vmtarget;

        public CodeGenerator()
        {
            this.initialize();
        }

        public object initialize()
        {
            this.code = new List<object>();
            this.stackList = new List<object>();
            this.localVariableList = new List<object>();
            this.put(null);
            this.beginNewScope();
            return null;
        }

        private object error(string param1)
        {
            throw new Exception("CodeGenerator [error] " + param1);
        }

        public List<object> getCode()
        {
            return this.code;
        }

        public double put(object param1)
        {
            this.code.Add(param1);
            return this.code.Count - 1;
        }

        public object putStoreStack()
        {
            this.stackList.Add(this.put(null));
            return null;
        }

        public object putLoadStack()
        {
            object loc3;
            List<object> loc1 = this.code;
            double loc2 = this.popStack();
            double loc4 = this.put(null);
            while (true)
            {
                loc3 = loc1[Convert.ToInt32(loc2)];
                loc1[Convert.ToInt32(loc2)] = loc4;
                if (loc3 == null)
                {
                    break;
                }
                loc2 = Convert.ToDouble(loc3);
            }
            return null;
        }

        public double popStack()
        {
            int index = this.stackList.Count - 1;
            object value = this.stackList[index];
            this.stackList.RemoveAt(index);
            return Convert.ToDouble(value);
        }

        public object pushStack(object param1)
        {
            this.stackList.Add(param1);
            return null;
        }

        public object setStackPatch(object param1)
        {
            int index = Convert.ToInt32(this.stackList[this.stackList.Count - 1]);
            if (this.code[index] != null)
            {
                this.setStackPatchRecursive(this.code[index], param1);
            }
            else
            {
                this.code[index] = param1;
            }
            return null;
        }

        public object setStackPatchRecursive(object param1, object param2)
        {
            int index = Convert.ToInt32(param1);
            if (this.code[index] == null)
            {
                this.code[index] = param2;
            }
            else
            {
                this.setStackPatchRecursive(this.code[index], param2);
            }
            return null;
        }

        public object putCrossLoadStack()
        {
            this.swapStack();
            this.putLoadStack();
            this.putLoadStack();
            return null;
        }

        public object swapStack(object param1 = null, object param2 = null)
        {
            int index1 = this.stackList.Count - (param1 != null ? Convert.ToInt32(param1) : 0) - 1;
            int index2 = this.stackList.Count - (param2 != null ? Convert.ToInt32(param2) : 1) - 1;
            object loc3 = this.stackList[index1];
            this.stackList[index1] = this.stackList[index2];
            this.stackList[index2] = loc3;
            return null;
        }

        public double getStackLength()
        {
            return this.stackList.Count;
        }

        public object cleanUpStack(object param1 = null)
        {
            if (param1 == null)
            {
                param1 = 0;
            }
            List<object> loc2 = this.stackList;
            while (loc2.Count > Convert.ToDouble(param1))
            {
                this.popAndDestroyStack();
            }
            return null;
        }

        public object popAndDestroyStack()
        {
            object loc3;
            List<object> loc1 = this.code;
            double loc2 = this.popStack();
            while (true)
            {
                loc3 = loc1[Convert.ToInt32(loc2)];
                loc1[Convert.ToInt32(loc2)] = 0;
                if (loc3 == null)
                {
                    break;
                }
                loc2 = Convert.ToDouble(loc3);
            }
            return null;
        }

        public object putLabel(Label param1)
        {
            if (param1.isExists)
            {
                this.put(param1.address);
            }
            else
            {
                param1.address = this.put(param1.address);
            }
            return null;
        }

        public object setLabel(Label param1)
        {
            object loc2 = this.code.Count;
            this.setLabelAddress(param1, loc2);
            param1.commitAddress(loc2);
            return null;
        }

        public object setLabelAddress(Label param1, object param2)
        {
            object loc5;
            List<object> loc3 = this.code;
            object loc4 = param1.address;
            while (loc4 != null)
            {
                int index = Convert.ToInt32(loc4);
                loc5 = loc3[index];
                loc3[index] = param2;
                loc4 = loc5;
            }
            return null;
        }

        public object beginNewScope()
        {
            this.localVariableList.Insert(0, new Dictionary<string, object>());
            return null;
        }

        public object closeScope()
        {
            this.localVariableList.RemoveAt(0);
            return null;
        }

        public bool isLocalVariable(object param1)
        {
            return ((Dictionary<string, object>)this.localVariableList[0]).ContainsKey(Convert.ToString(param1));
        }

        public object addLocalVariable(object param1)
        {
            ((Dictionary<string, object>)this.localVariableList[0])[Convert.ToString(param1)] = true;
            return null;
        }

        public object putExpressionResult(ExpressionResult param1)
        {
            switch (param1.type)
            {
                case "variable":
                    this.putGetVariable(Convert.ToString(param1.value));
                    param1.setType("stack");
                    break;
                case "member":
                    this.putGetMember(param1.getObjectExpression(), param1.getMemberExpression());
                    param1.setType("stack");
                    break;
            }
            return null;
        }

        private object putValue(ExpressionResult param1)
        {
            switch (param1.type)
            {
                case "literal":
                    this.put(param1.value);
                    break;
                case "stack":
                    this.putLoadStack();
                    break;
                default:
                    this.error("putValueError");
                    break;
            }
            return null;
        }

        private object putBinaryValue(ExpressionResult param1, ExpressionResult param2)
        {
            if (param1.isType("literal") && param2.isType("literal"))
            {
                this.put(param1.value);
                this.put(param2.value);
            }
            else if (param1.isType("stack") && param2.isType("stack"))
            {
                this.putCrossLoadStack();
            }
            else if (param1.isType("stack"))
            {
                this.putLoadStack();
                this.put(param2.value);
            }
            else if (param2.isType("stack"))
            {
                this.put(param1.value);
                this.putLoadStack();
            }
            else
            {
                this.error("putBinaryValueError");
            }
            return null;
        }

        public object putSuspend()
        {
            this.put(this.vmtarget == null ? "SPD" : CodeGenerator.VmOp(this.vmtarget, "SPD"));
            return null;
        }

        public object putLiteral(ExpressionResult param1)
        {
            this.put(this.vmtarget == null ? "LIT" : CodeGenerator.VmOp(this.vmtarget, "LIT"));
            this.put(param1.value);
            this.putStoreStack();
            return null;
        }

        public object putCall(ExpressionResult param1, object param2)
        {
            if (this.isLocalVariable(param1.value))
            {
                this.put(this.vmtarget == null ? "CALLL" : CodeGenerator.VmOp(this.vmtarget, "CALLL"));
            }
            else
            {
                this.put(this.vmtarget == null ? "CALL" : CodeGenerator.VmOp(this.vmtarget, "CALL"));
            }
            this.put(param1.value);
            this.put(param2);
            this.putStoreStack();
            return null;
        }

        public object putCallMember(ExpressionResult param1, ExpressionResult param2, object param3)
        {
            this.put(this.vmtarget == null ? "CALLM" : CodeGenerator.VmOp(this.vmtarget, "CALLM"));
            this.putBinaryValue(param1, param2);
            this.put(param3);
            this.putStoreStack();
            return null;
        }

        public object putCallFunctor(object param1)
        {
            this.put(this.vmtarget == null ? "CALLF" : CodeGenerator.VmOp(this.vmtarget, "CALLF"));
            this.putLoadStack();
            this.put(param1);
            this.putStoreStack();
            return null;
        }

        public object putReturnFunction(ExpressionResult param1)
        {
            this.put(this.vmtarget == null ? "RET" : CodeGenerator.VmOp(this.vmtarget, "RET"));
            this.putValue(param1);
            return null;
        }

        public object putReturnCoroutine(ExpressionResult param1)
        {
            this.put(this.vmtarget == null ? "CRET" : CodeGenerator.VmOp(this.vmtarget, "CRET"));
            this.putValue(param1);
            return null;
        }

        public Label putFunction()
        {
            Label loc1 = new Label();
            this.put(this.vmtarget == null ? "FUNC" : CodeGenerator.VmOp(this.vmtarget, "FUNC"));
            this.putLabel(loc1);
            this.putStoreStack();
            return loc1;
        }

        public Label putCoroutine()
        {
            Label loc1 = new Label();
            this.put(this.vmtarget == null ? "COR" : CodeGenerator.VmOp(this.vmtarget, "COR"));
            this.putLabel(loc1);
            this.putStoreStack();
            return loc1;
        }

        public object putArgument(object param1, string param2)
        {
            this.put(this.vmtarget == null ? "ARG" : CodeGenerator.VmOp(this.vmtarget, "ARG"));
            this.put(param1);
            this.put(param2);
            this.addLocalVariable(param2);
            return null;
        }

        public object putJump(Label param1)
        {
            this.put(this.vmtarget == null ? "JMP" : CodeGenerator.VmOp(this.vmtarget, "JMP"));
            this.putLabel(param1);
            return null;
        }

        public object putIf(ExpressionResult param1, Label param2)
        {
            this.put(this.vmtarget == null ? "IF" : CodeGenerator.VmOp(this.vmtarget, "IF"));
            this.putValue(param1);
            this.putLabel(param2);
            return null;
        }

        public object putNif(ExpressionResult param1, Label param2)
        {
            this.put(this.vmtarget == null ? "NIF" : CodeGenerator.VmOp(this.vmtarget, "NIF"));
            this.putValue(param1);
            this.putLabel(param2);
            return null;
        }

        public object putBinaryOperation(object param1, ExpressionResult param2, ExpressionResult param3)
        {
            this.put(param1);
            this.putBinaryValue(param2, param3);
            this.putStoreStack();
            return null;
        }

        public object putUnaryOperation(object param1, ExpressionResult param2)
        {
            this.put(param1);
            this.putValue(param2);
            this.putStoreStack();
            return null;
        }

        public object putIncrement(ExpressionResult param1)
        {
            this.putIncDec(this.vmtarget == null ? "INC" : CodeGenerator.VmOp(this.vmtarget, "INC"), false, param1);
            return null;
        }

        public object putDecrement(ExpressionResult param1)
        {
            this.putIncDec(this.vmtarget == null ? "DEC" : CodeGenerator.VmOp(this.vmtarget, "DEC"), false, param1);
            return null;
        }

        public object putPostfixIncrement(ExpressionResult param1)
        {
            this.putIncDec(this.vmtarget == null ? "INC" : CodeGenerator.VmOp(this.vmtarget, "INC"), true, param1);
            return null;
        }

        public object putPostfixDecrement(ExpressionResult param1)
        {
            this.putIncDec(this.vmtarget == null ? "DEC" : CodeGenerator.VmOp(this.vmtarget, "DEC"), true, param1);
            return null;
        }

        private object putIncDec(object param1, bool param2, ExpressionResult param3)
        {
            object loc4;
            ExpressionResult loc5;
            ExpressionResult loc6;
            string loc7;
            switch (param3.type)
            {
                case "member":
                    loc5 = param3.getObjectExpression();
                    loc6 = param3.getMemberExpression();
                    if (!loc6.isLiteral())
                    {
                        this.putExpressionResult(loc6);
                        loc4 = this.popStack();
                        this.putDuplicate(loc6);
                        this.pushStack(loc4);
                        this.putDuplicate(loc5);
                        this.swapStack(1, 2);
                    }
                    else
                    {
                        this.putDuplicate(loc5);
                    }
                    this.putGetMember(loc5, loc6);
                    param3.setTypeStack();
                    if (param2)
                    {
                        this.putDuplicate(param3);
                        loc4 = this.popStack();
                        this.putUnaryOperation(param1, param3);
                        this.putSetMember(loc5, loc6, param3);
                        this.popAndDestroyStack();
                        this.pushStack(loc4);
                    }
                    else
                    {
                        this.putUnaryOperation(param1, param3);
                        this.putSetMember(loc5, loc6, param3);
                    }
                    break;
                case "variable":
                    loc7 = Convert.ToString(param3.value);
                    this.putGetVariable(loc7);
                    param3.setTypeStack();
                    if (param2)
                    {
                        this.putDuplicate(param3);
                    }
                    this.putUnaryOperation(param1, param3);
                    this.putSetVariable(loc7, param3);
                    if (param2)
                    {
                        this.popAndDestroyStack();
                    }
                    break;
                default:
                    this.error("putIncDecError");
                    break;
            }
            return null;
        }

        public object putWith(ExpressionResult param1)
        {
            this.put(this.vmtarget == null ? "WITH" : CodeGenerator.VmOp(this.vmtarget, "WITH"));
            this.putValue(param1);
            return null;
        }

        public object putEndWith()
        {
            this.put(this.vmtarget == null ? "EWITH" : CodeGenerator.VmOp(this.vmtarget, "EWITH"));
            return null;
        }

        public object putPush(ExpressionResult param1)
        {
            this.put(this.vmtarget == null ? "PUSH" : CodeGenerator.VmOp(this.vmtarget, "PUSH"));
            this.putValue(param1);
            return null;
        }

        public object putPop()
        {
            this.put(this.vmtarget == null ? "POP" : CodeGenerator.VmOp(this.vmtarget, "POP"));
            this.putStoreStack();
            return null;
        }

        public object putDuplicate(ExpressionResult param1)
        {
            this.put(this.vmtarget == null ? "DUP" : CodeGenerator.VmOp(this.vmtarget, "DUP"));
            this.putValue(param1);
            this.putStoreStack();
            this.putStoreStack();
            return null;
        }

        public object putThis()
        {
            this.put(this.vmtarget == null ? "THIS" : CodeGenerator.VmOp(this.vmtarget, "THIS"));
            this.putStoreStack();
            return null;
        }

        public object putArrayLiteral(object param1)
        {
            this.put(this.vmtarget == null ? "ARRAY" : CodeGenerator.VmOp(this.vmtarget, "ARRAY"));
            this.put(param1);
            this.putStoreStack();
            return null;
        }

        public object putObjectLiteral(object param1)
        {
            this.put(this.vmtarget == null ? "OBJ" : CodeGenerator.VmOp(this.vmtarget, "OBJ"));
            this.put(param1);
            this.putStoreStack();
            return null;
        }

        public object putGetVariable(string param1)
        {
            if (this.isLocalVariable(param1))
            {
                this.put(this.vmtarget == null ? "GETL" : CodeGenerator.VmOp(this.vmtarget, "GETL"));
            }
            else
            {
                this.put(this.vmtarget == null ? "GET" : CodeGenerator.VmOp(this.vmtarget, "GET"));
            }
            this.put(param1);
            this.putStoreStack();
            return null;
        }

        public object putSetVariable(string param1, ExpressionResult param2)
        {
            if (this.isLocalVariable(param1))
            {
                this.put(this.vmtarget == null ? "SETL" : CodeGenerator.VmOp(this.vmtarget, "SETL"));
            }
            else
            {
                this.put(this.vmtarget == null ? "SET" : CodeGenerator.VmOp(this.vmtarget, "SET"));
            }
            this.put(param1);
            this.putValue(param2);
            this.putStoreStack();
            return null;
        }

        public object putSetLocalVariable(string param1, ExpressionResult param2)
        {
            this.put(this.vmtarget == null ? "SETL" : CodeGenerator.VmOp(this.vmtarget, "SETL"));
            this.put(param1);
            this.putValue(param2);
            this.putStoreStack();
            this.addLocalVariable(param1);
            return null;
        }

        public object putGetMember(ExpressionResult param1, ExpressionResult param2)
        {
            if (param2.isType("variable"))
            {
                this.put(this.vmtarget == null ? "GETMV" : CodeGenerator.VmOp(this.vmtarget, "GETMV"));
                if (param1.isType("literal"))
                {
                    this.put(param1.value);
                }
                else
                {
                    this.putLoadStack();
                }
                if (this.isLocalVariable(param2.value))
                {
                    this.put(this.vmtarget == null ? "GETL" : CodeGenerator.VmOp(this.vmtarget, "GETL"));
                }
                else
                {
                    this.put(this.vmtarget == null ? "GET" : CodeGenerator.VmOp(this.vmtarget, "GET"));
                }
                this.put(param2.value);
                this.putStoreStack();
            }
            else
            {
                this.put(this.vmtarget == null ? "GETM" : CodeGenerator.VmOp(this.vmtarget, "GETM"));
                this.putBinaryValue(param1, param2);
                this.putStoreStack();
            }
            return null;
        }

        public object putSetMember(ExpressionResult param1, ExpressionResult param2, ExpressionResult param3)
        {
            this.put(this.vmtarget == null ? "SETM" : CodeGenerator.VmOp(this.vmtarget, "SETM"));
            if (param1.isLiteral())
            {
                if (param2.isLiteral())
                {
                    if (param3.isLiteral())
                    {
                        this.put(param1.value);
                        this.put(param2.value);
                        this.put(param3.value);
                    }
                    else
                    {
                        this.put(param1.value);
                        this.put(param2.value);
                        this.putLoadStack();
                    }
                }
                else if (param3.isLiteral())
                {
                    this.put(param1.value);
                    this.putLoadStack();
                    this.put(param3.value);
                }
                else
                {
                    this.put(param1.value);
                    this.putCrossLoadStack();
                }
            }
            else if (param2.isLiteral())
            {
                if (param3.isLiteral())
                {
                    this.putLoadStack();
                    this.put(param2.value);
                    this.put(param3.value);
                }
                else
                {
                    this.swapStack();
                    this.putLoadStack();
                    this.put(param2.value);
                    this.putLoadStack();
                }
            }
            else if (param3.isLiteral())
            {
                this.putCrossLoadStack();
                this.put(param3.value);
            }
            else
            {
                this.swapStack(0, 2);
                this.putLoadStack();
                this.putLoadStack();
                this.putLoadStack();
            }
            this.putStoreStack();
            return null;
        }

        public object putNew(object param1)
        {
            this.put(this.vmtarget == null ? "NEW" : CodeGenerator.VmOp(this.vmtarget, "NEW"));
            this.putLoadStack();
            this.put(param1);
            this.putStoreStack();
            return null;
        }

        public object putDelete(ExpressionResult param1)
        {
            if (param1.isType("variable") || param1.isType("literal"))
            {
                if (this.isLocalVariable(param1.value))
                {
                    this.put(this.vmtarget == null ? "DELL" : CodeGenerator.VmOp(this.vmtarget, "DELL"));
                }
                else
                {
                    this.put(this.vmtarget == null ? "DEL" : CodeGenerator.VmOp(this.vmtarget, "DEL"));
                }
                this.put(param1.value);
                this.putStoreStack();
            }
            else
            {
                this.put(this.vmtarget == null ? "DEL" : CodeGenerator.VmOp(this.vmtarget, "DEL"));
                this.putLoadStack();
                this.putStoreStack();
            }
            return null;
        }

        public object putDeleteMember(ExpressionResult param1, ExpressionResult param2)
        {
            this.put(this.vmtarget == null ? "DELM" : CodeGenerator.VmOp(this.vmtarget, "DELM"));
            this.putBinaryValue(param1, param2);
            this.putStoreStack();
            return null;
        }
    }
}
