﻿namespace TextOn.Atom

open System
open System.IO

module internal Main =
    let private printUsage() =
        printfn "TextOn.Atom.exe --template <filename>"
    [<EntryPoint>]
    let main argv =
        if argv.Length = 0 || argv.[0] = "--help" then
            printUsage()
            0
        else if argv.Length <> 2 || argv.[0] <> "--template" then
            eprintfn "Invalid arguments"
            printUsage()
            1
        else if argv.[1] |> File.Exists |> not then
            eprintfn "File not found: %s" argv.[1]
            printUsage()
            1
        else
            let f = FileInfo argv.[1]
            let compilationResult =
                Preprocessor.preprocess Preprocessor.realFileResolver f.FullName (Some f.Directory.FullName) (f.FullName |> File.ReadAllLines |> List.ofArray)
                |> CommentStripper.stripComments
                |> LineCategorizer.categorize
                |> List.map (Tokenizer.tokenize >> Parser.parse)
                |> List.toArray
                |> Compiler.compile
            match compilationResult with
            | CompilationFailure errors ->
                errors
                |> Array.iter
                    (function
                        | GeneralError error ->
                            eprintfn "%s" error
                        | ParserError error ->
                            eprintfn "%s at %s line %d (character %d)" error.ErrorText error.File error.LineNumber error.StartLocation)
                1
            | CompilationSuccess template ->
                printfn ""
                let attributeValues =
                    template.Attributes
                    |> Array.fold
                        (fun m att ->
                            let values =
                                att.Values
                                |> Array.filter (fun a -> ConditionEvaluator.resolve m a.Condition)
                                |> Array.map (fun a -> a.Value)
                            if values |> Array.isEmpty then failwith "Invalid values for attributes"
                            else if values.Length = 1 then m |> Map.add att.Index values.[0]
                            else
                                let possibilities = String.Join(", ", (values |> Array.truncate 5))
                                printfn "Provide value for %s (possible values: %s)" att.Name possibilities
                                let mutable value = ""
                                while (values |> Array.contains value |> not) do
                                    value <- Console.ReadLine()
                                m |> Map.add att.Index value)
                        Map.empty
                let variableValues =
                    template.Variables
                    |> Array.fold
                        (fun m att ->
                            let values =
                                att.Values
                                |> Array.filter (fun a -> VariableConditionEvaluator.resolve attributeValues m a.Condition)
                                |> Array.map (fun a -> a.Value)
                            if (not att.PermitsFreeValue && values |> Array.isEmpty) then failwith "Invalid values for variables"
                            else if (not att.PermitsFreeValue && values.Length = 1) then m |> Map.add att.Index values.[0]
                            else
                                if values.Length = 0 then
                                    printfn "[%s] %s" att.Name att.Text
                                else
                                    let possibilities = String.Join(", ", (values |> Array.truncate 5))
                                    printfn "[%s] %s (suggested values: %s)" att.Name att.Text possibilities
                                let mutable value = ""
                                let mutable validValue = false
                                while (not validValue) do
                                    value <- Console.ReadLine()
                                    if att.PermitsFreeValue then validValue <- true
                                    else validValue <- (values |> Array.contains value)
                                m |> Map.add att.Index value)
                        Map.empty
                let generatorInput = {
                    RandomSeed = NoSeed
                    Config =
                        {   NumSpacesBetweenSentences = 2
                            NumBlankLinesBetweenParagraphs = 1
                            LineEnding = CRLF }
                    Attributes  =
                        template.Attributes
                        |> List.ofArray
                        |> List.map (fun att -> { Name = att.Name ; Value = attributeValues.[att.Index] })
                    Variables =
                        template.Variables
                        |> List.ofArray
                        |> List.map (fun att -> { Name = att.Name ; Value = variableValues.[att.Index] }) }
                Generator.generate generatorInput template
                |> function | GeneratorSuccess output -> output.Text | _ -> failwith ""
                |> Seq.map (fun t -> t.Value)
                |> Seq.fold (+) ""
                |> printfn "%s"
                0 // return an integer exit code
