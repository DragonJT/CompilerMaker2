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
            var args = new PsDecoratedValue(1, new PsToken("("), new PsWhileWithDeliminator("Args", expression, new PsToken(","), true), new PsToken(")"));
            var call = new PsObject("Call", new PsToken("Name", "identifier"), args);
            var parenthesisGroup = new PsDecoratedValue(1, new PsToken("("), expression, new PsToken(")"));
            var expressionOr = new PsOr(call, parenthesisGroup,
                new PsToken("int"), new PsToken("+"), new PsToken("-"), new PsToken("/"), new PsToken("*"));
            expression.compiler = new PsWhile("Expression", expressionOr, 1);
            var expressionStatement = new PsDecoratedValue(0, expression, new PsToken(";"));
            var instructions = new PsWhile("Instructions", expressionStatement, 0);

            var block = new PsDecoratedValue(1, new PsToken("{"), instructions, new PsToken("}"));
            var parameter = new PsObject("Parameter", new PsToken("Type", "identifier"), new PsToken("Name", "identifier"));
            var parameters = new PsDecoratedValue(1, new PsToken("("), new PsWhileWithDeliminator("Parameters", parameter, new PsToken(","), true), new PsToken(")"));
            var function = new PsObject("Function", new PsToken("ReturnType", "identifier"), new PsToken("Name", "identifier"), parameters, block);
            var functions = new PsWhile("Functions", function, 0);
            var compileUnit = functions;

            var p = compileUnit.Parse(new TokenReader(code, tokens));
            if(p is SyntaxError error)
            {
                return error.ToString();
            }
            var shuntingYard = new ShuntingYard();
            shuntingYard.Add("+", new BinaryOp(Associative.Left, 0));
            shuntingYard.Add("-", new BinaryOp(Associative.Left, 0));
            shuntingYard.Add("*", new BinaryOp(Associative.Left, 5));
            shuntingYard.Add("/", new BinaryOp(Associative.Left, 5));
            shuntingYard.AddExpression("Expression");
            var tree = shuntingYard.Transform(p);

            var asmEmitter = new AsmEmitter();
            asmEmitter.Add("int", AsmEmitters.ConstFloat);
            asmEmitter.Add("+", AsmEmitters.F32Add);
            asmEmitter.Add("-", AsmEmitters.F32Subtract);
            asmEmitter.Add("/", AsmEmitters.F32Divide);
            asmEmitter.Add("*", AsmEmitters.F32Multiply);
            asmEmitter.Add("Instructions", AsmEmitters.Instructions);
            asmEmitter.Add("Args", AsmEmitters.Args);
            asmEmitter.Add("Call", AsmEmitters.Call);
            asmEmitter.Add("Parameter", AsmEmitters.Parameter);
            asmEmitter.Add("Parameters", AsmEmitters.Parameters);
            asmEmitter.Add("ReturnType", AsmEmitters.ReturnType);
            asmEmitter.Add("Function", AsmEmitters.Function);
            asmEmitter.Add("Functions", AsmEmitters.Functions);

            var asm = (Asm)asmEmitter.Emit(tree);
            return asm.Emit();
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
void Main(){
    Print( 6*(2+1) );
    Print( 5+5*4*(2+1) );
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

