﻿namespace TextOn.Atom

open System
open System.IO
open FSharp.Reflection

/// Add a description to an argument.
type ArgDescriptionAttribute(description:string) =
    inherit Attribute()
    member __.Description = description

[<RequireQualifiedAccess>]
module ArgParser =
    type private ArgParserTypeInfo =
        | Required of string * string * Type
        | Optional of string * string * Type
        | OptionalBool of string * string
        | Record of Type * (string * ArgParserTypeInfo)[]
        | Union of Type * (string * ArgParserTypeInfo)[]
        | Invalid
        with
            member private this.Print(i) =
                match this with
                | Required(arg, desc, ty) ->
                    sprintf "%s%s (%A) : %s\n" (String.Join("", [1 .. i] |> List.toArray |> Array.map (fun _ -> " "))) arg ty desc
                | Optional(arg, desc, ty) ->
                    sprintf "%s[optional] %s (%A) : %s\n" (String.Join("", [1 .. i] |> List.toArray |> Array.map (fun _ -> " "))) arg ty desc
                | OptionalBool(arg, desc) ->
                    sprintf "%s[optional] %s (bool) : %s\n" (String.Join("", [1 .. i] |> List.toArray |> Array.map (fun _ -> " "))) arg desc
                | Record(ty, info) ->
                    let s = sprintf "%s{\n" (String.Join("", [1 .. i] |> List.toArray |> Array.map (fun _ -> " ")))
                    let a =
                        info
                        |> Array.fold
                            (fun a (name, info) ->
                                let nameLine = sprintf "%s%s:\n" (String.Join("", [1 .. (i + 2)] |> List.toArray |> Array.map (fun _ -> " "))) name
                                let infoLine = info.Print(i + 4)
                                (a + nameLine + infoLine))
                            ""
                    let e = sprintf "%s}\n" (String.Join("", [1 .. i] |> List.toArray |> Array.map (fun _ -> " ")))
                    s + a + e
                | Union(ty, info) ->
                    info
                    |> Array.fold
                        (fun a (name, info) ->
                            let nameLine = sprintf "%s| %s:\n" (String.Join("", [1 .. i] |> List.toArray |> Array.map (fun _ -> " "))) name
                            let infoLine = info.Print(i + 2)
                            (a + nameLine + infoLine))
                        ""
                | Invalid ->
                    sprintf "%sInvalid\n" (String.Join("", [1 .. i] |> List.toArray |> Array.map (fun _ -> " ")))
            override this.ToString() = this.Print(0)

    type private ArgParserDataInfo =
        | RequiredData of Type * string option
        | OptionalData of Type * string option
        | OptionalBoolData of bool
        | RecordData of Type * ArgParserDataInfo[]
        | UnionData of (UnionCaseInfo * ArgParserDataInfo) option
    let private makeName (s:string) =
        s.ToCharArray()
        |> Array.mapi (fun i c -> if c >= 'A' && c <= 'Z' then (if i = 0 then "" else "-") + (Char.ToLower(c).ToString()) else c.ToString())
        |> fun a -> "--" + String.Join("", a)
    let rec private isFilledIn data =
        match data with
        | RequiredData(_, o) -> o.IsSome
        | RecordData (_, r) ->
            r
            |> Array.tryFind (isFilledIn >> not)
            |> Option.isNone
        | UnionData (o) -> o.IsSome
        | _ -> true
    let rec private getArgs (ty:Type) =
        if (FSharpType.IsUnion ty) then
            FSharpType.GetUnionCases(ty)
            |> Array.map
                (fun caseInfo ->
                    let fields = caseInfo.GetFields()
                    if fields.Length <> 1 then
                        (caseInfo.Name, Invalid)
                    else
                        let field = fields.[0]
                        let ty = field.PropertyType
                        (caseInfo.Name, (getArgs ty)))
            |> fun x ->
                let isInvalid = x |> Array.map snd |> Array.tryFind (function | Invalid -> true | _ -> false) |> Option.isSome
                if isInvalid then Invalid
                else Union(ty, x)
        else if (FSharpType.IsRecord ty) then
            FSharpType.GetRecordFields(ty)
            |> Array.map
                (fun field ->
                    let ty = field.PropertyType
                    let description =
                        field.GetCustomAttributes(typeof<ArgDescriptionAttribute>, false)
                        |> Seq.tryFind (fun _ -> true)
                        |> Option.map (fun a -> (a :?> ArgDescriptionAttribute).Description)
                        |> defaultArg <| field.Name
                    if ty.IsGenericType && ty.GetGenericTypeDefinition() = typedefof<_ option> then
                        let genTy = ty.GetGenericArguments().[0]
                        if genTy = typeof<string> || genTy = typeof<int> || genTy = typeof<double> || genTy = typeof<DateTime> then
                            (field.Name, Optional((makeName field.Name), description, genTy))
                        else if genTy = typeof<bool> then
                            (field.Name, OptionalBool((makeName field.Name), description))
                        else if genTy |> FSharpType.IsUnion then
                            let cases = FSharpType.GetUnionCases genTy
                            let isSimple = cases |> Array.map (fun caseInfo -> caseInfo.GetFields() |> Array.isEmpty) |> Array.tryFind not |> Option.isNone
                            if isSimple then
                                (field.Name, Optional((makeName field.Name), description, genTy))
                            else
                                (field.Name, Invalid)
                        else
                            (field.Name, Invalid)
                    else if ty |> FSharpType.IsRecord then
                        let a = getArgs ty
                        if a = Invalid then (field.Name, Invalid)
                        else (field.Name, a)
                    else if ty |> FSharpType.IsUnion then
                        let cases = FSharpType.GetUnionCases ty
                        let isSimple = cases |> Array.map (fun caseInfo -> caseInfo.GetFields() |> Array.isEmpty) |> Array.tryFind not |> Option.isNone
                        if isSimple then
                            (field.Name, Required((makeName field.Name), description, ty))
                        else
                            (field.Name, getArgs ty)
                    else if ty = typeof<string> || ty = typeof<int> || ty = typeof<double> || ty = typeof<DateTime> || ty = typeof<bool> then
                        (field.Name, Required((makeName field.Name), description, ty))
                    else
                        (field.Name, Invalid))
            |> fun x ->
                let isInvalid = x |> Array.map snd |> Array.tryFind (function | Invalid -> true | _ -> false) |> Option.isSome
                if isInvalid then Invalid
                else Record(ty, x)
        else
            Invalid

    let rec private doParse typeInfo (args:string[]) : (string[] * ArgParserDataInfo) =
        match typeInfo with
        | Required(matchString, _, ty) ->
            let i = args |> Array.tryFindIndex (fun x -> x = matchString)
            if i |> Option.isNone then
                (args, RequiredData(ty, None))
            else if i.Value = args.Length - 1 then
                ((args |> Array.take (args.Length - 1)), RequiredData(ty, None))
            else
                let v = args.[i.Value + 1]
                ((Array.append (args |> Array.take i.Value) (args |> Array.skip (i.Value + 2))), RequiredData(ty, Some v))
        | Optional(matchString, _, ty) ->
            let i = args |> Array.tryFindIndex (fun x -> x = matchString)
            if i |> Option.isNone then
                (args, OptionalData(ty, None))
            else if i.Value = args.Length - 1 then
                ((args |> Array.take (args.Length - 1)), OptionalData(ty, None))
            else
                let v = args.[i.Value + 1]
                ((Array.append (args |> Array.take i.Value) (args |> Array.skip (i.Value + 2))), OptionalData(ty, Some v))
        | OptionalBool(matchString, _) ->
            let i = args |> Array.tryFindIndex (fun x -> x = matchString)
            if i |> Option.isNone then
                (args, OptionalBoolData(false))
            else
                ((Array.append (args |> Array.take i.Value) (args |> Array.skip (i.Value + 1))), OptionalBoolData(true))
        | Record(ty, fields) ->
            let (args, data) =
                fields
                |> Array.fold
                    (fun (args, output) (_, inner) ->
                        let (newArgs, newOutput) = doParse inner args
                        (newArgs, newOutput::output))
                    (args, [])
            (args, RecordData(ty, (data |> List.rev |> List.toArray)))
        | Union(ty, fields) ->
            let data =
                fields
                |> Array.zip (ty |> FSharpType.GetUnionCases)
                |> Array.map
                    (fun (unionCaseInfo, (_, inner)) ->
                        let (newArgs, output) = doParse inner args
                        (unionCaseInfo, newArgs, output))
                |> Array.tryFind (fun (_, _, a) -> isFilledIn a)
            if data.IsSome then
                let (unionCaseInfo, args, data) = data.Value
                (args, UnionData(Some (unionCaseInfo, data)))
            else
                (args, UnionData(None))
        | _ -> failwith "Internal error"

    let private buildSimple ty s =
        if ty = typeof<string> then (box s)
        else if ty = typeof<int> then (box (Int32.Parse s))
        else if ty = typeof<DateTime> then (box (DateTime.Parse s))
        else if ty = typeof<bool> then (box (Boolean.Parse s))
        else if FSharpType.IsUnion ty then
            FSharpType.GetUnionCases(ty)
            |> Array.tryFind (fun case -> case.Name = s)
            |> Option.get
            |> box
        else failwithf "Don't know how to make a %A" ty

    let private buildSimpleOptional ty s =
        if ty = typeof<string> then (box (Some s))
        else if ty = typeof<int> then (box (Some (Int32.Parse s)))
        else if ty = typeof<DateTime> then (box (Some (DateTime.Parse s)))
        else if ty = typeof<bool> then (box (Some (Boolean.Parse s)))
        else if FSharpType.IsUnion ty then
            FSharpType.GetUnionCases(ty)
            |> Array.tryFind (fun case -> case.Name = s)
            |> box
        else failwithf "Don't know how to make a %A" ty

    let rec private buildType data =
        match data with
        | RequiredData(ty, s) ->
            buildSimple ty s.Value
        | OptionalData(ty, s) ->
            if s.IsNone then box None
            else buildSimpleOptional ty s.Value
        | OptionalBoolData(v) ->
            if v then (box (Some(true))) else box None
        | RecordData(ty, data) ->
            FSharpValue.MakeRecord(ty, data |> Array.map buildType)
        | UnionData(o) ->
            let (case, data) = o.Value
            FSharpValue.MakeUnion(case, [|(buildType data)|])

    /// Parse command line arguments into a record.
    let parse<'r>(args) =
        let info = getArgs (typeof<'r>)
        match info with
        | Invalid ->
            failwith "Not a valid ArgParser type"
        | _ ->
            let help = args |> Array.tryFind (fun x -> x = "--help")
            if help.IsSome then
                eprintfn "Usage:"
                eprintfn "%s" (info.ToString())
                None
            else
                let (args, data) = doParse info args
                if (not (isFilledIn data)) then
                    eprintfn "%s" (info.ToString())
                    None
                else if args |> Array.isEmpty |> not then
                    eprintfn "%s" (info.ToString())
                    eprintfn "Extra args: %A" args
                    None
                else
                    (Some (unbox<'r>(buildType data)))
