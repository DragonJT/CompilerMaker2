using System;
namespace CompilerMaker2
{
    class TokenReader
    {
        public List<Token> tokens;
        public int index;

        public TokenReader(List<Token> tokens)
        {
            this.tokens = tokens;
            index = 0;
        }

        public bool OutOfRange => index >= tokens.Count;
        public Token Current => tokens[index];
    }

    interface ISyntax
    {
        int Type { get; }
    }

    enum ErrorType { OutOfRange, ExpectingDifferentToken, WhileNotLongEnough, NoValidBranches }

    class SyntaxError : ISyntax
    {
        public int Type => JEnum.Error;
        ErrorType errorType;

        public SyntaxError(ErrorType errorType)
        {
            this.errorType = errorType;
        }

        public override string ToString()
        {
            return errorType.ToString();
        }
    }

    class SyntaxTree : ISyntax
    {
        public int Type { get; }
        public List<ISyntax> children;

        public SyntaxTree(int type, List<ISyntax> children)
        {
            Type = type;
            this.children = children;
        }

        public override string ToString()
        {
            var result = JEnum.GetName(Type) + "[";
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
        public int Type => syntax.Type;
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

        public abstract void Emit(Emitter emitter, ISyntax syntax);

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
                return new SyntaxError(ErrorType.OutOfRange);
            var current = reader.Current;
            if (current.Type == type)
            {
                reader.index++;
                return current;
            }
            return new SyntaxError(ErrorType.ExpectingDifferentToken);
        }

        public override void Emit(Emitter emitter, ISyntax syntax)
        {
            throw new Exception("Cant emit CmToken");
        }
    }

    class CmWhile : ICompile
    {
        int type;
        protected ICompile element;
        int minLength;

        public CmWhile(string type, ICompile element, int minLength)
        {
            this.type = JEnum.Get(type);
            this.element = element;
            this.minLength = minLength;
        }

        public override ISyntax Parse(TokenReader reader)
        {
            List<ISyntax> children = new();
            while (true)
            {
                var p = element.Parse(reader);
                if (p.Type == JEnum.Error)
                {
                    if (children.Count < minLength)
                        return new SyntaxError(ErrorType.WhileNotLongEnough);
                    return new SyntaxTree(type, children);
                }
                children.Add(p);
            }
        }

        public override void Emit(Emitter emitter, ISyntax syntax)
        {
            var tree = (SyntaxTree)syntax;
            foreach(var c in tree.children)
            {
                element.Emit(emitter, c);
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
                if (p.Type != JEnum.Error)
                    return new SyntaxOr(p, b);
            }
            return new SyntaxError(ErrorType.NoValidBranches);
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

        public override void Emit(Emitter emitter, ISyntax syntax)
        {
            var syntaxOr = ((SyntaxOr)syntax);
            syntaxOr.branch.Emit(emitter, syntaxOr.syntax);
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
                if (p.Type == JEnum.Error)
                    return p;
            }
            var value = fields[id].Parse(reader);
            if (value.Type == JEnum.Error)
                return value;

            for (int i = id+1; i < fields.Length; i++)
            {
                var p = fields[i].Parse(reader);
                if (p.Type == JEnum.Error)
                    return p;
            }
            return value!;
        }

        public override void Emit(Emitter emitter, ISyntax syntax) => fields[id].Emit(emitter, syntax);

        public override Associative GetAssociative(ISyntax syntax) => fields[id].GetAssociative(syntax);

        public override int GetPrecedence(ISyntax syntax) => fields[id].GetPrecedence(syntax);

        public override ShuntingYardType GetShuntingYardType(ISyntax syntax) => fields[id].GetShuntingYardType(syntax);
    }

    abstract class CmObject:ICompile
    {
        int type;
        protected ICompile[] fields;

        public CmObject(string type, params ICompile[] fields)
        {
            this.type = JEnum.Get(type);
            this.fields = fields;
        }

        public override ISyntax Parse(TokenReader reader)
        {
            List<ISyntax> children = new();
            foreach(var f in fields)
            {
                var p = f.Parse(reader);
                if (p.Type == JEnum.Error)
                    return p;
                children.Add(p);
            }
            return new SyntaxTree(type, children);
        }
    }

    class CmCirular : ICompile
    {
        public ICompile? compiler;

        public override ISyntax Parse(TokenReader reader) => compiler!.Parse(reader);

        public override void Emit(Emitter emitter, ISyntax syntax) => compiler!.Emit(emitter, syntax);

        public override Associative GetAssociative(ISyntax syntax) => compiler!.GetAssociative(syntax);

        public override int GetPrecedence(ISyntax syntax) => compiler!.GetPrecedence(syntax);

        public override ShuntingYardType GetShuntingYardType(ISyntax syntax) => compiler!.GetShuntingYardType(syntax);
    }
}

