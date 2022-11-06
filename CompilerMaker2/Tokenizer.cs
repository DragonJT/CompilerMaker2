using System;
namespace CompilerMaker2
{
    static class JEnum
    {
        static Dictionary<string, int> enums = new();

        public static int Get(string @enum)
        {
            if (enums.TryGetValue(@enum, out int value))
                return value;
            var id = enums.Count;
            enums.Add(@enum, id);
            return id;
        }

        public static string GetName(int id)
        {
            foreach(var kv in enums)
            {
                if (kv.Value == id)
                    return kv.Key;
            }
            throw new Exception("JEnum: Cannot find id");
        }
    }

    class CodeReader
    {
        public string code;
        public int index = 0;

        public CodeReader(string code)
        {
            this.code = code;
        }

        public char Current => code[index];

        public bool OutOfRange => index >= code.Length;
    }

    class Token:ISyntax
    {
        public int Type { get; }
        public string value;
        public int start;
        public int end;

        public Token(int type, string value, int start, int end)
        {
            Type = type;
            this.value = value;
            this.start = start;
            this.end = end;
        }

        public override string ToString()
        {
            return "(" + JEnum.GetName(Type) + ": " + value + ")";
        }
    }

    interface ITokenize
    {
        bool Tokenize(CodeReader reader);
    }

    class TkCharRange : ITokenize
    {
        char min;
        char max;

        public TkCharRange(char min, char max)
        {
            this.min = min;
            this.max = max;
        }

        public bool Tokenize(CodeReader reader)
        {
            if (reader.OutOfRange)
                return false;
            var c = reader.Current;
            if( c >= min && c <= max)
            {
                reader.index++;
                return true;
            }
            return false;
        }
    }

    class TkChar : ITokenize
    {
        char c;

        public TkChar(char c)
        {
            this.c = c;
        }

        public bool Tokenize(CodeReader reader)
        {
            if (reader.OutOfRange)
                return false;
            if(reader.Current == c)
            {
                reader.index++;
                return true;
            }
            return false;
        }
    }

    class TkWhile : ITokenize
    {
        ITokenize element;
        int minLength;

        public TkWhile(ITokenize element, int minLength)
        {
            this.element = element;
            this.minLength = minLength;
        }

        public bool Tokenize(CodeReader reader)
        {
            int length = 0;
            while (true)
            {
                if (element.Tokenize(reader))
                    length++;
                else
                    return length >= minLength;
            }
        }
    }

    class TkOperatorLiteral : ITokenize
    {
        string operatorLiteral;

        public TkOperatorLiteral(string operatorLiteral)
        {
            this.operatorLiteral = operatorLiteral;
        }

        public bool Tokenize(CodeReader reader)
        {
            foreach(var c in operatorLiteral)
            {
                if (reader.OutOfRange)
                    return false;
                if (c != reader.Current)
                    return false;
                reader.index++;
            }
            return true;
        }
    }

    class TkOr : ITokenize
    {
        ITokenize[] branches;

        public TkOr(params ITokenize[] branches)
        {
            this.branches = branches;
        }

        public bool Tokenize(CodeReader reader)
        {
            var start = reader.index;
            foreach(var b in branches)
            {
                reader.index = start;
                if (b.Tokenize(reader))
                    return true;
            }
            return false;
        }
    }

    class TkObject : ITokenize
    {
        ITokenize[] fields;

        public TkObject(params ITokenize[] fields)
        {
            this.fields = fields;
        }

        public bool Tokenize(CodeReader reader)
        {
            foreach(var f in fields)
            {
                if (!f.Tokenize(reader))
                    return false;
            }
            return true;
        }
    }

    class TkTokenCreator
    {
        public int type;
        public ITokenize tokenizer;
        public bool save;

        public TkTokenCreator(string type, ITokenize tokenizer, bool save)
        {
            this.type = JEnum.Get(type);
            this.tokenizer = tokenizer;
            this.save = save;
        }
    }

    class Tokenizer
    {
        List<TkTokenCreator> tokenCreators = new();

        public void AddPunctuation(params string[] ops)
        {
            foreach (var op in ops)
                tokenCreators.Add(new TkTokenCreator(op, new TkOperatorLiteral(op), true));
        }

        public void Add(string type, ITokenize tokenizer, bool save = true)
        {
            tokenCreators.Add(new TkTokenCreator(type, tokenizer, save));
        }

        public List<Token> Tokenize(string code)
        {
            var tokens = new List<Token>();
            var reader = new CodeReader(code);
            while (true)
            {
                if (reader.OutOfRange)
                    return tokens;
                bool tokenized = false;
                int start = reader.index;
                foreach(var tc in tokenCreators)
                {
                    reader.index = start;
                    if (tc.tokenizer.Tokenize(reader))
                    {
                        if (tc.save)
                            tokens.Add(new Token(tc.type, code.Substring(start, reader.index - start), start, reader.index - start));
                        tokenized = true;
                        break;
                    }
                }
                if (!tokenized)
                    throw new Exception("Tokenizer cannot tokenize: " + code.Substring(start));
            }
        }
    }
}

