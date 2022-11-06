using System;
namespace CompilerMaker2
{
    class Program
    {
        static string CSCompile(string code)
        {
            var tokenizer = new Tokenizer();

            var character = new TkOr(new TkCharRange('a', 'z'), new TkCharRange('A', 'Z'), new TkChar('_'));
            var digit = new TkCharRange('0', '9');
            var alphaNumeric = new TkOr(character, digit);
            var ws = new TkOr(new TkChar(' '), new TkChar('\n'), new TkChar('\t'), new TkChar('\r'));

            tokenizer.Add("int", new TkWhile(digit, 1));
            tokenizer.Add("identifier", new TkObject(character, new TkWhile(alphaNumeric, 0))); 
            tokenizer.AddPunctuation("*", "/", "+", "-", "(", ")", ";", "{", "}");
            tokenizer.Add("ws", ws, false);
            var tokens = tokenizer.Tokenize(code);

            var expression = new CmCirular();
            var integer = new CmConstF32("int");
            var add = new CmBinaryOp("+", Opcode.f32_add, 0, Associative.Left);
            var subtract = new CmBinaryOp("-", Opcode.f32_sub, 0, Associative.Left);
            var multiply = new CmBinaryOp("*", Opcode.f32_mul, 5, Associative.Left);
            var divide = new CmBinaryOp("/", Opcode.f32_div, 5, Associative.Left);
            var args = new CmDecoratedValue(1, new CmToken("("), new CmArgs(expression, new CmToken(","), true), new CmToken(")"));
            var call = new CmCall(0, 1, new CmToken("identifier"), args);
            var parenthesisGroup = new CmDecoratedValue(1, new CmToken("("), expression, new CmToken(")"));
            var expressionOr = new CmOr(call, parenthesisGroup, integer, add, subtract, divide, multiply);
            expression.compiler = new CmExpression(expressionOr, 1);
            var expressionStatement = new CmDecoratedValue(0, expression, new CmToken(";"));
            var statements = new CmStatements(expressionStatement);

            var block = new CmDecoratedValue(1, new CmToken("{"), statements, new CmToken("}"));
            var parameter = new CmObject(new CmToken("identifier"), new CmToken("identifier"));
            var parameters = new CmDecoratedValue(1, new CmToken("("), new CmWhileWithDeliminator(parameter, new CmToken(","), true), new CmToken(")"));
            var function = new CmFunction(0,1,2,3, new CmToken("identifier"), new CmToken("identifier"), parameters, block);
            var functions = new CmFunctions(function);
            var compileUnit = functions;

            var p = compileUnit.Parse(new TokenReader(code, tokens));
            if(p is SyntaxError error)
            {
                return error.ToString();
            }
            var emitter = new Emitter();
            compileUnit.Emit(emitter, p, CompileStep.FunctionCreation);
            compileUnit.Emit(emitter, p, CompileStep.EmitAsm);
            return emitter.Emit();
        }

        static void OpenWebBrowser(string filename)
        {
            string curDir = Directory.GetCurrentDirectory();
            string uri = String.Format("file:///{0}/{1}", curDir, filename);
            var psi = new System.Diagnostics.ProcessStartInfo();
            psi.UseShellExecute = true;
            psi.FileName = uri;
            System.Diagnostics.Process.Start(psi);
        }

        static void Main()
        {
            var code = @"
float GetValue(){
    23;
}

void Main(){
    Print( 4 * (4+3/2+13) );
    Print( 25 );
    Print( 23 + 25*2 - 33/4*5 + 12 );
    Print( GetValue() );
}";
            var html = @"<!DOCTYPE html>
<html>
<body>
    " + CSCompile(code) + @"
        </body>
</html>";
            File.WriteAllText("main.html", html);
            OpenWebBrowser("main.html");
        }
    }
}

