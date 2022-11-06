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

            var expression = new PsCircular();
            var identifier = new PsToken("identifier");
            var args = new PsDecoratedValue(1, new PsToken("("), new PsWhileWithDeliminator("Args", expression, new PsToken(","), true), new PsToken(")"));
            var call = new PsObject("Call", identifier, args);
            var parenthesisGroup = new PsDecoratedValue(1, new PsToken("("), expression, new PsToken(")"));
            var expressionOr = new PsOr(call, parenthesisGroup,
                new PsToken("int"), new PsToken("+"), new PsToken("-"), new PsToken("/"), new PsToken("*"));
            expression.compiler = new PsWhile("Expression", expressionOr, 1);
            var expressionStatement = new PsDecoratedValue(0, expression, new PsToken(";"));
            var statements = new PsWhile("Statements", expressionStatement, 0);

            var block = new PsDecoratedValue(1, new PsToken("{"), statements, new PsToken("}"));
            var parameter = new PsObject("Parameter", identifier, identifier);
            var parameters = new PsDecoratedValue(1, new PsToken("("), new PsWhileWithDeliminator("Parameters", parameter, new PsToken(","), true), new PsToken(")"));
            var function = new PsObject("Function", identifier, identifier, parameters, block);
            var functions = new PsWhile("Functions", function, 0);
            var compileUnit = functions;

            var p = compileUnit.Parse(new TokenReader(code, tokens));
            if(p is SyntaxError error)
            {
                return error.ToString();
            }
            var emitter = new Emitter(p);
            emitter.Add("Expression", new EmitShuntingYard());
            emitter.Add("int", new EmitConstF32());
            emitter.Add("+", new EmitBinaryOp(0, Associative.Left, Opcode.f32_add));
            emitter.Add("-", new EmitBinaryOp(0, Associative.Left, Opcode.f32_sub));
            emitter.Add("*", new EmitBinaryOp(5, Associative.Left, Opcode.f32_mul));
            emitter.Add("/", new EmitBinaryOp(5, Associative.Left, Opcode.f32_div));
            emitter.Add("Args", new EmitChildren());
            emitter.Add("Call", new EmitCall(0,1));
            emitter.Add("Statements", new EmitChildren());
            emitter.Add("Parameter", new EmitParameter(0, 1));
            emitter.Add("Parameters", new EmitParameters());
            emitter.Add("Function", new EmitFunction(0, 1, 2, 3));
            emitter.Add("Functions", new EmitChildren());
            emitter.CalcData("Parameter");
            emitter.CalcData("Parameters");
            emitter.CalcData("Function");
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
    12*4;
}

void Main(){
    Print(53 + 4 * 6 * (5 - 2));
    Print(64-23*2);
    Print(GetValue());
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

