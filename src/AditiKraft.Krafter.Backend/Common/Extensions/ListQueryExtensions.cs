using System.Linq.Dynamic.Core;
using System.Linq.Dynamic.Core.Exceptions;
using System.Linq.Expressions;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Common.Validation;

namespace AditiKraft.Krafter.Backend.Common.Extensions;

public static class ListQueryExtensions
{
    private static readonly GetRequestInputValidator Validator = new();
    private static readonly ParsingConfig Parsing = new()
    {
        DisallowNewKeyword = true,
        AllowEqualsAndToStringMethodsOnObject = false,
        RestrictOrderByToPropertyOrField = true,
        DateTimeIsParsedAsUTC = true
    };

    public static bool TryApplyListQuery<T>(this IQueryable<T> source, GetRequestInput request,
        IReadOnlySet<string> properties, out IQueryable<T> query, out string error)
    {
        query = source;
        var validation = Validator.Validate(request);
        if (!validation.IsValid)
        {
            error = string.Join(" ", validation.Errors.Select(failure => failure.ErrorMessage));
            return false;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(request.Filter))
            {
                Expression<Func<T, bool>> filter = DynamicExpressionParser.ParseLambda<T, bool>(
                    Parsing, false, request.Filter);
                new FilterValidator(filter.Parameters[0], properties).Visit(filter.Body);
                query = query.Where(filter);
            }

            if (!string.IsNullOrWhiteSpace(request.OrderBy))
            {
                // Sorting accepts only a projected property and an optional direction per column.
                foreach (string column in request.OrderBy.Split(','))
                {
                    string[] parts = column.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length is < 1 or > 2 || !properties.Contains(parts[0]) ||
                        (parts.Length == 2 && !parts[1].Equals("asc", StringComparison.OrdinalIgnoreCase) &&
                         !parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase)))
                    {
                        error = "OrderBy must contain supported properties with optional asc or desc directions.";
                        return false;
                    }
                }
                query = query.OrderBy(Parsing, request.OrderBy);
            }
        }
        catch (Exception exception) when (exception is ParseException or ArgumentException)
        {
            error = "Filter or OrderBy contains an invalid or unsupported expression.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    // Validate the parsed tree, not substrings in user text. These operations cover the current
    // Radzen string/boolean filters, including np(...), case folding, and combined conditions.
    // Restricting members and methods also prevents provider-unsupported calls reaching EF.
    private sealed class FilterValidator(ParameterExpression parameter, IReadOnlySet<string> properties)
        : ExpressionVisitor
    {
        private int nodes;
        private int classifiedNodes;
        private readonly Dictionary<Expression, TextKind> textKinds = [];
        private readonly Dictionary<Expression, bool> nullGuards = [];

        public override Expression? Visit(Expression? node)
        {
            if (node is null)
                return null;

            if (++nodes > 256 || node.NodeType is not (
                    ExpressionType.AndAlso or ExpressionType.OrElse or ExpressionType.Not or
                    ExpressionType.Equal or ExpressionType.NotEqual or ExpressionType.LessThan or
                    ExpressionType.LessThanOrEqual or ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual or
                    ExpressionType.Coalesce or ExpressionType.Conditional or ExpressionType.Convert or
                    ExpressionType.Constant or ExpressionType.Parameter or ExpressionType.MemberAccess or ExpressionType.Call))
            {
                throw new ArgumentException("Unsupported filter expression.");
            }

            return base.Visit(node);
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (node != parameter)
                throw new ArgumentException("Unsupported filter parameter.");
            return node;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression != parameter || !properties.Contains(node.Member.Name))
                throw new ArgumentException("Unsupported filter property.");
            return base.VisitMember(node);
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            // Validate the parsed literal too: an escaped character need not appear in the raw filter.
            if (node.Value is string value && !TextValue.IsValid(value))
                throw new ArgumentException("Filter values must contain valid Unicode text without null characters.");
            return node;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            bool caseConversion = (node.Method.Name is nameof(string.ToLower) or nameof(string.ToUpper)) &&
                                  node.Arguments.Count == 0;
            bool stringComparison = (node.Method.Name is nameof(string.Contains) or nameof(string.StartsWith) or
                                        nameof(string.EndsWith) or nameof(string.Equals)) &&
                                    node.Arguments.Count == 1 && IsLiteralText(node.Arguments[0]);
            bool nullCheck = (node.Method.Name is nameof(string.IsNullOrEmpty) or nameof(string.IsNullOrWhiteSpace)) &&
                             node.Object is null && node.Arguments.Count == 1 && node.Arguments[0].Type == typeof(string);
            if (node.Method.DeclaringType != typeof(string) ||
                (!nullCheck && (node.Object is null || (!IsRowText(node.Object) && !IsLiteralText(node.Object)) ||
                                (!caseConversion && !stringComparison))))
            {
                throw new ArgumentException("Unsupported filter method.");
            }
            return base.VisitMethodCall(node);
        }

        private bool IsLiteralText(Expression node) => ClassifyText(node) == TextKind.Literal;

        private bool IsRowText(Expression node) => ClassifyText(node) == TextKind.Row;

        // Keep computed receivers rooted in projected data. EF may evaluate an entirely
        // constant expression before SQL translation, where null.ToLower() would throw.
        private TextKind ClassifyText(Expression node)
        {
            if (textKinds.TryGetValue(node, out TextKind cached)) return cached;
            CountClassification();
            TextKind result = node switch
            {
                ConstantExpression { Value: string } => TextKind.Literal,
                ConstantExpression { Value: null } => TextKind.Null,
                MemberExpression member when member.Type == typeof(string) && member.Expression == parameter &&
                                             properties.Contains(member.Member.Name) => TextKind.Row,
                MethodCallExpression { Object: { } value, Arguments.Count: 0 } call when
                    call.Method.DeclaringType == typeof(string) &&
                    (call.Method.Name is nameof(string.ToLower) or nameof(string.ToUpper)) &&
                    ClassifyText(value) is TextKind.Row or TextKind.Literal => ClassifyText(value),
                BinaryExpression { NodeType: ExpressionType.Coalesce } coalesce when
                    IsRowText(coalesce.Left) && IsTextBranch(coalesce.Right) => TextKind.Row,
                ConditionalExpression conditional when IsNullGuard(conditional.Test) &&
                    IsTextBranch(conditional.IfTrue) && IsTextBranch(conditional.IfFalse) &&
                    (IsRowText(conditional.IfTrue) || IsRowText(conditional.IfFalse)) => TextKind.Row,
                _ => TextKind.Unsupported
            };
            textKinds[node] = result;
            return result;
        }

        private bool IsTextBranch(Expression node) =>
            ClassifyText(node) is TextKind.Null or TextKind.Literal or TextKind.Row;

        private bool IsNullGuard(Expression node)
        {
            if (nullGuards.TryGetValue(node, out bool cached)) return cached;
            CountClassification();
            bool result = node is BinaryExpression binary && (binary.NodeType is ExpressionType.AndAlso or ExpressionType.OrElse
                ? IsNullGuard(binary.Left) && IsNullGuard(binary.Right)
                : (binary.NodeType is ExpressionType.Equal or ExpressionType.NotEqual) &&
                  ((binary.Left is ConstantExpression { Value: null } && IsNullGuardValue(binary.Right)) ||
                   (binary.Right is ConstantExpression { Value: null } && IsNullGuardValue(binary.Left))));
            nullGuards[node] = result;
            return result;
        }

        private bool IsNullGuardValue(Expression node) => node == parameter || IsRowText(node);

        private void CountClassification()
        {
            // Memoize each classification, including failures, before the main visitor
            // revisits it. Nested conditional receivers must not duplicate recursive work.
            if (++classifiedNodes > 512) throw new ArgumentException("Filter expression is too complex.");
        }

        private enum TextKind { Unsupported, Null, Literal, Row }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            if (node.Method is not null || (node.NodeType == ExpressionType.Convert &&
                Nullable.GetUnderlyingType(node.Type) != node.Operand.Type))
            {
                throw new ArgumentException("Unsupported filter conversion.");
            }
            return base.VisitUnary(node);
        }
    }
}
