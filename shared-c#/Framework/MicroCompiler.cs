using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace AppInstall.Framework
{
    public static class MicroCompiler
    {

        public class SyntaxError : Exception
        {
            public SyntaxError(string message, int position)
                : base("syntax error at position " + position + ": " + message)
            {
            }
        }

        public class SymbolError : Exception
        {
            public SymbolError(string memberType, Type type, string memberName)
                : base("the type " + type + " has no " + memberType + " \"" + memberName + "\"")
            {
            }
        }

        public class RuntimeError : Exception
        {
            public RuntimeError(Type type, string memberName, Exception ex)
                : base("the execution of " + type + "." + memberName + " failed: " + ex.Message, ex)
            {
            }
        }


        public abstract class Expression
        {
            public abstract object Invoke();
        }

        public class ConstantExpression : Expression
        {
            private object constant;
            public ConstantExpression(object constant)
            {
                this.constant = constant;
            }
            public override object Invoke()
            {
                return constant;
            }
        }

        public class PropertyExpression : Expression
        {
            private Expression parent;
            private string name;
            public PropertyExpression(Expression parent, string name)
            {
                this.parent = parent;
                this.name = name;
            }
            public override object Invoke()
            {
                var instance = parent.Invoke();
                var type = instance.GetType();
                var property = type.GetProperty(name);
                if (property == null) throw new SymbolError("property", type, name);
                return property.GetValue(instance);
            }
        }

        public class MethodExpression : Expression
        {
            private Expression parent;
            private string name;
            private IEnumerable<Expression> args;
            public MethodExpression(Expression parent, string name, IEnumerable<Expression> args)
            {
                this.parent = parent;
                this.name = name;
                this.args = args;
            }
            public override object Invoke()
            {
                var instance = parent.Invoke();
                object[] arguments = args.Select((a) => a.Invoke()).ToArray();
                Type[] argTypes = arguments.Select((a) => a.GetType()).ToArray();
                var type = instance.GetType();
                var method = type.GetMethod(name, argTypes);
                if (method == null) throw new SymbolError("method", type, name + "(" + string.Join(", ", argTypes.Select((t) => t.ToString())) + ")");
                try {
                    return method.Invoke(instance, arguments);
                } catch (TargetInvocationException ex) {
                    throw new RuntimeError(type, name, ex.InnerException);
                }
            }
        }




        /// <summary>
        /// Consumes an expression.
        /// An expression can be a string or numerical constant or a member of the specified scope that is optionally invoked with the provided argument list.
        /// Any valid Expression.Member.SubMember[...] chain is also accepted.
        /// Valid whitespaces and parenthesis within the expression are also supported.
        /// </summary>
        public static Expression ParseExpression(string str, ref int position, Expression scope)
        {
            str.ConsumeWhitespace(ref position);

            Expression result;

            if (char.IsLetter(str[position]) || str[position] == '_') result = ParseMember(str, ref position, scope, scope);
            else if (str[position] == '\"') result = ParseString(str, ref position);
            else if (char.IsDigit(str[position]) || str[position] == '-') result = ParseNumber(str, ref position);
            else if (str[position] == '(') {
                position++;
                str.ConsumeWhitespace(ref position);
                result = ParseExpression(str, ref position, scope);
                if (str[position] != ')') throw new SyntaxError("')' expected", position);
                position++;
            } else throw new SyntaxError("expression expected", position);

            while (str[position] == '.') {
                position++;
                result = ParseMember(str, ref position, scope, result);
            }

            return result;
        }

        /// <summary>
        /// Consumes a type member, including an optional argument list. Whitespace in the argument list is ignored.
        /// </summary>
        public static Expression ParseMember(string str, ref int position, Expression scope, Expression parent)
        {
            var start = position;
            while (char.IsLetterOrDigit(str[position]) || str[position] == '_') position++;
            var member = str.Substring(start, position - start);

            if (str[position] != '(') return new PropertyExpression(parent, member);
            position++;
            str.ConsumeWhitespace(ref position);

            List<Expression> args = new List<Expression>();

            if (str[position] != ')') {
                while (true) {
                    args.Add(ParseExpression(str, ref position, scope));
                    str.ConsumeWhitespace(ref position);
                    if (str[position] != ',') break;
                    position++;
                }
            }

            if (str[position] != ')') throw new SyntaxError("')' expected", position);
            position++;

            return new MethodExpression(parent, member, args);
        }

        /// <summary>
        /// Consumes a 32-bit signed integer constant
        /// </summary>
        public static Expression ParseNumber(string str, ref int position)
        {
            var start = position;
            if (str[position] == '-') position++;
            while (char.IsDigit(str[++position])) ;
            int result;
            if (!int.TryParse(str.Substring(start, position - start), out result)) throw new SyntaxError("invalid number", position);
            return new ConstantExpression(result);
        }

        /// <summary>
        /// Consumes a string constant starting and ending in quotes (")
        /// </summary>
        public static Expression ParseString(string str, ref int position)
        {
            if (str[position] != '\"') throw new SyntaxError("expected '\"'", position);
            var start = ++position;
            while (str[position++] != '\"') ;
            return new ConstantExpression(str.Substring(start, position - 1 - start));
        }
    }
}
