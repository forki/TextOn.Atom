﻿namespace TextOn.Atom

/// Unique identifier for an attribute in some cache we prepare.
type AttributeOrVariableIdentity =
    | Attribute of int
    | Variable of int

/// Represents a tree of decisions.
type VariableCondition =
    | VarTrue
    | VarBoth of VariableCondition * VariableCondition
    | VarEither of VariableCondition * VariableCondition
    | VarAreEqual of AttributeOrVariableIdentity * string
    | VarAreNotEqual of AttributeOrVariableIdentity * string

/// Resolve a condition from a cache of values.
[<RequireQualifiedAccess>]
module VariableConditionEvaluator =
    /// Resolve a condition's value.
    let rec resolve (attributeCache:Map<int, string>) (variableCache:Map<int, string>) condition : bool =
        match condition with
        | VarTrue -> true
        | VarBoth(c1, c2) -> (c1 |> resolve attributeCache variableCache) && (c2 |> resolve attributeCache variableCache)
        | VarEither(c1, c2) -> (c1 |> resolve attributeCache variableCache) || (c2 |> resolve attributeCache variableCache)
        | VarAreEqual(identity, value) ->
            match identity with
            | Attribute attributeIdentity ->
                (attributeCache |> Map.find attributeIdentity) = value
            | Variable variableIdentity ->
                (variableCache |> Map.find variableIdentity) = value
        | VarAreNotEqual(identity, value) ->
            match identity with
            | Attribute attributeIdentity ->
                (attributeCache |> Map.find attributeIdentity) <> value
            | Variable variableIdentity ->
                (variableCache |> Map.find variableIdentity) <> value

    /// Resolve a condition's value from partial caches.
    let rec resolvePartial (attributeCache:Map<int, string>) (variableCache:Map<int, string>) condition : bool =
        match condition with
        | VarTrue -> true
        | VarBoth(c1, c2) -> (c1 |> resolvePartial attributeCache variableCache) && (c2 |> resolvePartial attributeCache variableCache)
        | VarEither(c1, c2) -> (c1 |> resolvePartial attributeCache variableCache) || (c2 |> resolvePartial attributeCache variableCache)
        | VarAreEqual(identity, value) ->
            match identity with
            | Attribute attributeIdentity ->
                attributeCache |> Map.tryFind attributeIdentity |> Option.map ((=) value) |> defaultArg <| true
            | Variable variableIdentity ->
                variableCache |> Map.tryFind variableIdentity |> Option.map ((=) value) |> defaultArg <| true
        | VarAreNotEqual(identity, value) ->
            match identity with
            | Attribute attributeIdentity ->
                attributeCache |> Map.tryFind attributeIdentity |> Option.map ((<>) value) |> defaultArg <| true
            | Variable variableIdentity ->
                variableCache |> Map.tryFind variableIdentity |> Option.map ((<>) value) |> defaultArg <| true
