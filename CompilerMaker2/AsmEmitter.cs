using System;
namespace CompilerMaker2
{
    static class AsmEmitters
    {
        public static object ConstFloat(AsmEmitter asmEmitter, ISyntax syntax)
        {
            var token = (SyntaxToken)syntax;
            return new List<IInstruction> { new AsmConstF32(float.Parse(token.value)) };
        }

        static List<IInstruction> ChildInstructions(AsmEmitter asmEmitter, ISyntax syntax)
        {
            var instructions = new List<IInstruction>();
            var tree = (SyntaxTree)syntax;
            foreach (var c in tree.children)
                instructions.AddRange((List<IInstruction>)asmEmitter.Emit(c));
            return instructions;
        }

        static object SimpleOperator(AsmEmitter asmEmitter, ISyntax syntax, Opcode opcode)
        {
            var instructions = ChildInstructions(asmEmitter, syntax);
            instructions.Add(new AsmOp(opcode));
            return instructions;
        }

        public static object Args(AsmEmitter asmEmitter, ISyntax syntax)
        {
            return ChildInstructions(asmEmitter, syntax);
        }

        public static object Call(AsmEmitter asmEmitter, ISyntax syntax)
        {
            var tree = (SyntaxTree)syntax;
            var name = asmEmitter.Value(tree.Get("Name"));
            var instructions = (List<IInstruction>)asmEmitter.Emit(tree.Get("Args"));
            instructions.Add(new AsmCall(name));
            return instructions;
        }

        public static object F32Add(AsmEmitter asmEmitter, ISyntax syntax)
        {
            return SimpleOperator(asmEmitter, syntax, Opcode.f32_add);
        }

        public static object F32Divide(AsmEmitter asmEmitter, ISyntax syntax)
        {
            return SimpleOperator(asmEmitter, syntax, Opcode.f32_div);
        }

        public static object F32Subtract(AsmEmitter asmEmitter, ISyntax syntax)
        {
            return SimpleOperator(asmEmitter, syntax, Opcode.f32_sub);
        }

        public static object F32Multiply(AsmEmitter asmEmitter, ISyntax syntax)
        {
            return SimpleOperator(asmEmitter, syntax, Opcode.f32_mul);
        }

        static Valtype? ReturnType(string value)
        {
            switch (value)
            {
                case "float": return Valtype.f32;
                case "int": return Valtype.i32;
                case "void": return null;
                default: throw new Exception("Error");
            }
        }

        public static object ReturnType(AsmEmitter asmEmitter, ISyntax syntax)
        {
            return ReturnType(((SyntaxToken)syntax).value);
        }

        public static object Instructions(AsmEmitter asmEmitter, ISyntax syntax)
        {
            return ChildInstructions(asmEmitter, syntax);
        }

        public static object Parameter(AsmEmitter asmEmitter, ISyntax syntax)
        {
            var tree = (SyntaxTree)syntax;
            var type = asmEmitter.Emit(tree.Get("Type"));
            var name = asmEmitter.Value(tree.Get("Name"));
            return new AsmParameter((Valtype)type, name);
        }

        public static object Parameters(AsmEmitter asmEmitter, ISyntax syntax)
        {
            var tree = (SyntaxTree)syntax;
            return tree.children.Select(c => (AsmParameter)asmEmitter.Emit(c)).ToArray();
        }

        public static object Function(AsmEmitter asmEmitter, ISyntax syntax)
        {
            var tree = (SyntaxTree)syntax;
            var export = true;
            var returnType = asmEmitter.Emit(tree.Get("ReturnType"));
            var name = asmEmitter.Value(tree.Get("Name"));
            var parameters = asmEmitter.Emit(tree.Get("Parameters"));
            var instructions = asmEmitter.Emit(tree.Get("Instructions"));
            return new AsmFunction(export, (Valtype?)returnType, (string)name, (AsmParameter[])parameters, (List<IInstruction>)instructions);
        }

        public static object Functions(AsmEmitter asmEmitter, ISyntax syntax)
        {
            var asm = new Asm();
            var print = new AsmImportFunction(null, "Print", new AsmParameter[] { new AsmParameter(Valtype.f32, "f") }, @"
var div = document.createElement('div');
div.innerHTML = f;
document.body.appendChild(div);");
            asm.Add(print);
            var tree = (SyntaxTree)syntax;
            foreach (var c in tree.children)
                asm.Add((IFunction)asmEmitter.Emit(c));
            return asm;
        }
    }

    class AsmEmitter
    {
        Func<AsmEmitter, ISyntax, object>[] asmEmitters;

        public AsmEmitter()
        {
            asmEmitters = new Func<AsmEmitter, ISyntax, object>[JEnum.Count];
        }

        public void Add(string type, Func<AsmEmitter, ISyntax, object> emit)
        {
            asmEmitters[JEnum.Get(type)] = emit;
        }

        public object Emit(ISyntax syntax)
        {
            var func = asmEmitters[syntax.type];
            if (func == null)
                throw new Exception("Cant find enum: "+JEnum.GetName(syntax.type));
            return func(this, syntax);
        }

        public string Value(ISyntax syntax)
        {
            return ((SyntaxToken)syntax).value;
        }
    }
}

