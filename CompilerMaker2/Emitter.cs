using System;
using System.Runtime.CompilerServices;

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
            asm.Function(true, null, "Main", Array.Empty<AsmParameter>());
        }

        public void Add(IInstruction instruction)
        {
            instructions.Add(instruction);
        }

        public IFunction FindFunction(string name)
        {
            return asm.FindFunction(name);
        }

        public Asm EmitAsm()
        {
            var main = (AsmFunction)asm.FindFunction("Main");
            foreach (var i in instructions)
                main.Add(i);
            return asm;
        }
    }

    class CmConstF32 : CmToken
    {
        public CmConstF32(string type):base(type) { }

        public override void Emit(Emitter emitter, ISyntax syntax)
        {
            emitter.Add(new AsmConstF32(float.Parse(((Token)syntax).value)));
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

        public override void Emit(Emitter emitter, ISyntax syntax)
        {
            emitter.Add(new AsmOp(opcode));
        }
    }

    class CmCall : CmObject
    {
        int funcID;
        int argsID;

        public CmCall(int funcID, int argsID, params ICompile[] fields):base("Call", fields)
        {
            this.funcID = funcID;
            this.argsID = argsID;
        }

        public override void Emit(Emitter emitter, ISyntax syntax)
        {
            var tree = (SyntaxTree)syntax;
            var name = ((Token)tree.children[funcID]).value;
            var func = emitter.FindFunction(name);
            var args = tree.children[argsID];
            fields[argsID].Emit(emitter, args);
            emitter.Add(new AsmCall(func));
        }

        public override ShuntingYardType GetShuntingYardType(ISyntax syntax) => ShuntingYardType.Value;
    }

    enum ShuntingYardType { None, Value, BinaryOp }

    enum Associative { None, Left, Right }

    class CmExpression : CmWhile
    {
        public CmExpression(ICompile element, int minLength) : base( "Expression", element, minLength)
        {
        }

        public override void Emit(Emitter emitter, ISyntax syntax)
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
            foreach(var o in output)
            {
                element.Emit(emitter, o);
            }
        }

        public override ShuntingYardType GetShuntingYardType(ISyntax syntax) => ShuntingYardType.Value;
    }
}

