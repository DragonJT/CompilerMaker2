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
        int Type { get; }
        string ToString();
    }

    enum ErrorType { OutOfRange, ExpectingDifferentToken, WhileNotLongEnough, NoValidBranches }

    class SyntaxError : ISyntax
    {
        public int Type { get; }
        string code;
        int index;
        ErrorType errorType;

        public SyntaxError(TokenReader reader, ErrorType errorType)
        {
            Type = JEnum.Error;
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
        public int Type { get; }
        public List<ISyntax> children;
        public object? data;

        public SyntaxTree(int type, List<ISyntax> children)
        {
            Type = type;
            this.children = children;
        }

        public override string ToString()
        {
            var result = JEnum.GetName(Type)+"[";
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

    interface IParse
    {
        ISyntax Parse(TokenReader reader);
    }

    class PsToken : IParse
    {
        int type;

        public PsToken(string type)
        {
            this.type = JEnum.Get(type);
        }

        public ISyntax Parse(TokenReader reader)
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

    class PsWhile : IParse
    {
        int type;
        protected IParse element;
        int minLength;

        public PsWhile(string type, IParse element, int minLength)
        {
            this.type = JEnum.Get(type);
            this.element = element;
            this.minLength = minLength;
        }

        public ISyntax Parse(TokenReader reader)
        {
            List<ISyntax> children = new();
            while (true)
            {
                var p = element.Parse(reader);
                if (p.Type == JEnum.Error)
                {
                    if (children.Count < minLength)
                        return new SyntaxError(reader, ErrorType.WhileNotLongEnough);
                    return new SyntaxTree(type, children);
                }
                children.Add(p);
            }
        }
    }

    class PsWhileWithDeliminator : IParse
    {
        int type;
        protected IParse element;
        IParse deliminator;
        bool strict;

        public PsWhileWithDeliminator(string type, IParse element, IParse deliminator, bool strict)
        {
            this.type = JEnum.Get(type);
            this.element = element;
            this.deliminator = deliminator;
            this.strict = strict;
        }

        public ISyntax Parse(TokenReader reader)
        {
            var length = 0;
            List<ISyntax> children = new();
            while (true)
            {
                var p = element.Parse(reader);
                if (p.Type == JEnum.Error)
                {
                    if (length == 0 || !strict)
                        return new SyntaxTree(type, children);
                    return p;
                }
                children.Add(p);
                var dp = deliminator.Parse(reader);
                if (dp.Type == JEnum.Error)
                    return new SyntaxTree(type, children);
                length++;
            }
        }
    }

    class PsOr : IParse
    {
        IParse[] branches;

        public PsOr(params IParse[] branches)
        {
            this.branches = branches;
        }

        public ISyntax Parse(TokenReader reader)
        {
            foreach(var b in branches)
            {
                var p = b.Parse(reader);
                if (p.Type != JEnum.Error)
                    return p;
            }
            return new SyntaxError(reader, ErrorType.NoValidBranches);
        }
    }

    class PsDecoratedValue : IParse
    {
        int id;
        IParse[] fields;

        public PsDecoratedValue(int id, params IParse[] fields)
        {
            this.id = id;
            this.fields = fields;
        }

        public ISyntax Parse(TokenReader reader)
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
    }

    class PsObject:IParse
    {
        int type;
        IParse[] fields;

        public PsObject(string type, params IParse[] fields)
        {
            this.type = JEnum.Get(type);
            this.fields = fields;
        }

        public ISyntax Parse(TokenReader reader)
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

    class PsCircular : IParse
    {
        public IParse? compiler;

        public ISyntax Parse(TokenReader reader) => compiler!.Parse(reader);
    }
}

