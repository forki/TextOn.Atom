﻿module TextOn.Atom.Test.TestConditionParser

open System
open FsUnit
open FsCheck
open NUnit.Framework
open Swensen.Unquote
open FSharp.Quotations
open System.Collections.Generic

open TextOn.Atom

[<Test>]
let ``Test simple equals``() =
    let makeToken s e t = { TokenStartLocation = s ; TokenEndLocation = e ; Token = t }
    let tokens =
        [
            makeToken 1 1 OpenBrace
            makeToken 2 8 (AttributeName "Gender")
            makeToken 10 10 Equals
            makeToken 12 17 (QuotedString "Male")
            makeToken 18 18 CloseBrace
        ]
    let expected = {
        HasErrors = false
        Condition = ParsedAreEqual("Gender", "Male") }
    test <@ ConditionParser.parseCondition tokens = expected @>

[<Test>]
let ``Test simple not equals``() =
    let makeToken s e t = { TokenStartLocation = s ; TokenEndLocation = e ; Token = t }
    let tokens =
        [
            makeToken 1 1 OpenBrace
            makeToken 2 8 (AttributeName "Gender")
            makeToken 10 11 NotEquals
            makeToken 13 18 (QuotedString "Male")
            makeToken 19 19 CloseBrace
        ]
    let expected = {
        HasErrors = false
        Condition = ParsedAreNotEqual("Gender", "Male") }
    test <@ ConditionParser.parseCondition tokens = expected @>

[<Test>]
let ``Test bracketed equals``() =
    let makeToken s e t = { TokenStartLocation = s ; TokenEndLocation = e ; Token = t }
    let tokens =
        [
            makeToken 1 1 OpenBrace
            makeToken 2 2 OpenBracket
            makeToken 3 9 (AttributeName "Gender")
            makeToken 11 11 Equals
            makeToken 13 18 (QuotedString "Male")
            makeToken 19 19 CloseBracket
            makeToken 20 20 CloseBrace
        ]
    let expected = {
        HasErrors = false
        Condition = ParsedAreEqual("Gender", "Male") }
    test <@ ConditionParser.parseCondition tokens = expected @>

[<Test>]
let ``Test bracketed not equals``() =
    let makeToken s e t = { TokenStartLocation = s ; TokenEndLocation = e ; Token = t }
    let tokens =
        [
            makeToken 1 1 OpenBrace
            makeToken 2 2 OpenBracket
            makeToken 3 9 (AttributeName "Gender")
            makeToken 11 12 NotEquals
            makeToken 14 19 (QuotedString "Male")
            makeToken 20 20 CloseBracket
            makeToken 21 21 CloseBrace
        ]
    let expected = {
        HasErrors = false
        Condition = ParsedAreNotEqual("Gender", "Male") }
    test <@ ConditionParser.parseCondition tokens = expected @>

[<Test>]
let ``Test single or``() =
    let makeToken s e t = { TokenStartLocation = s ; TokenEndLocation = e ; Token = t }
    let tokens =
        [
            makeToken 1 1 OpenBrace
            makeToken 2 8 (AttributeName "Gender")
            makeToken 10 10 Equals
            makeToken 12 17 (QuotedString "Male")
            makeToken 19 20 Or
            makeToken 22 28 (AttributeName "Gender")
            makeToken 30 30 Equals
            makeToken 32 39 (QuotedString "Female")
            makeToken 40 40 CloseBrace
        ]
    let expected = {
        HasErrors = false
        Condition = ParsedOr(ParsedAreEqual("Gender", "Male"), ParsedAreEqual("Gender", "Female")) }
    test <@ ConditionParser.parseCondition tokens = expected @>

[<Test>]
let ``Test multiple ors``() =
    let makeToken s e t = { TokenStartLocation = s ; TokenEndLocation = e ; Token = t }
    let tokens =
        [
            makeToken 1 1 OpenBrace
            makeToken 2 8 (AttributeName "Gender")
            makeToken 10 10 Equals
            makeToken 12 17 (QuotedString "Male")
            makeToken 19 20 Or
            makeToken 22 28 (AttributeName "Gender")
            makeToken 30 30 Equals
            makeToken 32 39 (QuotedString "Female")
            makeToken 41 42 Or
            makeToken 44 50 (AttributeName "Gender")
            makeToken 52 52 Equals
            makeToken 54 72 (QuotedString "Attack helicopter")
            makeToken 74 75 Or
            makeToken 77 83 (AttributeName "Gender")
            makeToken 85 85 Equals
            makeToken 87 93 (QuotedString "Other")
            makeToken 94 94 CloseBrace
        ]
    let expected = {
        HasErrors = false
        Condition =
            ParsedOr(
                ParsedAreEqual("Gender", "Male"),
                ParsedOr(
                    ParsedAreEqual("Gender", "Female"),
                    ParsedOr(
                        ParsedAreEqual("Gender", "Attack helicopter"),
                        ParsedAreEqual("Gender", "Other")))) }
    test <@ ConditionParser.parseCondition tokens = expected @>

[<Test>]
let ``Test single and``() =
    let makeToken s e t = { TokenStartLocation = s ; TokenEndLocation = e ; Token = t }
    let tokens =
        [
            makeToken 1 1 OpenBrace
            makeToken 2 8 (AttributeName "Gender")
            makeToken 10 10 Equals
            makeToken 12 17 (QuotedString "Male")
            makeToken 19 20 And
            makeToken 22 28 (AttributeName "Gender")
            makeToken 30 30 Equals
            makeToken 32 39 (QuotedString "Female")
            makeToken 40 40 CloseBrace
        ]
    let expected = {
        HasErrors = false
        Condition = ParsedAnd(ParsedAreEqual("Gender", "Male"), ParsedAreEqual("Gender", "Female")) }
    test <@ ConditionParser.parseCondition tokens = expected @>

[<Test>]
let ``Test multiple ands``() =
    let makeToken s e t = { TokenStartLocation = s ; TokenEndLocation = e ; Token = t }
    let tokens =
        [
            makeToken 1 1 OpenBrace
            makeToken 2 8 (AttributeName "Gender")
            makeToken 10 10 Equals
            makeToken 12 17 (QuotedString "Male")
            makeToken 19 20 And
            makeToken 22 28 (AttributeName "Gender")
            makeToken 30 30 Equals
            makeToken 32 39 (QuotedString "Female")
            makeToken 41 42 And
            makeToken 44 50 (AttributeName "Gender")
            makeToken 52 52 Equals
            makeToken 54 72 (QuotedString "Attack helicopter")
            makeToken 74 75 And
            makeToken 77 83 (AttributeName "Gender")
            makeToken 85 85 Equals
            makeToken 87 93 (QuotedString "Other")
            makeToken 94 94 CloseBrace
        ]
    let expected = {
        HasErrors = false
        Condition =
            ParsedAnd(
                ParsedAreEqual("Gender", "Male"),
                ParsedAnd(
                    ParsedAreEqual("Gender", "Female"),
                    ParsedAnd(
                        ParsedAreEqual("Gender", "Attack helicopter"),
                        ParsedAreEqual("Gender", "Other")))) }
    test <@ ConditionParser.parseCondition tokens = expected @>

// OPS I'm not actually sure this is the correct precedence, but I implemented a precedence anyway, can easily be reversed.
[<Test>]
let ``Test and/or precedence``() =
    let makeToken s e t = { TokenStartLocation = s ; TokenEndLocation = e ; Token = t }
    let tokens =
        [
            makeToken 1 1 OpenBrace
            makeToken 2 8 (AttributeName "Gender")
            makeToken 10 10 Equals
            makeToken 12 17 (QuotedString "Male")
            makeToken 19 20 And
            makeToken 22 28 (AttributeName "Gender")
            makeToken 30 30 Equals
            makeToken 32 39 (QuotedString "Female")
            makeToken 41 42 Or
            makeToken 44 50 (AttributeName "Gender")
            makeToken 52 52 Equals
            makeToken 54 72 (QuotedString "Attack helicopter")
            makeToken 74 75 And
            makeToken 77 83 (AttributeName "Gender")
            makeToken 85 85 Equals
            makeToken 87 93 (QuotedString "Other")
            makeToken 94 94 CloseBrace
        ]
    let expected = {
        HasErrors = false
        Condition =
            ParsedAnd(
                ParsedAreEqual ("Gender","Male"),
                ParsedAnd(
                    ParsedOr(
                        ParsedAreEqual ("Gender","Female"),
                        ParsedAreEqual ("Gender","Attack helicopter")),
                    ParsedAreEqual ("Gender","Other"))) }
    test <@ ConditionParser.parseCondition tokens = expected @>

[<Test>]
let ``Test brackets``() =
    let makeToken s e t = { TokenStartLocation = s ; TokenEndLocation = e ; Token = t }
    let tokens =
        [
            makeToken 1 1 OpenBrace
            makeToken 2 2 OpenBracket
            makeToken 3 9 (AttributeName "Gender")
            makeToken 11 11 Equals
            makeToken 13 18 (QuotedString "Male")
            makeToken 20 21 And
            makeToken 23 29 (AttributeName "Gender")
            makeToken 31 31 Equals
            makeToken 33 40 (QuotedString "Female")
            makeToken 41 41 CloseBracket
            makeToken 43 44 Or
            makeToken 46 52 (AttributeName "Gender")
            makeToken 54 54 Equals
            makeToken 56 74 (QuotedString "Attack helicopter")
            makeToken 76 77 And
            makeToken 79 85 (AttributeName "Gender")
            makeToken 87 87 Equals
            makeToken 89 95 (QuotedString "Other")
            makeToken 96 96 CloseBrace
        ]
    let expected = {
        HasErrors = false
        Condition =
            ParsedAnd(
                ParsedOr(
                    ParsedAnd(
                        ParsedAreEqual ("Gender","Male"),
                        ParsedAreEqual ("Gender","Female")),
                    ParsedAreEqual ("Gender","Attack helicopter")),
                ParsedAreEqual ("Gender","Other")) }
    test <@ ConditionParser.parseCondition tokens = expected @>
