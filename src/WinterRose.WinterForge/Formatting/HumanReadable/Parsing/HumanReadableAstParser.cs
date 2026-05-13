using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using WinterRose.WinterForgeSerializing.Formatting;
using WinterRose.WinterForgeSerializing.Formatting.HumanReadable.Parsing;

internal static class HumanReadableAstParser
{
    public static HumanReadableProgramNode Parse(string source, IReadOnlyList<HumanReadableToken> tokens)
    {
        TokenReader reader = new(source, tokens);
        List<HumanReadableStatementNode> statements = ParseStatements(reader, StatementContext.General, stopOnCloseBrace: false);

        int start = statements.Count == 0 ? 0 : statements[0].Start;
        int end = statements.Count == 0 ? 0 : statements[^1].End;
        string text = statements.Count == 0 ? string.Empty : SafeSlice(source, start, end);

        return new HumanReadableProgramNode(statements, start, end, text);
    }

    private static List<HumanReadableStatementNode> ParseStatements(TokenReader reader, StatementContext context, bool stopOnCloseBrace)
    {
        List<HumanReadableStatementNode> statements = [];

        while (!reader.IsEnd)
        {
            reader.SkipTrivia();

            if (reader.IsEnd)
                break;

            if (reader.Current.Kind == HumanReadableTokenKind.CloseBrace)
            {
                if (!stopOnCloseBrace)
                    throw reader.Error("Unexpected closing brace.", reader.Current);

                reader.Advance();
                break;
            }

            if (reader.Current.Kind == HumanReadableTokenKind.Semicolon)
            {
                reader.Advance();
                continue;
            }

            statements.Add(ParseStatement(reader, context));
        }

        if (stopOnCloseBrace && reader.IsEnd)
            throw reader.Error(
                "Missing closing brace for block.",
                reader.PreviousOrCurrent(),
                "Did you mean to place '}' before end of file?");

        return statements;
    }

    private static HumanReadableStatementNode ParseStatement(TokenReader reader, StatementContext context)
    {
        reader.SkipTrivia();
        HumanReadableToken startToken = reader.Current;

        if (context == StatementContext.AnonymousType && TryParseAnonymousTypeMember(reader, out HumanReadableAnonymousTypeMemberStatementNode? member))
            return member;

        if (reader.Current.Kind == HumanReadableTokenKind.KeywordReturn)
            return ParseReturnStatement(reader);

        if (reader.Current.Kind == HumanReadableTokenKind.KeywordAlias)
            return ParseAliasStatement(reader);

        if (reader.Current.Kind == HumanReadableTokenKind.KeywordImport)
            return ParseImportStatement(reader);

        if (reader.Current.Kind == HumanReadableTokenKind.KeywordIf)
            return ParseIfStatement(reader, context);

        if (reader.Current.Kind == HumanReadableTokenKind.KeywordWhile)
            return ParseWhileStatement(reader, context);

        if (reader.Current.Kind == HumanReadableTokenKind.KeywordFor)
            return ParseForStatement(reader, context);

        if (reader.Current.Kind == HumanReadableTokenKind.KeywordAnonymous)
            return ParseAnonymousTypeDeclarationStatement(reader);

        if (reader.Current.Kind == HumanReadableTokenKind.Directive)
        {
            return reader.Current.Text switch
            {
                "#container" => ParseContainerDeclaration(reader),
                "#template" => ParseTemplateDeclaration(reader),
                _ => throw reader.Error(
                    $"Unknown directive '{reader.Current.Text}'.",
                    reader.Current)
            };
        }

        HumanReadableExpressionNode expression = ParseExpression(reader, 0);

        reader.SkipTrivia();
        if (reader.Current.Kind == HumanReadableTokenKind.Colon)
            return ParseObjectDeclarationStatement(reader, expression);

        int end = expression.End;
        string text = SafeSlice(reader.Source, startToken.Start, end);
        return new HumanReadableExpressionStatementNode(expression, startToken.Start, end, text);
    }

    private static HumanReadableContainerDeclarationNode ParseContainerDeclaration(TokenReader reader)
    {
        HumanReadableToken startToken = reader.Current;

        reader.Advance();
        reader.SkipTrivia();

        HumanReadableToken nameToken =
            ExpectNameToken(reader, "Expected container name.");

        string name = nameToken.Text;

        List<HumanReadableParameterNode> genericParameters = [];

        reader.SkipTrivia();

        if (reader.Current.Kind == HumanReadableTokenKind.LessThan)
        {
            reader.Advance();
            reader.SkipTrivia();

            while (reader.Current.Kind != HumanReadableTokenKind.GreaterThan)
            {
                HumanReadableToken parameterToken =
                    ExpectNameToken(reader, "Expected generic parameter.");

                genericParameters.Add(
                    new HumanReadableParameterNode(
                        null,
                        parameterToken.Text,
                        parameterToken.Start,
                        parameterToken.End(),
                        parameterToken.Text));

                reader.SkipTrivia();

                if (reader.Current.Kind == HumanReadableTokenKind.Comma)
                {
                    reader.Advance();
                    reader.SkipTrivia();
                    continue;
                }

                break;
            }

            Expect(
                reader,
                HumanReadableTokenKind.GreaterThan,
                "Expected '>' after generic parameters.");
        }

        HumanReadableBlockStatementNode body =
            ParseBlock(reader, StatementContext.General);

        int end = body.End;

        return new HumanReadableContainerDeclarationNode(
            name,
            genericParameters,
            body.Statements,
            startToken.Start,
            end,
            SafeSlice(reader.Source, startToken.Start, end));
    }

    private static HumanReadableTemplateDeclarationNode ParseTemplateDeclaration(
    TokenReader reader)
    {
        HumanReadableToken startToken = reader.Current;

        reader.Advance();
        reader.SkipTrivia();

        HumanReadableToken nameToken =
            ExpectNameToken(reader, "Expected template name.");

        string name = nameToken.Text;

        Expect(
            reader,
            HumanReadableTokenKind.OpenParen,
            "Expected '(' after template name.");

        List<HumanReadableParameterNode> parameters = [];

        reader.SkipTrivia();

        while (reader.Current.Kind != HumanReadableTokenKind.CloseParen)
        {
            HumanReadableTypeReferenceNode? type = null;

            if (CanStartTypeReference(reader.Current.Kind))
            {
                type = ParseTypeReference(reader);
                reader.SkipTrivia();
            }

            HumanReadableToken parameterToken =
                ExpectNameToken(reader, "Expected parameter name.");

            parameters.Add(
                new HumanReadableParameterNode(
                    type,
                    parameterToken.Text,
                    parameterToken.Start,
                    parameterToken.End(),
                    parameterToken.Text));

            reader.SkipTrivia();

            if (reader.Current.Kind == HumanReadableTokenKind.Comma)
            {
                reader.Advance();
                reader.SkipTrivia();
                continue;
            }

            break;
        }

        Expect(
            reader,
            HumanReadableTokenKind.CloseParen,
            "Expected ')' after parameters.");

        reader.SkipTrivia();

        HumanReadableTypeReferenceNode? returnType = null;

        if (reader.Current.Kind == HumanReadableTokenKind.Arrow)
        {
            reader.Advance();
            reader.SkipTrivia();

            returnType = ParseTypeReference(reader);
        }

        HumanReadableBlockStatementNode body =
            ParseBlock(reader, StatementContext.General);

        int end = body.End;

        return new HumanReadableTemplateDeclarationNode(
            name,
            parameters,
            returnType,
            body,
            startToken.Start,
            end,
            SafeSlice(reader.Source, startToken.Start, end));
    }

    private static HumanReadableStatementNode ParseReturnStatement(TokenReader reader)
    {
        HumanReadableToken startToken = reader.Current;
        reader.Advance();
        reader.SkipTrivia();

        HumanReadableExpressionNode? expression = null;

        if (reader.Current.Kind is not HumanReadableTokenKind.Semicolon
            and not HumanReadableTokenKind.CloseBrace
            and not HumanReadableTokenKind.EndOfFile)
        {
            expression = ParseExpression(reader, 0);
        }

        int end = expression?.End ?? reader.PreviousOrCurrent().End();
        string text = SafeSlice(reader.Source, startToken.Start, end);
        return new HumanReadableReturnStatementNode(expression, startToken.Start, end, text);
    }

    private static HumanReadableStatementNode ParseAliasStatement(TokenReader reader)
    {
        HumanReadableToken startToken = reader.Current;
        reader.Advance();
        reader.SkipTrivia();

        HumanReadableExpressionNode target = ParseExpression(reader, 0);

        reader.SkipTrivia();
        ExpectKeyword(reader, HumanReadableTokenKind.KeywordAs, "Expected 'as' after alias target.");

        reader.SkipTrivia();
        HumanReadableToken aliasToken = ExpectNameToken(reader, "Expected alias name.");
        string alias = aliasToken.Text;

        int end = aliasToken.End();
        string text = SafeSlice(reader.Source, startToken.Start, end);
        return new HumanReadableAliasStatementNode(target, alias, startToken.Start, end, text);
    }

    private static HumanReadableStatementNode ParseImportStatement(TokenReader reader)
    {
        HumanReadableToken startToken = reader.Current;
        reader.Advance();
        reader.SkipTrivia();

        HumanReadableExpressionNode source = ParseExpression(reader, 0);
        int end = source.End;
        string text = SafeSlice(reader.Source, startToken.Start, end);

        return new HumanReadableImportStatementNode(source, startToken.Start, end, text);
    }

    private static HumanReadableStatementNode ParseIfStatement(TokenReader reader, StatementContext context)
    {
        HumanReadableToken startToken = reader.Current;
        reader.Advance();
        reader.SkipTrivia();

        HumanReadableExpressionNode condition = ParseExpression(reader, 0);
        HumanReadableBlockStatementNode thenBlock = ParseBlock(reader, context);

        reader.SkipTrivia();
        HumanReadableStatementNode? elseBranch = null;

        if (reader.Current.Kind == HumanReadableTokenKind.KeywordElse)
        {
            reader.Advance();
            reader.SkipTrivia();

            if (reader.Current.Kind == HumanReadableTokenKind.KeywordIf)
                elseBranch = ParseIfStatement(reader, context);
            else
                elseBranch = ParseBlock(reader, context);
        }

        int end = elseBranch?.End ?? thenBlock.End;
        string text = SafeSlice(reader.Source, startToken.Start, end);
        return new HumanReadableIfStatementNode(condition, thenBlock, elseBranch, startToken.Start, end, text);
    }

    private static HumanReadableStatementNode ParseWhileStatement(TokenReader reader, StatementContext context)
    {
        HumanReadableToken startToken = reader.Current;
        reader.Advance();
        reader.SkipTrivia();

        HumanReadableExpressionNode condition = ParseExpression(reader, 0);
        HumanReadableBlockStatementNode body = ParseBlock(reader, context);

        int end = body.End;
        string text = SafeSlice(reader.Source, startToken.Start, end);
        return new HumanReadableWhileStatementNode(condition, body, startToken.Start, end, text);
    }

    private static HumanReadableStatementNode ParseForStatement(TokenReader reader, StatementContext context)
    {
        HumanReadableToken startToken = reader.Current;
        reader.Advance();
        reader.SkipTrivia();

        HumanReadableExpressionNode? initializer = null;
        HumanReadableExpressionNode? condition = null;
        HumanReadableExpressionNode? iterator = null;

        if (reader.Current.Kind != HumanReadableTokenKind.Semicolon)
            initializer = ParseExpressionUntil(reader, HumanReadableTokenKind.Semicolon);

        Expect(reader, HumanReadableTokenKind.Semicolon, "Expected ';' after for-loop initializer.");
        reader.SkipTrivia();

        if (reader.Current.Kind != HumanReadableTokenKind.Semicolon)
            condition = ParseExpressionUntil(reader, HumanReadableTokenKind.Semicolon);

        Expect(reader, HumanReadableTokenKind.Semicolon, "Expected ';' after for-loop condition.");
        reader.SkipTrivia();

        if (reader.Current.Kind != HumanReadableTokenKind.OpenBrace)
            iterator = ParseExpressionUntil(reader, HumanReadableTokenKind.OpenBrace);

        HumanReadableBlockStatementNode body = ParseBlock(reader, context);

        int end = body.End;
        string text = SafeSlice(reader.Source, startToken.Start, end);
        return new HumanReadableForStatementNode(initializer, condition, iterator, body, startToken.Start, end, text);
    }

    private static HumanReadableStatementNode ParseAnonymousTypeDeclarationStatement(TokenReader reader)
    {
        HumanReadableToken startToken = reader.Current;
        reader.Advance();
        reader.SkipTrivia();

        string? name = null;
        HumanReadableTypeReferenceNode? baseType = null;

        if (reader.Current.Kind == HumanReadableTokenKind.KeywordAs)
        {
            reader.Advance();
            reader.SkipTrivia();
            name = ExpectNameToken(reader, "Expected anonymous type name after 'as'.").Text;
            reader.SkipTrivia();
        }

        if (reader.Current.Kind == HumanReadableTokenKind.KeywordInherits)
        {
            reader.Advance();
            reader.SkipTrivia();
            baseType = ParseTypeReference(reader);
            reader.SkipTrivia();
        }

        Expect(reader, HumanReadableTokenKind.Colon, "Expected ':' after anonymous type header.");
        reader.SkipTrivia();

        HumanReadableExpressionNode reference = ParseExpression(reader, 0);
        HumanReadableBlockStatementNode body = ParseBlock(reader, StatementContext.AnonymousType);

        int end = body.End;
        string text = SafeSlice(reader.Source, startToken.Start, end);
        return new HumanReadableAnonymousTypeDeclarationStatementNode(name, baseType, reference, body, startToken.Start, end, text);
    }

    private static HumanReadableStatementNode ParseObjectDeclarationStatement(TokenReader reader, HumanReadableExpressionNode head)
    {
        HumanReadableToken startToken = reader.PreviousOrCurrent();
        if (head.Start < startToken.Start)
            startToken = reader.Current;

        Expect(reader, HumanReadableTokenKind.Colon, "Expected ':' after object declaration head.");
        reader.SkipTrivia();

        HumanReadableExpressionNode reference = ParseExpression(reader, 0);
        reader.SkipTrivia();

        HumanReadableBlockStatementNode? body = null;
        if (reader.Current.Kind == HumanReadableTokenKind.OpenBrace)
            body = ParseBlock(reader, StatementContext.General);

        int end = body?.End ?? reference.End;
        string text = SafeSlice(reader.Source, head.Start, end);
        return new HumanReadableObjectDeclarationStatementNode(head, reference, body, head.Start, end, text);
    }

    private static HumanReadableBlockStatementNode ParseBlock(TokenReader reader, StatementContext context)
    {
        reader.SkipTrivia();
        HumanReadableToken open = Expect(reader, HumanReadableTokenKind.OpenBrace, "Expected '{' to start block.");
        reader.SkipTrivia();

        List<HumanReadableStatementNode> statements = ParseStatements(reader, context, stopOnCloseBrace: true);
        int end = reader.PreviousOrCurrent().End();
        string text = SafeSlice(reader.Source, open.Start, end);

        return new HumanReadableBlockStatementNode(statements, open.Start, end, text);
    }

    private static bool TryParseAnonymousTypeMember(TokenReader reader, out HumanReadableAnonymousTypeMemberStatementNode? member)
    {
        member = null;

        int startIndex = reader.Index;
        HumanReadableToken startToken = reader.Current;

        try
        {
            HumanReadableTypeReferenceNode? typeReference = null;

            if (CanStartTypeReference(reader.Current.Kind))
            {
                typeReference = ParseTypeReference(reader);
                reader.SkipTrivia();
            }

            if (reader.Current.Kind == HumanReadableTokenKind.Colon)
            {
                reader.Advance();
                reader.SkipTrivia();

                HumanReadableToken nameToken = ExpectNameToken(reader, "Expected field name after ':' in anonymous type member.");
                string name = nameToken.Text;

                reader.SkipTrivia();
                if (reader.Current.Kind == HumanReadableTokenKind.Equals)
                {
                    reader.Advance();
                }
                else
                {
                    throw reader.Error("Expected '=' after anonymous type member name.", reader.Current);
                }

                reader.SkipTrivia();
                HumanReadableExpressionNode value = ParseExpression(reader, 0);

                int end = value.End;
                string text = SafeSlice(reader.Source, startToken.Start, end);
                member = new HumanReadableAnonymousTypeMemberStatementNode(typeReference, name, value, startToken.Start, end, text);
                return true;
            }

            if (reader.Current.Kind == HumanReadableTokenKind.Identifier
                && PeekKind(reader, 1) == HumanReadableTokenKind.Equals)
            {
                HumanReadableToken nameToken = reader.Current;
                reader.Advance();
                reader.SkipTrivia();
                Expect(reader, HumanReadableTokenKind.Equals, "Expected '=' after member name in anonymous type.");
                reader.SkipTrivia();

                HumanReadableExpressionNode value = ParseExpression(reader, 0);
                int end = value.End;
                string text = SafeSlice(reader.Source, startToken.Start, end);
                member = new HumanReadableAnonymousTypeMemberStatementNode(null, nameToken.Text, value, startToken.Start, end, text);
                return true;
            }

            reader.Index = startIndex;
            return false;
        }
        catch
        {
            reader.Index = startIndex;
            throw;
        }
    }

    private static HumanReadableTokenKind PeekKind(TokenReader reader, int v)
    {
        return reader.Next().Kind;
    }

    private static bool TryParseDirectiveParameter(TokenReader reader, out HumanReadableParameterNode? parameter)
    {
        parameter = null;

        int startIndex = reader.Index;
        try
        {
            if (!CanStartTypeReference(reader.Current.Kind))
                return false;

            HumanReadableTypeReferenceNode typeReference = ParseTypeReference(reader);
            reader.SkipTrivia();

            if (!IsNameToken(reader.Current.Kind))
            {
                reader.Index = startIndex;
                return false;
            }

            HumanReadableToken nameToken = reader.Current;
            reader.Advance();

            int end = nameToken.End();
            string text = SafeSlice(reader.Source, typeReference.Start, end);
            parameter = new HumanReadableParameterNode(typeReference, nameToken.Text, typeReference.Start, end, text);
            return true;
        }
        catch
        {
            reader.Index = startIndex;
            throw;
        }
    }

    private static HumanReadableExpressionNode ParseExpressionUntil(TokenReader reader, HumanReadableTokenKind terminator)
    {
        int startIndex = reader.Index;
        HumanReadableExpressionNode expression = ParseExpression(reader, 0);

        if (reader.Current.Kind != terminator)
        {
            reader.Index = startIndex;
            throw reader.Error($"Expected '{terminator}'.", reader.Current);
        }

        return expression;
    }

    private static HumanReadableExpressionNode ParseExpression(TokenReader reader, int minimumPrecedence)
    {
        reader.SkipTrivia();
        HumanReadableExpressionNode left = ParsePrefixExpression(reader);

        while (true)
        {
            reader.SkipTrivia();
            HumanReadableToken token = reader.Current;

            if (token.Kind is HumanReadableTokenKind.EndOfFile
                or HumanReadableTokenKind.CloseParen
                or HumanReadableTokenKind.CloseBracket
                or HumanReadableTokenKind.CloseBrace
                or HumanReadableTokenKind.Semicolon
                or HumanReadableTokenKind.Comma
                or HumanReadableTokenKind.Colon
                or HumanReadableTokenKind.KeywordElse)
            {
                break;
            }

            if (token.Kind == HumanReadableTokenKind.OpenParen)
            {
                if (PostfixPrecedence < minimumPrecedence)
                    break;

                left = ParseCallExpression(reader, left);
                continue;
            }

            if (token.Kind is HumanReadableTokenKind.Dot or HumanReadableTokenKind.Arrow)
            {
                if (PostfixPrecedence < minimumPrecedence)
                    break;

                left = ParseMemberAccessExpression(reader, left);
                continue;
            }

            if (!TryGetBinaryPrecedence(token.Kind, out int precedence, out bool rightAssociative))
                break;

            if (precedence < minimumPrecedence)
                break;

            reader.Advance();
            reader.SkipTrivia();

            int nextMinimum = rightAssociative ? precedence : precedence + 1;
            HumanReadableExpressionNode right = ParseExpression(reader, nextMinimum);

            int start = left.Start;
            int end = right.End;
            string text = SafeSlice(reader.Source, start, end);

            left = token.Kind == HumanReadableTokenKind.Equals
                ? new HumanReadableAssignmentExpressionNode(left, right, start, end, text)
                : new HumanReadableBinaryExpressionNode(left, token.Text, right, start, end, text);
        }

        return left;
    }

    private static HumanReadableExpressionNode ParsePrefixExpression(TokenReader reader)
    {
        reader.SkipTrivia();
        HumanReadableToken token = reader.Current;

        if (token.Kind == HumanReadableTokenKind.Number)
        {
            reader.Advance();
            object? value = decimal.TryParse(token.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsed)
                ? parsed
                : null;

            return new HumanReadableLiteralExpressionNode(HumanReadableLiteralKind.Number, token.Text, value, token.Start, token.End(), token.Text);
        }

        if (token.Kind == HumanReadableTokenKind.String)
        {
            reader.Advance();
            return new HumanReadableLiteralExpressionNode(HumanReadableLiteralKind.String, token.Text, token.Text, token.Start, token.End(), token.Text);
        }

        if (token.Kind == HumanReadableTokenKind.KeywordTrue)
        {
            reader.Advance();
            return new HumanReadableLiteralExpressionNode(HumanReadableLiteralKind.Boolean, token.Text, true, token.Start, token.End(), token.Text);
        }

        if (token.Kind == HumanReadableTokenKind.KeywordFalse)
        {
            reader.Advance();
            return new HumanReadableLiteralExpressionNode(HumanReadableLiteralKind.Boolean, token.Text, false, token.Start, token.End(), token.Text);
        }

        if (token.Kind == HumanReadableTokenKind.KeywordNull)
        {
            reader.Advance();
            return new HumanReadableLiteralExpressionNode(HumanReadableLiteralKind.Null, token.Text, null, token.Start, token.End(), token.Text);
        }

        if (token.Kind == HumanReadableTokenKind.KeywordDefault)
        {
            reader.Advance();
            return new HumanReadableLiteralExpressionNode(HumanReadableLiteralKind.Default, token.Text, null, token.Start, token.End(), token.Text);
        }

        if (token.Kind == HumanReadableTokenKind.KeywordThis)
        {
            reader.Advance();
            return new HumanReadableThisExpressionNode(token.Start, token.End(), token.Text);
        }

        if (token.Kind == HumanReadableTokenKind.Identifier)
        {
            reader.Advance();
            HumanReadableExpressionNode left = new HumanReadableIdentifierExpressionNode(token.Text, token.Start, token.End(), token.Text);
            return ParsePostfixExpression(reader, left);
        }

        if (token.Kind == HumanReadableTokenKind.Directive)
        {
            if (token.Text is "#ref" or "#stack" or "#type")
                return ParseBuiltinCallExpression(reader);

            throw reader.Error("Unexpected directive in expression.", token);
        }

        if (token.Kind == HumanReadableTokenKind.OpenParen)
        {
            int start = token.Start;
            reader.Advance();
            reader.SkipTrivia();

            HumanReadableExpressionNode expression = ParseExpression(reader, 0);
            reader.SkipTrivia();

            Expect(reader, HumanReadableTokenKind.CloseParen, "Expected ')' to close grouped expression.");
            int end = reader.PreviousOrCurrent().End();
            string text = SafeSlice(reader.Source, start, end);

            return new HumanReadableGroupedExpressionNode(expression, start, end, text);
        }

        if (token.Kind is HumanReadableTokenKind.Plus or HumanReadableTokenKind.Minus or HumanReadableTokenKind.Bang)
        {
            reader.Advance();
            reader.SkipTrivia();

            HumanReadableExpressionNode operand = ParseExpression(reader, UnaryPrecedence);
            int end = operand.End;
            string text = SafeSlice(reader.Source, token.Start, end);

            return new HumanReadableUnaryExpressionNode(token.Text, operand, token.Start, end, text);
        }

        if (token.Kind == HumanReadableTokenKind.LessThan)
            return ParseCollectionExpression(reader);

        throw reader.Error("Unexpected token in expression.", token);
    }

    private static HumanReadableExpressionNode ParsePostfixExpression(TokenReader reader, HumanReadableExpressionNode left)
    {
        while (true)
        {
            reader.SkipTrivia();

            if (reader.Current.Kind == HumanReadableTokenKind.OpenParen)
            {
                left = ParseCallExpression(reader, left);
                continue;
            }

            if (reader.Current.Kind is HumanReadableTokenKind.Dot or HumanReadableTokenKind.Arrow)
            {
                left = ParseMemberAccessExpression(reader, left);
                continue;
            }

            break;
        }

        return left;
    }

    private static HumanReadableExpressionNode ParseCallExpression(TokenReader reader, HumanReadableExpressionNode target)
    {
        HumanReadableToken open = Expect(reader, HumanReadableTokenKind.OpenParen, "Expected '(' after callable expression.");
        reader.SkipTrivia();

        List<HumanReadableExpressionNode> arguments = [];
        if (reader.Current.Kind != HumanReadableTokenKind.CloseParen)
        {
            while (true)
            {
                HumanReadableExpressionNode argument = ParseExpression(reader, 0);
                arguments.Add(argument);

                reader.SkipTrivia();
                if (reader.Current.Kind == HumanReadableTokenKind.Comma)
                {
                    reader.Advance();
                    reader.SkipTrivia();
                    continue;
                }

                break;
            }
        }

        Expect(reader, HumanReadableTokenKind.CloseParen, "Expected ')' after call arguments.");
        int end = reader.PreviousOrCurrent().End();
        string text = SafeSlice(reader.Source, target.Start, end);
        return new HumanReadableCallExpressionNode(target, arguments, target.Start, end, text);
    }

    private static HumanReadableExpressionNode ParseMemberAccessExpression(TokenReader reader, HumanReadableExpressionNode target)
    {
        HumanReadableToken accessToken = reader.Current;
        reader.Advance();
        reader.SkipTrivia();

        HumanReadableToken memberToken = ExpectNameToken(reader, "Expected member name after access operator.");
        int end = memberToken.End();
        string text = SafeSlice(reader.Source, target.Start, end);

        HumanReadableAccessKind kind = accessToken.Kind == HumanReadableTokenKind.Arrow
            ? HumanReadableAccessKind.Arrow
            : HumanReadableAccessKind.Dot;

        return new HumanReadableMemberAccessExpressionNode(target, memberToken.Text, kind, target.Start, end, text);
    }

    private static HumanReadableExpressionNode ParseBuiltinCallExpression(TokenReader reader)
    {
        HumanReadableToken directive = reader.Current;
        reader.Advance();
        reader.SkipTrivia();

        string name = directive.Text.TrimStart('#');
        Expect(reader, HumanReadableTokenKind.OpenParen, $"Expected '(' after '{directive.Text}'.");
        reader.SkipTrivia();

        List<HumanReadableExpressionNode> arguments = [];
        if (reader.Current.Kind != HumanReadableTokenKind.CloseParen)
        {
            while (true)
            {
                HumanReadableExpressionNode argument = ParseExpression(reader, 0);
                arguments.Add(argument);

                reader.SkipTrivia();
                if (reader.Current.Kind == HumanReadableTokenKind.Comma)
                {
                    reader.Advance();
                    reader.SkipTrivia();
                    continue;
                }

                break;
            }
        }

        Expect(reader, HumanReadableTokenKind.CloseParen, $"Expected ')' after '{directive.Text}' call.");
        int end = reader.PreviousOrCurrent().End();
        string text = SafeSlice(reader.Source, directive.Start, end);
        return new HumanReadableBuiltinCallExpressionNode(name, arguments, directive.Start, end, text);
    }

    private static HumanReadableExpressionNode ParseCollectionExpression(TokenReader reader)
    {
        HumanReadableToken startToken = Expect(reader, HumanReadableTokenKind.LessThan, "Expected '<' to begin collection type.");
        reader.SkipTrivia();

        List<HumanReadableTypeReferenceNode> typeParts = [ParseTypeReference(reader)];
        reader.SkipTrivia();

        while (reader.Current.Kind == HumanReadableTokenKind.Comma)
        {
            reader.Advance();
            reader.SkipTrivia();
            typeParts.Add(ParseTypeReference(reader));
            reader.SkipTrivia();
        }

        Expect(reader, HumanReadableTokenKind.GreaterThan, "Expected '>' after collection type.");
        reader.SkipTrivia();
        Expect(reader, HumanReadableTokenKind.OpenBracket, "Expected '[' after collection type.");
        reader.SkipTrivia();

        if (typeParts.Count == 1)
        {
            List<HumanReadableExpressionNode> items = [];

            if (reader.Current.Kind != HumanReadableTokenKind.CloseBracket)
            {
                while (true)
                {
                    HumanReadableExpressionNode item = ParseExpression(reader, 0);
                    items.Add(item);

                    reader.SkipTrivia();
                    if (reader.Current.Kind == HumanReadableTokenKind.Comma)
                    {
                        reader.Advance();
                        reader.SkipTrivia();
                        continue;
                    }

                    break;
                }
            }

            Expect(reader, HumanReadableTokenKind.CloseBracket, "Expected ']' after collection elements.");
            int end = reader.PreviousOrCurrent().End();
            string text = SafeSlice(reader.Source, startToken.Start, end);
            return new HumanReadableCollectionExpressionNode(typeParts[0], items, startToken.Start, end, text);
        }

        HumanReadableTypeReferenceNode keyType = typeParts[0];
        HumanReadableTypeReferenceNode valueType = typeParts[1];

        List<HumanReadableDictionaryEntryNode> entries = [];

        if (reader.Current.Kind != HumanReadableTokenKind.CloseBracket)
        {
            while (true)
            {
                HumanReadableExpressionNode key = ParseExpression(reader, 0);
                reader.SkipTrivia();

                Expect(reader, HumanReadableTokenKind.FatArrow, "Expected '=>' between dictionary key and value.");
                reader.SkipTrivia();

                HumanReadableExpressionNode value = ParseExpression(reader, 0);
                int entryStart = key.Start;
                int entryEnd = value.End;
                string entryText = SafeSlice(reader.Source, entryStart, entryEnd);
                entries.Add(new HumanReadableDictionaryEntryNode(key, value, entryStart, entryEnd, entryText));

                reader.SkipTrivia();
                if (reader.Current.Kind == HumanReadableTokenKind.Comma)
                {
                    reader.Advance();
                    reader.SkipTrivia();
                    continue;
                }

                break;
            }
        }

        Expect(reader, HumanReadableTokenKind.CloseBracket, "Expected ']' after dictionary entries.");
        int dictionaryEnd = reader.PreviousOrCurrent().End();
        string dictionaryText = SafeSlice(reader.Source, startToken.Start, dictionaryEnd);
        return new HumanReadableDictionaryExpressionNode(keyType, valueType, entries, startToken.Start, dictionaryEnd, dictionaryText);
    }

    private static HumanReadableTypeReferenceNode ParseTypeReference(TokenReader reader)
    {
        reader.SkipTrivia();
        HumanReadableToken startToken = reader.Current;
        List<string> segments = [ExpectNameToken(reader, "Expected type name.").Text];
        int end = reader.PreviousOrCurrent().End();

        while (true)
        {
            reader.SkipTrivia();
            if (reader.Current.Kind != HumanReadableTokenKind.Dot)
                break;

            reader.Advance();
            reader.SkipTrivia();

            HumanReadableToken segmentToken = ExpectNameToken(reader, "Expected type segment after '.'.");
            segments.Add(segmentToken.Text);
            end = segmentToken.End();
        }

        List<HumanReadableTypeReferenceNode> genericArguments = [];
        reader.SkipTrivia();

        if (reader.Current.Kind == HumanReadableTokenKind.LessThan)
        {
            reader.Advance();
            reader.SkipTrivia();

            if (reader.Current.Kind != HumanReadableTokenKind.GreaterThan)
            {
                while (true)
                {
                    genericArguments.Add(ParseTypeReference(reader));
                    reader.SkipTrivia();

                    if (reader.Current.Kind == HumanReadableTokenKind.Comma)
                    {
                        reader.Advance();
                        reader.SkipTrivia();
                        continue;
                    }

                    break;
                }
            }

            Expect(reader, HumanReadableTokenKind.GreaterThan, "Expected '>' after generic arguments.");
            end = reader.PreviousOrCurrent().End();
        }

        string text = SafeSlice(reader.Source, startToken.Start, end);
        return new HumanReadableTypeReferenceNode(segments, genericArguments, startToken.Start, end, text);
    }

    private static bool TryGetBinaryPrecedence(HumanReadableTokenKind kind, out int precedence, out bool rightAssociative)
    {
        rightAssociative = false;
        precedence = kind switch
        {
            HumanReadableTokenKind.Equals => 5,
            HumanReadableTokenKind.OrOr => 10,
            HumanReadableTokenKind.Pipe => 12,
            HumanReadableTokenKind.Caret => 13,
            HumanReadableTokenKind.AndAnd => 14,
            HumanReadableTokenKind.DoubleEquals or HumanReadableTokenKind.NotEquals => 20,
            HumanReadableTokenKind.GreaterThan or HumanReadableTokenKind.GreaterOrEqual or HumanReadableTokenKind.LessThan or HumanReadableTokenKind.LessOrEqual => 30,
            HumanReadableTokenKind.Plus or HumanReadableTokenKind.Minus => 40,
            HumanReadableTokenKind.Star or HumanReadableTokenKind.Slash or HumanReadableTokenKind.Percent => 50,
            _ => -1
        };

        if (kind == HumanReadableTokenKind.Equals)
            rightAssociative = true;

        return precedence >= 0;
    }

    private static bool IsDirectiveBlock(string text)
        => text is "#container" or "#template";

    private static bool CanStartTypeReference(HumanReadableTokenKind kind)
        => kind is HumanReadableTokenKind.Identifier
            or HumanReadableTokenKind.KeywordTemplate
            or HumanReadableTokenKind.KeywordContainer
            or HumanReadableTokenKind.KeywordVariables
            or HumanReadableTokenKind.KeywordThis;

    private static bool IsNameToken(HumanReadableTokenKind kind)
        => kind is HumanReadableTokenKind.Identifier
            or HumanReadableTokenKind.KeywordTemplate
            or HumanReadableTokenKind.KeywordContainer
            or HumanReadableTokenKind.KeywordVariables
            or HumanReadableTokenKind.KeywordThis;

    private static int UnaryPrecedence => 60;
    private static int PostfixPrecedence => 80;

    private static HumanReadableToken Expect(TokenReader reader, HumanReadableTokenKind kind, string message)
    {
        if (reader.Current.Kind != kind)
            throw reader.Error(message, reader.Current);

        HumanReadableToken token = reader.Current;
        reader.Advance();
        return token;
    }

    private static void ExpectKeyword(TokenReader reader, HumanReadableTokenKind kind, string message)
    {
        if (reader.Current.Kind != kind)
            throw reader.Error(message, reader.Current);

        reader.Advance();
    }

    private static HumanReadableToken ExpectNameToken(TokenReader reader, string message)
    {
        if (!IsNameToken(reader.Current.Kind))
            throw reader.Error(message, reader.Current);

        HumanReadableToken token = reader.Current;
        reader.Advance();
        return token;
    }

    private static string SafeSlice(string source, int start, int end)
    {
        if (start < 0 || end < start || end > source.Length)
            return string.Empty;

        return source[start..end];
    }

    private enum StatementContext
    {
        General,
        AnonymousType
    }

    private sealed class TokenReader
    {
        private readonly IReadOnlyList<HumanReadableToken> tokens;

        public TokenReader(string source, IReadOnlyList<HumanReadableToken> tokens)
        {
            Source = source;
            this.tokens = tokens;
            Index = 0;
        }

        public string Source { get; }
        public int Index { get; set; }

        public HumanReadableToken Current => tokens[Math.Min(Index, tokens.Count - 1)];
        public bool IsEnd => Current.Kind == HumanReadableTokenKind.EndOfFile;

        public void Advance()
        {
            if (Index < tokens.Count - 1)
                Index++;
        }

        public HumanReadableToken PreviousOrCurrent()
            => tokens[Math.Max(0, Math.Min(Index - 1, tokens.Count - 1))];

        public HumanReadableToken Next()
            => tokens[Math.Max(0, Math.Min(Index + 1, tokens.Count - 1))];

        public void SkipTrivia()
        {
            while (!IsEnd && (Current.Kind == HumanReadableTokenKind.NewLine || Current.Kind == HumanReadableTokenKind.Comment))
                Advance();
        }

        public WinterForgeSyntaxException Error(string message, HumanReadableToken token, string? suggestion = null)
            => WinterForgeSyntaxException.ForToken(message, Source, token.Line, token.Column, token.Text, suggestion);
    }
}

internal static class HumanReadableTokenExtensions
{
    public static int End(this HumanReadableToken token) => token.Start + token.Length;
}
