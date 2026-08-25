using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace scripting
{
    /// <summary>
    /// Stack-machine virtual machine executing bytecode produced by CodeGenerator/Parser.
    /// Direct C# port of the AS3 scripting.VirtualMachine (Bilibili M8 danmaku engine).
    /// Instructions are stored in byteCode either as op-name strings (default) or
    /// as bound method delegates (optimized mode, when parser was given a vm target).
    /// The byteCode array doubles as the register file: instruction results are written
    /// back into code slots (indexed by operand values), mirroring the AS3 semantics.
    /// </summary>
    public class VirtualMachine
    {
        private List<object> byteCode;
        private int byteCodeLength;
        private int programCounter;
        private object global;
        private Dictionary<string, object> localObject;
        private object thisObject;
        private object returnValue;
        private List<object> stack;

        public bool optimized = false;

        public VirtualMachine()
        {
            this.initialize();
        }

        public void initialize()
        {
            this.programCounter = 1;
            this.byteCode = new List<object>();
            this.stack = new List<object>();
            this.global = new Dictionary<string, object>();
            SetObj(this.global as Dictionary<string, object>, "__scope", null);
            this.localObject = this.global as Dictionary<string, object>;
            this.thisObject = this.global;
        }

        public void rewind()
        {
            this.programCounter = 1;
        }

        public void setProgramCounter(object param1)
        {
            this.programCounter = ToIndex(param1);
        }

        public object runCoroutine(object param1, List<object> param2 = null)
        {
            List<object> loc3 = param2 ?? new List<object>();
            var g = this.global as Dictionary<string, object>;
            object entry = null, scopeObj = null;
            if (g != null && g.TryGetValue(As3String(param1), out var coro) && coro is Dictionary<string, object> cd)
            {
                cd.TryGetValue("__entryPoint", out entry);
                cd.TryGetValue("__scope", out scopeObj);
            }
            if (entry == null) return null;
            return this.executeFunction(new Dictionary<string, object>(), loc3,
                ToIndex(entry), scopeObj as Dictionary<string, object>);
        }

        public object getGlobalObject()
        {
            return this.global;
        }

        public object getLocalObject()
        {
            return this.localObject;
        }

        public void setByteCode(List<object> param1)
        {
            this.byteCode = param1;
            this.byteCodeLength = param1.Count;
        }

        public List<object> getByteCode()
        {
            return this.byteCode;
        }

        public int getByteCodeLength()
        {
            return this.byteCodeLength;
        }

        public bool execute()
        {
            List<object> loc1 = this.byteCode;
            int loc2 = (int)this.programCounter;
            int loc3 = this.byteCodeLength;
            if (this.optimized)
            {
                while (loc2 < loc3)
                {
                    object loc4;
                    try
                    {
                        loc4 = ((Delegate)loc1[loc2]).DynamicInvoke(loc1, (double)loc2);
                    }
                    catch (TargetInvocationException tie)
                    {
                        // DynamicInvoke wraps exceptions; surface the real one
                        // (M8StopException from stopExecution() is control flow, not an error).
                        if (tie.InnerException != null) throw tie.InnerException;
                        throw;
                    }
                    if (loc4 == null)
                    {
                        break;
                    }
                    loc2 = ToIndex(loc4);
                }
            }
            else
            {
                while (loc2 < loc3)
                {
                    object loc4 = this.Dispatch((string)loc1[loc2], loc1, loc2);
                    if (loc4 == null)
                    {
                        break;
                    }
                    loc2 = ToIndex(loc4);
                }
            }
            if (loc2 >= loc3)
            {
                // Reached the end of the bytecode: normal completion.
                this.programCounter = loc2;
                loc1[0] = null;
                return false;
            }
            // Interrupted by an explicit stop (SPD / coroutine suspension).
            this.programCounter = loc2 + 1;
            loc1[0] = null;
            return true;
        }

        private object executeFunction(object param1, List<object> param2, int param3, Dictionary<string, object> param4)
        {
            object loc6 = this.thisObject;
            Dictionary<string, object> loc7 = this.localObject;
            int loc8 = this.programCounter;
            this.thisObject = param1;
            this.localObject = new Dictionary<string, object>
            {
                { "arguments", param2 },
                { "__scope", param4 }
            };
            this.programCounter = param3;
            while (this.execute())
            {
            }
            this.programCounter = loc8;
            this.localObject = loc7;
            this.thisObject = loc6;
            return this.returnValue;
        }

        private object __resolve(string param1)
        {
            throw new Exception("VirtualMachine [UnknownOperation] : " + param1);
        }

        private object Dispatch(string op, List<object> code, int pc)
        {
            switch (op)
            {
                case "NOP": return NOP(code, pc);
                case "SPD": return SPD(code, pc);
                case "LIT": return LIT(code, pc);
                case "CALL": return CALL(code, pc);
                case "CALLL": return CALLL(code, pc);
                case "CALLM": return CALLM(code, pc);
                case "CALLF": return CALLF(code, pc);
                case "RET": return RET(code, pc);
                case "CRET": return CRET(code, pc);
                case "FUNC": return FUNC(code, pc);
                case "COR": return COR(code, pc);
                case "ARG": return ARG(code, pc);
                case "JMP": return JMP(code, pc);
                case "IF": return IF(code, pc);
                case "NIF": return NIF(code, pc);
                case "ADD": return ADD(code, pc);
                case "SUB": return SUB(code, pc);
                case "MUL": return MUL(code, pc);
                case "DIV": return DIV(code, pc);
                case "MOD": return MOD(code, pc);
                case "AND": return AND(code, pc);
                case "OR": return OR(code, pc);
                case "XOR": return XOR(code, pc);
                case "NOT": return NOT(code, pc);
                case "LNOT": return LNOT(code, pc);
                case "LSH": return LSH(code, pc);
                case "RSH": return RSH(code, pc);
                case "URSH": return URSH(code, pc);
                case "INC": return INC(code, pc);
                case "DEC": return DEC(code, pc);
                case "CEQ": return CEQ(code, pc);
                case "CSEQ": return CSEQ(code, pc);
                case "CNE": return CNE(code, pc);
                case "CSNE": return CSNE(code, pc);
                case "CLT": return CLT(code, pc);
                case "CGT": return CGT(code, pc);
                case "CLE": return CLE(code, pc);
                case "CGE": return CGE(code, pc);
                case "DUP": return DUP(code, pc);
                case "THIS": return THIS(code, pc);
                case "ARRAY": return ARRAY(code, pc);
                case "OBJ": return OBJ(code, pc);
                case "SETL": return SETL(code, pc);
                case "GETL": return GETL(code, pc);
                case "SET": return SET(code, pc);
                case "GET": return GET(code, pc);
                case "SETM": return SETM(code, pc);
                case "GETM": return GETM(code, pc);
                case "GETMV": return GETMV(code, pc);
                case "NEW": return NEW(code, pc);
                case "DEL": return DEL(code, pc);
                case "DELL": return DELL(code, pc);
                case "DELM": return DELM(code, pc);
                case "TYPEOF": return TYPEOF(code, pc);
                case "INSOF": return INSOF(code, pc);
                case "NUM": return NUM(code, pc);
                case "STR": return STR(code, pc);
                case "WITH": return WITH(code, pc);
                case "EWITH": return EWITH(code, pc);
                case "PUSH": return PUSH(code, pc);
                case "POP": return POP(code, pc);
                default:
                    throw new Exception("VirtualMachine [UnknownOperation] : " + op);
            }
        }

        // ---------------------------------------------------------------- opcodes

        public object NOP(List<object> param1, double param2)
        {
            return param2 + 1;
        }

        public object SPD(List<object> param1, double param2)
        {
            return null;
        }

        public object LIT(List<object> param1, double param2)
        {
            // code[code[pc+2]] = code[pc+1]
            SetSlot(param1, GetI(param1, (int)param2 + 2), GetSlot(param1, (int)param2 + 1));
            return param2 + 3;
        }

        public object CALL(List<object> param1, double param2)
        {
            string loc4 = As3String(GetSlot(param1, (int)param2 + 1));
            object loc5 = null;
            Dictionary<string, object> loc6 = this.localObject;
            while (loc6 != null)
            {
                if (loc6.TryGetValue(loc4, out loc5))
                {
                    break;
                }
                loc6 = ScopeOf(loc6);
            }
            int loc7 = GetI(param1, (int)param2 + 2) + 1;
            List<object> loc8 = this.stack;
            List<object> loc9 = new List<object>();
            while (--loc7 > 0)
            {
                loc9.Add(loc8[loc8.Count - 1]);
                loc8.RemoveAt(loc8.Count - 1);
            }
            loc9.Reverse();
            if (loc5 is Dictionary<string, object> cd && cd.ContainsKey("__entryPoint"))
            {
                loc8.Add((double)(param2 + 4));
                loc8.Add(this.thisObject);
                loc8.Add(this.localObject);
                loc8.Add(GetSlot(param1, (int)param2 + 3));
                this.thisObject = this.global;
                this.localObject = new Dictionary<string, object>
                {
                    { "arguments", loc9 },
                    { "__scope", cd["__scope"] }
                };
                return (double)ToNumber(cd["__entryPoint"]);
            }
            SetSlot(param1, GetI(param1, (int)param2 + 3), CallFunction(loc5, this.global, loc9));
            return param2 + 4;
        }

        public object CALLL(List<object> param1, double param2)
        {
            object loc4 = this.localObject != null && this.localObject.TryGetValue(As3String(GetSlot(param1, (int)param2 + 1)), out var v4) ? v4 : null;
            int loc5 = GetI(param1, (int)param2 + 2) + 1;
            List<object> loc6 = this.stack;
            List<object> loc7 = new List<object>();
            while (--loc5 > 0)
            {
                loc7.Add(loc6[loc6.Count - 1]);
                loc6.RemoveAt(loc6.Count - 1);
            }
            loc7.Reverse();
            if (loc4 is Dictionary<string, object> cd && cd.ContainsKey("__entryPoint"))
            {
                loc6.Add((double)(param2 + 4));
                loc6.Add(this.thisObject);
                loc6.Add(this.localObject);
                loc6.Add(GetSlot(param1, (int)param2 + 3));
                this.thisObject = this.global;
                this.localObject = new Dictionary<string, object>
                {
                    { "arguments", loc7 },
                    { "__scope", cd["__scope"] }
                };
                return (double)ToNumber(cd["__entryPoint"]);
            }
            SetSlot(param1, GetI(param1, (int)param2 + 3), CallFunction(loc4, this.global, loc7));
            return param2 + 4;
        }

        public object CALLM(List<object> param1, double param2)
        {
            object loc4 = GetSlot(param1, (int)param2 + 1);
            object loc5 = GetMember(loc4, As3String(GetSlot(param1, (int)param2 + 2)));
            int loc6 = GetI(param1, (int)param2 + 3) + 1;
            List<object> loc7 = this.stack;
            List<object> loc8 = new List<object>();
            while (--loc6 > 0)
            {
                loc8.Add(loc7[loc7.Count - 1]);
                loc7.RemoveAt(loc7.Count - 1);
            }
            loc8.Reverse();
            if (loc5 is Dictionary<string, object> cd && cd.ContainsKey("__entryPoint"))
            {
                loc7.Add((double)(param2 + 5));
                loc7.Add(this.thisObject);
                loc7.Add(this.localObject);
                loc7.Add(GetSlot(param1, (int)param2 + 4));
                this.thisObject = loc4;
                this.localObject = new Dictionary<string, object>
                {
                    { "arguments", loc8 },
                    { "__scope", cd["__scope"] }
                };
                return (double)ToNumber(cd["__entryPoint"]);
            }
            SetSlot(param1, GetI(param1, (int)param2 + 4), CallFunction(loc5, loc4, loc8));
            return param2 + 5;
        }

        public object CALLF(List<object> param1, double param2)
        {
            object loc4 = GetSlot(param1, (int)param2 + 1);
            int loc5 = GetI(param1, (int)param2 + 2) + 1;
            List<object> loc6 = this.stack;
            List<object> loc7 = new List<object>();
            while (--loc5 > 0)
            {
                loc7.Add(loc6[loc6.Count - 1]);
                loc6.RemoveAt(loc6.Count - 1);
            }
            loc7.Reverse();
            if (loc4 is Dictionary<string, object> cd && cd.ContainsKey("__entryPoint"))
            {
                loc6.Add((double)(param2 + 4));
                loc6.Add(this.thisObject);
                loc6.Add(this.localObject);
                loc6.Add(GetSlot(param1, (int)param2 + 3));
                this.thisObject = this.global;
                this.localObject = new Dictionary<string, object>
                {
                    { "arguments", loc7 },
                    { "__scope", cd["__scope"] }
                };
                return (double)ToNumber(cd["__entryPoint"]);
            }
            SetSlot(param1, GetI(param1, (int)param2 + 3), CallFunction(loc4, this.global, loc7));
            return param2 + 4;
        }

        public object RET(List<object> param1, double param2)
        {
            this.returnValue = GetSlot(param1, (int)param2 + 1);
            return (double)this.byteCodeLength;
        }

        public object CRET(List<object> param1, double param2)
        {
            List<object> loc3 = this.stack;
            SetSlot(param1, ToIndex(loc3[loc3.Count - 1]), GetSlot(param1, (int)param2 + 1));
            loc3.RemoveAt(loc3.Count - 1);
            this.localObject = loc3[loc3.Count - 1] as Dictionary<string, object>;
            loc3.RemoveAt(loc3.Count - 1);
            this.thisObject = loc3[loc3.Count - 1];
            loc3.RemoveAt(loc3.Count - 1);
            double ret = ToNumber(loc3[loc3.Count - 1]);
            loc3.RemoveAt(loc3.Count - 1);
            return ret;
        }

        public object FUNC(List<object> param1, double param2)
        {
            FuncObject fn = new FuncObject(this, param2 + 3, this.localObject);
            SetSlot(param1, GetI(param1, (int)param2 + 2), fn);
            return GetSlot(param1, (int)param2 + 1);
        }

        public object COR(List<object> param1, double param2)
        {
            var obj = new Dictionary<string, object>();
            obj["__entryPoint"] = (double)(param2 + 3);
            obj["__scope"] = this.localObject;
            SetSlot(param1, GetI(param1, (int)param2 + 2), obj);
            return GetSlot(param1, (int)param2 + 1);
        }

        public object ARG(List<object> param1, double param2)
        {
            string argName = As3String(GetSlot(param1, (int)param2 + 2));
            int argIdx = GetI(param1, (int)param2 + 1);
            object argsVal = null;
            if (this.localObject.TryGetValue("arguments", out var argsObj) && argsObj is List<object> argsList)
            {
                if (argIdx >= 0 && argIdx < argsList.Count) argsVal = argsList[argIdx];
            }
            SetObj(this.localObject, argName, argsVal);
            if (!this.localObject.ContainsKey("parameters"))
            {
                this.localObject["parameters"] = new List<object>();
            }
            ((List<object>)this.localObject["parameters"]).Add(argName);
            return param2 + 3;
        }

        public object JMP(List<object> param1, double param2)
        {
            return GetSlot(param1, (int)param2 + 1);
        }

        public object IF(List<object> param1, double param2)
        {
            if (Truthy(GetSlot(param1, (int)param2 + 1)))
            {
                return param2 + 3;
            }
            return GetSlot(param1, (int)param2 + 2);
        }

        public object NIF(List<object> param1, double param2)
        {
            if (Truthy(GetSlot(param1, (int)param2 + 1)))
            {
                return GetSlot(param1, (int)param2 + 2);
            }
            return param2 + 3;
        }

        public object ADD(List<object> param1, double param2)
        {
            SetSlot(param1, GetI(param1, (int)param2 + 3), As3Add(GetSlot(param1, (int)param2 + 1), GetSlot(param1, (int)param2 + 2)));
            return param2 + 4;
        }

        public object SUB(List<object> param1, double param2)
        {
            SetSlot(param1, GetI(param1, (int)param2 + 3), ToNumber(GetSlot(param1, (int)param2 + 1)) - ToNumber(GetSlot(param1, (int)param2 + 2)));
            return param2 + 4;
        }

        public object MUL(List<object> param1, double param2)
        {
            SetSlot(param1, GetI(param1, (int)param2 + 3), ToNumber(GetSlot(param1, (int)param2 + 1)) * ToNumber(GetSlot(param1, (int)param2 + 2)));
            return param2 + 4;
        }

        public object DIV(List<object> param1, double param2)
        {
            SetSlot(param1, GetI(param1, (int)param2 + 3), ToNumber(GetSlot(param1, (int)param2 + 1)) / ToNumber(GetSlot(param1, (int)param2 + 2)));
            return param2 + 4;
        }

        public object MOD(List<object> param1, double param2)
        {
            double b = ToNumber(GetSlot(param1, (int)param2 + 2));
            SetSlot(param1, GetI(param1, (int)param2 + 3), ToNumber(GetSlot(param1, (int)param2 + 1)) % b);
            return param2 + 4;
        }

        public object AND(List<object> param1, double param2)
        {
            SetSlot(param1, GetI(param1, (int)param2 + 3), (double)(Bit32(GetSlot(param1, (int)param2 + 1)) & Bit32(GetSlot(param1, (int)param2 + 2))));
            return param2 + 4;
        }

        public object OR(List<object> param1, double param2)
        {
            SetSlot(param1, GetI(param1, (int)param2 + 3), (double)(Bit32(GetSlot(param1, (int)param2 + 1)) | Bit32(GetSlot(param1, (int)param2 + 2))));
            return param2 + 4;
        }

        public object XOR(List<object> param1, double param2)
        {
            SetSlot(param1, GetI(param1, (int)param2 + 3), (double)(Bit32(GetSlot(param1, (int)param2 + 1)) ^ Bit32(GetSlot(param1, (int)param2 + 2))));
            return param2 + 4;
        }

        public object NOT(List<object> param1, double param2)
        {
            SetSlot(param1, GetI(param1, (int)param2 + 2), (double)(~Bit32(GetSlot(param1, (int)param2 + 1))));
            return param2 + 3;
        }

        public object LNOT(List<object> param1, double param2)
        {
            SetSlot(param1, GetI(param1, (int)param2 + 2), !Truthy(GetSlot(param1, (int)param2 + 1)));
            return param2 + 3;
        }

        public object LSH(List<object> param1, double param2)
        {
            SetSlot(param1, GetI(param1, (int)param2 + 3), (double)(Bit32(GetSlot(param1, (int)param2 + 1)) << (Bit32(GetSlot(param1, (int)param2 + 2)) & 31)));
            return param2 + 4;
        }

        public object RSH(List<object> param1, double param2)
        {
            SetSlot(param1, GetI(param1, (int)param2 + 3), (double)(Bit32(GetSlot(param1, (int)param2 + 1)) >> (Bit32(GetSlot(param1, (int)param2 + 2)) & 31)));
            return param2 + 4;
        }

        public object URSH(List<object> param1, double param2)
        {
            uint a = unchecked((uint)Bit32(GetSlot(param1, (int)param2 + 1)));
            int b = Bit32(GetSlot(param1, (int)param2 + 2)) & 31;
            SetSlot(param1, GetI(param1, (int)param2 + 3), (double)(a >> b));
            return param2 + 4;
        }

        public object INC(List<object> param1, double param2)
        {
            SetSlot(param1, GetI(param1, (int)param2 + 2), ToNumber(GetSlot(param1, (int)param2 + 1)) + 1);
            return param2 + 3;
        }

        public object DEC(List<object> param1, double param2)
        {
            SetSlot(param1, GetI(param1, (int)param2 + 2), ToNumber(GetSlot(param1, (int)param2 + 1)) - 1);
            return param2 + 3;
        }

        public object CEQ(List<object> param1, double param2)
        {
            SetSlot(param1, GetI(param1, (int)param2 + 3), As3Eq(GetSlot(param1, (int)param2 + 1), GetSlot(param1, (int)param2 + 2)));
            return param2 + 4;
        }

        public object CSEQ(List<object> param1, double param2)
        {
            SetSlot(param1, GetI(param1, (int)param2 + 3), As3StrictEq(GetSlot(param1, (int)param2 + 1), GetSlot(param1, (int)param2 + 2)));
            return param2 + 4;
        }

        public object CNE(List<object> param1, double param2)
        {
            SetSlot(param1, GetI(param1, (int)param2 + 3), !As3Eq(GetSlot(param1, (int)param2 + 1), GetSlot(param1, (int)param2 + 2)));
            return param2 + 4;
        }

        public object CSNE(List<object> param1, double param2)
        {
            SetSlot(param1, GetI(param1, (int)param2 + 3), !As3StrictEq(GetSlot(param1, (int)param2 + 1), GetSlot(param1, (int)param2 + 2)));
            return param2 + 4;
        }

        public object CLT(List<object> param1, double param2)
        {
            SetSlot(param1, GetI(param1, (int)param2 + 3), ToNumber(GetSlot(param1, (int)param2 + 1)) < ToNumber(GetSlot(param1, (int)param2 + 2)));
            return param2 + 4;
        }

        public object CGT(List<object> param1, double param2)
        {
            SetSlot(param1, GetI(param1, (int)param2 + 3), ToNumber(GetSlot(param1, (int)param2 + 1)) > ToNumber(GetSlot(param1, (int)param2 + 2)));
            return param2 + 4;
        }

        public object CLE(List<object> param1, double param2)
        {
            SetSlot(param1, GetI(param1, (int)param2 + 3), ToNumber(GetSlot(param1, (int)param2 + 1)) <= ToNumber(GetSlot(param1, (int)param2 + 2)));
            return param2 + 4;
        }

        public object CGE(List<object> param1, double param2)
        {
            SetSlot(param1, GetI(param1, (int)param2 + 3), ToNumber(GetSlot(param1, (int)param2 + 1)) >= ToNumber(GetSlot(param1, (int)param2 + 2)));
            return param2 + 4;
        }

        public object DUP(List<object> param1, double param2)
        {
            object loc3 = GetSlot(param1, (int)param2 + 1);
            SetSlot(param1, GetI(param1, (int)param2 + 2), loc3);
            SetSlot(param1, GetI(param1, (int)param2 + 3), loc3);
            return param2 + 4;
        }

        public object THIS(List<object> param1, double param2)
        {
            SetSlot(param1, GetI(param1, (int)param2 + 1), this.thisObject);
            return param2 + 2;
        }

        public object ARRAY(List<object> param1, double param2)
        {
            int loc3 = GetI(param1, (int)param2 + 1);
            List<object> loc4 = new List<object>(loc3);
            for (int i = 0; i < loc3; i++) loc4.Add(null);
            List<object> loc5 = this.stack;
            int loc6 = 0;
            while (loc6 < loc3)
            {
                loc4[loc6] = loc5[loc5.Count - 1];
                loc5.RemoveAt(loc5.Count - 1);
                loc6++;
            }
            loc4.Reverse();
            SetSlot(param1, GetI(param1, (int)param2 + 2), loc4);
            return param2 + 3;
        }

        public object OBJ(List<object> param1, double param2)
        {
            int loc3 = GetI(param1, (int)param2 + 1);
            Dictionary<string, object> loc4 = new Dictionary<string, object>();
            List<object> loc5 = this.stack;
            int loc6 = 0;
            while (loc6 < loc3)
            {
                object loc7 = loc5[loc5.Count - 1];
                loc5.RemoveAt(loc5.Count - 1);
                object key = loc5[loc5.Count - 1];
                loc5.RemoveAt(loc5.Count - 1);
                SetObj(loc4, As3String(key), loc7);
                loc6++;
            }
            SetSlot(param1, GetI(param1, (int)param2 + 2), loc4);
            return param2 + 3;
        }

        public object SETL(List<object> param1, double param2)
        {
            string name = As3String(GetSlot(param1, (int)param2 + 1));
            if (name == "__scope")
            {
                throw new Exception("不能用  __scope!");
            }
            object val = GetSlot(param1, (int)param2 + 2);
            SetSlot(param1, GetI(param1, (int)param2 + 3), val);
            SetObj(this.localObject, name, val);
            return param2 + 4;
        }

        public object GETL(List<object> param1, double param2)
        {
            object v = null;
            this.localObject.TryGetValue(As3String(GetSlot(param1, (int)param2 + 1)), out v);
            SetSlot(param1, GetI(param1, (int)param2 + 2), v);
            return param2 + 3;
        }

        public object SET(List<object> param1, double param2)
        {
            string loc3 = As3String(GetSlot(param1, (int)param2 + 1));
            if (loc3 == "__scope")
            {
                throw new Exception("不能用  __scope!");
            }
            object val = GetSlot(param1, (int)param2 + 2);
            Dictionary<string, object> loc4 = this.localObject;
            while (loc4 != null)
            {
                if (loc4.ContainsKey(loc3))
                {
                    SetSlot(param1, GetI(param1, (int)param2 + 3), val);
                    loc4[loc3] = val;
                    return param2 + 4;
                }
                loc4 = ScopeOf(loc4);
            }
            SetSlot(param1, GetI(param1, (int)param2 + 3), val);
            SetObj(this.global as Dictionary<string, object>, loc3, val);
            return param2 + 4;
        }

        public object GET(List<object> param1, double param2)
        {
            string loc3 = As3String(GetSlot(param1, (int)param2 + 1));
            Dictionary<string, object> loc4 = this.localObject;
            while (loc4 != null)
            {
                if (loc4.TryGetValue(loc3, out var v))
                {
                    SetSlot(param1, GetI(param1, (int)param2 + 2), v);
                    return param2 + 3;
                }
                loc4 = ScopeOf(loc4);
            }
            SetSlot(param1, GetI(param1, (int)param2 + 2), null);
            return param2 + 3;
        }

        public object SETM(List<object> param1, double param2)
        {
            string key = As3String(GetSlot(param1, (int)param2 + 2));
            if (key == "__scope")
            {
                throw new Exception("不能用  __scope!");
            }
            object obj = GetSlot(param1, (int)param2 + 1);
            object val = GetSlot(param1, (int)param2 + 3);
            SetSlot(param1, GetI(param1, (int)param2 + 4), val);
            SetMember(obj, key, val);
            return param2 + 5;
        }

        public object GETM(List<object> param1, double param2)
        {
            object obj = GetSlot(param1, (int)param2 + 1);
            string loc3 = As3String(GetSlot(param1, (int)param2 + 2));
            SetSlot(param1, GetI(param1, (int)param2 + 3), GetMember(obj, loc3));
            return param2 + 4;
        }

        public object GETMV(List<object> param1, double param2)
        {
            object loc3 = null;
            if (As3String(GetSlot(param1, (int)param2 + 2)) == "GETL")
            {
                this.localObject.TryGetValue(As3String(GetSlot(param1, (int)param2 + 3)), out loc3);
            }
            else
            {
                Dictionary<string, object> loc4 = this.localObject;
                string name3 = As3String(GetSlot(param1, (int)param2 + 3));
                while (loc4 != null)
                {
                    if (loc4.TryGetValue(name3, out var v))
                    {
                        loc3 = v;
                    }
                    loc4 = ScopeOf(loc4);
                }
            }
            object obj2 = GetSlot(param1, (int)param2 + 1);
            string key2 = As3String(loc3);
            SetSlot(param1, GetI(param1, (int)param2 + 4), GetMember(obj2, key2));
            return param2 + 5;
        }

        public object NEW(List<object> param1, double param2)
        {
            object loc3 = GetSlot(param1, (int)param2 + 1);
            int loc5 = GetI(param1, (int)param2 + 2) + 1;
            List<object> loc6 = this.stack;
            List<object> loc7 = new List<object>();
            while (--loc5 > 0)
            {
                loc7.Add(loc6[loc6.Count - 1]);
                loc6.RemoveAt(loc6.Count - 1);
            }
            loc7.Reverse();
            object result = CallFunction(loc3, new Dictionary<string, object>(), loc7);
            SetSlot(param1, GetI(param1, (int)param2 + 3), result ?? loc3);
            return param2 + 4;
        }

        public object DEL(List<object> param1, double param2)
        {
            string loc3 = As3String(GetSlot(param1, (int)param2 + 1));
            Dictionary<string, object> loc4 = this.localObject;
            while (loc4 != null)
            {
                if (loc4.ContainsKey(loc3))
                {
                    bool ok = loc4.Remove(loc3);
                    SetSlot(param1, GetI(param1, (int)param2 + 2), ok);
                    return param2 + 3;
                }
                loc4 = ScopeOf(loc4);
            }
            SetSlot(param1, GetI(param1, (int)param2 + 2), false);
            return param2 + 3;
        }

        public object DELL(List<object> param1, double param2)
        {
            bool ok = this.localObject.Remove(As3String(GetSlot(param1, (int)param2 + 1)));
            SetSlot(param1, GetI(param1, (int)param2 + 2), ok);
            return param2 + 3;
        }

        public object DELM(List<object> param1, double param2)
        {
            object obj = GetSlot(param1, (int)param2 + 1);
            string key = As3String(GetSlot(param1, (int)param2 + 2));
            bool ok = false;
            if (obj is Dictionary<string, object> d) ok = d.Remove(key);
            else if (obj is List<object> l)
            {
                int idx = ToIndex(key);
                if (idx >= 0 && idx < l.Count) { l.RemoveAt(idx); ok = true; }
            }
            else if (obj is FuncObject fo) ok = fo.props.Remove(key);
            SetSlot(param1, GetI(param1, (int)param2 + 3), ok);
            return param2 + 4;
        }

        public object TYPEOF(List<object> param1, double param2)
        {
            SetSlot(param1, GetI(param1, (int)param2 + 2), As3TypeOf(GetSlot(param1, (int)param2 + 1)));
            return param2 + 3;
        }

        public object INSOF(List<object> param1, double param2)
        {
            object v = GetSlot(param1, (int)param2 + 1);
            object target = GetSlot(param1, (int)param2 + 2);
            bool result = false;
            if (v != null && (target is Type t))
            {
                result = t.IsInstanceOfType(v);
            }
            SetSlot(param1, GetI(param1, (int)param2 + 3), result);
            return param2 + 4;
        }

        public object NUM(List<object> param1, double param2)
        {
            SetSlot(param1, GetI(param1, (int)param2 + 2), ToNumber(GetSlot(param1, (int)param2 + 1)));
            return param2 + 3;
        }

        public object STR(List<object> param1, double param2)
        {
            SetSlot(param1, GetI(param1, (int)param2 + 2), As3String(GetSlot(param1, (int)param2 + 1)));
            return param2 + 3;
        }

        public object WITH(List<object> param1, double param2)
        {
            object loc3 = GetSlot(param1, (int)param2 + 1);
            if (loc3 is Dictionary<string, object> d)
            {
                SetObj(d, "__scope", this.localObject);
                this.localObject = d;
            }
            return param2 + 2;
        }

        public object EWITH(List<object> param1, double param2)
        {
            this.localObject = ScopeOf(this.localObject);
            return param2 + 1;
        }

        public object PUSH(List<object> param1, double param2)
        {
            this.stack.Add(GetSlot(param1, (int)param2 + 1));
            return param2 + 2;
        }

        public object POP(List<object> param1, double param2)
        {
            object v = this.stack[this.stack.Count - 1];
            this.stack.RemoveAt(this.stack.Count - 1);
            SetSlot(param1, GetI(param1, (int)param2 + 1), v);
            return param2 + 2;
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>Function value created by FUNC opcode (interpreted closure).</summary>
        public class FuncObject
        {
            public VirtualMachine vm;
            public double entryPoint;
            public Dictionary<string, object> scope;
            public Dictionary<string, object> props = new Dictionary<string, object>();
            public bool hasEntryPoint;
            public string name;

            public FuncObject(VirtualMachine vm, double ep, Dictionary<string, object> scope)
            {
                this.vm = vm;
                this.entryPoint = ep;
                this.scope = scope;
                this.hasEntryPoint = false;
            }

            public object Invoke(object thisArg, List<object> args)
            {
                return vm.executeFunction(thisArg, args, ToIndex(this.entryPoint), this.scope);
            }

            public override string ToString()
            {
                return "function" + (name != null ? " " + name : "") + "() { [native] }";
            }
        }

        /// <summary>
        /// Invokes a callable value (script function or host delegate) with the given
        /// arguments. Exposed for sandbox host APIs (e.g. Utils.foreach / interval).
        /// </summary>
        public object InvokeFunction(object fn, object thisArg, List<object> args)
        {
            if (fn == null) return null;
            return this.CallFunction(fn, thisArg, args);
        }

        private object CallFunction(object fn, object thisArg, List<object> args)
        {
            if (fn == null) return null;
            if (fn is FuncObject fo)
            {
                return fo.Invoke(thisArg, args);
            }
            if (fn is Delegate d)
            {
                return InvokeHost(d, thisArg, args);
            }
            // AS3 would raise a TypeError calling a non-function; mimic by returning undefined
            return null;
        }

        private object InvokeHost(Delegate d, object thisArg, List<object> args)
        {
            var ps = d.Method.GetParameters();
            // Variadic host functions (JS-style parseInt / Math.max / Math.min ...)
            // declare a single object[] parameter: pass the whole argument list.
            if (ps.Length == 1 && ps[0].ParameterType == typeof(object[]))
            {
                try
                {
                    return d.DynamicInvoke(new object[] { args.ToArray() });
                }
                catch (Exception)
                {
                    return null;
                }
            }
            try
            {
                object[] p = args.ToArray();
                return d.DynamicInvoke(p);
            }
            catch (TargetParameterCountException)
            {
                return null;
            }
            catch (Exception ex)
            {
                // Control-flow exception from the sandbox (stopExecution etc.) must propagate
                // unwrapped (DynamicInvoke wraps it in TargetInvocationException).
                if (ex is M8StopException) throw;
                if (ex.InnerException is M8StopException) throw ex.InnerException;
                return null;
            }
        }

        private static object GetSlot(List<object> a, int i)
        {
            return i >= 0 && i < a.Count ? a[i] : null;
        }

        private static void SetSlot(List<object> a, int i, object v)
        {
            while (a.Count <= i) a.Add(null);
            a[i] = v;
        }

        private static int GetI(List<object> a, int i)
        {
            return ToIndex(GetSlot(a, i));
        }

        private static Dictionary<string, object> ScopeOf(Dictionary<string, object> d)
        {
            if (d != null && d.TryGetValue("__scope", out var s) && s is Dictionary<string, object> sd)
            {
                return sd;
            }
            return null;
        }

        private static void SetObj(Dictionary<string, object> d, string key, object val)
        {
            if (d == null) return;
            d[key] = val;
        }

        /// <summary>Read a runtime member from any object/array/function/host value.</summary>
        public static object GetMember(object obj, string key)
        {
            if (obj == null) return null;
            if (obj is IM8ScriptObject m8Object)
            {
                return m8Object.Get(key);
            }
            if (obj is Dictionary<string, object> d)
            {
                return d.TryGetValue(key, out var v) ? v : null;
            }
            if (obj is FuncObject fo)
            {
                if (key == "__entryPoint") return fo.entryPoint;
                if (key == "__scope") return fo.scope;
                return fo.props.TryGetValue(key, out var v2) ? v2 : null;
            }
            if (obj is List<object> l)
            {
                if (key == "length") return (double)l.Count;
                if (IsArrayIndex(key)) return ToIndex(key) >= 0 && ToIndex(key) < l.Count ? l[ToIndex(key)] : null;
                return null;
            }
            if (obj is string s)
            {
                if (key == "length") return (double)s.Length;
                if (key == "charAt") return (Func<object, object, object>)((idx, _) => { int i = ToIndex(idx); return (i >= 0 && i < s.Length) ? s[i].ToString() : ""; });
                if (key == "charCodeAt") return (Func<object, object, object>)((idx, _) => { int i = ToIndex(idx); return (double)((i >= 0 && i < s.Length) ? (int)s[i] : double.NaN); });
                return null;
            }
            if (obj is bool || obj is double || obj is int)
            {
                return null;
            }
            // Host object: reflect
            var t = obj.GetType();
            var pi = t.GetProperty(key);
            if (pi != null) return pi.GetValue(obj, null);
            var fi = t.GetField(key);
            if (fi != null) return fi.GetValue(obj);
            return null;
        }

        /// <summary>Write a runtime member onto any object/array/function/host value.</summary>
        public static void SetMember(object obj, string key, object val)
        {
            if (obj == null) return;
            if (obj is IM8ScriptObject m8Object)
            {
                m8Object.Set(key, val);
                return;
            }
            if (obj is Dictionary<string, object> d)
            {
                if (key == "__scope" && val != null && !(val is Dictionary<string, object>)) throw new Exception("不能用  __scope!");
                d[key] = val;
                return;
            }
            if (obj is FuncObject fo)
            {
                if (key == "__entryPoint") { fo.entryPoint = ToNumber(val); fo.hasEntryPoint = true; return; }
                if (key == "__scope") { fo.scope = val as Dictionary<string, object>; return; }
                fo.props[key] = val;
                return;
            }
            if (obj is List<object> l)
            {
                if (key == "length") return; // setting array length unsupported (rare in M8 scripts)
                if (IsArrayIndex(key))
                {
                    int i = ToIndex(key);
                    while (l.Count <= i) l.Add(null);
                    l[i] = val;
                }
                return;
            }
            // Host object reflection
            var t = obj.GetType();
            var pi = t.GetProperty(key);
            if (pi != null && pi.CanWrite) pi.SetValue(obj, Convert.ChangeType(val, pi.PropertyType), null);
            var fi = t.GetField(key);
            if (fi != null) fi.SetValue(obj, Convert.ChangeType(val, fi.FieldType));
        }

        private static bool IsArrayIndex(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s) if (!char.IsDigit(c)) return false;
            return s.Length > 0;
        }

        // ---------------------------------------------------------------- AS3 loose semantics

        public static bool Truthy(object v)
        {
            if (v == null) return false;
            if (v is bool b) return b;
            if (v is double dd) return dd != 0 && !double.IsNaN(dd);
            if (v is int ii) return ii != 0;
            if (v is long ll) return ll != 0;
            if (v is string s) return s.Length > 0;
            if (v is Dictionary<string, object>) return true;
            if (v is List<object>) return true;
            if (v is FuncObject) return true;
            if (v is Delegate) return true;
            return true;
        }

        public static double ToNumber(object v)
        {
            if (v == null) return 0;
            if (v is double d) return d;
            if (v is int i) return i;
            if (v is long l) return l;
            if (v is float f) return f;
            if (v is decimal m) return (double)m;
            if (v is bool b) return b ? 1 : 0;
            if (v is string s)
            {
                string t = s.Trim();
                if (t.Length == 0) return 0;
                if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out double x)) return x;
                return double.NaN;
            }
            return double.NaN;
        }

        public static int ToIndex(object v)
        {
            double d = ToNumber(v);
            if (double.IsNaN(d) || double.IsInfinity(d)) return 0;
            return (int)d;
        }

        public static int Bit32(object v)
        {
            double d = ToNumber(v);
            if (double.IsNaN(d) || double.IsInfinity(d)) return 0;
            return unchecked((int)d);
        }

        public static string As3String(object v)
        {
            if (v == null) return "undefined";
            if (v is string s) return s;
            if (v is bool b) return b ? "true" : "false";
            if (v is double d)
            {
                if (double.IsNaN(d)) return "NaN";
                if (double.IsPositiveInfinity(d)) return "Infinity";
                if (double.IsNegativeInfinity(d)) return "-Infinity";
                if (d == Math.Floor(d) && Math.Abs(d) < 1e21) return d.ToString("0", CultureInfo.InvariantCulture);
                return d.ToString("R", CultureInfo.InvariantCulture);
            }
            if (v is int i) return i.ToString(CultureInfo.InvariantCulture);
            if (v is long l) return l.ToString(CultureInfo.InvariantCulture);
            if (v is Dictionary<string, object>) return "[object Object]";
            if (v is List<object> arr)
            {
                var sb = new StringBuilder();
                for (int n = 0; n < arr.Count; n++)
                {
                    if (n > 0) sb.Append(',');
                    object el = arr[n];
                    sb.Append(el == null ? "" : As3String(el));
                }
                return sb.ToString();
            }
            if (v is Delegate || v is FuncObject) return "function () { [native code] }";
            return Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        public static string As3TypeOf(object v)
        {
            if (v == null) return "undefined";
            if (v is bool) return "boolean";
            if (v is double || v is int || v is long || v is float || v is decimal) return "number";
            if (v is string) return "string";
            if (v is FuncObject || v is Delegate) return "function";
            return "object";
        }

        public static object As3Add(object a, object b)
        {
            if (a is string || b is string ||
                a is Dictionary<string, object> || b is Dictionary<string, object> ||
                a is List<object> || b is List<object> ||
                a is FuncObject || b is FuncObject ||
                a is Delegate || b is Delegate)
            {
                return As3String(a) + As3String(b);
            }
            return ToNumber(a) + ToNumber(b);
        }

        public static bool As3Eq(object a, object b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null) return b == null || b is Dictionary<string, object> == false && b is List<object> == false;
            if (b == null) return a == null;
            if (a is bool && b is bool) return (bool)a == (bool)b;
            if (a is string && b is string) return (string)a == (string)b;
            bool aNumLike = a is double || a is int || a is long || a is float || a is decimal || a is bool || a is string;
            bool bNumLike = b is double || b is int || b is long || b is float || b is decimal || b is bool || b is string;
            if (aNumLike && bNumLike)
            {
                return ToNumber(a) == ToNumber(b);
            }
            return a.Equals(b);
        }

        public static bool As3StrictEq(object a, object b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return a == null && b == null;
            if (a is double || a is int || a is long || a is float || a is decimal)
            {
                return (b is double || b is int || b is long || b is float || b is decimal) && ToNumber(a) == ToNumber(b);
            }
            return a.Equals(b);
        }
    }
}
