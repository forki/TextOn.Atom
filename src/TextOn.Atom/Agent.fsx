﻿#r "bin/Debug/TextOn.Atom.exe"
open TextOn.Atom
open System
open System.IO
open System.Text.RegularExpressions

type Message<'a,'b> =
    | Quit
    | Connect of ('b -> unit)
    | NewData of 'a
    | Fetch of 'b option AsyncReplyChannel
type Agent<'a,'b> = Message<'a,'b> MailboxProcessor
type EitherOr<'a,'b> =
    | Either of 'a
    | Or of 'b
[<RequireQualifiedAccess>]
module Agent =
    let map (f:'a -> 'b) (node:Agent<_,'a>) =
        let agent =
            Agent.Start
                (fun inbox ->
                    let mutable b = None
                    let mutable listeners = []
                    let rec loop() =
                        async {
                            let! msg = inbox.Receive()
                            match msg with
                            | Quit -> return ()
                            | Connect f ->
                                listeners <- f::listeners
                                return! loop()
                            | NewData a ->
                                b <- Some (f a)
                                listeners
                                |> List.iter
                                    (fun f ->
                                        f b.Value)
                                return! loop()
                            | Fetch(reply) ->
                                reply.Reply(b)
                                return! loop() }
                    loop())
        node.Post(Connect(NewData >> agent.Post))
        agent
    let map2 (f:'a -> 'b -> 'c) (node1:Agent<_,'a>) (node2:Agent<_,'b>) =
        let nodeA = node1 |> map Either
        let nodeB = node2 |> map Or
        let agent =
            Agent.Start
                (fun inbox ->
                    let mutable a = None
                    let mutable b = None
                    let mutable c = None
                    let mutable listeners = []
                    let rec loop() =
                        async {
                            let! msg = inbox.Receive()
                            match msg with
                            | Quit -> return ()
                            | Connect f ->
                                listeners <- f::listeners
                                return! loop()
                            | NewData data ->
                                match data with
                                | Either x ->
                                    a <- Some x
                                    if b.IsSome then
                                        c <- Some (f x b.Value)
                                        listeners |> List.iter (fun f -> f c.Value)
                                | Or x ->
                                    b <- Some x
                                    if a.IsSome then
                                        c <- Some (f a.Value x)
                                        listeners |> List.iter (fun f -> f c.Value)
                                return! loop()
                            | Fetch(reply) ->
                                reply.Reply(c)
                                return! loop() }
                    loop())
        nodeA.Post(Connect(NewData >> agent.Post))
        nodeB.Post(Connect(NewData >> agent.Post))
        agent
    let source() =
        Agent.Start
            (fun inbox ->
                let mutable b = None
                let mutable listeners = []
                let rec loop() =
                    async {
                        let! msg = inbox.Receive()
                        match msg with
                        | Quit -> return ()
                        | Connect f ->
                            listeners <- f::listeners
                            return! loop()
                        | NewData a ->
                            b <- Some a
                            listeners
                            |> List.iter
                                (fun f ->
                                    f b.Value)
                            return! loop()
                        | Fetch(reply) ->
                            reply.Reply(b)
                            return! loop() }
                loop())
    let fetch (agent:Agent<_,_>) =
        agent.PostAndReply(Fetch)
    let iter f (agent:Agent<_,_>) =
        agent.Post(Connect(f))
    let post data (agent:Agent<_,_>) =
        agent.Post(NewData(data))

// Set up pipeline.
let source          = Agent.source()
let preprocessor    = source |> Agent.map (fun (a,b,c:string seq) -> Preprocessor.preprocess Preprocessor.realFileResolver a b c)
let stripper        = preprocessor |> Agent.map CommentStripper.stripComments
let categorizer     = stripper |> Agent.map LineCategorizer.categorize
let tokenizer       = categorizer |> Agent.map (fun s -> s |> Seq.map (fun x -> async { return Tokenizer.tokenize x }) |> Async.Parallel |> Async.RunSynchronously)

// Example data.
let f               = FileInfo(@"D:\NodeJs\TextOn.Atom\examples\example.texton")
let directory       = f.Directory.FullName |> Some
let file            = f.Name
let lines           = f.FullName |> File.ReadAllLines |> Seq.ofArray
source |> Agent.post (file,directory,lines)

// Retrieve result.
let result          =
    tokenizer
    |> Agent.fetch
    |> Option.get
    |> Seq.collect Option.get
    |> Seq.concat

