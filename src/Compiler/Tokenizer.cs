﻿using Cascadium.Object;
using System.Collections.Generic;

namespace Cascadium.Compiler;

internal class Tokenizer
{
    public TextInterpreter Interpreter { get; }

    public Tokenizer(string code)
    {
        Interpreter = new TextInterpreter(code);
    }

    public void Tokenize(TokenBuffer collection)
    {
        char[] hitChars = new char[] { Token.Ch_BraceOpen, Token.Ch_BraceClose, Token.Ch_Semicolon };
        int opennedRules = 0;
        TokenDebugInfo lastOpennedRule = default;

        string result;
        char hit;
        while ((hit = Interpreter.ReadUntil(hitChars, true, out result)) != '\0')
        {
            if (hit == Token.Ch_Semicolon)
            {
                collection.Write(ReadCurrentDeclaration(result));
            }
            else if (hit == Token.Ch_BraceOpen)
            {
                collection.Write(ReadSelectors(result));
                collection.Write(new Token(TokenType.Em_RuleStart, "", Interpreter));

                opennedRules++;
                lastOpennedRule = Interpreter.TakeSnapshot(-1);

            }
            else if (hit == Token.Ch_BraceClose)
            {
                if (!string.IsNullOrWhiteSpace(result))
                {
                    // remaining declaration
                    collection.Write(ReadCurrentDeclaration(result));
                }

                collection.Write(new Token(TokenType.Em_RuleEnd, "", Interpreter));
                opennedRules--;
            }
        }

        if (hit == '\0' && !string.IsNullOrWhiteSpace(result))
        {
            throw new CascadiumException(Interpreter.TakeSnapshot(result), Interpreter.InputString, "syntax error: unexpected token");
        }

        if (opennedRules != 0)
        {
            throw new CascadiumException(lastOpennedRule, Interpreter.InputString, "syntax error: unclosed rule");
        }
    }

    IEnumerable<Token> ReadCurrentDeclaration(string declaration)
    {
        if (declaration.TrimStart().StartsWith('@'))
        {
            // its an statement
            yield return new Token(TokenType.Em_Statement, declaration.Trim(), Interpreter);
            yield break;
        }

        int dotPos = declaration.IndexOf(Token.Ch_DoubleDots);
        if (dotPos == -1)
        {
            throw new CascadiumException(Interpreter.TakeSnapshot(declaration), Interpreter.InputString, "syntax error: unexpected token \"" + declaration.Trim() + "\"");
        }

        string property = declaration.Substring(0, dotPos).Trim();
        string value = declaration.Substring(dotPos + 1).Trim();

        if (!Token.IsValidPropertyName(property))
        {
            throw new CascadiumException(Interpreter.TakeSnapshot(declaration), Interpreter.InputString, "syntax error: invalid property name");
        }
        else if (Token.IsPropertyValueUnescapedDoubleDots(value))
        {
            throw new CascadiumException(Interpreter.TakeSnapshot(declaration), Interpreter.InputString, "syntax error: unclosed declaration");
        }
        else
        {
            yield return new Token(TokenType.Em_PropertyName, property, Interpreter);
            yield return new Token(TokenType.Em_PropertyValue, value, Interpreter);
        }
    }

    IEnumerable<Token> ReadSelectors(string selectorCode)
    {
        if (selectorCode.IndexOf(',') < 0)
        {
            yield return new Token(TokenType.Em_Selector, selectorCode.Trim(), Interpreter);
        }
        else
        {
            string[] selectors = Helper.SafeSplit(selectorCode, ',');
            foreach (string s in selectors)
            {
                yield return new Token(TokenType.Em_Selector, s.Trim(), Interpreter);
            }
        }
    }
}
