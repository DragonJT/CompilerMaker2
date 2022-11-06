using System;
using System.Linq;

namespace CompilerMaker2
{
    static class EmitterHelper
    {
        public static string GetValue(this ISyntax syntax)
        {
            return ((Token)syntax).value;
        }

        public static Valtype? GetReturnValtype(this ISyntax syntax)
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

        public static Valtype GetValtype(this ISyntax syntax)
        {
            var identifier = ((Token)syntax).value;
            switch (identifier)
            {
                case "float": return Valtype.f32;
                case "int": return Valtype.i32;
                default: throw new Exception("GetType defaulted");
            }
        }

        public static object? GetData(this ISyntax syntax)
        {
            return ((SyntaxTree)syntax).data;
        }
    }

    interface ICalcData
    {
        void CalcData(Emitter emitter, SyntaxTree tree);
    }

    interface IEmitter
    {
        void Visit(Emitter emitter, ISyntax syntax);
    }

    enum Associative { Left, Right }

    interface IShuntingYardValue { }

    interface IShuntingYardBinaryOp
    {
        int precedence { get; }
        Associative associative { get; }
    }

    class EmitConstF32 : IEmitter, IShuntingYardValue
    {
        public void Visit(Emitter emitter, ISyntax syntax)
        {
            var token = (Token)syntax;
            emitter.Add(new AsmConstF32(float.Parse(token.value)));
        }
    }

    class EmitChildren : IEmitter
    {
        public void Visit(Emitter emitter, ISyntax syntax)
        {
            var tree = (SyntaxTree)syntax;
            foreach (var c in tree.children)
                emitter.Visit(c);
        }
    }

    class EmitCall : IEmitter, IShuntingYardValue
    {
        int nameID;
        int argsID;

        public EmitCall(int funcNameID, int argsID)
        {
            this.nameID = funcNameID;
            this.argsID = argsID;
        }

        public void Visit(Emitter emitter, ISyntax syntax)
        {
            var tree = (SyntaxTree)syntax;
            var functionName = tree.children[nameID].GetValue();
            var function = emitter.FindFunction(functionName);
            var args = tree.children[argsID];
            emitter.Visit(args);
            emitter.Add(new AsmCall(function));
        }
    }

    class EmitParameter : IEmitter, ICalcData
    {
        int typeID, nameID;

        public EmitParameter(int typeID, int nameID)
        {
            this.typeID = typeID;
            this.nameID = nameID;
        }

        public void CalcData(Emitter emitter, SyntaxTree tree)
        {
            var type = tree.children[typeID].GetValtype();
            var name = tree.children[nameID].GetValue();
            tree.data = new AsmParameter(type, name);
        }

        public void Visit(Emitter emitter, ISyntax syntax) { }
    }

    class EmitParameters : IEmitter, ICalcData
    {
        public void CalcData(Emitter emitter, SyntaxTree tree)
        {
            tree.data = tree.children.Select(c => (AsmParameter)c.GetData()!).ToArray();
        }

        public void Visit(Emitter emitter, ISyntax syntax) { }
    }

    class EmitFunction : IEmitter, ICalcData
    {
        int returnTypeID, nameID, parametersID, blockID;

        public EmitFunction(int returnTypeID, int nameID, int parametersID, int blockID)
        {
            this.returnTypeID = returnTypeID;
            this.nameID = nameID;
            this.parametersID = parametersID;
            this.blockID = blockID;
        }

        public void CalcData(Emitter emitter, SyntaxTree tree)
        {
            var returnType = tree.children[returnTypeID].GetReturnValtype();
            var name = tree.children[nameID].GetValue();
            var parameters = (AsmParameter[])tree.children[parametersID].GetData()!;
            tree.data = emitter.asm.Function(true, returnType, name, parameters);
        }

        public void Visit(Emitter emitter, ISyntax syntax)
        {
            var tree = (SyntaxTree)syntax;
            emitter.function = (AsmFunction)tree.data!;
            emitter.Visit(tree.children[blockID]);
        }
    }

    class EmitBinaryOp : IEmitter, IShuntingYardBinaryOp
    {
        public int precedence { get; }
        public Associative associative { get; }
        Opcode opcode;

        public EmitBinaryOp(int precedence, Associative associative, Opcode opcode)
        {
            this.precedence = precedence;
            this.associative = associative;
            this.opcode = opcode;
        }

        public void Visit(Emitter emitter, ISyntax syntax)
        {
            emitter.Add(new AsmOp(opcode));
        }
    }
    
    class EmitShuntingYard : IEmitter, IShuntingYardValue
    {
        public void Visit(Emitter emitter, ISyntax syntax)
        {
            var tree = (SyntaxTree)syntax;
            var stack = new Stack<ISyntax>();
            var output = new List<ISyntax>();

            foreach (var c in tree.children)
            {
                var e = emitter.Get(c);
                if(e is IShuntingYardValue)
                    output.Add(c);
                else if(e is IShuntingYardBinaryOp op)
                {
                    while (stack.Count > 0)
                    {
                        var associative = op.associative;
                        var precedence = op.precedence;
                        var stackPeekPrecedence = ((IShuntingYardBinaryOp)emitter.Get(stack.Peek())).precedence;

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
                }
                else
                {
                    throw new Exception(e.GetType().Name + " is not shuntingyard value or binaryOp");
                }
            }
            while (stack.Count > 0)
            {
                output.Add(stack.Pop());
            }
            foreach (var o in output)
            {
                emitter.Visit(o);
            }
        }
    }

    class Emitter
    {
        IEmitter[] emitters;
        ISyntax syntax;
        public Asm asm = new Asm();
        public AsmFunction? function;

        public Emitter(ISyntax syntax)
        {
            this.syntax = syntax;
            emitters = new IEmitter[JEnum.Count];
            asm.ImportFunction(null, "Print", new AsmParameter[] { new AsmParameter(Valtype.f32, "f") }, @"
var div = document.createElement('div');
div.innerHTML = f;
document.body.appendChild(div);");
        }

        public IFunction FindFunction(string name)
        {
            return asm.FindFunction(name);
        }

        public void Visit(ISyntax syntax)
        {
            emitters[syntax.Type].Visit(this, syntax);
        }

        public IEmitter Get(ISyntax syntax)
        {
            return emitters[syntax.Type];
        }

        public void Add(IInstruction instruction)
        {
            function!.Add(instruction);
        }

        public void Add(string type, IEmitter emitter)
        {
            emitters[JEnum.Get(type)] = emitter;
        }

        void CalcData(ISyntax syntax, int type)
        {
            if(syntax is SyntaxTree tree)
            {
                if (tree.Type == type)
                {
                    ((ICalcData)emitters[tree.Type]).CalcData(this, tree);
                }
                foreach (var c in tree.children)
                {
                    CalcData(c, type);
                }
            }
        }

        public void CalcData(string type)
        {
            CalcData(syntax, JEnum.Get(type));
        }

        public string Emit()
        {
            Visit(syntax);
            return asm.Emit();
        }
    }
}

