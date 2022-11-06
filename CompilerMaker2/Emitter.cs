using System;
using System.Linq;
using System.Linq.Expressions;

namespace CompilerMaker2
{
    class Emitter
    {
        Asm asm = new();
        List<IInstruction> instructions = new();

        public Emitter()
        {
            asm.ImportFunction(null, "Print", new AsmParameter[] { new AsmParameter(Valtype.f32, "f") }, @"
var div = document.createElement('div');
div.innerHTML = f;
document.body.appendChild(div);");
        }

        public void Add(IInstruction instruction)
        {
            instructions.Add(instruction);
        }

        public IFunction FindFunction(string name)
        {
            return asm.FindFunction(name);
        }

        public void SetFunctionInstructions(AsmFunction function)
        {
            foreach (var i in instructions)
                function.Add(i);
            instructions.Clear();
        }

        public AsmFunction Function(Valtype? returnType, string name, AsmParameter[] parameters)
        {
            return asm.Function(true, returnType, name, parameters);
        }

        public string Emit()
        {
            return asm.Emit();
        }
    }

    class CmConstF32 : CmToken
    {
        public CmConstF32(string type):base(type) { }

        public override void Emit(Emitter emitter, ISyntax syntax, CompileStep compileStep)
        {
            if(compileStep == CompileStep.EmitAsm)
            {
                emitter.Add(new AsmConstF32(float.Parse(((Token)syntax).value)));
            }
        }

        public override ShuntingYardType GetShuntingYardType(ISyntax syntax) => ShuntingYardType.Value;
    }

    class CmBinaryOp : CmToken
    {
        Opcode opcode;
        int precedence;
        Associative associative;

        public CmBinaryOp(string type, Opcode opcode, int precedence, Associative associative):base(type)
        {
            this.opcode = opcode;
            this.precedence = precedence;
            this.associative = associative;
        }

        public override int GetPrecedence(ISyntax syntax) => precedence;

        public override Associative GetAssociative(ISyntax syntax) => associative;

        public override ShuntingYardType GetShuntingYardType(ISyntax syntax) => ShuntingYardType.BinaryOp;

        public override void Emit(Emitter emitter, ISyntax syntax, CompileStep compileStep)
        {
            if(compileStep == CompileStep.EmitAsm)
            {
                emitter.Add(new AsmOp(opcode));
            }
        }
    }

    class CmFunction : CmObject
    {
        int returnID;
        int nameID;
        int parametersID;
        int blockID;

        public CmFunction(int returnID, int nameID, int parametersID, int blockID, params ICompile[] fields):base(fields)
        {
            this.returnID = returnID;
            this.nameID = nameID;
            this.parametersID = parametersID;
            this.blockID = blockID;
        }

        static Valtype? GetReturnType(ISyntax syntax)
        {
            var identifier = ((Token)syntax).value;
            switch (identifier)
            {
                case "void": return null;
                case "float": return Valtype.f32;
                case "int": return Valtype.i32;
                default: throw new Exception("GetReturnType defaulted");
            }
        }

        static Valtype GetType(ISyntax syntax)
        {
            var identifier = ((Token)syntax).value;
            switch (identifier)
            {
                case "float": return Valtype.f32;
                case "int": return Valtype.i32;
                default: throw new Exception("GetType defaulted");
            }
        }

        static string GetValue(ISyntax syntax)
        {
            return ((Token)syntax).value;
        }
        
        public override void Emit(Emitter emitter, ISyntax syntax, CompileStep compileStep)
        {
            if(compileStep == CompileStep.FunctionCreation)
            {
                var tree = (SyntaxTree)syntax;
                var returnType = GetReturnType(tree.children[returnID]);
                var name = GetValue(tree.children[nameID]);
                var parameters = ((SyntaxTree)tree.children[parametersID]).children
                    .Select(c => new AsmParameter(GetType(((SyntaxTree)c).children[0]), GetValue(((SyntaxTree)c).children[1])))
                    .ToArray();
                tree.compilerData = emitter.Function(returnType, name, parameters);
            }
            else if(compileStep == CompileStep.EmitAsm)
            {
                fields[blockID].Emit(emitter, ((SyntaxTree)syntax).children[blockID], compileStep);
                emitter.SetFunctionInstructions((AsmFunction)((SyntaxTree)syntax).compilerData!);
            }
        }
    }

    class CmFunctions : CmWhile
    {
        public CmFunctions(ICompile element) : base(element, 0) { }

        public override void Emit(Emitter emitter, ISyntax syntax, CompileStep compileStep)
        {
            var tree = (SyntaxTree)syntax;
            foreach (var c in tree.children)
                element.Emit(emitter, c, compileStep);
        }
    }

    class CmStatements : CmWhile
    {
        public CmStatements(ICompile element):base(element, 0) { }

        public override void Emit(Emitter emitter, ISyntax syntax, CompileStep compileStep)
        {
            if(compileStep == CompileStep.EmitAsm)
            {
                var tree = (SyntaxTree)syntax;
                foreach (var c in tree.children)
                    element.Emit(emitter, c, compileStep);
            }
        }
    }

    class CmArgs : CmWhileWithDeliminator
    {
        public CmArgs(ICompile expression, ICompile deliminator, bool strict):base(expression, deliminator, strict)
        {
        }

        public override void Emit(Emitter emitter, ISyntax syntax, CompileStep compileStep)
        {
            if(compileStep == CompileStep.EmitAsm)
            {
                var tree = (SyntaxTree)syntax;
                foreach (var c in tree.children)
                    element.Emit(emitter, c, compileStep);
            }
        }
    }

    class CmCall : CmObject
    {
        int funcID;
        int argsID;

        public CmCall(int funcID, int argsID, params ICompile[] fields):base(fields)
        {
            this.funcID = funcID;
            this.argsID = argsID;
        }

        public override void Emit(Emitter emitter, ISyntax syntax, CompileStep compileStep)
        {
            if(compileStep == CompileStep.EmitAsm)
            {
                var tree = (SyntaxTree)syntax;
                var name = ((Token)tree.children[funcID]).value;
                var func = emitter.FindFunction(name);
                var args = tree.children[argsID];
                fields[argsID].Emit(emitter, args, compileStep);
                emitter.Add(new AsmCall(func));
            }
        }

        public override ShuntingYardType GetShuntingYardType(ISyntax syntax) => ShuntingYardType.Value;
    }

    enum ShuntingYardType { None, Value, BinaryOp }

    enum Associative { None, Left, Right }

    class CmExpression : CmWhile
    {
        public CmExpression(ICompile element, int minLength) : base(element, minLength)
        {
        }

        public override void Emit(Emitter emitter, ISyntax syntax, CompileStep compileStep)
        {
            if(compileStep == CompileStep.EmitAsm)
            {
                var tree = (SyntaxTree)syntax;
                var stack = new Stack<ISyntax>();
                var output = new List<ISyntax>();

                foreach (var c in tree.children)
                {
                    var sytype = element.GetShuntingYardType(c);
                    switch (sytype)
                    {
                        case ShuntingYardType.None:
                            throw new Exception("Shuntingyardtype is none");
                        case ShuntingYardType.Value:
                            output.Add(c);
                            break;
                        case ShuntingYardType.BinaryOp:
                            while (stack.Count > 0)
                            {
                                var associative = element.GetAssociative(c);
                                var precedence = element.GetPrecedence(c);
                                var stackPeekPrecedence = element.GetPrecedence(stack.Peek());
                                if (associative == Associative.None)
                                    throw new Exception("Invalid associative");
                                if (precedence == -1)
                                    throw new Exception("Invalid precedence");

                                if ((associative == Associative.Left && precedence <= stackPeekPrecedence) ||
                                    (associative == Associative.Right && precedence < stackPeekPrecedence))
                                {
                                    var poppedOp = stack.Pop();
                                    output.Add(poppedOp);
                                }
                                else
                                    break;
                            }
                            stack.Push(c);
                            break;
                        default:
                            throw new Exception("Shuntingyardtype defaulted");
                    }
                }
                while (stack.Count > 0)
                {
                    output.Add(stack.Pop());
                }
                foreach (var o in output)
                {
                    element.Emit(emitter, o, compileStep);
                }
            }
        }

        public override ShuntingYardType GetShuntingYardType(ISyntax syntax) => ShuntingYardType.Value;
    }
}

