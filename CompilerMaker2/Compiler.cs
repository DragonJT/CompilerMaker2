using System;
namespace CompilerMaker2
{
    enum CompileStep { FunctionCreation, EmitAsm }

    class TokenReader
    {
        public string code;
        public List<Token> tokens;
        public int index;

        public TokenReader(string code, List<Token> tokens)
        {
            this.code = code;
            this.tokens = tokens;
            index = 0;
        }

        public bool OutOfRange => index >= tokens.Count;

        public Token Current => tokens[index];
    }

    interface ISyntax
    {
    }

    enum ErrorType { OutOfRange, ExpectingDifferentToken, WhileNotLongEnough, NoValidBranches }

    class SyntaxError : ISyntax
    {
        string code;
        int index;
        ErrorType errorType;

        public SyntaxError(TokenReader reader, ErrorType errorType)
        {
            code = reader.code;
            if (reader.index >= reader.tokens.Count)
                index = code.Length;
            else
                index = reader.tokens[reader.index].start;
            this.errorType = errorType;
        }

        public override string ToString()
        {
            return errorType.ToString() + ": " + code.Substring(0, index) + "]" + code.Substring(index);
        }
    }

    class SyntaxTree : ISyntax
    {
        public List<ISyntax> children;
        public object? compilerData;

        public SyntaxTree(List<ISyntax> children)
        {
            this.children = children;
        }

        public override string ToString()
        {
            var result = "[";
            for (var i = 0; i < children.Count; i++)
            {
                result += children[i];
                if (i < children.Count - 1)
                    result += ", ";
            }
            result += "]";
            return result;
        }
    }

    class SyntaxOr : ISyntax
    {
        public ISyntax syntax;
        public ICompile branch;

        public SyntaxOr(ISyntax syntax, ICompile branch)
        {
            this.syntax = syntax;
            this.branch = branch;
        }
    }

    abstract class ICompile
    {
        public abstract ISyntax Parse(TokenReader reader);

        public virtual void Emit(Emitter emitter, ISyntax syntax, CompileStep compileStep) { }

        public virtual ShuntingYardType GetShuntingYardType(ISyntax syntax) => ShuntingYardType.None;

        public virtual int GetPrecedence(ISyntax syntax) => -1;

        public virtual Associative GetAssociative(ISyntax syntax) => Associative.None;
    }

    class CmToken : ICompile
    {
        int type;

        public CmToken(string type)
        {
            this.type = JEnum.Get(type);
        }

        public override ISyntax Parse(TokenReader reader)
        {
            if (reader.OutOfRange)
                return new SyntaxError(reader, ErrorType.OutOfRange);
            var current = reader.Current;
            if (current.Type == type)
            {
                reader.index++;
                return current;
            }
            return new SyntaxError(reader, ErrorType.ExpectingDifferentToken);
        }
    }

    abstract class CmWhile : ICompile
    {
        protected ICompile element;
        int minLength;

        public CmWhile(ICompile element, int minLength)
        {
            this.element = element;
            this.minLength = minLength;
        }

        public override ISyntax Parse(TokenReader reader)
        {
            List<ISyntax> children = new();
            while (true)
            {
                var p = element.Parse(reader);
                if (p is SyntaxError)
                {
                    if (children.Count < minLength)
                        return new SyntaxError(reader, ErrorType.WhileNotLongEnough);
                    return new SyntaxTree(children);
                }
                children.Add(p);
            }
        }
    }

    class CmWhileWithDeliminator : ICompile
    {
        protected ICompile element;
        ICompile deliminator;
        bool strict;

        public CmWhileWithDeliminator(ICompile element, ICompile deliminator, bool strict)
        {
            this.element = element;
            this.deliminator = deliminator;
            this.strict = strict;
        }

        public override ISyntax Parse(TokenReader reader)
        {
            var length = 0;
            List<ISyntax> children = new();
            while (true)
            {
                var p = element.Parse(reader);
                if (p is SyntaxError)
                {
                    if (length == 0 || !strict)
                        return new SyntaxTree(children);
                    return p;
                }
                children.Add(p);
                var dp = deliminator.Parse(reader);
                if (dp is SyntaxError)
                    return new SyntaxTree(children);
                length++;
            }
        }
    }

    class CmOr : ICompile
    {
        ICompile[] branches;

        public CmOr(params ICompile[] branches)
        {
            this.branches = branches;
        }

        public override ISyntax Parse(TokenReader reader)
        {
            foreach(var b in branches)
            {
                var p = b.Parse(reader);
                if (!(p is SyntaxError))
                    return new SyntaxOr(p, b);
            }
            return new SyntaxError(reader, ErrorType.NoValidBranches);
        }

        public override ShuntingYardType GetShuntingYardType(ISyntax syntax)
        {
            var syntaxOr = ((SyntaxOr)syntax);
            return syntaxOr.branch.GetShuntingYardType(syntaxOr.syntax);
        }

        public override int GetPrecedence(ISyntax syntax)
        {
            var syntaxOr = ((SyntaxOr)syntax);
            return syntaxOr.branch.GetPrecedence(syntaxOr.syntax);
        }
          
        public override Associative GetAssociative(ISyntax syntax)
        {
            var syntaxOr = ((SyntaxOr)syntax);
            return syntaxOr.branch.GetAssociative(syntaxOr.syntax);
        }

        public override void Emit(Emitter emitter, ISyntax syntax, CompileStep compileStep)
        {
            var syntaxOr = ((SyntaxOr)syntax);
            syntaxOr.branch.Emit(emitter, syntaxOr.syntax, compileStep);
        }
    }

    class CmDecoratedValue : ICompile
    {
        int id;
        protected ICompile[] fields;

        public CmDecoratedValue(int id, params ICompile[] fields)
        {
            this.id = id;
            this.fields = fields;
        }

        public override ISyntax Parse(TokenReader reader)
        {
            for(int i=0;i<id;i++)
            {
                var p = fields[i].Parse(reader);
                if (p is SyntaxError)
                    return p;
            }
            var value = fields[id].Parse(reader);
            if (value is SyntaxError)
                return value;

            for (int i = id+1; i < fields.Length; i++)
            {
                var p = fields[i].Parse(reader);
                if (p is SyntaxError)
                    return p;
            }
            return value!;
        }

        public override void Emit(Emitter emitter, ISyntax syntax, CompileStep compileStep) => fields[id].Emit(emitter, syntax, compileStep);

        public override Associative GetAssociative(ISyntax syntax) => fields[id].GetAssociative(syntax);

        public override int GetPrecedence(ISyntax syntax) => fields[id].GetPrecedence(syntax);

        public override ShuntingYardType GetShuntingYardType(ISyntax syntax) => fields[id].GetShuntingYardType(syntax);
    }

    class CmObject:ICompile
    {
        protected ICompile[] fields;

        public CmObject(params ICompile[] fields)
        {
            this.fields = fields;
        }

        public override ISyntax Parse(TokenReader reader)
        {
            List<ISyntax> children = new();
            foreach(var f in fields)
            {
                var p = f.Parse(reader);
                if (p is SyntaxError)
                    return p;
                children.Add(p);
            }
            return new SyntaxTree(children);
        }
    }

    class CmCirular : ICompile
    {
        public ICompile? compiler;

        public override ISyntax Parse(TokenReader reader) => compiler!.Parse(reader);

        public override void Emit(Emitter emitter, ISyntax syntax, CompileStep compileStep) => compiler!.Emit(emitter, syntax, compileStep);

        public override Associative GetAssociative(ISyntax syntax) => compiler!.GetAssociative(syntax);

        public override int GetPrecedence(ISyntax syntax) => compiler!.GetPrecedence(syntax);

        public override ShuntingYardType GetShuntingYardType(ISyntax syntax) => compiler!.GetShuntingYardType(syntax);
    }
}

