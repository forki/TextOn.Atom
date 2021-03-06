﻿namespace TextOn.Atom

open System

type Token =
    | Att
    | Var
    | Func
    | Free
    | Break
    | Sequential
    | Choice
    | VariableName of string
    | OpenBrace
    | CloseBrace
    | OpenCurly
    | CloseCurly
    | AttributeName of string
    | OpenBracket
    | CloseBracket
    | QuotedString of string
    | Or
    | And
    | Equals
    | NotEquals
    | InvalidPreprocessorError of string
    | InvalidUnrecognised of string
    | ChoiceSeparator
    | RawText of string
    | InvalidReservedToken of string
    | FunctionName of string
    | Private

type AttributedToken = {
    TokenStartLocation : int
    TokenEndLocation : int
    Token : Token }

type AttributedTokenizedLine =
    {
        LineNumber : int
        Tokens : AttributedToken list
    }

type CategorizedAttributedTokenSet =
    {
        Category : Category
        Index : int
        File : string
        StartLine : int
        EndLine : int
        Tokens : AttributedTokenizedLine list
    }
