using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using WinterRose.WinterForgeSerializing.Formatting.HumanReadable.Parsing;
using WinterRose.WinterForgeSerializing.Formatting;
using WinterRose.WinterForgeSerializing.Formatting.HumanReadable.Binding;

namespace WinterRose.WinterForgeSerializing.Formatting.HumanReadable.Binding;

internal abstract class BoundNode
{
    public abstract BoundNodeKind Kind { get; }
}

internal enum StatementContext
{
    General,
    AnonymousType,
    ContainerBody,
    TemplateBody
}

internal abstract class BoundStatementNode : BoundNode;

internal abstract class BoundExpressionNode : BoundNode
{
    public abstract BoundTypeSymbol Type { get; }
}

internal enum BoundNodeKind
{
    Program,

    BlockStatement,
    ExpressionStatement,
    ReturnStatement,
    VariableDeclarationStatement,
    ObjectDeclarationStatement,
    ContainerDeclarationStatement,
    TemplateDeclarationStatement,
    IfStatement,
    WhileStatement,
    ForStatement,

    LiteralExpression,
    VariableExpression,
    AssignmentExpression,
    BinaryExpression,
    UnaryExpression,
    CallExpression,
    MemberAccessExpression,
    ThisExpression,
    CollectionExpression,
    DictionaryExpression
}

internal sealed class BoundProgramNode : BoundNode
{
    public BoundProgramNode(
        IReadOnlyList<BoundStatementNode> statements)
    {
        Statements = statements;
    }

    public IReadOnlyList<BoundStatementNode> Statements { get; }

    public override BoundNodeKind Kind =>
        BoundNodeKind.Program;
}
internal sealed class BoundBlockStatementNode : BoundStatementNode
{
    public BoundBlockStatementNode(
        IReadOnlyList<BoundStatementNode> statements)
    {
        Statements = statements;
    }

    public IReadOnlyList<BoundStatementNode> Statements { get; }

    public override BoundNodeKind Kind =>
        BoundNodeKind.BlockStatement;
}
internal sealed class BoundExpressionStatementNode : BoundStatementNode
{
    public BoundExpressionStatementNode(
        BoundExpressionNode expression)
    {
        Expression = expression;
    }

    public BoundExpressionNode Expression { get; }

    public override BoundNodeKind Kind =>
        BoundNodeKind.ExpressionStatement;
}
internal sealed class BoundReturnStatementNode : BoundStatementNode
{
    public BoundReturnStatementNode(
        BoundExpressionNode? expression)
    {
        Expression = expression;
    }

    public BoundExpressionNode? Expression { get; }

    public override BoundNodeKind Kind =>
        BoundNodeKind.ReturnStatement;
}
internal sealed class BoundContainerDeclarationStatementNode : BoundStatementNode
{
    public BoundContainerDeclarationStatementNode(
        BoundContainerSymbol symbol,
        BoundBlockStatementNode body)
    {
        Symbol = symbol;
        Body = body;
    }

    public BoundContainerSymbol Symbol { get; }

    public BoundBlockStatementNode Body { get; }

    public override BoundNodeKind Kind =>
        BoundNodeKind.ContainerDeclarationStatement;
}
internal sealed class BoundTemplateDeclarationStatementNode : BoundStatementNode
{
    public BoundTemplateDeclarationStatementNode(
        BoundTemplateSymbol symbol,
        BoundBlockStatementNode body)
    {
        Symbol = symbol;
        Body = body;
    }

    public BoundTemplateSymbol Symbol { get; }

    public BoundBlockStatementNode Body { get; }

    public override BoundNodeKind Kind =>
        BoundNodeKind.TemplateDeclarationStatement;
}
internal sealed class BoundIfStatementNode : BoundStatementNode
{
    public BoundIfStatementNode(
        BoundExpressionNode condition,
        BoundBlockStatementNode thenBlock,
        BoundStatementNode? elseStatement)
    {
        Condition = condition;
        ThenBlock = thenBlock;
        ElseStatement = elseStatement;
    }

    public BoundExpressionNode Condition { get; }

    public BoundBlockStatementNode ThenBlock { get; }

    public BoundStatementNode? ElseStatement { get; }

    public override BoundNodeKind Kind =>
        BoundNodeKind.IfStatement;
}
internal sealed class BoundWhileStatementNode : BoundStatementNode
{
    public BoundWhileStatementNode(
        BoundExpressionNode condition,
        BoundBlockStatementNode body)
    {
        Condition = condition;
        Body = body;
    }

    public BoundExpressionNode Condition { get; }

    public BoundBlockStatementNode Body { get; }

    public override BoundNodeKind Kind =>
        BoundNodeKind.WhileStatement;
}
internal sealed class BoundForStatementNode : BoundStatementNode
{
    public BoundForStatementNode(
        BoundExpressionNode? initializer,
        BoundExpressionNode? condition,
        BoundExpressionNode? iterator,
        BoundBlockStatementNode body)
    {
        Initializer = initializer;
        Condition = condition;
        Iterator = iterator;
        Body = body;
    }

    public BoundExpressionNode? Initializer { get; }

    public BoundExpressionNode? Condition { get; }

    public BoundExpressionNode? Iterator { get; }

    public BoundBlockStatementNode Body { get; }

    public override BoundNodeKind Kind =>
        BoundNodeKind.ForStatement;
}
internal sealed class BoundLiteralExpressionNode : BoundExpressionNode
{
    public BoundLiteralExpressionNode(
        object? value,
        BoundTypeSymbol type)
    {
        Value = value;
        this.type = type;
    }

    private readonly BoundTypeSymbol type;

    public object? Value { get; }

    public override BoundTypeSymbol Type =>
        type;

    public override BoundNodeKind Kind =>
        BoundNodeKind.LiteralExpression;
}
internal sealed class BoundVariableExpressionNode : BoundExpressionNode
{
    public BoundVariableExpressionNode(
        BoundVariableSymbol variable)
    {
        Variable = variable;
    }

    public BoundVariableSymbol Variable { get; }

    public override BoundTypeSymbol Type =>
        Variable.Type;

    public override BoundNodeKind Kind =>
        BoundNodeKind.VariableExpression;
}
internal sealed class BoundAssignmentExpressionNode : BoundExpressionNode
{
    public BoundAssignmentExpressionNode(
        BoundExpressionNode target,
        BoundExpressionNode expression)
    {
        Target = target;
        Expression = expression;
    }

    public BoundExpressionNode Target { get; }

    public BoundExpressionNode Expression { get; }

    public override BoundTypeSymbol Type =>
        Expression.Type;

    public override BoundNodeKind Kind =>
        BoundNodeKind.AssignmentExpression;
}
internal sealed class BoundBinaryExpressionNode : BoundExpressionNode
{
    public BoundBinaryExpressionNode(
        BoundExpressionNode left,
        BoundBinaryOperator op,
        BoundExpressionNode right,
        BoundTypeSymbol resultType)
    {
        Left = left;
        Operator = op;
        Right = right;
        this.resultType = resultType;
    }

    private readonly BoundTypeSymbol resultType;

    public BoundExpressionNode Left { get; }

    public BoundBinaryOperator Operator { get; }

    public BoundExpressionNode Right { get; }

    public override BoundTypeSymbol Type =>
        resultType;

    public override BoundNodeKind Kind =>
        BoundNodeKind.BinaryExpression;
}
internal sealed class BoundUnaryExpressionNode : BoundExpressionNode
{
    public BoundUnaryExpressionNode(
        BoundUnaryOperator op,
        BoundExpressionNode operand,
        BoundTypeSymbol resultType)
    {
        Operator = op;
        Operand = operand;
        this.resultType = resultType;
    }

    private readonly BoundTypeSymbol resultType;

    public BoundUnaryOperator Operator { get; }

    public BoundExpressionNode Operand { get; }

    public override BoundTypeSymbol Type =>
        resultType;

    public override BoundNodeKind Kind =>
        BoundNodeKind.UnaryExpression;
}
internal sealed class BoundCallExpressionNode : BoundExpressionNode
{
    public BoundCallExpressionNode(
        BoundExpressionNode target,
        IReadOnlyList<BoundExpressionNode> arguments,
        BoundTemplateSymbol template)
    {
        Target = target;
        Arguments = arguments;
        Template = template;
    }

    public BoundExpressionNode Target { get; }

    public IReadOnlyList<BoundExpressionNode> Arguments { get; }

    public BoundTemplateSymbol Template { get; }

    public override BoundTypeSymbol Type =>
        Template.ReturnType;

    public override BoundNodeKind Kind =>
        BoundNodeKind.CallExpression;
}
internal sealed class BoundMemberAccessExpressionNode : BoundExpressionNode
{
    public BoundMemberAccessExpressionNode(
        BoundExpressionNode target,
        BoundSymbol member,
        BoundTypeSymbol type)
    {
        Target = target;
        Member = member;
        this.type = type;
    }

    private readonly BoundTypeSymbol type;

    public BoundExpressionNode Target { get; }

    public BoundSymbol Member { get; }

    public override BoundTypeSymbol Type =>
        type;

    public override BoundNodeKind Kind =>
        BoundNodeKind.MemberAccessExpression;
}
internal sealed class BoundThisExpressionNode : BoundExpressionNode
{
    public BoundThisExpressionNode(
        BoundContainerSymbol container)
    {
        Container = container;
    }

    public BoundContainerSymbol Container { get; }

    public override BoundTypeSymbol Type =>
        Container;

    public override BoundNodeKind Kind =>
        BoundNodeKind.ThisExpression;
}
internal sealed class BoundCollectionExpressionNode : BoundExpressionNode
{
    public BoundCollectionExpressionNode(
        BoundTypeSymbol elementType,
        IReadOnlyList<BoundExpressionNode> items,
        BoundTypeSymbol collectionType)
    {
        ElementType = elementType;
        Items = items;
        this.collectionType = collectionType;
    }

    private readonly BoundTypeSymbol collectionType;

    public BoundTypeSymbol ElementType { get; }

    public IReadOnlyList<BoundExpressionNode> Items { get; }

    public override BoundTypeSymbol Type =>
        collectionType;

    public override BoundNodeKind Kind =>
        BoundNodeKind.CollectionExpression;
}
internal sealed class BoundDictionaryExpressionNode : BoundExpressionNode
{
    public BoundDictionaryExpressionNode(
        BoundTypeSymbol keyType,
        BoundTypeSymbol valueType,
        IReadOnlyList<BoundDictionaryEntryNode> entries,
        BoundTypeSymbol dictionaryType)
    {
        KeyType = keyType;
        ValueType = valueType;
        Entries = entries;
        this.dictionaryType = dictionaryType;
    }

    private readonly BoundTypeSymbol dictionaryType;

    public BoundTypeSymbol KeyType { get; }

    public BoundTypeSymbol ValueType { get; }

    public IReadOnlyList<BoundDictionaryEntryNode> Entries { get; }

    public override BoundTypeSymbol Type =>
        dictionaryType;

    public override BoundNodeKind Kind =>
        BoundNodeKind.DictionaryExpression;
}
internal sealed class BoundDictionaryEntryNode
{
    public BoundDictionaryEntryNode(
        BoundExpressionNode key,
        BoundExpressionNode value)
    {
        Key = key;
        Value = value;
    }

    public BoundExpressionNode Key { get; }

    public BoundExpressionNode Value { get; }
}
internal static class HumanReadableBinder
{
    public static BoundProgramNode Bind(
        HumanReadableProgramNode program,
        BoundScope rootScope)
    {
        BinderContext context = new(rootScope);

        List<BoundStatementNode> statements = [];

        foreach (HumanReadableStatementNode statement in program.Statements)
            statements.Add(BindStatement(context, statement));

        return new BoundProgramNode(statements);
    }

    private static BoundStatementNode BindStatement(
        BinderContext context,
        HumanReadableStatementNode statement)
    {
        return statement switch
        {
            HumanReadableExpressionStatementNode node =>
                new BoundExpressionStatementNode(
                    BindExpression(context, node.Expression)),

            HumanReadableReturnStatementNode node =>
                new BoundReturnStatementNode(
                    node.Expression is null
                        ? null
                        : BindExpression(context, node.Expression)),

            HumanReadableContainerDeclarationNode node =>
                BindContainer(context, node),

            HumanReadableTemplateDeclarationNode node =>
                BindTemplate(context, node),

            HumanReadableIfStatementNode node =>
                BindIf(context, node),

            HumanReadableWhileStatementNode node =>
                BindWhile(context, node),

            HumanReadableForStatementNode node =>
                BindFor(context, node),

            HumanReadableBlockStatementNode node =>
                BindBlock(context, node),

            _ => throw new Exception(
                $"Unsupported statement '{statement.GetType().Name}'.")
        };
    }

    private static BoundBlockStatementNode BindBlock(
        BinderContext parentContext,
        HumanReadableBlockStatementNode block)
    {
        BinderContext context =
            new(parentContext.Scope);

        List<BoundStatementNode> statements = [];

        foreach (HumanReadableStatementNode statement in block.Statements)
            statements.Add(BindStatement(context, statement));

        return new BoundBlockStatementNode(statements);
    }

    private static BoundStatementNode BindContainer(
        BinderContext context,
        HumanReadableContainerDeclarationNode node)
    {
        BoundContainerSymbol symbol =
            new(node.Name);

        context.Scope.Declare(symbol);

        BinderContext child =
            new(new BoundScope(context.Scope));

        child.CurrentContainer = symbol;

        BoundBlockStatementNode body =
            BindBlock(
                child,
                new HumanReadableBlockStatementNode(
                    node.Members,
                    node.Start,
                    node.End,
                    node.Text));

        return new BoundContainerDeclarationStatementNode(
            symbol,
            body);
    }

    private static BoundStatementNode BindTemplate(
        BinderContext context,
        HumanReadableTemplateDeclarationNode node)
    {
        BoundTypeSymbol returnType =
            node.ReturnType is null
                ? BoundBuiltInTypes.Void
                : BindType(context, node.ReturnType);

        BoundTemplateSymbol symbol =
            new(node.Name, returnType);

        context.Scope.Declare(symbol);

        BinderContext child =
            new(new BoundScope(context.Scope));

        foreach (HumanReadableParameterNode parameter in node.Parameters)
        {
            BoundTypeSymbol parameterType =
                parameter.TypeReference is null
                    ? BoundBuiltInTypes.Any
                    : BindTypeReference(child, parameter.TypeReference);

            BoundVariableSymbol variable =
                new(parameter.Name, parameterType);

            symbol.Parameters.Add(variable);

            child.Scope.Declare(variable);
        }

        BoundBlockStatementNode body =
            BindBlock(child, node.Body);

        return new BoundTemplateDeclarationStatementNode(
            symbol,
            body);
    }

    private static BoundStatementNode BindIf(
        BinderContext context,
        HumanReadableIfStatementNode node)
    {
        BoundExpressionNode condition =
            BindExpression(context, node.Condition);

        BoundBlockStatementNode thenBlock =
            BindBlock(context, node.ThenBlock);

        BoundStatementNode? elseStatement =
            node.ElseBranch is null
                ? null
                : BindStatement(context, node.ElseBranch);

        return new BoundIfStatementNode(
            condition,
            thenBlock,
            elseStatement);
    }

    private static BoundStatementNode BindWhile(
        BinderContext context,
        HumanReadableWhileStatementNode node)
    {
        return new BoundWhileStatementNode(
            BindExpression(context, node.Condition),
            BindBlock(context, node.Body));
    }

    private static BoundStatementNode BindFor(
        BinderContext context,
        HumanReadableForStatementNode node)
    {
        return new BoundForStatementNode(
            node.Initializer is null
                ? null
                : BindExpression(context, node.Initializer),

            node.Condition is null
                ? null
                : BindExpression(context, node.Condition),

            node.Iterator is null
                ? null
                : BindExpression(context, node.Iterator),

            BindBlock(context, node.Body));
    }

    private static BoundExpressionNode BindExpression(
        BinderContext context,
        HumanReadableExpressionNode expression)
    {
        return expression switch
        {
            HumanReadableLiteralExpressionNode node =>
                BindLiteral(node),

            HumanReadableIdentifierExpressionNode node =>
                BindIdentifier(context, node),

            HumanReadableAssignmentExpressionNode node =>
                BindAssignment(context, node),

            HumanReadableBinaryExpressionNode node =>
                BindBinary(context, node),

            HumanReadableUnaryExpressionNode node =>
                BindUnary(context, node),

            HumanReadableCallExpressionNode node =>
                BindCall(context, node),

            HumanReadableMemberAccessExpressionNode node =>
                BindMemberAccess(context, node),

            HumanReadableThisExpressionNode =>
                new BoundThisExpressionNode(
                    context.CurrentContainer!),

            _ => throw new Exception(
                $"Unsupported expression '{expression.GetType().Name}'.")
        };
    }
}

namespace WinterRose.WinterForgeSerializing.Formatting.HumanReadable.Binding;

internal abstract record BoundSymbol(
    string Name,
    BoundSymbolKind Kind,
    HumanReadableAstNode? Declaration);

internal enum BoundSymbolKind
{
    Type,
    TypeParameter,
    Variable,
    Parameter,
    Container,
    Template,
    Alias,
    Member
}

internal record BoundTypeSymbol : BoundSymbol
{
    public BoundTypeSymbol(string name, HumanReadableAstNode? declaration = null)
        : base(name, BoundSymbolKind.Type, declaration)
    {
    }
}

internal sealed record BoundTypeParameterSymbol : BoundTypeSymbol
{
    public BoundTypeParameterSymbol(string name, HumanReadableAstNode? declaration = null)
        : base(name, declaration)
    {
    }
}

internal sealed record BoundGenericTypeSymbol : BoundTypeSymbol
{
    public BoundGenericTypeSymbol(
        string name,
        BoundTypeSymbol baseType,
        IReadOnlyList<BoundTypeSymbol> typeArguments,
        HumanReadableAstNode? declaration = null)
        : base(name, declaration)
    {
        BaseType = baseType;
        TypeArguments = typeArguments;
    }

    public BoundTypeSymbol BaseType { get; }

    public IReadOnlyList<BoundTypeSymbol> TypeArguments { get; }
}

internal sealed record BoundCollectionTypeSymbol : BoundTypeSymbol
{
    public BoundCollectionTypeSymbol(
        BoundTypeSymbol itemType,
        HumanReadableAstNode? declaration = null)
        : base($"Collection<{itemType.Name}>", declaration)
    {
        ItemType = itemType;
    }

    public BoundTypeSymbol ItemType { get; }
}

internal sealed record BoundDictionaryTypeSymbol : BoundTypeSymbol
{
    public BoundDictionaryTypeSymbol(
        BoundTypeSymbol keyType,
        BoundTypeSymbol valueType,
        HumanReadableAstNode? declaration = null)
        : base($"Dictionary<{keyType.Name}, {valueType.Name}>", declaration)
    {
        KeyType = keyType;
        ValueType = valueType;
    }

    public BoundTypeSymbol KeyType { get; }

    public BoundTypeSymbol ValueType { get; }
}

internal sealed record BoundVariableSymbol : BoundSymbol
{
    public BoundVariableSymbol(
        string name,
        BoundTypeSymbol type,
        HumanReadableAstNode? declaration = null)
        : base(name, BoundSymbolKind.Variable, declaration)
    {
        Type = type;
    }

    public BoundTypeSymbol Type { get; }
}

internal sealed record BoundParameterSymbol : BoundSymbol
{
    public BoundParameterSymbol(
        string name,
        BoundTypeSymbol type,
        HumanReadableAstNode? declaration = null)
        : base(name, BoundSymbolKind.Parameter, declaration)
    {
        Type = type;
    }

    public BoundTypeSymbol Type { get; }
}

internal sealed record BoundMemberSymbol : BoundSymbol
{
    public BoundMemberSymbol(
        string name,
        BoundTypeSymbol type,
        HumanReadableAstNode? declaration = null)
        : base(name, BoundSymbolKind.Member, declaration)
    {
        Type = type;
    }

    public BoundTypeSymbol Type { get; }
}

internal sealed record BoundAliasSymbol : BoundSymbol
{
    public BoundAliasSymbol(
        string name,
        BoundExpressionNode target,
        HumanReadableAstNode? declaration = null)
        : base(name, BoundSymbolKind.Alias, declaration)
    {
        Target = target;
    }

    public BoundExpressionNode Target { get; }
}

internal sealed record BoundTemplateSymbol : BoundSymbol
{
    public BoundTemplateSymbol(
        string name,
        BoundTypeSymbol returnType,
        HumanReadableTemplateDeclarationNode declaration)
        : base(name, BoundSymbolKind.Template, declaration)
    {
        ReturnType = returnType;
        Parameters = [];
        Scope = new BoundScope(null);
    }

    public BoundTypeSymbol ReturnType { get; }

    public List<BoundParameterSymbol> Parameters { get; }

    public BoundScope Scope { get; }
}

internal sealed record BoundContainerSymbol : BoundTypeSymbol
{
    public BoundContainerSymbol(
        string name,
        HumanReadableContainerDeclarationNode declaration)
        : base(name, declaration)
    {
        Scope = new BoundScope(null);
        Members = [];
        Templates = [];
        NestedContainers = [];
        GenericParameters = [];
    }

    public BoundScope Scope { get; }

    public List<BoundTypeParameterSymbol> GenericParameters { get; }

    public Dictionary<string, BoundMemberSymbol> Members { get; }

    public Dictionary<string, BoundTemplateSymbol> Templates { get; }

    public Dictionary<string, BoundContainerSymbol> NestedContainers { get; }
}

internal sealed record BoundUnknownSymbol : BoundSymbol
{
    public BoundUnknownSymbol(string name, HumanReadableAstNode? declaration = null)
        : base(name, BoundSymbolKind.Type, declaration)
    {
    }
}

internal sealed class BoundScope
{
    private readonly Dictionary<string, BoundSymbol> symbols = new(StringComparer.Ordinal);

    public BoundScope(BoundScope? parent)
    {
        Parent = parent;
    }

    public BoundScope? Parent { get; }

    public bool TryDeclare(BoundSymbol symbol, out BoundSymbol? existing)
    {
        if (symbols.TryGetValue(symbol.Name, out existing))
            return false;

        symbols.Add(symbol.Name, symbol);
        existing = null;
        return true;
    }

    public bool TryLookup(string name, out BoundSymbol? symbol)
    {
        if (symbols.TryGetValue(name, out symbol))
            return true;

        if (Parent is not null)
            return Parent.TryLookup(name, out symbol);

        symbol = null;
        return false;
    }

    public BoundScope CreateChild() => new(this);
}

internal static class BoundBuiltInTypes
{
    public static readonly BoundTypeSymbol Any = new("any");
    public static readonly BoundTypeSymbol Void = new("void");
    public static readonly BoundTypeSymbol Number = new("number");
    public static readonly BoundTypeSymbol String = new("string");
    public static readonly BoundTypeSymbol Boolean = new("bool");
    public static readonly BoundTypeSymbol Type = new("type");
}

internal abstract record BoundNode;

internal abstract record BoundStatementNode : BoundNode;

internal abstract record BoundExpressionNode : BoundNode
{
    public abstract BoundTypeSymbol Type { get; }
}

internal sealed record BoundProgramNode(IReadOnlyList<BoundStatementNode> Statements) : BoundNode;

internal sealed record BoundBlockStatementNode(IReadOnlyList<BoundStatementNode> Statements) : BoundStatementNode;

internal sealed record BoundExpressionStatementNode(BoundExpressionNode Expression) : BoundStatementNode;

internal sealed record BoundReturnStatementNode(BoundExpressionNode? Expression) : BoundStatementNode;

internal sealed record BoundAliasStatementNode(BoundAliasSymbol AliasSymbol, BoundExpressionNode Target) : BoundStatementNode;

internal sealed record BoundImportStatementNode(BoundExpressionNode Source) : BoundStatementNode;

internal sealed record BoundObjectDeclarationStatementNode(
    BoundExpressionNode Head,
    BoundExpressionNode Reference,
    BoundBlockStatementNode? Body) : BoundStatementNode;

internal sealed record BoundAnonymousTypeDeclarationStatementNode(
    BoundTypeSymbol? NamedType,
    BoundTypeSymbol? BaseType,
    BoundExpressionNode Reference,
    BoundBlockStatementNode Body) : BoundStatementNode;

internal sealed record BoundIfStatementNode(
    BoundExpressionNode Condition,
    BoundBlockStatementNode ThenBlock,
    BoundStatementNode? ElseBranch) : BoundStatementNode;

internal sealed record BoundWhileStatementNode(
    BoundExpressionNode Condition,
    BoundBlockStatementNode Body) : BoundStatementNode;

internal sealed record BoundForStatementNode(
    BoundExpressionNode? Initializer,
    BoundExpressionNode? Condition,
    BoundExpressionNode? Iterator,
    BoundBlockStatementNode Body) : BoundStatementNode;

internal sealed record BoundContainerDeclarationStatementNode(
    BoundContainerSymbol Symbol,
    BoundBlockStatementNode Body) : BoundStatementNode;

internal sealed record BoundTemplateDeclarationStatementNode(
    BoundTemplateSymbol Symbol,
    BoundBlockStatementNode Body) : BoundStatementNode;

internal sealed record BoundAnonymousTypeMemberStatementNode(
    BoundMemberSymbol Symbol,
    BoundExpressionNode Value) : BoundStatementNode;

internal sealed record BoundLiteralExpressionNode(object? Value, BoundTypeSymbol BoundType) : BoundExpressionNode
{
    public override BoundTypeSymbol Type => BoundType;
}

internal sealed record BoundSymbolExpressionNode(BoundSymbol Symbol, BoundTypeSymbol BoundType) : BoundExpressionNode
{
    public override BoundTypeSymbol Type => BoundType;
}

internal sealed record BoundTypeExpressionNode(BoundTypeSymbol TypeSymbol) : BoundExpressionNode
{
    public override BoundTypeSymbol Type => BoundBuiltInTypes.Type;
}

internal sealed record BoundThisExpressionNode(BoundContainerSymbol Container) : BoundExpressionNode
{
    public override BoundTypeSymbol Type => Container;
}

internal enum BoundUnaryOperatorKind
{
    Identity,
    Negation,
    LogicalNegation
}

internal sealed record BoundUnaryOperator(string Text, BoundUnaryOperatorKind Kind);

internal sealed record BoundUnaryExpressionNode(
    BoundUnaryOperator Operator,
    BoundExpressionNode Operand,
    BoundTypeSymbol ResultType) : BoundExpressionNode
{
    public override BoundTypeSymbol Type => ResultType;
}

internal enum BoundBinaryOperatorKind
{
    Addition,
    Subtraction,
    Multiplication,
    Division,
    Modulo,
    Power,
    Equals,
    NotEquals,
    GreaterThan,
    LessThan,
    GreaterOrEqual,
    LessOrEqual,
    LogicalAnd,
    LogicalOr,
    LogicalXor
}

internal sealed record BoundBinaryOperator(string Text, BoundBinaryOperatorKind Kind);

internal sealed record BoundBinaryExpressionNode(
    BoundExpressionNode Left,
    BoundBinaryOperator Operator,
    BoundExpressionNode Right,
    BoundTypeSymbol ResultType) : BoundExpressionNode
{
    public override BoundTypeSymbol Type => ResultType;
}

internal sealed record BoundAssignmentExpressionNode(
    BoundExpressionNode Target,
    BoundExpressionNode Value) : BoundExpressionNode
{
    public override BoundTypeSymbol Type => Value.Type;
}

internal sealed record BoundCallExpressionNode(
    BoundExpressionNode Target,
    IReadOnlyList<BoundExpressionNode> Arguments,
    BoundTypeSymbol ResultType,
    BoundTemplateSymbol? Template = null,
    BoundTypeSymbol? ConstructedType = null) : BoundExpressionNode
{
    public override BoundTypeSymbol Type => ResultType;
}

internal sealed record BoundMemberAccessExpressionNode(
    BoundExpressionNode Target,
    BoundSymbol Member,
    BoundTypeSymbol ResultType) : BoundExpressionNode
{
    public override BoundTypeSymbol Type => ResultType;
}

internal sealed record BoundBuiltinCallExpressionNode(
    string Name,
    IReadOnlyList<BoundExpressionNode> Arguments,
    BoundTypeSymbol ResultType) : BoundExpressionNode
{
    public override BoundTypeSymbol Type => ResultType;
}

internal sealed record BoundCollectionExpressionNode(
    BoundTypeSymbol ItemType,
    IReadOnlyList<BoundExpressionNode> Items,
    BoundCollectionTypeSymbol ResultType) : BoundExpressionNode
{
    public override BoundTypeSymbol Type => ResultType;
}

internal sealed record BoundDictionaryEntryNode(
    BoundExpressionNode Key,
    BoundExpressionNode Value);

internal sealed record BoundDictionaryExpressionNode(
    BoundTypeSymbol KeyType,
    BoundTypeSymbol ValueType,
    IReadOnlyList<BoundDictionaryEntryNode> Entries,
    BoundDictionaryTypeSymbol ResultType) : BoundExpressionNode
{
    public override BoundTypeSymbol Type => ResultType;
}

internal sealed record BoundGroupedExpressionNode(BoundExpressionNode Expression) : BoundExpressionNode
{
    public override BoundTypeSymbol Type => Expression.Type;
}

internal static class HumanReadableBinder
{
    public static HumanReadableBindingResult Bind(HumanReadableProgramNode program)
    {
        BoundScope globalScope = CreateGlobalScope();
        HumanReadableDiagnosticBag diagnostics = new();
        BinderContext context = new(globalScope, diagnostics);

        List<BoundStatementNode> statements = [];
        foreach (HumanReadableStatementNode statement in program.Statements)
            statements.Add(BindStatement(context, statement, StatementContext.General));

        return new HumanReadableBindingResult(
            new BoundProgramNode(statements),
            globalScope,
            diagnostics);
    }

    private static BoundScope CreateGlobalScope()
    {
        BoundScope scope = new(null);

        scope.TryDeclare(BoundBuiltInTypes.Any, out _);
        scope.TryDeclare(BoundBuiltInTypes.Void, out _);
        scope.TryDeclare(BoundBuiltInTypes.Number, out _);
        scope.TryDeclare(BoundBuiltInTypes.String, out _);
        scope.TryDeclare(BoundBuiltInTypes.Boolean, out _);
        scope.TryDeclare(BoundBuiltInTypes.Type, out _);

        return scope;
    }

    private static BoundStatementNode BindStatement(BinderContext context, HumanReadableStatementNode statement, StatementContext statementContext)
    {
        switch (statement)
        {
            case HumanReadableBlockStatementNode blockStatement:
                return BindBlock(context, blockStatement, statementContext);

            case HumanReadableExpressionStatementNode expressionStatement:
                return new BoundExpressionStatementNode(BindExpression(context, expressionStatement.Expression));

            case HumanReadableReturnStatementNode returnStatement:
                return new BoundReturnStatementNode(
                    returnStatement.Expression is null
                        ? null
                        : BindExpression(context, returnStatement.Expression));

            case HumanReadableAliasStatementNode aliasStatement:
                return BindAlias(context, aliasStatement);

            case HumanReadableImportStatementNode importStatement:
                return new BoundImportStatementNode(BindExpression(context, importStatement.Source));

            case HumanReadableObjectDeclarationStatementNode objectDeclaration:
                return BindObjectDeclaration(context, objectDeclaration);

            case HumanReadableAnonymousTypeDeclarationStatementNode anonymousTypeDeclaration:
                return BindAnonymousTypeDeclaration(context, anonymousTypeDeclaration);

            case HumanReadableIfStatementNode ifStatement:
                return BindIf(context, ifStatement, statementContext);

            case HumanReadableWhileStatementNode whileStatement:
                return new BoundWhileStatementNode(
                    BindExpression(context, whileStatement.Condition),
                    BindBlock(context, whileStatement.Body, statementContext));

            case HumanReadableForStatementNode forStatement:
                return BindFor(context, forStatement, statementContext);

            case HumanReadableContainerDeclarationNode containerDeclaration:
                return BindContainer(context, containerDeclaration);

            case HumanReadableTemplateDeclarationNode templateDeclaration:
                return BindTemplate(context, templateDeclaration);

            case HumanReadableAnonymousTypeMemberStatementNode anonymousMember:
                return BindAnonymousTypeMember(context, anonymousMember);

            default:
                context.Diagnostics.AddError(
                    statement.Start,
                    statement.End,
                    $"Unsupported statement kind '{statement.NodeKind}'.");
                return new BoundExpressionStatementNode(new BoundLiteralExpressionNode(null, BoundBuiltInTypes.Any));
        }
    }

    private static BoundBlockStatementNode BindBlock(BinderContext context, HumanReadableBlockStatementNode blockStatement, StatementContext statementContext)
    {
        BinderContext child = context.CreateChild();
        List<BoundStatementNode> statements = [];

        foreach (HumanReadableStatementNode statement in blockStatement.Statements)
            statements.Add(BindStatement(child, statement, statementContext));

        return new BoundBlockStatementNode(statements);
    }

    private static BoundAliasStatementNode BindAlias(BinderContext context, HumanReadableAliasStatementNode aliasStatement)
    {
        BoundExpressionNode target = BindExpression(context, aliasStatement.Target);
        BoundAliasSymbol aliasSymbol = new(aliasStatement.Alias, target, aliasStatement);

        if (!context.Scope.TryDeclare(aliasSymbol, out BoundSymbol? existing))
        {
            context.Diagnostics.AddError(
                aliasStatement.Start,
                aliasStatement.End,
                $"The alias '{aliasStatement.Alias}' is already declared in this scope.",
                $"Remove the earlier '{existing?.Name}' declaration or rename this alias.");
            return new BoundAliasStatementNode(aliasSymbol, target);
        }

        return new BoundAliasStatementNode(aliasSymbol, target);
    }

    private static BoundStatementNode BindObjectDeclaration(BinderContext context, HumanReadableObjectDeclarationStatementNode objectDeclaration)
    {
        BoundExpressionNode head = BindExpression(context, objectDeclaration.Head);
        BoundExpressionNode reference = BindExpression(context, objectDeclaration.Reference);
        BoundBlockStatementNode? body = objectDeclaration.Body is null
            ? null
            : BindBlock(context, objectDeclaration.Body, StatementContext.General);

        return new BoundObjectDeclarationStatementNode(head, reference, body);
    }

    private static BoundStatementNode BindAnonymousTypeDeclaration(BinderContext context, HumanReadableAnonymousTypeDeclarationStatementNode anonymousTypeDeclaration)
    {
        BoundTypeSymbol? namedType = null;
        if (anonymousTypeDeclaration.Name is not null)
        {
            namedType = new BoundTypeSymbol(anonymousTypeDeclaration.Name, anonymousTypeDeclaration);
            if (!context.Scope.TryDeclare(namedType, out BoundSymbol? existing))
            {
                context.Diagnostics.AddError(
                    anonymousTypeDeclaration.Start,
                    anonymousTypeDeclaration.End,
                    $"The type name '{anonymousTypeDeclaration.Name}' is already declared in this scope.",
                    $"Rename this anonymous type or remove the earlier '{existing?.Name}' declaration.");
            }
        }

        BoundTypeSymbol? baseType = anonymousTypeDeclaration.BaseType is null
            ? null
            : BindTypeReference(context, anonymousTypeDeclaration.BaseType);

        BoundExpressionNode reference = BindExpression(context, anonymousTypeDeclaration.Reference);
        BoundBlockStatementNode body = BindBlock(context, anonymousTypeDeclaration.Body, StatementContext.AnonymousType);

        return new BoundAnonymousTypeDeclarationStatementNode(
            namedType,
            baseType,
            reference,
            body);
    }

    private static BoundStatementNode BindIf(BinderContext context, HumanReadableIfStatementNode ifStatement, StatementContext statementContext)
    {
        BoundExpressionNode condition = BindExpression(context, ifStatement.Condition);
        BoundBlockStatementNode thenBlock = BindBlock(context, ifStatement.ThenBlock, statementContext);

        BoundStatementNode? elseBranch = ifStatement.ElseBranch is null
            ? null
            : BindStatement(context.CreateChild(), ifStatement.ElseBranch, statementContext);

        return new BoundIfStatementNode(condition, thenBlock, elseBranch);
    }

    private static BoundStatementNode BindFor(BinderContext context, HumanReadableForStatementNode forStatement, StatementContext statementContext)
    {
        BoundExpressionNode? initializer = forStatement.Initializer is null
            ? null
            : BindExpression(context, forStatement.Initializer);

        BoundExpressionNode? condition = forStatement.Condition is null
            ? null
            : BindExpression(context, forStatement.Condition);

        BoundExpressionNode? iterator = forStatement.Iterator is null
            ? null
            : BindExpression(context, forStatement.Iterator);

        BoundBlockStatementNode body = BindBlock(context, forStatement.Body, statementContext);

        return new BoundForStatementNode(initializer, condition, iterator, body);
    }

    private static BoundContainerDeclarationStatementNode BindContainer(BinderContext context, HumanReadableContainerDeclarationNode containerDeclaration)
    {
        BoundContainerSymbol containerSymbol = new(containerDeclaration.Name, containerDeclaration);

        if (!context.Scope.TryDeclare(containerSymbol, out BoundSymbol? existing))
        {
            context.Diagnostics.AddError(
                containerDeclaration.Start,
                containerDeclaration.End,
                $"The container '{containerDeclaration.Name}' is already declared in this scope.",
                $"Rename this container or remove the earlier '{existing?.Name}' declaration.");
        }

        BinderContext containerContext = context.CreateChild(containerSymbol);

        foreach (HumanReadableParameterNode genericParameter in containerDeclaration.GenericParameters)
        {
            BoundTypeParameterSymbol typeParameter = new(genericParameter.Name, genericParameter);

            if (!containerContext.Scope.TryDeclare(typeParameter, out BoundSymbol? duplicate))
            {
                context.Diagnostics.AddError(
                    genericParameter.Start,
                    genericParameter.End,
                    $"The generic parameter '{genericParameter.Name}' is already declared in this container.",
                    $"Rename this parameter or remove the earlier '{duplicate?.Name}' declaration.");
                continue;
            }

            containerSymbol.GenericParameters.Add(typeParameter);
        }

        List<BoundStatementNode> bodyStatements = [];
        foreach (HumanReadableStatementNode member in containerDeclaration.Members)
        {
            BoundStatementNode boundMember = BindStatement(containerContext, member, StatementContext.ContainerBody);
            bodyStatements.Add(boundMember);

            if (boundMember is BoundTemplateDeclarationStatementNode templateDeclaration)
            {
                containerSymbol.Templates[templateDeclaration.Symbol.Name] = templateDeclaration.Symbol;
                continue;
            }

            if (boundMember is BoundContainerDeclarationStatementNode nestedContainer)
            {
                containerSymbol.NestedContainers[nestedContainer.Symbol.Name] = nestedContainer.Symbol;
                continue;
            }

            if (boundMember is BoundAnonymousTypeMemberStatementNode anonymousMember)
            {
                containerSymbol.Members[anonymousMember.Symbol.Name] = anonymousMember.Symbol;
                continue;
            }
        }

        BoundBlockStatementNode body = new(bodyStatements);
        return new BoundContainerDeclarationStatementNode(containerSymbol, body);
    }

    private static BoundTemplateDeclarationStatementNode BindTemplate(BinderContext context, HumanReadableTemplateDeclarationNode templateDeclaration)
    {
        BoundTypeSymbol returnType = templateDeclaration.ReturnType is null
            ? BoundBuiltInTypes.Void
            : BindTypeReference(context, templateDeclaration.ReturnType);

        BoundTemplateSymbol templateSymbol = new(templateDeclaration.Name, returnType, templateDeclaration);

        if (!context.Scope.TryDeclare(templateSymbol, out BoundSymbol? existing))
        {
            context.Diagnostics.AddError(
                templateDeclaration.Start,
                templateDeclaration.End,
                $"The template '{templateDeclaration.Name}' is already declared in this scope.",
                $"Rename this template or remove the earlier '{existing?.Name}' declaration.");
        }

        BinderContext templateContext = context.CreateChild(templateSymbol);

        foreach (HumanReadableParameterNode parameter in templateDeclaration.Parameters)
        {
            BoundTypeSymbol parameterType = parameter.TypeReference is null
                ? BoundBuiltInTypes.Any
                : BindTypeReference(context, parameter.TypeReference);

            if (parameter.TypeReference is null)
            {
                context.Diagnostics.AddError(
                    parameter.Start,
                    parameter.End,
                    $"Template parameter '{parameter.Name}' must have a type.",
                    "Add a type before the parameter name.");
            }

            BoundParameterSymbol parameterSymbol = new(parameter.Name, parameterType, parameter);

            if (!templateContext.Scope.TryDeclare(parameterSymbol, out BoundSymbol? duplicate))
            {
                context.Diagnostics.AddError(
                    parameter.Start,
                    parameter.End,
                    $"The template parameter '{parameter.Name}' is already declared in this template.",
                    $"Rename this parameter or remove the earlier '{duplicate?.Name}' declaration.");
                continue;
            }

            templateSymbol.Parameters.Add(parameterSymbol);
        }

        BoundBlockStatementNode body = BindBlock(templateContext, templateDeclaration.Body, StatementContext.TemplateBody);
        return new BoundTemplateDeclarationStatementNode(templateSymbol, body);
    }

    private static BoundAnonymousTypeMemberStatementNode BindAnonymousTypeMember(BinderContext context, HumanReadableAnonymousTypeMemberStatementNode anonymousMember)
    {
        BoundTypeSymbol memberType = anonymousMember.TypeReference is null
            ? BoundBuiltInTypes.Any
            : BindTypeReference(context, anonymousMember.TypeReference);

        BoundExpressionNode value = BindExpression(context, anonymousMember.Value);

        BoundMemberSymbol symbol = new(anonymousMember.Name, memberType, anonymousMember);

        if (!context.Scope.TryDeclare(symbol, out BoundSymbol? existing))
        {
            context.Diagnostics.AddError(
                anonymousMember.Start,
                anonymousMember.End,
                $"The member '{anonymousMember.Name}' is already declared in this scope.",
                $"Rename this member or remove the earlier '{existing?.Name}' declaration.");
        }

        return new BoundAnonymousTypeMemberStatementNode(symbol, value);
    }

    private static BoundExpressionNode BindExpression(BinderContext context, HumanReadableExpressionNode expression)
    {
        switch (expression)
        {
            case HumanReadableLiteralExpressionNode literalExpression:
                return BindLiteral(literalExpression);

            case HumanReadableIdentifierExpressionNode identifierExpression:
                return BindIdentifier(context, identifierExpression);

            case HumanReadableThisExpressionNode thisExpression:
                return BindThis(context, thisExpression);

            case HumanReadableGroupedExpressionNode groupedExpression:
                return new BoundGroupedExpressionNode(BindExpression(context, groupedExpression.Expression));

            case HumanReadableUnaryExpressionNode unaryExpression:
                return BindUnary(context, unaryExpression);

            case HumanReadableBinaryExpressionNode binaryExpression:
                return BindBinary(context, binaryExpression);

            case HumanReadableAssignmentExpressionNode assignmentExpression:
                return BindAssignment(context, assignmentExpression);

            case HumanReadableCallExpressionNode callExpression:
                return BindCall(context, callExpression);

            case HumanReadableMemberAccessExpressionNode memberAccessExpression:
                return BindMemberAccess(context, memberAccessExpression);

            case HumanReadableBuiltinCallExpressionNode builtinCallExpression:
                return BindBuiltinCall(context, builtinCallExpression);

            case HumanReadableCollectionExpressionNode collectionExpression:
                return BindCollection(context, collectionExpression);

            case HumanReadableDictionaryExpressionNode dictionaryExpression:
                return BindDictionary(context, dictionaryExpression);

            default:
                context.Diagnostics.AddError(
                    expression.Start,
                    expression.End,
                    $"Unsupported expression kind '{expression.NodeKind}'.");
                return new BoundLiteralExpressionNode(null, BoundBuiltInTypes.Any);
        }
    }

    private static BoundLiteralExpressionNode BindLiteral(HumanReadableLiteralExpressionNode literalExpression)
    {
        BoundTypeSymbol type = literalExpression.LiteralKind switch
        {
            HumanReadableLiteralKind.Number => BoundBuiltInTypes.Number,
            HumanReadableLiteralKind.String => BoundBuiltInTypes.String,
            HumanReadableLiteralKind.Boolean => BoundBuiltInTypes.Boolean,
            HumanReadableLiteralKind.Null => BoundBuiltInTypes.Any,
            HumanReadableLiteralKind.Default => BoundBuiltInTypes.Any,
            _ => BoundBuiltInTypes.Any
        };

        return new BoundLiteralExpressionNode(literalExpression.Value, type);
    }

    private static BoundExpressionNode BindIdentifier(BinderContext context, HumanReadableIdentifierExpressionNode identifierExpression)
    {
        if (!context.Scope.TryLookup(identifierExpression.Name, out BoundSymbol? symbol) || symbol is null)
        {
            context.Diagnostics.AddError(
                identifierExpression.Start,
                identifierExpression.End,
                $"The name '{identifierExpression.Name}' is not defined in this scope.");
            return new BoundSymbolExpressionNode(new BoundUnknownSymbol(identifierExpression.Name, identifierExpression), BoundBuiltInTypes.Any);
        }

        return symbol switch
        {
            BoundContainerSymbol containerSymbol =>
                new BoundTypeExpressionNode(containerSymbol),

            BoundTypeSymbol typeSymbol =>
                new BoundTypeExpressionNode(typeSymbol),

            BoundTemplateSymbol templateSymbol =>
                new BoundSymbolExpressionNode(templateSymbol, BoundBuiltInTypes.Any),

            BoundVariableSymbol variableSymbol =>
                new BoundSymbolExpressionNode(variableSymbol, variableSymbol.Type),

            BoundParameterSymbol parameterSymbol =>
                new BoundSymbolExpressionNode(parameterSymbol, parameterSymbol.Type),

            BoundMemberSymbol memberSymbol =>
                new BoundSymbolExpressionNode(memberSymbol, memberSymbol.Type),

            BoundAliasSymbol aliasSymbol =>
                new BoundSymbolExpressionNode(aliasSymbol, BoundBuiltInTypes.Any),

            _ =>
                new BoundSymbolExpressionNode(symbol, BoundBuiltInTypes.Any)
        };
    }

    private static BoundExpressionNode BindThis(BinderContext context, HumanReadableThisExpressionNode thisExpression)
    {
        if (context.CurrentContainer is null)
        {
            context.Diagnostics.AddError(
                thisExpression.Start,
                thisExpression.End,
                "'this' can only be used inside a container.");
            return new BoundLiteralExpressionNode(null, BoundBuiltInTypes.Any);
        }

        return new BoundThisExpressionNode(context.CurrentContainer);
    }

    private static BoundExpressionNode BindUnary(BinderContext context, HumanReadableUnaryExpressionNode unaryExpression)
    {
        BoundExpressionNode operand = BindExpression(context, unaryExpression.Operand);

        BoundUnaryOperatorKind kind = unaryExpression.OperatorText switch
        {
            "+" => BoundUnaryOperatorKind.Identity,
            "-" => BoundUnaryOperatorKind.Negation,
            "!" => BoundUnaryOperatorKind.LogicalNegation,
            _ => BoundUnaryOperatorKind.Identity
        };

        BoundTypeSymbol resultType = kind == BoundUnaryOperatorKind.LogicalNegation
            ? BoundBuiltInTypes.Boolean
            : operand.Type;

        return new BoundUnaryExpressionNode(new BoundUnaryOperator(unaryExpression.OperatorText, kind), operand, resultType);
    }

    private static BoundExpressionNode BindBinary(BinderContext context, HumanReadableBinaryExpressionNode binaryExpression)
    {
        BoundExpressionNode left = BindExpression(context, binaryExpression.Left);
        BoundExpressionNode right = BindExpression(context, binaryExpression.Right);

        BoundBinaryOperatorKind kind = binaryExpression.OperatorText switch
        {
            "+" => BoundBinaryOperatorKind.Addition,
            "-" => BoundBinaryOperatorKind.Subtraction,
            "*" => BoundBinaryOperatorKind.Multiplication,
            "/" => BoundBinaryOperatorKind.Division,
            "%" => BoundBinaryOperatorKind.Modulo,
            "**" => BoundBinaryOperatorKind.Power,
            "==" => BoundBinaryOperatorKind.Equals,
            "!=" => BoundBinaryOperatorKind.NotEquals,
            ">" => BoundBinaryOperatorKind.GreaterThan,
            "<" => BoundBinaryOperatorKind.LessThan,
            ">=" => BoundBinaryOperatorKind.GreaterOrEqual,
            "<=" => BoundBinaryOperatorKind.LessOrEqual,
            "&&" => BoundBinaryOperatorKind.LogicalAnd,
            "||" => BoundBinaryOperatorKind.LogicalOr,
            "^|" => BoundBinaryOperatorKind.LogicalXor,
            _ => BoundBinaryOperatorKind.Addition
        };

        BoundTypeSymbol resultType = kind switch
        {
            BoundBinaryOperatorKind.Equals or
            BoundBinaryOperatorKind.NotEquals or
            BoundBinaryOperatorKind.GreaterThan or
            BoundBinaryOperatorKind.LessThan or
            BoundBinaryOperatorKind.GreaterOrEqual or
            BoundBinaryOperatorKind.LessOrEqual or
            BoundBinaryOperatorKind.LogicalAnd or
            BoundBinaryOperatorKind.LogicalOr or
            BoundBinaryOperatorKind.LogicalXor => BoundBuiltInTypes.Boolean,

            _ => left.Type
        };

        return new BoundBinaryExpressionNode(
            left,
            new BoundBinaryOperator(binaryExpression.OperatorText, kind),
            right,
            resultType);
    }

    private static BoundExpressionNode BindAssignment(BinderContext context, HumanReadableAssignmentExpressionNode assignmentExpression)
    {
        BoundExpressionNode target = BindExpression(context, assignmentExpression.Target);
        BoundExpressionNode value = BindExpression(context, assignmentExpression.Value);

        if (target is not BoundSymbolExpressionNode and not BoundMemberAccessExpressionNode and not BoundThisExpressionNode)
        {
            context.Diagnostics.AddError(
                assignmentExpression.Start,
                assignmentExpression.End,
                "The left-hand side of an assignment is not assignable.");
        }

        return new BoundAssignmentExpressionNode(target, value);
    }

    private static BoundExpressionNode BindCall(BinderContext context, HumanReadableCallExpressionNode callExpression)
    {
        BoundExpressionNode target = BindExpression(context, callExpression.Target);
        List<BoundExpressionNode> arguments = [];

        foreach (HumanReadableExpressionNode argument in callExpression.Arguments)
            arguments.Add(BindExpression(context, argument));

        if (target is BoundTypeExpressionNode typeExpression)
        {
            return new BoundCallExpressionNode(
                target,
                arguments,
                typeExpression.TypeSymbol,
                Template: null,
                ConstructedType: typeExpression.TypeSymbol);
        }

        if (target is BoundSymbolExpressionNode symbolExpression && symbolExpression.Symbol is BoundTemplateSymbol templateSymbol)
        {
            return new BoundCallExpressionNode(
                target,
                arguments,
                templateSymbol.ReturnType,
                templateSymbol,
                null);
        }

        if (target is BoundMemberAccessExpressionNode memberAccess && memberAccess.Member is BoundTemplateSymbol memberTemplate)
        {
            return new BoundCallExpressionNode(
                target,
                arguments,
                memberTemplate.ReturnType,
                memberTemplate,
                null);
        }

        context.Diagnostics.AddError(
            callExpression.Start,
            callExpression.End,
            "The expression being called is not callable.");
        return new BoundCallExpressionNode(target, arguments, BoundBuiltInTypes.Any);
    }

    private static BoundExpressionNode BindMemberAccess(BinderContext context, HumanReadableMemberAccessExpressionNode memberAccessExpression)
    {
        BoundExpressionNode target = BindExpression(context, memberAccessExpression.Target);
        BoundTypeSymbol? targetType = target.Type;

        if (targetType is BoundContainerSymbol containerType)
        {
            if (containerType.Members.TryGetValue(memberAccessExpression.MemberName, out BoundMemberSymbol? member))
                return new BoundMemberAccessExpressionNode(target, member, member.Type);

            if (containerType.Templates.TryGetValue(memberAccessExpression.MemberName, out BoundTemplateSymbol? template))
                return new BoundMemberAccessExpressionNode(target, template, template.ReturnType);

            if (containerType.NestedContainers.TryGetValue(memberAccessExpression.MemberName, out BoundContainerSymbol? nestedContainer))
                return new BoundMemberAccessExpressionNode(target, nestedContainer, nestedContainer);
        }

        if (target is BoundTypeExpressionNode typeExpression && typeExpression.TypeSymbol is BoundContainerSymbol staticContainer)
        {
            if (staticContainer.Members.TryGetValue(memberAccessExpression.MemberName, out BoundMemberSymbol? staticMember))
                return new BoundMemberAccessExpressionNode(target, staticMember, staticMember.Type);

            if (staticContainer.Templates.TryGetValue(memberAccessExpression.MemberName, out BoundTemplateSymbol? staticTemplate))
                return new BoundMemberAccessExpressionNode(target, staticTemplate, staticTemplate.ReturnType);

            if (staticContainer.NestedContainers.TryGetValue(memberAccessExpression.MemberName, out BoundContainerSymbol? staticNested))
                return new BoundMemberAccessExpressionNode(target, staticNested, staticNested);
        }

        context.Diagnostics.AddError(
            memberAccessExpression.Start,
            memberAccessExpression.End,
            $"The member '{memberAccessExpression.MemberName}' could not be resolved.");
        return new BoundMemberAccessExpressionNode(target, new BoundUnknownSymbol(memberAccessExpression.MemberName, memberAccessExpression), BoundBuiltInTypes.Any);
    }

    private static BoundExpressionNode BindBuiltinCall(BinderContext context, HumanReadableBuiltinCallExpressionNode builtinCallExpression)
    {
        List<BoundExpressionNode> arguments = [];
        foreach (HumanReadableExpressionNode argument in builtinCallExpression.Arguments)
            arguments.Add(BindExpression(context, argument));

        BoundTypeSymbol resultType = builtinCallExpression.Name switch
        {
            "type" => BoundBuiltInTypes.Type,
            "ref" => BoundBuiltInTypes.Any,
            "stack" => BoundBuiltInTypes.Any,
            _ => BoundBuiltInTypes.Any
        };

        return new BoundBuiltinCallExpressionNode(builtinCallExpression.Name, arguments, resultType);
    }

    private static BoundExpressionNode BindCollection(BinderContext context, HumanReadableCollectionExpressionNode collectionExpression)
    {
        BoundTypeSymbol itemType = BindTypeReference(context, collectionExpression.ItemType);
        List<BoundExpressionNode> items = [];

        foreach (HumanReadableExpressionNode item in collectionExpression.Items)
            items.Add(BindExpression(context, item));

        BoundCollectionTypeSymbol collectionType = new(itemType, collectionExpression);
        return new BoundCollectionExpressionNode(itemType, items, collectionType);
    }

    private static BoundExpressionNode BindDictionary(BinderContext context, HumanReadableDictionaryExpressionNode dictionaryExpression)
    {
        BoundTypeSymbol keyType = BindTypeReference(context, dictionaryExpression.KeyType);
        BoundTypeSymbol valueType = BindTypeReference(context, dictionaryExpression.ValueType);

        List<BoundDictionaryEntryNode> entries = [];
        foreach (HumanReadableDictionaryEntryNode entry in dictionaryExpression.Entries)
        {
            BoundExpressionNode key = BindExpression(context, entry.Key);
            BoundExpressionNode value = BindExpression(context, entry.Value);
            entries.Add(new BoundDictionaryEntryNode(key, value));
        }

        BoundDictionaryTypeSymbol dictionaryType = new(keyType, valueType, dictionaryExpression);
        return new BoundDictionaryExpressionNode(keyType, valueType, entries, dictionaryType);
    }

    private static BoundTypeSymbol BindTypeReference(BinderContext context, HumanReadableTypeReferenceNode typeReference)
    {
        string fullName = string.Join(".", typeReference.Segments);

        if (!context.Scope.TryLookup(fullName, out BoundSymbol? symbol) || symbol is null)
        {
            if (typeReference.Segments.Count > 0 && context.Scope.TryLookup(typeReference.Segments[0], out symbol) && symbol is not null)
            {
                return WrapTypeReference(context, typeReference, symbol);
            }

            context.Diagnostics.AddError(
                typeReference.Start,
                typeReference.End,
                $"The type '{fullName}' could not be resolved.");
            return BoundBuiltInTypes.Any;
        }

        return WrapTypeReference(context, typeReference, symbol);
    }

    private static BoundTypeSymbol WrapTypeReference(BinderContext context, HumanReadableTypeReferenceNode typeReference, BoundSymbol symbol)
    {
        BoundTypeSymbol baseType = symbol switch
        {
            BoundContainerSymbol containerSymbol => containerSymbol,
            BoundTypeSymbol typeSymbol => typeSymbol,
            _ => BoundBuiltInTypes.Any
        };

        if (baseType == BoundBuiltInTypes.Any && symbol is not BoundContainerSymbol and not BoundTypeSymbol)
        {
            context.Diagnostics.AddError(
                typeReference.Start,
                typeReference.End,
                $"The name '{string.Join(".", typeReference.Segments)}' is not a valid type.");
        }

        if (typeReference.GenericArguments.Count == 0)
            return baseType;

        List<BoundTypeSymbol> genericArguments = [];
        foreach (HumanReadableTypeReferenceNode genericArgument in typeReference.GenericArguments)
            genericArguments.Add(BindTypeReference(context, genericArgument));

        return new BoundGenericTypeSymbol(
            $"{baseType.Name}<{string.Join(", ", genericArguments.Select(x => x.Name))}>",
            baseType,
            genericArguments,
            typeReference);
    }

    private sealed class BinderContext
    {
        public BinderContext(BoundScope scope, HumanReadableDiagnosticBag diagnostics)
        {
            Scope = scope;
            Diagnostics = diagnostics;
        }

        public BoundScope Scope { get; }

        public HumanReadableDiagnosticBag Diagnostics { get; }

        public BoundContainerSymbol? CurrentContainer { get; set; }

        public BinderContext CreateChild()
        {
            BinderContext child = new(Scope.CreateChild(), Diagnostics)
            {
                CurrentContainer = CurrentContainer
            };

            return child;
        }

        public BinderContext CreateChild(BoundContainerSymbol container)
        {
            BinderContext child = new(Scope.CreateChild(), Diagnostics)
            {
                CurrentContainer = container
            };

            child.Scope.TryDeclare(container, out _);
            return child;
        }

        public BinderContext CreateChild(BoundTemplateSymbol template)
        {
            BinderContext child = new(Scope.CreateChild(), Diagnostics)
            {
                CurrentContainer = CurrentContainer
            };

            child.Scope.TryDeclare(template, out _);
            return child;
        }
    }
}

internal sealed class HumanReadableBindingResult
{
    public HumanReadableBindingResult(
        BoundProgramNode program,
        BoundScope globalScope,
        HumanReadableDiagnosticBag diagnostics)
    {
        Program = program;
        GlobalScope = globalScope;
        Diagnostics = diagnostics;
    }

    public BoundProgramNode Program { get; }

    public BoundScope GlobalScope { get; }

    public HumanReadableDiagnosticBag Diagnostics { get; }
}