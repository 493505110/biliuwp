using System;
using System.Collections.Generic;

namespace scripting
{
    public class Parser : IParser
    {
        private IScanner scanner;

        private CodeGenerator generator;

        private Token token;

        private bool parseForceCoroutine = false;

        private VirtualMachine vmtarget = null;

        private List<Label> breakLabelList;

        private List<Label> continueLabelList;

        private List<bool> functionStack;

        private bool hasLastReturn;

        private int lastReturnCaution = 0;

        public Parser(IScanner param1)
        {
            this.scanner = param1;
        }

        private Token getToken()
        {
            return this.token;
        }

        private Token nextToken()
        {
            return this.token = this.scanner.getToken();
        }

        private bool isToken(string param1)
        {
            if (this.token == null)
            {
                return false;
            }
            return this.token.type == param1;
        }

        private bool isNextToken(string param1)
        {
            Token next = this.nextToken();
            return next != null && next.type == param1;
        }

        private object initialize()
        {
            this.scanner.rewind();
            this.generator = new CodeGenerator();
            this.breakLabelList = new List<Label>();
            this.continueLabelList = new List<Label>();
            this.functionStack = new List<bool>();
            this.nextToken();
            return null;
        }

        public object setForceCoroutine(bool param1)
        {
            this.parseForceCoroutine = param1;
            return null;
        }

        public List<object> parse(object param1 = null)
        {
            this.initialize();
            if (param1 != null)
            {
                this.generator.vmtarget = param1 as VirtualMachine;
                this.generator.vmtarget.optimized = true;
                this.vmtarget = param1 as VirtualMachine;
            }
            this.parse_program();
            return this.generator.getCode();
        }

        private void causeSyntaxError(string param1)
        {
            Token t = this.getToken();
            string tokType = t != null ? t.type : "<eof>";
            throw new VMSyntaxError("Parser [causeSyntaxError] on line " + this.scanner.getLineNumber() + " " + param1 + " (" + tokType + ")" + " " + this.scanner.getLine());
        }

        private void pushBreakLabel(Label param1)
        {
            this.breakLabelList.Insert(0, param1);
        }

        private Label popBreakLabel()
        {
            Label result = this.breakLabelList[0];
            this.breakLabelList.RemoveAt(0);
            return result;
        }

        private Label getBreakLabel()
        {
            if (this.breakLabelList.Count < 1)
            {
                this.causeSyntaxError("break cannot be used here");
            }
            return this.breakLabelList[0];
        }

        private void pushContinueLabel(Label param1)
        {
            this.continueLabelList.Insert(0, param1);
        }

        private Label popContinueLabel()
        {
            Label result = this.continueLabelList[0];
            this.continueLabelList.RemoveAt(0);
            return result;
        }

        private Label getContinueLabel()
        {
            if (this.continueLabelList.Count < 1)
            {
                this.causeSyntaxError("continue cannot be used here");
            }
            return this.continueLabelList[0];
        }

        private void beginFunction()
        {
            this.functionStack.Insert(0, true);
        }

        private void endFunction()
        {
            this.functionStack.RemoveAt(0);
        }

        private void beginCoroutine()
        {
            this.functionStack.Insert(0, false);
        }

        private void endCoroutine()
        {
            this.functionStack.RemoveAt(0);
        }

        private bool isAllowReturn()
        {
            return this.functionStack.Count > 0;
        }

        private bool isInFunction()
        {
            return this.functionStack.Count > 0 && this.functionStack[0];
        }

        private void parse_program()
        {
            this.parse_sourceElements();
        }

        private void parse_sourceElements()
        {
            this.parse_sourceElement();
            while (this.getToken() != null)
            {
                if (this.isToken("}"))
                {
                    return;
                }
                this.parse_sourceElement();
            }
        }

        private void parse_sourceElement()
        {
            if (this.isToken("function"))
            {
                if (this.parseForceCoroutine == true)
                {
                    this.token.type = "coroutine";
                    this.parse_coroutineDeclaration();
                }
                else
                {
                    this.parse_functionDeclaration();
                }
            }
            else if (this.isToken("coroutine"))
            {
                this.parse_coroutineDeclaration();
            }
            else if (this.isStatementFirst(this.getToken() == null ? null : this.getToken().type))
            {
                this.parse_statement();
            }
            else
            {
                this.causeSyntaxError("SourceElement found an unexpected token");
            }
        }

        private void parse_functionDeclaration()
        {
            if (!this.isToken("function"))
            {
                this.causeSyntaxError("'function' not found in function declaration");
            }
            if (!this.isNextToken("identifier"))
            {
                this.causeSyntaxError("function name not found in function declaration");
            }
            string loc1 = Convert.ToString(this.getToken().value);
            Label loc2 = this.generator.putFunction();
            this.beginFunction();
            this.generator.beginNewScope();
            if (!this.isNextToken("("))
            {
                this.causeSyntaxError("'(' not found in function declaration");
            }
            if (this.isNextToken("identifier"))
            {
                this.parse_formalParameterList();
            }
            if (!this.isToken(")"))
            {
                this.causeSyntaxError("')' not found in function declaration");
            }
            if (!this.isNextToken("{"))
            {
                this.causeSyntaxError("'{' not found in function declaration");
            }
            if (!this.isNextToken("}"))
            {
                this.parse_functionBody();
            }
            if (!this.hasLastReturn)
            {
                this.generator.putReturnFunction(ExpressionResult.createLiteral(null));
            }
            if (!this.isToken("}"))
            {
                this.causeSyntaxError("'}' not found in function declaration");
            }
            this.endFunction();
            this.generator.closeScope();
            this.generator.setLabel(loc2);
            this.generator.putSetLocalVariable(loc1, ExpressionResult.createStack());
            this.generator.popAndDestroyStack();
            this.nextToken();
        }

        private void parse_functionExpression(ExpressionResult param1)
        {
            if (!this.isToken("function"))
            {
                this.causeSyntaxError("'function' not found in function expression");
            }
            string loc2 = null;
            if (this.isNextToken("identifier"))
            {
                loc2 = Convert.ToString(this.getToken().value);
                this.nextToken();
            }
            Label loc3 = this.generator.putFunction();
            this.beginFunction();
            this.generator.beginNewScope();
            if (!this.isToken("("))
            {
                this.causeSyntaxError("'(' not found in function expression");
            }
            if (this.isNextToken("identifier"))
            {
                this.parse_formalParameterList();
            }
            if (!this.isToken(")"))
            {
                this.causeSyntaxError("')' not found in function expression");
            }
            if (!this.isNextToken("{"))
            {
                this.causeSyntaxError("'{' not found in function expression");
            }
            if (!this.isNextToken("}"))
            {
                this.parse_functionBody();
            }
            if (!this.hasLastReturn)
            {
                this.generator.putReturnFunction(ExpressionResult.createLiteral(null));
            }
            if (!this.isToken("}"))
            {
                this.causeSyntaxError("'}' not found in function expression");
            }
            this.generator.closeScope();
            this.endFunction();
            this.generator.setLabel(loc3);
            if (loc2 != null)
            {
                this.generator.putSetLocalVariable(loc2, ExpressionResult.createStack());
            }
            param1.setTypeStack();
            this.nextToken();
        }

        private void parse_coroutineDeclaration()
        {
            if (!this.isToken("coroutine"))
            {
                this.causeSyntaxError("'coroutine' not found in coroutine declaration");
            }
            if (!this.isNextToken("identifier"))
            {
                this.causeSyntaxError("coroutine name not found in coroutine declaration");
            }
            string loc1 = Convert.ToString(this.getToken().value);
            Label loc2 = this.generator.putCoroutine();
            this.beginCoroutine();
            this.generator.beginNewScope();
            if (!this.isNextToken("("))
            {
                this.causeSyntaxError("'(' not found in coroutine declaration");
            }
            if (this.isNextToken("identifier"))
            {
                this.parse_formalParameterList();
            }
            if (!this.isToken(")"))
            {
                this.causeSyntaxError("')' not found in coroutine declaration");
            }
            if (!this.isNextToken("{"))
            {
                this.causeSyntaxError("'{' not found in coroutine declaration");
            }
            if (!this.isNextToken("}"))
            {
                this.parse_functionBody();
            }
            if (!this.hasLastReturn)
            {
                this.generator.putReturnCoroutine(ExpressionResult.createLiteral(null));
            }
            if (!this.isToken("}"))
            {
                this.causeSyntaxError("'}' not found in coroutine declaration");
            }
            this.generator.closeScope();
            this.endCoroutine();
            this.generator.setLabel(loc2);
            this.generator.putSetLocalVariable(loc1, ExpressionResult.createStack());
            this.generator.popAndDestroyStack();
            this.nextToken();
        }

        private void parse_coroutineExpression(ExpressionResult param1)
        {
            if (!this.isToken("coroutine"))
            {
                this.causeSyntaxError("'coroutine' not found in coroutine expression");
            }
            string loc2 = null;
            if (this.isNextToken("identifier"))
            {
                loc2 = Convert.ToString(this.getToken().value);
                this.nextToken();
            }
            Label loc3 = this.generator.putCoroutine();
            this.beginCoroutine();
            this.generator.beginNewScope();
            if (!this.isToken("("))
            {
                this.causeSyntaxError("'(' not found in coroutine expression");
            }
            if (this.isNextToken("identifier"))
            {
                this.parse_formalParameterList();
            }
            if (!this.isToken(")"))
            {
                this.causeSyntaxError("')' not found in coroutine expression");
            }
            if (!this.isNextToken("{"))
            {
                this.causeSyntaxError("'{' not found in coroutine expression");
            }
            if (!this.isNextToken("}"))
            {
                this.parse_functionBody();
            }
            if (!this.hasLastReturn)
            {
                this.generator.putReturnCoroutine(ExpressionResult.createLiteral(null));
            }
            if (!this.isToken("}"))
            {
                this.causeSyntaxError("'}' not found in coroutine expression");
            }
            this.generator.closeScope();
            this.endCoroutine();
            this.generator.setLabel(loc3);
            if (loc2 != null)
            {
                this.generator.putSetLocalVariable(loc2, ExpressionResult.createStack());
            }
            param1.setTypeStack();
            this.nextToken();
        }

        private void parse_formalParameterList()
        {
            int loc1 = 0;
            while (true)
            {
                if (!this.isToken("identifier"))
                {
                    this.causeSyntaxError("Parameter name is required");
                }
                this.generator.putArgument(loc1, Convert.ToString(this.getToken().value));
                loc1++;
                if (!this.isNextToken(","))
                {
                    break;
                }
                this.nextToken();
            }
        }

        private void parse_functionBody()
        {
            this.parse_sourceElements();
        }

        private void parse_statement()
        {
            this.hasLastReturn = false;
            double loc1 = this.generator.getStackLength();
            switch (this.getToken().type)
            {
                case "{":
                    this.parse_block();
                    break;
                case "var":
                    this.parse_variableStatement();
                    break;
                case ";":
                    this.parse_emptyStatement();
                    break;
                case "if":
                    this.parse_ifStatement();
                    break;
                case "do":
                case "while":
                case "for":
                    this.parse_iterationStatement();
                    break;
                case "continue":
                    this.parse_continueStatement();
                    break;
                case "break":
                    this.parse_breakStatement();
                    break;
                case "return":
                    this.parse_returnStatement();
                    break;
                case "with":
                    this.parse_withStatement();
                    break;
                case "switch":
                    this.parse_switchStatement();
                    break;
                case "yield":
                    this.parse_yieldStatement();
                    break;
                case "suspend":
                    this.parse_suspendStatement();
                    break;
                case "loop":
                    this.parse_loopStatement();
                    break;
                case "function":
                    this.causeSyntaxError("Functions are not defined in statements");
                    break;
                default:
                    if (this.isExpressionFirst(this.getToken().type))
                    {
                        this.parse_expressionStatement();
                        break;
                    }
                    this.causeSyntaxError("Unexpected statement token");
                    break;
            }
            this.generator.cleanUpStack(loc1);
        }

        private bool isStatementFirst(string param1)
        {
            return param1 == "{" || param1 == "var" || param1 == ";" || param1 == "if" || param1 == "do" || param1 == "while" || param1 == "for" || param1 == "for" || param1 == "continue" || param1 == "break" || param1 == "return" || param1 == "with" || param1 == "switch" || param1 == "yield" || param1 == "suspend" || param1 == "loop" || param1 != "function" && param1 != "coroutine" && this.isExpressionFirst(param1);
        }

        private void parse_block()
        {
            if (!this.isToken("{"))
            {
                this.causeSyntaxError("'{' not found in block");
            }
            if (!this.isNextToken("}"))
            {
                this.parse_statementList();
            }
            if (!this.isToken("}"))
            {
                this.causeSyntaxError("'}' not found in block");
            }
            this.nextToken();
        }

        private void parse_statementList()
        {
            this.parse_statement();
            while (!this.isToken("}"))
            {
                if (!this.isStatementFirst(this.getToken().type))
                {
                    return;
                }
                this.parse_statement();
            }
        }

        private void parse_variableStatement()
        {
            if (!this.isToken("var"))
            {
                this.causeSyntaxError("'var' not found in variable declaration");
            }
            this.nextToken();
            this.parse_variableDeclarationList();
            if (!this.isToken(";"))
            {
                this.causeSyntaxError("Variable declaration must end with ;");
            }
            this.nextToken();
        }

        private void parse_variableDeclarationList()
        {
            this.parse_variableDeclaration();
            while (this.isToken(","))
            {
                this.nextToken();
                this.parse_variableDeclaration();
            }
        }

        private void parse_variableDeclaration()
        {
            ExpressionResult loc2 = null;
            if (!this.isToken("identifier"))
            {
                this.causeSyntaxError("Variable name not found in variable declaration");
            }
            string loc1 = Convert.ToString(this.getToken().value);
            if (this.isNextToken("="))
            {
                loc2 = new ExpressionResult();
                this.parse_initialiser(loc2);
                this.generator.putExpressionResult(loc2);
                this.generator.putSetLocalVariable(loc1, loc2);
                this.generator.popAndDestroyStack();
            }
            else
            {
                this.generator.putSetLocalVariable(loc1, ExpressionResult.createLiteral(null));
                this.generator.popAndDestroyStack();
            }
        }

        private void parse_initialiser(ExpressionResult param1)
        {
            if (!this.isToken("="))
            {
                this.causeSyntaxError("'=' not found in variable initialization");
            }
            this.nextToken();
            this.parse_assignmentExpression(param1);
        }

        private void parse_emptyStatement()
        {
            if (!this.isToken(";"))
            {
                this.causeSyntaxError("';' not found in empty statement");
            }
            this.nextToken();
        }

        private void parse_expressionStatement()
        {
            if (this.isToken("{") || this.isToken("function"))
            {
                this.causeSyntaxError("Ambiguity found in ExpressionStatement");
            }
            ExpressionResult loc1 = new ExpressionResult();
            this.parse_expression(loc1);
            this.generator.putExpressionResult(loc1);
            if (!this.isToken(";"))
            {
                this.causeSyntaxError("';' not found in expression statement");
            }
            this.nextToken();
        }

        private void parse_ifStatement()
        {
            Label loc3 = null;
            ++this.lastReturnCaution;
            if (!this.isToken("if"))
            {
                this.causeSyntaxError("'if' not found in if statement");
            }
            if (!this.isNextToken("("))
            {
                this.causeSyntaxError("'(' not found in if statement");
            }
            this.nextToken();
            ExpressionResult loc1 = new ExpressionResult();
            this.parse_expression(loc1);
            if (!this.isToken(")"))
            {
                this.causeSyntaxError("')' not found in if statement");
            }
            this.generator.putExpressionResult(loc1);
            Label loc2 = new Label();
            this.generator.putIf(loc1, loc2);
            this.nextToken();
            this.parse_statement();
            if (this.isToken("else"))
            {
                loc3 = new Label();
                this.generator.putJump(loc3);
                this.generator.setLabel(loc2);
                this.nextToken();
                this.parse_statement();
                this.generator.setLabel(loc3);
            }
            else
            {
                this.generator.setLabel(loc2);
            }
            --this.lastReturnCaution;
        }

        private void parse_iterationStatement()
        {
            ++this.lastReturnCaution;
            if (this.isToken("for"))
            {
                this.parse_forStatement();
            }
            else if (this.isToken("while"))
            {
                this.parse_whileStatement();
            }
            else if (this.isToken("do"))
            {
                this.parse_doStatement();
            }
            else
            {
                this.causeSyntaxError("unexpected token found in loop statement");
            }
            --this.lastReturnCaution;
        }

        private void parse_forStatement()
        {
            object loc5 = null;
            ExpressionResult loc6 = null;
            ExpressionResult loc7 = null;
            ExpressionResult loc8 = null;
            if (!this.isNextToken("("))
            {
                this.causeSyntaxError("'(' not found in for statement");
            }
            Label loc1 = new Label();
            Label loc2 = new Label();
            Label loc3 = new Label();
            Label loc4 = new Label();
            this.pushBreakLabel(loc4);
            this.pushContinueLabel(loc2);
            if (this.isNextToken("var"))
            {
                this.nextToken();
                this.parse_variableDeclaration();
            }
            else if (!this.isToken(";"))
            {
                loc5 = this.generator.getStackLength();
                loc6 = new ExpressionResult();
                this.parse_expression(loc6);
                this.generator.putExpressionResult(loc6);
                this.generator.cleanUpStack(loc5);
            }
            if (!this.isToken(";"))
            {
                this.causeSyntaxError("';' not found in for statement");
            }
            this.generator.setLabel(loc1);
            if (!this.isNextToken(";"))
            {
                loc5 = this.generator.getStackLength();
                loc7 = new ExpressionResult();
                this.parse_expression(loc7);
                this.generator.putExpressionResult(loc7);
                this.generator.putIf(loc7, loc4);
                this.generator.cleanUpStack(loc5);
            }
            if (!this.isToken(";"))
            {
                this.causeSyntaxError("';' not found in for statement");
            }
            this.generator.putJump(loc3);
            this.generator.setLabel(loc2);
            if (!this.isNextToken(")"))
            {
                loc5 = this.generator.getStackLength();
                loc8 = new ExpressionResult();
                this.parse_expression(loc8);
                this.generator.putExpressionResult(loc8);
                this.generator.cleanUpStack(loc5);
            }
            this.generator.putJump(loc1);
            if (!this.isToken(")"))
            {
                this.causeSyntaxError("')' not found in for statement");
            }
            this.nextToken();
            this.generator.setLabel(loc3);
            this.parse_statement();
            this.generator.putJump(loc2);
            this.generator.setLabel(loc4);
            this.popContinueLabel();
            this.popBreakLabel();
        }

        private void parse_whileStatement()
        {
            if (!this.isNextToken("("))
            {
                this.causeSyntaxError("'(' not found in while statement");
            }
            this.nextToken();
            Label loc1 = new Label();
            Label loc2 = new Label();
            this.pushBreakLabel(loc2);
            this.pushContinueLabel(loc1);
            this.generator.setLabel(loc1);
            ExpressionResult loc3 = new ExpressionResult();
            this.parse_expression(loc3);
            this.generator.putExpressionResult(loc3);
            this.generator.putIf(loc3, loc2);
            if (!this.isToken(")"))
            {
                this.causeSyntaxError("')' not found in while statement");
            }
            this.nextToken();
            this.parse_statement();
            this.generator.putJump(loc1);
            this.generator.setLabel(loc2);
            this.popContinueLabel();
            this.popBreakLabel();
        }

        private void parse_doStatement()
        {
            this.nextToken();
            Label loc1 = new Label();
            Label loc2 = new Label();
            Label loc3 = new Label();
            this.pushBreakLabel(loc3);
            this.pushContinueLabel(loc2);
            this.generator.setLabel(loc1);
            this.parse_statement();
            if (!this.isToken("while"))
            {
                this.causeSyntaxError("'while' not found in do statement");
            }
            if (!this.isNextToken("("))
            {
                this.causeSyntaxError("'(' not found in do-while statement");
            }
            this.nextToken();
            this.generator.setLabel(loc2);
            ExpressionResult loc4 = new ExpressionResult();
            this.parse_expression(loc4);
            this.generator.putExpressionResult(loc4);
            this.generator.putIf(loc4, loc3);
            this.generator.putJump(loc1);
            if (!this.isToken(")"))
            {
                this.causeSyntaxError("')' not found in do-while statement");
            }
            this.generator.setLabel(loc3);
            this.popContinueLabel();
            this.popBreakLabel();
            this.nextToken();
        }

        private void parse_continueStatement()
        {
            if (!this.isToken("continue"))
            {
                this.causeSyntaxError("'continue' not found in continue statement");
            }
            if (!this.isNextToken(";"))
            {
                this.causeSyntaxError("';' not found in continue statement");
            }
            this.generator.putJump(this.getContinueLabel());
            this.nextToken();
        }

        private void parse_breakStatement()
        {
            if (!this.isToken("break"))
            {
                this.causeSyntaxError("'break' not found in break statement");
            }
            if (!this.isNextToken(";"))
            {
                this.causeSyntaxError("';' not found in break statement");
            }
            this.generator.putJump(this.getBreakLabel());
            this.nextToken();
        }

        private void parse_returnStatement()
        {
            ExpressionResult loc1 = null;
            if (!this.isToken("return"))
            {
                this.causeSyntaxError("'return' not found in return statement");
            }
            if (!this.isAllowReturn())
            {
                this.causeSyntaxError("return is only used in functions or coroutines");
            }
            if (this.lastReturnCaution == 0)
            {
                this.hasLastReturn = true;
            }
            Token next = this.nextToken();
            if (this.isExpressionFirst(next == null ? null : next.type))
            {
                loc1 = new ExpressionResult();
                this.parse_expression(loc1);
                this.generator.putExpressionResult(loc1);
            }
            else
            {
                loc1 = ExpressionResult.createLiteral(null);
            }
            if (this.isInFunction())
            {
                this.generator.putReturnFunction(loc1);
            }
            else
            {
                this.generator.putReturnCoroutine(loc1);
            }
            if (!this.isToken(";"))
            {
                this.causeSyntaxError("';' not found in return statement");
            }
            this.nextToken();
        }

        private void parse_withStatement()
        {
            if (!this.isToken("with"))
            {
                this.causeSyntaxError("'with' not found in with statement");
            }
            if (!this.isNextToken("("))
            {
                this.causeSyntaxError("'(' not found in with statement");
            }
            throw new Exception("不能使用 with!");
        }

        private void parse_switchStatement()
        {
            if (!this.isToken("switch"))
            {
                this.causeSyntaxError("'switch' not found in switch statement");
            }
            if (!this.isNextToken("("))
            {
                this.causeSyntaxError("'(' not found in switch statement");
            }
            this.nextToken();
            ExpressionResult loc1 = new ExpressionResult();
            this.parse_expression(loc1);
            this.generator.putExpressionResult(loc1);
            if (!this.isToken(")"))
            {
                this.causeSyntaxError("')' not found in switch statement");
            }
            Label loc2 = new Label();
            this.pushBreakLabel(loc2);
            this.nextToken();
            this.parse_caseBlock(loc1);
            this.generator.setLabel(loc2);
            this.popBreakLabel();
        }

        private void parse_caseBlock(ExpressionResult param1)
        {
            Label loc4 = null;
            if (!this.isToken("{"))
            {
                this.causeSyntaxError("'{' not found in switch-case statement");
            }
            Label loc2 = new Label();
            Label loc3 = new Label();
            if (this.isNextToken("case"))
            {
                this.parse_caseClauses(param1, loc2, loc3);
            }
            if (this.isToken("default"))
            {
                loc4 = new Label();
                this.generator.setLabel(loc4);
                this.parse_defaultClause(loc3);
                if (this.isToken("case"))
                {
                    this.parse_caseClauses(param1, loc2, loc3);
                }
                this.generator.setLabelAddress(loc2, loc4.address);
                this.generator.setLabel(loc3);
            }
            else
            {
                this.generator.setLabel(loc2);
                this.generator.setLabel(loc3);
            }
            if (!this.isToken("}"))
            {
                this.causeSyntaxError("'}' not found in switch-case statement");
            }
            this.nextToken();
        }

        private void parse_caseClauses(ExpressionResult param1, Label param2, Label param3)
        {
            while (this.isToken("case"))
            {
                this.parse_caseClause(param1, param2, param3);
            }
        }

        private void parse_caseClause(ExpressionResult param1, Label param2, Label param3)
        {
            if (!this.isToken("case"))
            {
                this.causeSyntaxError("'case' not found in case statement");
            }
            this.generator.setLabel(param2);
            param2.initialize();
            if (!param1.isLiteral())
            {
                this.generator.putDuplicate(param1);
            }
            this.nextToken();
            ExpressionResult loc4 = new ExpressionResult();
            this.parse_expression(loc4);
            this.generator.putExpressionResult(loc4);
            this.generator.putBinaryOperation(this.vmtarget == null ? "CSEQ" : CodeGenerator.VmOp(this.vmtarget, "CSEQ"), param1, loc4);
            this.generator.putIf(ExpressionResult.createStack(), param2);
            if (!this.isToken(":"))
            {
                this.causeSyntaxError("':' not found in case statement");
            }
            this.generator.setLabel(param3);
            param3.initialize();
            Token next = this.nextToken();
            if (this.isStatementFirst(next == null ? null : next.type))
            {
                this.parse_statementList();
            }
            this.generator.putJump(param3);
        }

        private void parse_defaultClause(Label param1)
        {
            if (!this.isToken("default"))
            {
                this.causeSyntaxError("'default' not found in default statement");
            }
            if (!this.isNextToken(":"))
            {
                this.causeSyntaxError("':' not found in default statement");
            }
            this.generator.setLabel(param1);
            param1.initialize();
            Token next = this.nextToken();
            if (this.isStatementFirst(next == null ? null : next.type))
            {
                this.parse_statementList();
            }
            this.generator.putJump(param1);
        }

        private void parse_yieldStatement()
        {
            if (!this.isToken("yield"))
            {
                this.causeSyntaxError("'yield' not found in yield statement");
            }
            if (!this.isNextToken(";"))
            {
                this.causeSyntaxError("';' not found in yield statement");
            }
            if (this.isInFunction())
            {
                this.causeSyntaxError("yield statement can only be used in a coroutine");
            }
            this.generator.putSuspend();
            this.nextToken();
        }

        private void parse_suspendStatement()
        {
            if (!this.isToken("suspend"))
            {
                this.causeSyntaxError("'suspend' not found in suspend statement");
            }
            if (!this.isNextToken(";"))
            {
                this.causeSyntaxError("';' not found in suspend statement");
            }
            if (this.isInFunction())
            {
                this.causeSyntaxError("suspend statement can only be used in a coroutine");
            }
            this.generator.putSuspend();
            this.nextToken();
        }

        private void parse_loopStatement()
        {
            if (!this.isToken("loop"))
            {
                this.causeSyntaxError("'loop' not found in loop statement");
            }
            this.nextToken();
            Label loc1 = new Label();
            Label loc2 = new Label();
            this.pushBreakLabel(loc2);
            this.pushContinueLabel(loc1);
            this.generator.setLabel(loc1);
            this.parse_statement();
            this.generator.putJump(loc1);
            this.generator.setLabel(loc2);
            this.popContinueLabel();
            this.popBreakLabel();
        }

        private void parse_expression(ExpressionResult param1)
        {
            this.parse_assignmentExpression(param1);
            while (this.isToken(","))
            {
                this.nextToken();
                this.generator.putExpressionResult(param1);
                this.generator.popAndDestroyStack();
                param1.initialize();
                this.parse_assignmentExpression(param1);
            }
        }

        private bool isExpressionFirst(string param1)
        {
            return this.isUnaryExpressionFirst(param1);
        }

        private bool areBothLiteral(ExpressionResult param1, ExpressionResult param2)
        {
            return param1.isType("literal") && param2.isType("literal");
        }

        private void parse_assignmentExpression(ExpressionResult param1)
        {
            ExpressionResult loc2 = null;
            ExpressionResult loc3 = null;
            ExpressionResult loc4 = null;
            object loc5 = null;
            object loc6 = null;
            string loc7 = null;
            this.parse_conditionalExpression(param1);
            switch (this.getToken().type)
            {
                case "=":
                    this.nextToken();
                    switch (param1.type)
                    {
                        case "member":
                            loc3 = param1.getObjectExpression();
                            loc4 = param1.getMemberExpression();
                            this.generator.putExpressionResult(loc4);
                            loc2 = new ExpressionResult();
                            this.parse_assignmentExpression(loc2);
                            this.generator.putExpressionResult(loc2);
                            this.generator.putSetMember(loc3, loc4, loc2);
                            break;
                        case "variable":
                            loc2 = new ExpressionResult();
                            this.parse_assignmentExpression(loc2);
                            this.generator.putExpressionResult(loc2);
                            this.generator.putSetVariable(Convert.ToString(param1.value), loc2);
                            break;
                        default:
                            this.causeSyntaxError("L-value must be a variable or property");
                            break;
                    }
                    param1.setTypeStack();
                    break;
                case "*=":
                case "/=":
                case "%=":
                case "+=":
                case "-=":
                case "<<=":
                case ">>=":
                case ">>>=":
                case "&=":
                case "^=":
                case "|=":
                    switch (this.getToken().type)
                    {
                        case "*=":
                            loc5 = this.vmtarget == null ? "MUL" : CodeGenerator.VmOp(this.vmtarget, "MUL");
                            break;
                        case "/=":
                            loc5 = this.vmtarget == null ? "DIV" : CodeGenerator.VmOp(this.vmtarget, "DIV");
                            break;
                        case "%=":
                            loc5 = this.vmtarget == null ? "MOD" : CodeGenerator.VmOp(this.vmtarget, "MOD");
                            break;
                        case "+=":
                            loc5 = this.vmtarget == null ? "ADD" : CodeGenerator.VmOp(this.vmtarget, "ADD");
                            break;
                        case "-=":
                            loc5 = this.vmtarget == null ? "SUB" : CodeGenerator.VmOp(this.vmtarget, "SUB");
                            break;
                        case "<<=":
                            loc5 = this.vmtarget == null ? "LSH" : CodeGenerator.VmOp(this.vmtarget, "LSH");
                            break;
                        case ">>=":
                            loc5 = this.vmtarget == null ? "RSH" : CodeGenerator.VmOp(this.vmtarget, "RSH");
                            break;
                        case ">>>=":
                            loc5 = this.vmtarget == null ? "URSH" : CodeGenerator.VmOp(this.vmtarget, "URSH");
                            break;
                        case "&=":
                            loc5 = this.vmtarget == null ? "AND" : CodeGenerator.VmOp(this.vmtarget, "AND");
                            break;
                        case "^=":
                            loc5 = this.vmtarget == null ? "XOR" : CodeGenerator.VmOp(this.vmtarget, "XOR");
                            break;
                        case "|=":
                            loc5 = this.vmtarget == null ? "OR" : CodeGenerator.VmOp(this.vmtarget, "OR");
                            break;
                    }
                    this.nextToken();
                    switch (param1.type)
                    {
                        case "member":
                            loc3 = param1.getObjectExpression();
                            loc4 = param1.getMemberExpression();
                            if (!loc4.isLiteral())
                            {
                                this.generator.putExpressionResult(loc4);
                                loc6 = this.generator.popStack();
                                this.generator.putDuplicate(loc4);
                                this.generator.pushStack(loc6);
                                this.generator.putDuplicate(loc3);
                                this.generator.swapStack(1, 2);
                            }
                            else
                            {
                                this.generator.putDuplicate(loc3);
                            }
                            this.generator.putGetMember(loc3, loc4);
                            param1.setTypeStack();
                            loc2 = new ExpressionResult();
                            this.parse_assignmentExpression(loc2);
                            this.generator.putExpressionResult(loc2);
                            this.generator.putBinaryOperation(loc5, param1, loc2);
                            this.generator.putSetMember(loc3, loc4, param1);
                            break;
                        case "variable":
                            loc7 = Convert.ToString(param1.value);
                            this.generator.putGetVariable(loc7);
                            param1.setTypeStack();
                            loc2 = new ExpressionResult();
                            this.parse_assignmentExpression(loc2);
                            this.generator.putExpressionResult(loc2);
                            this.generator.putBinaryOperation(loc5, param1, loc2);
                            this.generator.putSetVariable(loc7, param1);
                            break;
                        default:
                            this.causeSyntaxError("L-value must be a variable or property");
                            break;
                    }
                    param1.setTypeStack();
                    break;
            }
        }

        private void parse_conditionalExpression(ExpressionResult param1)
        {
            Label loc2 = null;
            object loc3 = null;
            Label loc4 = null;
            this.parse_logicalORExpression(param1);
            if (this.isToken("?"))
            {
                this.generator.putExpressionResult(param1);
                loc2 = new Label();
                this.generator.putIf(param1, loc2);
                this.nextToken();
                param1.initialize();
                this.parse_assignmentExpression(param1);
                if (param1.isType("literal"))
                {
                    this.generator.putLiteral(param1);
                }
                else
                {
                    this.generator.putExpressionResult(param1);
                }
                loc3 = this.generator.popStack();
                loc4 = new Label();
                this.generator.putJump(loc4);
                this.generator.setLabel(loc2);
                if (!this.isToken(":"))
                {
                    this.causeSyntaxError("':' not found in ?: statement");
                }
                this.nextToken();
                param1.initialize();
                this.parse_assignmentExpression(param1);
                if (param1.isType("literal"))
                {
                    this.generator.putLiteral(param1);
                }
                else
                {
                    this.generator.putExpressionResult(param1);
                }
                param1.setType("stack");
                this.generator.setStackPatch(loc3);
                this.generator.setLabel(loc4);
            }
        }

        private void parse_logicalORExpression(ExpressionResult param1)
        {
            object loc2 = null;
            Label loc3 = null;
            this.parse_logicalANDExpression(param1);
            while (this.isToken("||"))
            {
                this.nextToken();
                this.generator.putExpressionResult(param1);
                this.generator.putDuplicate(param1);
                loc2 = this.generator.popStack();
                param1.setType("stack");
                loc3 = new Label();
                this.generator.putNif(param1, loc3);
                param1.initialize();
                this.parse_logicalANDExpression(param1);
                if (param1.isType("literal"))
                {
                    this.generator.putLiteral(param1);
                }
                else
                {
                    this.generator.putExpressionResult(param1);
                }
                this.generator.setStackPatch(loc2);
                param1.setType("stack");
                this.generator.setLabel(loc3);
            }
        }

        private void parse_logicalANDExpression(ExpressionResult param1)
        {
            object loc2 = null;
            Label loc3 = null;
            this.parse_bitwiseORExpression(param1);
            while (this.isToken("&&"))
            {
                this.nextToken();
                this.generator.putExpressionResult(param1);
                this.generator.putDuplicate(param1);
                loc2 = this.generator.popStack();
                param1.setType("stack");
                loc3 = new Label();
                this.generator.putIf(param1, loc3);
                param1.initialize();
                this.parse_bitwiseORExpression(param1);
                if (param1.isType("literal"))
                {
                    this.generator.putLiteral(param1);
                }
                else
                {
                    this.generator.putExpressionResult(param1);
                }
                this.generator.setStackPatch(loc2);
                param1.setType("stack");
                this.generator.setLabel(loc3);
            }
        }

        private void parse_bitwiseORExpression(ExpressionResult param1)
        {
            string loc2 = null;
            ExpressionResult loc3 = null;
            this.parse_bitwiseXORExpression(param1);
            while (this.isToken("|"))
            {
                loc2 = this.getToken().type;
                this.nextToken();
                this.generator.putExpressionResult(param1);
                loc3 = new ExpressionResult();
                this.parse_bitwiseXORExpression(loc3);
                this.generator.putExpressionResult(loc3);
                if (this.areBothLiteral(param1, loc3))
                {
                    param1.setValue(BitwiseOr(param1.value, loc3.value));
                }
                else
                {
                    this.generator.putBinaryOperation(this.vmtarget == null ? "OR" : CodeGenerator.VmOp(this.vmtarget, "OR"), param1, loc3);
                    param1.setType("stack");
                }
            }
        }

        private void parse_bitwiseXORExpression(ExpressionResult param1)
        {
            string loc2 = null;
            ExpressionResult loc3 = null;
            this.parse_bitwiseANDExpression(param1);
            while (this.isToken("^"))
            {
                loc2 = this.getToken().type;
                this.nextToken();
                this.generator.putExpressionResult(param1);
                loc3 = new ExpressionResult();
                this.parse_bitwiseANDExpression(loc3);
                this.generator.putExpressionResult(loc3);
                if (this.areBothLiteral(param1, loc3))
                {
                    param1.setValue(BitwiseXor(param1.value, loc3.value));
                }
                else
                {
                    this.generator.putBinaryOperation(this.vmtarget == null ? "XOR" : CodeGenerator.VmOp(this.vmtarget, "XOR"), param1, loc3);
                    param1.setType("stack");
                }
            }
        }

        private void parse_bitwiseANDExpression(ExpressionResult param1)
        {
            string loc2 = null;
            ExpressionResult loc3 = null;
            this.parse_equalityExpression(param1);
            while (this.isToken("&"))
            {
                loc2 = this.getToken().type;
                this.nextToken();
                this.generator.putExpressionResult(param1);
                loc3 = new ExpressionResult();
                this.parse_equalityExpression(loc3);
                this.generator.putExpressionResult(loc3);
                if (this.areBothLiteral(param1, loc3))
                {
                    param1.setValue(BitwiseAnd(param1.value, loc3.value));
                }
                else
                {
                    this.generator.putBinaryOperation(this.vmtarget == null ? "AND" : CodeGenerator.VmOp(this.vmtarget, "AND"), param1, loc3);
                    param1.setType("stack");
                }
            }
        }

        private void parse_equalityExpression(ExpressionResult param1)
        {
            string loc2 = null;
            ExpressionResult loc3 = null;
            this.parse_relationalExpression(param1);
            while (this.isToken("==") || this.isToken("!=") || this.isToken("===") || this.isToken("!=="))
            {
                loc2 = this.getToken().type;
                this.nextToken();
                this.generator.putExpressionResult(param1);
                loc3 = new ExpressionResult();
                this.parse_relationalExpression(loc3);
                this.generator.putExpressionResult(loc3);
                if (this.areBothLiteral(param1, loc3))
                {
                    switch (loc2)
                    {
                        case "==":
                            param1.setValue(LooseEquals(param1.value, loc3.value));
                            break;
                        case "!=":
                            param1.setValue(!LooseEquals(param1.value, loc3.value));
                            break;
                        case "===":
                            param1.setValue(StrictEquals(param1.value, loc3.value));
                            break;
                        case "!==":
                            param1.setValue(!StrictEquals(param1.value, loc3.value));
                            break;
                    }
                }
                else
                {
                    switch (loc2)
                    {
                        case "==":
                            this.generator.putBinaryOperation(this.vmtarget == null ? "CEQ" : CodeGenerator.VmOp(this.vmtarget, "CEQ"), param1, loc3);
                            break;
                        case "!=":
                            this.generator.putBinaryOperation(this.vmtarget == null ? "CNE" : CodeGenerator.VmOp(this.vmtarget, "CNE"), param1, loc3);
                            break;
                        case "===":
                            this.generator.putBinaryOperation(this.vmtarget == null ? "CSEQ" : CodeGenerator.VmOp(this.vmtarget, "CSEQ"), param1, loc3);
                            break;
                        case "!==":
                            this.generator.putBinaryOperation(this.vmtarget == null ? "CSNE" : CodeGenerator.VmOp(this.vmtarget, "CSNE"), param1, loc3);
                            break;
                    }
                    param1.setType("stack");
                }
            }
        }

        private void parse_relationalExpression(ExpressionResult param1)
        {
            string loc2 = null;
            ExpressionResult loc3 = null;
            this.parse_shiftExpression(param1);
            while (this.isToken("<") || this.isToken(">") || this.isToken("<=") || this.isToken(">=") || this.isToken("instanceof"))
            {
                loc2 = this.getToken().type;
                this.nextToken();
                this.generator.putExpressionResult(param1);
                loc3 = new ExpressionResult();
                this.parse_shiftExpression(loc3);
                this.generator.putExpressionResult(loc3);
                if (this.areBothLiteral(param1, loc3))
                {
                    param1.setValue(CompareValues(param1.value, loc3.value, loc2));
                }
                else
                {
                    switch (loc2)
                    {
                        case "<":
                            this.generator.putBinaryOperation(this.vmtarget == null ? "CLT" : CodeGenerator.VmOp(this.vmtarget, "CLT"), param1, loc3);
                            break;
                        case ">":
                            this.generator.putBinaryOperation(this.vmtarget == null ? "CGT" : CodeGenerator.VmOp(this.vmtarget, "CGT"), param1, loc3);
                            break;
                        case "<=":
                            this.generator.putBinaryOperation(this.vmtarget == null ? "CLE" : CodeGenerator.VmOp(this.vmtarget, "CLE"), param1, loc3);
                            break;
                        case ">=":
                            this.generator.putBinaryOperation(this.vmtarget == null ? "CGE" : CodeGenerator.VmOp(this.vmtarget, "CGE"), param1, loc3);
                            break;
                        case "instanceof":
                            this.generator.putBinaryOperation(this.vmtarget == null ? "INSOF" : CodeGenerator.VmOp(this.vmtarget, "INSOF"), param1, loc3);
                            break;
                    }
                    param1.setType("stack");
                }
            }
        }

        private void parse_shiftExpression(ExpressionResult param1)
        {
            string loc2 = null;
            ExpressionResult loc3 = null;
            this.parse_additiveExpression(param1);
            while (this.isToken("<<") || this.isToken(">>") || this.isToken(">>>"))
            {
                loc2 = this.getToken().type;
                this.nextToken();
                this.generator.putExpressionResult(param1);
                loc3 = new ExpressionResult();
                this.parse_additiveExpression(loc3);
                this.generator.putExpressionResult(loc3);
                if (this.areBothLiteral(param1, loc3))
                {
                    param1.setValue(ShiftValues(param1.value, loc3.value, loc2));
                }
                else
                {
                    switch (loc2)
                    {
                        case "<<":
                            this.generator.putBinaryOperation(this.vmtarget == null ? "LSH" : CodeGenerator.VmOp(this.vmtarget, "LSH"), param1, loc3);
                            break;
                        case ">>":
                            this.generator.putBinaryOperation(this.vmtarget == null ? "RSH" : CodeGenerator.VmOp(this.vmtarget, "RSH"), param1, loc3);
                            break;
                        case ">>>":
                            this.generator.putBinaryOperation(this.vmtarget == null ? "URSH" : CodeGenerator.VmOp(this.vmtarget, "URSH"), param1, loc3);
                            break;
                    }
                    param1.setType("stack");
                }
            }
        }

        private void parse_additiveExpression(ExpressionResult param1)
        {
            string loc2 = null;
            ExpressionResult loc3 = null;
            this.parse_multiplicativeExpression(param1);
            while (this.isToken("+") || this.isToken("-"))
            {
                loc2 = this.getToken().type;
                this.nextToken();
                this.generator.putExpressionResult(param1);
                loc3 = new ExpressionResult();
                this.parse_multiplicativeExpression(loc3);
                this.generator.putExpressionResult(loc3);
                if (this.areBothLiteral(param1, loc3))
                {
                    switch (loc2)
                    {
                        case "+":
                            param1.setValue(AddValues(param1.value, loc3.value));
                            break;
                        case "-":
                            param1.setValue(SubtractValues(param1.value, loc3.value));
                            break;
                    }
                }
                else
                {
                    switch (loc2)
                    {
                        case "+":
                            this.generator.putBinaryOperation(this.vmtarget == null ? "ADD" : CodeGenerator.VmOp(this.vmtarget, "ADD"), param1, loc3);
                            break;
                        case "-":
                            this.generator.putBinaryOperation(this.vmtarget == null ? "SUB" : CodeGenerator.VmOp(this.vmtarget, "SUB"), param1, loc3);
                            break;
                    }
                    param1.setType("stack");
                }
            }
        }

        private void parse_multiplicativeExpression(ExpressionResult param1)
        {
            string loc2 = null;
            ExpressionResult loc3 = null;
            this.parse_unaryExpression(param1);
            while (this.isToken("*") || this.isToken("/") || this.isToken("%"))
            {
                loc2 = this.getToken().type;
                this.nextToken();
                this.generator.putExpressionResult(param1);
                loc3 = new ExpressionResult();
                this.parse_unaryExpression(loc3);
                this.generator.putExpressionResult(loc3);
                if (this.areBothLiteral(param1, loc3))
                {
                    switch (loc2)
                    {
                        case "*":
                            param1.setValue(MultiplyValues(param1.value, loc3.value));
                            break;
                        case "/":
                            param1.setValue(DivideValues(param1.value, loc3.value));
                            break;
                        case "%":
                            param1.setValue(RemainderValues(param1.value, loc3.value));
                            break;
                    }
                }
                else
                {
                    switch (loc2)
                    {
                        case "*":
                            this.generator.putBinaryOperation(this.vmtarget == null ? "MUL" : CodeGenerator.VmOp(this.vmtarget, "MUL"), param1, loc3);
                            break;
                        case "/":
                            this.generator.putBinaryOperation(this.vmtarget == null ? "DIV" : CodeGenerator.VmOp(this.vmtarget, "DIV"), param1, loc3);
                            break;
                        case "%":
                            this.generator.putBinaryOperation(this.vmtarget == null ? "MOD" : CodeGenerator.VmOp(this.vmtarget, "MOD"), param1, loc3);
                            break;
                    }
                    param1.setType("stack");
                }
            }
        }

        private void parse_unaryExpression(ExpressionResult param1)
        {
            ExpressionResult loc2 = null;
            ExpressionResult loc3 = null;
            ExpressionResult loc4 = null;
            switch (this.getToken().type)
            {
                case "delete":
                    this.nextToken();
                    this.parse_unaryExpression(param1);
                    switch (param1.type)
                    {
                        case "member":
                            loc2 = param1.getObjectExpression();
                            loc3 = param1.getMemberExpression();
                            this.generator.putExpressionResult(loc3);
                            this.generator.putDeleteMember(loc2, loc3);
                            break;
                        case "variable":
                            this.generator.putDelete(param1);
                            break;
                        default:
                            this.generator.putExpressionResult(param1);
                            this.generator.putDelete(param1);
                            break;
                    }
                    param1.setTypeStack();
                    break;
                case "void":
                    this.nextToken();
                    this.parse_unaryExpression(param1);
                    break;
                case "typeof":
                    this.nextToken();
                    this.parse_unaryExpression(param1);
                    this.generator.putExpressionResult(param1);
                    if (param1.isType("literal"))
                    {
                        param1.setValue(TypeOf(param1.value));
                    }
                    else
                    {
                        this.generator.putUnaryOperation(this.vmtarget == null ? "TYPEOF" : CodeGenerator.VmOp(this.vmtarget, "TYPEOF"), param1);
                        param1.setTypeStack();
                    }
                    break;
                case "++":
                    this.nextToken();
                    this.parse_unaryExpression(param1);
                    this.generator.putIncrement(param1);
                    param1.setTypeStack();
                    break;
                case "--":
                    this.nextToken();
                    this.parse_unaryExpression(param1);
                    this.generator.putDecrement(param1);
                    param1.setTypeStack();
                    break;
                case "+":
                    this.nextToken();
                    this.parse_unaryExpression(param1);
                    break;
                case "-":
                    this.nextToken();
                    this.parse_unaryExpression(param1);
                    this.generator.putExpressionResult(param1);
                    if (param1.isType("literal"))
                    {
                        param1.setValue(NegateValue(param1.value));
                    }
                    else
                    {
                        loc4 = new ExpressionResult();
                        loc4.setTypeAndValue("literal", 0);
                        this.generator.putBinaryOperation(this.vmtarget == null ? "SUB" : CodeGenerator.VmOp(this.vmtarget, "SUB"), loc4, param1);
                        param1.setType("stack");
                    }
                    break;
                case "~":
                    this.nextToken();
                    this.parse_unaryExpression(param1);
                    this.generator.putExpressionResult(param1);
                    if (param1.isType("literal"))
                    {
                        param1.setValue(BitwiseNot(param1.value));
                    }
                    else
                    {
                        this.generator.putUnaryOperation(this.vmtarget == null ? "NOT" : CodeGenerator.VmOp(this.vmtarget, "NOT"), param1);
                        param1.setType("stack");
                    }
                    break;
                case "!":
                    this.nextToken();
                    this.parse_unaryExpression(param1);
                    this.generator.putExpressionResult(param1);
                    if (param1.isType("literal"))
                    {
                        param1.setValue(!IsTruthy(param1.value));
                    }
                    else
                    {
                        this.generator.putUnaryOperation(this.vmtarget == null ? "LNOT" : CodeGenerator.VmOp(this.vmtarget, "LNOT"), param1);
                        param1.setType("stack");
                    }
                    break;
                default:
                    this.parse_postfixExpression(param1);
                    break;
            }
        }

        private bool isUnaryExpressionFirst(string param1)
        {
            return param1 == "delete" || param1 == "void" || param1 == "typeof" || param1 == "++" || param1 == "--" || param1 == "+" || param1 == "-" || param1 == "~" || param1 == "!" || this.isMemberExpressionFirst(param1);
        }

        private void parse_postfixExpression(ExpressionResult param1)
        {
            this.parse_leftHandSideExpression(param1);
            if (this.isToken("++") || this.isToken("--"))
            {
                switch (this.getToken().type)
                {
                    case "++":
                        this.generator.putPostfixIncrement(param1);
                        break;
                    case "--":
                        this.generator.putPostfixDecrement(param1);
                        break;
                }
                param1.setTypeStack();
                this.nextToken();
            }
        }

        private void parse_leftHandSideExpression(ExpressionResult param1)
        {
            this.parse_callExpression(param1);
        }

        private void parse_callExpression(ExpressionResult param1)
        {
            double loc2 = 0;
            ExpressionResult loc3 = null;
            ExpressionResult loc4 = null;
            this.parse_memberExpression(param1);
            while (this.isToken("("))
            {
                loc2 = this.parse_arguments();
                switch (param1.type)
                {
                    case "member":
                        loc3 = param1.getObjectExpression();
                        loc4 = param1.getMemberExpression();
                        this.generator.putExpressionResult(loc4);
                        this.generator.putCallMember(loc3, loc4, loc2);
                        break;
                    case "stack":
                        this.generator.putCallFunctor(loc2);
                        break;
                    default:
                        this.generator.putCall(param1, loc2);
                        break;
                }
                param1.setType("stack");
            }
        }

        private void parse_memberExpression(ExpressionResult param1)
        {
            double loc2 = 0;
            ExpressionResult loc3 = null;
            switch (this.getToken().type)
            {
                case "function":
                    if (this.parseForceCoroutine == false)
                    {
                        this.parse_functionExpression(param1);
                    }
                    else
                    {
                        this.token.type = "coroutine";
                        this.parse_coroutineExpression(param1);
                    }
                    break;
                case "coroutine":
                    this.parse_coroutineExpression(param1);
                    break;
                case "new":
                    this.nextToken();
                    this.parse_memberExpression(param1);
                    this.generator.putExpressionResult(param1);
                    loc2 = 0;
                    if (this.isToken("("))
                    {
                        loc2 += this.parse_arguments();
                    }
                    this.generator.putNew(loc2);
                    param1.setType("stack");
                    break;
                default:
                    this.parse_primaryExpression(param1);
                    break;
            }
            while (true)
            {
                if (this.isToken("["))
                {
                    this.nextToken();
                    this.generator.putExpressionResult(param1);
                    loc3 = new ExpressionResult();
                    this.parse_expression(loc3);
                    param1.setTypeMember(param1.clone(), loc3);
                    if (!this.isToken("]"))
                    {
                        this.causeSyntaxError("']' not found in array access");
                    }
                    this.nextToken();
                }
                else
                {
                    if (!this.isToken("."))
                    {
                        break;
                    }
                    this.generator.putExpressionResult(param1);
                    if (!this.isNextToken("identifier"))
                    {
                        this.causeSyntaxError("'.' not found in property access");
                    }
                    param1.setTypeMember(param1.clone(), ExpressionResult.createLiteral(this.getToken().value));
                    this.nextToken();
                }
            }
        }

        private bool isMemberExpressionFirst(string param1)
        {
            return param1 == "new" || param1 == "function" || this.isPrimaryExpressionFirst(param1);
        }

        private double parse_arguments()
        {
            if (!this.isToken("("))
            {
                this.causeSyntaxError("'(' not found in argument list");
            }
            double loc1 = 0;
            if (!this.isNextToken(")"))
            {
                loc1 += this.parse_argumentList();
            }
            if (!this.isToken(")"))
            {
                this.causeSyntaxError("')' not found in argument list");
            }
            this.nextToken();
            return loc1;
        }

        private double parse_argumentList()
        {
            ExpressionResult loc2 = null;
            double loc1 = 0;
            while (true)
            {
                loc2 = new ExpressionResult();
                this.parse_assignmentExpression(loc2);
                this.generator.putExpressionResult(loc2);
                this.generator.putPush(loc2);
                loc1++;
                if (!this.isToken(","))
                {
                    break;
                }
                this.nextToken();
            }
            return loc1;
        }

        private void parse_primaryExpression(ExpressionResult param1)
        {
            double loc2;
            double loc3;
            switch (this.getToken().type)
            {
                case "this":
                    this.generator.putThis();
                    param1.setType("stack");
                    this.nextToken();
                    break;
                case "identifier":
                    param1.setTypeAndValue("variable", this.getToken().value);
                    this.nextToken();
                    break;
                case "string":
                case "number":
                case "bool":
                case "null":
                case "undefined":
                    param1.setTypeAndValue("literal", this.getToken().value);
                    this.nextToken();
                    break;
                case "[":
                    loc2 = this.parse_arrayLiteral();
                    this.generator.putArrayLiteral(loc2);
                    param1.setType("stack");
                    break;
                case "{":
                    loc3 = this.parse_objectLiteral();
                    this.generator.putObjectLiteral(loc3);
                    param1.setType("stack");
                    break;
                case "(":
                    this.nextToken();
                    this.parse_expression(param1);
                    if (!this.isToken(")"))
                    {
                        this.causeSyntaxError("matching ')' not found in expression");
                    }
                    this.nextToken();
                    break;
                default:
                    this.causeSyntaxError("unexpected token");
                    break;
            }
        }

        private bool isPrimaryExpressionFirst(string param1)
        {
            return param1 == "this" || param1 == "identifier" || param1 == "string" || param1 == "number" || param1 == "bool" || param1 == "undefined" || param1 == "null" || param1 == "{" || param1 == "[" || param1 == "(";
        }

        private double parse_arrayLiteral()
        {
            if (!this.isToken("["))
            {
                this.causeSyntaxError("'[' not found in array initializer");
            }
            double loc1 = 0;
            if (!this.isNextToken("]"))
            {
                if (this.isToken(","))
                {
                    loc1 += this.parse_elision();
                }
                if (!this.isToken("]"))
                {
                    loc1 += this.parse_elementList();
                }
                if (this.isToken(","))
                {
                    loc1 += this.parse_elision();
                }
            }
            if (!this.isToken("]"))
            {
                this.causeSyntaxError("']' not found in array initializer");
            }
            this.nextToken();
            return loc1;
        }

        private double parse_elision()
        {
            if (!this.isToken(","))
            {
                this.causeSyntaxError("',' not found in elision");
            }
            double loc1 = 1;
            ExpressionResult loc2 = ExpressionResult.createLiteral(null);
            do
            {
                this.generator.putPush(loc2);
                loc1++;
            }
            while (this.isNextToken(","));
            if (this.isToken("]"))
            {
                this.generator.putPush(loc2);
                loc1++;
            }
            return loc1;
        }

        private double parse_elementList()
        {
            ExpressionResult loc2 = null;
            double loc1 = 0;
            while (true)
            {
                if (this.isToken(","))
                {
                    loc1 += this.parse_elision();
                }
                else
                {
                    if (this.isToken("]"))
                    {
                        break;
                    }
                    loc2 = new ExpressionResult();
                    this.parse_assignmentExpression(loc2);
                    this.generator.putExpressionResult(loc2);
                    this.generator.putPush(loc2);
                    loc1++;
                    if (!this.isToken(","))
                    {
                        break;
                    }
                    this.nextToken();
                }
            }
            return loc1;
        }

        private double parse_objectLiteral()
        {
            if (!this.isToken("{"))
            {
                this.causeSyntaxError("'{' not found in object initializer");
            }
            double loc1 = 0;
            if (!this.isNextToken("}"))
            {
                loc1 += this.parse_propertyNameAndValueList();
            }
            if (!this.isToken("}"))
            {
                this.causeSyntaxError("'}' not found in object initializer");
            }
            this.nextToken();
            return loc1;
        }

        private double parse_propertyNameAndValueList()
        {
            ExpressionResult loc2 = null;
            double loc1 = 0;
            while (true)
            {
                this.parse_propertyName();
                if (!this.isToken(":"))
                {
                    this.causeSyntaxError("':' not found in object name-value initializer");
                }
                this.nextToken();
                loc2 = new ExpressionResult();
                this.parse_assignmentExpression(loc2);
                this.generator.putExpressionResult(loc2);
                this.generator.putPush(loc2);
                loc1++;
                if (!this.isToken(","))
                {
                    break;
                }
                this.nextToken();
            }
            return loc1;
        }

        private void parse_propertyName()
        {
            ExpressionResult loc1 = null;
            switch (this.getToken().type)
            {
                case "identifier":
                case "string":
                case "number":
                    loc1 = ExpressionResult.createLiteral(this.getToken().value);
                    this.generator.putPush(loc1);
                    this.nextToken();
                    break;
                default:
                    this.causeSyntaxError("unexpected token in property name");
                    break;
            }
        }

        private static bool TryGetNumber(object value, out double result)
        {
            if (value == null)
            {
                result = 0;
                return true;
            }
            if (value is bool)
            {
                result = (bool)value ? 1 : 0;
                return true;
            }
            if (value is string)
            {
                string text = (string)value;
                if (text.Length == 0)
                {
                    result = 0;
                    return true;
                }
                if (double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result))
                {
                    return true;
                }
                result = double.NaN;
                return false;
            }
            if (value is char)
            {
                result = (char)value;
                return true;
            }
            if (value is byte || value is sbyte || value is short || value is ushort || value is int || value is uint || value is long || value is ulong || value is float || value is double || value is decimal)
            {
                result = Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }
            result = double.NaN;
            return false;
        }

        private static double ToNumber(object value)
        {
            double result;
            return TryGetNumber(value, out result) ? result : double.NaN;
        }

        private static string ToStringValue(object value)
        {
            if (value == null)
            {
                return "null";
            }
            if (value is bool)
            {
                return (bool)value ? "true" : "false";
            }
            IFormattable formattable = value as IFormattable;
            if (formattable != null)
            {
                return formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture);
            }
            return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static object AddValues(object param1, object param2)
        {
            if (param1 is string || param2 is string)
            {
                return ToStringValue(param1) + ToStringValue(param2);
            }
            return ToNumber(param1) + ToNumber(param2);
        }

        private static object SubtractValues(object param1, object param2)
        {
            return ToNumber(param1) - ToNumber(param2);
        }

        private static object MultiplyValues(object param1, object param2)
        {
            return ToNumber(param1) * ToNumber(param2);
        }

        private static object DivideValues(object param1, object param2)
        {
            return ToNumber(param1) / ToNumber(param2);
        }

        private static object RemainderValues(object param1, object param2)
        {
            return ToNumber(param1) % ToNumber(param2);
        }

        private static int ToInt32(object value)
        {
            double number = ToNumber(value);
            if (double.IsNaN(number) || double.IsInfinity(number) || number == 0)
            {
                return 0;
            }
            number = Math.Truncate(number);
            number %= 4294967296d;
            if (number < 0)
            {
                number += 4294967296d;
            }
            uint result = (uint)number;
            return unchecked((int)result);
        }

        private static uint ToUInt32(object value)
        {
            return unchecked((uint)ToInt32(value));
        }

        private static object BitwiseOr(object param1, object param2)
        {
            return ToInt32(param1) | ToInt32(param2);
        }

        private static object BitwiseXor(object param1, object param2)
        {
            return ToInt32(param1) ^ ToInt32(param2);
        }

        private static object BitwiseAnd(object param1, object param2)
        {
            return ToInt32(param1) & ToInt32(param2);
        }

        private static object ShiftValues(object param1, object param2, string operation)
        {
            int count = ToInt32(param2) & 31;
            switch (operation)
            {
                case "<<":
                    return ToInt32(param1) << count;
                case ">>":
                    return ToInt32(param1) >> count;
                default:
                    return ToUInt32(param1) >> count;
            }
        }

        private static object NegateValue(object value)
        {
            return -ToNumber(value);
        }

        private static object BitwiseNot(object value)
        {
            return ~ToInt32(value);
        }

        private static bool IsTruthy(object value)
        {
            if (value == null)
            {
                return false;
            }
            if (value is bool)
            {
                return (bool)value;
            }
            if (value is string)
            {
                return ((string)value).Length > 0;
            }
            double number;
            if (TryGetNumber(value, out number))
            {
                return number != 0 && !double.IsNaN(number);
            }
            return true;
        }

        private static bool LooseEquals(object param1, object param2)
        {
            if (param1 == null || param2 == null)
            {
                return param1 == null && param2 == null;
            }
            double number1;
            double number2;
            if (TryGetNumber(param1, out number1) && TryGetNumber(param2, out number2) && (param1 is string || param2 is string || param1 is bool || param2 is bool || param1 is IConvertible || param2 is IConvertible))
            {
                return number1 == number2;
            }
            return object.Equals(param1, param2);
        }

        private static bool StrictEquals(object param1, object param2)
        {
            if (param1 == null || param2 == null)
            {
                return param1 == null && param2 == null;
            }
            if ((param1 is byte || param1 is sbyte || param1 is short || param1 is ushort || param1 is int || param1 is uint || param1 is long || param1 is ulong || param1 is float || param1 is double || param1 is decimal) && (param2 is byte || param2 is sbyte || param2 is short || param2 is ushort || param2 is int || param2 is uint || param2 is long || param2 is ulong || param2 is float || param2 is double || param2 is decimal))
            {
                return ToNumber(param1) == ToNumber(param2);
            }
            return param1.GetType() == param2.GetType() && object.Equals(param1, param2);
        }

        private static bool CompareValues(object param1, object param2, string operation)
        {
            if (operation == "instanceof")
            {
                return IsInstanceOf(param1, param2);
            }
            if (param1 is string && param2 is string)
            {
                int stringResult = string.CompareOrdinal((string)param1, (string)param2);
                return CompareResult(stringResult, operation);
            }
            double number1;
            double number2;
            if (!TryGetNumber(param1, out number1) || !TryGetNumber(param2, out number2))
            {
                return false;
            }
            switch (operation)
            {
                case "<":
                    return number1 < number2;
                case ">":
                    return number1 > number2;
                case "<=":
                    return number1 <= number2;
                default:
                    return number1 >= number2;
            }
        }

        private static bool CompareResult(int value, string operation)
        {
            switch (operation)
            {
                case "<":
                    return value < 0;
                case ">":
                    return value > 0;
                case "<=":
                    return value <= 0;
                default:
                    return value >= 0;
            }
        }

        private static string TypeOf(object value)
        {
            if (value == null)
            {
                return "object";
            }
            if (value is bool)
            {
                return "boolean";
            }
            if (value is string)
            {
                return "string";
            }
            if (value is byte || value is sbyte || value is short || value is ushort || value is int || value is uint || value is long || value is ulong || value is float || value is double || value is decimal)
            {
                return "number";
            }
            if (value is Delegate)
            {
                return "function";
            }
            return "object";
        }

        private static bool IsInstanceOf(object value, object target)
        {
            Type targetType = target as Type;
            return targetType != null && targetType.IsInstanceOfType(value);
        }
    }
}
