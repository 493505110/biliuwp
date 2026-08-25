using System;

namespace scripting
{
    public class Scanner : IScanner
    {
        private string source;

        private object index;

        private object linesCount;

        public Scanner(string param1)
        {
            this.source = param1;
            this.rewind();
        }

        public object rewind()
        {
            this.index = 0;
            this.linesCount = 0;
            return null;
        }

        public double getLineNumber()
        {
            return (int)this.linesCount + 1;
        }

        public string getLine()
        {
            string[] lines = this.source.Split(new[] { '\n' }, StringSplitOptions.None);
            int lineIndex = (int)this.linesCount;
            if (lineIndex < 0 || lineIndex >= lines.Length)
            {
                return null;
            }
            return lines[lineIndex];
        }

        private string getChar()
        {
            int currentIndex = (int)this.index;
            if (currentIndex < 0 || currentIndex >= this.source.Length)
            {
                return string.Empty;
            }
            return this.source.Substring(currentIndex, 1);
        }

        private string nextChar()
        {
            if (this.getChar() == "\n")
            {
                this.linesCount = (int)this.linesCount + 1;
            }
            this.index = (int)this.index + 1;
            return this.getChar();
        }

        private bool isSpace(string param1)
        {
            return param1 == " " || param1 == "\t" || param1 == "\r" || param1 == "\n";
        }

        private bool isAlphabet(string param1)
        {
            if (string.IsNullOrEmpty(param1))
            {
                return false;
            }
            int loc2 = param1[0];
            return 65 <= loc2 && loc2 <= 90 || 97 <= loc2 && loc2 <= 122;
        }

        private bool isNumber(string param1)
        {
            if (string.IsNullOrEmpty(param1))
            {
                return false;
            }
            int loc2 = param1[0];
            return 48 <= loc2 && loc2 <= 57;
        }

        private bool isAlphabetOrNumber(string param1)
        {
            if (string.IsNullOrEmpty(param1))
            {
                return false;
            }
            int loc2 = param1[0];
            return 48 <= loc2 && loc2 <= 57 || 65 <= loc2 && loc2 <= 90 || 97 <= loc2 && loc2 <= 122;
        }

        private bool isHex(string param1)
        {
            if (string.IsNullOrEmpty(param1))
            {
                return false;
            }
            int loc2 = param1[0];
            return 48 <= loc2 && loc2 <= 57 || 65 <= loc2 && loc2 <= 70 || 97 <= loc2 && loc2 <= 102;
        }

        private bool isIdentifier(string param1)
        {
            if (string.IsNullOrEmpty(param1))
            {
                return false;
            }
            int loc2 = param1[0];
            return loc2 == 36 || loc2 == 95 || 48 <= loc2 && loc2 <= 57 || 65 <= loc2 && loc2 <= 90 || 97 <= loc2 && loc2 <= 122;
        }

        public Token getToken()
        {
            string loc2 = null;
            string loc3 = null;
            string loc4 = null;
            string loc5 = null;
            string loc1 = this.getChar();
            while (this.isSpace(loc1))
            {
                loc1 = this.nextChar();
            }
            if (string.IsNullOrEmpty(loc1))
            {
                return null;
            }
            if (this.isAlphabet(loc1) || loc1 == "$" || loc1 == "_")
            {
                loc2 = loc1;
                while (true)
                {
                    loc1 = this.nextChar();
                    if (!(string.IsNullOrEmpty(loc1) == false && this.isIdentifier(loc1)))
                    {
                        break;
                    }
                    loc2 += loc1;
                }
                loc3 = loc2.ToLowerInvariant();
                switch (loc3)
                {
                    case "break":
                    case "case":
                    case "continue":
                    case "default":
                    case "delete":
                    case "do":
                    case "else":
                    case "for":
                    case "function":
                    case "if":
                    case "instanceof":
                    case "new":
                    case "return":
                    case "switch":
                    case "this":
                    case "typeof":
                    case "var":
                    case "while":
                    case "with":
                    case "coroutine":
                    case "suspend":
                    case "yield":
                    case "loop":
                        return new Token(loc3, null);
                    case "null":
                        return new Token("null", null);
                    case "undefined":
                        return new Token("undefined", null);
                    case "true":
                        return new Token("bool", true);
                    case "false":
                        return new Token("bool", false);
                    default:
                        return new Token("identifier", loc2);
                }
            }
            else
            {
                if (this.isNumber(loc1))
                {
                    loc2 = loc1;
                    if (loc1 == "0")
                    {
                        loc1 = this.nextChar();
                        if (loc1 == "x" || loc1 == "X")
                        {
                            loc2 += loc1;
                            while (true)
                            {
                                loc1 = this.nextChar();
                                if (!(string.IsNullOrEmpty(loc1) == false && this.isHex(loc1)))
                                {
                                    break;
                                }
                                loc2 += loc1;
                            }
                        }
                        else if (this.isNumber(loc1))
                        {
                            loc2 += loc1;
                            while (true)
                            {
                                loc1 = this.nextChar();
                                if (!(string.IsNullOrEmpty(loc1) == false && this.isNumber(loc1)))
                                {
                                    break;
                                }
                                loc2 += loc1;
                            }
                        }
                    }
                    else
                    {
                        while (true)
                        {
                            loc1 = this.nextChar();
                            if (!(string.IsNullOrEmpty(loc1) == false && this.isNumber(loc1)))
                            {
                                break;
                            }
                            loc2 += loc1;
                        }
                    }
                    if (loc1 == ".")
                    {
                        loc2 += loc1;
                        while (true)
                        {
                            loc1 = this.nextChar();
                            if (!(string.IsNullOrEmpty(loc1) == false && this.isNumber(loc1)))
                            {
                                break;
                            }
                            loc2 += loc1;
                        }
                        return new Token("number", parseFloat(loc2));
                    }
                    return new Token("number", parseInt(loc2));
                }
                if (loc1 == "'")
                {
                    loc2 = "";
                    while (true)
                    {
                        loc1 = this.nextChar();
                        if (!(string.IsNullOrEmpty(loc1) == false && loc1 != "'"))
                        {
                            break;
                        }
                        if (loc1 == "\\")
                        {
                            loc1 = this.nextChar();
                            if (loc1 == "n")
                            {
                                loc2 += "\n";
                                continue;
                            }
                            if (loc1 == "t")
                            {
                                loc2 += "\t";
                                continue;
                            }
                            if (loc1 == "r")
                            {
                                loc2 += "\r";
                                continue;
                            }
                            if (loc1 == "x")
                            {
                                loc4 = this.nextChar();
                                loc5 = this.nextChar();
                                loc2 += fromCharCode(parseInt("0x" + loc4 + loc5));
                                continue;
                            }
                            if (loc1 == "0")
                            {
                                loc4 = this.nextChar();
                                loc5 = this.nextChar();
                                loc2 += fromCharCode(parseInt(loc4 + loc5, 8));
                                continue;
                            }
                            if (loc1 == "\\")
                            {
                                loc2 += "\\";
                                continue;
                            }
                        }
                        loc2 += loc1;
                    }
                    if (loc1 != "'")
                    {
                        throw new VMSyntaxError("String literal is not closed.");
                    }
                    this.nextChar();
                    return new Token("string", loc2);
                }
                if (loc1 == "\"")
                {
                    loc2 = "";
                    while (true)
                    {
                        loc1 = this.nextChar();
                        if (!(string.IsNullOrEmpty(loc1) == false && loc1 != "\""))
                        {
                            break;
                        }
                        if (loc1 == "\\")
                        {
                            loc1 = this.nextChar();
                            if (loc1 == "n")
                            {
                                loc2 += "\n";
                                continue;
                            }
                            if (loc1 == "t")
                            {
                                loc2 += "\t";
                                continue;
                            }
                            if (loc1 == "r")
                            {
                                loc2 += "\r";
                                continue;
                            }
                            if (loc1 == "x")
                            {
                                loc4 = this.nextChar();
                                loc5 = this.nextChar();
                                loc2 += fromCharCode(parseInt("0x" + loc4 + loc5));
                                continue;
                            }
                            if (loc1 == "0")
                            {
                                loc4 = this.nextChar();
                                loc5 = this.nextChar();
                                loc2 += fromCharCode(parseInt(loc4 + loc5, 8));
                                continue;
                            }
                            if (loc1 == "\\")
                            {
                                loc2 += "\\";
                                continue;
                            }
                        }
                        loc2 += loc1;
                    }
                    if (loc1 != "\"")
                    {
                        throw new VMSyntaxError("String literal is not closed.");
                    }
                    this.nextChar();
                    return new Token("string", loc2);
                }
                if (loc1 == "/")
                {
                    loc1 = this.nextChar();
                    if (!string.IsNullOrEmpty(loc1))
                    {
                        if (loc1 == "=")
                        {
                            this.nextChar();
                            return new Token("/=", null);
                        }
                        if (loc1 == "/")
                        {
                            while (true)
                            {
                                loc1 = this.nextChar();
                                if (!(string.IsNullOrEmpty(loc1) == false && loc1 != "\n"))
                                {
                                    break;
                                }
                            }
                            this.nextChar();
                            return this.getToken();
                        }
                        if (loc1 == "*")
                        {
                            loc1 = this.nextChar();
                            while (!string.IsNullOrEmpty(loc1))
                            {
                                if (loc1 == "*")
                                {
                                    loc1 = this.nextChar();
                                    if (!string.IsNullOrEmpty(loc1) && loc1 == "/")
                                    {
                                        break;
                                    }
                                }
                                else
                                {
                                    loc1 = this.nextChar();
                                }
                            }
                            this.nextChar();
                            return this.getToken();
                        }
                    }
                    return new Token("/", null);
                }
                if (loc1 == "*" || loc1 == "%" || loc1 == "^")
                {
                    loc3 = loc1;
                    loc1 = this.nextChar();
                    if (!string.IsNullOrEmpty(loc1) && loc1 == "=")
                    {
                        this.nextChar();
                        return new Token(loc3 + "=", null);
                    }
                    return new Token(loc3, null);
                }
                if (loc1 == "+" || loc1 == "-" || loc1 == "|" || loc1 == "&")
                {
                    loc3 = loc1;
                    loc1 = this.nextChar();
                    if (!string.IsNullOrEmpty(loc1))
                    {
                        if (loc1 == loc3)
                        {
                            this.nextChar();
                            return new Token(loc3 + loc3, null);
                        }
                        if (loc1 == "=")
                        {
                            this.nextChar();
                            return new Token(loc3 + "=", null);
                        }
                    }
                    return new Token(loc3, null);
                }
                if (loc1 == "=" || loc1 == "!")
                {
                    loc3 = loc1;
                    loc1 = this.nextChar();
                    if (!string.IsNullOrEmpty(loc1) && loc1 == "=")
                    {
                        loc1 = this.nextChar();
                        if (!string.IsNullOrEmpty(loc1) && loc1 == "=")
                        {
                            this.nextChar();
                            return new Token(loc3 + "==", null);
                        }
                        return new Token(loc3 + "=", null);
                    }
                    return new Token(loc3, null);
                }
                if (loc1 == ">" || loc1 == "<")
                {
                    loc3 = loc1;
                    loc1 = this.nextChar();
                    if (!string.IsNullOrEmpty(loc1))
                    {
                        if (loc1 == "=")
                        {
                            this.nextChar();
                            return new Token(loc3 + "=", null);
                        }
                        if (loc1 == loc3)
                        {
                            loc1 = this.nextChar();
                            if (!string.IsNullOrEmpty(loc1))
                            {
                                if (loc3 == ">" && loc1 == ">")
                                {
                                    loc1 = this.nextChar();
                                    if (!string.IsNullOrEmpty(loc1) && loc1 == "=")
                                    {
                                        this.nextChar();
                                        return new Token(">>>=", null);
                                    }
                                    return new Token(">>>", null);
                                }
                                if (loc1 == "=")
                                {
                                    this.nextChar();
                                    return new Token(loc3 + loc3 + "=", null);
                                }
                            }
                            return new Token(loc3 + loc3, null);
                        }
                    }
                    return new Token(loc3, null);
                }
                switch (loc1)
                {
                    case "{":
                    case "}":
                    case "(":
                    case ")":
                    case "[":
                    case "]":
                    case ".":
                    case ";":
                    case ",":
                    case "~":
                    case "?":
                    case ":":
                        this.nextChar();
                        return new Token(loc1, null);
                    default:
                        throw new VMSyntaxError("Unknown character : \"" + loc1 + "\" at index " + this.index + ".");
                }
            }
        }

        private static double parseFloat(string param1)
        {
            double result;
            if (double.TryParse(param1, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result))
            {
                return result;
            }
            return double.NaN;
        }

        private static double parseInt(string param1)
        {
            int start = 0;
            int radix = 10;
            if (param1 != null && param1.Length >= 2 && param1[0] == '0' && (param1[1] == 'x' || param1[1] == 'X'))
            {
                start = 2;
                radix = 16;
            }
            return parseInt(param1, start, radix);
        }

        private static double parseInt(string param1, int radix)
        {
            return parseInt(param1, 0, radix);
        }

        private static double parseInt(string param1, int start, int radix)
        {
            if (string.IsNullOrEmpty(param1))
            {
                return double.NaN;
            }
            bool negative = false;
            if (start < param1.Length && (param1[start] == '+' || param1[start] == '-'))
            {
                negative = param1[start] == '-';
                ++start;
            }
            double result = 0;
            bool hasDigit = false;
            while (start < param1.Length)
            {
                char current = param1[start];
                int digit;
                if (current >= '0' && current <= '9')
                {
                    digit = current - '0';
                }
                else if (current >= 'A' && current <= 'Z')
                {
                    digit = current - 'A' + 10;
                }
                else if (current >= 'a' && current <= 'z')
                {
                    digit = current - 'a' + 10;
                }
                else
                {
                    break;
                }
                if (digit >= radix)
                {
                    break;
                }
                result = result * radix + digit;
                hasDigit = true;
                ++start;
            }
            if (!hasDigit)
            {
                return double.NaN;
            }
            return negative ? -result : result;
        }

        private static string fromCharCode(double param1)
        {
            if (double.IsNaN(param1) || double.IsInfinity(param1))
            {
                return "\0";
            }
            long code = (long)param1;
            return new string((char)(code & 65535), 1);
        }
    }
}
