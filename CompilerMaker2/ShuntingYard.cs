using System;
using System.Linq;

namespace CompilerMaker2
{
    enum Associative { Left, Right }

    class BinaryOp
    {
        public Associative associative;
        public int precedence;

        public BinaryOp(Associative associative, int precedence)
        {
            this.associative = associative;
            this.precedence = precedence;
        }
    }

    class ShuntingYard
    {
        Dictionary<int, BinaryOp> operators = new();
        HashSet<int> expressions = new();

        public void Add(string type, BinaryOp binaryOp)
        {
            operators.Add(JEnum.Get(type), binaryOp);
        }

        public void AddExpression(string type)
        {
            expressions.Add(JEnum.Get(type));
        }

        List<ISyntax> CalcRPN(ISyntax syntax)
        {
            var tree = (SyntaxTree)syntax;
            var stack = new Stack<ISyntax>();
            var output = new List<ISyntax>();

            foreach (var c in tree.children)
            {
                if(!operators.TryGetValue(c.type, out BinaryOp op))
                {
                    output.Add(c);
                }
                else
                {
                    while (stack.Count > 0)
                    {
                        var associative = op.associative;
                        var precedence = op.precedence;
                        var stackPeekPrecedence = operators[stack.Peek().type].precedence;

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
            }
            while (stack.Count > 0)
            {
                output.Add(stack.Pop());
            }
            return output;
        }

        ISyntax CalcTree(ISyntax syntax)
        {
            var rpn = CalcRPN(syntax);
            var stack = new Stack<ISyntax>();

            foreach(var o in rpn)
            {
                if (!operators.ContainsKey(o.type))
                {
                    stack.Push(Transform(o));
                }
                else
                {
                    var b = stack.Pop();
                    var a = stack.Pop();
                    stack.Push(new SyntaxTree(o.type, new List<ISyntax> { a, b }));
                }
            }
            if(stack.Count == 1)
                return stack.Pop();
            throw new Exception("stack has " + stack.Count + "items left");
        }

        public ISyntax Transform(ISyntax syntax)
        {
            if (expressions.Contains(syntax.type))
            {
                return CalcTree(syntax);
            }
            else
            {
                if (syntax is SyntaxTree tree)
                {
                    tree.children = tree.children.Select(c => Transform(c)).ToList();
                }
                return syntax;
            }
        }
    }
}

