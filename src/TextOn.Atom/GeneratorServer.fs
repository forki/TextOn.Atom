﻿namespace TextOn.Atom

open TextOn.Atom.DTO.DTO
open System
open System.IO

type GeneratorStartResult =
    | CompilationFailure of CompilationError[]
    | GeneratorStarted of GeneratorData

[<Sealed>]
// OPS World's least thread-safe implementation for now.
type GeneratorServer(file, name) =
    let mutable currentTemplate = None
    let mutable lastResult = None
    let mutable attributeValues = Map.empty
    let mutable variableValues : Map<string, (string * bool)> = Map.empty // stores whether the user chose this value or the system did
    member __.File = file |> System.IO.FileInfo
    member __.UpdateTemplate (template:CompiledTemplate) = currentTemplate <- Some template
    member __.Data =
        let currentTemplate = currentTemplate.Value
        let (haveSeenEmpty, attributes, variables) =
            let attributeNameToIndex, attributeIndexToName = currentTemplate.Attributes |> Array.map (fun a -> a.Name, a.Index) |> fun a -> (a |> Map.ofArray |> fun m x -> m |> Map.tryFind x), (a |> Array.map (fun (x,y) -> (y,x)) |> Map.ofArray |> fun m x -> m |> Map.tryFind x)
            attributeValues <- attributeValues |> Map.filter (fun k _ -> k |> attributeNameToIndex |> Option.isSome)
            let variableNameToIndex, variableIndexToName = currentTemplate.Variables |> Array.map (fun a -> a.Name, a.Index) |> fun a -> (a |> Map.ofArray |> fun m x -> m |> Map.tryFind x), (a |> Array.map (fun (x,y) -> (y,x)) |> Map.ofArray |> fun m x -> m |> Map.tryFind x)
            variableValues <- variableValues |> Map.filter (fun k _ -> k |> variableNameToIndex |> Option.isSome)
            let functionDef = currentTemplate.Functions |> Array.find (fun functionDef -> functionDef.Name = name)
            let requiredAttributes = functionDef.AttributeDependencies |> Array.map (fun i -> currentTemplate.Attributes |> Array.find (fun a -> a.Index = i))
            let requiredVariables = functionDef.VariableDependencies |> Array.map (fun i -> currentTemplate.Variables |> Array.find (fun a -> a.Index = i))
            Array.append
                (requiredAttributes |> Array.map Choice1Of2)
                (requiredVariables |> Array.map Choice2Of2)
            |> Array.scan
                (fun (haveSeenFilled, haveSeenEmpty, _) value ->
                    match value with
                    | Choice1Of2 att ->
                        let data =
                            if haveSeenEmpty then
                                // I cannot know if this value is valid, so remove it.
                                attributeValues <- attributeValues |> Map.remove att.Name
                                {
                                    name = att.Name
                                    text = att.Text
                                    value = ""
                                    items = [||]
                                    permitsFreeValue = false
                                }
                            else
                                let attributeValuesByIndex = attributeValues |> Map.toSeq |> Seq.choose (fun (n,v) -> n |> attributeNameToIndex |> Option.map (fun i -> (i,v))) |> Map.ofSeq
                                let suggestions = att.Values |> Array.filter (fun a -> ConditionEvaluator.resolve attributeValuesByIndex a.Condition) |> Array.map (fun a -> a.Value)
                                let value = attributeValuesByIndex |> Map.tryFind att.Index
                                let value =
                                    if value.IsSome && (suggestions |> Array.tryFind ((=) value.Value) |> Option.isNone) then
                                        attributeValues <- attributeValues |> Map.remove att.Name
                                        None
                                    else
                                        value
                                let newValue, newSuggestions =
                                    if value.IsNone && suggestions.Length > 0 then
                                        attributeValues <- attributeValues |> Map.add att.Name suggestions.[0]
                                        suggestions.[0], suggestions
                                    else if value.IsSome then
                                        // Put the current value at the front.
                                        value.Value, (value.Value::(suggestions |> List.ofArray |> List.filter ((<>) value.Value))) |> List.toArray
                                    else
                                        "", [|""|]
                                {
                                    name = att.Name
                                    value = newValue
                                    items = newSuggestions
                                    text = att.Text
                                    permitsFreeValue = false
                                }
                        (haveSeenFilled || data.value <> "", haveSeenEmpty || data.value = "", (Some (Choice1Of2 data)))
                    | Choice2Of2 var ->
                        let data =
                            if haveSeenEmpty && (not var.PermitsFreeValue) then
                                // I cannot know if this value is valid, so remove it.
                                variableValues <- variableValues |> Map.remove var.Name
                                {
                                    name = var.Name
                                    value = ""
                                    items = [|""|]
                                    text = var.Text
                                    permitsFreeValue = var.PermitsFreeValue
                                }
                            else
                                let attributeValuesByIndex = attributeValues |> Map.toSeq |> Seq.choose (fun (n,v) -> n |> attributeNameToIndex |> Option.map (fun i -> (i,v))) |> Map.ofSeq
                                let (currentValue, userChoseThisValue) = variableValues |> Map.tryFind var.Name |> fun o -> if o.IsSome then (Some (o.Value |> fst), o.Value |> snd) else (None, false)
                                let value =
                                    if (not userChoseThisValue) then
                                        variableValues <- variableValues |> Map.remove var.Name
                                        None
                                    else
                                        currentValue
                                let variableValuesByIndex = variableValues |> Map.toSeq |> Seq.choose (fun (n,(v,_)) -> n |> variableNameToIndex |> Option.map (fun i -> (i,v))) |> Map.ofSeq
                                let suggestions =
                                    if haveSeenEmpty then
                                        [||]
                                    else
                                        var.Values
                                        |> Array.filter (fun a -> VariableConditionEvaluator.resolve attributeValuesByIndex variableValuesByIndex a.Condition)
                                        |> Array.map (fun a -> a.Value)
                                // We remove the choice if it is invalid, or if it was system generated.
                                let value =
                                    if value.IsSome && (not var.PermitsFreeValue) && (suggestions |> Array.tryFind ((=) value.Value) |> Option.isNone) then
                                        variableValues <- variableValues |> Map.remove var.Name
                                        None
                                    else
                                        value
                                let newValue, newSuggestions =
                                    if value.IsNone && suggestions.Length > 0 then
                                        variableValues <- variableValues |> Map.add var.Name (suggestions.[0], false) // I chose this one.
                                        suggestions.[0], suggestions
                                    else if value.IsSome then
                                        // Put the current value at the front.
                                        value.Value, (value.Value::(suggestions |> List.ofArray |> List.filter ((<>) value.Value))) |> List.toArray
                                    else
                                        "", [|""|]
                                {
                                    name = var.Name
                                    value = newValue
                                    items = newSuggestions
                                    text = var.Text
                                    permitsFreeValue = var.PermitsFreeValue
                                }
                        (haveSeenFilled || data.value <> "", haveSeenEmpty || data.value = "", (Some (Choice2Of2 data))))
                    (false, false, None)
            |> Array.skip 1
            |> Array.map (fun (_,e,o) -> (e,o.Value))
            |> fun a ->
                let attributes = a |> Array.map snd |> Array.choose (function | Choice1Of2 a -> Some a | _ -> None)
                let variables = a |> Array.map snd |> Array.choose (function | Choice2Of2 a -> Some a | _ -> None)
                let haveSeenEmpty = a |> Array.map fst |> List.ofArray |> List.tryLast |> defaultArg <| false
                (haveSeenEmpty, attributes, variables)
        {
            fileName = file
            functionName = name
            attributes = attributes
            variables = variables
            canGenerate = (not haveSeenEmpty)
            output = if haveSeenEmpty || lastResult.IsNone then [||] else lastResult.Value
        }
    member __.SetValue ty name value =
        if ty = "Variable" then
            variableValues <- variableValues |> Map.add name (value, true) // The user chose this one.
        else
            attributeValues <- attributeValues |> Map.add name value
    member __.Generate (config:GeneratorConfiguration) =
        // OPS Super unsafe.
        let functionDef = currentTemplate.Value.Functions |> Array.find (fun functionDef -> functionDef.Name = name)
        let requiredAttributes = functionDef.AttributeDependencies |> Array.map (fun i -> currentTemplate.Value.Attributes |> Array.find (fun a -> a.Index = i))
        let requiredVariables = functionDef.VariableDependencies |> Array.map (fun i -> currentTemplate.Value.Variables |> Array.find (fun a -> a.Index = i))
        let generatorInput =
            {   RandomSeed = NoSeed
                Config = {  NumSpacesBetweenSentences = config.NumSpacesBetweenSentences
                            NumBlankLinesBetweenParagraphs = config.NumBlankLinesBetweenParagraphs
                            LineEnding = (if config.WindowsLineEndings then CRLF else LF) }
                Attributes  = requiredAttributes |> List.ofArray |> List.map (fun a -> { Name = a.Name ; Value = attributeValues |> Map.find a.Name })
                Variables   = requiredVariables |> List.ofArray |> List.map (fun a -> { Name = a.Name ; Value = variableValues |> Map.find a.Name |> fst })
                Function = name }
        match (Generator.generate generatorInput currentTemplate.Value) with
        | GeneratorError _ -> lastResult <- None
        | GeneratorSuccess r -> lastResult <- Some (r.Text |> Array.map (fun a -> {File = a.InputFile ; LineNumber = a.InputLineNumber ; Value = a.Value ; IsParagraphBreak = a.IsPb }))
