namespace WinterRose.WinterForgeSerializing.Formatting.HumanReadable.Parsing;

internal enum HumanReadableNodeKind
{
    Program,

    BlockStatement,
    ExpressionStatement,
    ReturnStatement,
    AliasStatement,
    ImportStatement,
    ObjectDeclarationStatement,
    AnonymousTypeDeclarationStatement,
    IfStatement,
    WhileStatement,
    ForStatement,
    AnonymousTypeMemberStatement,

    IdentifierExpression,
    LiteralExpression,
    ThisExpression,
    UnaryExpression,
    BinaryExpression,
    AssignmentExpression,
    CallExpression,
    MemberAccessExpression,
    BuiltinCallExpression,
    CollectionExpression,
    DictionaryExpression,
    DictionaryEntry,
    GroupedExpression,
    ObjectDeclarationExpression,
    TypeReference,

    ContainerDeclaration,
    TemplateDeclaration,
    Parameter,
    VariableDeclaration,
    FieldDeclaration,
    PropertyDeclaration,
    Modifier
}

internal enum HumanReadableLiteralKind
{
    Number,
    String,
    Boolean,
    Null,
    Default
}

internal enum HumanReadableAccessKind
{
    Dot,
    Arrow
}

internal sealed record HumanReadableModifierNode(
    HumanReadableModifierKind ModifierKind,
    int Start,
    int End,
    string Text)
    : HumanReadableAstNode(
        HumanReadableNodeKind.Modifier,
        Start,
        End,
        Text);

internal abstract record HumanReadableDeclarationNode(
    IReadOnlyList<HumanReadableModifierNode> Modifiers,
    HumanReadableNodeKind NodeKind,
    int Start,
    int End,
    string Text)
    : HumanReadableStatementNode(NodeKind, Start, End, Text);

internal sealed record HumanReadableContainerDeclarationNode(
    string Name,
    IReadOnlyList<HumanReadableParameterNode> GenericParameters,
    IReadOnlyList<HumanReadableStatementNode> Members,
    int Start,
    int End,
    string Text)
    : HumanReadableDeclarationNode(
        Array.Empty<HumanReadableModifierNode>(),
        HumanReadableNodeKind.ContainerDeclaration,
        Start,
        End,
        Text);

internal sealed record HumanReadableTemplateDeclarationNode(
    string Name,
    IReadOnlyList<HumanReadableParameterNode> Parameters,
    HumanReadableTypeReferenceNode? ReturnType,
    HumanReadableBlockStatementNode Body,
    int Start,
    int End,
    string Text)
    : HumanReadableDeclarationNode(
        Array.Empty<HumanReadableModifierNode>(),
        HumanReadableNodeKind.TemplateDeclaration,
        Start,
        End,
        Text);

internal abstract record HumanReadableAstNode(
    HumanReadableNodeKind NodeKind,
    int Start,
    int End,
    string Text);

internal abstract record HumanReadableStatementNode(
    HumanReadableNodeKind NodeKind,
    int Start,
    int End,
    string Text)
    : HumanReadableAstNode(NodeKind, Start, End, Text)
{
    public virtual bool IsBlock => false;

    public virtual IReadOnlyList<HumanReadableStatementNode> Children => Array.Empty<HumanReadableStatementNode>();
}


internal abstract record HumanReadableExpressionNode(
    HumanReadableNodeKind NodeKind,
    int Start,
    int End,
    string Text)
    : HumanReadableAstNode(NodeKind, Start, End, Text);

internal sealed record HumanReadableProgramNode(
    IReadOnlyList<HumanReadableStatementNode> Statements,
    int Start,
    int End,
    string Text)
    : HumanReadableAstNode(HumanReadableNodeKind.Program, Start, End, Text);

internal sealed record HumanReadableBlockStatementNode(
    IReadOnlyList<HumanReadableStatementNode> Statements,
    int Start,
    int End,
    string Text)
    : HumanReadableStatementNode(HumanReadableNodeKind.BlockStatement, Start, End, Text)
{
    public override bool IsBlock => true;

    public override IReadOnlyList<HumanReadableStatementNode> Children => Statements;
}

internal sealed record HumanReadableExpressionStatementNode(
    HumanReadableExpressionNode Expression,
    int Start,
    int End,
    string Text)
    : HumanReadableStatementNode(HumanReadableNodeKind.ExpressionStatement, Start, End, Text);

internal sealed record HumanReadableReturnStatementNode(
    HumanReadableExpressionNode? Expression,
    int Start,
    int End,
    string Text)
    : HumanReadableStatementNode(HumanReadableNodeKind.ReturnStatement, Start, End, Text);

internal sealed record HumanReadableAliasStatementNode(
    HumanReadableExpressionNode Target,
    string Alias,
    int Start,
    int End,
    string Text)
    : HumanReadableStatementNode(HumanReadableNodeKind.AliasStatement, Start, End, Text);

internal sealed record HumanReadableImportStatementNode(
    HumanReadableExpressionNode Source,
    int Start,
    int End,
    string Text)
    : HumanReadableStatementNode(HumanReadableNodeKind.ImportStatement, Start, End, Text);

internal sealed record HumanReadableObjectDeclarationStatementNode(
    HumanReadableExpressionNode Head,
    HumanReadableExpressionNode Reference,
    HumanReadableBlockStatementNode? Body,
    int Start,
    int End,
    string Text)
    : HumanReadableStatementNode(HumanReadableNodeKind.ObjectDeclarationStatement, Start, End, Text)
{
    public override bool IsBlock => Body is not null;

    public override IReadOnlyList<HumanReadableStatementNode> Children => Body?.Statements ?? Array.Empty<HumanReadableStatementNode>();
}

internal sealed record HumanReadableAnonymousTypeDeclarationStatementNode(
    string? Name,
    HumanReadableTypeReferenceNode? BaseType,
    HumanReadableExpressionNode Reference,
    HumanReadableBlockStatementNode Body,
    int Start,
    int End,
    string Text)
    : HumanReadableStatementNode(HumanReadableNodeKind.AnonymousTypeDeclarationStatement, Start, End, Text)
{
    public override bool IsBlock => true;

    public override IReadOnlyList<HumanReadableStatementNode> Children => Body.Statements;
}

internal sealed record HumanReadableIfStatementNode(
    HumanReadableExpressionNode Condition,
    HumanReadableBlockStatementNode ThenBlock,
    HumanReadableStatementNode? ElseBranch,
    int Start,
    int End,
    string Text)
    : HumanReadableStatementNode(HumanReadableNodeKind.IfStatement, Start, End, Text)
{
    public override bool IsBlock => true;

    public override IReadOnlyList<HumanReadableStatementNode> Children => ThenBlock.Statements;
}

internal sealed record HumanReadableWhileStatementNode(
    HumanReadableExpressionNode Condition,
    HumanReadableBlockStatementNode Body,
    int Start,
    int End,
    string Text)
    : HumanReadableStatementNode(HumanReadableNodeKind.WhileStatement, Start, End, Text)
{
    public override bool IsBlock => true;

    public override IReadOnlyList<HumanReadableStatementNode> Children => Body.Statements;
}

internal sealed record HumanReadableForStatementNode(
    HumanReadableExpressionNode? Initializer,
    HumanReadableExpressionNode? Condition,
    HumanReadableExpressionNode? Iterator,
    HumanReadableBlockStatementNode Body,
    int Start,
    int End,
    string Text)
    : HumanReadableStatementNode(HumanReadableNodeKind.ForStatement, Start, End, Text)
{
    public override bool IsBlock => true;

    public override IReadOnlyList<HumanReadableStatementNode> Children => Body.Statements;
}

internal sealed record HumanReadableAnonymousTypeMemberStatementNode(
    HumanReadableTypeReferenceNode? TypeReference,
    string Name,
    HumanReadableExpressionNode Value,
    int Start,
    int End,
    string Text)
    : HumanReadableStatementNode(HumanReadableNodeKind.AnonymousTypeMemberStatement, Start, End, Text);

internal sealed record HumanReadableParameterNode(
    HumanReadableTypeReferenceNode? TypeReference,
    string Name,
    int Start,
    int End,
    string Text)
    : HumanReadableAstNode(HumanReadableNodeKind.Parameter, Start, End, Text);

internal sealed record HumanReadableIdentifierExpressionNode(
    string Name,
    int Start,
    int End,
    string Text)
    : HumanReadableExpressionNode(HumanReadableNodeKind.IdentifierExpression, Start, End, Text);

internal sealed record HumanReadableThisExpressionNode(
    int Start,
    int End,
    string Text)
    : HumanReadableExpressionNode(HumanReadableNodeKind.ThisExpression, Start, End, Text);

internal sealed record HumanReadableLiteralExpressionNode(
    HumanReadableLiteralKind LiteralKind,
    string RawText,
    object? Value,
    int Start,
    int End,
    string Text)
    : HumanReadableExpressionNode(HumanReadableNodeKind.LiteralExpression, Start, End, Text);

internal sealed record HumanReadableGroupedExpressionNode(
    HumanReadableExpressionNode Expression,
    int Start,
    int End,
    string Text)
    : HumanReadableExpressionNode(HumanReadableNodeKind.GroupedExpression, Start, End, Text);

internal sealed record HumanReadableObjectDeclarationExpressionNode(
    HumanReadableExpressionNode Head,
    HumanReadableExpressionNode Reference,
    HumanReadableBlockStatementNode? Body,
    int Start,
    int End,
    string Text)
    : HumanReadableExpressionNode(HumanReadableNodeKind.ObjectDeclarationExpression, Start, End, Text);

internal sealed record HumanReadableUnaryExpressionNode(
    string OperatorText,
    HumanReadableExpressionNode Operand,
    int Start,
    int End,
    string Text)
    : HumanReadableExpressionNode(HumanReadableNodeKind.UnaryExpression, Start, End, Text);

internal sealed record HumanReadableBinaryExpressionNode(
    HumanReadableExpressionNode Left,
    string OperatorText,
    HumanReadableExpressionNode Right,
    int Start,
    int End,
    string Text)
    : HumanReadableExpressionNode(HumanReadableNodeKind.BinaryExpression, Start, End, Text);

internal sealed record HumanReadableAssignmentExpressionNode(
    HumanReadableExpressionNode Target,
    HumanReadableExpressionNode Value,
    int Start,
    int End,
    string Text)
    : HumanReadableExpressionNode(HumanReadableNodeKind.AssignmentExpression, Start, End, Text);

internal sealed record HumanReadableCallExpressionNode(
    HumanReadableExpressionNode Target,
    IReadOnlyList<HumanReadableExpressionNode> Arguments,
    int Start,
    int End,
    string Text)
    : HumanReadableExpressionNode(HumanReadableNodeKind.CallExpression, Start, End, Text);

internal sealed record HumanReadableMemberAccessExpressionNode(
    HumanReadableExpressionNode Target,
    string MemberName,
    HumanReadableAccessKind AccessKind,
    int Start,
    int End,
    string Text)
    : HumanReadableExpressionNode(HumanReadableNodeKind.MemberAccessExpression, Start, End, Text);

internal sealed record HumanReadableBuiltinCallExpressionNode(
    string Name,
    IReadOnlyList<HumanReadableExpressionNode> Arguments,
    int Start,
    int End,
    string Text)
    : HumanReadableExpressionNode(HumanReadableNodeKind.BuiltinCallExpression, Start, End, Text);

internal sealed record HumanReadableCollectionExpressionNode(
    HumanReadableTypeReferenceNode ItemType,
    IReadOnlyList<HumanReadableExpressionNode> Items,
    int Start,
    int End,
    string Text)
    : HumanReadableExpressionNode(HumanReadableNodeKind.CollectionExpression, Start, End, Text);

internal sealed record HumanReadableDictionaryEntryNode(
    HumanReadableExpressionNode Key,
    HumanReadableExpressionNode Value,
    int Start,
    int End,
    string Text)
    : HumanReadableAstNode(HumanReadableNodeKind.DictionaryEntry, Start, End, Text);

internal sealed record HumanReadableDictionaryExpressionNode(
    HumanReadableTypeReferenceNode KeyType,
    HumanReadableTypeReferenceNode ValueType,
    IReadOnlyList<HumanReadableDictionaryEntryNode> Entries,
    int Start,
    int End,
    string Text)
    : HumanReadableExpressionNode(HumanReadableNodeKind.DictionaryExpression, Start, End, Text);

internal sealed record HumanReadableTypeReferenceNode(
    IReadOnlyList<string> Segments,
    IReadOnlyList<HumanReadableTypeReferenceNode> GenericArguments,
    int Start,
    int End,
    string Text)
    : HumanReadableAstNode(HumanReadableNodeKind.TypeReference, Start, End, Text);


