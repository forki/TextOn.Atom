﻿namespace TextOn.Atom

open System
open System.IO
open Suave
open Suave.Http
open Suave.Operators
open Suave.Web
open Suave.WebPart
open Suave.WebSocket
open Suave.Sockets.Control
open Suave.Filters
open Newtonsoft.Json

type ServerModeConfig =
    {
        [<ArgDescription("The port to listen on.")>]
        [<ArgRange(8100, 8999)>]
        Port : int
    }

[<RequireQualifiedAccess>]
module internal RunServer =
    let run serverModeConfig =
        let mutable client : WebSocket option  = None

        System.Threading.ThreadPool.SetMinThreads(8, 8) |> ignore
        let commands = Commands(JsonSerializer.writeJson)

        let handler f : WebPart = fun (r : HttpContext) -> async {
              let data = r.request |> SuaveUtils.getResourceFromReq
              let! res = Async.Catch (f data)
              match res with
              | Choice1Of2 res ->
                 let res' = res |> List.toArray |> Json.toJson
                 return! Response.response HttpCode.HTTP_200 res' r
              | Choice2Of2 e -> return! Response.response HttpCode.HTTP_500 (Json.toJson e) r
            }

        let app =
            choose [
                path "/parse" >=> handler (fun (data : ParseRequest) -> commands.Parse data.FileName (data.Lines |> List.ofArray))
                path "/lint" >=> handler (fun (data: LintRequest) -> commands.Lint data.FileName)
            ]

        let port = serverModeConfig.Port
        let defaultBinding = defaultConfig.bindings.[0]
        let withPort = { defaultBinding.socketBinding with port = uint16 port }
        let serverConfig =
            { defaultConfig with bindings = [{ defaultBinding with socketBinding = withPort }]}
        startWebServer serverConfig app
        0