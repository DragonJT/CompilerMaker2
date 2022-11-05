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
            tokenizer.AddPunctuation("*", "/", "+", "-", "(", ")", ";");
            tokenizer.Add("ws", ws, false);
            var tokens = tokenizer.Tokenize(code);

            var expression = new CmCirular();
            var integer = new CmConstF32("int");
            var add = new CmBinaryOp("+", Opcode.f32_add, 0, Associative.Left);
            var subtract = new CmBinaryOp("-", Opcode.f32_sub, 0, Associative.Left);
            var multiply = new CmBinaryOp("*", Opcode.f32_mul, 5, Associative.Left);
            var divide = new CmBinaryOp("/", Opcode.f32_div, 5, Associative.Left);
            var call = new CmCall(0, 2, new CmToken("identifier"), new CmToken("("), expression, new CmToken(")"));
            var parenthesisGroup = new CmDecoratedValue(1, new CmToken("("), expression, new CmToken(")"));
            var expressionOr = new CmOr(call, parenthesisGroup, integer, add, subtract, divide, multiply);
            expression.compiler = new CmExpression(expressionOr, 1);
            var expressionStatement = new CmDecoratedValue(0, expression, new CmToken(";"));
            var statements = new CmWhile("statements", expressionStatement, 0);
            var compileUnit = statements;

            var p = compileUnit.Parse(new TokenReader(tokens));
            if(p.Type != JEnum.Error)
            {
                var emitter = new Emitter();
                compileUnit.Emit(emitter, p);
                return emitter.EmitAsm().Emit();
            }
            return ((SyntaxError)p).ToString();
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
Print( 4 * (4+3/2+13) );
Print( 25 );
Print( 23 + 25*2 - 33/4*5 + 12 );";
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

